using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using BigInteger = System.Numerics.BigInteger;
using Random = System.Random;

namespace Chisel.Core.Tests
{
    // ConvexDecomposition checked against its contract: convex counter-clockwise pieces (straight at a corner, never
    // turning back), with only the outline's corners, that together are the outline and nothing else; each outline edge
    // in one piece with the segment it had, each cut in two pieces, once each way; the sides of a piece on one line
    // that show with one segment, a cut among them with it too; and no cut that could go, its two pieces making one
    // that keeps to all that (Hertel and Mehlhorn).
    [TestFixture]
    public class ConvexDecompositionTests
    {
        // ---- running it ----------------------------------------------------------------------------------------------

        sealed class Result
        {
            public bool ok;
            public List<SegmentVertex[]> pieces = new List<SegmentVertex[]>();
        }

        static Result Decompose(IList<float2> points, IList<int> segments = null)
        {
            var outline  = new NativeList<SegmentVertex>(points.Count, Allocator.Temp);
            var vertices = new UnsafeList<SegmentVertex>(16, Allocator.Temp);
            var ends     = new UnsafeList<int>(4, Allocator.Temp);
            try
            {
                for (int i = 0; i < points.Count; i++)
                    outline.Add(new SegmentVertex { position = points[i], segmentIndex = segments != null ? segments[i] : i });
                var result = new Result { ok = ConvexDecomposition.Decompose(outline, ref vertices, ref ends) };
                for (int p = 0, start = 0; p < ends.Length; start = ends[p], p++)
                {
                    var piece = new SegmentVertex[ends[p] - start];
                    for (int v = 0; v < piece.Length; v++)
                        piece[v] = vertices[start + v];
                    result.pieces.Add(piece);
                }
                return result;
            }
            finally
            {
                outline.Dispose();
                vertices.Dispose();
                ends.Dispose();
            }
        }

        static float2[] Points(params float[] xy)
        {
            var points = new float2[xy.Length / 2];
            for (int i = 0; i < points.Length; i++)
                points[i] = new float2(xy[i * 2], xy[i * 2 + 1]);
            return points;
        }

        // ---- the contract ---------------------------------------------------------------------------------------------

        static long Key(float2 p) => ((long)BitConverter.SingleToInt32Bits(p.x + 0.0f) << 32) | (uint)BitConverter.SingleToInt32Bits(p.y + 0.0f);

        // Twice the signed area, in units of 2^-298
        static BigInteger TwiceArea(IList<float2> points)
        {
            var sum = BigInteger.Zero;
            for (int i = 0; i < points.Count; i++)
            {
                var a = points[i]; var b = points[(i + 1) % points.Count];
                sum += FloatReference.Whole(a.x) * FloatReference.Whole(b.y) - FloatReference.Whole(b.x) * FloatReference.Whole(a.y);
            }
            return sum;
        }

