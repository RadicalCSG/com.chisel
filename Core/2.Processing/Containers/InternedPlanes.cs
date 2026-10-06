using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Chisel.Core
{
    public unsafe struct InternedPlanes : INativeDisposable
    {
        internal const int kMinHashTableSize = 1024;

        internal const double kMaxNormalDeviation = 1e-7;

        // A matching normal differs by at most sqrt(2 * kMaxNormalDeviation) in a single component,
        // so it lands in this cell or an adjacent one - which is what makes the 3^4 probe exhaustive.
        internal const float kNormalCellSize = 4.4721e-4f * 2.5f;

        internal const float kOffsetCellSize = 1.0f;

        const long kHashMagicValue = (long)1099511628211ul;

        [NativeDisableUnsafePtrRestriction] UnsafeList<float4>* m_Planes;
        [NativeDisableUnsafePtrRestriction] UnsafeList<int>*    m_ChainedIndices;
        [NativeDisableUnsafePtrRestriction] void*               m_HashTable;

        readonly int m_HashMask;

        readonly Allocator m_AllocatorLabel;

        public readonly bool IsCreated => m_Planes != null && m_ChainedIndices != null && m_HashTable != null;
        public readonly int  Count     => m_Planes == null ? 0 : m_Planes->Length;

        public readonly float4 this[int id] => ((float4*)m_Planes->Ptr)[id];

        public InternedPlanes(int minCapacity, Allocator allocator)
        {
            m_AllocatorLabel = allocator;
            var tableSize = math.max(kMinHashTableSize, math.ceilpow2(math.max(1, minCapacity)));
            m_HashMask = tableSize - 1;
            var hashTableMemSize = tableSize * UnsafeUtility.SizeOf<int>();
            m_HashTable = UnsafeUtility.Malloc(hashTableMemSize, UnsafeUtility.AlignOf<int>(), allocator);
            UnsafeUtility.MemClear(m_HashTable, hashTableMemSize);
            m_Planes         = UnsafeList<float4>.Create(math.max(1, minCapacity), allocator);
            m_ChainedIndices = UnsafeList<int>   .Create(math.max(1, minCapacity), allocator);
        }

        public void Dispose()
        {
            if (m_Planes != null)         { UnsafeList<float4>.Destroy(m_Planes);         m_Planes = null; }
            if (m_ChainedIndices != null) { UnsafeList<int>   .Destroy(m_ChainedIndices); m_ChainedIndices = null; }
            if (m_HashTable != null)      { UnsafeUtility.Free(m_HashTable, m_AllocatorLabel); m_HashTable = null; }
        }

        [BurstCompile]
        struct DisposeJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public UnsafeList<float4>* planes;
            [NativeDisableUnsafePtrRestriction] public UnsafeList<int>*    chainedIndices;
            [NativeDisableUnsafePtrRestriction] public void*               hashTable;
            public Allocator allocator;
            public void Execute()
            {
                if (planes != null)         UnsafeList<float4>.Destroy(planes);
                if (chainedIndices != null) UnsafeList<int>   .Destroy(chainedIndices);
                if (hashTable != null)      UnsafeUtility.Free(hashTable, allocator);
            }
        }

        // Lets the table be torn down on a worker once the jobs reading it have finished, matching
        // how the rest of the pipeline's temporaries are released.
        public JobHandle Dispose(JobHandle inputDeps)
        {
            var jobHandle = new DisposeJob
            {
                planes         = m_Planes,
                chainedIndices = m_ChainedIndices,
                hashTable      = m_HashTable,
                allocator      = m_AllocatorLabel
            }.Schedule(inputDeps);
            m_Planes = null;
            m_ChainedIndices = null;
            m_HashTable = null;
            return jobHandle;
        }

        public void Clear()
        {
            m_Planes->Clear();
            m_ChainedIndices->Clear();
            UnsafeUtility.MemClear(m_HashTable, (m_HashMask + 1) * UnsafeUtility.SizeOf<int>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 Normalize(float4 plane)
        {
            var length = math.length(plane.xyz);
            return (length > (float)CSGConstants.kDivideMinimumEpsilon) ? (plane / length) : plane;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Distance(float4 plane, float3 point)
        {
            return (double)plane.x * point.x + (double)plane.y * point.y + (double)plane.z * point.z + plane.w;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int4 CellOf(float4 plane)
        {
            // floor, not truncation: truncating toward zero makes a double-width cell straddling 0,
            // and a plane through the origin is the common case here, not a rare one.
            return new int4((int)math.floor(plane.x / kNormalCellSize),
                            (int)math.floor(plane.y / kNormalCellSize),
                            (int)math.floor(plane.z / kNormalCellSize),
                            (int)math.floor(plane.w / kOffsetCellSize));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly int GetHash(int4 index)
        {
            var hashCode = (uint)((index.w ^ ((index.y ^ ((index.x ^ index.z) * kHashMagicValue)) * kHashMagicValue)) * kHashMagicValue);
            return (int)(hashCode & (uint)m_HashMask);
        }

        readonly bool Matches(float4 stored, float4 query, float3* points, int pointCount, out bool flipped)
        {
            var dot = math.dot(stored.xyz, query.xyz);
            flipped = dot < 0;
            if (1.0 - math.abs((double)dot) > kMaxNormalDeviation)
                return false;
            for (int i = 0; i < pointCount; i++)
            {
                if (math.abs(Distance(stored, points[i])) > CSGConstants.kPlaneDAlignEpsilon)
                    return false;
            }
            return true;
        }

        readonly bool ProbeCell(int4 cell, float4 query, float3* points, int pointCount,
                                out int id, out bool flipped)
        {
            var planes  = (float4*)m_Planes->Ptr;
            var chained = (int*)m_ChainedIndices->Ptr;
            var chainIndex = ((int*)m_HashTable)[GetHash(cell)] - 1;
            while (chainIndex != -1)
            {
                if (Matches(planes[chainIndex], query, points, pointCount, out flipped))
                {
                    id = chainIndex;
                    return true;
                }
                chainIndex = chained[chainIndex] - 1;
            }
            id = -1;
            flipped = false;
            return false;
        }

        readonly bool ProbeNeighbourhood(float4 cellSource, float4 query, float3* points, int pointCount,
                                         out int id, out bool flipped)
        {
            var center = CellOf(cellSource);
            if (ProbeCell(center, query, points, pointCount, out id, out flipped))
                return true;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            for (int dw = -1; dw <= 1; dw++)
            {
                if ((dx | dy | dz | dw) == 0)
                    continue;                       // already done
                if (ProbeCell(center + new int4(dx, dy, dz, dw), query, points, pointCount, out id, out flipped))
                    return true;
            }
            id = -1;
            flipped = false;
            return false;
        }

        public readonly bool TryFind(float4 plane, float3* points, int pointCount, out int id, out bool flipped)
        {
            plane = Normalize(plane);
            var cell = CellOf(plane);
            var flippedCell = CellOf(-plane);
            if (ProbeCell(cell, plane, points, pointCount, out id, out flipped)) return true;
            if (ProbeCell(flippedCell, plane, points, pointCount, out id, out flipped)) return true;
            if (ProbeNeighbourhood(plane, plane, points, pointCount, out id, out flipped)) return true;
            if (ProbeNeighbourhood(-plane, plane, points, pointCount, out id, out flipped)) return true;
            return false;
        }

        public readonly bool TryFind(float4 plane, float3 pointOnPlane, out int id, out bool flipped)
        {
            return TryFind(plane, &pointOnPlane, 1, out id, out flipped);
        }

        public readonly bool TryFind(float4 plane, NativeArray<float3> pointsOnPlane, out int id, out bool flipped)
        {
            return TryFind(plane, (float3*)pointsOnPlane.GetUnsafeReadOnlyPtr(), pointsOnPlane.Length, out id, out flipped);
        }

        // Returns the shared id for this plane, inserting it if it is new.
        public int Add(float4 plane, float3* points, int pointCount, out bool flipped)
        {
            plane = Normalize(plane);
            if (TryFind(plane, points, pointCount, out int existing, out flipped))
                return existing;

            flipped = false;
            var hash          = GetHash(CellOf(plane));
            var hashTable     = (int*)m_HashTable;
            var previousChain = hashTable[hash];
            m_Planes->Add(plane);
            m_ChainedIndices->Add(previousChain);
            hashTable[hash] = m_Planes->Length;     // stored as id + 1, so 0 means "empty"
            return m_Planes->Length - 1;
        }

        public int Add(float4 plane, float3 pointOnPlane, out bool flipped)
        {
            return Add(plane, &pointOnPlane, 1, out flipped);
        }

        public int Add(float4 plane, NativeArray<float3> pointsOnPlane, out bool flipped)
        {
            return Add(plane, (float3*)pointsOnPlane.GetUnsafeReadOnlyPtr(), pointsOnPlane.Length, out flipped);
        }

        public int Add(float4 plane, float3 pointOnPlane)
        {
            return Add(plane, &pointOnPlane, 1, out _);
        }

        public NativeArray<float4> AsArray()
        {
            return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<float4>(
                        m_Planes->Ptr, m_Planes->Length, Allocator.None);
        }
    }
}
