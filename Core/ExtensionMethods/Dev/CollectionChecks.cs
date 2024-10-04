using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

using ConditionalAttribute = System.Diagnostics.ConditionalAttribute;

namespace Chisel
{
	internal static class CollectionChecks
	{
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal unsafe static void CheckPtr(void* ptr)
		{
			if (ptr == null)
				throw new ArgumentNullException("ptr");
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void CheckPositive(int length)
		{
			if (length < 0)
			{
				throw new ArgumentOutOfRangeException("length", $"length ({length}) needs to be a positive number");
			}
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckRangeInRange(int index, int indexLength, int length)
		{
			if (index < 0)
			{
				throw new IndexOutOfRangeException($"Start of range ({index}) must be positive");
			}

			if (indexLength < 0)
			{
				throw new ArgumentException($"Range length ({indexLength}) must be positive");
			}

			int last = index + indexLength;
			if (last > length)
			{
				throw new ArgumentOutOfRangeException($"End of range ({last - 1}) is out of range of '{length}' Length.");
			}

			if (last < 0)
			{
				throw new ArgumentException($"The range {index}-{last - 1} caused an integer overflow and is outside the range of the collection 0-{length - 1}");
			}
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void CheckLengthInRange(int value, int length)
		{
			if (value < 0)
				throw new IndexOutOfRangeException($"Value {value} must be positive.");
			if ((uint)value > (uint)length)
				throw new IndexOutOfRangeException($"Value {value} is out of range of '{length}' Length.");
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void CheckIndexInRange(int index, int length)
		{
			if (index < 0)
				throw new IndexOutOfRangeException($"Index {index} must be positive.");
			if ((uint)index > (uint)length)
				throw new IndexOutOfRangeException($"Index {index} is out of range of '{length}' Length.");
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckWriteAndThrow<T>(NativeArray<T> array) where T : unmanaged
		{
			AtomicSafetyHandle.CheckWriteAndThrow(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(array));
			CheckValidCollection(array);
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckWriteAndThrow(ref NativeStream.Writer writer)
		{
			// Can't actually check anything
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckWriteAndThrow<T>(NativeSlice<T> slice) where T : unmanaged
		{
			AtomicSafetyHandle.CheckWriteAndThrow(NativeSliceUnsafeUtility.GetAtomicSafetyHandle(slice));
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckWriteAndThrow<T>(NativeList<T> list) where T : unmanaged
		{
			AtomicSafetyHandle.CheckWriteAndThrow(NativeListUnsafeUtility.GetAtomicSafetyHandle(ref list));
			CheckValidCollection(list);
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckWriteAndThrow<T>(NativeList<T>.ParallelWriter writer) where T : unmanaged
		{
			// Can't actually check m_Safety
			CheckValidCollection(writer);
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckWriteAndThrow<T>(UnsafeList<T> list) where T : unmanaged
		{
			// Can't actually check any rights
			CheckValidCollection(list);
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckWriteAndThrow<T>(UnsafeList<T>.ParallelWriter writer) where T : unmanaged
		{
			// Can't actually check m_Safety
			CheckValidCollection(writer);
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckWriteAndThrow<T>(ref BlobArray<T> array) where T : unmanaged
		{
			// Can't actually check any rights
			CheckValidCollection(ref array);
		}

		internal static void CheckWriteAndThrow<T>(T[] array) where T : unmanaged
		{
			CheckValidCollection(array);
		}

		internal static void CheckWriteAndThrow<T>(List<T> list) where T : unmanaged
		{
			CheckValidCollection(list);
		}

		internal static void CheckWriteAndThrow<T>(HashSet<T> list) where T : unmanaged
		{
			CheckValidCollection(list);
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckReadAndThrow<T>(NativeArray<T> array) where T : unmanaged
		{
			AtomicSafetyHandle.CheckReadAndThrow(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(array));
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckReadAndThrow(NativeStream.Reader reader)
		{
			// Can't actually check any rights here
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckReadAndThrow<T>(NativeArray<T>.ReadOnly array) where T : unmanaged
		{
			// Can't actually check any rights
			CheckValidCollection(array);
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckReadAndThrow<T>(NativeList<T> list) where T : unmanaged
		{
			AtomicSafetyHandle.CheckReadAndThrow(NativeListUnsafeUtility.GetAtomicSafetyHandle(ref list));
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckReadAndThrow<T>(NativeSlice<T> slice) where T : unmanaged
		{
			AtomicSafetyHandle.CheckReadAndThrow(NativeSliceUnsafeUtility.GetAtomicSafetyHandle(slice));
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckReadAndThrow<T>(UnsafeList<T> list) where T : unmanaged
		{
			// Can't actually check any rights
			CheckValidCollection(list);
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckReadAndThrow<T>(UnsafeList<T>.ReadOnly list) where T : unmanaged
		{
			// Can't actually check any rights
			CheckValidCollection(list);
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckReadAndThrow<T>(UnsafeList<T>.ParallelReader list) where T : unmanaged
		{
			// Can't actually check any rights
			CheckValidCollection(list);
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckReadAndThrow<T>(ref BlobArray<T> array) where T : unmanaged
		{
			// Can't actually check any rights
			CheckValidCollection(ref array);
		}

		internal static void CheckReadAndThrow<T>(T[] array) where T : unmanaged
		{
			CheckValidCollection(array);
		}

		internal static void CheckReadAndThrow<T>(List<T> list) where T : unmanaged
		{
			CheckValidCollection(list);
		}

		internal static void CheckReadAndThrow<T>(HashSet<T> list) where T : unmanaged
		{
			CheckValidCollection(list);
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckValidCollection<T>(in NativeArray<T> array) where T : unmanaged
		{
			if (!array.IsCreated)
				throw new NullReferenceException("Using an uninitialized NativeArray");
			var safetyHandle = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(array);
			AtomicSafetyHandle.ValidateNonDefaultHandle(safetyHandle);
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckValidCollection<T>(in NativeArray<T>.ReadOnly array) where T : unmanaged
		{
			if (!array.IsCreated)
				throw new NullReferenceException("Using an uninitialized NativeArray.ReadOnly");
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckValidCollection<T>(NativeList<T> list) where T : unmanaged
		{
			if (!list.IsCreated)
				throw new NullReferenceException("Using an uninitialized NativeList");
			var safetyHandle = NativeListUnsafeUtility.GetAtomicSafetyHandle(ref list);
			AtomicSafetyHandle.ValidateNonDefaultHandle(safetyHandle);
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckValidCollection<T>(in NativeSlice<T> slice) where T : unmanaged
		{
			var safetyHandle = NativeSliceUnsafeUtility.GetAtomicSafetyHandle(slice);
			AtomicSafetyHandle.ValidateNonDefaultHandle(safetyHandle);
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckValidCollection<T>(in UnsafeList<T> list) where T : unmanaged
		{
			unsafe
			{
				if (!list.IsCreated || list.Ptr == null)
					throw new NullReferenceException("Using an uninitialized UnsafeList");
			}
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckValidCollection<T>(in UnsafeList<T>.ReadOnly list) where T : unmanaged
		{
			unsafe
			{
				if (list.Ptr == null)
					throw new NullReferenceException("Using an uninitialized UnsafeList.ReadOnly");
			}
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckValidCollection<T>(in UnsafeList<T>.ParallelReader list) where T : unmanaged
		{
			unsafe
			{
				if (list.Ptr == null)
					throw new NullReferenceException("Using an uninitialized UnsafeList.ParallelReader");
			}
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckValidCollection<T>(in NativeList<T>.ParallelWriter writer) where T : unmanaged
		{
			unsafe
			{
				if (writer.Ptr == null || !(*writer.ListData).IsCreated || (*writer.ListData).Ptr == null)
					throw new NullReferenceException("Using an uninitialized NativeList<>.ParallelWriter");
			}
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckValidCollection<T>(in UnsafeList<T>.ParallelWriter writer) where T : unmanaged
		{
			unsafe
			{
				if (writer.Ptr == null || !(*writer.ListData).IsCreated || (*writer.ListData).Ptr == null)
					throw new NullReferenceException("Using an uninitialized UnsafeList<>.ParallelWriter");
			}
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckValidCollection<T>(ref BlobArray<T> blobArray) where T : unmanaged
		{
			unsafe
			{
				if (blobArray.GetUnsafePtr() == null)
					throw new NullReferenceException("Using an uninitialized BlobArray");
			}
		}

		internal static void CheckValidCollection<T>(T[] array) where T : unmanaged
		{
			if (array == null)
				throw new NullReferenceException("Using an uninitialized Array");
		}

		internal static void CheckValidCollection<T>(List<T> list) where T : unmanaged
		{
			if (list == null)
				throw new NullReferenceException("Using an uninitialized List<>");
		}

		internal static void CheckValidCollection<T>(HashSet<T> set) where T : unmanaged
		{
			if (set == null)
				throw new NullReferenceException("Using an uninitialized HashSet<>");
		}


		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckCopyArguments(int srcLength, int srcIndex, int dstLength, int dstIndex, int length)
		{
			if (length < 0)
			{
				throw new ArgumentOutOfRangeException("length", "length must be equal or greater than zero.");
			}

			if (srcIndex < 0 || srcIndex > srcLength || (srcIndex == srcLength && srcLength > 0))
			{
				throw new ArgumentOutOfRangeException("srcIndex", "srcIndex is outside the range of valid indexes for the source NativeArray.");
			}

			if (dstIndex < 0 || dstIndex > dstLength || (dstIndex == dstLength && dstLength > 0))
			{
				throw new ArgumentOutOfRangeException("dstIndex", "dstIndex is outside the range of valid indexes for the destination NativeArray.");
			}

			if (srcIndex + length > srcLength)
			{
				throw new ArgumentException("length is greater than the number of elements from srcIndex to the end of the source NativeArray.", "length");
			}

			if (srcIndex + length < 0)
			{
				throw new ArgumentException("srcIndex + length causes an integer overflow");
			}

			if (dstIndex + length > dstLength)
			{
				throw new ArgumentException("length is greater than the number of elements from dstIndex to the end of the destination NativeArray.", "length");
			}

			if (dstIndex + length < 0)
			{
				throw new ArgumentException("dstIndex + length causes an integer overflow");
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static unsafe void CheckMoveArguments(int dstIndex, int srcIndex, int moveLength, int arrayLength)
		{
			if (dstIndex < 0)
				throw new ArgumentOutOfRangeException($"{nameof(dstIndex)} must be positive.", nameof(dstIndex));
			if (srcIndex < 0)
				throw new ArgumentOutOfRangeException($"{nameof(srcIndex)} must be positive.", nameof(srcIndex));
			if (moveLength < 0)
				throw new ArgumentOutOfRangeException($"{nameof(moveLength)} must be positive.", nameof(moveLength));
			if (dstIndex + moveLength > arrayLength)
				throw new ArgumentOutOfRangeException($"{nameof(dstIndex)} + {nameof(moveLength)} must be within bounds of array ({arrayLength}).", nameof(moveLength));
			if (srcIndex + moveLength > arrayLength)
				throw new ArgumentOutOfRangeException($"{nameof(srcIndex)} + {nameof(moveLength)} must be within bounds of array ({arrayLength}).", nameof(moveLength));
		}


		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		internal static void CheckCopyLengths(int srcLength, int dstLength)
		{
			if (srcLength != dstLength)
			{
				throw new ArgumentException("source and destination length must be the same");
			}
		}
	}
}