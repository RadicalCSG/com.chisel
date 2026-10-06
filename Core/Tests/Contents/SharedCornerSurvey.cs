using System.Collections.Generic;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	public static class SharedCornerSurvey
	{
		// Output vertices are collected within the weld distance of a corner: that is as far as any
		// decision about it can move it.
		public const float kWeldRadius    = CSGConstants.kVertexEqualEpsilon;
		// Two decisions that land closer than this count as the same. 0.05 mm is the bottom of the
		// disagreement population measured on bm_c0a0a, and far above float noise at these sizes.
		public const float kSamePosition  = 0.00005f;
		// A corner lies on a brush's boundary when it is within this of the brush's planes.
		const double kOnBoundary          = 0.0001;
		// A plane passes through a corner when it is within this of it; any other plane closer than
		// the weld radius means the weld has something to decide there.
		const double kThroughCorner       = 0.00001;
		// Three planes that are this close to dependent don't define a usable point.
		const double kMinDeterminant      = 0.000001;
		// Corners closer than this are one corner.
		const float kSameCorner           = 0.00001f;
		// Output vertices closer than this are one vertex. Well below kSamePosition, so a pair of
		// vertices a weld left apart is never folded into one here.
		const float kSameVertex           = 0.000001f;

		public struct Corner
		{
			public float3 position;
			public int    brushA, brushB;
			// The planes of each brush that pass through the corner: its faces there.
			public int[]  planesA, planesB;
			// No plane of any brush near this corner comes within the weld radius, other than the ones
			// passing through it: the weld has nothing to decide here, so both sides must agree.
			public bool   isolated;
		}

		// In rising order of how bad it is; a corner takes the worst verdict of its face pairs.
		public enum Verdict
		{
			// Neither brush drew anything near the corner on its faces through it: something else covers it.
			Hidden,
			// Both put their nearest vertex at the same place.
			Same,
			// Each emitted one vertex, at measurably different places.
			Apart,
			// One side emitted one vertex, the other two, and their nearest ones differ.
			OneVsTwo,
			// Any other disagreement about where the corner is.
			Other,
			// One side drew faces through the corner and the other drew nothing near it: a hole.
			Missing
		}

		public struct Result
		{
			public Verdict verdict;
			// Between the two sides' nearest vertices, the largest over the face pairs that disagree;
			// NaN when there is nothing to measure.
			public float   distance;
			public int     countA, countB;
			// Whether each side drew any vertex near the corner on its faces through it
			public bool    drewA, drewB;
			public bool    isDisagreement => verdict >= Verdict.Apart;
			// The two sides put the corner in different places. Unlike Missing, which can come from a
			// face lost anywhere along its outline, this is about the corner itself.
			public bool    isPositionDisagreement => verdict == Verdict.Apart || verdict == Verdict.OneVsTwo || verdict == Verdict.Other;
		}

		public enum Expectation
		{
			// None of the side's faces through the corner is drawn around it: the side must draw nothing
			// near the corner.
			NoFace,
			// A face is drawn all the way around the corner: a vertex there is allowed, not needed.
			DrawnThrough,
			// A face the corner lies inside of is cut by one straight line through it: a vertex there is
			// allowed, not needed.
			StraightBoundary,
			// A face's drawn region bends or ends at the corner: the side must have a vertex there.
			Boundary
		}

		// Well inside the weld radius, so the circle only meets planes through the corner, and far enough
		// out that float error at scene coordinates (a few micrometres) can't flip a sample.
		public const float kExpectationRadius = 0.008f;
		// The probes on either side of a face. Much smaller than the radius, so a probe beside a sharp edge
		// doesn't step out through the neighbouring face.
		const float kExpectationOffset        = 0.00004f;
		const int   kExpectationSectors       = 720;

		public static Expectation Expect(ContentsOracle oracle, ContentsSceneNode brush, int[] facePlanes, float3 corner)
		{
			var planes = oracle.TreePlanesOf(brush);
			var result = Expectation.NoFace;
			foreach (var plane in facePlanes)
			{
				var expectation = ExpectFace(SampleAround(oracle, brush, planes, plane, corner));
				if (expectation > result)
					result = expectation;
			}
			return result;
		}

		// Per direction around the corner on one face: true drawn, false not drawn, null off the face.
		static bool?[] SampleAround(ContentsOracle oracle, ContentsSceneNode brush, float4[] planes, int plane, float3 corner)
		{
			var normal = planes[plane].xyz;
			var u = math.normalize(math.cross(normal, math.abs(normal.x) < 0.9f ? new float3(1, 0, 0) : new float3(0, 1, 0)));
			var v = math.cross(normal, u);
			var sectors = new bool?[kExpectationSectors];
			for (int k = 0; k < sectors.Length; k++)
			{
				// Off the sector boundaries, so a plane through the corner at a round angle isn't sampled
				var angle = (k + 0.37f) * 2 * math.PI / sectors.Length;
				var point = corner + kExpectationRadius * ((math.cos(angle) * u) + (math.sin(angle) * v));
				point -= normal * (math.dot(normal, point) + planes[plane].w);
				var onFace = true;
				for (int q = 0; q < planes.Length && onFace; q++)
					onFace = q == plane || math.dot(planes[q].xyz, point) + planes[q].w <= 0;
				if (onFace)
					sectors[k] = oracle.JudgeFace(brush, plane, point, kExpectationOffset) != ContentsFaceVerdict.None;
			}
			return sectors;
		}

		// Classifies one face from its samples around the corner.
		public static Expectation ExpectFace(bool?[] sectors)
		{
			var anyDrawn = false;
			var anyUndrawn = false;
			var offFace = false;
			foreach (var sector in sectors)
			{
				if (sector == null) offFace = true;
				else if (sector.Value) anyDrawn = true;
				else anyUndrawn = true;
			}
			if (!anyDrawn)   return Expectation.NoFace;
			if (!anyUndrawn) return Expectation.DrawnThrough;

			// Where drawn turns into undrawn or back. On a face the corner only bounds (the edge brush's), any
			// change means an outline ends at the edge there.
			var transitions = new List<int>();
			for (int k = 0; k < sectors.Length; k++)
			{
				var current = sectors[k];
				var next = sectors[(k + 1) % sectors.Length];
				if (current != null && next != null && current != next)
					transitions.Add(k);
			}
			if (!offFace && transitions.Count == 2 &&
				math.abs(math.abs(transitions[1] - transitions[0]) - (sectors.Length / 2)) <= 1)
				return Expectation.StraightBoundary;
			return Expectation.Boundary;
		}

		// Whether what one side drew near an isolated corner agrees with the oracle: null when it does,
		// otherwise what is wrong.
		public static string JudgeSide(Expectation expectation, bool drewNearCorner)
		{
			if (expectation == Expectation.NoFace && drewNearCorner)
				return "draws where the oracle draws nothing";
			if (expectation == Expectation.Boundary && !drewNearCorner)
				return "has no vertex where the oracle's outline has a corner";
			return null;
		}

		public static List<Corner> SharedCorners(IReadOnlyList<float4[]> brushPlanes)
		{
			var corners = new List<Corner>();
			for (int a = 0; a < brushPlanes.Count; a++)
			{
				for (int b = a + 1; b < brushPlanes.Count; b++)
				{
					if (!BoundsNear(brushPlanes[a], brushPlanes[b]))
						continue;

					var found = new List<float3>();
					AddCrossings(brushPlanes, a, b, edgeBrush: a, faceBrush: b, found, corners);
					AddCrossings(brushPlanes, a, b, edgeBrush: b, faceBrush: a, found, corners);
				}
			}
			return corners;
		}

		static void AddCrossings(IReadOnlyList<float4[]> brushPlanes, int a, int b, int edgeBrush, int faceBrush,
								 List<float3> found, List<Corner> corners)
		{
			var edgePlanes = brushPlanes[edgeBrush];
			var facePlanes = brushPlanes[faceBrush];
			for (int i = 0; i < edgePlanes.Length; i++)
			{
				for (int j = i + 1; j < edgePlanes.Length; j++)
				{
					for (int k = 0; k < facePlanes.Length; k++)
					{
						if (!TryIntersect(edgePlanes[i], edgePlanes[j], facePlanes[k], out var point))
							continue;
						if (!OnBoundary(edgePlanes, point) || !OnBoundary(facePlanes, point))
							continue;
						// Strictly inside the edge, and strictly inside the face
						var throughEdgeBrush = PlanesThrough(edgePlanes, point);
						var throughFaceBrush = PlanesThrough(facePlanes, point);
						if (throughEdgeBrush.Length != 2 || throughFaceBrush.Length != 1)
							continue;
						var position = (float3)point;
						if (Contains(found, position, kSameCorner))
							continue;
						found.Add(position);
						corners.Add(new Corner
						{
							position = position,
							brushA   = a,
							brushB   = b,
							planesA  = edgeBrush == a ? throughEdgeBrush : throughFaceBrush,
							planesB  = edgeBrush == a ? throughFaceBrush : throughEdgeBrush,
							isolated = IsIsolated(brushPlanes, point)
						});
					}
				}
			}
		}

		// Compares one corner across its two brushes, face against face. verticesByPlane[p] holds the
		// distinct output vertices a brush drew on its plane p.
		public static Result ClassifyCorner(Corner corner, IReadOnlyList<List<float3>> verticesByPlaneA,
													  IReadOnlyList<List<float3>> verticesByPlaneB)
		{
			var facesA = NearFaces(corner.position, corner.planesA, verticesByPlaneA);
			var facesB = NearFaces(corner.position, corner.planesB, verticesByPlaneB);
			var result = new Result { distance = float.NaN, countA = Total(facesA), countB = Total(facesB),
									  drewA = facesA.Count > 0, drewB = facesB.Count > 0 };

			if (facesA.Count == 0 && facesB.Count == 0) { result.verdict = Verdict.Hidden;  return result; }
			if (facesA.Count == 0 || facesB.Count == 0) { result.verdict = Verdict.Missing; return result; }

			result.verdict = Verdict.Same;
			foreach (var faceA in facesA)
			{
				foreach (var faceB in facesB)
				{
					var pair = Classify(corner.position, faceA, faceB);
					if (pair.isDisagreement)
						result.distance = float.IsNaN(result.distance) ? pair.distance : math.max(result.distance, pair.distance);
					if (pair.verdict > result.verdict)
					{
						result.verdict = pair.verdict;
						result.countA  = pair.countA;
						result.countB  = pair.countB;
					}
				}
			}
			return result;
		}

		// Compares the vertices two faces emitted near one corner.
		public static Result Classify(float3 corner, IReadOnlyList<float3> verticesA, IReadOnlyList<float3> verticesB)
		{
			var nearA = Near(corner, verticesA);
			var nearB = Near(corner, verticesB);
			var result = new Result { countA = nearA.Count, countB = nearB.Count, distance = float.NaN };

			if (nearA.Count == 0 && nearB.Count == 0) { result.verdict = Verdict.Hidden;  return result; }
			if (nearA.Count == 0 || nearB.Count == 0) { result.verdict = Verdict.Missing; return result; }

			result.distance = math.distance(Nearest(corner, nearA), Nearest(corner, nearB));
			if (result.distance <= kSamePosition)
				result.verdict = Verdict.Same;
			else if (nearA.Count == 1 && nearB.Count == 1)
				result.verdict = Verdict.Apart;
			else if (nearA.Count + nearB.Count == 3 && math.min(nearA.Count, nearB.Count) == 1)
				result.verdict = Verdict.OneVsTwo;
			else
				result.verdict = Verdict.Other;
			return result;
		}

		public static int PlaneOf(float3 normal, float3 center, float4[] planes)
		{
			var best = -1;
			var bestDistance = float.MaxValue;
			for (int p = 0; p < planes.Length; p++)
			{
				if (math.abs(math.abs(math.dot(normal, planes[p].xyz)) - 1) > 0.001f)
					continue;
				var distance = math.abs(math.dot(planes[p].xyz, center) + planes[p].w);
				if (distance < bestDistance)
				{
					best = p;
					bestDistance = distance;
				}
			}
			return bestDistance <= 2 * kWeldRadius ? best : -1;
		}

		// The distinct positions in a list of vertices; the render buffer repeats a position for every
		// surface that uses it.
		public static List<float3> Distinct(IEnumerable<float3> positions)
		{
			var result = new List<float3>();
			foreach (var position in positions)
			{
				if (!Contains(result, position, kSameVertex))
					result.Add(position);
			}
			return result;
		}

		static List<List<float3>> NearFaces(float3 corner, int[] planes, IReadOnlyList<List<float3>> verticesByPlane)
		{
			var faces = new List<List<float3>>();
			foreach (var plane in planes)
			{
				var near = Near(corner, verticesByPlane[plane]);
				if (near.Count > 0)
					faces.Add(near);
			}
			return faces;
		}

		static int Total(List<List<float3>> faces)
		{
			var all = new List<float3>();
			foreach (var face in faces)
				all.AddRange(face);
			return Distinct(all).Count;
		}

		static List<float3> Near(float3 corner, IReadOnlyList<float3> vertices)
		{
			var result = new List<float3>();
			for (int i = 0; i < vertices.Count; i++)
			{
				if (math.distance(corner, vertices[i]) <= kWeldRadius)
					result.Add(vertices[i]);
			}
			return result;
		}

		static float3 Nearest(float3 corner, List<float3> vertices)
		{
			var best = vertices[0];
			for (int i = 1; i < vertices.Count; i++)
			{
				if (math.distancesq(corner, vertices[i]) < math.distancesq(corner, best))
					best = vertices[i];
			}
			return best;
		}

		static bool Contains(List<float3> positions, float3 position, float distance)
		{
			for (int i = 0; i < positions.Count; i++)
			{
				if (math.distance(positions[i], position) <= distance)
					return true;
			}
			return false;
		}

		static bool TryIntersect(float4 p0, float4 p1, float4 p2, out double3 point)
		{
			double3 n0 = p0.xyz, n1 = p1.xyz, n2 = p2.xyz;
			var determinant = math.dot(n0, math.cross(n1, n2));
			if (math.abs(determinant) < kMinDeterminant)
			{
				point = default;
				return false;
			}
			// Cramer's rule for dot(n, x) = -w on all three planes
			point = (-(double)p0.w * math.cross(n1, n2)
					 -(double)p1.w * math.cross(n2, n0)
					 -(double)p2.w * math.cross(n0, n1)) / determinant;
			return true;
		}

		static double Distance(float4 plane, double3 point) => math.dot((double3)plane.xyz, point) + plane.w;

		// Inside or on every plane, and on at least one
		static bool OnBoundary(float4[] planes, double3 point)
		{
			var onOne = false;
			for (int i = 0; i < planes.Length; i++)
			{
				var distance = Distance(planes[i], point);
				if (distance > kOnBoundary)
					return false;
				if (distance >= -kOnBoundary)
					onOne = true;
			}
			return onOne;
		}

		static int[] PlanesThrough(float4[] planes, double3 point)
		{
			var result = new List<int>();
			for (int i = 0; i < planes.Length; i++)
			{
				if (math.abs(Distance(planes[i], point)) <= kThroughCorner)
					result.Add(i);
			}
			return result.ToArray();
		}

		static bool IsIsolated(IReadOnlyList<float4[]> brushPlanes, double3 point)
		{
			foreach (var planes in brushPlanes)
			{
				// A brush whose planes all keep the corner further out than the weld radius can't
				// reach it: the largest plane distance is a lower bound on the distance to the brush.
				var furthest = double.MinValue;
				for (int i = 0; i < planes.Length; i++)
					furthest = math.max(furthest, Distance(planes[i], point));
				if (furthest > kWeldRadius)
					continue;

				for (int i = 0; i < planes.Length; i++)
				{
					var distance = math.abs(Distance(planes[i], point));
					if (distance > kThroughCorner && distance <= kWeldRadius)
						return false;
				}
			}
			return true;
		}

		static bool BoundsNear(float4[] planesA, float4[] planesB)
		{
			var boundsA = BoundsOf(planesA);
			var boundsB = BoundsOf(planesB);
			return math.all(boundsA.min - kWeldRadius <= boundsB.max) &&
				   math.all(boundsB.min - kWeldRadius <= boundsA.max);
		}

		// The bounds of a brush's vertices, found the same way as the corners
		static (float3 min, float3 max) BoundsOf(float4[] planes)
		{
			var min = new float3(float.MaxValue);
			var max = new float3(float.MinValue);
			for (int i = 0; i < planes.Length; i++)
			{
				for (int j = i + 1; j < planes.Length; j++)
				{
					for (int k = j + 1; k < planes.Length; k++)
					{
						if (!TryIntersect(planes[i], planes[j], planes[k], out var point) || !OnBoundary(planes, point))
							continue;
						min = math.min(min, (float3)point);
						max = math.max(max, (float3)point);
					}
				}
			}
			return (min, max);
		}
	}
}
