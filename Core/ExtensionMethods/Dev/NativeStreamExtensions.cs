using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
	public static class NativeStreamExtensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>([NoAlias, WriteOnly] ref this NativeStream.Writer dst, [NoAlias, ReadOnly] NativeArray<T> src) where T : unmanaged { Write(ref dst, src, 0, src.Length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>([NoAlias, WriteOnly] ref this NativeStream.Writer dst, [NoAlias, ReadOnly] NativeArray<T> src, int length) where T : unmanaged { Write(ref dst, src, 0, length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>([NoAlias, WriteOnly] ref this NativeStream.Writer dst, [NoAlias, ReadOnly] NativeArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(ref dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			dst.Write(length);
			if (length <= 0) return;
			for (int i = 0; i < length; i++)
				dst.Write(src[i + index]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>([NoAlias, WriteOnly] ref this NativeStream.Writer dst, [NoAlias, ReadOnly] NativeSlice<T> src) where T : unmanaged { Write(ref dst, src, 0, src.Length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>([NoAlias, WriteOnly] ref this NativeStream.Writer dst, [NoAlias, ReadOnly] NativeSlice<T> src, int length) where T : unmanaged { Write(ref dst, src, 0, length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>([NoAlias, WriteOnly] ref this NativeStream.Writer dst, [NoAlias, ReadOnly] NativeSlice<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(ref dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			dst.Write(length);
			if (length <= 0) return;
			for (int i = 0; i < length; i++)
				dst.Write(src[i + index]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>([NoAlias, WriteOnly] ref this NativeStream.Writer dst, [NoAlias, ReadOnly] NativeList<T> src) where T : unmanaged { Write(ref dst, src, 0, src.Length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>([NoAlias, WriteOnly] ref this NativeStream.Writer dst, [NoAlias, ReadOnly] NativeList<T> src, int length) where T : unmanaged { Write(ref dst, src, 0, length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>([NoAlias, WriteOnly] ref this NativeStream.Writer dst, [NoAlias, ReadOnly] NativeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(ref dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			dst.Write(length);
			if (length <= 0) return;
			for (int i = 0; i < length; i++)
				dst.Write(src[i + index]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>([NoAlias, WriteOnly] ref this NativeStream.Writer dst, [NoAlias, ReadOnly] UnsafeList<T> src) where T : unmanaged { Write(ref dst, src, 0, src.Length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>([NoAlias, WriteOnly] ref this NativeStream.Writer dst, [NoAlias, ReadOnly] UnsafeList<T> src, int length) where T : unmanaged { Write(ref dst, src, 0, length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>([NoAlias, WriteOnly] ref this NativeStream.Writer dst, [NoAlias, ReadOnly] UnsafeList<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(ref dst);
			CollectionChecks.CheckReadAndThrow(src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			dst.Write(length);
			if (length <= 0) return;
			for (int i = 0; i < length; i++)
				dst.Write(src[i + index]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>([NoAlias, WriteOnly] ref this NativeStream.Writer dst, [NoAlias, ReadOnly] ref BlobArray<T> src) where T : unmanaged { Write(ref dst, ref src, 0, src.Length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>([NoAlias, WriteOnly] ref this NativeStream.Writer dst, [NoAlias, ReadOnly] ref BlobArray<T> src, int length) where T : unmanaged { Write(ref dst, ref src, 0, length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>([NoAlias, WriteOnly] ref this NativeStream.Writer dst, [NoAlias, ReadOnly] ref BlobArray<T> src, int index, int length) where T : unmanaged
		{
			CollectionChecks.CheckWriteAndThrow(ref dst);
			CollectionChecks.CheckReadAndThrow(ref src);
			CollectionChecks.CheckRangeInRange(index, length, src.Length);
			dst.Write(length);
			if (length <= 0) return;
			for (int i = 0; i < length; i++)
				dst.Write(src[i + index]);
		}



		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ReadArrayAndEnsureSize<T>([NoAlias, ReadOnly] ref this NativeStream.Reader src, [NoAlias] ref NativeArray<T> dst, out int length) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			length = src.Read<int>();
			if (length == 0) return;
			if (!dst.IsCreated || dst.Length < length)
			{
				if (dst.IsCreated) dst.Dispose();
				dst = new NativeArray<T>(length, Allocator.Temp);
			}
			for (int i = 0; i < length; i++)
				dst[i] = src.Read<T>();
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Read<T>([NoAlias, ReadOnly] ref this NativeStream.Reader src, [NoAlias] ref NativeList<T> dst) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			var length = src.Read<int>();
			if (length == 0) return;
			dst.ResizeUninitialized(length);
			for (int i = 0; i < length; i++)
				dst[i] = src.Read<T>();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Read<T>([NoAlias, ReadOnly] ref this NativeStream.Reader src, [NoAlias] out T item) where T : unmanaged
		{
			CollectionChecks.CheckReadAndThrow(src);
			item = src.Read<T>();
        }
    }
}
