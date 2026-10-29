using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
	// xxHash3 of the bytes of containers and values, so the code that hashes them needs no pointers.
	// Empty or missing data hashes to default.
	public static unsafe class HashExtensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static uint4 Bytes(void* data, long length) { return length == 0 ? default : xxHash3.Hash128(data, length); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint4 Hash128<T>(this NativeArray<T> array) where T : unmanaged
		{
			return array.IsCreated ? Bytes(array.GetUnsafeReadOnlyPtr(), (long)array.Length * sizeof(T)) : default;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint4 Hash128<T>(this UnsafeList<T> list) where T : unmanaged
		{
			return Bytes(list.Ptr, (long)list.Length * sizeof(T));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint4 Hash128<T>(ref this BlobArray<T> array) where T : unmanaged
		{
			return Bytes(array.GetUnsafePtr(), (long)array.Length * sizeof(T));
		}

		// All of the buffer's bytes, also when it has none
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint4 Hash128(this UnsafeAppendBuffer buffer)
		{
			return xxHash3.Hash128(buffer.Ptr, buffer.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint2 Hash64(this UnsafeAppendBuffer buffer)
		{
			return xxHash3.Hash64(buffer.Ptr, buffer.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint4 HashValue<T>(in T value) where T : unmanaged
		{
			var copy = value;
			return Bytes(&copy, sizeof(T));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint4 HashValues<T>(ReadOnlySpan<T> values) where T : unmanaged
		{
			fixed (T* data = values) { return Bytes(data, (long)values.Length * sizeof(T)); }
		}

		// 32 bit math.hash of the bytes, also when there are none
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash32<T>(this NativeArray<T> array) where T : unmanaged
		{
			return math.hash(array.IsCreated ? array.GetUnsafeReadOnlyPtr() : null, array.Length * sizeof(T));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash32Value<T>(in T value) where T : unmanaged
		{
			var copy = value;
			return math.hash(&copy, sizeof(T));
		}

		// The blob's data, as long as the header in front of it (BlobAssetHeader: 32 bytes, its length at 8) gives a sane size
		public static bool TryHash128<T>(this BlobAssetReference<T> blob, out uint4 hash) where T : unmanaged
		{
			hash = default;
			if (!blob.IsCreated)
				return false;
			var data   = (byte*)blob.GetUnsafePtr();
			var length = *(int*)(data - 32 + 8);
			if (length <= 0 || length >= (1 << 30))
				return false;
			hash = xxHash3.Hash128(data, length);
			return true;
		}

		// The elements' bytes, without their count
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void AddBytes<T>(ref this UnsafeAppendBuffer buffer, ref BlobArray<T> array) where T : unmanaged
		{
			buffer.Add(array.GetUnsafePtr(), array.Length * sizeof(T));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void AddBytes<T>(ref this UnsafeAppendBuffer buffer, NativeArray<T> array) where T : unmanaged
		{
			buffer.Add(array.GetUnsafeReadOnlyPtr(), array.Length * sizeof(T));
		}
	}
}
