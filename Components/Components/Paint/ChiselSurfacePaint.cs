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

        public Vector4 this[int i, int j]
        {
            get => values[j * resolution.x + i];
            set => values[j * resolution.x + i] = value;
        }
    }
}
