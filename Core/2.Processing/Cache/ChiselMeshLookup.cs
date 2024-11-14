using System.Runtime.CompilerServices;
using UnityEngine;
using Unity.Collections;
using Unity.Entities;

namespace Chisel.Core
{
    public struct RefCountedBrushMeshBlob
    {
        public int refCount;
        public BlobAssetReference<BrushMeshBlob> brushMeshBlob;
    }

    internal sealed class ChiselMeshLookup : ScriptableObject
    {
        public class Data
		{
            public NativeParallelHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobCache;

            internal void Initialize()
            {
                brushMeshBlobCache = new NativeParallelHashMap<int, RefCountedBrushMeshBlob>(1000, Allocator.Persistent);
			}

			public void Dispose()
            {
                if (brushMeshBlobCache.IsCreated)
                {
                    try
                    {
                        using var items = brushMeshBlobCache.GetValueArray(Allocator.Persistent);                        
                        foreach (var item in items)
                        {
                            if (item.brushMeshBlob.IsCreated)
                                item.brushMeshBlob.Dispose();
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
			data.Dispose();
			instance = null;
		}
	}
}
