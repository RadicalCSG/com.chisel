using System.Runtime.CompilerServices;

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using UnityEngine;
using UnityEngine.Scripting;

namespace Chisel
{
    public static class BoundsExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(float3 min, float3 max)
        {
            const float kMinSize = 0.0001f;
            if (math.abs(max.x - min.x) < kMinSize ||
                math.abs(max.y - min.y) < kMinSize ||
                math.abs(max.z - min.z) < kMinSize ||
                !math.isfinite(min.x) || !math.isfinite(min.y) || !math.isfinite(min.z) ||
                !math.isfinite(max.x) || !math.isfinite(max.y) || !math.isfinite(max.z) ||
                math.isnan(min.x) || math.isnan(min.y) || math.isnan(min.z) ||
                math.isnan(max.x) || math.isnan(max.y) || math.isnan(max.z))
                return false;
            return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Intersects(this AABB left, AABB right, double epsilon)
		{
			return ((right.Max.x - left.Min.x) >= -epsilon) && ((left.Max.x - right.Min.x) >= -epsilon) &&
				   ((right.Max.y - left.Min.y) >= -epsilon) && ((left.Max.y - right.Min.y) >= -epsilon) &&
				   ((right.Max.z - left.Min.z) >= -epsilon) && ((left.Max.z - right.Min.z) >= -epsilon);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Intersects(this AABB left, MinMaxAABB right, double epsilon)
		{
			return ((right.Max.x - left.Min.x) >= -epsilon) && ((left.Max.x - right.Min.x) >= -epsilon) &&
				   ((right.Max.y - left.Min.y) >= -epsilon) && ((left.Max.y - right.Min.y) >= -epsilon) &&
				   ((right.Max.z - left.Min.z) >= -epsilon) && ((left.Max.z - right.Min.z) >= -epsilon);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Intersects(this MinMaxAABB left, AABB right, double epsilon)
		{
			return ((right.Max.x - left.Min.x) >= -epsilon) && ((left.Max.x - right.Min.x) >= -epsilon) &&
				   ((right.Max.y - left.Min.y) >= -epsilon) && ((left.Max.y - right.Min.y) >= -epsilon) &&
				   ((right.Max.z - left.Min.z) >= -epsilon) && ((left.Max.z - right.Min.z) >= -epsilon);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Intersects(this MinMaxAABB left, MinMaxAABB right, double epsilon)
        {
            return ((right.Max.x - left.Min.x) >= -epsilon) && ((left.Max.x - right.Min.x) >= -epsilon) &&
                   ((right.Max.y - left.Min.y) >= -epsilon) && ((left.Max.y - right.Min.y) >= -epsilon) &&
                   ((right.Max.z - left.Min.z) >= -epsilon) && ((left.Max.z - right.Min.z) >= -epsilon);
        }

        public static MinMaxAABB Create(float4x4 transformation, float3[] vertices)
        {
            if (vertices == null ||
                vertices.Length == 0)
                return default;

            var min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < vertices.Length; i++)
            {
                var vert = math.mul(transformation, new float4(vertices[i], 1)).xyz;
                min = math.min(min, vert);
                max = math.max(max, vert);
			}
			return new MinMaxAABB { Min = min, Max = max };
		}

        public static MinMaxAABB Create(ref BlobArray<float3> vertices, float4x4 transformation)
        {
            if (vertices.Length == 0)
                return default;

            var min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < vertices.Length; i++)
            {
                var vert = math.mul(transformation, new float4(vertices[i], 1)).xyz;
                min = math.min(min, vert);
                max = math.max(max, vert);
            }
            return new MinMaxAABB { Min = min, Max = max };
        }


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsEmpty(this MinMaxAABB self) { return self.Equals(Empty); }
		readonly static MinMaxAABB Empty = new() { Min = math.float3(float.NegativeInfinity), Max = math.float3(float.PositiveInfinity) };

	
		// These setters require a reference because Bounds is a value type.

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Encapsulate(ref this AABB self, AABB other)
		{
			var min = math.min(self.Min, other.Min);
			var max = math.max(self.Max, other.Max);
			self.Center = (min + max) * 0.5f;
			self.Extents = (max - min) * 0.5f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Encapsulate(ref this AABB self, float3 point)
		{
			var min = math.min(self.Min, point);
			var max = math.max(self.Max, point);
			self.Center = (min + max) * 0.5f;
			self.Extents = (max - min) * 0.5f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MinMaxAABB CreateAABB(float3 min, float3 max) { return new AABB { Center = (max + min) * 0.5f, Extents = (max - min) * 0.5f }; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MinMaxAABB ToMinMaxAABB(this AABB bounds) { return new MinMaxAABB { Min = bounds.Min, Max = bounds.Max }; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MinMaxAABB ToMinMaxAABB(this Bounds bounds) { return new MinMaxAABB { Min = bounds.min, Max = bounds.max }; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)] 
		public static Bounds ToBounds(this AABB aabb) { return new Bounds(aabb.GetCenter(), aabb.GetSize()); }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Bounds ToBounds(this MinMaxAABB aabb) { return new Bounds { center = (aabb.Max + aabb.Min) * 0.5f, extents = (aabb.Max - aabb.Min) * 0.5f }; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AABB ToAABB(this MinMaxAABB aabb) { return new AABB { Center = (aabb.Max + aabb.Min) * 0.5f, Extents = (aabb.Max - aabb.Min) * 0.5f }; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 GetCenter(this MinMaxAABB aabb) { return (aabb.Max + aabb.Min) * 0.5f; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 GetCenter(this AABB aabb) { return aabb.Center; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 GetCenter(this Bounds aabb) { return aabb.center; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 GetSize(this MinMaxAABB aabb) { return aabb.Max - aabb.Min; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 GetSize(this AABB aabb) { return aabb.Size; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 GetSize(this Bounds aabb) { return aabb.size; }

		// Moves the bounds, keeping their size
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCenter(ref this MinMaxAABB aabb, float3 center)
		{
			var extents = math.abs(aabb.Max - aabb.Min) * 0.5f;
			aabb.Min = center - extents;
			aabb.Max = center + extents;
		}

		// Resizes the bounds around their center
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetExtents(ref this MinMaxAABB aabb, float3 extents)
		{
			var center = (aabb.Max + aabb.Min) * 0.5f;
			aabb.Min = center - extents;
			aabb.Max = center + extents;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMin(ref this MinMaxAABB aabb, float3 min)
		{
			aabb.Min = min;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMax(ref this MinMaxAABB aabb, float3 max)
		{
			aabb.Max = max;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMinMax(ref this MinMaxAABB aabb, float3 min, float3 max)
		{
			aabb.Min = min;
			aabb.Max = max;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)] public static void SetCenter(ref this AABB aabb, float3 center) { aabb.Center = center; }
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public static void SetExtents(ref this AABB aabb, float3 extents) { aabb.Extents = extents; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMin(ref this AABB aabb, float3 min)
		{
			aabb.SetMinMax(min, aabb.Max);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMax(ref this AABB aabb, float3 max)
		{
			aabb.SetMinMax(aabb.Min, max);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMinMax(ref this AABB aabb, float3 min, float3 max)
		{
			aabb.Center = (max + min) * 0.5f;
			aabb.Extents = math.abs(max - min) * 0.5f;
		}

		// Bounds has SetMinMax of its own
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public static void SetCenter(ref this Bounds aabb, float3 center) { aabb.center = center; }
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public static void SetExtents(ref this Bounds aabb, float3 extents) { aabb.extents = extents; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMin(ref this Bounds aabb, float3 min)
		{
			aabb.SetMinMax(min, aabb.max);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMax(ref this Bounds aabb, float3 max)
		{
			aabb.SetMinMax(aabb.min, max);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MinMaxAABB GetMinMax(this NativeList<float3> vertices)
		{
			var aabb = new MinMaxAABB()
			{
				Min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
				Max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity)
			};
			for (int i = 0; i < vertices.Length; i++)
			{
				aabb.Min = math.min(aabb.Min, vertices[i]);
				aabb.Max = math.max(aabb.Max, vertices[i]);
			}
			return aabb;
		}
    }
}
