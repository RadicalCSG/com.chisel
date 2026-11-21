using System.Collections.Generic;
using Chisel.Core;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    // An extruded shape's curved side, set smooth, is drawn as one smooth surface, while its corners stay sharp
    [TestFixture]
    public class ChiselShapeSmoothSidesMeshTests
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

        // the normals the drawn meshes have at a position
        static List<Vector3> NormalsAt(ChiselModelComponent model, Vector3 position)
        {
            var normals = new List<Vector3>();
            foreach (var renderable in model.generated.renderables)
            {
                if (renderable == null || !renderable.Valid || !renderable.HasGeometry ||
                    (renderable.query & SurfaceDestinationFlags.Renderable) == 0)
                    continue;
                var mesh     = renderable.sharedMesh;
                var vertices = mesh.vertices;
                var meshNormals = mesh.normals;
                for (int v = 0; v < vertices.Length; v++)
                {
                    if ((vertices[v] - position).sqrMagnitude < 1e-6f)
                        normals.Add(meshNormals[v]);
                }
            }
            return normals;
        }

        // how many directions the normals at a position point, the shape's ends' left out
        static int SideDirectionsAt(ChiselModelComponent model, Vector3 position, Vector3 along)
        {
            var directions = new List<Vector3>();
            foreach (var normal in NormalsAt(model, position))
            {
                if (math.abs(Vector3.Dot(normal, along)) > 0.9f)
                    continue;
                var known = false;
                foreach (var direction in directions)
                    known |= Vector3.Dot(direction, normal) > 0.9999f;
                if (!known)
                    directions.Add(normal);
            }
            return directions.Count;
        }

        [Test]
        public void ACurvedSideSetSmooth_IsDrawnSmooth_AndItsCornersStaySharp()
        {
            var model = new GameObject("model").AddComponent<ChiselModelComponent>();
            roots.Add(model.gameObject);
            // nothing smoothed by the angle between faces (as by default), only by the side
            model.renderSettings.normalSmoothing = false;
            model.SyncModelSettingsStore();
            var extruded = new GameObject("extruded").AddComponent<ChiselExtrudedShapeComponent>();
            extruded.transform.SetParent(model.transform, false);
            // a square, its side from (1,-1) to (1,1) bulging out
            var points = new[] { new CurveControlPoint2D(-1, -1), new CurveControlPoint2D(1, -1), new CurveControlPoint2D(1, 1), new CurveControlPoint2D(-1, 1) };
            points[1].constraint2 = ControlPointConstraint.Broken;
            points[1].tangent2    = new Vector2(-0.5f, -0.5f);
            points[2].constraint1 = ControlPointConstraint.Broken;
            points[2].tangent1    = new Vector2(-0.5f, 0.5f);
            extruded.definition.shape = new Curve2D(points);
            extruded.definition.settings.curveSegments = 8;
            extruded.OnValidate();
            Update();

            // where the curve's two middle faces meet (half way along it), and its first corner, at the shape's start
            var start  = extruded.definition.path.segments[0].ToMatrix();
            var end    = extruded.definition.path.segments[1].ToMatrix();
            var along  = (end.MultiplyPoint(Vector3.zero) - start.MultiplyPoint(Vector3.zero)).normalized;
            var middle = start.MultiplyPoint(new Vector3(1.375f, 0, 0));
            var corner = start.MultiplyPoint(new Vector3(1, -1, 0));
            Assert.That(NormalsAt(model, middle), Is.Not.Empty, "drawn half way along the curve");
            Assert.That(SideDirectionsAt(model, middle, along), Is.EqualTo(2), "not smooth: a face either side");

            extruded.definition.shape.controlPoints[1].smoothSide = true;
            extruded.OnValidate();
            Update();

            Assert.That(SideDirectionsAt(model, middle, along), Is.EqualTo(1), "smooth");
            Assert.That(SideDirectionsAt(model, corner, along), Is.EqualTo(2), "the corner, sharp");
        }
    }
}
