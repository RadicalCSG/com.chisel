using System;
using System.Collections.Generic;
using Unity.Mathematics;
using BigInteger = System.Numerics.BigInteger;

namespace Chisel.Core.Tests
{
	static class ExactJudge
	{
		// ---- exact values -------------------------------------------------------------------------------------------

		// a x + b y + c z + w = 0, the solid on its negative side
		struct Plane
		{
			public BigInteger a, b, c, w;
			public BigInteger Normal(int axis) => axis == 0 ? a : (axis == 1 ? b : c);
		}

		// (X/W, Y/W, Z/W), always with W > 0
		struct Point
		{
			public BigInteger X, Y, Z, W;
			public BigInteger Coordinate(int axis) => axis == 0 ? X : (axis == 1 ? Y : Z);
		}

		// A point of one plane in that plane's (u, v) projection: (U/W, V/W), W > 0
		struct Point2
		{
			public BigInteger U, V, W;
		}

		// num / den with den > 0
		struct Rational
		{
			public BigInteger num, den;
			public double Approximate => (double)num / (double)den;
		}

		static int Compare(in Rational x, in Rational y) => (x.num * y.den - y.num * x.den).Sign;

		static BigInteger Big(Chisel.Core.Int128 value) => ((BigInteger)(long)value.hi << 64) + value.lo;

		static Plane ToPlane(in ExactPlane plane) => new Plane { a = plane.a, b = plane.b, c = plane.c, w = plane.w };

		static Point ToPoint(in ExactVertex vertex) => MakePoint(Big(vertex.X), Big(vertex.Y), Big(vertex.Z), Big(vertex.W));

		static Point MakePoint(BigInteger X, BigInteger Y, BigInteger Z, BigInteger W)
		{
			if (W.Sign < 0)
				return new Point { X = -X, Y = -Y, Z = -Z, W = -W };
			return new Point { X = X, Y = Y, Z = Z, W = W };
		}

		static int Side(in Plane plane, in Point point) => (plane.a * point.X + plane.b * point.Y + plane.c * point.Z + plane.w * point.W).Sign;

		static bool SamePoint(in Point p, in Point q) => p.X * q.W == q.X * p.W && p.Y * q.W == q.Y * p.W && p.Z * q.W == q.Z * p.W;

		static BigInteger Det3(BigInteger a0, BigInteger a1, BigInteger a2,
							   BigInteger b0, BigInteger b1, BigInteger b2,
							   BigInteger c0, BigInteger c1, BigInteger c2)
		{
			return a0 * (b1 * c2 - b2 * c1) - a1 * (b0 * c2 - b2 * c0) + a2 * (b0 * c1 - b1 * c0);
		}

		// Where three planes meet, by Cramer's rule on N x = -w; false when they do not meet in one point.
		static bool Intersect(in Plane p, in Plane q, in Plane r, out Point point)
		{
			point = default;
			var determinant = Det3(p.a, p.b, p.c, q.a, q.b, q.c, r.a, r.b, r.c);
			if (determinant.IsZero)
				return false;
			var X = Det3(-p.w, p.b, p.c, -q.w, q.b, q.c, -r.w, r.b, r.c);
			var Y = Det3(p.a, -p.w, p.c, q.a, -q.w, q.c, r.a, -r.w, r.c);
			var Z = Det3(p.a, p.b, -p.w, q.a, q.b, -q.w, r.a, r.b, -r.w);
			point = MakePoint(X, Y, Z, determinant);
			return true;
		}

		// A plane as a set: the same key for every positive or negative multiple of it. `orientation` is +1 when the plane
		// is a positive multiple of its key.
		struct PlaneKey : IEquatable<PlaneKey>
		{
			public BigInteger a, b, c, w;
			public bool Equals(PlaneKey other) => a == other.a && b == other.b && c == other.c && w == other.w;
			public override bool Equals(object obj) => obj is PlaneKey other && Equals(other);
			public override int GetHashCode() => ((a.GetHashCode() * 31 + b.GetHashCode()) * 31 + c.GetHashCode()) * 31 + w.GetHashCode();
			public Plane AsPlane => new Plane { a = a, b = b, c = c, w = w };
		}

		static PlaneKey KeyOf(in Plane plane, out int orientation)
		{
			var divisor = BigInteger.GreatestCommonDivisor(BigInteger.GreatestCommonDivisor(plane.a, plane.b),
														   BigInteger.GreatestCommonDivisor(plane.c, plane.w));
			orientation = plane.a.Sign != 0 ? plane.a.Sign : (plane.b.Sign != 0 ? plane.b.Sign : plane.c.Sign);
			var scale = divisor * orientation;
			return new PlaneKey { a = plane.a / scale, b = plane.b / scale, c = plane.c / scale, w = plane.w / scale };
		}

		// The largest normal component (ties to the lowest axis), and the two axes a plane is projected onto.
		static void Axes(in Plane plane, out int dominant, out int u, out int v)
		{
			var ax = BigInteger.Abs(plane.a); var ay = BigInteger.Abs(plane.b); var az = BigInteger.Abs(plane.c);
			dominant = (ax >= ay && ax >= az) ? 0 : (ay >= az ? 1 : 2);
			u = (dominant + 1) % 3;
			v = (dominant + 2) % 3;
		}

		static Point2 Project(in Point point, int u, int v) => new Point2 { U = point.Coordinate(u), V = point.Coordinate(v), W = point.W };

		// +1 counter-clockwise in (u, v), -1 clockwise, 0 collinear
		static int Orientation(in Point2 p, in Point2 q, in Point2 r)
		{
			return Det3(p.U, p.V, p.W, q.U, q.V, q.W, r.U, r.V, r.W).Sign;
		}

		// ---- convex polygons on a plane -----------------------------------------------------------------------------

		// A corner of a convex polygon on a plane; the edge from it to the next corner lies on `edge`.
		struct Corner
		{
			public Point point;
			public Plane edge;
		}

		// Far beyond anything an exact plane reaches (2^24 from the origin): a brush the CSG accepts lies well inside.
		static readonly BigInteger kWorld = BigInteger.One << 26;

		static Plane AxisBound(int axis, int sign)
		{
			var plane = new Plane { w = -kWorld };
			if (axis == 0) plane.a = sign; else if (axis == 1) plane.b = sign; else plane.c = sign;
			return plane;
		}

		// The plane within a square far larger than the world, counter-clockwise in its (u, v) projection.
		static List<Corner> WorldSquare(in Plane plane, int u, int v)
		{
			var edges = new[] { AxisBound(v, -1), AxisBound(u, +1), AxisBound(v, +1), AxisBound(u, -1) };
			var polygon = new List<Corner>(4);
			for (int i = 0; i < 4; i++)
			{
				if (!Intersect(plane, edges[(i + 3) % 4], edges[i], out var corner))
					throw new InvalidOperationException("ExactJudge: a plane parallel to its own projection axes");
				polygon.Add(new Corner { point = corner, edge = edges[i] });
			}
			return polygon;
		}

		// The part of a convex polygon on `face` where `plane` is not positive.
		static List<Corner> Clip(in Plane face, List<Corner> polygon, in Plane plane)
		{
			int n = polygon.Count;
			if (n == 0)
				return polygon;
			var sides = new int[n];
			bool anyOutside = false, anyInside = false;
			for (int i = 0; i < n; i++)
			{
				sides[i] = Side(plane, polygon[i].point);
				anyOutside |= sides[i] > 0;
				anyInside  |= sides[i] < 0;
			}
			if (!anyOutside)
				return polygon;
			if (!anyInside)
				return new List<Corner>();
			var result = new List<Corner>(n + 1);
			for (int i = 0; i < n; i++)
			{
				int j = (i + 1) % n;
				var corner = polygon[i];
				if (sides[i] <= 0)
				{
					if (sides[j] > 0)
					{
						// leaving: along the clipping plane from where the edge crosses it (or from this corner, on it)
						if (sides[i] < 0)
						{
							result.Add(corner);
							result.Add(new Corner { point = Cut(face, corner.edge, plane), edge = plane });
						} else
							result.Add(new Corner { point = corner.point, edge = plane });
					} else
						result.Add(corner);
				} else if (sides[j] < 0)
				{
					// entering: along this edge again from where it crosses the clipping plane
					result.Add(new Corner { point = Cut(face, corner.edge, plane), edge = corner.edge });
				}
			}
			return result;
		}

