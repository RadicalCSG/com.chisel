using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    /// <summary>The vertex attribute paint is written to. What it means is up to the shader of the surface's material.
    /// TexCoord0 and TexCoord1 are taken (texture mapping, lightmaps).</summary>
    public enum ChiselPaintAttribute : byte
    {
        Color     = 0,
        TexCoord2 = 2,
        TexCoord3 = 3,
        TexCoord4 = 4,
        TexCoord5 = 5,
        TexCoord6 = 6,
        TexCoord7 = 7
    }

    /// <summary>The components of a vertex attribute paint writes; the others keep their default</summary>
    [Flags]
    public enum ChiselPaintComponents : byte
    {
        None = 0,
        X    = 1,
        Y    = 2,
        Z    = 4,
        W    = 8,
        XYZ  = X | Y | Z,
        XYZW = X | Y | Z | W
    }

    /// <summary>
    /// The paint of one surface in one vertex attribute: values on a grid in the surface's plane, in the tree's (model's)
    /// space. Grid point (i, j) is at <c>origin + i * cellU + j * cellV</c>; a vertex takes the value at its place on the
    /// grid, interpolated between the four points round it, in <see cref="components"/>, and
    /// <see cref="defaultValue"/> in the others. The grid's cells are also how finely the surface is cut to show it.
    /// </summary>
    public struct ChiselPaintLayer : IEquatable<ChiselPaintLayer>
    {
        /// <summary>The node the surface's brush was created with, as for <see cref="ChiselDecalTarget.brushEntityID"/></summary>
        public ulong                    entityID;
        /// <summary>The surface, as the brush's surface array numbers it</summary>
        public int                      surfaceIndex;
        public ChiselPaintAttribute     attribute;
        public ChiselPaintComponents    components;
        public float4                   defaultValue;
        public float3                   origin;
        public float3                   cellU;
        public float3                   cellV;
        /// <summary>How many grid points there are along cellU and cellV</summary>
        public int2                     resolution;
        /// <summary>Where the grid's values start in the values given with the layers: row by row along cellU, each point
        /// as only its components (x, y, z, w in that order)</summary>
        public int                      valueStart;

        /// <summary>How many values each grid point has: one per component</summary>
        public readonly int Stride     => math.countbits((int)components & 15);
        public readonly int PointCount => math.max(resolution.x, 0) * math.max(resolution.y, 0);
        public readonly int ValueCount => PointCount * Stride;

        public readonly bool Equals(ChiselPaintLayer other)
        {
            return entityID == other.entityID && surfaceIndex == other.surfaceIndex &&
                   attribute == other.attribute && components == other.components &&
                   math.all(defaultValue == other.defaultValue) && math.all(origin == other.origin) &&
                   math.all(cellU == other.cellU) && math.all(cellV == other.cellV) &&
                   math.all(resolution == other.resolution) && valueStart == other.valueStart;
        }

        public override readonly bool Equals(object obj) => obj is ChiselPaintLayer other && Equals(other);
        public override readonly int GetHashCode() => (int)math.hash(new uint4((uint)entityID, (uint)surfaceIndex, (uint)attribute, math.hash(origin)));

        /// <summary>The layer's value at a point, in all four components (its grid projected along the plane's normal)</summary>
        public readonly unsafe float4 Sample(NativeArray<float> values, float3 point)
        {
            return Sample((float*)Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(values), point);
        }

        internal readonly unsafe float4 Sample(float* values, float3 point)
        {
            if (!GridPosition(point, out var cell, out var t))
                return defaultValue;
            var row0 = cell.y * resolution.x + cell.x;
            var row1 = row0 + (resolution.y > 1 ? resolution.x : 0);
            var next = resolution.x > 1 ? 1 : 0;
            var bottom = math.lerp(Load(values, row0), Load(values, row0 + next), t.x);
            var top    = math.lerp(Load(values, row1), Load(values, row1 + next), t.x);
            return math.lerp(bottom, top, t.y);
        }

        // A grid point's value, the default in the components the layer doesn't have
        readonly unsafe float4 Load(float* values, int point)
        {
            var value = defaultValue;
            var at = values + valueStart + point * Stride;
            if ((components & ChiselPaintComponents.X) != 0) value.x = *at++;
            if ((components & ChiselPaintComponents.Y) != 0) value.y = *at++;
            if ((components & ChiselPaintComponents.Z) != 0) value.z = *at++;
            if ((components & ChiselPaintComponents.W) != 0) value.w = *at;
            return value;
        }

        // Where a point is on the grid: the cell (its lower grid point) and how far across it, clamped to the grid
        readonly bool GridPosition(float3 point, out int2 cell, out float2 t)
        {
            cell = int2.zero;
            t    = float2.zero;
            if (math.any(resolution < 1))
                return false;
            // the point's coordinates along cellU and cellV, which needn't be at right angles
            var offset = point - origin;
            var uu = math.dot(cellU, cellU);
            var uv = math.dot(cellU, cellV);
            var vv = math.dot(cellV, cellV);
            var determinant = uu * vv - uv * uv;
            if (!(math.abs(determinant) > 1e-12f))
                return false;
            var du = math.dot(offset, cellU);
            var dv = math.dot(offset, cellV);
            var grid = new float2((vv * du - uv * dv) / determinant, (uu * dv - uv * du) / determinant);
            grid = math.clamp(grid, float2.zero, (float2)(resolution - 1));
            cell = math.min((int2)math.floor(grid), math.max(resolution - 2, 0));
            t    = grid - cell;
            return true;
        }

        /// <summary>A value written in the layer's components over a default</summary>
        public static float4 Apply(float4 value, float4 over, ChiselPaintComponents components)
        {
            return math.select(over, value, new bool4((components & ChiselPaintComponents.X) != 0,
                                                      (components & ChiselPaintComponents.Y) != 0,
                                                      (components & ChiselPaintComponents.Z) != 0,
                                                      (components & ChiselPaintComponents.W) != 0));
        }
    }

    public static class CSGTreePaintExtensions
    {
        /// <summary>
        /// Replaces all paint of the tree at once: the layers, and the values their grids take from (each layer's
        /// <see cref="ChiselPaintLayer.valueStart"/>). Returns the entities whose paint changed; their brushes have to be
        /// rebuilt to show it (the caller marks them dirty).
        /// </summary>
        public static bool SetPaint(this CSGTree tree, NativeArray<ChiselPaintLayer> layers, NativeArray<float> values, NativeList<ulong> changedEntities)
        {
            return ChiselPaintStore.SetPaint(tree, layers, values, changedEntities);
        }

        /// <summary>
        /// Replaces the paint of one entity: its layers, and the values their grids take from. True when it changed; the
        /// entity's brushes then have to be rebuilt to show it (<see cref="CSGTreeNode.SetSurfacesDirty"/>).
        /// </summary>
        public static bool SetPaint(this CSGTree tree, ulong entityID, NativeArray<ChiselPaintLayer> layers, NativeArray<float> values)
        {
            return ChiselPaintStore.SetPaint(tree, entityID, layers, values);
        }
    }
}
