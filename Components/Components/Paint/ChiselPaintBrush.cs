using System.Collections.Generic;
using Chisel.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components
{
    /// <summary>What a dab of the paint brush does to the paint under it</summary>
    public enum ChiselPaintOperation : byte
    {
        /// <summary>Towards the brush's color or value</summary>
        Paint    = 0,
        /// <summary>Up by the brush's value (a value mode)</summary>
        Add      = 1,
        /// <summary>Down by the brush's value (a value mode)</summary>
        Subtract = 2,
        /// <summary>Towards the average of the neighbouring grid points</summary>
        Smooth   = 3,
        /// <summary>Back towards the mode's default</summary>
        Erase    = 4
    }

    /// <summary>
    /// The paint brush's work on a generator's surface paint: the grid a surface gets when it is first painted, and what
    /// a dab does to the grid points under it. A dab weighs each point by the brush's strength, fully within its hardness
    /// (a part of its radius) and falling off smoothly to its edge.
    /// </summary>
    public static class ChiselPaintBrush
    {
        /// <summary>Most grid points a surface gets: the cells grow until it fits (PaintSubdivision takes as many)</summary>
        public const int kMaxGridPoints = 4096;

        static readonly List<Vector3> s_Corners = new();

        /// <summary>
        /// The corners of a generator's surface, in the generator's own space, and its normal: of every brush polygon the
        /// generator gives that surface. False when it has none.
        /// </summary>
        public static bool SurfaceOutline(ChiselGeneratorComponent generator, int surfaceIndex, List<Vector3> corners, out Vector3 normal)
        {
            corners.Clear();
            normal = Vector3.zero;
            var model = generator ? generator.hierarchyItem?.Model : null;
            if (!model)
                return false;
            var modelToGenerator = (model.transform.worldToLocalMatrix * generator.transform.localToWorldMatrix).inverse;
            AddOutline(generator.TopTreeNode, surfaceIndex, modelToGenerator, corners, ref normal);
            if (corners.Count < 3 || normal == Vector3.zero)
                return false;
            normal.Normalize();
            return true;
        }

        static void AddOutline(CSGTreeNode node, int surfaceIndex, Matrix4x4 treeToGenerator, List<Vector3> corners, ref Vector3 normal)
        {
            if (!node.Valid)
                return;
            if (node.Type != CSGNodeType.Brush)
            {
                for (int i = 0; i < node.Count; i++)
                    AddOutline(node[i], surfaceIndex, treeToGenerator, corners, ref normal);
                return;
            }
            var brush = (CSGTreeBrush)node;
            var blob  = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
            if (!blob.IsCreated)
                return;
            ref var mesh = ref blob.Value;
            var toGenerator = treeToGenerator * (Matrix4x4)brush.NodeToTreeSpaceMatrix;
            for (int p = 0; p < mesh.polygons.Length; p++)
            {
                if (mesh.polygons[p].descriptionIndex != surfaceIndex)
                    continue;
                if (normal == Vector3.zero && p < mesh.localPlanes.Length)
                    normal = toGenerator.inverse.transpose.MultiplyVector((Vector3)mesh.localPlanes[p].xyz);
                var polygon = mesh.polygons[p];
                for (int e = polygon.firstEdge; e < polygon.firstEdge + polygon.edgeCount; e++)
                    corners.Add(toGenerator.MultiplyPoint3x4(mesh.localVertices[mesh.halfEdges[e].vertexIndex]));
            }
        }

        /// <summary>
        /// A grid over a surface of a generator, in the surface's plane, its cells about spacing (larger when it would
        /// have more than <see cref="kMaxGridPoints"/>), every point at the default. Null when the surface has no outline.
        /// </summary>
        public static ChiselSurfacePaint CreateGrid(ChiselGeneratorComponent generator, int surfaceIndex, ChiselPaintAttribute attribute,
                                                    ChiselPaintComponents components, Vector4 defaultValue, float spacing)
        {
            var cellSize = spacing;
            if (!SurfaceOutline(generator, surfaceIndex, s_Corners, out var normal))
                return null;
            var axisU = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.99f ? Vector3.up : Vector3.forward).normalized;
            var axisV = Vector3.Cross(normal, axisU).normalized;
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in s_Corners)
            {
                var flat = new Vector2(Vector3.Dot(corner, axisU), Vector3.Dot(corner, axisV));
                min = Vector2.Min(min, flat);
                max = Vector2.Max(max, flat);
            }
            var size = max - min;
            cellSize = Mathf.Max(cellSize, 1e-3f);
            // grow the cells until the grid fits
            var resolution = Vector2Int.one;
            for (int tries = 0; tries < 64; tries++)
            {
                resolution = new Vector2Int(Mathf.CeilToInt(size.x / cellSize) + 1, Mathf.CeilToInt(size.y / cellSize) + 1);
                if (resolution.x * resolution.y <= kMaxGridPoints)
                    break;
                cellSize *= 1.25f;
            }
            var planePoint = s_Corners[0];
            var origin = planePoint + (min.x - Vector3.Dot(planePoint, axisU)) * axisU + (min.y - Vector3.Dot(planePoint, axisV)) * axisV;
            var paint = ChiselSurfacePaint.Create(surfaceIndex, attribute, components, defaultValue, origin, axisU * cellSize, axisV * cellSize, resolution);
            paint.spacing = spacing;
            return paint;
        }

        /// <summary>The paint spacing of a generator's surface (<see cref="ChiselSurface.paintSpacing"/>)</summary>
        public static float SpacingOf(ChiselGeneratorComponent generator, int surfaceIndex)
        {
            var surfaces = generator ? generator.SurfaceDefinition?.surfaces : null;
            return surfaces != null && surfaceIndex >= 0 && surfaceIndex < surfaces.Length && surfaces[surfaceIndex] != null
                ? surfaces[surfaceIndex].PaintSpacing : ChiselSurface.kDefaultPaintSpacing;
        }

        /// <summary>
        /// Puts the paint of every surface of a generator whose paint spacing changed on a new grid of that spacing,
        /// resampled from the old one. True when any changed.
        /// </summary>
        public static bool Respace(ChiselGeneratorComponent generator)
        {
            var changed = false;
            var all = generator.SurfacePaint;
            for (int p = 0; p < all.Count; p++)
            {
                var paint = all[p];
                if (paint == null)
                    continue;
                var spacing = SpacingOf(generator, paint.surfaceIndex);
                if (paint.spacing == spacing)
                    continue;
                if (!(paint.spacing > 0))
                {
                    paint.spacing = spacing;
                    changed = true;
                    continue;
                }
                var grid = CreateGrid(generator, paint.surfaceIndex, paint.attribute, paint.components, paint.defaultValue, spacing);
                if (grid == null)
                    continue;
                for (int j = 0; j < grid.resolution.y; j++)
                {
                    for (int i = 0; i < grid.resolution.x; i++)
                        grid[i, j] = paint.Sample(grid.origin + i * grid.cellU + j * grid.cellV);
                }
                // the old grid leaves the list, the new one goes to its end: the next paint is at p now
                generator.SetSurfacePaint(grid);
                p--;
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// The paint a mode writes on a surface: the surface's paint in the mode's attribute, with the mode's components
        /// (at its default where they weren't painted yet), made with a grid of the surface's paint spacing when it has none
        /// </summary>
        public static ChiselSurfacePaint PaintFor(ChiselGeneratorComponent generator, int surfaceIndex, in ChiselPaintMode mode)
        {
            Respace(generator);
            var paint = generator.GetSurfacePaint(surfaceIndex, mode.attribute);
            if (paint == null)
            {
                paint = CreateGrid(generator, surfaceIndex, mode.attribute, mode.components, mode.defaultValue, SpacingOf(generator, surfaceIndex));
                if (paint != null)
                    generator.SetSurfacePaint(paint);
                return paint;
            }
            var added = mode.components & ~paint.components;
            if (added != ChiselPaintComponents.None)
            {
                // another mode of the attribute painted it already: its components stay, the mode's start at its default
                paint.defaultValue = ChiselPaintLayer.Apply(mode.defaultValue, paint.defaultValue, added);
                for (int i = 0; i < paint.values.Length; i++)
                    paint.values[i] = ChiselPaintLayer.Apply(mode.defaultValue, paint.values[i], added);
                paint.components |= added;
                generator.PaintChanged();
            }
            return paint;
        }

        /// <summary>How much a dab counts at a distance from its middle: strength within hardness of the radius, then
        /// falling off smoothly to nothing at the radius</summary>
        public static float Weight(float distance, float radius, float strength, float hardness)
        {
            if (!(radius > 0) || distance >= radius)
                return 0;
            var f = distance / radius;
            hardness = Mathf.Clamp01(hardness);
            if (f <= hardness)
                return strength;
            var t = (f - hardness) / Mathf.Max(1 - hardness, 1e-5f);
            return strength * (1 - t * t * (3 - 2 * t));
        }

        /// <summary>
        /// One dab on the paint, round a point in the generator's space (measured in the surface's plane), in the mode's
        /// components. target is the brush's color or value. False when no grid point was in reach.
        /// </summary>
        public static bool Dab(ChiselSurfacePaint paint, in ChiselPaintMode mode, ChiselPaintOperation operation, Vector4 target,
                               Vector3 center, float radius, float strength, float hardness,
                               ChiselPaintComponents group = ChiselPaintComponents.None)
        {
            if (paint == null || !paint.IsValid)
                return false;
            var normal  = Vector3.Cross(paint.cellU, paint.cellV).normalized;
            var before  = operation == ChiselPaintOperation.Smooth ? (Vector4[])paint.values.Clone() : null;
            var changed = false;
            for (int j = 0; j < paint.resolution.y; j++)
            {
                for (int i = 0; i < paint.resolution.x; i++)
                {
                    var offset = paint.origin + i * paint.cellU + j * paint.cellV - center;
                    offset -= normal * Vector3.Dot(offset, normal);
                    var weight = Weight(offset.magnitude, radius, strength, hardness);
                    if (weight <= 0)
                        continue;
                    var value = paint[i, j];
                    Vector4 result;
                    switch (operation)
                    {
                        case ChiselPaintOperation.Add:      result = value + target * weight; break;
                        case ChiselPaintOperation.Subtract: result = value - target * weight; break;
                        case ChiselPaintOperation.Erase:    result = Vector4.Lerp(value, mode.defaultValue, weight); break;
                        case ChiselPaintOperation.Smooth:   result = Vector4.Lerp(value, Average(before, paint.resolution, i, j), weight); break;
                        default:                            result = Vector4.Lerp(value, target, weight); break;
                    }
                    if (mode.kind == ChiselPaintKind.Value)
                        result = Vector4.Max(Vector4.Min(result, Vector4.one * mode.range.y), Vector4.one * mode.range.x);
                    result = ChiselPaintLayer.Apply(result, value, mode.components);
                    if (group != ChiselPaintComponents.None)
                        result = Normalize(result, mode.components, group);
                    paint[i, j] = result;
                    changed = true;
                }
            }
            return changed;
        }

        /// <summary>The whole surface set to the brush's color or value, in the mode's components</summary>
        public static void Fill(ChiselSurfacePaint paint, in ChiselPaintMode mode, Vector4 target, ChiselPaintComponents group = ChiselPaintComponents.None)
        {
            if (paint == null || !paint.IsValid)
                return;
            for (int i = 0; i < paint.values.Length; i++)
            {
                Vector4 result = ChiselPaintLayer.Apply(target, paint.values[i], mode.components);
                if (group != ChiselPaintComponents.None)
                    result = Normalize(result, mode.components, group);
                paint.values[i] = result;
            }
        }

        /// <summary>One component's paint copied to another (0 x, 1 y, 2 z, 3 w)</summary>
        public static void CopyComponent(ChiselSurfacePaint paint, int from, int to)
        {
            for (int i = 0; paint != null && i < paint.values.Length; i++)
                paint.values[i][to] = paint.values[i][from];
        }

        /// <summary>Two components' paint swapped</summary>
        public static void SwapComponents(ChiselSurfacePaint paint, int a, int b)
        {
            for (int i = 0; paint != null && i < paint.values.Length; i++)
            {
                var value = paint.values[i];
                (value[a], value[b]) = (value[b], value[a]);
                paint.values[i] = value;
            }
        }

        /// <summary>A component's paint turned over within a range: its lowest becomes its highest</summary>
        public static void InvertComponent(ChiselSurfacePaint paint, int component, Vector2 range)
        {
            for (int i = 0; paint != null && i < paint.values.Length; i++)
                paint.values[i][component] = range.x + range.y - paint.values[i][component];
        }

        // The weights of a group made to sum to one again after the painted components changed: the others share what is
        // left, in the proportion they had (equally when they were all 0)
        static Vector4 Normalize(Vector4 value, ChiselPaintComponents painted, ChiselPaintComponents others)
        {
            float paintedSum = 0, othersSum = 0;
            int otherCount = 0;
            for (int c = 0; c < 4; c++)
            {
                var bit = (ChiselPaintComponents)(1 << c);
                if ((painted & bit) != 0) paintedSum += value[c];
                else if ((others & bit) != 0) { othersSum += value[c]; otherCount++; }
            }
            if (otherCount == 0)
                return value;
            var left = Mathf.Max(0, 1 - paintedSum);
            for (int c = 0; c < 4; c++)
            {
                var bit = (ChiselPaintComponents)(1 << c);
                if ((painted & bit) != 0 || (others & bit) == 0)
                    continue;
                value[c] = othersSum > 1e-6f ? value[c] * left / othersSum : left / otherCount;
            }
            return value;
        }

        // The average of a grid point and its neighbours
        static Vector4 Average(Vector4[] values, Vector2Int resolution, int i, int j)
        {
            var sum = Vector4.zero;
            var count = 0;
            for (int dj = -1; dj <= 1; dj++)
            {
                for (int di = -1; di <= 1; di++)
                {
                    int x = i + di, y = j + dj;
                    if (x < 0 || y < 0 || x >= resolution.x || y >= resolution.y)
                        continue;
                    sum += values[y * resolution.x + x];
                    count++;
                }
            }
            return sum / count;
        }
    }
}
