using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // A surface found by picking gives the plane facing the viewer, as the surface is drawn: seen from inside a brush (a
    // subtractive brush's walls), its plane is turned around
    [TestFixture]
    public sealed class ChiselPickingPlaneTests
    {
        readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i]) Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }

        static void UpdateModels()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        // The box's brush and the index of its face whose plane points along +z, with where that face lies
        bool BoxFacingPlusZ(out ChiselBoxComponent box, out CSGTreeBrush brush, out int surfaceIndex, out float faceZ)
        {
            var model = new GameObject("Model").AddComponent<ChiselModelComponent>();
            created.Add(model.gameObject);
            box = new GameObject("Box").AddComponent<ChiselBoxComponent>();
            box.transform.SetParent(model.transform, false);
            UpdateModels();

            brush = default;
            surfaceIndex = -1;
            faceZ = 0;
            var brushes = new List<CSGTreeBrush>();
            ChiselGeneratorParts.BrushesOf(box, brushes);
            if (brushes.Count != 1)
                return false;
            brush = brushes[0];
            ref var mesh = ref BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh).Value;
            for (int p = 0; p < mesh.localPlanes.Length; p++)
            {
                if (mesh.localPlanes[p].z > 0.99f)
                {
                    surfaceIndex = p;
                    faceZ = -mesh.localPlanes[p].w;
                    return true;
                }
            }
            return false;
        }

        [Test]
        public void ASurfaceSeenFromOutside_KeepsItsPlane()
        {
            Assert.That(BoxFacingPlusZ(out var box, out var brush, out var surfaceIndex, out var faceZ), Is.True);
            var intersection = ChiselSelectionManager.PickedIntersection(box.gameObject, brush, surfaceIndex,
                                                                          worldCameraOrigin: new Vector3(0, 0, faceZ + 5),
                                                                          worldIntersectionPos: new Vector3(0, 0, faceZ));
            Assert.That(intersection.worldPlane.normal.z, Is.EqualTo(1).Within(1e-4f));
        }

        [Test]
        public void ASurfaceSeenFromInside_FacesTheViewer()
        {
            Assert.That(BoxFacingPlusZ(out var box, out var brush, out var surfaceIndex, out var faceZ), Is.True);
            var intersection = ChiselSelectionManager.PickedIntersection(box.gameObject, brush, surfaceIndex,
                                                                          worldCameraOrigin: Vector3.zero,
                                                                          worldIntersectionPos: new Vector3(0, 0, faceZ));
            Assert.That(intersection.worldPlane.normal.z, Is.EqualTo(-1).Within(1e-4f));
            Assert.That(intersection.worldPlaneIntersection.z, Is.EqualTo(faceZ).Within(1e-4f));
        }
    }
}
