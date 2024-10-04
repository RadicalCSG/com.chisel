using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

using ConditionalAttribute = System.Diagnostics.ConditionalAttribute;

namespace Chisel
{
	public static class NativeCollectionExtensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearValues<T>(this NativeArray<T> dst) where T : unmanaged
		{
			unsafe
			{
				CollectionChecks.CheckWriteAndThrow(dst);
				if (dst.Length == 0) return;
				UnsafeUtility.MemSet(dst.GetUnsafePtr(), 0, dst.Length * sizeof(T));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearValues<T>(ref this NativeSlice<T> dst) where T : unmanaged
		{
			unsafe
			{
				CollectionChecks.CheckWriteAndThrow(dst);
				if (dst.Length == 0) return;
				UnsafeUtility.MemSet(dst.GetUnsafePtr(), 0, dst.Length * sizeof(T));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearValues<T>(ref this NativeList<T> dst) where T : unmanaged
		{
			unsafe
			{
				CollectionChecks.CheckWriteAndThrow(dst);
				if (dst.Length == 0) return;
				UnsafeUtility.MemSet(dst.GetUnsafePtr(), 0, dst.Length * sizeof(T));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearValues<T>(ref this UnsafeList<T> dst) where T : unmanaged
		{
			unsafe
			{
				CollectionChecks.CheckWriteAndThrow(dst);
				if (dst.Length == 0) return;
				UnsafeUtility.MemSet(dst.Ptr, 0, dst.Length * sizeof(T));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearValues<T>(ref BlobArray<T> dst) where T : unmanaged
		{
			unsafe
			{
				CollectionChecks.CheckWriteAndThrow(ref dst);
				if (dst.Length == 0) return;
				UnsafeUtility.MemSet(dst.GetUnsafePtr(), 0, dst.Length * sizeof(T));
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearValues<T>(this NativeArray<T> dst, int length) where T : unmanaged
		{
			unsafe
			{
				CollectionChecks.CheckWriteAndThrow(dst);
				CollectionChecks.CheckLengthInRange(length, dst.Length);
				if (length <= 0) return;
				UnsafeUtility.MemSet(dst.GetUnsafePtr(), 0, math.min(length, dst.Length) * sizeof(T));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearValues<T>(ref this NativeSlice<T> dst, int length) where T : unmanaged
		{
			unsafe
			{
				CollectionChecks.CheckWriteAndThrow(dst);
				CollectionChecks.CheckLengthInRange(length, dst.Length);
				if (length <= 0) return;
				UnsafeUtility.MemSet(dst.GetUnsafePtr(), 0, math.min(length, dst.Length) * sizeof(T));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearValues<T>(ref this NativeList<T> dst, int length) where T : unmanaged
		{
			unsafe
			{
				CollectionChecks.CheckWriteAndThrow(dst);
				CollectionChecks.CheckLengthInRange(length, dst.Length);
				if (length <= 0) return;
				UnsafeUtility.MemSet(dst.GetUnsafePtr(), 0, math.min(length, dst.Length) * sizeof(T));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearValues<T>(ref this UnsafeList<T> dst, int length) where T : unmanaged
		{
			unsafe
			{
				CollectionChecks.CheckWriteAndThrow(dst);
				CollectionChecks.CheckLengthInRange(length, dst.Length);
				if (length <= 0) return;
				UnsafeUtility.MemSet(dst.Ptr, 0, math.min(length, dst.Length) * sizeof(T));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearValues<T>(ref BlobArray<T> dst, int length) where T : unmanaged
		{
			unsafe
			{
				CollectionChecks.CheckWriteAndThrow(ref dst);
				CollectionChecks.CheckLengthInRange(length, dst.Length);
				if (length <= 0) return;
				UnsafeUtility.MemSet(dst.GetUnsafePtr(), 0, math.min(length, dst.Length) * sizeof(T));
			}
		}



		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearValues<T>(this NativeArray<T> dst, int offset, int length) where T : unmanaged
		{
			unsafe
			{
				CollectionChecks.CheckWriteAndThrow(dst);
				CollectionChecks.CheckRangeInRange(offset, length, dst.Length);
				if (length <= 0) return;
				UnsafeUtility.MemSet((T*)dst.GetUnsafePtr() + offset, 0, math.min(length, dst.Length) * sizeof(T));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearValues<T>(ref this NativeSlice<T> dst, int offset, int length) where T : unmanaged
		{
			unsafe
			{
				CollectionChecks.CheckWriteAndThrow(dst);
				CollectionChecks.CheckRangeInRange(offset, length, dst.Length);
				if (length <= 0) return;
				UnsafeUtility.MemSet((T*)dst.GetUnsafePtr() + offset, 0, math.min(length, dst.Length) * sizeof(T));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearValues<T>(ref this NativeList<T> dst, int offset, int length) where T : unmanaged
		{
			unsafe
			{
				CollectionChecks.CheckWriteAndThrow(dst);
				CollectionChecks.CheckRangeInRange(offset, length, dst.Length);
				if (length <= 0) return;
				UnsafeUtility.MemSet(dst.GetUnsafePtr() + offset, 0, math.min(length, dst.Length) * sizeof(T));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearValues<T>(ref this UnsafeList<T> dst, int offset, int length) where T : unmanaged
		{
			unsafe
			{
				CollectionChecks.CheckWriteAndThrow(dst);
				CollectionChecks.CheckRangeInRange(offset, length, dst.Length);
				if (length <= 0) return;
				UnsafeUtility.MemSet(dst.Ptr + offset, 0, math.min(length, dst.Length) * sizeof(T));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearValues<T>(ref BlobArray<T> dst, int offset, int length) where T : unmanaged
		{
			unsafe
			{
				CollectionChecks.CheckWriteAndThrow(ref dst);
				CollectionChecks.CheckRangeInRange(offset, length, dst.Length);
				if (length <= 0) return;
				UnsafeUtility.MemSet((T*)dst.GetUnsafePtr() + offset, 0, math.min(length, dst.Length) * sizeof(T));
			}
		}



		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe uint Hash<T>([ReadOnly] ref this T input) where T : unmanaged
		{
			fixed (void* ptr = &input) { return math.hash((byte*)ptr, sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] this NativeArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			unsafe { return src.Length == 0 ? 0 : math.hash(src.GetUnsafeReadOnlyPtr(), src.Length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] this NativeArray<T>.ReadOnly src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			unsafe { return src.Length == 0 ? 0 : math.hash(src.GetUnsafeReadOnlyPtr(), src.Length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] this NativeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return 0;
			unsafe { return math.hash(src.GetUnsafeReadOnlyPtr(), src.Length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] this NativeSlice<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return 0;
			unsafe { return math.hash(src.GetUnsafeReadOnlyPtr(), src.Length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] in this UnsafeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return 0;
			unsafe { return math.hash(src.Ptr, src.Length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] in this UnsafeList<T>.ReadOnly src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return 0;
			unsafe { return math.hash(src.Ptr, src.Length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] ref this BlobArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(ref src);
			if (src.Length == 0) return 0;
			unsafe { return math.hash(src.GetUnsafePtr(), src.Length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>(this T[] src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return 0;
			unsafe { fixed (T* ptr = src) { return math.hash(ptr, src.Length * sizeof(T)); } }
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] this NativeArray<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckPositive(length);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return 0;
			unsafe { return math.hash(src.GetUnsafeReadOnlyPtr(), length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] this NativeArray<T>.ReadOnly src, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckPositive(length);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return 0;
			unsafe { return math.hash(src.GetUnsafeReadOnlyPtr(), length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] this NativeSlice<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckPositive(length);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return 0;
			unsafe { return math.hash(src.GetUnsafeReadOnlyPtr(), length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] this NativeList<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckPositive(length);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return 0;
			unsafe { return math.hash(src.GetUnsafeReadOnlyPtr(), length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] in this UnsafeList<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckPositive(length);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return 0;
			unsafe { return math.hash(src.Ptr, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] ref this BlobArray<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckPositive(length);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return 0;
			unsafe { return math.hash(src.GetUnsafePtr(), length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] this T[] src, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckPositive(length);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return 0;
			unsafe { fixed (T* ptr = src) { return math.hash(ptr, length * sizeof(T)); } }
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] this NativeArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckPositive(length);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return 0;
			unsafe { return math.hash((T*)src.GetUnsafeReadOnlyPtr() + index, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] this NativeSlice<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckPositive(length);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return 0;
			unsafe { return math.hash((T*)src.GetUnsafeReadOnlyPtr() + index, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] this NativeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckPositive(length);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return 0;
			unsafe { return math.hash(src.GetUnsafeReadOnlyPtr() + index, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] in this UnsafeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckPositive(length);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return 0;
			unsafe { return math.hash(src.Ptr + index, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] in this UnsafeList<T>.ReadOnly src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckPositive(length);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return 0;
			unsafe { return math.hash(src.Ptr + index, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>([ReadOnly] ref this BlobArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckPositive(length);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return 0;
			unsafe { return math.hash((T*)src.GetUnsafePtr() + index, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash<T>(this T[] src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckPositive(length);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return 0;
			unsafe { fixed (T* ptr = src) { return math.hash(ptr + index, length * sizeof(T)); } }
		}



		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Contains<T, U>(this T[] src, U value) where T : unmanaged, IEquatable<U>
		{
			return IndexOf<T, U>(src, value) != -1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Contains<T, U>([ReadOnly] this NativeArray<T> src, U value) where T : unmanaged, IEquatable<U>
		{
			return IndexOf<T, U>(src, value) != -1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Contains<T, U>([ReadOnly] this NativeArray<T>.ReadOnly src, U value) where T : unmanaged, IEquatable<U>
		{
			return IndexOf<T, U>(src, value) != -1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Contains<T, U>([ReadOnly] this NativeSlice<T> src, U value) where T : unmanaged, IEquatable<U>
		{
			return IndexOf<T, U>(src, value) != -1;
		}

		/*
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Contains<T, U>([ReadOnly] this NativeList<T> src, U value) where T : unmanaged, IEquatable<U>
		{
			return IndexOf<T, U>(src, value) != -1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Contains<T, U>([ReadOnly] this UnsafeList<T> src, U value) where T : unmanaged, IEquatable<U>
		{
			return IndexOf<T, U>(src, value) != -1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Contains<T, U>([ReadOnly] this UnsafeList<T>.ReadOnly src, U value) where T : unmanaged, IEquatable<U>
		{
			return IndexOf<T, U>(src, value) != -1;
		}

		*/

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Contains<T, U>([ReadOnly] ref this BlobArray<T> src, U value) where T : unmanaged, IEquatable<U>
		{
			return IndexOf<T, U>(ref src, value) != -1;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int IndexOf<T, U>(this T[] src, U value) where T : unmanaged, IEquatable<U>
		{
			CollectionChecks.CheckReadAndThrow(src);
			unsafe { fixed (T* ptr = src) { return NativeArrayExtensions.IndexOf<T, U>(ptr, src.Length, value); } }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int IndexOf<T, U>([ReadOnly] this NativeArray<T> src, U value) where T : unmanaged, IEquatable<U>
		{
			CollectionChecks.CheckReadAndThrow(src);
			unsafe { return NativeArrayExtensions.IndexOf<T, U>(src.GetUnsafeReadOnlyPtr(), src.Length, value); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int IndexOf<T, U>([ReadOnly] this NativeArray<T>.ReadOnly src, U value) where T : unmanaged, IEquatable<U>
		{
			CollectionChecks.CheckReadAndThrow(src);
			unsafe { return NativeArrayExtensions.IndexOf<T, U>(src.GetUnsafeReadOnlyPtr(), src.Length, value); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int IndexOf<T, U>([ReadOnly] this NativeSlice<T> src, U value) where T : unmanaged, IEquatable<U>
		{
			CollectionChecks.CheckReadAndThrow(src);
			unsafe { return NativeArrayExtensions.IndexOf<T, U>(src.GetUnsafeReadOnlyPtr(), src.Length, value); }
		}
		/*
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int IndexOf<T, U>([ReadOnly] this NativeList<T> src, U value) where T : unmanaged, IEquatable<U>
		{
			CollectionChecks.CheckReadAndThrow(src);
			unsafe { return NativeArrayExtensions.IndexOf<T, U>(src.GetUnsafeReadOnlyPtr(), src.Length, value); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int IndexOf<T, U>([ReadOnly] this UnsafeList<T> src, U value) where T : unmanaged, IEquatable<U>
		{
			CollectionChecks.CheckReadAndThrow(src);
			unsafe { return NativeArrayExtensions.IndexOf<T, U>(src.Ptr, src.Length, value); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int IndexOf<T, U>([ReadOnly] this UnsafeList<T>.ReadOnly src, U value) where T : unmanaged, IEquatable<U>
		{
			CollectionChecks.CheckReadAndThrow(src);
			unsafe { return NativeArrayExtensions.IndexOf<T, U>(src.Ptr, src.Length, value); }
		}
		
		*/

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int IndexOf<T, U>([ReadOnly] ref this BlobArray<T> src, U value) where T : unmanaged, IEquatable<U>
		{
			CollectionChecks.CheckReadAndThrow(ref src);
			unsafe { return NativeArrayExtensions.IndexOf<T, U>(src.GetUnsafePtr(), src.Length, value); }
		}


		public static NativeArray<T> AsArray<T>([ReadOnly] this NativeArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			var arraySafety = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(src);
			AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(arraySafety);
			AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif
			unsafe
			{
				var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(((T*)src.GetUnsafePtr()) + index, length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);
#endif
				return array;
			}
		}

		public static NativeArray<T> AsArray<T>([ReadOnly] ref this NativeSlice<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			var arraySafety = NativeSliceUnsafeUtility.GetAtomicSafetyHandle(src);
			AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(arraySafety);
			AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif
			unsafe
			{
				var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>((T*)src.GetUnsafePtr() + index, length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);
#endif
				return array;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static NativeArray<T> AsArray<T>([ReadOnly] ref this NativeSlice<T> src) where T : unmanaged
		{
			return AsArray<T>(ref src, 0, src.Length);
		}

		public static NativeArray<T> AsArray<T>([ReadOnly] ref this NativeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			var arraySafety = NativeListUnsafeUtility.GetAtomicSafetyHandle(ref src);
			AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(arraySafety);
			AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif
			unsafe
			{
				var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(src.GetUnsafePtr() + index, length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);
#endif
				return array;
			}
		}

		public static NativeArray<T> AsArray<T>(this T[] src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			unsafe { fixed (T* ptr = src) { return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr + index, length, Allocator.None); } }
		}

		public static NativeArray<T>.ReadOnly AsReadOnlyArray<T>([ReadOnly] this NativeArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			var arraySafety = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(src);
			AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(arraySafety);
			AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif
			unsafe
			{
				var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(((T*)src.GetUnsafeReadOnlyPtr()) + index, length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);
#endif
				return array.AsReadOnly();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static NativeArray<T>.ReadOnly AsReadOnlyArray<T>([ReadOnly] ref this NativeSlice<T> src) where T : unmanaged
		{
			return AsReadOnlyArray(ref src, 0, src.Length);
		}

		public static NativeArray<T>.ReadOnly AsReadOnlyArray<T>([ReadOnly] ref this NativeSlice<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			var arraySafety = NativeSliceUnsafeUtility.GetAtomicSafetyHandle(src);
			AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(arraySafety);
			AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif
			unsafe
			{
				var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>((T*)src.GetUnsafeReadOnlyPtr() + index, length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);
#endif
				return array.AsReadOnly();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static NativeArray<T>.ReadOnly AsReadOnlyArray<T>([ReadOnly] ref this NativeList<T> src) where T : unmanaged
		{
			return AsReadOnlyArray(ref src, 0, src.Length);
		}

		public static NativeArray<T>.ReadOnly AsReadOnlyArray<T>([ReadOnly] ref this NativeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			var arraySafety = NativeListUnsafeUtility.GetAtomicSafetyHandle(ref src);
			AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(arraySafety);
			AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif
			unsafe
			{
				var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(src.GetUnsafeReadOnlyPtr() + index, length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);
#endif
				return array.AsReadOnly();
			}
		}

		public static NativeArray<T> ToNativeArray<T>(this List<T> src, Allocator allocator) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.AsSpan<T>(list) + fixed to get ptr
			var nativeList = new NativeArray<T>(src.Count, allocator);
			for (int i = 0; i < src.Count; i++)
				nativeList[i] = src[i];
			return nativeList;
		}

		public static NativeArray<T> ToNativeArray<T>(this HashSet<T> src, Allocator allocator) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.AsSpan<T>(list) + fixed to get ptr
			var nativeList = new NativeArray<T>(src.Count, allocator);
			int i = 0;
			foreach (var item in src)
			{
				nativeList[i] = item;
				i++;
			}
			return nativeList;
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static NativeArray<T> ToNativeArray<T>(this T[] src, Allocator allocator) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			return new NativeArray<T>(src, allocator);
		}




		public static NativeList<T> ToNativeList<T>(this List<T> src, Allocator allocator) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.AsSpan<T>(list) + fixed to get ptr
			var nativeList = new NativeList<T>(src.Count, allocator);
			for (int i = 0; i < src.Count; i++)
				nativeList.AddNoResize(src[i]);
			return nativeList;
		}
		public static NativeList<T> ToNativeList<T>(this HashSet<T> src, Allocator allocator) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.AsSpan<T>(list) + fixed to get ptr
			var nativeList = new NativeList<T>(src.Count, allocator);
			foreach (var item in src)
				nativeList.AddNoResize(item);
			return nativeList;
		}

		public static NativeList<T> ToNativeList<T>(this T[] src, Allocator allocator) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.AsSpan<T>(list) + fixed to get ptr
			var nativeList = new NativeList<T>(src.Length, allocator);
			for (int i = 0; i < src.Length; i++)
				nativeList.AddNoResize(src[i]);
			return nativeList;
		}



		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo<T>([ReadOnly] this NativeArray<T> src, T[] dst, int length) where T : unmanaged { CopyTo(src, 0, dst, 0, length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo<T>([ReadOnly] ref this NativeSlice<T> src, T[] dst, int length) where T : unmanaged { CopyTo(ref src, 0, dst, 0, length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo<T>([ReadOnly] ref this NativeList<T> src, T[] dst, int length) where T : unmanaged { CopyTo(ref src, 0, dst, 0, length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo<T>([ReadOnly] in this UnsafeList<T> src, T[] dst, int length) where T : unmanaged { CopyTo(in src, 0, dst, 0, length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo<T>([ReadOnly] ref this BlobArray<T> src, T[] dst, int length) where T : unmanaged { CopyTo(ref src, 0, dst, 0, length); }


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo<T>([ReadOnly] this NativeArray<T> src, T[] dst, int dstOffset, int length) where T : unmanaged { CopyTo(src, 0, dst, dstOffset, length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo<T>([ReadOnly] ref this NativeSlice<T> src, T[] dst, int dstOffset, int length) where T : unmanaged { CopyTo(ref src, 0, dst, dstOffset, length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo<T>([ReadOnly] ref this NativeList<T> src, T[] dst, int dstOffset, int length) where T : unmanaged { CopyTo(ref src, 0, dst, dstOffset, length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo<T>([ReadOnly] in this UnsafeList<T> src, T[] dst, int dstOffset, int length) where T : unmanaged { CopyTo(in src, 0, dst, dstOffset, length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo<T>([ReadOnly] ref this BlobArray<T> src, T[] dst, int dstOffset, int length) where T : unmanaged { CopyTo(ref src, 0, dst, dstOffset, length); }



		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo<T>([ReadOnly] this NativeArray<T> src, int srcOffset, T[] dst, int dstOffset, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyArguments(src.Length, srcOffset, dst.Length, dstOffset, length);
			if (length <= 0) return;
			unsafe
			{
				fixed (T* dstPtr = &dst[0])
				{
					UnsafeUtility.MemCpy(dstPtr + dstOffset, (T*)src.GetUnsafeReadOnlyPtr() + srcOffset, length * sizeof(T));
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo<T>([ReadOnly] ref this NativeSlice<T> src, int srcOffset, T[] dst, int dstOffset, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyArguments(src.Length, srcOffset, dst.Length, dstOffset, length);
			if (length <= 0) return;
			unsafe
			{
				fixed (T* dstPtr = &dst[0])
				{
					UnsafeUtility.MemCpy(dstPtr + dstOffset, (T*)src.GetUnsafePtr() + srcOffset, length * sizeof(T));
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo<T>([ReadOnly] ref this NativeList<T> src, int srcOffset, T[] dst, int dstOffset, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyArguments(src.Length, srcOffset, dst.Length, dstOffset, length);
			if (length <= 0) return;
			unsafe
			{
				fixed (T* dstPtr = &dst[0])
				{
					UnsafeUtility.MemCpy(dstPtr + dstOffset, src.GetUnsafePtr() + srcOffset, length * sizeof(T));
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo<T>([ReadOnly] in this UnsafeList<T> src, int srcOffset, T[] dst, int dstOffset, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyArguments(src.Length, srcOffset, dst.Length, dstOffset, length);
			if (length <= 0) return;
			unsafe
			{
				fixed (T* dstPtr = &dst[0])
				{
					UnsafeUtility.MemCpy(dstPtr + dstOffset, src.Ptr + srcOffset, length * sizeof(T));
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo<T>([ReadOnly] ref this BlobArray<T> src, int srcOffset, T[] dst, int dstOffset, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckCopyArguments(src.Length, srcOffset, dst.Length, dstOffset, length);
			if (length <= 0) return;
			unsafe
			{
				fixed (T* dstPtr = &dst[0])
				{
					UnsafeUtility.MemCpy(dstPtr + dstOffset, (T*)src.GetUnsafePtr() + srcOffset, length * sizeof(T));
				}
			}
		}





		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, [ReadOnly] NativeArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyLengths(dst.Length, src.Length);
			dst.CopyFrom(0, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, [ReadOnly] NativeArray<T>.ReadOnly src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyLengths(dst.Length, src.Length);
			dst.CopyFrom(0, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, [ReadOnly] NativeSlice<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyLengths(dst.Length, src.Length);
			dst.CopyFrom(0, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, [ReadOnly] NativeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyLengths(dst.Length, src.Length);
			dst.CopyFrom(0, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, [ReadOnly] UnsafeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyLengths(dst.Length, src.Length);
			dst.CopyFrom(0, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, [ReadOnly] ref BlobArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckCopyLengths(dst.Length, src.Length);
			dst.CopyFrom(0, ref src, 0, src.Length);
		}




		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeList<T> dst, [ReadOnly] NativeArray<T>.ReadOnly src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyLengths(dst.Length, src.Length);
			dst.CopyFrom(src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeList<T> dst, [ReadOnly] ref BlobArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckCopyLengths(dst.Length, src.Length);
			dst.CopyFrom(ref src, 0, src.Length);
		}




		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this UnsafeList<T> dst, [ReadOnly] NativeSlice<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyLengths(dst.Length, src.Length);
			dst.CopyFrom(src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this UnsafeList<T> dst, [ReadOnly] NativeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyLengths(dst.Length, src.Length);
			dst.CopyFrom(src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this UnsafeList<T> dst, [ReadOnly] NativeArray<T>.ReadOnly src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyLengths(dst.Length, src.Length);
			dst.CopyFrom(src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this UnsafeList<T> dst, [ReadOnly] ref BlobArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckCopyLengths(dst.Length, src.Length);
			dst.CopyFrom(ref src, 0, src.Length);
		}



		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, T[] src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyArguments(src.Length, srcIndex, dst.Length, 0, length);
			if (length <= 0) return;
			unsafe { fixed (T* srcPtr = &src[0]) { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr(), srcPtr + srcIndex, length * sizeof(T)); } }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, [ReadOnly] NativeArray<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyArguments(src.Length, srcIndex, dst.Length, 0, length);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr(), (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, [ReadOnly] NativeArray<T>.ReadOnly src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyArguments(src.Length, srcIndex, dst.Length, 0, length);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr(), (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, [ReadOnly] NativeSlice<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyArguments(src.Length, srcIndex, dst.Length, 0, length);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr(), (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, [ReadOnly] NativeList<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyArguments(src.Length, srcIndex, dst.Length, 0, length);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr(), (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, [ReadOnly] UnsafeList<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyArguments(src.Length, srcIndex, dst.Length, 0, length);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr(), (T*)src.Ptr + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, [ReadOnly] ref BlobArray<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckCopyArguments(src.Length, srcIndex, dst.Length, 0, length);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr(), (T*)src.GetUnsafePtr() + srcIndex, length * sizeof(T)); }
		}




		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, int dstIndex, T[] src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
			if (length <= 0) return;
			unsafe { fixed (T* srcPtr = &src[0]) { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr() + dstIndex, srcPtr + srcIndex, length * sizeof(T)); } }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, int dstIndex, [ReadOnly] NativeArray<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr() + dstIndex, (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, int dstIndex, [ReadOnly] NativeArray<T>.ReadOnly src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr() + dstIndex, (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, int dstIndex, [ReadOnly] NativeSlice<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr() + dstIndex, (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, int dstIndex, [ReadOnly] NativeList<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr() + dstIndex, (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, int dstIndex, [ReadOnly] UnsafeList<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr() + dstIndex, (T*)src.Ptr + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeArray<T> dst, int dstIndex, [ReadOnly] ref BlobArray<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr() + dstIndex, (T*)src.GetUnsafePtr() + srcIndex, length * sizeof(T)); }
		}





		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeList<T> dst, T[] src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			dst.Resize(length, NativeArrayOptions.UninitializedMemory);
			if (length <= 0) return;
			unsafe { fixed (T* srcPtr = &src[0]) { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr(), srcPtr + srcIndex, length * sizeof(T)); } }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeList<T> dst, [ReadOnly] NativeArray<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			dst.Resize(length, NativeArrayOptions.UninitializedMemory);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr(), (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeList<T> dst, [ReadOnly] NativeArray<T>.ReadOnly src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			dst.Resize(length, NativeArrayOptions.UninitializedMemory);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr(), (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeList<T> dst, [ReadOnly] NativeSlice<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			dst.Resize(length, NativeArrayOptions.UninitializedMemory);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr(), (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeList<T> dst, [ReadOnly] NativeList<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			dst.Resize(length, NativeArrayOptions.UninitializedMemory);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr(), (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeList<T> dst, [ReadOnly] UnsafeList<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			dst.Resize(length, NativeArrayOptions.UninitializedMemory);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr(), (T*)src.Ptr + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this NativeList<T> dst, [ReadOnly] ref BlobArray<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			dst.Resize(length, NativeArrayOptions.UninitializedMemory);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy((T*)dst.GetUnsafePtr(), (T*)src.GetUnsafePtr() + srcIndex, length * sizeof(T)); }
		}




		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this UnsafeList<T> dst, T[] src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			dst.Resize(length, NativeArrayOptions.UninitializedMemory);
			if (length <= 0) return;
			unsafe { fixed (T* srcPtr = &src[0]) { UnsafeUtility.MemCpy(dst.Ptr, srcPtr + srcIndex, length * sizeof(T)); } }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this UnsafeList<T> dst, [ReadOnly] NativeArray<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			dst.Resize(length, NativeArrayOptions.UninitializedMemory);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy(dst.Ptr, (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this UnsafeList<T> dst, [ReadOnly] NativeArray<T>.ReadOnly src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			dst.Resize(length, NativeArrayOptions.UninitializedMemory);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy(dst.Ptr, (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this UnsafeList<T> dst, [ReadOnly] NativeSlice<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			dst.Resize(length, NativeArrayOptions.UninitializedMemory);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy(dst.Ptr, (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this UnsafeList<T> dst, [ReadOnly] NativeList<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			dst.Resize(length, NativeArrayOptions.UninitializedMemory);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy(dst.Ptr, (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this UnsafeList<T> dst, [ReadOnly] UnsafeList<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			dst.Resize(length, NativeArrayOptions.UninitializedMemory);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy(dst.Ptr, (T*)src.Ptr + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyFrom<T>(this UnsafeList<T> dst, [ReadOnly] ref BlobArray<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			dst.Resize(length, NativeArrayOptions.UninitializedMemory);
			if (length <= 0) return;
			unsafe { UnsafeUtility.MemCpy(dst.Ptr, (T*)src.GetUnsafePtr() + srcIndex, length * sizeof(T)); }
		}





		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T2[] ToArray<T1,T2>([ReadOnly] in this UnsafeList<T1> src) 
			where T1 : unmanaged 
			where T2 : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return Array.Empty<T2>();
			var array = new T2[src.Length];
			unsafe
			{
				if (sizeof(T1) != sizeof(T2)) throw new InvalidOperationException();
				fixed (T2* dstPtr = &array[0])
				{
					var srcPtr = src.Ptr;
					var byteCount = src.Length * sizeof(T2); // can have overflow
					CollectionChecks.CheckPositive(byteCount);

					UnsafeUtility.MemCpy(dstPtr, srcPtr, byteCount);
				}
			}
			return array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T2[] ToArray<T1, T2>([ReadOnly] ref this BlobArray<T1> src)
			where T1 : unmanaged
			where T2 : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(ref src);
			if (src.Length == 0) return Array.Empty<T2>();
			var array = new T2[src.Length];
			unsafe
			{
				fixed (T2* dstPtr = &array[0])
				{
					var srcPtr = src.GetUnsafePtr();
					var byteCount = src.Length * sizeof(T2); // can have overflow
					CollectionChecks.CheckPositive(byteCount);
					UnsafeUtility.MemCpy(dstPtr, srcPtr, byteCount);
				}
			}
			return array;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T[] ToArray<T>([ReadOnly] in this UnsafeList<T> src) where T : unmanaged { return ToArray<T, T>(src); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T[] ToArray<T>([ReadOnly] ref this BlobArray<T> src) where T : unmanaged { return ToArray<T,T>(ref src); }


		public static void AddRange<T>(this List<T> dst, [ReadOnly] NativeArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.SetCount<T>(list, list.Count + collection.Length);
			for (int i = 0; i < src.Length; i++) dst.Add(src[i]);
		}

		public static void AddRange<T>(this List<T> dst, [ReadOnly] NativeSlice<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.SetCount<T>(list, list.Count + collection.Length);
			for (int i = 0; i < src.Length; i++) dst.Add(src[i]);
		}

		public static void AddRange<T>(this List<T> dst, [ReadOnly] NativeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.SetCount<T>(list, list.Count + collection.Length);
			for (int i = 0; i < src.Length; i++) dst.Add(src[i]);
		}

		public static void AddRange<T>(this List<T> dst, [ReadOnly] in UnsafeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.SetCount<T>(list, list.Count + collection.Length);
			for (int i = 0; i < src.Length; i++) dst.Add(src[i]);
		}

		public static void AddRange<T>(this List<T> dst, [ReadOnly] ref BlobArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			if (src.Length == 0) return;
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.SetCount<T>(list, list.Count + collection.Length);
			for (int i = 0; i < src.Length; i++) dst.Add(src[i]);
		}



		public static void AddRange<T>(this List<T> dst, [ReadOnly] NativeArray<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.SetCount<T>(list, list.Count + collection.Length);
			for (int i = 0; i < length; i++) dst.Add(src[i]);
		}

		public static void AddRange<T>(this List<T> dst, [ReadOnly] NativeSlice<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.SetCount<T>(list, list.Count + collection.Length);
			for (int i = 0; i < length; i++) dst.Add(src[i]);
		}

		public static void AddRange<T>(this List<T> dst, [ReadOnly] NativeList<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.SetCount<T>(list, list.Count + collection.Length);
			for (int i = 0; i < length; i++) dst.Add(src[i]);
		}

		public static void AddRange<T>(this List<T> dst, [ReadOnly] in UnsafeList<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.SetCount<T>(list, list.Count + collection.Length);
			for (int i = 0; i < length; i++) dst.Add(src[i]);
		}

		public static void AddRange<T>(this List<T> dst, [ReadOnly] ref BlobArray<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.SetCount<T>(list, list.Count + collection.Length);
			for (int i = 0; i < length; i++) dst.Add(src[i]);
		}






		public static void AddRange<T>(this List<T> dst, [ReadOnly] NativeArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.SetCount<T>(list, list.Count + collection.Length);
			for (int i = 0; i < length; i++) dst.Add(src[i + index]);
		}

		public static void AddRange<T>(this List<T> dst, [ReadOnly] NativeSlice<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.SetCount<T>(list, list.Count + collection.Length);
			for (int i = 0; i < length; i++) dst.Add(src[i + index]);
		}

		public static void AddRange<T>(this List<T> dst, NativeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.SetCount<T>(list, list.Count + collection.Length);
			for (int i = 0; i < length; i++) dst.Add(src[i + index]);
		}

		public static void AddRange<T>(this List<T> dst, [ReadOnly] in UnsafeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.SetCount<T>(list, list.Count + collection.Length);
			for (int i = 0; i < length; i++) dst.Add(src[i + index]);
		}

		public static void AddRange<T>(this List<T> dst, [ReadOnly] ref BlobArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			// TODO: once Unity supports .NET 8.0 API use this instead:
			//System.Runtime.InteropServices.CollectionsMarshal.SetCount<T>(list, list.Count + collection.Length);
			for (int i = 0; i < length; i++) dst.Add(src[i + index]);
		}



		public static void AddRange<T>(ref this NativeList<T> dst, [ReadOnly] NativeSlice<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRange(src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static void AddRange<T>(ref this NativeList<T> dst, [ReadOnly] NativeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRange(src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static void AddRange<T>(ref this NativeList<T> dst, [ReadOnly] UnsafeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRange(src.Ptr, src.Length); }
		}

		public static void AddRange<T>(ref this NativeList<T> dst, [ReadOnly] ref BlobArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			if (src.Length == 0) return;
			unsafe { dst.AddRange(src.GetUnsafePtr(), src.Length); }
		}

		public static void AddRange<T>(ref this NativeList<T> dst, ref T[] src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRange(ptr, src.Length); } }
		}

		public static void AddRange<T>(ref this NativeList<T> dst, [ReadOnly] NativeArray<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRange<T>(ref this NativeList<T> dst, [ReadOnly] NativeSlice<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRange<T>(ref this NativeList<T> dst, [ReadOnly] NativeList<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRange<T>(ref this NativeList<T> dst, [ReadOnly] UnsafeList<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange(src.Ptr, length); }
		}

		public static void AddRange<T>(ref this NativeList<T> dst, [ReadOnly] ref BlobArray<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange(src.GetUnsafePtr(), length); }
		}

		public static void AddRange<T>(ref this NativeList<T> dst, T[] src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRange(ptr, length); } }
		}



		public static void AddRange<T>(ref this NativeList<T> dst, [ReadOnly] NativeArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange((T*)src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRange<T>(ref this NativeList<T> dst, [ReadOnly] NativeSlice<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange((T*)src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRange<T>(ref this NativeList<T> dst, [ReadOnly] NativeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange(src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRange<T>(ref this NativeList<T> dst, [ReadOnly] UnsafeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange(src.Ptr + index, length); }
		}

		public static void AddRange<T>(ref this NativeList<T> dst, [ReadOnly] ref BlobArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange((T*)src.GetUnsafePtr() + index, length); }
		}

		public static void AddRange<T>(ref this NativeList<T> dst, T[] src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRange(ptr + index, length); } }
		}


		public static void AddRange<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRange(src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static void AddRange<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeSlice<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRange(src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static void AddRange<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRange(src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static void AddRange<T>(ref this UnsafeList<T> dst, [ReadOnly] UnsafeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRange(src.Ptr, src.Length); }
		}

		public static void AddRange<T>(ref this UnsafeList<T> dst, [ReadOnly] ref BlobArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			if (src.Length == 0) return;
			unsafe { dst.AddRange(src.GetUnsafePtr(), src.Length); }
		}

		public static void AddRange<T>(ref this UnsafeList<T> dst, T[] src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRange(ptr, src.Length); } }
		}





		public static void AddRange<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeArray<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRange<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeSlice<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRange<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeList<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRange<T>(ref this UnsafeList<T> dst, [ReadOnly] UnsafeList<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange(src.Ptr, length); }
		}

		public static void AddRange<T>(ref this UnsafeList<T> dst, [ReadOnly] ref BlobArray<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange(src.GetUnsafePtr(), length); }
		}

		public static void AddRange<T>(ref this UnsafeList<T> dst, T[] src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRange(ptr, length); } }
		}



		public static void AddRange<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange((T*)src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRange<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeSlice<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange((T*)src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRange<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange(src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRange<T>(ref this UnsafeList<T> dst, [ReadOnly] UnsafeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange(src.Ptr + index, length); }
		}

		public static void AddRange<T>(ref this UnsafeList<T> dst, [ReadOnly] ref BlobArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRange((T*)src.GetUnsafePtr() + index, length); }
		}

		public static void AddRange<T>(ref this UnsafeList<T> dst, T[] src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRange(ptr + index, length); } }
		}




		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, [ReadOnly] NativeSlice<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, [ReadOnly] NativeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, [ReadOnly] UnsafeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.Ptr, src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, [ReadOnly] ref BlobArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafePtr(), src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, ref T[] src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRangeNoResize(ptr, src.Length); } }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, [ReadOnly] NativeArray<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, [ReadOnly] NativeSlice<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, [ReadOnly] NativeList<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, [ReadOnly] UnsafeList<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.Ptr, length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, [ReadOnly] ref BlobArray<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafePtr(), length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, T[] src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRangeNoResize(ptr, length); } }
		}



		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, [ReadOnly] NativeArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize((T*)src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, [ReadOnly] NativeSlice<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize((T*)src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, [ReadOnly] NativeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, [ReadOnly] UnsafeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.Ptr + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, [ReadOnly] ref BlobArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize((T*)src.GetUnsafePtr() + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T> dst, T[] src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRangeNoResize(ptr + index, length); } }
		}




		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, [ReadOnly] NativeSlice<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, [ReadOnly] NativeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, [ReadOnly] UnsafeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.Ptr, src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, [ReadOnly] ref BlobArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafePtr(), src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, [ReadOnly] ref T[] src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRangeNoResize(ptr, src.Length); } }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, [ReadOnly] NativeArray<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, [ReadOnly] NativeSlice<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, [ReadOnly] NativeList<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, [ReadOnly] UnsafeList<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.Ptr, length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, [ReadOnly] ref BlobArray<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafePtr(), length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, T[] src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRangeNoResize(ptr, length); } }
		}



		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, [ReadOnly] NativeArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize((T*)src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, [ReadOnly] NativeSlice<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize((T*)src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, [ReadOnly] NativeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, [ReadOnly] UnsafeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.Ptr + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, [ReadOnly] ref BlobArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize((T*)src.GetUnsafePtr() + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this NativeList<T>.ParallelWriter dst, T[] src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRangeNoResize(ptr + index, length); } }
		}




		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeSlice<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, [ReadOnly] UnsafeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.Ptr, src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, [ReadOnly] ref BlobArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafePtr(), src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, T[] src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRangeNoResize(ptr, src.Length); } }
		}





		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeArray<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeSlice<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeList<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, [ReadOnly] UnsafeList<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.Ptr, length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, [ReadOnly] ref BlobArray<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafePtr(), length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, T[] src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRangeNoResize(ptr, length); } }
		}



		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize((T*)src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeSlice<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize((T*)src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, [ReadOnly] NativeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, [ReadOnly] UnsafeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.Ptr + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, [ReadOnly] ref BlobArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize((T*)src.GetUnsafePtr() + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T> dst, T[] src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRangeNoResize(ptr + index, length); } }
		}


		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, [ReadOnly] NativeSlice<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, [ReadOnly] NativeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, [ReadOnly] UnsafeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.Ptr, src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, [ReadOnly] ref BlobArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			if (src.Length == 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafePtr(), src.Length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, ref T[] src) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			if (src.Length == 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRangeNoResize(ptr, src.Length); } }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, [ReadOnly] NativeArray<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, [ReadOnly] NativeSlice<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, [ReadOnly] NativeList<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr(), length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, [ReadOnly] UnsafeList<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.Ptr, length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, [ReadOnly] ref BlobArray<T> src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafePtr(), length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, T[] src, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(length, src.Length);
			if (length <= 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRangeNoResize(ptr, length); } }
		}



		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, [ReadOnly] NativeArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize((T*)src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, [ReadOnly] NativeSlice<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize((T*)src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, [ReadOnly] NativeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.GetUnsafeReadOnlyPtr() + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, [ReadOnly] UnsafeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize(src.Ptr + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, [ReadOnly] ref BlobArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { dst.AddRangeNoResize((T*)src.GetUnsafePtr() + index, length); }
		}

		public static void AddRangeNoResize<T>(ref this UnsafeList<T>.ParallelWriter dst, T[] src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			if (length <= 0) return;
			unsafe { fixed (T* ptr = src) { dst.AddRangeNoResize(ptr + index, length); } }
		}



		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Remove<T, U>(this NativeList<T> dst, U item) where T : unmanaged, IEquatable<U>
		{
			int index = dst.IndexOf(item);
			if (index == -1) return false; 
			dst.RemoveAt(index);
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Remove<T, U>(this UnsafeList<T> dst, U item) where T : unmanaged, IEquatable<U>
		{
			int index = dst.IndexOf(item);
			if (index == -1) return false;
			dst.RemoveAt(index);
			return true;
		}




		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void RemoveRange<T>(this NativeArray<T> dst, int index, int removeLength, ref int arrayLength) where T : unmanaged
		{
			CollectionChecks.CheckValidCollection(dst);
			CollectionChecks.CheckRangeInRange(index, removeLength, arrayLength);
			if (removeLength == 0) return;
			if (index >= 0 && index + removeLength < arrayLength)
			{
				dst.MemMove(index, index + removeLength, arrayLength - (index + removeLength));
			}
			arrayLength -= removeLength;
		}



		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void MemMove<T>(this NativeArray<T> dst, int dstIndex, int srcIndex, int moveLength) where T : unmanaged
		{
			CollectionChecks.CheckValidCollection(dst);
			CollectionChecks.CheckMoveArguments(dstIndex, srcIndex, moveLength, dst.Length);
			if (moveLength == 0 || dstIndex == srcIndex) return;
			var dataPtr = ((T*)dst.GetUnsafePtr());
			UnsafeUtility.MemMove(dataPtr + dstIndex, dataPtr + srcIndex, moveLength * sizeof(T));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void MemMove<T>(this NativeList<T> dst, int dstIndex, int srcIndex, int moveLength) where T : unmanaged
		{
			CollectionChecks.CheckValidCollection(dst);
			CollectionChecks.CheckMoveArguments(dstIndex, srcIndex, moveLength, dst.Length);
			if (moveLength == 0 || dstIndex == srcIndex) return;
			var dataPtr = dst.GetUnsafePtr();
			UnsafeUtility.MemMove(dataPtr + dstIndex, dataPtr + srcIndex, moveLength * sizeof(T));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void MemMove<T>(this NativeSlice<T> dst, int dstIndex, int srcIndex, int moveLength) where T : unmanaged
		{
			CollectionChecks.CheckValidCollection(dst);
			CollectionChecks.CheckMoveArguments(dstIndex, srcIndex, moveLength, dst.Length);
			if (moveLength == 0 || dstIndex == srcIndex) return;
			var dataPtr = (T*)dst.GetUnsafePtr();
			UnsafeUtility.MemMove(dataPtr + dstIndex, dataPtr + srcIndex, moveLength * sizeof(T));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void MemMove<T>(this UnsafeList<T> dst, int dstIndex, int srcIndex, int moveLength) where T : unmanaged
		{
			CollectionChecks.CheckValidCollection(dst);
			CollectionChecks.CheckMoveArguments(dstIndex, srcIndex, moveLength, dst.Length);
			if (moveLength == 0 || dstIndex == srcIndex) return;
			var dataPtr = dst.Ptr;
			UnsafeUtility.MemMove(dataPtr + dstIndex, dataPtr + srcIndex, moveLength * sizeof(T));
		}




		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertAt<T>(this NativeList<T> dst, int index, T item) where T : unmanaged
		{
			dst.InsertRange(index, 1);
			dst[index] = item;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(this NativeList<T> dst, int index, T[] src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			dst.InsertRangeAt(index, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(this NativeList<T> dst, int index, [ReadOnly] NativeArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			dst.InsertRangeAt(index, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(this NativeList<T> dst, int index, [ReadOnly] NativeArray<T>.ReadOnly src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			dst.InsertRangeAt(index, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(this NativeList<T> dst, int index, [ReadOnly] NativeSlice<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			dst.InsertRangeAt(index, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(this NativeList<T> dst, int index, [ReadOnly] NativeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			dst.InsertRangeAt(index, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(this NativeList<T> dst, int index, [ReadOnly] UnsafeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			dst.InsertRangeAt(index, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(this NativeList<T> dst, int index, [ReadOnly] UnsafeList<T>.ReadOnly src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			dst.InsertRangeAt(index, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(this NativeList<T> dst, int index, [ReadOnly] ref BlobArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(ref src);
			dst.InsertRangeAt(index, ref src, 0, src.Length);
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(this NativeList<T> dst, int dstIndex, T[] src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			if (length == 0) return;
			dst.InsertRange(dstIndex, length);
			unsafe { fixed (T* srcPtr = src) { UnsafeUtility.MemCpy(dst.GetUnsafePtr() + dstIndex, srcPtr + srcIndex, length * sizeof(T)); } }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(this NativeList<T> dst, int dstIndex, [ReadOnly] NativeArray<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			if (length == 0) return;
			dst.InsertRange(dstIndex, length);
			unsafe { UnsafeUtility.MemCpy(dst.GetUnsafePtr() + dstIndex, (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(this NativeList<T> dst, int dstIndex, [ReadOnly] NativeArray<T>.ReadOnly src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			if (length == 0) return;
			dst.InsertRange(dstIndex, length);
			unsafe { UnsafeUtility.MemCpy(dst.GetUnsafePtr() + dstIndex, (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(this NativeList<T> dst, int dstIndex, [ReadOnly] NativeSlice<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			if (length == 0) return;
			dst.InsertRange(dstIndex, length);
			unsafe { UnsafeUtility.MemCpy(dst.GetUnsafePtr() + dstIndex, (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(this NativeList<T> dst, int dstIndex, [ReadOnly] NativeList<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			if (length == 0) return;
			dst.InsertRange(dstIndex, length);
			unsafe { UnsafeUtility.MemCpy(dst.GetUnsafePtr() + dstIndex, src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(this NativeList<T> dst, int dstIndex, [ReadOnly] UnsafeList<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			if (length == 0) return;
			dst.InsertRange(dstIndex, length);
			unsafe { UnsafeUtility.MemCpy(dst.GetUnsafePtr() + dstIndex, (T*)src.Ptr + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(this NativeList<T> dst, int dstIndex, [ReadOnly] UnsafeList<T>.ReadOnly src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			if (length == 0) return;
			dst.InsertRange(dstIndex, length);
			unsafe { UnsafeUtility.MemCpy(dst.GetUnsafePtr() + dstIndex, (T*)src.Ptr + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(this NativeList<T> dst, int dstIndex, [ReadOnly] ref BlobArray<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			if (length == 0) return;
			dst.InsertRange(dstIndex, length);
			unsafe { UnsafeUtility.MemCpy(dst.GetUnsafePtr() + dstIndex, (T*)src.GetUnsafePtr() + srcIndex, length * sizeof(T)); }
		}




		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertAt<T>(ref this UnsafeList<T> dst, int index, T item) where T : unmanaged
		{
			dst.InsertRange(index, 1);
			dst[index] = item;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(ref this UnsafeList<T> dst, int index, T[] src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			dst.InsertRangeAt(index, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(ref this UnsafeList<T> dst, int index, [ReadOnly] NativeArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			dst.InsertRangeAt(index, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(ref this UnsafeList<T> dst, int index, [ReadOnly] NativeArray<T>.ReadOnly src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			dst.InsertRangeAt(index, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(ref this UnsafeList<T> dst, int index, [ReadOnly] NativeSlice<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			dst.InsertRangeAt(index, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(ref this UnsafeList<T> dst, int index, [ReadOnly] NativeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			dst.InsertRangeAt(index, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(ref this UnsafeList<T> dst, int index, [ReadOnly] UnsafeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			dst.InsertRangeAt(index, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(ref this UnsafeList<T> dst, int index, [ReadOnly] UnsafeList<T>.ReadOnly src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			dst.InsertRangeAt(index, src, 0, src.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(ref this UnsafeList<T> dst, int index, [ReadOnly] ref BlobArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(ref src);
			dst.InsertRangeAt(index, ref src, 0, src.Length);
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(ref this UnsafeList<T> dst, int dstIndex, T[] src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			if (length == 0) return;
			dst.InsertRange(dstIndex, length);
			unsafe { fixed (T* srcPtr = src) { UnsafeUtility.MemCpy(dst.Ptr + dstIndex, srcPtr + srcIndex, length * sizeof(T)); } }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(ref this UnsafeList<T> dst, int dstIndex, [ReadOnly] NativeArray<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			if (length == 0) return;
			dst.InsertRange(dstIndex, length);
			unsafe { UnsafeUtility.MemCpy(dst.Ptr + dstIndex, (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(ref this UnsafeList<T> dst, int dstIndex, [ReadOnly] NativeArray<T>.ReadOnly src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			if (length == 0) return;
			dst.InsertRange(dstIndex, length);
			unsafe { UnsafeUtility.MemCpy(dst.Ptr + dstIndex, (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(ref this UnsafeList<T> dst, int dstIndex, [ReadOnly] NativeSlice<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			if (length == 0) return;
			dst.InsertRange(dstIndex, length);
			unsafe { UnsafeUtility.MemCpy(dst.Ptr + dstIndex, (T*)src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(ref this UnsafeList<T> dst, int dstIndex, [ReadOnly] NativeList<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			if (length == 0) return;
			dst.InsertRange(dstIndex, length);
			unsafe { UnsafeUtility.MemCpy(dst.Ptr + dstIndex, src.GetUnsafeReadOnlyPtr() + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(ref this UnsafeList<T> dst, int dstIndex, [ReadOnly] UnsafeList<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			if (length == 0) return;
			dst.InsertRange(dstIndex, length);
			unsafe { UnsafeUtility.MemCpy(dst.Ptr + dstIndex, (T*)src.Ptr + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(ref this UnsafeList<T> dst, int dstIndex, [ReadOnly] UnsafeList<T>.ReadOnly src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			if (length == 0) return;
			dst.InsertRange(dstIndex, length);
			unsafe { UnsafeUtility.MemCpy(dst.Ptr + dstIndex, (T*)src.Ptr + srcIndex, length * sizeof(T)); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InsertRangeAt<T>(ref this UnsafeList<T> dst, int dstIndex, [ReadOnly] ref BlobArray<T> src, int srcIndex, int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckRangeInRange(srcIndex, length, src.Length);
			if (length == 0) return;
			dst.InsertRange(dstIndex, length);
			unsafe { UnsafeUtility.MemCpy(dst.Ptr + dstIndex, (T*)src.GetUnsafePtr() + srcIndex, length * sizeof(T)); }
		}

	}
}