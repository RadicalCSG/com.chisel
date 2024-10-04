using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel
{
    public static class BlobBuilderExtensions
	{   
		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] NativeArray<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			unsafe { return builder.Construct(ref dst, (T*)src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] NativeArray<T> src, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(srcLength, src.Length);
			unsafe { return builder.Construct(ref dst, (T*)src.GetUnsafeReadOnlyPtr(), srcLength); }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] NativeArray<T> src, int srcOffset, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcOffset, srcLength, src.Length);
			unsafe { return builder.Construct(ref dst, ((T*)src.GetUnsafeReadOnlyPtr()) + srcOffset, srcLength); }
		}



		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] NativeArray<T>.ReadOnly src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			unsafe { return builder.Construct(ref dst, (T*)src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] NativeArray<T>.ReadOnly src, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(srcLength, src.Length);
			unsafe { return builder.Construct(ref dst, (T*)src.GetUnsafeReadOnlyPtr(), srcLength); }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] NativeArray<T>.ReadOnly src, int srcOffset, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcOffset, srcLength, src.Length);
			unsafe { return builder.Construct(ref dst, ((T*)src.GetUnsafeReadOnlyPtr()) + srcOffset, srcLength); }
		}



		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] NativeSlice<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			unsafe { return builder.Construct(ref dst, (T*)src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] NativeSlice<T> src, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(srcLength, src.Length);
			unsafe { return builder.Construct(ref dst, (T*)src.GetUnsafeReadOnlyPtr(), srcLength); }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] NativeSlice<T> src, int srcOffset, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcOffset, srcLength, src.Length);
			unsafe { return builder.Construct(ref dst, ((T*)src.GetUnsafeReadOnlyPtr()) + srcOffset, srcLength); }
		}



		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] NativeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			unsafe { return builder.Construct(ref dst, (T*)src.GetUnsafeReadOnlyPtr(), src.Length); }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] NativeList<T> src, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(srcLength, src.Length);
			unsafe { return builder.Construct(ref dst, (T*)src.GetUnsafeReadOnlyPtr(), srcLength); }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] NativeList<T> src, int srcOffset, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcOffset, srcLength, src.Length);
			unsafe { return builder.Construct(ref dst, ((T*)src.GetUnsafeReadOnlyPtr()) + srcOffset, srcLength); }
		}



		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] UnsafeList<T> src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			unsafe { return builder.Construct(ref dst, src.Ptr, src.Length); }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] UnsafeList<T> src, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(srcLength, src.Length);
			unsafe { return builder.Construct(ref dst, src.Ptr, srcLength); }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] UnsafeList<T> src, int srcOffset, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcOffset, srcLength, src.Length);
			unsafe { return builder.Construct(ref dst, src.Ptr + srcOffset, srcLength); }
		}



		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] UnsafeList<T>.ReadOnly src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			unsafe { return builder.Construct(ref dst, (T*)src.Ptr, src.Length); }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] UnsafeList<T>.ReadOnly src, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(srcLength, src.Length);
			unsafe { return builder.Construct(ref dst, (T*)src.Ptr, srcLength); }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] UnsafeList<T>.ReadOnly src, int srcOffset, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcOffset, srcLength, src.Length);
			unsafe { return builder.Construct(ref dst, ((T*)src.Ptr) + srcOffset, srcLength); }
		}



		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] UnsafeList<T>.ParallelReader src) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			unsafe { return builder.Construct(ref dst, (T*)src.Ptr, src.Length); }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] UnsafeList<T>.ParallelReader src, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckLengthInRange(srcLength, src.Length);
			unsafe { return builder.Construct(ref dst, (T*)src.Ptr, srcLength); }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] UnsafeList<T>.ParallelReader src, int srcOffset, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(srcOffset, srcLength, src.Length);
			unsafe { return builder.Construct(ref dst, ((T*)src.Ptr) + srcOffset, srcLength); }
		}


		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] ref BlobArray<T> src) where T : unmanaged
		{
			unsafe { return builder.Construct(ref dst, (T*)src.GetUnsafePtr(), src.Length); }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] ref BlobArray<T> src, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckLengthInRange(srcLength, src.Length);
			unsafe { return builder.Construct(ref dst, (T*)src.GetUnsafePtr(), srcLength); }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] ref BlobArray<T> src, int srcOffset, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckRangeInRange(srcOffset, srcLength, src.Length);
            unsafe { return builder.Construct(ref dst, ((T*)src.GetUnsafePtr()) + srcOffset, srcLength); }
		}


		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, T[] src) where T : unmanaged
		{
			unsafe { fixed (T* srcPtr = src) { return builder.Construct(ref dst, srcPtr, src.Length); } }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, T[] src, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckLengthInRange(srcLength, src.Length);
			unsafe { fixed (T* srcPtr = src) { return builder.Construct(ref dst, srcPtr, srcLength); } }
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, T[] src, int srcOffset, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckRangeInRange(srcOffset, srcLength, src.Length);
			unsafe { fixed (T* srcPtr = src) { return builder.Construct(ref dst, srcPtr + srcOffset, srcLength); } }
		}


		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, List<T> src) where T : unmanaged
		{
			var blobArray = builder.Allocate(ref dst, src.Count);
			for (int i = 0; i < src.Count; i++)
				blobArray[i] = src[i];
			return blobArray;
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, List<T> src, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckLengthInRange(srcLength, src.Count);
			var blobArray = builder.Allocate(ref dst, srcLength);
			for (int i = 0; i < srcLength; i++)
				blobArray[i] = src[i];
			return blobArray;
		}

		public static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, List<T> src, int srcOffset, int srcLength) where T : unmanaged
		{
			CollectionChecks.CheckRangeInRange(srcOffset, srcLength, src.Count);
			var blobArray = builder.Allocate(ref dst, src.Count);
			for (int i = 0; i < srcLength; i++)
				blobArray[i] = src[i + srcOffset];
			return blobArray;
		}



		unsafe static BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> dst, [ReadOnly] T* srcPtr, int srcLength) where T : unmanaged
        {
            CollectionChecks.CheckPtr(srcPtr);
            CollectionChecks.CheckPositive(srcLength);
			srcLength = math.max(srcLength, 0);
            var blobBuilderArray = builder.Allocate(ref dst, srcLength);
            if (srcLength > 0) UnsafeUtility.MemCpy(blobBuilderArray.GetUnsafePtr(), srcPtr, blobBuilderArray.Length * sizeof(T));
            return blobBuilderArray;
        }
    }
}
