using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components.Tests
{
    [TestFixture]
    public class ChiselDisplacementTests
    {
        // The top of a box from (0,0,0) to (2,1,2): a grid of 3 by 3 points over it, a cell 1 by 1
        static readonly float4[] kBox =
        {
            new(-1, 0, 0, 0), new(1, 0, 0, -2), new(0, -1, 0, 0), new(0, 1, 0, -1), new(0, 0, -1, 0), new(0, 0, 1, -2)
        };

        static ChiselSurfacePaint Grid(float height)
        {
            var grid = ChiselSurfacePaint.Create(0, Core.ChiselPaintAttribute.Color, Core.ChiselPaintComponents.X, Vector4.zero,
                                                 new Vector3(0, 1, 0), Vector3.right, Vector3.forward, new Vector2Int(3, 3));
            for (int i = 0; i < grid.PointCount; i++)
                grid[i] = new Vector4(height, 0, 0, 0);
            return grid;
        }

        static bool Inside(ChiselDisplacementColumn column, float3 point)
        {
            foreach (var plane in column.planes)
            {
                if (math.dot(plane.xyz, point) + plane.w > 1e-5f)
                    return false;
            }
            return true;
        }

        static int Count(List<ChiselDisplacementColumn> columns, bool additive)
        {
            var count = 0;
            foreach (var column in columns)
                count += column.additive == additive ? 1 : 0;
            return count;
        }

        // A column made directly is the brush cutting it from its planes makes: the same faces, numbered by plane, with
        // the same corners in the same order round them
        [Test]
        public void AColumnMadeDirectly_IsTheOneCutFromItsPlanes()
        {
            var box = new float4[]
            {
                new(-1, 0, 0, 0), new(1, 0, 0, -4), new(0, -1, 0, 0), new(0, 1, 0, -1), new(0, 0, -1, 0), new(0, 0, 1, -4)
            };
            var grid = ChiselSurfacePaint.Create(0, Core.ChiselPaintAttribute.Color, Core.ChiselPaintComponents.X, Vector4.zero,
                                                 new Vector3(0, 1, 0), Vector3.right, Vector3.forward, new Vector2Int(5, 5));
            for (int i = 0; i < grid.PointCount; i++)
                grid[i] = new Vector4(0.1f + 0.37f * ((i * 7) % 5) - 0.6f * (i % 2), 0, 0, 0);
            var columns = new List<ChiselDisplacementColumn>();
            ChiselDisplacement.Columns(grid, Vector3.up, box, columns);

            var direct = 0;
            foreach (var column in columns)
            {
                var mesh = ChiselDisplacement.ColumnMesh(column);
                var bounds = new Bounds();
                bounds.SetMinMax(column.boundsMin, column.boundsMax);
                var surfaces = new Core.ChiselSurfaceArray();
                Core.BrushMeshFactory.CreateFromPlanes(column.planes, bounds, ref surfaces, out var cut);
                if (column.corners != null && ChiselDisplacement.InsideSides(column))
                    direct++;

                Assert.That(mesh.Validate(logErrors: true), Is.True);
                Assert.That(mesh.polygons.Length, Is.EqualTo(cut.polygons.Length));
                foreach (var polygon in mesh.polygons)
                {
                    var other = System.Array.Find(cut.polygons, p => p.descriptionIndex == polygon.descriptionIndex);
                    Assert.That(other.edgeCount, Is.EqualTo(polygon.edgeCount), $"face {polygon.descriptionIndex}");
                    var first = mesh.vertices[mesh.halfEdges[polygon.firstEdge].vertexIndex];
                    var start = -1;
                    for (int k = 0; k < other.edgeCount && start < 0; k++)
                    {
                        if (math.distance(cut.vertices[cut.halfEdges[other.firstEdge + k].vertexIndex], first) < 1e-4f)
                            start = k;
                    }
                    Assert.That(start, Is.Not.EqualTo(-1), $"face {polygon.descriptionIndex} has the corner");
                    for (int k = 0; k < polygon.edgeCount; k++)
                    {
                        var a = mesh.vertices[mesh.halfEdges[polygon.firstEdge + k].vertexIndex];
                        var b = cut.vertices[cut.halfEdges[other.firstEdge + (start + k) % other.edgeCount].vertexIndex];
                        Assert.That(math.distance(a, b), Is.LessThan(1e-4f), $"face {polygon.descriptionIndex}, corner {k}");
                    }
                    Assert.That(math.dot(mesh.planes[System.Array.IndexOf(mesh.polygons, polygon)].xyz,
                                         cut.planes[System.Array.IndexOf(cut.polygons, other)].xyz), Is.GreaterThan(0.9999f));
                }
            }
            Assert.That(direct, Is.GreaterThan(0), "columns away from the sides are made directly");
        }

        [Test]
        public void AFlatGrid_IsOneFlatColumn()
        {
            var columns = new List<ChiselDisplacementColumn>();
            ChiselDisplacement.Columns(Grid(0), Vector3.up, kBox, columns);
            Assert.That(columns.Count, Is.EqualTo(1), "one rectangle of flat cells");
            Assert.That(columns[0].additive, Is.True);
            Assert.That(Inside(columns[0], new float3(1, 1 - ChiselDisplacement.kOverlap * 0.5f, 1)), Is.True, "just under the surface");
            Assert.That(Inside(columns[0], new float3(1, 1 + ChiselDisplacement.kOverlap * 0.5f, 1)), Is.False, "and not above it");
        }

        [Test]
        public void ARaisedGrid_AddsColumnsUpToItsHeight()
        {
            var columns = new List<ChiselDisplacementColumn>();
            ChiselDisplacement.Columns(Grid(0.5f), Vector3.up, kBox, columns);
            Assert.That(Count(columns, true), Is.EqualTo(8), "two triangles for each of the four cells");
            Assert.That(Count(columns, false), Is.Zero, "nothing cut");
            var below = new float3(0.6f, 1.25f, 0.3f);
            var above = new float3(0.6f, 1.55f, 0.3f);
            Assert.That(columns.Exists(c => c.additive && Inside(c, below)), Is.True, "what is raised is inside a column");
            Assert.That(columns.Exists(c => c.additive && Inside(c, above)), Is.False, "nothing above the height");
        }

        [Test]
        public void ALoweredGrid_CutsDownToItsDepth_AndPushesTheOtherSideOutWhereItGetsTooThin([Values(0.4f, 1.3f)] float depth)
        {
            var columns = new List<ChiselDisplacementColumn>();
            ChiselDisplacement.Columns(Grid(-depth), Vector3.up, kBox, columns);
            var cut = columns.FindAll(c => !c.additive);
            Assert.That(cut.Count, Is.EqualTo(8));
            Assert.That(cut.Exists(c => Inside(c, new float3(0.6f, 1 - depth + 0.05f, 0.3f))), Is.True, "cut down to the depth");
            Assert.That(cut.Exists(c => Inside(c, new float3(0.6f, 1 - depth - 0.05f, 0.3f))), Is.False, "and no further");

            var pushed = columns.FindAll(c => c.group == ChiselDisplacement.kPushed);
            if (depth < 1 - ChiselDisplacement.kMinThickness)
            {
                Assert.That(pushed, Is.Empty, "the box is thick enough");
                return;
            }
            Assert.That(pushed.Count, Is.EqualTo(8), "the bottom is pushed out under every cut");
            var underneath = new float3(0.6f, 1 - depth - ChiselDisplacement.kMinThickness * 0.5f, 0.3f);
            Assert.That(pushed.Exists(c => Inside(c, underneath)), Is.True, "solid just below the lowered surface");
        }

        [Test]
        public void ARaisedNeighbour_MovesTheSideItSharesOut()
        {
            // the box's front (+z, plane 5) is raised by 0.25: the top's columns reach out over it
            var columns = new List<ChiselDisplacementColumn>();
            var grid = ChiselSurfacePaint.Create(0, Core.ChiselPaintAttribute.Color, Core.ChiselPaintComponents.X, Vector4.zero,
                                                 new Vector3(0, 1, 0), Vector3.right, Vector3.forward, new Vector2Int(3, 4));
            for (int i = 0; i < grid.PointCount; i++)
                grid[i] = new Vector4(0.5f, 0, 0, 0);
            ChiselDisplacement.Columns(grid, Vector3.up, kBox, columns, (side, point) => side == 5 ? 0.25f : 0);
            var corner = new float3(0.6f, 1.25f, 2.1f);
            Assert.That(columns.Exists(c => Inside(c, corner)), Is.True, "the corner over the raised front is filled");
            Assert.That(columns.Exists(c => Inside(c, new float3(0.6f, 1.25f, 2.3f))), Is.False, "and no further than the front is raised");
        }

        // The grid has points past the box's side (x = 2) that were never painted: the top goes on to the side as it is
        // painted, not bending down towards them
        [Test]
        public void PointsPastTheEdge_TakeTheHeightAtTheEdge()
        {
            var grid = ChiselSurfacePaint.Create(0, Core.ChiselPaintAttribute.Color, Core.ChiselPaintComponents.X, Vector4.zero,
                                                 new Vector3(0, 1, 0), Vector3.right * 0.75f, Vector3.forward * 0.75f, new Vector2Int(4, 4));
            for (int j = 0; j < 4; j++)
                for (int i = 0; i < 3; i++)
                    grid[i, j] = new Vector4(0.5f, 0, 0, 0);
            var columns = new List<ChiselDisplacementColumn>();
            ChiselDisplacement.Columns(grid, Vector3.up, kBox, columns);
            Assert.That(columns.Exists(c => c.additive && Inside(c, new float3(1.9f, 1.4f, 1))), Is.True, "raised right up to the side");
        }

        // One cell over the whole top, raised by 0.5 at x = 0 and lowered by 0.5 at x = 2: the surface is a straight slope
        // through the middle, the raised part and the lowered part meeting where it crosses the top
        [Test]
        public void ACellRaisedAndLowered_IsOneSlope()
        {
            var grid = ChiselSurfacePaint.Create(0, Core.ChiselPaintAttribute.Color, Core.ChiselPaintComponents.X, Vector4.zero,
                                                 new Vector3(0, 1, 0), Vector3.right * 2, Vector3.forward * 2, new Vector2Int(2, 2));
            grid[0, 0] = grid[0, 1] = new Vector4( 0.5f, 0, 0, 0);
            grid[1, 0] = grid[1, 1] = new Vector4(-0.5f, 0, 0, 0);
            var columns = new List<ChiselDisplacementColumn>();
            ChiselDisplacement.Columns(grid, Vector3.up, kBox, columns);
            // at x = 0.5 the slope is 0.25 up, at x = 1.5 0.25 down
            Assert.That(columns.Exists(c => c.additive && Inside(c, new float3(0.5f, 1.2f, 0.1f))), Is.True, "raised up to the slope");
            Assert.That(columns.Exists(c => c.additive && Inside(c, new float3(0.5f, 1.3f, 0.1f))), Is.False, "and no higher");
            Assert.That(columns.Exists(c => !c.additive && c.seenFace == 4 && Inside(c, new float3(1.5f, 0.8f, 0.1f))), Is.True, "cut down to the slope");
            Assert.That(columns.Exists(c => !c.additive && c.seenFace == 4 && Inside(c, new float3(1.5f, 0.7f, 0.1f))), Is.False, "and no lower");
        }

        [Test]
        public void Thickness_IsHowFarTheOtherSideIs()
        {
            var planes = new List<float4>(kBox);
            Assert.That(ChiselDisplacement.Thickness(new float3(1, 1, 1), new float3(0, 1, 0), planes), Is.EqualTo(1).Within(1e-5f));
        }
    }
}
