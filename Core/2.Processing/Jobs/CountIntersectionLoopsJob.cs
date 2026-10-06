using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;
using Unity.Entities;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct CountIntersectionLoopsJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<BrushPair2>                           uniqueBrushPairs;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>    brushMeshLookup;

        // Write
        [NoAlias, WriteOnly] public NativeReference<int> intersectionLoopCountRef;

        readonly int FaceCountOf(int nodeOrder)
        {
            if (nodeOrder < 0 || nodeOrder >= brushMeshLookup.Length)
                return 0;
            var brushMesh = brushMeshLookup[nodeOrder];
            // A brush without a mesh has no faces, so it makes no loops - see 879a890, which is where the rest of
            // the pipeline learned to expect one.
            if (!brushMesh.IsCreated)
                return 0;
            return brushMesh.Value.localPlaneCount;
        }

        public void Execute()
        {
            var total = 0;
            for (int i = 0; i < uniqueBrushPairs.Length; i++)
            {
                var pair = uniqueBrushPairs[i];
                if (pair.type == IntersectionType.InvalidValue)
                    continue;
                total += FaceCountOf(pair.brushIndexOrder0.nodeOrder) +
                         FaceCountOf(pair.brushIndexOrder1.nodeOrder);
            }
            intersectionLoopCountRef.Value = total;
        }
    }
}
