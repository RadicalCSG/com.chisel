﻿using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    unsafe struct UpdateBrushMeshIDsJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public CompactHierarchy.ReadOnly compactHierarchy;
        [NoAlias, ReadOnly] public NativeParallelHashMap<int, RefCountedBrushMeshBlob>  brushMeshBlobs;
        [NoAlias, ReadOnly] public int                       brushCount;
        [NoAlias, ReadOnly] public NativeList<CompactNodeID> brushes;

        // Read/Write
        [NoAlias] public NativeParallelHashSet<int>          allKnownBrushMeshIndices;
        [NoAlias] public NativeArray<ChiselLayerParameters>  parameters;
        [NoAlias] public NativeArray<int>                    parameterCounts;

        // Write
        [NoAlias, WriteOnly] public NativeArray<int>         allBrushMeshIDs;

        public void Execute()
        {
            Debug.Assert(parameters.Length == SurfaceDestinationParameters.ParameterCount);
            Debug.Assert(parameterCounts.Length == SurfaceDestinationParameters.ParameterCount);
            Debug.Assert(SurfaceDestinationParameters.kSurfaceDestinationParameterFlagMask.Length == SurfaceDestinationParameters.ParameterCount);

            var capacity = math.max(1, math.max(allKnownBrushMeshIndices.Count(), brushCount));
            
            NativeParallelHashSet<int> removeBrushMeshIndices;
			using var _removeBrushMeshIndices = removeBrushMeshIndices = new NativeParallelHashSet<int>(capacity, Allocator.Temp);
            for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
            {
                var brushCompactNodeID = brushes[nodeOrder];
                int brushMeshHash = -1;
                if (!compactHierarchy.IsValidCompactNodeID(brushCompactNodeID) ||
                    // NOTE: Assignment is intended, this is not supposed to be a comparison
                    (brushMeshHash = compactHierarchy.GetBrushMeshID(brushCompactNodeID)) == 0)
                {
                    // The brushMeshID is invalid: a Generator created/didn't update a TreeBrush correctly, or the input values didn't produce a valid mesh (for example; 0 height cube)
                    Debug.LogError($"Brush with ID ({brushCompactNodeID}) has its brushMeshID set to ({brushMeshHash}), which is invalid.");
                    allBrushMeshIDs[nodeOrder] = 0;
                } else
                {
                    allBrushMeshIDs[nodeOrder] = brushMeshHash;

                    if (removeBrushMeshIndices.Add(brushMeshHash))
                        allKnownBrushMeshIndices.Remove(brushMeshHash);
                    else
                        allKnownBrushMeshIndices.Add(brushMeshHash);
                } 
            }

            // TODO: optimize all of this, especially slow for complete update

            // Regular index operator will return a copy instead of a reference *sigh* 
            var parameterPtr = (ChiselLayerParameters*)parameters.GetUnsafePtr();
            
            using var brushMeshIndicesArray = allKnownBrushMeshIndices.ToNativeArray(Allocator.Temp); // NativeHashSet iterator is broken, so need to copy it to an array *sigh*
            foreach (int brushMeshHash in brushMeshIndicesArray)
            {
                //if (removeBrushMeshIndices.Contains(brushMeshHash)) 
                //    continue;
                    
                if (!brushMeshBlobs.ContainsKey(brushMeshHash))
                    continue;

                ref var polygons = ref brushMeshBlobs[brushMeshHash].brushMeshBlob.Value.polygons;
                for (int p = 0; p < polygons.Length; p++)
                {
                    ref var polygon = ref polygons[p];
                    var destinationFlags = polygon.surface.destinationFlags;
                    for (int l = 0; l < SurfaceDestinationParameters.ParameterCount; l++)
                    {
                        var surfaceDestinationMask = SurfaceDestinationParameters.kSurfaceDestinationParameterFlagMask[l];
                        if ((destinationFlags & surfaceDestinationMask) != 0) parameterPtr[l].RegisterParameter(polygon.surface.parameters.parameters[l]);
                    }
                }
            }

            //foreach (int brushMeshHash in removeBrushMeshIndices)
            //    allKnownBrushMeshIndices.Remove(brushMeshHash);

            for (int l = 0; l < SurfaceDestinationParameters.ParameterCount; l++)
                parameterCounts[l] = parameterPtr[l].uniqueParameterCount;
        }
    }
}
