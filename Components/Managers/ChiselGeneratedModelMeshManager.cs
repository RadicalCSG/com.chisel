using System;
using System.Collections.Generic;
using Chisel.Core;
using Unity.Jobs;
using UnityEngine.Profiling;

namespace Chisel.Components
{
    public static class ChiselGeneratedModelMeshManager
    {
        public static event Action              PreReset;
        public static event Action              PostReset;
        public static event Action<ChiselModelComponent> PostUpdateModel;
        public static event Action              PostUpdateModels;
        
        internal static HashSet<ChiselNode>         s_RegisteredNodeLookup = new();

        // TODO: should not be public
        public static List<ChiselModelComponent>    s_RegisteredModels = new();

        static readonly ChiselGeneratedComponentManager componentGenerator = new();
        
        internal static void Reset()
        {
            PreReset?.Invoke();

            s_RegisteredNodeLookup.Clear();
            s_RegisteredModels.Clear();


            PostReset?.Invoke();
        }

        internal static void Unregister(ChiselNode node)
        {
            if (!s_RegisteredNodeLookup.Remove(node))
                return;

            var model = node as ChiselModelComponent;
            if (!ReferenceEquals(model, null))
            {
                componentGenerator.Unregister(model);
                s_RegisteredModels.Remove(model);
            }
        }

        internal static void Register(ChiselNode node)
        {
            if (!s_RegisteredNodeLookup.Add(node))
                return;

            var model = node as ChiselModelComponent;
            if (ReferenceEquals(model, null))
                return;
            
            s_RegisteredModels.Add(model);
            componentGenerator.Register(model);
        }

        static int FinishMeshUpdates(CSGTree tree, ChiselMeshUpdates meshUpdates, JobHandle dependencies)
        {
            ChiselModelComponent model = null;
            for (int m = 0; m < s_RegisteredModels.Count; m++)
            {
                if (!s_RegisteredModels[m])
                    continue;

                if (s_RegisteredModels[m].Node == tree)
                    model = s_RegisteredModels[m];
            }

            if (model == null)
            {
                if (meshUpdates.meshDataArray.Length > 0) meshUpdates.meshDataArray.Dispose();
				meshUpdates.meshDataArray = default;
                return 0;
            }

            if (!ChiselGeneratedObjects.IsValid(model.generated))
            {
                model.generated?.Destroy();
                model.generated = ChiselGeneratedObjects.Create(model.gameObject);
            }

            var count = model.generated.FinishMeshUpdates(model, meshUpdates, dependencies);
            componentGenerator.Rebuild(model);
            PostUpdateModel?.Invoke(model);
            return count;
        }
        
        static readonly FinishMeshUpdate s_FinishMeshUpdates = (FinishMeshUpdate)FinishMeshUpdates;

        public static void UpdateModels()
        {

            // Update the tree meshes
            Profiler.BeginSample("Flush");
            try
            {
                if (!CompactHierarchyManager.Flush(s_FinishMeshUpdates))
                {
                    ChiselGeneratedComponentManager.DelayedUVGeneration();
                    return; // Nothing to update ..
                }
            }
            finally
            {
                Profiler.EndSample();
            }

            {
                Profiler.BeginSample("PostUpdateModels");
                PostUpdateModels?.Invoke();
                Profiler.EndSample();
            }
        }
    }
}
