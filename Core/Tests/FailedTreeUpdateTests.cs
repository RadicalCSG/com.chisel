using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class FailedTreeUpdateTests
    {
        // Two trees updated together, the second failing as it is initialized (its decals can't be copied): the first
        // one's temporaries, and what the second had made, are freed all the same
        [Test]
        public void ATreeThatFailsToInitialize_LeavesNothingAllocated()
        {
            BrushMeshFactory.CreateBox(new Vector3(0, 0, 0), new Vector3(1.25f, 1.5f, 1.75f), out var box);
            var surfaceArray = new ChiselSurfaceArray();
            surfaceArray.EnsureSize(box.polygons.Length);
            for (int i = 0; i < surfaceArray.surfaces.Length; i++)
                surfaceArray.surfaces[i] = new ChiselSurface();
            var instance = BrushMeshInstance.Create(box, surfaceArray);
            try
            {
                void Round()
                {
                    var good = CSGTree.Create(default(UnityEngine.EntityId));
                    var bad  = CSGTree.Create(default(UnityEngine.EntityId));
                    var a = good.CreateBrush(brushMesh: instance);
                    var b = bad.CreateBrush(brushMesh: instance);
                    var data = ChiselTreeLookup.Value[bad];
                    data.decalVolumes.Dispose();
                    try
                    {
                        LogAssert.Expect(LogType.Exception, new Regex("."));
                        LogAssert.Expect(LogType.Error, new Regex("CSG mesh update failed"));
                        CompactHierarchyManager.Flush(null);
                    }
                    finally
                    {
                        data.decalVolumes = new NativeList<DecalVolume>(16, Allocator.Persistent);
                        a.Destroy(); b.Destroy();
                        good.Destroy(); bad.Destroy();
                    }
                }

                // the caches that grow once are made first
                Round();
                Round();
                UnsafeUtility.CheckForLeaks();
                Round();
                Assert.That(UnsafeUtility.CheckForLeaks(), Is.Zero, "a failed update left allocations behind");
            }
            finally
            {
                instance.Destroy();
            }
        }
    }
}
