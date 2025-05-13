using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

using UnityEngine;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    public struct SlotIndex
    {
		public int index;
        public int generation;

		[EditorBrowsable(EditorBrowsableState.Never), BurstDiscard]
		public readonly override string ToString() { return $"({nameof(index)} = {index}, {nameof(generation)} = {generation})"; }

		#region Comparison
		[EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(SlotIndex left, SlotIndex right) { return left.index == right.index && left.generation == right.generation; }
		[EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(SlotIndex left, SlotIndex right) { return left.index != right.index || left.generation != right.generation; }
		[EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override readonly bool Equals(object obj) { if (obj is SlotIndex slot) return this == slot; return false; }
		[EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly override int GetHashCode() { return (int)Hash(); }
		[EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly uint Hash() { unchecked { return math.hash(new int2(index, generation)); } }

		[EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly int CompareTo(SlotIndex other)
		{
			var diff = index.CompareTo(other.index);
			if (diff != 0) return diff;
			return generation.CompareTo(other);
		}

		[EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Equals(SlotIndex other) { return index == other.index && generation == other.generation; }
		#endregion
	}

	// TODO: make sure everything is covered in tests
	// TODO: use native containers, make hierarchy use this as well
	[GenerateTestsForBurstCompatibility]
    struct SlotIndexMap : IDisposable
    {
        [System.Diagnostics.DebuggerDisplay("Index = {index}, Generation = {generation}")]
        [StructLayout(LayoutKind.Sequential)]
        struct IndexLookup
        {
            public Int32 index;
            public Int32 generation;
        }

        [NoAlias, ReadOnly] UnsafeList<IndexLookup> slotToIndex;
        [NoAlias, ReadOnly] UnsafeList<int>         indexToSlot;
        [NoAlias, ReadOnly] SectionManager          sectionManager;
        [NoAlias, ReadOnly] UnsafeList<int>         freeSlots; // TODO: use SectionManager, or something like that, so we can easily allocate ids/id ranges in order


        [GenerateTestsForBurstCompatibility]
        public readonly bool CheckConsistency()
        {
            for (int i = 0; i < slotToIndex.Length; i++)
            {
                var index = slotToIndex[i].index;
                if (index == -1)
                {
                    if (!freeSlots.Contains(i))
                    {
                        Debug.LogError($"!{nameof(freeSlots)}.Contains({i})");
                        return false;
                    }
                    continue;
                }

                if (index < 0 || index >= indexToSlot.Length)
                {
                    Debug.LogError($"{index} < 0 || {index} >= {indexToSlot.Length}");
                    return false;
                }

                if ((indexToSlot[index] - 1) != i)
                {
                    Debug.LogError($"{nameof(indexToSlot)}[{index}] - 1 ({(indexToSlot[index] - 1)}) == {i}");
                    return false;
                }

                if (sectionManager.IsIndexFree(index))
                {
                    Debug.LogError($"{nameof(sectionManager)}.IsIndexFree({index})");
                    return false;
                }
            }

            for (int i = 0; i < indexToSlot.Length; i++)
            {
                var slot = indexToSlot[i];
                if (slot == 0)
                {
                    if (!sectionManager.IsIndexFree(i))
                    {
                        Debug.LogError($"!sectionManager.IsIndexFree({i})");
                        return false;
                    }
                    continue;
                }

                slot--;

                if (slot < 0 || slot >= slotToIndex.Length)
                {
                    Debug.LogError($"{slot} < 0 || {slot} >= {slotToIndex.Length}");
                    return false;
                }

                if (slotToIndex[slot].index != i)
                {
                    Debug.LogError($"idToIndex[{slot}].index ({slotToIndex[slot].index}) == {i}");
                    return false;
                }

                if (sectionManager.IsIndexFree(i))
                {
                    Debug.LogError($"sectionManager.IsIndexFree({i})");
                    return false;
                }
            }
            return true;
        }

        // Note: not all indices might be in use
        public readonly int IndexCount   { get { return indexToSlot.Length; } }

        public readonly bool IsIndexFree(int index) { return sectionManager.IsIndexFree(index); }
        public readonly bool IsAnyIndexFree(int index, int count) { return sectionManager.IsAnyIndexFree(index, count); }

        public readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return slotToIndex.IsCreated &&
                       indexToSlot.IsCreated &&
                       sectionManager.IsCreated &&
                       freeSlots.IsCreated;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SlotIndexMap Create(Allocator allocator)
        {
            return new SlotIndexMap
            {
                slotToIndex    = new UnsafeList<IndexLookup>(1024, allocator),
                indexToSlot    = new UnsafeList<int>(1024, allocator),
                sectionManager = SectionManager.Create(allocator),
                freeSlots      = new UnsafeList<int>(1024, allocator)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            if (freeSlots.IsCreated) freeSlots.Clear();
            if (slotToIndex.IsCreated) slotToIndex.Clear();
            if (indexToSlot.IsCreated) indexToSlot.Clear();
            if (sectionManager.IsCreated) sectionManager.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
		{
			// Confirmed to be called
            if (freeSlots.IsCreated) freeSlots.Dispose(); freeSlots = default;
			if (slotToIndex.IsCreated) slotToIndex.Dispose(); slotToIndex = default;
            if (indexToSlot.IsCreated) indexToSlot.Dispose(); indexToSlot = default;
            if (sectionManager.IsCreated) sectionManager.Dispose(); sectionManager = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void GetSlotIndex(int index, out SlotIndex slotIndex)
        {
			slotIndex.index = default; //out
			slotIndex.generation = default; //out

            if (index < 0 || index >= indexToSlot.Length ||
                !sectionManager.IsAllocatedIndex(index))
                throw new ArgumentOutOfRangeException($"{nameof(index)} ({index}) must be allocated and lie between 0 ... {indexToSlot.Length}");

            var idInternal = indexToSlot[index] - 1;
            if (idInternal < 0 || idInternal >= slotToIndex.Length)
                throw new IndexOutOfRangeException($"{nameof(slotIndex.index)} ({slotIndex.index}) must be between 1 ... {1 + slotToIndex.Length}");

			slotIndex.generation = slotToIndex[idInternal].generation;
            if (slotToIndex[idInternal].index != index)
                throw new FieldAccessException($"Internal mismatch of ids and indices");

			slotIndex.index = idInternal + 1;//out
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsValidSlotIndex(SlotIndex slotIndex, out int index)
        {
            var idInternal = slotIndex.index - 1; // We don't want 0 to be a valid index

            index = -1;
            if (idInternal < 0 || idInternal >= slotToIndex.Length)
                return false;

            var idLookup = slotToIndex[idInternal];
            if (idLookup.generation != slotIndex.generation)
                return false;

            index = idLookup.index;
            return sectionManager.IsAllocatedIndex(idLookup.index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly bool IsValidSlotIndexUnsafe(SlotIndex slotIndex, out int index)
        {
            var idInternal = slotIndex.index - 1; // We don't want 0 to be a valid index

			index = -1;
            if (idInternal < 0 || idInternal >= slotToIndex.Length)
                return false;

            var idLookup = slotToIndex[idInternal];
            if (idLookup.generation != slotIndex.generation)
                return false;

            index = idLookup.index;
            return true;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsValidIndex(int index, out SlotIndex slotIndex)
        {
			slotIndex.index = default; //out
			slotIndex.generation = default;//out

            if (!sectionManager.IsAllocatedIndex(index))
                return false;

            var idInternal = indexToSlot[index] - 1;
            if (idInternal < 0 || idInternal >= slotToIndex.Length)
                return false;

			slotIndex.generation = slotToIndex[idInternal].generation;
            if (slotToIndex[idInternal].index != index)
                return false;

			slotIndex.index = idInternal + 1;//out
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int GetIndexNoErrors(SlotIndex slotIndex)
        {
            Debug.Assert(IsCreated);
            var idInternal = slotIndex.index - 1; // We don't want 0 to be a valid index

			if (idInternal < 0 || idInternal >= slotToIndex.Length)
                return -1;

            var idLookup = slotToIndex[idInternal];
            if (idLookup.generation != slotIndex.generation)
                return -1;

            var index = idLookup.index;
            if (index < 0 || index >= indexToSlot.Length)
                return -1;

            return idLookup.index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int GetIndex(SlotIndex slotIndex)
        {
            Debug.Assert(IsCreated);
            var indexInternal = slotIndex.index - 1; // We don't want 0 to be a valid index

			var maxIndex = slotToIndex.Length;
            if (indexInternal < 0 || indexInternal >= maxIndex)
            {
                CheckSlotIndex(slotIndex.index, maxIndex);
                return -1;
            }
             
            var idLookup = slotToIndex[indexInternal];
            if (idLookup.generation != slotIndex.generation)
            {
                CheckGeneration(slotIndex.generation, idLookup.generation);
                return -1;
            }

            var maxSlotIndex = indexToSlot.Length;
            var index = idLookup.index;
            if (index < 0 || index >= maxSlotIndex)
            {
                CheckIndexInRange(slotIndex.index, index, maxSlotIndex);
                return -1;
            }

            return idLookup.index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CreateSlotIndex(out SlotIndex slotIndex)
        {
            var index = sectionManager.AllocateRange(1);
            AllocateIndexRange(index, 1);
            GetSlotIndex(index, out slotIndex);
            return index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CreateSlotIndex(int index, out SlotIndex slotIndex)
        {
            AllocateIndexRange(index, 1);
            GetSlotIndex(index, out slotIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AllocateIndexRange(int range)
        {
            if (range == 0)
                return -1;

            var index = sectionManager.AllocateRange(range);
            AllocateIndexRange(index, range);
            return index;
        }

        internal void AllocateIndexRange(int index, int range)
        {
            if ((index + range) > indexToSlot.Length)
                indexToSlot.Resize((index + range), NativeArrayOptions.ClearMemory);

            int indexInternal, generation;
            // TODO: should make it possible to allocate ids in a range as well, for cache locality
            if (freeSlots.Length >= range)
            {
                var childIndex = index;
                while (range > 0)
                {
                    var freeSlotIndex = freeSlots.Length - 1;
                    indexInternal = freeSlots[freeSlotIndex];
                    freeSlots.RemoveAt(freeSlotIndex);
                    
                    generation = slotToIndex[indexInternal].generation + 1;
                    slotToIndex[indexInternal] = new IndexLookup { index = childIndex, generation = generation };

                    indexToSlot[childIndex] = indexInternal + 1;

                    range--;
                    childIndex++;
                }
            }

            if (range <= 0)
                return;
            
            generation = 1;
            indexInternal = slotToIndex.Length;
            slotToIndex.Resize((indexInternal + range), NativeArrayOptions.ClearMemory);

            for (int childIndex = index, lastSlotIndex = (indexInternal + range); indexInternal < lastSlotIndex; indexInternal++, childIndex++)
            {
                indexToSlot[childIndex] = indexInternal + 1;
                slotToIndex[indexInternal] = new IndexLookup { index = childIndex, generation = generation };                    
            }
        }

        public void SwapIndexRangeToBack(int sectionIndex, int sectionLength, int swapIndex, int swapRange)
        {
            if (sectionIndex < 0)
                throw new ArgumentException($"{nameof(sectionIndex)} must be 0 or higher.");

            if (sectionLength < 0)
                throw new ArgumentException($"{nameof(sectionLength)} must be 0 or higher.");

            if (swapIndex < 0)
                throw new ArgumentException($"{nameof(swapIndex)} must be positive.");

            if (swapIndex + swapRange > sectionLength)
                throw new ArgumentException($"{nameof(swapIndex)} ({swapIndex}) + {nameof(swapRange)} ({swapRange}) must be smaller than {nameof(sectionIndex)} ({sectionIndex}).");

            if (sectionLength == 0 || swapRange == 0)
                return;

            var lengthBehindSwapIndex = sectionLength - swapIndex;
            var tempOffset = indexToSlot.Length;

            // aaaaaabbbbcc ....
            // aaaaaabbbbcc .... bbbb
            //        |           ^
            //        |___________|

            // Copy the original indices to beyond the end of the list
            // Make space for these indices, hopefully the index list already has the capacity for 
            // this and no allocation needs to be made
            indexToSlot.Resize(tempOffset + swapRange, 
                NativeArrayOptions.ClearMemory);
			    //NativeArrayOptions.UninitializedMemory);
			indexToSlot.MemMove(tempOffset, sectionIndex + swapIndex, swapRange);
            
            // aaaaaabbbbcc .... bbbb
            // aaaaaaccbbcc .... bbbb
            //        ^  |       
            //        |__|       

            // Move indices behind our swapIndex/swapRange on top of where our swap region begins
            var count = lengthBehindSwapIndex - swapRange;
            indexToSlot.MemMove(sectionIndex + swapIndex, sectionIndex + swapIndex + swapRange, count);
            
            // aaaaaaccbbcc .... bbbb
            // aaaaaaccbbbb .... bbbb
            //         ^           |
            //         |___________|

            // Copy the original indices to the end
            indexToSlot.MemMove(sectionIndex + swapIndex + count, tempOffset, swapRange);

            // aaaaaaccbbbb .... bbbb
            // aaaaaaccbbbb .... 

            // Resize indices list to remove the temporary data, this is basically just 
            //   indexToSlotIndex.length = tempOffset
            indexToSlot.Resize(tempOffset, 
                NativeArrayOptions.ClearMemory);
			    //NativeArrayOptions.UninitializedMemory);

			for (int index = sectionIndex + swapIndex, lastIndex = sectionIndex + sectionLength; index < lastIndex; index++)
            {
                var idInternal = indexToSlot[index] - 1;

                var idLookup = slotToIndex[idInternal];
                idLookup.index = index;
                slotToIndex[idInternal] = idLookup;
            }
        }

        internal int InsertIndexRange(int originalOffset, int originalCount, int srcIndex, int dstIndex, int moveCount = 1)
        {
            if (moveCount <= 0)
                throw new ArgumentException($"{nameof(moveCount)} must be 1 or higher.");

            var newCount = originalCount + moveCount;
            var newOffset = sectionManager.ReallocateRange(originalOffset, originalCount, newCount);
            if (newOffset < 0)
                throw new ArgumentException($"{nameof(newOffset)} must be 0 or higher.");

            if (indexToSlot.Length < newOffset + newCount)
                indexToSlot.Resize(newOffset + newCount, NativeArrayOptions.ClearMemory);

            unsafe
            {
                var originalSlotIndices = stackalloc int[moveCount];
                for (int index = 0; index < moveCount; index++)
                {
                    originalSlotIndices[index] = indexToSlot[srcIndex + index];
                    indexToSlot[srcIndex + index] = default;
                }

                // We first move the front part (when necesary)
                var range = dstIndex;
                if (range > 0)
                    indexToSlot.MemMove(newOffset, originalOffset, range);

                // Then we move the back part to the correct new offset (when necesary) ..
                range = originalCount - dstIndex;
                if (range > 0)
                    indexToSlot.MemMove(newOffset + dstIndex + moveCount, originalOffset + dstIndex, range);

                // Then we copy srcIndex to the new location
                var newNodeIndex = newOffset + dstIndex;
                for (int index = 0; index < moveCount; index++)
                {
                    indexToSlot[newNodeIndex + index] = originalSlotIndices[index];
                }

                // Then we set the old indices to 0
                if (srcIndex >= newOffset && srcIndex + moveCount <= newOffset + newCount)
                    sectionManager.FreeRange(srcIndex, moveCount);
                for (int index = srcIndex; index < srcIndex + moveCount; index++)
                {
                    if (index >= newOffset && index < newOffset + newCount)
                        continue;

                    indexToSlot[index] = default;
                }
                for (int index = originalOffset, lastIndex = (originalOffset + originalCount); index < lastIndex; index++)
                {
                    // TODO: figure out if there's an off by one here
                    if (index >= newOffset && index < newOffset + newCount)
                        continue;

                    indexToSlot[index] = default;
                }

                // And fixup the slot to index lookup
                for (int index = newOffset, lastIndex = (newOffset + newCount); index < lastIndex; index++)
                {
                    var idInternal = indexToSlot[index] - 1;

                    var idLookup = slotToIndex[idInternal];
                    idLookup.index = index;
                    slotToIndex[idInternal] = idLookup;
                }

                return newOffset;
            }
        }

        internal void RemoveIndexRange(int offset, int count, int removeIndex, int removeRange)
        {
            if (offset < 0) throw new ArgumentException($"{nameof(offset)} must be positive");
            if (count < 0) throw new ArgumentException($"{nameof(count)} must be positive");
            if (removeIndex < 0) throw new ArgumentException($"{nameof(removeIndex)} must be positive");
            if (removeRange < 0) throw new ArgumentException($"{nameof(removeRange)} must be positive");
            if (removeRange == 0) throw new ArgumentException($"{nameof(removeRange)} must be above 0");
            if (count == 0) throw new ArgumentException($"{nameof(count)} must be above 0");
            if (removeIndex < offset)
                throw new ArgumentException($"{nameof(removeIndex)} ({removeIndex}) < {nameof(offset)} ({offset})");
            if (removeIndex + removeRange > offset + count) 
                throw new ArgumentException($"{nameof(removeIndex)} ({removeIndex}) + {nameof(removeRange)} ({removeRange}) > {nameof(count)} ({count})");

            // Remove the range of indices we want to remove
            for (int i = removeIndex, lastIndex = removeIndex + removeRange; i < lastIndex; i++)
            {
                var idInternal = indexToSlot[i] - 1;

                Debug.Assert(!freeSlots.Contains(idInternal));
                freeSlots.Add(idInternal);

                var idLookup = slotToIndex[idInternal];
                idLookup.index = -1;
                slotToIndex[idInternal] = idLookup;

                indexToSlot[i] = 0;
            }

            var leftOver = (offset + count) - (removeIndex + removeRange);
            if (leftOver < 0)
            {
                throw new ArgumentException($"{nameof(leftOver)} ({leftOver}) < 0");
            }
            if (removeIndex + leftOver > indexToSlot.Length)
            {
                throw new ArgumentException($"{nameof(removeIndex)} ({removeIndex}) + {nameof(leftOver)} ({leftOver}) < {nameof(indexToSlot)}.Length ({indexToSlot.Length})");
            }
            indexToSlot.MemMove(removeIndex, removeIndex + removeRange, leftOver);

            // Then fixup the slot to index lookup
            for (int i = removeIndex, lastIndex = removeIndex + leftOver; i < lastIndex; i++)
            {
                var idInternal = indexToSlot[i] - 1;

                var idLookup = slotToIndex[idInternal];
                idLookup.index = i;
                slotToIndex[idInternal] = idLookup;
            }

            // And we set the old indices to 0
            for (int i = offset + count - removeRange; i < offset + count; i++)
                indexToSlot[i] = 0;

            sectionManager.FreeRange(offset + count - removeRange, removeRange);
        }

        public int FreeSlotIndex(SlotIndex slotIndex)
        {
            int index = GetIndexNoErrors(slotIndex);
            if (index < 0)
                return -1;

            var idInternal = slotIndex.index - 1; // We don't want 0 to be a valid index

			sectionManager.FreeRange(index, 1);

            Debug.Assert(!freeSlots.Contains(idInternal));
            freeSlots.Add(idInternal);

            var idLookup = slotToIndex[idInternal];
            indexToSlot[index] = 0;
            idLookup.index = -1;
            slotToIndex[idInternal] = idLookup;
            return index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FreeIndex(int index)
        {
            FreeIndexRange(index, 1);
        }

        public void FreeIndexRange(int startIndex, int range)
        {
            var lastIndex = startIndex + range;
            if (startIndex < 0 || lastIndex > indexToSlot.Length)
                throw new ArgumentOutOfRangeException($"StartIndex {startIndex} with range {range}, must be between 0 and {indexToSlot.Length}");

            if (range == 0)
                return; // nothing to do

            if (freeSlots.Capacity < freeSlots.Length + (lastIndex - startIndex + 1))
                freeSlots.SetCapacity((int)((freeSlots.Length + (lastIndex - startIndex + 1)) * 1.5f));
            for (int index = startIndex; index < lastIndex; index++)
            {
                var idInternal = indexToSlot[index] - 1;
                indexToSlot[index] = 0;

                //Debug.Assert(!freeSlots.Contains(idInternal)); // slow
                freeSlots.AddNoResize(idInternal);

                var idLookup = slotToIndex[idInternal];
                idLookup.index = -1;
                slotToIndex[idInternal] = idLookup;
            }

            sectionManager.FreeRange(startIndex, range);
        }




		[System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		static void CheckIndexInRange(int slotIndex, int index, int length)
		{
			if (index < 0 || index >= length)
			{
				if (index == -1)
					throw new Exception($"Using an {nameof(slotIndex)} ({slotIndex}) that seems to have already been destroyed.");
				else
				if (length == 0)
					throw new Exception($"{nameof(slotIndex)} ({slotIndex}) does not point to an valid index ({index}). This lookup table does not contain any valid indices at the moment.");
				else
					throw new Exception($"{nameof(slotIndex)} ({slotIndex}) does not point to an valid index ({index}). It must be >= 0 and < {length}.");
			}
		}

		[System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		static void CheckGeneration(int generation, int expectedGeneration)
		{
			if (expectedGeneration != generation)
				throw new Exception($"The given generation ({generation}) was not identical to the expected generation ({expectedGeneration}), are you using an old reference?");
		}

		[System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		static void CheckSlotIndex(int slotIndex, int maxSlotIndex)
		{
			var slotIndexInternal = slotIndex - 1; // We don't want 0 to be a valid index
			if (slotIndexInternal < 0 || slotIndexInternal >= maxSlotIndex)
				throw new ArgumentException($"{nameof(slotIndex)}", $"({slotIndex}) must be between 1 and {1 + maxSlotIndex}");
		}
	}
}