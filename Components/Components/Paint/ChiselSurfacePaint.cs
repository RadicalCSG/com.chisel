using System;
using Chisel.Core;
using UnityEngine;

namespace Chisel.Components
{
    /// <summary>
    /// The paint of one surface of a generator in one vertex attribute: a grid of values in the surface's plane, in the
    /// generator's own space. Grid point (i, j) is at <c>origin + i * cellU + j * cellV</c>; <see cref="values"/> holds them
    /// row by row along cellU. The cells are also how finely the surface is cut to show the paint.
    /// </summary>
    [Serializable]
    public sealed class ChiselSurfacePaint
    {
        /// <summary>The surface, as the generator's surface array numbers it</summary>
        public int                      surfaceIndex;
        public ChiselPaintAttribute     attribute;
        public ChiselPaintComponents    components;
        public Vector4                  defaultValue;
        public Vector3                  origin;
        public Vector3                  cellU;
        public Vector3                  cellV;
        public Vector2Int               resolution;
        /// <summary>The surface's paint spacing the grid was made for (its cells are larger when that would have been
        /// too many points); 0 when not known</summary>
        public float                    spacing;
        public Vector4[]                values = Array.Empty<Vector4>();

        /// <summary>A grid of resolution points from origin along cellU and cellV, every point at the default</summary>
        public static ChiselSurfacePaint Create(int surfaceIndex, ChiselPaintAttribute attribute, ChiselPaintComponents components,
                                                Vector4 defaultValue, Vector3 origin, Vector3 cellU, Vector3 cellV, Vector2Int resolution)
        {
            resolution = Vector2Int.Max(resolution, Vector2Int.one);
            var values = new Vector4[resolution.x * resolution.y];
            for (int i = 0; i < values.Length; i++)
                values[i] = defaultValue;
            return new ChiselSurfacePaint
            {
                surfaceIndex = surfaceIndex,
                attribute    = attribute,
                components   = components,
                defaultValue = defaultValue,
                origin       = origin,
                cellU        = cellU,
                cellV        = cellV,
                resolution   = resolution,
                values       = values
            };
        }

        public bool IsValid => resolution.x > 0 && resolution.y > 0 && values != null && values.Length == resolution.x * resolution.y;

        /// <summary>The paint at a point (in the generator's space), between the four grid points round it; the grid is
        /// projected along its plane's normal, and a point beyond it takes the nearest edge's paint</summary>
        public Vector4 Sample(Vector3 point)
        {
            if (!IsValid)
                return defaultValue;
            var offset = point - origin;
            var uu = Vector3.Dot(cellU, cellU);
            var uv = Vector3.Dot(cellU, cellV);
            var vv = Vector3.Dot(cellV, cellV);
            var determinant = uu * vv - uv * uv;
            if (!(Mathf.Abs(determinant) > 1e-12f))
                return defaultValue;
            var du = Vector3.Dot(offset, cellU);
            var dv = Vector3.Dot(offset, cellV);
            var x = Mathf.Clamp((vv * du - uv * dv) / determinant, 0, resolution.x - 1);
            var y = Mathf.Clamp((uu * dv - uv * du) / determinant, 0, resolution.y - 1);
            var i = Mathf.Min((int)x, Mathf.Max(resolution.x - 2, 0));
            var j = Mathf.Min((int)y, Mathf.Max(resolution.y - 2, 0));
            var i1 = Mathf.Min(i + 1, resolution.x - 1);
            var j1 = Mathf.Min(j + 1, resolution.y - 1);
            var bottom = Vector4.Lerp(this[i, j],  this[i1, j],  x - i);
            var top    = Vector4.Lerp(this[i, j1], this[i1, j1], x - i);
            return Vector4.Lerp(bottom, top, y - j);
        }

        public Vector4 this[int i, int j]
        {
            get => values[j * resolution.x + i];
            set => values[j * resolution.x + i] = value;
        }
    }
}