        // The pieces of an outline that doesn't touch itself (its corners all apart): checks them against the contract
        // and returns how many there are
        static int AssertPartition(IList<float2> points, IList<int> segments, Result result, string what)
        {
            Assert.That(result.ok, Is.True, what + ": not decomposed");
            var n = points.Count;
            segments ??= Range(n);
            var counterClockwise = TwiceArea(points).Sign > 0;
            var index = new Dictionary<long, int>();
            for (int i = 0; i < n; i++)
                index.Add(Key(points[i]), i);
            // the outline's edges as the pieces run them: counter-clockwise round the area
            var outlineSegment = new Dictionary<(int, int), int>();
            for (int i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                if (counterClockwise) outlineSegment.Add((i, j), segments[i]);
                else                  outlineSegment.Add((j, i), segments[i]);
            }

            var runs  = new Dictionary<(int, int), int>();
            var total = BigInteger.Zero;
            var pieceCorners = new List<int[]>();
            foreach (var piece in result.pieces)
            {
                AssertOneSegmentPerLine(points, outlineSegment, piece, what);
                Assert.That(piece.Length, Is.GreaterThanOrEqualTo(3), what + ": a piece of fewer than three corners");
                var corners = new int[piece.Length];
                pieceCorners.Add(corners);
                var positions = new float2[piece.Length];
                for (int v = 0; v < piece.Length; v++)
                {
                    Assert.That(index.TryGetValue(Key(piece[v].position), out corners[v]), Is.True, $"{what}: a corner {piece[v].position} the outline doesn't have");
                    positions[v] = piece[v].position;
                }
                var area = TwiceArea(positions);
                Assert.That(area.Sign, Is.EqualTo(1), what + ": a piece without area, or clockwise");
                total += area;
                for (int v = 0; v < piece.Length; v++)
                {
                    int u = corners[(v + piece.Length - 1) % piece.Length], c = corners[v], w = corners[(v + 1) % piece.Length];
                    var turn = FloatReference.Orientation(points[u], points[c], points[w]);
                    Assert.That(turn, Is.GreaterThanOrEqualTo(0), $"{what}: a piece turns right at {points[c]}");
                    if (turn == 0)
                        Assert.That(FloatReference.Dot(points[u], points[c], points[c], points[w]).Sign, Is.EqualTo(1), $"{what}: a piece turns back at {points[c]}");
                    runs.TryGetValue((c, w), out var count);
                    runs[(c, w)] = count + 1;
                    if (outlineSegment.TryGetValue((c, w), out var segment))
                        Assert.That(piece[v].segmentIndex, Is.EqualTo(segment), $"{what}: the edge from {points[c]} has another segment");
                }
            }
            foreach (var edge in outlineSegment.Keys)
            {
                runs.TryGetValue(edge, out var count);
                Assert.That(count, Is.EqualTo(1), $"{what}: the outline edge from {points[edge.Item1]} to {points[edge.Item2]} is in {count} pieces");
            }
            foreach (var pair in runs)
            {
                if (outlineSegment.ContainsKey(pair.Key))
                    continue;
                Assert.That(outlineSegment.ContainsKey((pair.Key.Item2, pair.Key.Item1)), Is.False, $"{what}: a piece runs an outline edge the wrong way");
                Assert.That(pair.Value, Is.EqualTo(1), $"{what}: the cut from {points[pair.Key.Item1]} to {points[pair.Key.Item2]} is in {pair.Value} pieces one way");
                runs.TryGetValue((pair.Key.Item2, pair.Key.Item1), out var back);
                Assert.That(back, Is.EqualTo(1), $"{what}: the cut from {points[pair.Key.Item1]} to {points[pair.Key.Item2]} has no piece on its other side");
            }
            Assert.That(total, Is.EqualTo(BigInteger.Abs(TwiceArea(points))), what + ": the pieces don't add up to the outline");
            AssertNoCutCanGo(points, outlineSegment, pieceCorners, what);
            return result.pieces.Count;
        }

        // Every cut is needed: the two pieces on either side of it, as one, would turn right or back somewhere, or show
        // two segments on one line
        static void AssertNoCutCanGo(IList<float2> points, Dictionary<(int, int), int> outlineSegment, List<int[]> pieces, string what)
        {
            var cutAt = new Dictionary<(int, int), (int piece, int corner)>();
            for (int p = 0; p < pieces.Count; p++)
            {
                for (int v = 0; v < pieces[p].Length; v++)
                {
                    var side = (pieces[p][v], pieces[p][(v + 1) % pieces[p].Length]);
                    if (!outlineSegment.ContainsKey(side))
                        cutAt[side] = (p, v);
                }
            }
            foreach (var cut in cutAt)
            {
                var (a, b) = cut.Key;
                if (a > b || !cutAt.TryGetValue((b, a), out var other))
                    continue;
                // the piece from b round to a, then the other from a round to b, each end once
                var one = pieces[cut.Value.piece];
                var two = pieces[other.piece];
                var merged = new List<int>();
                for (int s = 1; s <= one.Length; s++)
                    merged.Add(one[(cut.Value.corner + s) % one.Length]);
                for (int s = 2; s < two.Length; s++)
                    merged.Add(two[(other.corner + s) % two.Length]);
                Assert.That(KeepsToTheContract(points, outlineSegment, merged), Is.False,
                            $"{what}: the cut from {points[a]} to {points[b]} could go");
            }
        }

        // A piece, as corners of the outline, convex and with one segment shown on each line
        static bool KeepsToTheContract(IList<float2> points, Dictionary<(int, int), int> outlineSegment, List<int> piece)
        {
            var count = piece.Count;
            bool Straight(int v) => FloatReference.Orientation(points[piece[(v + count - 1) % count]], points[piece[v]], points[piece[(v + 1) % count]]) == 0;
            for (int v = 0; v < count; v++)
            {
                float2 a = points[piece[(v + count - 1) % count]], c = points[piece[v]], b = points[piece[(v + 1) % count]];
                var turn = FloatReference.Orientation(a, c, b);
                if (turn < 0 || (turn == 0 && FloatReference.Dot(a, c, c, b).Sign <= 0))
                    return false;
            }
            var first = 0;
            while (first < count && Straight(first))
                first++;
            for (int start = 0; start < count; )
            {
                var end = start + 1;
                while (end < count && Straight((first + end) % count))
                    end++;
                int? shown = null;
                for (int s = start; s < end; s++)
                {
                    var v = (first + s) % count;
                    if (!outlineSegment.TryGetValue((piece[v], piece[(v + 1) % count]), out var segment))
                        continue;
                    if (shown != null && shown != segment)
                        return false;
                    shown = segment;
                }
                start = end;
            }
            return true;
        }

