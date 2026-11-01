using NUnit.Framework;
using UnityEngine;

namespace Chisel.Core.Tests
{
    [TestFixture]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public class ChiselMeshLookupTests
    {
        static readonly System.Reflection.FieldInfo kInstance = typeof(ChiselMeshLookup).GetField("instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        static BrushMeshInstance Box()
        {
            BrushMeshFactory.CreateBox(new Vector3(0, 0, 0), new Vector3(1.5f, 1.25f, 1.75f), out var box);
            var surfaceArray = new ChiselSurfaceArray();
            surfaceArray.EnsureSize(box.polygons.Length);
            for (int i = 0; i < surfaceArray.surfaces.Length; i++)
                surfaceArray.surfaces[i] = new ChiselSurface();
            return BrushMeshInstance.Create(box, surfaceArray);
        }

        // A domain reload forgets the lookup (its statics) but keeps the hidden object, and enables it again: it takes
        // its place again, with the meshes registered before
        [Test]
        public void TheLookup_AfterADomainReload_KeepsItsCache()
        {
            var instance = Box();
            try
            {
                _ = ChiselMeshLookup.Value;
                var lookup = (ChiselMeshLookup)kInstance.GetValue(null);
                Assert.That(lookup, Is.Not.Null);

                kInstance.SetValue(null, null);
                lookup.OnEnable();

                Assert.That(kInstance.GetValue(null), Is.SameAs(lookup), "another lookup took its place");
                Assert.That(instance.Valid, Is.True, "the meshes registered before the reload are gone");
            }
            finally
            {
                instance.Destroy();
            }
        }

        // Only the lookup in use makes a cache: another one, as a reload left one before, has none and doesn't take
        // the one in use down with it
        [Test]
        public void AnotherLookup_HasNoCacheOfItsOwn()
        {
            var instance = Box();
            var other = ScriptableObject.CreateInstance<ChiselMeshLookup>();
            try
            {
                var inUse = kInstance.GetValue(null);
                var data  = typeof(ChiselMeshLookup).GetField("data", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(((ChiselMeshLookup.Data)data.GetValue(other)).brushMeshBlobCache.IsCreated, Is.False, "the other lookup made a cache");
                Object.DestroyImmediate(other);
                other = null;
                Assert.That(kInstance.GetValue(null), Is.SameAs(inUse), "destroying the other lookup dropped the one in use");
                Assert.That(instance.Valid, Is.True, "destroying the other lookup freed the meshes");
            }
            finally
            {
                if (other)
                    Object.DestroyImmediate(other);
                instance.Destroy();
            }
        }
    }
}
