using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst.CompilerServices;

namespace Chisel.Core
{
    public static class NativeListExtensions
    {
		/// <summary>
		/// Returns parallel writer instance.
		/// </summary>
		/// <returns>Parallel writer instance.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ParallelWriterExt<T> AsParallelWriterExt<T>(this NativeList<T> list)
            where T : unmanaged
		{
			unsafe
            {
                var m_ListData = list.GetUnsafeList();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                var m_Safety = NativeListUnsafeUtility.GetAtomicSafetyHandle(ref list);
                return new ParallelWriterExt<T>(m_ListData->Ptr, m_ListData, ref m_Safety);
#else
                return new ParallelWriterExt<T>(m_ListData->Ptr, m_ListData);
#endif
            }
        }

        /// <summary>
        /// Implements parallel writer. Use AsParallelWriterExt to obtain it from container.
        /// </summary>
        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        public struct ParallelWriterExt<T>
            where T : unmanaged
        {
            /// <summary>
            ///
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            public unsafe readonly void* Ptr;

            /// <summary>
            ///
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            public unsafe UnsafeList<T>* ListData;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal unsafe ParallelWriterExt(void* ptr, UnsafeList<T>* listData, ref AtomicSafetyHandle safety)
            {
                Ptr = ptr;
                ListData = listData;
                m_Safety = safety;
            }

#else
            internal unsafe ParallelWriterExt(void* ptr, UnsafeList<T>* listData)
            {
                Ptr = ptr;
                ListData = listData;
            }

#endif

            [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void CheckSufficientCapacity(int capacity, int length)
            {
                if (capacity < length)
                    throw new Exception($"Length {length} exceeds capacity Capacity {capacity}");
            }

            [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void CheckArgPositive(int value)
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException($"Value {value} must be positive.");
            }

            /// <summary>
            /// Tell Burst that an integer can be assumed to map to an always positive value.
            /// </summary>
            /// <param name="value">The integer that is always positive.</param>
            /// <returns>Returns `x`, but allows the compiler to assume it is always positive.</returns>
            [return: AssumeRange(0, int.MaxValue)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static int AssumePositive(int value)
            {
                return value;
            }

            /// <summary>
            /// Adds an element to the list.
            /// </summary>
            /// <param name="value">The value to be added at the end of the list.</param>
            /// <remarks>
            /// If the list has reached its current capacity, internal array won't be resized, and exception will be thrown.
            /// </remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int AddNoResize(T value)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                unsafe
                {
                    var idx = Interlocked.Increment(ref ListData->m_length) - 1;
                    CheckSufficientCapacity(ListData->Capacity, idx + 1);

                    UnsafeUtility.WriteArrayElement(Ptr, idx, value);
					return idx;
				}
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            unsafe int AddRangeNoResize(int sizeOf, int alignOf, void* ptr, int length)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
				unsafe
                {
                    var idx = Interlocked.Add(ref ListData->m_length, length) - length;
                    CheckSufficientCapacity(ListData->Capacity, idx + length);

                    void* dst = (byte*)Ptr + idx * sizeOf;
                    UnsafeUtility.MemCpy(dst, ptr, length * sizeOf);
                    return idx;
                }
            }

            /// <summary>
            /// Adds elements from a buffer to this list.
            /// </summary>
            /// <param name="ptr">A pointer to the buffer.</param>
            /// <param name="length">The number of elements to add to the list.</param>
            /// <remarks>
            /// If the list has reached its current capacity, internal array won't be resized, and exception will be thrown.
            /// </remarks>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if length is negative.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            unsafe int AddRangeNoResize(void* ptr, int length)
			{
				unsafe
                {
                    CheckArgPositive(length);
                    return AddRangeNoResize(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), ptr, AssumePositive(length));
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int AddRangeNoResize(NativeArray<T> array, int length)
            {
                CheckArgPositive(length);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

				unsafe
                {
                    var idx = Interlocked.Add(ref ListData->m_length, length) - length;
                    CheckSufficientCapacity(ListData->Capacity, idx + length);

                    int sizeOf = UnsafeUtility.SizeOf<T>();
                    int alignOf = UnsafeUtility.AlignOf<T>();
                    var ptr = array.GetUnsafeReadOnlyPtr();
                    void* dst = (byte*)Ptr + idx * sizeOf;
                    UnsafeUtility.MemCpy(dst, ptr, length * sizeOf);
                    return idx;
                }
            }

            /// <summary>
            /// Adds elements from a list to this list.
            /// </summary>
            /// <param name="list">Other container to copy elements from.</param>
            /// <remarks>
            /// If the list has reached its current capacity, internal array won't be resized, and exception will be thrown.
            /// </remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int AddRangeNoResize(UnsafeList<T> list)
			{
				unsafe
                {
                    return AddRangeNoResize(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), list.Ptr, list.Length);
                }
            }

            /// <summary>
            /// Adds elements from a list to this list.
            /// </summary>
            /// <param name="list">Other container to copy elements from.</param>
            /// <remarks>
            /// If the list has reached its current capacity, internal array won't be resized, and exception will be thrown.
            /// </remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int AddRangeNoResize(NativeList<T> list)
			{
				unsafe
                {
                    var m_ListData = list.GetUnsafeList();
                    return AddRangeNoResize(*m_ListData);
                }
            }
        }
    }
}
