using System.Runtime.CompilerServices;
using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using System;

namespace Chisel.Core
{
    public struct RefCountedBrushMeshBlob : IDisposable
    {
        public int refCount;
        public BlobAssetReference<BrushMeshBlob> brushMeshBlob;

		public void Dispose()
		{
            refCount = 0;
            if (brushMeshBlob.IsCreated)
                brushMeshBlob.Dispose();
            brushMeshBlob = default;
		}
	}

    internal sealed class ChiselMeshLookup : ScriptableObject
    {
        public class Data
		{
            public NativeParallelHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobCache; // Confirmed to get disposed

			internal void Initialize()
            {
                brushMeshBlobCache = new NativeParallelHashMap<int, RefCountedBrushMeshBlob>(1000, Allocator.Persistent); // Confirmed to get disposed
			}

			public void Dispose()
            {
                // Confirmed to get disposed
                if (brushMeshBlobCache.IsCreated)
                {
                    try
                    {
                        using var items = brushMeshBlobCache.GetValueArray(Allocator.Temp);
                        foreach (var item in items)
                        {
                            if (item.brushMeshBlob.IsCreated)
                            {
                                item.Dispose();
							}
                        }
					}
                    finally
                    {
                        brushMeshBlobCache.Dispose();
                        brushMeshBlobCache = default;
                    }
                }
				// FIXME: temporary hack
				CompactHierarchyManager.Destroy();
			}
        }

        static ChiselMeshLookup instance;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void UpdateValue()
        {
            if (instance == null)
            {
                instance = ScriptableObject.CreateInstance<ChiselMeshLookup>();
                instance.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        public static Data Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (instance == null)
                    UpdateValue();
                return instance.data;
            }
        }

        readonly Data data = new();

        internal void OnEnable() { data.Initialize(); }
        internal void OnDisable() { Dispose(); }
		private void OnDestroy() { Dispose(); }

		public void Dispose()
		{
			// Confirmed to be called
			data.Dispose();
			instance = null;
		}
	}
}
