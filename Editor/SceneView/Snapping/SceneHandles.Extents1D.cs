using System;
using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    [Serializable]
    public struct Extents1D
    {
        public Extents1D(float value)
        {
            min = value;
            max = value;
        }
        public Extents1D(float min, float max)
        {
            this.min = min;
            this.max = max;
        }
        public float min;
        public float max;
        
        readonly static Extents1D empty = new(0, 0);
        public static Extents1D Empty => empty;

		public readonly float Size	 { get { return max - min; } }
        public readonly float Center { get { return (max + min) * 0.5f; } }

        public static Extents1D operator +(Extents1D extents, float offset) { return new Extents1D(extents.min + offset, extents.max + offset); }
        public static Extents1D operator -(Extents1D extents, float offset) { return new Extents1D(extents.min - offset, extents.max - offset); }
        
        public static Extents1D operator +(Extents1D extents, Extents1D other) { return new Extents1D(extents.min + other.min, extents.max + other.max); }
        public static Extents1D operator -(Extents1D extents, Extents1D other) { return new Extents1D(extents.min - other.min, extents.max - other.max); }
        

        public static Extents1D GetExtentsOfPointArray(Vector3[] points, Vector3 direction, Vector3 origin)
        {
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            for (var i = 0; i < points.Length; i++)
            {
                var distance = SnappingUtility.WorldPointToDistance(points[i], direction, origin);
                min = Mathf.Min(min, distance);
                max = Mathf.Max(max, distance);
            }
            return new Extents1D(min, max);
        }

        public static Extents1D GetExtentsOfPointArray(Vector3[] points, Vector3 direction) { return GetExtentsOfPointArray(points, direction, Vector3.zero); }


        public override readonly string ToString() { return $"(Min: {min}, Max: {max})"; }
    }
}
