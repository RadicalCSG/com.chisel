using System;
using Chisel.Core;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Components
{
    public struct ChiselColliderObjectUpdate
    {
        public int meshIndex;
    }

    [Serializable, BurstCompile(CompileSynchronously = true)]
    public class ChiselColliderObjects
    {
        public ulong            surfaceParameter;
        public Mesh             sharedMesh;
        public MeshCollider     meshCollider;
        public PhysicsMaterial  physicsMaterial;

        public uint             geometryHashValue;

        private ChiselColliderObjects() { }
        public static ChiselColliderObjects Create(GameObject container, ulong surfaceParameter)
        {
            var physicsMaterial = surfaceParameter == 0 ? null : Resources.EntityIdToObject(UnityEngine.EntityId.FromULong(surfaceParameter)) as PhysicsMaterial;
            var sharedMesh      = new Mesh { name = ChiselGeneratedObjects.kGeneratedMeshColliderName };
            var meshCollider    = container.AddComponent<MeshCollider>();
            var colliderObjects = new ChiselColliderObjects
            {
                surfaceParameter    = surfaceParameter,
                meshCollider        = meshCollider,
                physicsMaterial     = physicsMaterial,
                sharedMesh          = sharedMesh
            };
            colliderObjects.Initialize();
            return colliderObjects;
        }

        public void Destroy()
        {
            ChiselObjectUtility.SafeDestroy(meshCollider);
            ChiselObjectUtility.SafeDestroy(sharedMesh);
            sharedMesh = null;
            meshCollider = null;
            physicsMaterial = null;
        }

        public void DestroyWithUndo()
        {
            ChiselObjectUtility.SafeDestroyWithUndo(meshCollider);
            ChiselObjectUtility.SafeDestroyWithUndo(sharedMesh);
        }

        public void RemoveContainerFlags()
        {
            ChiselObjectUtility.RemoveContainerFlags(meshCollider);
        }

        public bool IsValid()
        {
            if (!sharedMesh ||
                !meshCollider)
                return false;

            return true;
        }

        void Initialize()
        {
            meshCollider.sharedMesh = sharedMesh;
            meshCollider.sharedMaterial = physicsMaterial;
        }

        //*
        [BurstCompile(CompileSynchronously = true)]
        struct BakeColliderJobParallel : IJobParallelFor
        {
            [NoAlias, ReadOnly] public NativeArray<BakeData> bakingSettings;
            public void Execute(int index)
            {
                if (bakingSettings[index].entityID != 0)
                    Physics.BakeMesh(UnityEngine.EntityId.FromULong(bakingSettings[index].entityID), bakingSettings[index].convex, bakingSettings[index].cookingOptions);
            }
        }
        /*/
        [BurstCompile(CompileSynchronously = true)]
        struct BakeColliderJob : IJob
        {
            [NoAlias, ReadOnly] public BakeData bakingSettings;
            public void Execute()
            {
                if (bakingSettings.entityID != 0)
                    Physics.BakeMesh(UnityEngine.EntityId.FromULong(bakingSettings.entityID), bakingSettings.convex, bakingSettings.cookingOptions);
            }
        }
        //*/
        struct BakeData
        {
            public bool                         convex;
            public MeshColliderCookingOptions   cookingOptions;
            public ulong                        entityID;
        }

        //*/
        public static void UpdateProperties(ChiselModelComponent model, ChiselColliderObjects[] colliders)
        {
            var colliderSettings = model.ColliderSettings;
            for (int i = 0; i < colliders.Length; i++)
            {
                var meshCollider = colliders[i].meshCollider;
                if (!meshCollider)
                    continue;

                // If the cookingOptions are not the default values it would force a full slow rebake later, 
                // even if we already did a Bake in a job
                if (meshCollider.cookingOptions != colliderSettings.cookingOptions)
                    meshCollider.cookingOptions = colliderSettings.cookingOptions;

                if (meshCollider.convex != colliderSettings.convex)
                    meshCollider.convex = colliderSettings.convex;
                if (meshCollider.isTrigger != colliderSettings.isTrigger)
                    meshCollider.isTrigger = colliderSettings.isTrigger;

                var sharedMesh = colliders[i].sharedMesh;
                var expectedEnabled = sharedMesh.vertexCount > 0;
                if (meshCollider.enabled != expectedEnabled)
                    meshCollider.enabled = expectedEnabled;
            }
        }

        const Allocator defaultAllocator = Allocator.TempJob;

        public static bool DeferBaking { get; set; }

        struct DeferredBake
        {
            public ChiselModelComponent   model;
            public ChiselColliderObjects[] colliders;
        }
        static readonly System.Collections.Generic.List<DeferredBake> s_DeferredBakes = new();

        public static void ScheduleColliderBake(ChiselModelComponent model, ChiselColliderObjects[] colliders)
        {
            if (DeferBaking)
            {
                // Only the most recent state of a model matters; an earlier deferred bake for it is stale.
                for (int i = 0; i < s_DeferredBakes.Count; i++)
                {
                    if (s_DeferredBakes[i].model != model)
                        continue;
                    s_DeferredBakes[i] = new DeferredBake { model = model, colliders = colliders };
                    return;
                }
                s_DeferredBakes.Add(new DeferredBake { model = model, colliders = colliders });
                return;
            }
            BakeColliders(model, colliders);
        }

        // Must be called once an interaction ends, and before anything relies on the colliders being
        // up to date (entering play mode, for instance).
        public static void FlushDeferredBakes()
        {
            if (s_DeferredBakes.Count == 0)
                return;
            var deferred = s_DeferredBakes.ToArray();
            s_DeferredBakes.Clear();
            for (int i = 0; i < deferred.Length; i++)
            {
                if (deferred[i].model == null || deferred[i].colliders == null)
                    continue;
                BakeColliders(deferred[i].model, deferred[i].colliders);
            }
        }

        static void BakeColliders(ChiselModelComponent model, ChiselColliderObjects[] colliders)
        {
            var colliderSettings = model.ColliderSettings;
            //*
            // TODO: find all the entityIDs before we start doing CSG, then we can do the Bake's in the same job that sets the meshes
            //          hopefully that will make it easier for Unity to not screw up the scheduling
            var bakingSettings = new NativeArray<BakeData>(colliders.Length, defaultAllocator);
            for (int i = 0; i < colliders.Length; i++)
            {
                // A deferred bake can outlive what it referred to, so nothing here may assume the
                // collider or its mesh still exists.
                var meshCollider = colliders[i] == null ? null : colliders[i].meshCollider;
                var sharedMesh   = colliders[i] == null ? null : colliders[i].sharedMesh;
                if (!meshCollider || !sharedMesh)
                {
                    bakingSettings[i] = new BakeData
                    {
                        entityID = 0
                    };
                    continue;
                }

                bakingSettings[i] = new BakeData
                {
                    convex          = colliderSettings.convex,
                    cookingOptions  = colliderSettings.cookingOptions,
                    entityID        = UnityEngine.EntityId.ToULong(sharedMesh.GetEntityId())
                };
            }
            //*
            var bakeColliderJob = new BakeColliderJobParallel
            {
                bakingSettings = bakingSettings
            };
            // WHY ARE ALL OF THESE JOBS SEQUENTIAL ON A SINGLE WORKER THREAD?
            var allJobHandles = bakeColliderJob.Schedule(colliders.Length, 1);
            /*/
            var allJobHandles = default(JobHandle);
            for (int i = 0; i < bakingSettings.Length; i++)
            {
                var bakeColliderJob = new BakeColliderJob
                {
                    bakingSettings = bakingSettings[i]
                };
                var jobHandle = bakeColliderJob.Schedule();
                allJobHandles = JobHandle.CombineDependencies(allJobHandles, jobHandle);
            }
            //*/

            var disposeJob = bakingSettings.Dispose(allJobHandles);
            bakingSettings = default;

            allJobHandles.Complete();
            disposeJob.Complete(); 

			//*/
			//*
			// TODO: is there a way to defer forcing the collider to update?
			for (int i = 0; i < colliders.Length; i++)
            {
                var meshCollider = colliders[i] == null ? null : colliders[i].meshCollider;
                if (!meshCollider)
                    continue;

                meshCollider.sharedMesh = colliders[i].sharedMesh;
            }//*/
        }
    }
}