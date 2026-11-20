using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // A revolved shape that doesn't go all the way round is closed at its start and its end: its mesh has its shape
    // there, at each
    [TestFixture]
    public class ChiselRevolvedShapeMeshTests
    {
        readonly List<GameObject> roots = new();
        (UnityEngine.SceneManagement.Scene scene, bool wasDirty) remembered;

        [SetUp]
        public void SetUp() { remembered = SceneLeftClean.Remember(); }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in roots)
                if (root) Object.DestroyImmediate(root);
            roots.Clear();
            Update();
            SceneLeftClean.Restore(remembered);
        }

        static void Update()
        {
            for (int i = 0; i < 3; i++)
            {
                ChiselNodeHierarchyManager.Update();
                ChiselModelManager.Instance.UpdateModels();
            }
        }

        static double Area(Vector3 a, Vector3 b, Vector3 c) => math.length(math.cross((double3)(float3)(b - a), (double3)(float3)(c - a))) * 0.5;

        // the area of the drawn triangles in the plane through the up axis at an angle, on the side that angle points to
        static double DrawnAreaAt(ChiselModelComponent model, float degrees)
        {
            var radians = math.radians(degrees);
            var along   = new Vector3(math.cos(radians), 0, -math.sin(radians));
            var normal  = Vector3.Cross(Vector3.up, along);
            double area = 0;
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || !renderable.Valid || !renderable.HasGeometry ||
                    (renderable.query & SurfaceDestinationFlags.Renderable) == 0)
                    continue;
                var mesh      = renderable.sharedMesh;
                var vertices  = mesh.vertices;
                var triangles = mesh.triangles;
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                    if (math.abs(Vector3.Dot(a, normal)) > 1e-3f || math.abs(Vector3.Dot(b, normal)) > 1e-3f || math.abs(Vector3.Dot(c, normal)) > 1e-3f)
                        continue;
                    if (Vector3.Dot(a + b + c, along) <= 0)
                        continue;
                    area += Area(a, b, c);
                }
            }
            return area;
        }

        [Test]
        public void APartRevolvedShape_IsClosedAtItsStartAndEnd()
        {
            var model = new GameObject("model").AddComponent<ChiselModelComponent>();
            roots.Add(model.gameObject);
            var revolved = new GameObject("revolved").AddComponent<ChiselRevolvedShapeComponent>();
            revolved.transform.SetParent(model.transform, false);
            // a 1 by 1 square away from the axis, from 30 degrees round to 120
            revolved.definition.shape = new Curve2D(new[] { new CurveControlPoint2D(1, 0), new CurveControlPoint2D(2, 0), new CurveControlPoint2D(2, 1), new CurveControlPoint2D(1, 1) });
            revolved.definition.settings.revolveSegments = 6;
            revolved.definition.settings.startAngle      = 30;
            revolved.definition.settings.totalAngle      = 90;
            revolved.OnValidate();
            Update();

            Assert.That(DrawnAreaAt(model, 30),  Is.EqualTo(1).Within(1e-3), "its start");
            Assert.That(DrawnAreaAt(model, 120), Is.EqualTo(1).Within(1e-3), "its end");
        }
    }
}