		static Point Cut(in Plane face, in Plane edge, in Plane plane)
		{
			if (!Intersect(face, edge, plane, out var point))
				throw new InvalidOperationException("ExactJudge: an edge crossing a plane it is parallel to");
			return point;
		}

		// Whether a convex polygon encloses any area.
		static bool HasArea(List<Corner> polygon, int u, int v)
		{
			if (polygon.Count < 3)
				return false;
			var first = Project(polygon[0].point, u, v);
			for (int i = 1; i + 1 < polygon.Count; i++)
			{
				if (Orientation(first, Project(polygon[i].point, u, v), Project(polygon[i + 1].point, u, v)) != 0)
					return true;
			}
			return false;
		}

		// ---- the brushes --------------------------------------------------------------------------------------------

		sealed class Brush
		{
			public ContentsSceneNode        node;
			public int                      index;          // in the oracle's brush order
			public ExactCSGCapture.Brush    capture;
			public Plane[]                  planes;
			public PlaneKey[]               keys;
			public int[]                    orientations;   // of each plane against its key
			public bool                     flat;           // a plane and its opposite: no volume
			public bool                     hasVolume;
			public readonly List<Point>     corners = new List<Point>();
			public double3                  min, max;       // around the corners, widened far beyond the rounding of doubles
		}

		static Brush Prepare(ContentsSceneNode node, int index, ExactCSGCapture.Brush capture)
		{
			int count = capture.planes.Length;
			var brush = new Brush
			{
				node = node, index = index, capture = capture,
				planes = new Plane[count], keys = new PlaneKey[count], orientations = new int[count]
			};
			for (int p = 0; p < count; p++)
			{
				brush.planes[p] = ToPlane(capture.planes[p]);
				if (brush.planes[p].a.IsZero && brush.planes[p].b.IsZero && brush.planes[p].c.IsZero)
					throw new InvalidOperationException($"ExactJudge: {Name(brush)} has an invalid plane {p}");
				brush.keys[p] = KeyOf(brush.planes[p], out brush.orientations[p]);
			}
			for (int p = 0; p < count && !brush.flat; p++)
				for (int q = p + 1; q < count && !brush.flat; q++)
					brush.flat = brush.keys[p].Equals(brush.keys[q]) && brush.orientations[p] != brush.orientations[q];
			if (brush.flat)
				return brush;

			for (int f = 0; f < count; f++)
			{
				Axes(brush.planes[f], out _, out int u, out int v);
				var polygon = Section(brush, brush.planes[f], brush.keys[f], u, v);
				if (!HasArea(polygon, u, v))
					continue;
				brush.hasVolume = true;
				foreach (var corner in polygon)
				{
					bool known = false;
					foreach (var existing in brush.corners)
						if (SamePoint(existing, corner.point)) { known = true; break; }
					if (!known)
						brush.corners.Add(corner.point);
				}
			}

			// A box around the corners, only ever used to skip what cannot meet: widened by far more than the doubles round.
			brush.min = new double3(double.PositiveInfinity);
			brush.max = new double3(double.NegativeInfinity);
			foreach (var corner in brush.corners)
			{
				double w = (double)corner.W;
				var position = new double3((double)corner.X / w, (double)corner.Y / w, (double)corner.Z / w);
				brush.min = math.min(brush.min, position);
				brush.max = math.max(brush.max, position);
			}
			for (int axis = 0; axis < 3; axis++)
			{
				brush.min[axis] -= Slack(brush.min[axis]);
				brush.max[axis] += Slack(brush.max[axis]);
			}
			return brush;
		}

		// The brush cut by the plane with the given key: the polygon of the plane inside every plane of the brush that is
		// not that plane itself.
		static List<Corner> Section(Brush brush, in Plane plane, in PlaneKey key, int u, int v)
		{
			var polygon = WorldSquare(plane, u, v);
			for (int p = 0; p < brush.planes.Length && polygon.Count > 0; p++)
			{
				if (brush.keys[p].Equals(key))
					continue;
				polygon = Clip(plane, polygon, brush.planes[p]);
			}
			return polygon;
		}

		static string Name(Brush brush) => brush.node.name ?? ("brush" + brush.index);

		// ---- one plane ----------------------------------------------------------------------------------------------

		const int kCrossing = 0, kBelow = 1, kAbove = 2;

		// A convex polygon on the judged plane: a brush's cross-section, or an output triangle
		sealed class Shape
		{
			public Brush    brush;
			public bool     triangle;
			public int      relation;   // cross-sections: kCrossing, kBelow (the brush has a face on the plane and lies
										// behind it), kAbove (in front of it)
			public int      face;       // the brush's plane on the judged plane (kBelow / kAbove), or the triangle's face
			public int      facing;     // triangles: +1 drawn along the judged plane's normal, -1 against it
			public Point2[] corners;
			public bool     active;
		}

		sealed class Edge
		{
			public Shape      shape;
			public Point2     a, b;
			public BigInteger A, B, C;          // the line: A U + B V + C W = 0
			public double     minU, maxU, minV, maxV;
		}

		struct Crossing
		{
			public Rational v;
			public Shape    shape;
		}

		sealed class Context
		{
			public ContentsOracle                   oracle;
			public List<Brush>                      brushes;
			public Dictionary<ContentsSceneNode, Brush> byNode;
			public List<ContentsComparison.Mismatch> mismatches = new List<ContentsComparison.Mismatch>();
			public int                              maxReported;
			public int                              intervals;
			// Rounded exact triangles with two corners at one float position, which the weld at the very end drops (OutputWeld)
			public int                              droppedByTheWeld;
			public int                              flippedByTheWeld;
			// Needles the weld across the model split into the triangle across them, of another brush (OutputModelWeld)
			public int                              splitByTheWeld;
			// Pairs of facing needles the weld across the model dropped (OutputModelWeld)
			public int                              pairedByTheWeld;
			// Pairs of identical triangles facing opposite ways the weld across the model dropped (OutputModelWeld)
			public int                              cancelledByTheWeld;
			public bool[]                           inMinus, inPlus;
			public bool Full => mismatches.Count >= maxReported;

			// Every brush with a face on a plane, and which of its planes that is (the first, if it has the plane twice)
			public readonly Dictionary<PlaneKey, List<(Brush brush, int plane)>> onPlane = new Dictionary<PlaneKey, List<(Brush, int)>>();

			// The brushes that can hold an interval's probes, and whether they do: nothing else can (ContentsOracle.IsFilled)
			public readonly List<ContentsSceneNode> candidates = new List<ContentsSceneNode>();
			public Func<ContentsSceneNode, bool>    insideMinus, insidePlus;

			// Per plane, how much work the judgement may take (WorkBudget); a plane that would take more is not judged but
			// listed in `unexamined`, with its numbers, so it is never mistaken for one that passed
			public WorkBudget                       budget = WorkBudget.Unlimited;
			public readonly List<string>            unexamined = new List<string>();
			// spent over the whole judgement so far, and on the last plane (for the progress output)
			public long                             spentPairs, spentSlab, lastPairs, lastSlab;
		}

