using System.Collections.Generic;
using Chisel.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Editors
{
    // A tile tried on a rectangular surface by Chisel's own matching and mapping (HotspotMatcher.Choose with the tile
    // pinned, then the surface's sections, cut and uvs as the output makes them): the surface's longer side as long as
    // the tile's at its pixels per unit, the other side at the aspect
    sealed class HotspotTryOn
    {
        public static readonly float[] kAspects = { 1, 2, 4, 8 };

        public Vector2 size;             // the surface, in units
        public bool    mapped;           // matching took the tile
        public HotspotChoice choice;
        public bool    bordered, allBorder, budgetLimited;
        // what is drawn: triangles in the surface's units (x right, y up, from its bottom left) and their uvs
        public readonly List<Vector2> positions = new();
        public readonly List<Vector2> uvs       = new();
        public readonly List<int>     indices   = new();

        public static HotspotTryOn Run(ChiselHotspotLayout layout, int tileId, float aspect)
        {
            var index = layout != null ? layout.IndexOfTile(tileId) : -1;
            if (index < 0 || aspect <= 0)
                return null;
            var source = layout.tiles[index];
            if (source.points == null || source.points.Length < 3)
                return null;

            var result = new HotspotTryOn();
            using var tiles = new NativeList<HotspotTileData>(Allocator.Temp);
            using var sides = new NativeList<float4>(Allocator.Temp);
            HotspotMatcher.AddTiles(layout, tiles, out var data, sides);
            var t = -1;
            for (int i = 0; i < tiles.Length; i++)
                if (tiles[i].id == tileId) { t = i; break; }
            if (t < 0)
                return result;

            // the surface: the tile's longer side at its pixels per unit, the other at the aspect
            var min = source.points[0]; var max = source.points[0];
            foreach (var point in source.points) { min = Vector2Int.Min(min, point); max = Vector2Int.Max(max, point); }
            var pixelsPerUnit = tiles[t].pixelsPerUnit > 0 ? tiles[t].pixelsPerUnit : 1;
            var longest = Mathf.Max(max.x - min.x, max.y - min.y) / pixelsPerUnit;
            result.size = new Vector2(longest, longest / aspect);

            // facing -z: the frame's right is +x and its up +y
            var right = new float3(1, 0, 0); var up = new float3(0, 1, 0);
            using var surfaceIndices = new NativeList<int>(Allocator.Temp);
            using var collider = new NativeList<float3>(Allocator.Temp);
            using var select   = new NativeList<SelectVertex>(Allocator.Temp);
            using var render   = new NativeList<RenderVertex>(Allocator.Temp);
            Quad(result.size, surfaceIndices, collider, select, render);

            var shape = HotspotMatcher.ShapeOf(collider.AsArray(), right, up);
            var choice = HotspotMatcher.Choose(tiles.AsArray(), data, shape, 0, tileId);
            result.choice = choice;
            if (choice.tile < 0)
                return result;
            result.mapped = true;
            var tile = tiles[choice.tile];
            var input = HotspotSections.Input(tile, data, shape, choice, right, up, 1);
            result.bordered = tile.border > 0;
            result.allBorder = result.bordered && HotspotSections.IsAllBorder(input);

            // as the output maps a surface on its own (OutputHotspotSections): its sections, cut and given uvs; over its
            // budget without its interior's repeats; still over, or one section, uncut
            var sections = HotspotSections.Create(Allocator.Temp);
            try
            {
                if (OutputHotspotSections.BuildSections(ref sections, input, surfaceIndices, collider, default, sides.AsArray()))
                {
                    OutputHotspotSections.CutAndMap(sections, input, right, up, true, surfaceIndices, collider, select, render);
                    if (surfaceIndices.Length / 3 > input.MaxTriangles)
                    {
                        result.budgetLimited = true;
                        Quad(result.size, surfaceIndices, collider, select, render);
                        OutputHotspotSections.CutAndMap(sections, input, right, up, false, surfaceIndices, collider, select, render);
                        if (surfaceIndices.Length / 3 > input.MaxTriangles)
                            Uncut(result.size, tile, data, shape, choice, right, up, surfaceIndices, collider, render);
                    }
                } else
                    Uncut(result.size, tile, data, shape, choice, right, up, surfaceIndices, collider, render);
            }
            finally
            {
                sections.Dispose();
            }

            for (int i = 0; i < render.Length; i++)
            {
                result.positions.Add(new Vector2(render[i].position.x, render[i].position.y));
                result.uvs.Add(render[i].uv0);
            }
            for (int i = 0; i < surfaceIndices.Length; i++)
                result.indices.Add(surfaceIndices[i]);
            return result;
        }

        // The surface as two triangles
        static void Quad(Vector2 size, NativeList<int> indices, NativeList<float3> collider, NativeList<SelectVertex> select, NativeList<RenderVertex> render)
        {
            indices.Clear(); collider.Clear(); select.Clear(); render.Clear();
            var corners = new[] { new float3(0, 0, 0), new float3(size.x, 0, 0), new float3(size.x, size.y, 0), new float3(0, size.y, 0) };
            foreach (var corner in corners)
            {
                collider.Add(corner);
                select.Add(new SelectVertex { position = corner });
                render.Add(new RenderVertex { position = corner, normal = new float3(0, 0, -1) });
            }
            // wound as the hotspot tests wind a surface facing -z
            indices.Add(0); indices.Add(1); indices.Add(2);
            indices.Add(0); indices.Add(2); indices.Add(3);
        }

        // One section: the surface mapped on its tile as a whole
        static void Uncut(Vector2 size, in HotspotTileData tile, in HotspotLayoutData data, in HotspotShape shape, in HotspotChoice choice, float3 right, float3 up,
                          NativeList<int> indices, NativeList<float3> collider, NativeList<RenderVertex> render)
        {
            using var select = new NativeList<SelectVertex>(Allocator.Temp);
            Quad(size, indices, collider, select, render);
            var mapping = HotspotMatcher.Mapping(tile, data, shape, choice, right, up);
            for (int i = 0; i < render.Length; i++)
            {
                var vertex = render[i];
                vertex.uv0 = math.mul(mapping, new float4(vertex.position, 1)).xy;
                render[i] = vertex;
            }
        }

        // How much the tile stretches along each of the surface's axes: surface pixels for each of the tile's (1: not)
        public Vector2 Stretch => choice.scales.x > 0 && choice.scales.y > 0 ? new Vector2(1 / choice.scales.x, 1 / choice.scales.y) : Vector2.one;

        const float kSame = 0.02f;

        // What happens to the tile on the surface, and whether that is as it is meant to be (no stretching)
        public string Describe(out bool good)
        {
            good = false;
            if (!mapped)
                return "Doesn't map onto a surface like this";
            if (allBorder)
                return "Too small for its border: all border";
            var parts = new List<string>();
            var repeats = new Vector2Int(Mathf.Max(1, choice.repeatX), Mathf.Max(1, choice.repeatY));
            var partial = (choice.lastRepeat.x > 0 && choice.lastRepeat.x < 1) || (choice.lastRepeat.y > 0 && choice.lastRepeat.y < 1);
            if (repeats.x > 1 && repeats.y > 1)
                parts.Add($"Repeats {repeats.x} {HotspotTileChoice.kTimes} {repeats.y} times");
            else if (repeats.x > 1)
                parts.Add($"Repeats {repeats.x} times across");
            else if (repeats.y > 1)
                parts.Add($"Repeats {repeats.y} times up");
            if (partial)
                parts.Add("the last one cut off");
            var stretch = Stretch;
            var stretched = false;
            foreach (var (factor, more, less) in new[] { (stretch.x, "wider", "narrower"), (stretch.y, "taller", "lower") })
            {
                if (Mathf.Abs(factor - 1) <= kSame)
                    continue;
                stretched = true;
                parts.Add(factor > 1 ? $"stretches {Number(factor)}{HotspotTileChoice.kTimes} {more}" : $"squeezed {Number(1 / factor)}{HotspotTileChoice.kTimes} {less}");
            }
            if (stretched && bordered)
                parts.Add("the border keeps its width");
            if (choice.rotated)
                parts.Add("turned a quarter");
            if (budgetLimited)
                parts.Add("over its budget, its interior's repeats left out");
            good = !stretched && !budgetLimited;
            if (parts.Count == 0)
                return "Fits without stretching";
            var text = string.Join(", ", parts);
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        static string Number(float value) => value.ToString(value >= 10 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture);
    }
}
