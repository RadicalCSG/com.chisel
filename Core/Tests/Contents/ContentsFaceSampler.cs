using System.Collections.Generic;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	public static class ContentsFaceSampler
	{
		// The face of plane planeIndex: a large quad on that plane, clipped by every other plane of
		// the same convex brush. Empty when the plane contributes no face.
		public static List<float3> FacePolygon(float4[] treePlanes, int planeIndex, float extent = 64f)
		{
			var plane  = treePlanes[planeIndex];
			var normal = plane.xyz;
			var origin = -normal * plane.w;
			Basis(normal, out var u, out var v);

			var polygon = new List<float3>
			{
				origin + ((-u - v) * extent),
				origin + (( u - v) * extent),
				origin + (( u + v) * extent),
				origin + ((-u + v) * extent)
			};

			for (int p = 0; p < treePlanes.Length; p++)
			{
				if (p == planeIndex)
					continue;
				polygon = ClipByPlane(polygon, treePlanes[p]);
				if (polygon.Count < 3)
					return new List<float3>();
			}
			return polygon;
		}

		// Sample points on the face, kept inset from its edges: the edges are where welding and
		// neighbouring geometry decide what happens, and this suite is asking about contents.
		public static List<float3> GridSamples(List<float3> polygon, float3 normal, float inset, float spacing)
		{
			var samples = new List<float3>();
			if (polygon.Count < 3)
				return samples;

			var centroid = float3.zero;
			foreach (var point in polygon)
				centroid += point;
			centroid /= polygon.Count;

			var shrunk = InsetConvex(polygon, normal, centroid, inset);
			for (int halving = 0; shrunk.Count < 3 && halving < 4; halving++)
			{
				inset *= 0.5f;
				shrunk = InsetConvex(polygon, normal, centroid, inset);
			}
			if (shrunk.Count < 3)
				return samples;

			Basis(normal, out var u, out var v);
			var min = new float2(float.MaxValue);
			var max = new float2(float.MinValue);
			var flat = new List<float2>(shrunk.Count);
			foreach (var point in shrunk)
			{
				var local = new float2(math.dot(point - centroid, u), math.dot(point - centroid, v));
				flat.Add(local);
				min = math.min(min, local);
				max = math.max(max, local);
			}

			// Always include the centre, so even a face smaller than the grid spacing is asked about.
			if (InsideConvex2D(flat, float2.zero))
				samples.Add(centroid);

			for (var x = min.x; x <= max.x; x += spacing)
			{
				for (var y = min.y; y <= max.y; y += spacing)
				{
					var local = new float2(x, y);
					if (math.all(math.abs(local) < 1e-6f))
						continue;
					if (!InsideConvex2D(flat, local))
						continue;
					samples.Add(centroid + (u * local.x) + (v * local.y));
				}
			}
			return samples;
		}

		public static List<float3> CellSamples(List<float3> polygon, float4 facePlane, IReadOnlyList<float4[]> otherBrushPlanes,
											   float minimumClearance, int maximumCells = 4096)
		{
			var samples = new List<float3>();
			if (polygon.Count < 3)
				return samples;
			var cells = new List<List<float3>> { polygon };
			for (int b = 0; b < otherBrushPlanes.Count && cells.Count < maximumCells; b++)
			{
				var planes = otherBrushPlanes[b];
				for (int p = 0; p < planes.Length; p++)
				{
					var plane = planes[p];
					if (math.abs(math.dot(plane.xyz, facePlane.xyz)) > 0.9999f)
						continue;
					for (int c = cells.Count - 1; c >= 0; c--)
					{
						var cell = cells[c];
						bool below = false, above = false;
						foreach (var point in cell)
						{
							var distance = math.dot(plane.xyz, point) + plane.w;
							if (distance < -1e-4f) below = true;
							if (distance >  1e-4f) above = true;
						}
						if (!below || !above)
							continue;
						var inside  = ClipByPlane(cell, plane);
						var outside = ClipByPlane(cell, -plane);
						if (inside.Count < 3 || outside.Count < 3)
							continue;
						cells[c] = inside;
						cells.Add(outside);
					}
				}
			}

			foreach (var cell in cells)
			{
				var centroid = float3.zero;
				foreach (var point in cell)
					centroid += point;
				centroid /= cell.Count;
				if (Clearance(cell, facePlane.xyz, centroid) >= minimumClearance)
					samples.Add(centroid);
			}
			return samples;
		}

		public static List<float3> FaceSamples(float4[] planes, int planeIndex, IReadOnlyList<float4[]> otherBrushPlanes,
											   IReadOnlyList<(float3 min, float3 max)> otherBounds, float extent, float inset,
											   float spacing, float clearance)
		{
			var polygon = FacePolygon(planes, planeIndex, extent);
			var samples = GridSamples(polygon, planes[planeIndex].xyz, inset, spacing);
			if (polygon.Count < 3)
				return samples;
			var min = new float3(float.PositiveInfinity);
			var max = new float3(float.NegativeInfinity);
			foreach (var point in polygon) { min = math.min(min, point); max = math.max(max, point); }
			var reaching = new List<float4[]>();
			for (int o = 0; o < otherBrushPlanes.Count; o++)
			{
				var bounds = otherBounds[o];
				if (math.all(bounds.min <= max + 0.01f) && math.all(bounds.max >= min - 0.01f))
					reaching.Add(otherBrushPlanes[o]);
			}
			samples.AddRange(CellSamples(polygon, planes[planeIndex], reaching, clearance));
			return samples;
		}

		// The box around a convex brush given by its planes, from its faces as FacePolygon clips them.
		public static (float3 min, float3 max) BrushBounds(float4[] planes, float extent)
		{
			var min = new float3(float.PositiveInfinity);
			var max = new float3(float.NegativeInfinity);
			for (int p = 0; p < planes.Length; p++)
				foreach (var point in FacePolygon(planes, p, extent)) { min = math.min(min, point); max = math.max(max, point); }
			return (min, max);
		}

		// Distance from a point inside a convex polygon to its nearest edge.
		static float Clearance(List<float3> polygon, float3 normal, float3 point)
		{
			var nearest = float.MaxValue;
			for (int i = 0; i < polygon.Count; i++)
			{
				var a    = polygon[i];
				var edge = polygon[(i + 1) % polygon.Count] - a;
				var length = math.length(edge);
				if (length < 1e-6f)
					continue;
				var inward = math.cross(normal, edge / length);
				nearest = math.min(nearest, math.abs(math.dot(inward, point - a)));
			}
			return nearest;
		}

		public static bool CoversPoint(float3 a, float3 b, float3 c, float4 plane, float3 point,
									   float onPlaneEpsilon, float insideEpsilon)
		{
			if (math.abs(math.dot(plane.xyz, a) + plane.w) > onPlaneEpsilon) return false;
			if (math.abs(math.dot(plane.xyz, b) + plane.w) > onPlaneEpsilon) return false;
			if (math.abs(math.dot(plane.xyz, c) + plane.w) > onPlaneEpsilon) return false;

			// Same-side test around the three edges, in the plane.
			var normal = plane.xyz;
			float l0 = math.length(b - a), l1 = math.length(c - b), l2 = math.length(a - c);
			if (!(l0 > 0) || !(l1 > 0) || !(l2 > 0))
				return false;
			var d0 = math.dot(math.cross(b - a, point - a), normal) / l0;
			var d1 = math.dot(math.cross(c - b, point - b), normal) / l1;
			var d2 = math.dot(math.cross(a - c, point - c), normal) / l2;
			return (d0 >= -insideEpsilon && d1 >= -insideEpsilon && d2 >= -insideEpsilon) ||
				   (d0 <=  insideEpsilon && d1 <=  insideEpsilon && d2 <=  insideEpsilon);
		}

		public static bool NearACrossingPlane(IReadOnlyList<float4[]> otherBrushPlanes, float4 facePlane,
											  float3 point, float band)
		{
			for (int b = 0; b < otherBrushPlanes.Count; b++)
			{
				var planes = otherBrushPlanes[b];
				for (int p = 0; p < planes.Length; p++)
				{
					if (math.abs(math.dot(planes[p].xyz, facePlane.xyz)) > 0.9999f)
						continue;
					if (math.abs(math.dot(planes[p].xyz, point) + planes[p].w) < band)
						return true;
				}
			}
			return false;
		}

		// The convex polygon with every edge moved inwards by `inset`: the intersection of the half-planes that lie at
		// least that far inside each edge. Empty when the face is too thin for it.
		static List<float3> InsetConvex(List<float3> polygon, float3 normal, float3 inside, float inset)
		{
			var result = new List<float3>(polygon);
			for (int i = 0; i < polygon.Count && result.Count >= 3; i++)
			{
				var a    = polygon[i];
				var b    = polygon[(i + 1) % polygon.Count];
				var edge = b - a;
				if (math.lengthsq(edge) < 1e-12f)
					continue;
				var inward = math.normalize(math.cross(normal, edge));
				if (math.dot(inward, inside - a) < 0)
					inward = -inward;
				// keep what is at least `inset` inside this edge: -dot(inward, x) + dot(inward, a) + inset <= 0
				result = ClipByPlane(result, new float4(-inward, math.dot(inward, a) + inset));
			}
			return result.Count >= 3 ? result : new List<float3>();
		}

		static bool InsideConvex2D(List<float2> polygon, float2 point)
		{
			var positive = false;
			var negative = false;
			for (int i = 0; i < polygon.Count; i++)
			{
				var a    = polygon[i];
				var b    = polygon[(i + 1) % polygon.Count];
				var side = ((b.x - a.x) * (point.y - a.y)) - ((b.y - a.y) * (point.x - a.x));
				if (side > 1e-6f)  positive = true;
				if (side < -1e-6f) negative = true;
				if (positive && negative)
					return false;
			}
			return true;
		}

		static List<float3> ClipByPlane(List<float3> polygon, float4 plane)
		{
			var result = new List<float3>(polygon.Count + 1);
			for (int i = 0; i < polygon.Count; i++)
			{
				var current = polygon[i];
				var next    = polygon[(i + 1) % polygon.Count];
				var dCurrent = math.dot(plane.xyz, current) + plane.w;
				var dNext    = math.dot(plane.xyz, next) + plane.w;

				if (dCurrent <= 0)
					result.Add(current);
				if ((dCurrent < 0 && dNext > 0) || (dCurrent > 0 && dNext < 0))
					result.Add(math.lerp(current, next, dCurrent / (dCurrent - dNext)));
			}
			return result;
		}

		static void Basis(float3 normal, out float3 u, out float3 v)
		{
			var reference = math.abs(normal.y) < 0.9f ? new float3(0, 1, 0) : new float3(1, 0, 0);
			u = math.normalize(math.cross(reference, normal));
			v = math.cross(normal, u);
		}
	}
}