        // A brush has one face per plane: the sides of a piece on one line are drawn with one surface. Those along the
        // outline must have one segment, and a cut among them has it too
        static void AssertOneSegmentPerLine(IList<float2> points, Dictionary<(int, int), int> outlineSegment, SegmentVertex[] piece, string what)
        {
            var count = piece.Length;
            var at = new int[count];
            for (int v = 0; v < count; v++)
                at[v] = points.IndexOf(piece[v].position);
            bool Straight(int v) => FloatReference.Orientation(piece[(v + count - 1) % count].position, piece[v].position, piece[(v + 1) % count].position) == 0;
            var turn = 0;
            while (turn < count && Straight(turn))
                turn++;
            Assert.That(turn, Is.LessThan(count), what + ": a piece on one line");
            for (int start = 0; start < count; )
            {
                var end = start + 1;
                while (end < count && Straight((turn + end) % count))
                    end++;
                int? shown = null;
                for (int s = start; s < end; s++)
                {
                    var v = (turn + s) % count;
                    if (!outlineSegment.TryGetValue((at[v], at[(v + 1) % count]), out var segment))
                        continue;
                    Assert.That(shown ?? segment, Is.EqualTo(segment), $"{what}: sides on one line through {piece[v].position} show two segments");
                    shown = segment;
                }
                for (int s = start; s < end && shown != null; s++)
                    Assert.That(piece[(turn + s) % count].segmentIndex, Is.EqualTo(shown.Value), $"{what}: a cut on the line of a side through {piece[(turn + s) % count].position} has another segment");
                start = end;
            }
        }

        static int[] Range(int n)
        {
            var range = new int[n];
            for (int i = 0; i < n; i++)
                range[i] = i;
            return range;
        }

        // ---- random outlines ----------------------------------------------------------------------------------------

        // Orientation of whole-number points
        static int Turn(int2 a, int2 b, int2 c) => Math.Sign((long)(b.x - a.x) * (c.y - a.y) - (long)(b.y - a.y) * (c.x - a.x));
        static long Dot(int2 a, int2 b, int2 c, int2 d) => (long)(b.x - a.x) * (d.x - c.x) + (long)(b.y - a.y) * (d.y - c.y);

        static bool OnSegment(int2 p, int2 a, int2 b) => Turn(a, b, p) == 0 && Dot(a, p, a, b) >= 0 && Dot(b, p, b, a) >= 0;

        static bool Intersect(int2 a, int2 b, int2 c, int2 d)
        {
            int o1 = Turn(a, b, c), o2 = Turn(a, b, d), o3 = Turn(c, d, a), o4 = Turn(c, d, b);
            if (o1 * o2 < 0 && o3 * o4 < 0) return true;
            return (o1 == 0 && OnSegment(c, a, b)) || (o2 == 0 && OnSegment(d, a, b)) ||
                   (o3 == 0 && OnSegment(a, c, d)) || (o4 == 0 && OnSegment(b, c, d));
        }