		internal struct WorkBudget
		{
			public long edgePairs;
			public long slabCrossings;
			public long totalEdgePairs;
			public long totalSlabCrossings;
			public static WorkBudget Unlimited => new WorkBudget { edgePairs = long.MaxValue, slabCrossings = long.MaxValue,
																	totalEdgePairs = long.MaxValue, totalSlabCrossings = long.MaxValue };
		}

		internal static readonly WorkBudget SweepBudget = new WorkBudget { edgePairs = 2_000_000_000L, slabCrossings = 50_000_000L,
																		   totalEdgePairs = 6_000_000_000L, totalSlabCrossings = 150_000_000L };

		// What this plane may still take: its own budget, or what is left of the judgement's, whichever is less
		static long Allowed(long perPlane, long total, long spent) => total == long.MaxValue ? perPlane : Math.Min(perPlane, Math.Max(0, total - spent));

		static void JudgePlane(Context context, in PlaneKey key, List<Shape> triangles)
		{
			var plane = key.AsPlane;
			Axes(plane, out int dominant, out int u, out int v);

			// The faces on the plane, and the triangles drawn on it: nothing can be expected or drawn anywhere else on it, so
			// they are the region the plane is judged in.
			var shapes = new List<Shape>(triangles);
			var region = new List<Box2>();
			foreach (var triangle in triangles)
				region.Add(BoxOf(triangle.corners));
			var onPlane = new HashSet<Brush>();
			if (context.onPlane.TryGetValue(key, out var faces))
			{
				foreach (var (brush, face) in faces)
				{
					onPlane.Add(brush);
					var shape = SectionShape(brush, plane, key, u, v, brush.orientations[face] > 0 ? kBelow : kAbove, face);
					if (shape == null)
						continue;
					shapes.Add(shape);
					region.Add(BoxOf(shape.corners));
				}
			}
			if (region.Count == 0)
				return;
			var union = region[0];
			foreach (var box in region)
				union = Union(union, box);

			double pa = (double)plane.a, pb = (double)plane.b, pc = (double)plane.c, pw = (double)plane.w;
			foreach (var brush in context.brushes)
			{
				if (!brush.hasVolume || onPlane.Contains(brush))
					continue;
				if (!MayStraddle(brush, pa, pb, pc, pw))
					continue;
				var reach = new Box2 { minU = brush.min[u], maxU = brush.max[u], minV = brush.min[v], maxV = brush.max[v] };
				if (!Overlaps(reach, union))
					continue;
				if (region.Count <= kRegionBoxes)
				{
					bool meets = false;
					foreach (var box in region)
						if (Overlaps(reach, box)) { meets = true; break; }
					if (!meets)
						continue;
				}

				// a brush entirely on one side of the plane does not meet it
				bool below = false, above = false;
				foreach (var corner in brush.corners)
				{
					int side = Side(plane, corner);
					below |= side < 0;
					above |= side > 0;
				}
				if (!below || !above)
					continue;
				var crossing = SectionShape(brush, plane, key, u, v, kCrossing, -1);
				if (crossing != null)
					shapes.Add(crossing);
			}

			// Their edges, and every u-coordinate where the overlay has a vertex
			var edges = new List<Edge>();
			var vertexU = new List<Rational>();
			foreach (var shape in shapes)
			{
				for (int i = 0; i < shape.corners.Length; i++)
				{
					var a = shape.corners[i];
					var b = shape.corners[(i + 1) % shape.corners.Length];
					vertexU.Add(new Rational { num = a.U, den = a.W });
					var edge = new Edge
					{
						shape = shape, a = a, b = b,
						A = a.V * b.W - a.W * b.V,
						B = a.W * b.U - a.U * b.W,
						C = a.U * b.V - a.V * b.U
					};
					if (edge.B.IsZero)
						continue;   // runs along u = constant: never crosses a slab, and its ends are vertices already
					double ua = (double)a.U / (double)a.W, ub = (double)b.U / (double)b.W;
					double va = (double)a.V / (double)a.W, vb = (double)b.V / (double)b.W;
					edge.minU = Math.Min(ua, ub); edge.maxU = Math.Max(ua, ub);
					edge.minV = Math.Min(va, vb); edge.maxV = Math.Max(va, vb);
					edges.Add(edge);
				}
			}
			// in the order they start in u, so each edge only meets the ones that start before it ends
			edges.Sort((x, y) => x.minU.CompareTo(y.minU));
			long pairs = 0;
			long allowedPairs = Allowed(context.budget.edgePairs, context.budget.totalEdgePairs, context.spentPairs);
			context.lastPairs = context.lastSlab = 0;
			for (int i = 0; i < edges.Count; i++)
			{
				var e1 = edges[i];
				var end = e1.maxU + Slack(e1.maxU);
				for (int j = i + 1; j < edges.Count; j++)
				{
					var e2 = edges[j];
					if (end < e2.minU)
						break;
					if (++pairs > allowedPairs)
					{
						context.spentPairs += pairs;
						NotExamined(context, key, triangles.Count, shapes.Count, edges.Count,
									allowedPairs < context.budget.edgePairs ? "the judgement's edge pairs are spent"
																			: $"more than {context.budget.edgePairs} edge pairs to compare");
						return;
					}
					// a box test with room far beyond any rounding of the doubles: only pairs that cannot meet are skipped
					if (e1.maxV + Slack(e1.maxV) < e2.minV || e2.maxV + Slack(e2.maxV) < e1.minV)
						continue;
					int o1 = Orientation(e1.a, e1.b, e2.a), o2 = Orientation(e1.a, e1.b, e2.b);
					if (o1 == 0 || o2 == 0 || o1 == o2)
						continue;   // no crossing, or one that is at a vertex already
					int o3 = Orientation(e2.a, e2.b, e1.a), o4 = Orientation(e2.a, e2.b, e1.b);
					if (o3 == 0 || o4 == 0 || o3 == o4)
						continue;
					// the lines' intersection, (A1, B1, C1) x (A2, B2, C2)
					var U = e1.B * e2.C - e1.C * e2.B;
					var W = e1.A * e2.B - e1.B * e2.A;
					if (W.Sign < 0) { U = -U; W = -W; }
					vertexU.Add(new Rational { num = U, den = W });
				}
			}
			vertexU.Sort((x, y) => Compare(x, y));
			context.spentPairs += pairs;
			context.lastPairs = pairs;

			// The slab lines' work, counted before it is done: for every slab, the edges whose u-range reaches its line
			long allowedSlab = Allowed(context.budget.slabCrossings, context.budget.totalSlabCrossings, context.spentSlab);
			if (allowedSlab != long.MaxValue)
			{
				var starts = new double[edges.Count];
				var ends   = new double[edges.Count];
				for (int i = 0; i < edges.Count; i++) { starts[i] = edges[i].minU; ends[i] = edges[i].maxU; }
				Array.Sort(starts);
				Array.Sort(ends);
				long slabWork = 0;
				for (int s = 0; s + 1 < vertexU.Count; s++)
				{
					double lo = vertexU[s].Approximate, hi = vertexU[s + 1].Approximate;
					if (!(hi > lo))
						continue;   // one boundary, or two a double cannot tell apart: an estimate needs no more
					double mid = (lo + hi) * 0.5;
					slabWork += CountAtMost(starts, mid) - CountBelow(ends, mid);
					if (slabWork > allowedSlab)
					{
						NotExamined(context, key, triangles.Count, shapes.Count, edges.Count,
									allowedSlab < context.budget.slabCrossings
										? $"the judgement's slab crossings are spent ({context.spentSlab} of {context.budget.totalSlabCrossings})"
										: $"more than {context.budget.slabCrossings} slab crossings (edges a slab line reaches) on {vertexU.Count} slab boundaries");
						return;
					}
				}
				context.spentSlab += slabWork;
				context.lastSlab = slabWork;
			}

			// One line through the middle of every slab, left to right, with the edges whose u-range can reach it
			var crossings = new List<Crossing>();
			var active    = new List<Shape>();
			var reaching  = new List<Edge>();
			int nextEdge  = 0;
			for (int s = 0; s + 1 < vertexU.Count && !context.Full; s++)
			{
				var lo = vertexU[s];
				var hi = vertexU[s + 1];
				if (Compare(lo, hi) >= 0)
					continue;
				Between(lo, hi, out var k, out int e);
				var pow = BigInteger.One << e;
				double m = (double)k / Math.Pow(2, e);

				while (nextEdge < edges.Count && edges[nextEdge].minU - Slack(edges[nextEdge].minU) <= m)
					reaching.Add(edges[nextEdge++]);
				reaching.RemoveAll(edge => m > edge.maxU + Slack(edge.maxU));

				crossings.Clear();
				foreach (var edge in reaching)
				{
					int sa = (edge.a.U * pow - k * edge.a.W).Sign;
					int sb = (edge.b.U * pow - k * edge.b.W).Sign;
					if (sa == sb || sa == 0 || sb == 0)
						continue;
					var num = -(edge.A * k + edge.C * pow);
					var den = edge.B * pow;
					if (den.Sign < 0) { num = -num; den = -den; }
					crossings.Add(new Crossing { v = new Rational { num = num, den = den }, shape = edge.shape });
				}
				crossings.Sort((x, y) => Compare(x.v, y.v));

				active.Clear();
				for (int i = 0; i < crossings.Count && !context.Full;)
				{
					int j = i;
					for (; j < crossings.Count && Compare(crossings[j].v, crossings[i].v) == 0; j++)
					{
						var shape = crossings[j].shape;
						shape.active = !shape.active;
						if (shape.active) active.Add(shape); else active.Remove(shape);
					}
					if (j < crossings.Count && active.Count > 0)
						JudgeInterval(context, plane, dominant, u, v, m, crossings[i].v, crossings[j].v, active);
					i = j;
				}
				if (active.Count != 0 && !context.Full)
					throw new InvalidOperationException("ExactJudge: a convex shape crossed a line an odd number of times");
				foreach (var shape in active)
					shape.active = false;
			}
		}

