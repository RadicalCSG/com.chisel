using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Chisel.Core
{
    // The outline of the shape a node makes on its own: the brushes under it combined by their operations, as if nothing
    // else were in the tree. It is the edges where its surface turns, so a shape built from several brushes is drawn as
    // one: the cuts between its brushes, and the edges inside a flat face several of them share, are left out.
    //
    // Each face of each brush is cut into pieces where the other brushes' planes cross it, so every piece is either
    // inside or outside each of them. A piece is on the surface when the shape is solid on one side of it and not on
    // the other (tested just in front and just behind its middle), and is turned to face out of the solid. The outline
    // is the pieces' edges, without the parts that two pieces in one plane share; an edge two pieces at an angle share
    // (a crease) is kept once.
    public static class ChiselShapeOutline
    {
        // how far in front and behind a piece the shape is tested, and how near a plane a point is on it
        const double kOffset    = 1e-4;
        const double kOnPlane   = 1e-5;
        const double kCoplanar  = 2e-5;
        // the grid lines are matched on: a line's direction, and its point nearest the origin
        const double kDirectionGrid = 1e4;
        const double kPointGrid     = 1e4;

        internal sealed class Brush
        {
            public double3[] vertices;
            // each polygon's corners, counter-clockwise seen from outside
            public int[][]   polygons;
            // each polygon's plane, pointing out: dot(xyz, point) + w
            public double4[] planes;
            public double3   min, max;
        }

        // A brush (brush >= 0) or a branch of items, combined in order by their operations
        internal struct Item
        {
            public int              brush;
            public CSGOperationType operation;
            public int              firstChild;
            public int              childCount;
        }

        internal sealed class Shape
        {
            public readonly List<Brush> brushes = new();
            // the first is the node itself; a branch's children follow one another
            public readonly List<Item>  items   = new();

            public int AddBrush(double3[] vertices, int[][] polygons)
            {
                var brush = new Brush { vertices = vertices };
                brush.min = vertices[0];
                brush.max = vertices[0];
                var middle = double3.zero;
                for (int v = 0; v < vertices.Length; v++) { brush.min = math.min(brush.min, vertices[v]); brush.max = math.max(brush.max, vertices[v]); middle += vertices[v]; }
                middle /= vertices.Length;
                var kept   = new List<int[]>(polygons.Length);
                var planes = new List<double4>(polygons.Length);
                for (int p = 0; p < polygons.Length; p++)
                {
                    // Newell's normal, so a turned or scaled brush keeps exact planes
                    var polygon = polygons[p];
                    var normal  = double3.zero;
                    var center  = double3.zero;
                    var longest = 0.0;
                    for (int c = 0; c < polygon.Length; c++)
                    {
                        var a = vertices[polygon[c]];
                        var b = vertices[polygon[(c + 1) % polygon.Length]];
                        normal += math.cross(a, b);
                        center += a;
                        longest = math.max(longest, math.distance(a, b));
                    }
                    // a face without width (a box with a side folded to an edge, give or take rounding) bounds nothing:
                    // its plane would only be the rounding's. Its width is twice its area over its longest edge
                    if (longest <= 0 || math.length(normal) / longest <= kOnPlane)
                        continue;
                    normal = math.normalize(normal);
                    center /= polygon.Length;
                    // pointing away from the brush's middle, whichever way round its corners go (or a mirroring
                    // transformation turned them)
                    if (math.dot(normal, middle - center) > 0)
                    {
                        normal = -normal;
                        System.Array.Reverse(polygon);
                    }
                    kept.Add(polygon);
                    planes.Add(new double4(normal, -math.dot(normal, center)));
                }
                brush.polygons = kept.ToArray();
                brush.planes   = planes.ToArray();
                brushes.Add(brush);
                return brushes.Count - 1;
            }
        }

        // The node's brushes and branches, in its own space
        internal static Shape Gather(CSGTreeNode node)
        {
            var shape  = new Shape();
            var toNode = node.TreeToNodeSpaceMatrix;
            shape.items.Add(default);
            shape.items[0] = GatherItem(shape, node, toNode);
            return shape;
        }

        static Item GatherItem(Shape shape, CSGTreeNode node, float4x4 toNode)
        {
            var item = new Item { brush = -1, operation = node.Operation };
            if (node.Type == CSGNodeType.Brush)
            {
                var brush = (CSGTreeBrush)node;
                var mesh  = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
                if (!mesh.IsCreated)
                    return item;
                ref var blob     = ref mesh.Value;
                var matrix       = math.mul(toNode, brush.NodeToTreeSpaceMatrix);
                var vertices     = new double3[blob.localVertices.Length];
                for (int v = 0; v < vertices.Length; v++)
                    vertices[v] = math.mul(matrix, new float4(blob.localVertices[v], 1)).xyz;
                var polygons     = new List<int[]>(blob.polygons.Length);
                for (int p = 0; p < blob.polygons.Length; p++)
                {
                    var polygon = blob.polygons[p];
                    if (polygon.edgeCount < 3)
                        continue;
                    var corners = new int[polygon.edgeCount];
                    for (int c = 0; c < corners.Length; c++)
                        corners[c] = blob.halfEdges[polygon.firstEdge + c].vertexIndex;
                    polygons.Add(corners);
                }
                if (vertices.Length >= 4 && polygons.Count >= 4)
                    item.brush = shape.AddBrush(vertices, polygons.ToArray());
                return item;
            }
            // the children one after another, then each of theirs
            var count = node.Count;
            item.firstChild = shape.items.Count;
            item.childCount = count;
            for (int c = 0; c < count; c++)
                shape.items.Add(default);
            for (int c = 0; c < count; c++)
                shape.items[item.firstChild + c] = GatherItem(shape, node[c], toNode);
            return item;
        }

        // What the node's shape is made of, so it's only worked out again when that changes: its brushes' meshes and
        // operations, how they're grouped, and where each is in what it's in (not where the node itself is: moving it
        // doesn't change its shape)
        public static uint Hash(CSGTreeNode node)
        {
            if (!node.Valid)
                return 0;
            return HashOf(node, 1, isTop: true);
        }

        static uint HashOf(CSGTreeNode node, uint hash, bool isTop)
        {
            hash = math.hash(new uint3(hash, (uint)node.Operation, (uint)node.Type));
            if (!isTop)
                hash = math.hash(new uint2(hash, math.hash(node.LocalTransformation)));
            if (node.Type == CSGNodeType.Brush)
                return math.hash(new uint2(hash, (uint)((CSGTreeBrush)node).BrushMesh.BrushMeshID));
            var count = node.Count;
            hash = math.hash(new uint2(hash, (uint)count));
            for (int c = 0; c < count; c++)
                hash = HashOf(node[c], hash, isTop: false);
            return hash;
        }

        // The outline of the node's shape on its own, in its own space: corners, and pairs of them a line each
        public static void Build(CSGTreeNode node, List<float3> vertices, List<int> lines)
        {
            vertices.Clear();
            lines.Clear();
            if (!node.Valid)
                return;
            Build(Gather(node), vertices, lines);
        }

        #region Surface pieces
        struct BrushRange
        {
            public int     firstVertex, vertexCount;
            public int     firstPolygon, polygonCount;
            public double3 min, max;
        }

        // What is solid at a point, as a program run on a stack: a brush pushes whether the point is in it (-1: never),
        // an operation combines the two values on top, the one before with the one after
        enum Operation : byte { Nothing, Brush, Additive, Subtractive, Intersecting }

        struct Instruction
        {
            public Operation operation;
            public int       brush;
        }

        static Operation Combine(CSGOperationType operation)
        {
            switch (operation)
            {
                case CSGOperationType.Subtractive:  return Operation.Subtractive;
                case CSGOperationType.Intersecting: return Operation.Intersecting;
                default:                            return Operation.Additive;
            }
        }

        static void Emit(Shape shape, int index, List<Instruction> program)
        {
            var item = shape.items[index];
            if (item.brush >= 0 || item.childCount == 0)
            {
                program.Add(new Instruction { operation = Operation.Brush, brush = item.brush });
                return;
            }
            program.Add(new Instruction { operation = Operation.Nothing });
            for (int c = 0; c < item.childCount; c++)
            {
                Emit(shape, item.firstChild + c, program);
                program.Add(new Instruction { operation = Combine(shape.items[item.firstChild + c].operation) });
            }
        }

        [BurstCompile]
        struct SurfacePiecesJob : IJob
        {
            [ReadOnly] public NativeArray<double3>     vertices;
            [ReadOnly] public NativeArray<int>         corners;
            // each polygon's corners in corners: first, count
            [ReadOnly] public NativeArray<int2>        polygons;
            [ReadOnly] public NativeArray<double4>     planes;
            [ReadOnly] public NativeArray<BrushRange>  brushes;
            [ReadOnly] public NativeArray<Instruction> program;
            // When the shape is one branch of brushes, as most generators are, its children's brushes and operations
            // in order: what is solid near a face only depends on the ones near it. One that isn't near is never what
            // the point is in: an additive or subtractive one changes nothing then, an intersecting one leaves nothing
            [ReadOnly] public NativeArray<Instruction> children;
            public bool                                oneBranch;

            public NativeList<double3>                 pieceCorners;
            public NativeList<int2>                    pieceRanges;
            public NativeList<double4>                 piecePlanes;

            static double Distance(double4 plane, double3 point) { return math.dot(plane.xyz, point) + plane.w; }

            static bool Overlaps(double3 minA, double3 maxA, double3 minB, double3 maxB)
            {
                return math.all(minA <= maxB + kOffset) && math.all(minB <= maxA + kOffset);
            }

            bool InsideBrush(int index, double3 point)
            {
                if (index < 0)
                    return false;
                var brush = brushes[index];
                if (math.any(point < brush.min) || math.any(point > brush.max))
                    return false;
                for (int p = brush.firstPolygon; p < brush.firstPolygon + brush.polygonCount; p++)
                {
                    if (Distance(planes[p], point) >= 0)
                        return false;
                }
                return true;
            }

            bool Solid(NativeArray<Instruction> instructions, int count, NativeArray<bool> stack, double3 point)
            {
                var top = 0;
                for (int i = 0; i < count; i++)
                {
                    var instruction = instructions[i];
                    switch (instruction.operation)
                    {
                        case Operation.Nothing:      stack[top++] = false; break;
                        case Operation.Brush:        stack[top++] = InsideBrush(instruction.brush, point); break;
                        case Operation.Additive:     top--; stack[top - 1] = stack[top - 1] || stack[top]; break;
                        case Operation.Subtractive:  top--; stack[top - 1] = stack[top - 1] && !stack[top]; break;
                        case Operation.Intersecting: top--; stack[top - 1] = stack[top - 1] && stack[top]; break;
                    }
                }
                return top > 0 && stack[0];
            }

            // Wider than rounding: a piece no wider than that, as a cut along a plane can leave, lies on that plane,
            // where what is in front of or behind it can't be told apart. Its width is twice its area over its longest edge
            static bool HasArea(NativeList<double3> polygon, int first, int count)
            {
                if (count < 3)
                    return false;
                var normal  = double3.zero;
                var longest = 0.0;
                for (int c = 0; c < count; c++)
                {
                    var a = polygon[first + c];
                    var b = polygon[first + (c + 1) % count];
                    normal += math.cross(a, b);
                    longest = math.max(longest, math.distance(a, b));
                }
                return longest > 0 && math.length(normal) / longest > kOnPlane;
            }

            static bool OnPlane(NativeList<double3> polygon, double4 plane)
            {
                for (int c = 0; c < polygon.Length; c++)
                {
                    if (math.abs(Distance(plane, polygon[c])) > kCoplanar)
                        return false;
                }
                return true;
            }

            // The polygon cut by the plane: the part in front of it and the part behind it (either can be empty)
            static void Split(NativeList<double3> polygon, double4 plane, NativeList<double3> front, NativeList<double3> back)
            {
                front.Clear();
                back.Clear();
                for (int c = 0; c < polygon.Length; c++)
                {
                    var a  = polygon[c];
                    var b  = polygon[(c + 1) % polygon.Length];
                    var da = Distance(plane, a);
                    var db = Distance(plane, b);
                    if (da >  kOnPlane) front.Add(a);
                    else if (da < -kOnPlane) back.Add(a);
                    else { front.Add(a); back.Add(a); }
                    if ((da > kOnPlane && db < -kOnPlane) || (da < -kOnPlane && db > kOnPlane))
                    {
                        var cut = a + (b - a) * (da / (da - db));
                        front.Add(cut);
                        back.Add(cut);
                    }
                }
                if (!HasArea(front, 0, front.Length)) front.Clear();
                if (!HasArea(back,  0, back.Length))  back.Clear();
            }

            static void Append(NativeList<double3> from, int first, int count, NativeList<double3> to, NativeList<int2> ranges)
            {
                ranges.Add(new int2(to.Length, count));
                for (int c = 0; c < count; c++)
                    to.Add(from[first + c]);
            }

            // The pieces of a face's piece outside the brush, and the one inside it
            void SplitBy(NativeList<double3> cells, int2 cell, BrushRange brush, NativeList<double3> result, NativeList<int2> resultRanges,
                         NativeList<double3> remaining, NativeList<double3> front, NativeList<double3> back)
            {
                // all of it outside one of the brush's planes, or nowhere outside any: it stays whole
                var crossed = false;
                for (int p = brush.firstPolygon; p < brush.firstPolygon + brush.polygonCount; p++)
                {
                    double lowest = double.MaxValue, highest = double.MinValue;
                    for (int c = 0; c < cell.y; c++)
                    {
                        var distance = Distance(planes[p], cells[cell.x + c]);
                        lowest  = math.min(lowest, distance);
                        highest = math.max(highest, distance);
                    }
                    if (lowest > kOnPlane)
                    {
                        Append(cells, cell.x, cell.y, result, resultRanges);
                        return;
                    }
                    crossed |= highest > kOnPlane && lowest < -kOnPlane;
                }
                if (!crossed)
                {
                    Append(cells, cell.x, cell.y, result, resultRanges);
                    return;
                }

                remaining.Clear();
                for (int c = 0; c < cell.y; c++)
                    remaining.Add(cells[cell.x + c]);
                for (int p = brush.firstPolygon; p < brush.firstPolygon + brush.polygonCount; p++)
                {
                    if (OnPlane(remaining, planes[p]))
                        continue;
                    Split(remaining, planes[p], front, back);
                    if (front.Length > 0)
                        Append(front, 0, front.Length, result, resultRanges);
                    if (back.Length == 0)
                        return;
                    remaining.Clear();
                    for (int c = 0; c < back.Length; c++)
                        remaining.Add(back[c]);
                }
                Append(remaining, 0, remaining.Length, result, resultRanges);
            }

            // Whether the brush comes as near a face's plane as the shape is tested in front of and behind it: one
            // further away can't cut the face, nor be where it's tested
            bool NearPlane(BrushRange brush, double4 plane)
            {
                double lowest = double.MaxValue, highest = double.MinValue;
                for (int v = brush.firstVertex; v < brush.firstVertex + brush.vertexCount; v++)
                {
                    var distance = Distance(plane, vertices[v]);
                    lowest  = math.min(lowest, distance);
                    highest = math.max(highest, distance);
                }
                return lowest <= 2 * kOffset && highest >= -2 * kOffset;
            }

            public void Execute()
            {
                var cells        = new NativeList<double3>(64, Allocator.Temp);
                var cellRanges   = new NativeList<int2>(16, Allocator.Temp);
                var next         = new NativeList<double3>(64, Allocator.Temp);
                var nextRanges   = new NativeList<int2>(16, Allocator.Temp);
                var remaining    = new NativeList<double3>(16, Allocator.Temp);
                var front        = new NativeList<double3>(16, Allocator.Temp);
                var back         = new NativeList<double3>(16, Allocator.Temp);
                var faceProgram  = new NativeList<Instruction>(children.Length * 2 + 1, Allocator.Temp);
                var stack        = new NativeArray<bool>(math.max(program.Length, children.Length * 2 + 1) + 1, Allocator.Temp);
                for (int i = 0; i < brushes.Length; i++)
                {
                    var brush = brushes[i];
                    for (int p = brush.firstPolygon; p < brush.firstPolygon + brush.polygonCount; p++)
                    {
                        var plane   = planes[p];
                        var polygon = polygons[p];
                        cells.Clear();
                        cellRanges.Clear();
                        var min = vertices[corners[polygon.x]];
                        var max = min;
                        for (int c = 0; c < polygon.y; c++)
                        {
                            var corner = vertices[corners[polygon.x + c]];
                            cells.Add(corner);
                            min = math.min(min, corner);
                            max = math.max(max, corner);
                        }
                        if (!HasArea(cells, 0, cells.Length))
                            continue;
                        cellRanges.Add(new int2(0, cells.Length));
                        for (int j = 0; j < brushes.Length; j++)
                        {
                            var other = brushes[j];
                            if (j == i || !Overlaps(min, max, other.min, other.max) || !NearPlane(other, plane))
                                continue;
                            next.Clear();
                            nextRanges.Clear();
                            for (int r = 0; r < cellRanges.Length; r++)
                                SplitBy(cells, cellRanges[r], other, next, nextRanges, remaining, front, back);
                            (cells, next)           = (next, cells);
                            (cellRanges, nextRanges) = (nextRanges, cellRanges);
                        }

                        var instructions = program;
                        var count        = program.Length;
                        if (oneBranch)
                        {
                            faceProgram.Clear();
                            faceProgram.Add(new Instruction { operation = Operation.Nothing });
                            for (int c = 0; c < children.Length; c++)
                            {
                                var child  = children[c];
                                var isNear = child.brush >= 0 && Overlaps(min, max, brushes[child.brush].min, brushes[child.brush].max);
                                if (!isNear && child.operation != Operation.Intersecting)
                                    continue;
                                faceProgram.Add(new Instruction { operation = Operation.Brush, brush = isNear ? child.brush : -1 });
                                faceProgram.Add(new Instruction { operation = child.operation });
                            }
                            instructions = faceProgram.AsArray();
                            count        = faceProgram.Length;
                        }

                        for (int r = 0; r < cellRanges.Length; r++)
                        {
                            var cell   = cellRanges[r];
                            var middle = double3.zero;
                            for (int c = 0; c < cell.y; c++)
                                middle += cells[cell.x + c];
                            middle /= cell.y;
                            var solidInFront = Solid(instructions, count, stack, middle + plane.xyz * kOffset);
                            var solidBehind  = Solid(instructions, count, stack, middle - plane.xyz * kOffset);
                            if (solidInFront == solidBehind)
                                continue;
                            // facing out of the solid
                            pieceRanges.Add(new int2(pieceCorners.Length, cell.y));
                            piecePlanes.Add(solidInFront ? -plane : plane);
                            for (int c = 0; c < cell.y; c++)
                                pieceCorners.Add(cells[cell.x + (solidInFront ? cell.y - 1 - c : c)]);
                        }
                    }
                }
                cells.Dispose();
                cellRanges.Dispose();
                next.Dispose();
                nextRanges.Dispose();
                remaining.Dispose();
                front.Dispose();
                back.Dispose();
                faceProgram.Dispose();
                stack.Dispose();
            }
        }

        internal struct Piece
        {
            public double3[] corners;
            public double4   plane;
        }

        // The pieces of the brushes' faces on the shape's surface, each facing out of the solid
        internal static List<Piece> SurfacePieces(Shape shape)
        {
            var brushCount   = shape.brushes.Count;
            var vertexCount  = 0;
            var polygonCount = 0;
            var cornerCount  = 0;
            foreach (var brush in shape.brushes)
            {
                vertexCount  += brush.vertices.Length;
                polygonCount += brush.polygons.Length;
                foreach (var polygon in brush.polygons)
                    cornerCount += polygon.Length;
            }

            var program = new List<Instruction>();
            Emit(shape, 0, program);
            var root      = shape.items[0];
            var oneBranch = root.brush < 0 && root.childCount > 0;
            for (int c = 0; c < root.childCount && oneBranch; c++)
                oneBranch = shape.items[root.firstChild + c].childCount == 0;

            NativeArray<double3> vertices;
            using var _vertices = vertices = new NativeArray<double3>(vertexCount, Allocator.TempJob);
            NativeArray<int> corners;
            using var _corners  = corners = new NativeArray<int>(cornerCount, Allocator.TempJob);
            NativeArray<int2> polygons;
            using var _polygons = polygons = new NativeArray<int2>(polygonCount, Allocator.TempJob);
            NativeArray<double4> planes;
            using var _planes   = planes = new NativeArray<double4>(polygonCount, Allocator.TempJob);
            NativeArray<BrushRange> brushes;
            using var _brushes  = brushes = new NativeArray<BrushRange>(brushCount, Allocator.TempJob);
            using var programs = new NativeArray<Instruction>(program.ToArray(), Allocator.TempJob);
            NativeArray<Instruction> children;
            using var _children = children = new NativeArray<Instruction>(oneBranch ? root.childCount : 0, Allocator.TempJob);
            int vertexOffset = 0, polygonOffset = 0, cornerOffset = 0;
            for (int b = 0; b < brushCount; b++)
            {
                var brush = shape.brushes[b];
                brushes[b] = new BrushRange
                {
                    firstVertex  = vertexOffset,  vertexCount  = brush.vertices.Length,
                    firstPolygon = polygonOffset, polygonCount = brush.polygons.Length,
                    min = brush.min, max = brush.max
                };
                for (int v = 0; v < brush.vertices.Length; v++)
                    vertices[vertexOffset + v] = brush.vertices[v];
                for (int p = 0; p < brush.polygons.Length; p++)
                {
                    var polygon = brush.polygons[p];
                    polygons[polygonOffset + p] = new int2(cornerOffset, polygon.Length);
                    planes[polygonOffset + p]   = brush.planes[p];
                    for (int c = 0; c < polygon.Length; c++)
                        corners[cornerOffset + c] = vertexOffset + polygon[c];
                    cornerOffset += polygon.Length;
                }
                vertexOffset  += brush.vertices.Length;
                polygonOffset += brush.polygons.Length;
            }
            for (int c = 0; c < children.Length; c++)
            {
                var child = shape.items[root.firstChild + c];
                children[c] = new Instruction { operation = Combine(child.operation), brush = child.brush };
            }

            using var pieceCorners = new NativeList<double3>(cornerCount * 2, Allocator.TempJob);
            using var pieceRanges  = new NativeList<int2>(polygonCount, Allocator.TempJob);
            using var piecePlanes  = new NativeList<double4>(polygonCount, Allocator.TempJob);
            new SurfacePiecesJob
            {
                vertices     = vertices,
                corners      = corners,
                polygons     = polygons,
                planes       = planes,
                brushes      = brushes,
                program      = programs,
                children     = children,
                oneBranch    = oneBranch,
                pieceCorners = pieceCorners,
                pieceRanges  = pieceRanges,
                piecePlanes  = piecePlanes
            }.Run();

            var pieces = new List<Piece>(pieceRanges.Length);
            for (int p = 0; p < pieceRanges.Length; p++)
            {
                var range = pieceRanges[p];
                var piece = new double3[range.y];
                for (int c = 0; c < range.y; c++)
                    piece[c] = pieceCorners[range.x + c];
                pieces.Add(new Piece { corners = piece, plane = piecePlanes[p] });
            }
            return pieces;
        }
        #endregion

        #region Lines
        struct EdgeOnLine
        {
            public int    piece;
            public double from, to;
            // along the line's direction, or against it
            public bool   along;
        }

        static (int3, int3) LineKey(double3 a, double3 b, out double3 direction, out double3 origin, out bool along)
        {
            direction = math.normalize(b - a);
            // one direction for a line, whichever way the edge goes
            along = direction.x > 1e-9 || (math.abs(direction.x) <= 1e-9 && (direction.y > 1e-9 || (math.abs(direction.y) <= 1e-9 && direction.z > 0)));
            if (!along)
                direction = -direction;
            origin = a - direction * math.dot(a, direction);
            return ((int3)math.round(direction * kDirectionGrid), (int3)math.round(origin * kPointGrid));
        }

        static bool SamePlane(double4 a, double4 b)
        {
            return math.dot(a.xyz, b.xyz) > 1 - 1e-6 && math.abs(a.w - b.w) < kCoplanar;
        }

        // The outline of the shape: its corners, and pairs of them, a line each
        internal static void Build(Shape shape, List<float3> vertices, List<int> lines)
        {
            vertices.Clear();
            lines.Clear();
            if (shape.items.Count == 0)
                return;

            var pieces = SurfacePieces(shape);
            var onLine = new Dictionary<(int3, int3), List<EdgeOnLine>>();
            var lineOf = new Dictionary<(int3, int3), (double3 direction, double3 origin)>();
            for (int p = 0; p < pieces.Count; p++)
            {
                var corners = pieces[p].corners;
                for (int c = 0; c < corners.Length; c++)
                {
                    var a = corners[c];
                    var b = corners[(c + 1) % corners.Length];
                    if (math.lengthsq(b - a) < kOnPlane * kOnPlane)
                        continue;
                    var key = LineKey(a, b, out var direction, out var origin, out var along);
                    if (!onLine.TryGetValue(key, out var edges))
                    {
                        onLine[key] = edges = new List<EdgeOnLine>();
                        lineOf[key] = (direction, origin);
                    }
                    var ta = math.dot(a, lineOf[key].direction);
                    var tb = math.dot(b, lineOf[key].direction);
                    edges.Add(new EdgeOnLine { piece = p, from = math.min(ta, tb), to = math.max(ta, tb), along = along });
                }
            }

            var indexOf = new Dictionary<int3, int>();
            int Index(double3 point)
            {
                var key = (int3)math.round(point * 1e5);
                if (!indexOf.TryGetValue(key, out var index))
                {
                    indexOf[key] = index = vertices.Count;
                    vertices.Add((float3)point);
                }
                return index;
            }

            var covered = new List<(double from, double to)>();
            var kept    = new List<(double from, double to)>();
            foreach (var pair in onLine)
            {
                var edges = pair.Value;
                var (direction, origin) = lineOf[pair.Key];
                kept.Clear();
                for (int e = 0; e < edges.Count; e++)
                {
                    var edge = edges[e];
                    // the parts of it a piece in its plane is on the other side of (inside a flat face), or a piece before
                    // it shares (a crease, drawn once)
                    covered.Clear();
                    for (int o = 0; o < edges.Count; o++)
                    {
                        var other = edges[o];
                        if (o == e || other.piece == edge.piece || other.along == edge.along)
                            continue;
                        if (!SamePlane(pieces[other.piece].plane, pieces[edge.piece].plane) && other.piece > edge.piece)
                            continue;
                        var from = math.max(edge.from, other.from);
                        var to   = math.min(edge.to, other.to);
                        if (to - from > kOnPlane)
                            covered.Add((from, to));
                    }
                    covered.Sort((x, y) => x.from.CompareTo(y.from));
                    var start = edge.from;
                    foreach (var (from, to) in covered)
                    {
                        if (from - start > kOnPlane)
                            kept.Add((start, from));
                        start = math.max(start, to);
                    }
                    if (edge.to - start > kOnPlane)
                        kept.Add((start, edge.to));
                }

                // the parts kept on this line that touch drawn as one line
                kept.Sort((x, y) => x.from.CompareTo(y.from));
                for (int k = 0; k < kept.Count; )
                {
                    var (from, to) = kept[k++];
                    while (k < kept.Count && kept[k].from - to <= kOnPlane)
                        to = math.max(to, kept[k++].to);
                    lines.Add(Index(origin + direction * from));
                    lines.Add(Index(origin + direction * to));
                }
            }
        }
        #endregion
    }
}
