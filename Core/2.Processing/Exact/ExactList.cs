using System;
using System.Runtime.CompilerServices;
#if !EXACT_OFFLINE
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
#endif

namespace Chisel.Core
{
    unsafe struct ExactList<T> : IDisposable where T : unmanaged
    {
        T*  m_Buffer;
        int m_Length;
        int m_Capacity;

        public ExactList(int capacity)
        {
            m_Buffer   = null;
            m_Length   = 0;
            m_Capacity = 0;
            Reserve(Math.Max(capacity, 4));
        }

        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Buffer != null;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Length;
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckIndex(index);
                return ref m_Buffer[index];
            }
        }

        // By value, not `in`: the value may be an element of this very list (list.Add(list[i])), and growing the list frees
        // the buffer it lives in before it would be read.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T value)
        {
            if (m_Length == m_Capacity)
                Reserve(m_Capacity * 2);
            m_Buffer[m_Length] = value;
            m_Length++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() { m_Length = 0; }

        public void RemoveAt(int index)
        {
            CheckIndex(index);
            for (int i = index + 1; i < m_Length; i++)
                m_Buffer[i - 1] = m_Buffer[i];
            m_Length--;
        }

        public void RemoveAtSwapBack(int index)
        {
            CheckIndex(index);
            m_Buffer[index] = m_Buffer[m_Length - 1];
            m_Length--;
        }

        // New elements are zeroed.
        public void Resize(int length)
        {
            if (length > m_Capacity)
                Reserve(Math.Max(length, m_Capacity * 2));
            for (int i = m_Length; i < length; i++)
                m_Buffer[i] = default;
            m_Length = length;
        }

        public void Reserve(int capacity)
        {
            if (capacity <= m_Capacity)
                return;
            var buffer = (T*)Allocate((long)capacity * sizeof(T));
            if (m_Buffer != null)
            {
                for (int i = 0; i < m_Length; i++)
                    buffer[i] = m_Buffer[i];
                Free(m_Buffer);
            }
            m_Buffer   = buffer;
            m_Capacity = capacity;
        }

        public void Dispose()
        {
            if (m_Buffer != null)
                Free(m_Buffer);
            m_Buffer   = null;
            m_Length   = 0;
            m_Capacity = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CheckIndex(int index)
        {
#if EXACT_OFFLINE
            if ((uint)index >= (uint)m_Length)
                throw new IndexOutOfRangeException($"{index} of {m_Length}");
#elif ENABLE_UNITY_COLLECTIONS_CHECKS
            if ((uint)index >= (uint)m_Length)
                ThrowIndex(index, m_Length);
#endif
        }

#if !EXACT_OFFLINE
        [Unity.Burst.BurstDiscard]
        static void ThrowIndex(int index, int length)
        {
            throw new IndexOutOfRangeException($"{index} of {length}");
        }
#endif

        static void* Allocate(long bytes)
        {
#if EXACT_OFFLINE
            return (void*)System.Runtime.InteropServices.Marshal.AllocHGlobal((IntPtr)bytes);
#else
            return UnsafeUtility.Malloc(bytes, 16, Allocator.Temp);
#endif
        }

        static void Free(void* pointer)
        {
#if EXACT_OFFLINE
            System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)pointer);
#else
            UnsafeUtility.Free(pointer, Allocator.Temp);
#endif
        }
    }
}