		// A plane over the work budget: listed, with what made it heavy, instead of judged
		static void NotExamined(Context context, in PlaneKey key, int triangles, int shapes, int edges, string reason)
		{
			var plane = key.AsPlane;
			string first = null;
			if (context.onPlane.TryGetValue(key, out var faces) && faces.Count > 0)
				first = faces[0].brush.node.name;
			context.unexamined.Add($"plane ({(double)plane.a:G6}, {(double)plane.b:G6}, {(double)plane.c:G6}, {(double)plane.w:G6})" +
								   (first != null ? $" of {first} and {faces.Count - 1} more brush(es)" : "") +
								   $": {triangles} triangle(s), {shapes} shape(s), {edges} edge(s), {reason} - NOT judged");
		}

		// How many of the sorted values are <= x, and < x
		static long CountAtMost(double[] sorted, double x)
		{
			int lo = 0, hi = sorted.Length;
			while (lo < hi) { int mid = (lo + hi) >> 1; if (sorted[mid] <= x) lo = mid + 1; else hi = mid; }
			return lo;
		}

		static long CountBelow(double[] sorted, double x)
		{
			int lo = 0, hi = sorted.Length;
			while (lo < hi) { int mid = (lo + hi) >> 1; if (sorted[mid] < x) lo = mid + 1; else hi = mid; }
			return lo;
		}

		// A brush's cross-section with the plane as a shape, or null where it has no area there.
		static Shape SectionShape(Brush brush, in Plane plane, in PlaneKey key, int u, int v, int relation, int face)
		{
			var polygon = Section(brush, plane, key, u, v);
			if (!HasArea(polygon, u, v))
				return null;
			var corners = new Point2[polygon.Count];
			for (int i = 0; i < polygon.Count; i++)
				corners[i] = Project(polygon[i].point, u, v);
			return new Shape { brush = brush, relation = relation, face = face, corners = corners };
		}

		// Up to this many boxes a brush is tested against each box of a plane's region, past it only against their union.
		const int kRegionBoxes = 64;

		// A box in a plane's (u, v) projection, in doubles, only ever used to skip what cannot meet.
		struct Box2
		{
			public double minU, maxU, minV, maxV;
		}

		static Box2 BoxOf(Point2[] corners)
		{
			var box = new Box2 { minU = double.PositiveInfinity, maxU = double.NegativeInfinity,
								 minV = double.PositiveInfinity, maxV = double.NegativeInfinity };
			foreach (var corner in corners)
			{
				double w = (double)corner.W, cu = (double)corner.U / w, cv = (double)corner.V / w;
				box.minU = Math.Min(box.minU, cu); box.maxU = Math.Max(box.maxU, cu);
				box.minV = Math.Min(box.minV, cv); box.maxV = Math.Max(box.maxV, cv);
			}
			box.minU -= Slack(box.minU); box.maxU += Slack(box.maxU);
			box.minV -= Slack(box.minV); box.maxV += Slack(box.maxV);
			return box;
		}

		static Box2 Union(in Box2 x, in Box2 y)
		{
			return new Box2 { minU = Math.Min(x.minU, y.minU), maxU = Math.Max(x.maxU, y.maxU),
							  minV = Math.Min(x.minV, y.minV), maxV = Math.Max(x.maxV, y.maxV) };
		}

		static bool Overlaps(in Box2 x, in Box2 y) => x.minU <= y.maxU && y.minU <= x.maxU && x.minV <= y.maxV && y.minV <= x.maxV;

		// Whether a brush's box can reach both sides of the plane a x + b y + c z + w = 0: false only when the whole box
		// lies on one side by far more than the doubles round.
		static bool MayStraddle(Brush brush, double a, double b, double c, double w)
		{
			double low = w, high = w, magnitude = Math.Abs(w);
			Accumulate(a, brush.min.x, brush.max.x, ref low, ref high, ref magnitude);
			Accumulate(b, brush.min.y, brush.max.y, ref low, ref high, ref magnitude);
			Accumulate(c, brush.min.z, brush.max.z, ref low, ref high, ref magnitude);
			var slack = Slack(magnitude);
			return low <= slack && high >= -slack;
		}

		static void Accumulate(double n, double min, double max, ref double low, ref double high, ref double magnitude)
		{
			double p = n * min, q = n * max;
			low       += Math.Min(p, q);
			high      += Math.Max(p, q);
			magnitude += Math.Max(Math.Abs(p), Math.Abs(q));
		}

		// Room for the rounding of an exact value converted to double (a few units in the last place): far more than
		// enough, so a box test that uses it never skips anything that could meet.
		static double Slack(double value) => 1e-9 * (Math.Abs(value) + 1);

		// The simplest k / 2^e strictly between lo and hi.
		static void Between(in Rational lo, in Rational hi, out BigInteger k, out int e)
		{
			for (e = 0; ; e++)
			{
				var pow = BigInteger.One << e;
				k = FloorDivide(lo.num * pow, lo.den) + 1;
				if ((k * hi.den - hi.num * pow).Sign < 0)
					return;
			}
		}

		static BigInteger FloorDivide(BigInteger numerator, BigInteger denominator)
		{
			var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
			return remainder.Sign < 0 ? quotient - 1 : quotient;
		}