        // Whether an outline, counter-clockwise, crosses or touches itself anywhere but where its edges meet
        static bool Simple(IList<int2> p)
        {
            var n = p.Count;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    var adjacent = j == i + 1 || (i == 0 && j == n - 1);
                    if (adjacent)
                    {
                        // only their shared corner: not turning back along each other
                        int a = adjacent && j == i + 1 ? i : j, s = adjacent && j == i + 1 ? j : i, b = (s + 1) % n;
                        if (Turn(p[a], p[s], p[b]) == 0 && Dot(p[a], p[s], p[s], p[b]) < 0)
                            return false;
                        continue;
                    }
                    if (Intersect(p[i], p[(i + 1) % n], p[j], p[(j + 1) % n]))
                        return false;
                }
            }
            return true;
        }

        // A random simple outline of n corners on a grid: random corners in random order, untangled by reversing the
        // run between two crossing edges until none cross; one that still touches itself is drawn again
        static List<int2> RandomOutline(Random random, int n, int grid)
        {
            for (int attempt = 0; ; attempt++)
            {
                var used = new HashSet<int2>();
                var p = new List<int2>();
                while (p.Count < n)
                {
                    var q = new int2(random.Next(grid), random.Next(grid));
                    if (used.Add(q))
                        p.Add(q);
                }
                for (int pass = 0; pass < 10000; pass++)
                {
                    var untangled = true;
                    for (int i = 0; i < n && untangled; i++)
                    {
                        for (int j = i + 2; j < n && untangled; j++)
                        {
                            if (i == 0 && j == n - 1)
                                continue;
                            int2 a = p[i], b = p[i + 1], c = p[j], d = p[(j + 1) % n];
                            if (Turn(a, b, c) * Turn(a, b, d) < 0 && Turn(c, d, a) * Turn(c, d, b) < 0)
                            {
                                p.Reverse(i + 1, j - i);
                                untangled = false;
                            }
                        }
                    }
                    if (untangled)
                        break;
                }
                long area = 0;
                for (int i = 0; i < n; i++)
                    area += (long)p[i].x * p[(i + 1) % n].y - (long)p[(i + 1) % n].x * p[i].y;
                if (area == 0)
                    continue;
                if (area < 0)
                    p.Reverse();
                if (Simple(p))
                    return p;
            }
        }

        static float2[] AsFloats(IList<int2> p)
        {
            var points = new float2[p.Count];
            for (int i = 0; i < p.Count; i++)
                points[i] = new float2(p[i].x, p[i].y);
            return points;
        }

        // ---- shapes -------------------------------------------------------------------------------------------------

        [Test]
        public void AConvexOutline_IsOnePiece_AsItIs()
        {
            var points = Points(0, 0, 4, 0, 5, 3, 2, 5, -1, 3);
            var result = Decompose(points);
            Assert.That(AssertPartition(points, null, result, "pentagon"), Is.EqualTo(1));
            Assert.That(result.pieces[0].Length, Is.EqualTo(5));
        }

        [Test]
        public void ATriangle_IsOnePiece()
        {
            var points = Points(0, 0, 1, 0, 0, 1);
            Assert.That(AssertPartition(points, null, Decompose(points), "triangle"), Is.EqualTo(1));
        }

        // Clockwise, its pieces are counter-clockwise, each edge still with the segment of the corner it started from
        [Test]
        public void AClockwiseOutline_KeepsEachEdgesSegment()
        {
            var points = Points(0, 0, 0, 1, 2, 3, 4, 1, 4, 0);
            Assert.That(AssertPartition(points, new[] { 10, 11, 12, 13, 14 }, Decompose(points, new[] { 10, 11, 12, 13, 14 }), "clockwise"), Is.EqualTo(1));
            var lShape = Points(0, 0, 0, 2, 1, 2, 1, 1, 2, 1, 2, 0);
            Assert.That(AssertPartition(lShape, null, Decompose(lShape), "clockwise L"), Is.EqualTo(2));
        }

        [Test]
        public void AnLShape_IsTwoPieces()
        {
            var points = Points(0, 0, 2, 0, 2, 1, 1, 1, 1, 2, 0, 2);
            Assert.That(AssertPartition(points, null, Decompose(points), "L"), Is.EqualTo(2));
        }

        // An L with a corner where its left side goes straight on but the segment changes: a brush draws one face per
        // plane, so a cut ends there and no piece goes straight on through it
        [Test]
        public void AStraightCornerWhereTheSegmentChanges_IsCut()
        {
            var points = Points(0, 0, 2, 0, 2, 1, 1, 1, 1, 2, 0, 2, 0, 1);
            var result = Decompose(points);
            AssertPartition(points, null, result, "L with a corner on its side");
            var piecesThere = 0;
            foreach (var piece in result.pieces)
            {
                foreach (var corner in piece)
                    piecesThere += corner.position.Equals(new float2(0, 1)) ? 1 : 0;
            }
            Assert.That(piecesThere, Is.GreaterThanOrEqualTo(2), "no cut ends at the corner where the segment changes");
        }

        // Corners along a straight edge with one segment, as a curve flattened along a line gives: each stays a corner of
        // its piece, its edge with its segment
        [Test]
        public void CornersAlongAStraightEdge_Stay()
        {
            var points = Points(0, 0, 1, 0, 2, 0, 3, 0, 3, 1, 3, 3, 1.5f, 3, 1.5f, 1, 0, 1);
            var segments = new[] { 0, 0, 0, 1, 1, 2, 3, 4, 5 };
            var result = Decompose(points, segments);
            AssertPartition(points, segments, result, "edge with corners on it");
            var corners = 0;
            foreach (var piece in result.pieces)
                corners += piece.Length;
            Assert.That(corners, Is.GreaterThanOrEqualTo(points.Length), "a corner was left out");
        }

        // A side with a bump out of it, each side its own segment: a cut across the bump's opening would leave a piece
        // with the side either side of the bump on one line, two segments shown in one face
        [Test]
        public void ASideWithABump_ShowsOneSegmentPerLine()
        {
            var points = Points(0, 0, 1, 0, 1, -1, 2, -1, 2, 0, 3, 0, 3, 2, 0, 2);
            AssertPartition(points, null, Decompose(points), "side with a bump");
        }

        // The same outlines a power of two smaller or larger, which floats hold exactly: the same pieces, scaled
        [Test]
        public void ScaledOutlines_HaveTheSamePieces([Values(-60, -20, 20, 60)] int exponent)
        {
            var random = new Random(31 + exponent);
            var scale = (float)Math.Pow(2, exponent);
            for (int n = 0; n < 40; n++)
            {
                var outline = RandomOutline(random, 5 + random.Next(6), 4 + random.Next(4));
                var points = AsFloats(outline);
                var scaled = new float2[points.Length];
                for (int i = 0; i < points.Length; i++)
                    scaled[i] = points[i] * scale;
                var what = $"outline {n} at 2^{exponent}: " + string.Join(" ", outline);
                var plain = Decompose(points);
                var result = Decompose(scaled);
                AssertPartition(scaled, null, result, what);
                Assert.That(result.pieces.Count, Is.EqualTo(plain.pieces.Count), what);
                for (int p = 0; p < plain.pieces.Count; p++)
                {
                    Assert.That(result.pieces[p].Length, Is.EqualTo(plain.pieces[p].Length), what);
                    for (int v = 0; v < plain.pieces[p].Length; v++)
                        Assert.That(result.pieces[p][v].position, Is.EqualTo(plain.pieces[p][v].position * scale), what);
                }
            }
        }

        // A comb: its reflex corners are all on one line, and the sides between its teeth too, each its own segment
        [Test]
        public void AComb_IsDecomposed([Values(2, 5, 17)] int teeth)
        {
            var points = new List<float2> { new float2(0, 0), new float2(teeth * 2 - 1, 0) };
            for (int t = teeth - 1; t >= 0; t--)
            {
                points.Add(new float2(t * 2 + 1, 3));
                points.Add(new float2(t * 2, 3));
                if (t > 0)
                {
                    points.Add(new float2(t * 2, 1));
                    points.Add(new float2(t * 2 - 1, 1));
                }
            }
            AssertPartition(points, null, Decompose(points), $"comb of {teeth}");
        }

        // Random simple outlines on a small grid, many with corners on a line, the segment of each edge often the one before
        // (as along a curve), counter-clockwise and clockwise
        [Test]
        public void RandomOutlines_AreDecomposed([Values(5, 6, 7, 8, 9, 12)] int corners)
        {
            var random = new Random(1000 + corners);
            for (int n = 0; n < 60; n++)
            {
                var outline = RandomOutline(random, corners, 4 + random.Next(4));
                var segments = new int[corners];
                for (int i = 0; i < corners; i++)
                    segments[i] = i > 0 && random.NextDouble() < 0.5 ? segments[i - 1] : i;
                var points = AsFloats(outline);
                var what = $"outline {n} of {corners}: " + string.Join(" ", outline) + ", segments " + string.Join(",", segments);
                AssertPartition(points, segments, Decompose(points, segments), what);
                var reversed = new float2[corners];
                var reversedSegments = new int[corners];
                for (int i = 0; i < corners; i++)
                {
                    reversed[i] = points[corners - 1 - i];
                    reversedSegments[i] = segments[(2 * corners - 2 - i) % corners];
                }
                AssertPartition(reversed, reversedSegments, Decompose(reversed, reversedSegments), what + ", clockwise");
            }
        }

        // Outlines with float corners anywhere, many reflex, none on a line with two others: valid, and within the bounds.
        // A reflex corner needs a cut and a cut serves two at most; Hertel and Mehlhorn keep at most two at each
        [Test]
        public void RandomStarOutlines_AreDecomposed([Values(12, 40, 120)] int corners)
        {
            var random = new Random(corners);
            for (int n = 0; n < 8; n++)
            {
                var points = new float2[corners];
                for (int i = 0; i < corners; i++)
                {
                    var angle  = (i + (float)random.NextDouble() * 0.8f) * 2 * math.PI / corners;
                    var radius = 1 + (float)random.NextDouble() * 9;
                    points[i] = new float2(math.cos(angle), math.sin(angle)) * radius + new float2(100.25f, -3.5f);
                }
                var reflex = 0;
                for (int i = 0; i < corners; i++)
                    reflex += FloatReference.Orientation(points[(i + corners - 1) % corners], points[i], points[(i + 1) % corners]) < 0 ? 1 : 0;
                var count = AssertPartition(points, null, Decompose(points), $"star {n} of {corners}");
                Assert.That(count, Is.InRange((reflex + 1) / 2 + 1, reflex * 2 + 1), $"star {n} of {corners}");
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct DecomposeJob : IJob
        {
            [ReadOnly] public NativeList<SegmentVertex> outline;
            public NativeList<SegmentVertex> vertices;
            public NativeList<int>           ends;
            public NativeReference<bool>     ok;

            public void Execute()
            {
                var pieceVertices = new UnsafeList<SegmentVertex>(16, Allocator.Temp);
                var pieceEnds     = new UnsafeList<int>(4, Allocator.Temp);
                ok.Value = ConvexDecomposition.Decompose(outline, ref pieceVertices, ref pieceEnds);
                for (int v = 0; v < pieceVertices.Length; v++)
                    vertices.Add(pieceVertices[v]);
                for (int e = 0; e < pieceEnds.Length; e++)
                    ends.Add(pieceEnds[e]);
                pieceVertices.Dispose();
                pieceEnds.Dispose();
            }
        }

        static Result DecomposeInBurst(IList<float2> points)
        {
            using var outline  = new NativeList<SegmentVertex>(points.Count, Allocator.TempJob);
            using var vertices = new NativeList<SegmentVertex>(16, Allocator.TempJob);
            using var ends     = new NativeList<int>(4, Allocator.TempJob);
            using var ok       = new NativeReference<bool>(Allocator.TempJob);
            for (int i = 0; i < points.Count; i++)
                outline.Add(new SegmentVertex { position = points[i], segmentIndex = i });
            new DecomposeJob { outline = outline, vertices = vertices, ends = ends, ok = ok }.Run();
            var result = new Result { ok = ok.Value };
            for (int p = 0, start = 0; p < ends.Length; start = ends[p], p++)
            {
                var piece = new SegmentVertex[ends[p] - start];
                for (int v = 0; v < piece.Length; v++)
                    piece[v] = vertices[start + v];
                result.pieces.Add(piece);
            }
            return result;
        }

        // Burst-compiled, as the generators run it: the same pieces
        [Test]
        public void InABurstJob_ThePiecesAreTheSame()
        {
            var random = new Random(7);
            var outlines = new List<float2[]>
            {
                AsFloats(RandomOutline(random, 9, 6)),
                AsFloats(RandomOutline(random, 12, 5)),
                Points(0, 0, 2, 0, 2, 2, 4, 2, 4, 4, 2, 4, 2, 2, 0, 2),
            };
            var star = new float2[60];
            for (int i = 0; i < star.Length; i++)
            {
                var angle  = (i + (float)random.NextDouble() * 0.8f) * 2 * math.PI / star.Length;
                star[i] = new float2(math.cos(angle), math.sin(angle)) * (1 + (float)random.NextDouble() * 9);
            }
            outlines.Add(star);
            foreach (var points in outlines)
            {
                var managed = Decompose(points);
                var bursted = DecomposeInBurst(points);
                Assert.That(bursted.ok, Is.EqualTo(managed.ok));
                Assert.That(bursted.pieces.Count, Is.EqualTo(managed.pieces.Count));
                for (int p = 0; p < managed.pieces.Count; p++)
                    Assert.That(bursted.pieces[p], Is.EqualTo(managed.pieces[p]));
            }
        }

        // ---- outlines that aren't simple -----------------------------------------------------------------------------

        // A corner repeated takes the segment of the edge on from it
        [Test]
        public void ARepeatedCorner_IsOne()
        {
            var points = Points(0, 0, 1, 0, 1, 0, 1, 1, 0, 1);
            var result = Decompose(points);
            var clean = Points(0, 0, 1, 0, 1, 1, 0, 1);
            Assert.That(AssertPartition(clean, new[] { 0, 2, 3, 4 }, result, "repeated corner"), Is.EqualTo(1));
        }

        // A spike of no width out of an edge is no part of the outline. The edge keeps both its segments, one each side
        // of where the spike was, so a cut ends there
        [Test]
        public void ASpike_IsLeftOut()
        {
            var points = Points(0, 0, 4, 0, 4, 2, 2, 2, 2, 3, 2, 2, 0, 2);
            var result = Decompose(points);
            var clean = Points(0, 0, 4, 0, 4, 2, 2, 2, 0, 2);
            Assert.That(AssertPartition(clean, new[] { 0, 1, 2, 5, 6 }, result, "spike"), Is.EqualTo(2));
        }

        // A spike that turns back past the corner it left from: the edge left runs along the way back, and takes its
        // segment
        [Test]
        public void ASpikeBackPastItsStart_LeavesTheEdgeBackItsSegment()
        {
            var points = Points(0, 0, 2, 0, 2, 2, 1, 2, 3, 2, 3, 0, 4, 0, 4, 4, 0, 4);
            var result = Decompose(points);
            var clean = Points(0, 0, 2, 0, 2, 2, 3, 2, 3, 0, 4, 0, 4, 4, 0, 4);
            AssertPartition(clean, new[] { 0, 1, 3, 4, 5, 6, 7, 8 }, result, "spike back past its start");
        }

        // A coordinate that isn't finite has no place: the outline is refused
        [Test]
        public void ACornerThatIsNotFinite_IsRefused([Values(float.NaN, float.PositiveInfinity, float.NegativeInfinity)] float value)
        {
            Assert.That(Decompose(Points(0, 0, 1, 0, value, 1)).ok, Is.False);
            Assert.That(Decompose(Points(0, 0, 1, 0, 1, value)).ok, Is.False);
        }

        [Test]
        public void AnOutlineWithoutArea_IsNoPieces()
        {
            var result = Decompose(Points(0, 0, 1, 0, 2, 0));
            Assert.That(result.ok, Is.True);
            Assert.That(result.pieces.Count, Is.Zero);
        }

        // Two squares meeting at a corner, the outline passing it twice: each square on its own
        [Test]
        public void AnOutlineThroughOneCornerTwice_IsSplitThere()
        {
            var points = Points(0, 0, 2, 0, 2, 2, 4, 2, 4, 4, 2, 4, 2, 2, 0, 2);
            var result = Decompose(points);
            Assert.That(result.ok, Is.True);
            AssertPartition(Points(0, 0, 2, 0, 2, 2, 0, 2), new[] { 0, 1, 6, 7 }, new Result { ok = true, pieces = PiecesWithin(result, 0, 2) }, "first square");
            AssertPartition(Points(2, 2, 4, 2, 4, 4, 2, 4), new[] { 2, 3, 4, 5 }, new Result { ok = true, pieces = PiecesWithin(result, 2, 4) }, "second square");
        }

        // A corner on another edge: the edge is cut there, and each side is decomposed on its own
        [Test]
        public void ACornerOnAnotherEdge_SplitsTheOutlineThere()
        {
            var points = Points(0, 0, 4, 0, 4, 3, 2, 0, 0, 3);
            var result = Decompose(points);
            Assert.That(result.ok, Is.True);
            AssertPartition(Points(2, 0, 4, 0, 4, 3), new[] { 0, 1, 2 }, new Result { ok = true, pieces = PiecesWithin(result, 2, 4) }, "right side");
            AssertPartition(Points(0, 0, 2, 0, 0, 3), new[] { 0, 3, 4 }, new Result { ok = true, pieces = PiecesWithin(result, 0, 2) }, "left side");
        }

        // An outline that crosses itself is the caller's to split (ChiselCurve2DBlob.SplitAtCrossings): a bow tie, and one
        // whose crossing edges reach past each other's bounds
        [Test]
        public void AnOutlineThatCrossesItself_IsRefused()
        {
            Assert.That(Decompose(Points(0, 0, 2, 2, 2, 0, 0, 2)).ok, Is.False);
            Assert.That(Decompose(Points(1, 0, 3, 2, 0, 2, 4, 0)).ok, Is.False);
        }

        static List<SegmentVertex[]> PiecesWithin(Result result, float minX, float maxX)
        {
            var within = new List<SegmentVertex[]>();
            foreach (var piece in result.pieces)
            {
                var inside = true;
                foreach (var corner in piece)
                    inside &= corner.position.x >= minX && corner.position.x <= maxX;
                if (inside)
                    within.Add(piece);
            }
            return within;
        }

        // ---- through the shape generators' outline -----------------------------------------------------------------

        static BlobAssetReferenceScope<ChiselCurve2DBlob> Curve(Curve2D curve) => new BlobAssetReferenceScope<ChiselCurve2DBlob>(ChiselCurve2DBlob.Convert(curve, Allocator.Temp));

        sealed class BlobAssetReferenceScope<T> : IDisposable where T : unmanaged
        {
            public Unity.Entities.BlobAssetReference<T> blob;
            public BlobAssetReferenceScope(Unity.Entities.BlobAssetReference<T> blob) { this.blob = blob; }
            public void Dispose() { if (blob.IsCreated) blob.Dispose(); }
        }

        // A shape with curved, concave spans, as Extruded Shape and Revolved Shape divide it: its pieces are the outline
        [Test]
        public void ACurvedConcaveShape_IsDecomposedAsItsOutline([Values(4, 8, 32)] int curveSegments)
        {
            var controlPoints = new[]
            {
                new CurveControlPoint2D(0, 0), new CurveControlPoint2D(6, 0), new CurveControlPoint2D(6, 4), new CurveControlPoint2D(3, 2),
                new CurveControlPoint2D(0, 4)
            };
            // the spans into and out of the middle point bow inward, a valley that runs level through it (a span arrives
            // along tangent1 and leaves against tangent2: both leftward)
            controlPoints[3].constraint1 = ControlPointConstraint.Broken;
            controlPoints[3].constraint2 = ControlPointConstraint.Broken;
            controlPoints[3].tangent1 = new UnityEngine.Vector2(-1.5f, 0);
            controlPoints[3].tangent2 = new UnityEngine.Vector2(1.5f, 0);
            using var curve = Curve(new Curve2D(controlPoints));
            using var outline = new NativeList<SegmentVertex>(Allocator.Temp);
            curve.blob.Value.AddPathVertices(curveSegments, outline);
            Assert.That(curve.blob.Value.ConvexPartition(curveSegments, out var vertices, out var ends, Allocator.Temp), Is.True);
            try
            {
                var result = new Result { ok = true };
                for (int p = 0, start = 0; p < ends.Length; start = ends[p], p++)
                {
                    var piece = new SegmentVertex[ends[p] - start];
                    for (int v = 0; v < piece.Length; v++)
                        piece[v] = vertices[start + v];
                    result.pieces.Add(piece);
                }
                var points = new float2[outline.Length];
                var segments = new int[outline.Length];
                for (int i = 0; i < outline.Length; i++)
                {
                    points[i] = outline[i].position;
                    segments[i] = outline[i].segmentIndex;
                }
                AssertPartition(points, segments, result, $"curved shape in {curveSegments}");
            }
            finally
            {
                vertices.Dispose();
                ends.Dispose();
            }
        }

        // Edges along the axes crossing (their bounds are lines): split there, into two loops that are decomposed
        [Test]
        public void ACrossingOfEdgesAlongTheAxes_IsSplit()
        {
            using var outline = new NativeList<SegmentVertex>(Allocator.Temp);
            var corners = Points(0, 0, 2, 0, 2, 2, 1, 2, 1, -1, 0, -1);
            for (int i = 0; i < corners.Length; i++)
                outline.Add(new SegmentVertex { position = corners[i], segmentIndex = i });
            using var loops = new NativeList<SegmentVertex>(Allocator.Temp);
            using var ends  = new NativeList<int>(Allocator.Temp);
            ChiselCurve2DBlob.SplitAtCrossings(outline, loops, ends);
            Assert.That(ends.Length, Is.EqualTo(2), "not split at the crossing");
            var area = 0.0;
            for (int l = 0, start = 0; l < ends.Length; start = ends[l], l++)
            {
                var loop = new List<float2>();
                for (int v = start; v < ends[l]; v++)
                    loop.Add(loops[v].position);
                var result = Decompose(loop);
                Assert.That(result.ok, Is.True, $"loop {l} not decomposed");
                foreach (var piece in result.pieces)
                    area += (double)TwiceArea(piece.Select(corner => corner.position).ToList());
            }
            // the square (1, 0)-(2, 2) and the square (0, -1)-(1, 0), both filled
            Assert.That(area, Is.EqualTo(2 * (2.0 + 1.0) * Math.Pow(2, 298)).Within(1e-6).Percent);
        }

        // A crossing a hair from an edge's end is still a crossing: the outline is split there, and each part decomposed
        [Test]
        public void ACrossingNearAnEdgesEnd_IsSplit()
        {
            using var outline = new NativeList<SegmentVertex>(Allocator.Temp);
            var corners = Points(0, 0, 10, 0, 10, 1, 9.9999f, -1, 0, -1);
            for (int i = 0; i < corners.Length; i++)
                outline.Add(new SegmentVertex { position = corners[i], segmentIndex = i });
            using var loops = new NativeList<SegmentVertex>(Allocator.Temp);
            using var ends  = new NativeList<int>(Allocator.Temp);
            ChiselCurve2DBlob.SplitAtCrossings(outline, loops, ends);
            Assert.That(ends.Length, Is.EqualTo(2), "not split at the crossing");
            for (int l = 0, start = 0; l < ends.Length; start = ends[l], l++)
            {
                var loop = new List<float2>();
                for (int v = start; v < ends[l]; v++)
                    loop.Add(loops[v].position);
                var result = Decompose(loop);
                Assert.That(result.ok, Is.True, $"loop {l} not decomposed");
            }
        }
    }
}