		static void JudgeInterval(Context context, in Plane plane, int dominant, int u, int v, double m,
								  in Rational vLow, in Rational vHigh, List<Shape> active)
		{
			context.intervals++;

			// Which brushes hold the points an infinitely small step behind (minus) and in front of (plus) the plane here;
			// no other brush can
			context.candidates.Clear();
			foreach (var shape in active)
			{
				if (shape.triangle)
					continue;
				int b = shape.brush.index;
				context.inMinus[b] = shape.relation != kAbove;
				context.inPlus[b]  = shape.relation != kBelow;
				context.candidates.Add(shape.brush.node);
			}

			var expected = new List<ContentsComparison.Face>();
			foreach (var shape in active)
			{
				if (shape.triangle || shape.relation == kCrossing)
					continue;
				var judging = shape.brush.node.contents;
				bool minusFilled = context.oracle.IsFilled(context.candidates, context.insideMinus, judging);
				bool plusFilled  = context.oracle.IsFilled(context.candidates, context.insidePlus, judging);
				// the brush's face points along the plane's normal when the brush lies behind it
				bool behind = shape.relation == kBelow ? minusFilled : plusFilled;
				bool front  = shape.relation == kBelow ? plusFilled : minusFilled;
				int facing;
				if (behind && !front)       facing = 1;     // drawn facing out of its brush
				else if (!behind && front)  facing = -1;    // drawn facing into it
				else continue;
				if (shape.relation == kAbove)
					facing = -facing;
				expected.Add(new ContentsComparison.Face { brush = shape.brush.node, facing = facing });
			}

			var found = new List<ContentsComparison.Face>();
			string problem = null;
			foreach (var shape in active)
			{
				if (!shape.triangle)
					continue;
				var face = new ContentsComparison.Face { brush = shape.brush.node, facing = shape.facing };
				if (found.Contains(face))
					problem ??= $"the output draws {face} more than once here";
				found.Add(face);
			}
			problem ??= ContentsComparison.Compare(expected, found);

			foreach (var shape in active)
			{
				if (!shape.triangle)
					context.inMinus[shape.brush.index] = context.inPlus[shape.brush.index] = false;
			}
			if (problem == null)
				return;

			// Named after a face the oracle expects here, else a triangle drawn here
			Shape named = null;
			foreach (var shape in active)
				if (!shape.triangle && shape.relation != kCrossing) { named = shape; break; }
			if (named == null)
				foreach (var shape in active)
					if (shape.triangle) { named = shape; break; }
			named ??= active[0];

			double pv = (vLow.Approximate + vHigh.Approximate) * 0.5;
			var point = new double3();
			point[u] = m;
			point[v] = pv;
			point[dominant] = -((double)plane.Normal(u) * m + (double)plane.Normal(v) * pv + (double)plane.w) / (double)plane.Normal(dominant);
			context.mismatches.Add(new ContentsComparison.Mismatch
			{
				brush      = named.brush.node,
				brushIndex = named.brush.index,
				face       = named.face,
				point      = (float3)point,
				problem    = problem,
				nearest    = 0
			});
		}

		// ---- the whole judgement ------------------------------------------------------------------------------------

		// Where a long judgement spends its time, plane by plane: set by a caller that needs to see it
		// (SampleSceneHarvestTests.ProfileModel), null otherwise.
		internal static Action<string> progress;

		internal static List<ContentsComparison.Mismatch> FindMismatches(ContentsTreeHarness harness, int maxReported, out int judged,
																		 bool checkMesh = true)
		{
			return FindMismatches(harness, maxReported, out judged, out _, WorkBudget.Unlimited, checkMesh);
		}

		// The same, with a work budget per plane: a plane over it is listed in `unexamined` instead of judged. Only for
		// sweeps over real scenes (SampleSceneHarvestTests.CheckModels); tests judge every plane.
		internal static List<ContentsComparison.Mismatch> FindMismatches(ContentsTreeHarness harness, int maxReported, out int judged,
																		 out List<string> unexamined, WorkBudget budget,
																		 bool checkMesh = true)
		{
			var oracle  = new ContentsOracle(harness.Scene);
			var context = new Context
			{
				oracle      = oracle,
				brushes     = new List<Brush>(),
				byNode      = new Dictionary<ContentsSceneNode, Brush>(),
				maxReported = maxReported,
				budget      = budget
			};
			unexamined = context.unexamined;
			var sceneBrushes = oracle.Brushes;
			for (int b = 0; b < sceneBrushes.Count; b++)
			{
				var node   = sceneBrushes[b];
				var nodeID = harness.NodeIDOf(node);
				if (!ExactCSGCapture.Brushes.TryGetValue(nodeID, out var capture) || capture.planes == null)
					throw new InvalidOperationException($"ExactJudge: nothing was captured for {node.name ?? ("brush" + b)}; " +
														"does the harness run with the exact CSG on?");
				var brush = Prepare(node, b, capture);
				context.brushes.Add(brush);
				context.byNode[node] = brush;
			}
			context.inMinus = new bool[context.brushes.Count];
			context.inPlus  = new bool[context.brushes.Count];
			context.insideMinus = node => context.inMinus[context.byNode[node].index];
			context.insidePlus  = node => context.inPlus[context.byNode[node].index];
			foreach (var brush in context.brushes)
			{
				if (!brush.hasVolume)
					continue;
				for (int p = 0; p < brush.planes.Length; p++)
				{
					if (!context.onPlane.TryGetValue(brush.keys[p], out var list))
						context.onPlane[brush.keys[p]] = list = new List<(Brush, int)>();
					if (list.Count == 0 || list[list.Count - 1].brush != brush)
						list.Add((brush, p));
				}
			}

			// The input as the CSG saw it: the planes it used are the ones the brush has now, and a brush takes part exactly
			// when those planes enclose volume
			foreach (var brush in context.brushes)
			{
				CheckInput(context, harness, brush);
				bool coreSaysVolume = brush.capture.cornerCount > 0;
				if (brush.hasVolume != coreSaysVolume)
					Report(context, brush, -1, float3.zero,
						   $"the exact CSG counted {brush.capture.cornerCount} corners, the judge finds {(brush.hasVolume ? "volume" : "none")}");
			}

			// The triangles, on the planes of the faces they belong to
			var trianglesByPlane = new Dictionary<PlaneKey, List<Shape>>();
			var planeOf          = new Dictionary<PlaneKey, Plane>();
			foreach (var brush in context.brushes)
			{
				for (int p = 0; p < brush.planes.Length; p++)
				{
					if (!brush.hasVolume)
						break;
					if (!trianglesByPlane.ContainsKey(brush.keys[p]))
						trianglesByPlane[brush.keys[p]] = new List<Shape>();
				}
				if (brush.capture.parts == null)
					continue;
				foreach (var part in brush.capture.parts)
					CheckPart(context, brush, part, trianglesByPlane);
			}

			int planeIndex = 0;
			foreach (var pair in trianglesByPlane)
			{
				if (context.Full)
					break;
				var clock = progress != null ? System.Diagnostics.Stopwatch.StartNew() : null;
				if (progress != null)
					progress($"plane {planeIndex} of {trianglesByPlane.Count}: {pair.Value.Count} triangle(s), " +
							 $"{(context.onPlane.TryGetValue(pair.Key, out var faces) ? faces.Count : 0)} face(s) on it, {context.intervals} intervals so far");
				JudgePlane(context, pair.Key, pair.Value);
				if (progress != null)
					progress($"plane {planeIndex}: {clock.Elapsed.TotalSeconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)} s, " +
							 $"{context.lastPairs} edge pairs, {context.lastSlab} slab crossings, {context.intervals} intervals so far");
				planeIndex++;
			}

			if (!context.Full)
				CheckAttributes(context, harness);
			if (checkMesh && !context.Full)
				CheckMesh(context, harness);
			judged = context.intervals;
			LastDroppedByTheWeld = context.droppedByTheWeld;
			LastFlippedByTheWeld = context.flippedByTheWeld;
			LastSplitByTheWeld   = context.splitByTheWeld;
			LastPairedByTheWeld  = context.pairedByTheWeld;
			LastCancelledByTheWeld = context.cancelledByTheWeld;
			return context.mismatches;
		}

		// How many needles of the last judgement's rounded exact triangles the weld's edge flip took away (with those the weld
		// across the model split into a triangle of the same brush: the mesh shows the same)
		internal static int LastFlippedByTheWeld { get; private set; }

		// How many needles of the last judgement's rounded exact triangles the weld across the model split into the triangle
		// across them, of another brush, or into a half of a triangle it split before
		internal static int LastSplitByTheWeld { get; private set; }

		// How many pairs of facing needles of the last judgement's rounded exact triangles the weld across the model dropped
		internal static int LastPairedByTheWeld { get; private set; }

		// How many pairs of identical triangles facing opposite ways, of the last judgement's rounded exact triangles or the
		// halves of a split, the weld across the model dropped
		internal static int LastCancelledByTheWeld { get; private set; }

		// How many of the last judgement's rounded exact triangles the weld at the very end drops, so the mesh never has them
		internal static int LastDroppedByTheWeld { get; private set; }

		static void CheckInput(Context context, ContentsTreeHarness harness, Brush brush)
		{
			var treeBrush = harness.TreeBrushOf(brush.node);
			if (!treeBrush.Valid)
			{
				Report(context, brush, -1, float3.zero, "the brush has no tree brush in the harness");
				return;
			}
			var expected = new List<ExactPlane>();
			var instance = treeBrush.BrushMesh;
			var meshRef  = instance.Valid ? BrushMeshManager.GetBrushMeshBlob(instance) : default;
			if (meshRef.IsCreated)
			{
				var m      = treeBrush.NodeToTreeSpaceMatrix;
				var affine = ExactAffine.Create(m.c0.x, m.c0.y, m.c0.z, m.c1.x, m.c1.y, m.c1.z,
												m.c2.x, m.c2.y, m.c2.z, m.c3.x, m.c3.y, m.c3.z);
				ref var mesh = ref meshRef.Value;
				for (int p = 0; p < mesh.localPlaneCount; p++)
				{
					var plane = default(ExactPlane);
					if (affine.IsValid)
					{
						var local = mesh.localPlanes[p];
						affine.TransformPlane(local.x, local.y, local.z, local.w, out double nx, out double ny, out double nz, out double d);
						plane = ExactPlane.Quantize(nx, ny, nz, d);
					}
					expected.Add(plane);
				}
			}
			var captured = brush.capture.planes;
			if (captured.Length != expected.Count)
			{
				Report(context, brush, -1, float3.zero,
					   $"the exact CSG used {captured.Length} planes, the brush's current mesh has {expected.Count}");
				return;
			}
			for (int p = 0; p < captured.Length; p++)
			{
				var c = captured[p]; var e = expected[p];
				if (c.a == e.a && c.b == e.b && c.c == e.c && c.w == e.w)
					continue;
				Report(context, brush, p, float3.zero,
					   $"the exact CSG used plane {p} = ({c.a}, {c.b}, {c.c}, {c.w}), which is not the brush's current plane ({e.a}, {e.b}, {e.c}, {e.w})");
				return;
			}
		}

		static void CheckPart(Context context, Brush brush, ExactCSGCapture.Part part, Dictionary<PlaneKey, List<Shape>> trianglesByPlane)
		{
			if (part.surface < 0 || part.surface >= brush.planes.Length)
			{
				Report(context, brush, part.surface, float3.zero, $"the exact CSG drew on face {part.surface}, which the brush does not have");
				return;
			}
			var plane = brush.planes[part.surface];
			var key   = brush.keys[part.surface];
			Axes(plane, out int dominant, out int u, out int v);
			var points = new Point[part.vertices.Length];
			for (int i = 0; i < points.Length; i++)
			{
				points[i] = ToPoint(part.vertices[i]);
				if (Side(plane, points[i]) != 0)
					Report(context, brush, part.surface, part.positions[i], "an output vertex does not lie on its face's plane");
				var rounded = new float3(NearestFloat(points[i].X, points[i].W), NearestFloat(points[i].Y, points[i].W),
										 NearestFloat(points[i].Z, points[i].W));
				if (math.any(math.asint(rounded) != math.asint(part.positions[i])))
					Report(context, brush, part.surface, part.positions[i],
						   $"an output vertex was emitted as {Format(part.positions[i])}, the nearest float is {Format(rounded)}");
			}

			// the plane's normal along the dominant axis decides which way round (u, v) is seen from it
			int handedness = plane.Normal(dominant).Sign;
			int drawnAlong = (part.category == CategoryIndex.SelfAligned ? 1 : -1) * brush.orientations[part.surface];
			if (!trianglesByPlane.TryGetValue(key, out var list))
				trianglesByPlane[key] = list = new List<Shape>();
			for (int t = 0; t + 2 < part.triangles.Length; t += 3)
			{
				var corners = new[]
				{
					Project(points[part.triangles[t]], u, v),
					Project(points[part.triangles[t + 1]], u, v),
					Project(points[part.triangles[t + 2]], u, v)
				};
				if (Orientation(corners[0], corners[1], corners[2]) * handedness <= 0)
				{
					Report(context, brush, part.surface, part.positions[part.triangles[t]],
						   "an output triangle is not counter-clockwise seen from its face's normal, or has no area");
					continue;
				}
				list.Add(new Shape { brush = brush, triangle = true, face = part.surface, facing = drawnAlong, corners = corners });
			}
		}

		static void CheckAttributes(Context context, ContentsTreeHarness harness)
		{
			foreach (var triangle in harness.Triangles)
			{
				string what = null;
				if (!Finite(triangle.a) || !Finite(triangle.b) || !Finite(triangle.c))                         what = "position";
				else if (!Finite(triangle.normalA) || !Finite(triangle.normalB) || !Finite(triangle.normalC))  what = "normal";
				else if (!Finite(triangle.tangentA) || !Finite(triangle.tangentB) || !Finite(triangle.tangentC)) what = "tangent";
				else if (!Finite(triangle.uvA) || !Finite(triangle.uvB) || !Finite(triangle.uvC))               what = "texture coordinate";
				else if (!Finite(triangle.lightmapA) || !Finite(triangle.lightmapB) || !Finite(triangle.lightmapC)) what = "lightmap coordinate";
				if (what == null)
					continue;
				var node = harness.NodeOf(triangle.brushID);
				if (node == null || !context.byNode.TryGetValue(node, out var brush))
					continue;
				Report(context, brush, -1, triangle.Center,
					   $"the mesh has a vertex whose {what} is not a number, on the triangle {Format(triangle.a)} {Format(triangle.b)} {Format(triangle.c)}");
				if (context.Full)
					return;
			}
		}

		static bool Finite(float2 v) => math.all(math.isfinite(v));
		static bool Finite(float3 v) => math.all(math.isfinite(v));
		static bool Finite(float4 v) => math.all(math.isfinite(v));

		static void CheckMesh(Context context, ContentsTreeHarness harness)
		{
			var difference = new Dictionary<(int, int, int, int, int, int, int, int, int, int), int>();
			foreach (var brush in context.brushes)
			{
				if (brush.capture.parts == null)
					continue;
				foreach (var part in brush.capture.parts)
				{
					for (int t = 0; t + 2 < part.triangles.Length; t += 3)
					{
						var a = part.positions[part.triangles[t]];
						var b = part.positions[part.triangles[t + 1]];
						var c = part.positions[part.triangles[t + 2]];
						if (ContentsTreeHarness.TwoCornersAtOnePosition(a, b, c))
						{
							context.droppedByTheWeld++;
							continue;
						}
						var key = part.category == CategoryIndex.SelfAligned ? TriangleKey(brush.index, a, b, c) : TriangleKey(brush.index, a, c, b);
						difference.TryGetValue(key, out int count);
						difference[key] = count + 1;
					}
				}
			}
			foreach (var triangle in harness.Triangles)
			{
				var node = harness.NodeOf(triangle.brushID);
				if (node == null || !context.byNode.TryGetValue(node, out var brush))
					continue;
				var key = TriangleKey(brush.index, triangle.a, triangle.b, triangle.c);
				difference.TryGetValue(key, out int count);
				difference[key] = count - 1;
			}
			context.flippedByTheWeld = CancelFlippedNeedles(difference);
			CancelModelWeld(difference, out context.splitByTheWeld, out context.pairedByTheWeld, out context.cancelledByTheWeld);
			foreach (var pair in difference)
			{
				if (pair.Value == 0)
					continue;
				var k = pair.Key;
				var a = math.asfloat(new int3(k.Item2, k.Item3, k.Item4));
				var b = math.asfloat(new int3(k.Item5, k.Item6, k.Item7));
				var c = math.asfloat(new int3(k.Item8, k.Item9, k.Item10));
				var what = pair.Value > 0 ? "the exact CSG made a triangle the mesh does not have" : "the mesh has a triangle the exact CSG did not make";
				Report(context, context.brushes[k.Item1], -1, (a + b + c) / 3,
					   $"{what} ({Math.Abs(pair.Value)}x): {Format(a)} {Format(b)} {Format(c)}");
				if (context.Full)
					return;
			}
		}

		static int CancelFlippedNeedles(Dictionary<(int, int, int, int, int, int, int, int, int, int), int> difference)
		{
			// the expected triangles the mesh lacks, by brush and directed edge
			var byEdge = new Dictionary<(int, int3, int3), List<(int, int, int, int, int, int, int, int, int, int)>>();
			foreach (var pair in difference)
			{
				if (pair.Value <= 0)
					continue;
				var k = pair.Key;
				var corners = new[] { new int3(k.Item2, k.Item3, k.Item4), new int3(k.Item5, k.Item6, k.Item7), new int3(k.Item8, k.Item9, k.Item10) };
				for (int e = 0; e < 3; e++)
				{
					var edge = (k.Item1, corners[e], corners[(e + 1) % 3]);
					if (!byEdge.TryGetValue(edge, out var list))
						byEdge[edge] = list = new List<(int, int, int, int, int, int, int, int, int, int)>();
					list.Add(k);
				}
			}
			var cancelled = 0;
			foreach (var needle in new List<(int, int, int, int, int, int, int, int, int, int)>(difference.Keys))
			{
				if (difference[needle] <= 0)
					continue;
				var a = math.asfloat(new int3(needle.Item2, needle.Item3, needle.Item4));
				var b = math.asfloat(new int3(needle.Item5, needle.Item6, needle.Item7));
				var c = math.asfloat(new int3(needle.Item8, needle.Item9, needle.Item10));
				if (!ExactFloatLine.AsNeedle(a, b, c, out var u, out var m, out var w))
					continue;
				if (!byEdge.TryGetValue((needle.Item1, math.asint(u), math.asint(w)), out var neighbours))
					continue;
				foreach (var neighbour in neighbours)
				{
					if (neighbour.Equals(needle) || difference[neighbour] <= 0)
						continue;
					var n = new[] { math.asfloat(new int3(neighbour.Item2, neighbour.Item3, neighbour.Item4)),
									math.asfloat(new int3(neighbour.Item5, neighbour.Item6, neighbour.Item7)),
									math.asfloat(new int3(neighbour.Item8, neighbour.Item9, neighbour.Item10)) };
					var d = !n[0].Equals(u) && !n[0].Equals(w) ? n[0] : (!n[1].Equals(u) && !n[1].Equals(w) ? n[1] : n[2]);
					if (ExactFloatLine.OnOneLine(u, w, d))
						continue;
					var first  = TriangleKey(needle.Item1, u, m, d);
					var second = TriangleKey(needle.Item1, m, w, d);
					difference.TryGetValue(first, out var firstCount);
					difference.TryGetValue(second, out var secondCount);
					if (firstCount >= 0 || secondCount >= 0)
						continue;
					difference[needle]    -= 1;
					difference[neighbour] -= 1;
					difference[first]     = firstCount + 1;
					difference[second]    = secondCount + 1;
					cancelled++;
					break;
				}
			}
			return cancelled;
		}

		static void CancelModelWeld(Dictionary<(int, int, int, int, int, int, int, int, int, int), int> difference, out int split, out int paired, out int cancelled)
		{
			split = 0;
			paired = 0;
			cancelled = 0;
			for (var progress = true; progress; )
			{
				progress  = CancelOppositesOnce(difference, ref cancelled);
				progress |= CancelModelWeldOnce(difference, ref split, ref paired);
			}
		}

		// The expected triangles the mesh lacks, by their positions alone (the key's brush -1)
		static Dictionary<(int, int, int, int, int, int, int, int, int, int), List<(int, int, int, int, int, int, int, int, int, int)>> LackedByPositions(
			Dictionary<(int, int, int, int, int, int, int, int, int, int), int> difference)
		{
			var byPositions = new Dictionary<(int, int, int, int, int, int, int, int, int, int), List<(int, int, int, int, int, int, int, int, int, int)>>();
			foreach (var pair in difference)
			{
				if (pair.Value <= 0)
					continue;
				var k = pair.Key;
				var positions = TriangleKey(-1, math.asfloat(new int3(k.Item2, k.Item3, k.Item4)), math.asfloat(new int3(k.Item5, k.Item6, k.Item7)),
											math.asfloat(new int3(k.Item8, k.Item9, k.Item10)));
				if (!byPositions.TryGetValue(positions, out var list))
					byPositions[positions] = list = new List<(int, int, int, int, int, int, int, int, int, int)>();
				list.Add(k);
			}
			return byPositions;
		}

		// The key with its corners wound the other way round, by positions alone
		static (int, int, int, int, int, int, int, int, int, int) Reversed((int, int, int, int, int, int, int, int, int, int) k)
		{
			return TriangleKey(-1, math.asfloat(new int3(k.Item2, k.Item3, k.Item4)), math.asfloat(new int3(k.Item8, k.Item9, k.Item10)),
							   math.asfloat(new int3(k.Item5, k.Item6, k.Item7)));
		}

		// Expected triangles the mesh lacks, two at a time, at the same three positions wound the other way round. Needles are
		// left to CancelModelWeldOnce: two facing needles are a pair there.
		static bool CancelOppositesOnce(Dictionary<(int, int, int, int, int, int, int, int, int, int), int> difference, ref int cancelled)
		{
			var byPositions = LackedByPositions(difference);
			var progress = false;
			foreach (var k in new List<(int, int, int, int, int, int, int, int, int, int)>(difference.Keys))
			{
				if (difference[k] <= 0)
					continue;
				if (ExactFloatLine.OnOneLine(math.asfloat(new int3(k.Item2, k.Item3, k.Item4)), math.asfloat(new int3(k.Item5, k.Item6, k.Item7)),
											 math.asfloat(new int3(k.Item8, k.Item9, k.Item10))))
					continue;
				if (!byPositions.TryGetValue(Reversed(k), out var twins))
					continue;
				foreach (var twin in twins)
				{
					if (twin.Equals(k) || difference[twin] <= 0 || difference[k] <= 0)
						continue;
					difference[k]    -= 1;
					difference[twin] -= 1;
					cancelled++;
					progress = true;
					break;
				}
			}
			return progress;
		}

		// A triangle the mesh does not have, which the weld cancelled with an expected triangle at its positions wound the
		// other way round
		static bool CancelledWithATwin(Dictionary<(int, int, int, int, int, int, int, int, int, int), int> difference,
									   Dictionary<(int, int, int, int, int, int, int, int, int, int), List<(int, int, int, int, int, int, int, int, int, int)>> lacked,
									   (int, int, int, int, int, int, int, int, int, int) key)
		{
			difference.TryGetValue(key, out var count);
			return count == 0 && lacked.ContainsKey(Reversed(key));
		}

		static bool CancelModelWeldOnce(Dictionary<(int, int, int, int, int, int, int, int, int, int), int> difference, ref int split, ref int paired)
		{
			var progress = false;
			var lacked = LackedByPositions(difference);
			// the expected triangles the mesh lacks, by directed edge, of any brush
			var byEdge = new Dictionary<(int3, int3), List<(int, int, int, int, int, int, int, int, int, int)>>();
			foreach (var pair in difference)
			{
				if (pair.Value <= 0)
					continue;
				var k = pair.Key;
				var corners = new[] { new int3(k.Item2, k.Item3, k.Item4), new int3(k.Item5, k.Item6, k.Item7), new int3(k.Item8, k.Item9, k.Item10) };
				for (int e = 0; e < 3; e++)
				{
					var edge = (corners[e], corners[(e + 1) % 3]);
					if (!byEdge.TryGetValue(edge, out var list))
						byEdge[edge] = list = new List<(int, int, int, int, int, int, int, int, int, int)>();
					list.Add(k);
				}
			}
			foreach (var needle in new List<(int, int, int, int, int, int, int, int, int, int)>(difference.Keys))
			{
				if (difference[needle] <= 0)
					continue;
				var a = math.asfloat(new int3(needle.Item2, needle.Item3, needle.Item4));
				var b = math.asfloat(new int3(needle.Item5, needle.Item6, needle.Item7));
				var c = math.asfloat(new int3(needle.Item8, needle.Item9, needle.Item10));
				if (!ExactFloatLine.AsNeedle(a, b, c, out var u, out var m, out var w))
					continue;
				if (!byEdge.TryGetValue((math.asint(u), math.asint(w)), out var neighbours))
					continue;
				foreach (var neighbour in neighbours)
				{
					if (neighbour.Equals(needle) || difference[needle] <= 0 || difference[neighbour] <= 0)
						continue;
					var n = new[] { math.asfloat(new int3(neighbour.Item2, neighbour.Item3, neighbour.Item4)),
									math.asfloat(new int3(neighbour.Item5, neighbour.Item6, neighbour.Item7)),
									math.asfloat(new int3(neighbour.Item8, neighbour.Item9, neighbour.Item10)) };
					var d = !n[0].Equals(u) && !n[0].Equals(w) ? n[0] : (!n[1].Equals(u) && !n[1].Equals(w) ? n[1] : n[2]);
					if (!ExactFloatLine.OnOneLine(u, w, d))
					{
						var first  = TriangleKey(neighbour.Item1, u, m, d);
						var second = TriangleKey(neighbour.Item1, m, w, d);
						difference.TryGetValue(first, out var firstCount);
						difference.TryGetValue(second, out var secondCount);
						// the mesh has neither half, and the weld cancelled neither with a twin: not this split
						if (firstCount >= 0 && secondCount >= 0 &&
							!CancelledWithATwin(difference, lacked, first) && !CancelledWithATwin(difference, lacked, second))
							continue;
						difference[needle]    -= 1;
						difference[neighbour] -= 1;
						difference[first]     = firstCount + 1;
						difference[second]    = secondCount + 1;
						split++;
						progress = true;
						break;
					}
					if (math.all(math.asint(d) == math.asint(m)))
					{
						difference[needle]    -= 1;
						difference[neighbour] -= 1;
						paired++;
						progress = true;
						break;
					}
				}
			}
			return progress;
		}

		// A brush's triangle as its vertices' float bits, turned to the smallest of its three rotations, so the same
		// triangle wound the same way always gives the same key.
		static (int, int, int, int, int, int, int, int, int, int) TriangleKey(int brush, float3 a, float3 b, float3 c)
		{
			var ia = math.asint(a); var ib = math.asint(b); var ic = math.asint(c);
			var r0 = (ia, ib, ic);
			var r1 = (ib, ic, ia);
			var r2 = (ic, ia, ib);
			var best = r0;
			if (Less(r1, best)) best = r1;
			if (Less(r2, best)) best = r2;
			return (brush, best.Item1.x, best.Item1.y, best.Item1.z, best.Item2.x, best.Item2.y, best.Item2.z,
					best.Item3.x, best.Item3.y, best.Item3.z);
		}

		static bool Less((int3, int3, int3) p, (int3, int3, int3) q)
		{
			int c = Order(p.Item1, q.Item1);
			if (c == 0) c = Order(p.Item2, q.Item2);
			if (c == 0) c = Order(p.Item3, q.Item3);
			return c < 0;
		}

		static int Order(int3 p, int3 q) => p.x != q.x ? p.x.CompareTo(q.x) : (p.y != q.y ? p.y.CompareTo(q.y) : p.z.CompareTo(q.z));

		static void Report(Context context, Brush brush, int face, float3 point, string problem)
		{
			if (context.Full)
				return;
			context.mismatches.Add(new ContentsComparison.Mismatch
			{
				brush = brush.node, brushIndex = brush.index, face = face, point = point, problem = problem, nearest = 0
			});
		}

		static string Format(float3 p) => $"({p.x:R}, {p.y:R}, {p.z:R})";

		// ---- rounding -----------------------------------------------------------------------------------------------

		// The float nearest to num / den (den > 0), ties to the even one: what the CSG must emit, found independently.
		static float NearestFloat(BigInteger num, BigInteger den)
		{
			if (num.IsZero)
				return 0f;
			float estimate = (float)((double)num / (double)den);
			float best = estimate;
			foreach (var candidate in new[] { NextFloat(estimate, false), NextFloat(estimate, true) })
			{
				int closer = CompareDistance(num, den, candidate, best);
				if (closer < 0 || (closer == 0 && (math.asint(candidate) & 1) == 0))
					best = candidate;
			}
			return best;
		}

		// sign(|num/den - x| - |num/den - y|)
		static int CompareDistance(BigInteger num, BigInteger den, float x, float y)
		{
			ToRational(x, out var xn, out var xd);
			ToRational(y, out var yn, out var yd);
			// |num/den - xn/xd| = |num xd - xn den| / (den xd)
			var dx = BigInteger.Abs(num * xd - xn * den) * yd;
			var dy = BigInteger.Abs(num * yd - yn * den) * xd;
			return dx.CompareTo(dy);
		}

		// A finite float as an exact fraction with a power-of-two denominator.
		static void ToRational(float value, out BigInteger num, out BigInteger den)
		{
			int bits = math.asint(value);
			int exponent = (bits >> 23) & 0xFF;
			long mantissa = bits & 0x7FFFFF;
			if (exponent == 0) exponent = 1; else mantissa |= 1L << 23;
			int power = exponent - 127 - 23;
			num = new BigInteger(bits < 0 ? -mantissa : mantissa);
			den = BigInteger.One;
			if (power >= 0) num <<= power; else den <<= -power;
		}

		static float NextFloat(float value, bool up)
		{
			int bits = math.asint(value);
			if (value == 0f)
				bits = up ? 1 : unchecked((int)0x80000001);
			else if ((value > 0) == up)
				bits++;
			else
				bits--;
			return math.asfloat(bits);
		}
	}
}
