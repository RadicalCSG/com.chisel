using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    /// <summary>
    /// How a model's lightmap texture coordinates (UV1) are laid out: how many lightmap texels a unit of the model gets, and how
    /// many texels lie between two charts.
    /// </summary>
    public struct LightmapUVSettings
    {
        /// <summary>Unity's default lightmap resolution, in texels per unit</summary>
        public const float kDefaultTexelsPerUnit = 40.0f;
        /// <summary>The texels between two charts when nothing says otherwise</summary>
        public const float kDefaultPaddingTexels = 2.0f;

        /// <summary>The lightmap texels a unit of the model gets: the lighting's lightmap resolution times the model's scale in lightmap</summary>
        public float texelsPerUnit;
        /// <summary>The texels between two charts, so that filtering the lightmap doesn't mix their lighting</summary>
        public float paddingTexels;

        public static LightmapUVSettings Default => new() { texelsPerUnit = kDefaultTexelsPerUnit, paddingTexels = kDefaultPaddingTexels };

        /// <summary>These settings, with the default in place of a value that can't be used</summary>
        public readonly LightmapUVSettings Usable => new()
        {
            texelsPerUnit = (math.isfinite(texelsPerUnit) && texelsPerUnit > 0) ? texelsPerUnit : kDefaultTexelsPerUnit,
            paddingTexels = (math.isfinite(paddingTexels) && paddingTexels >= 0) ? paddingTexels : kDefaultPaddingTexels
        };
    }

    /// <summary>How many lightmap texels a surface gets</summary>
    internal enum LightmapChartMode : byte
    {
        /// <summary>Texels in proportion to its size</summary>
        Texels,
        /// <summary>
        /// One texel: a surface that gives off light, whose own lighting doesn't show, but whose light a bake takes from its texels
        /// (<see cref="SurfaceOutputFlags.SingleLightmapTexel"/>)
        /// </summary>
        SingleTexel,
        /// <summary>None: a surface that is never lit by a lightmap (<see cref="SurfaceOutputFlags.NoLightmap"/>)</summary>
        None
    }

    /// <summary>A surface's lightmap chart: the rectangle its lightmap coordinates span in its plane, before they are laid out</summary>
    internal struct LightmapChart
    {
        /// <summary>The lowest coordinates in xy, the highest in zw, in the units of the model</summary>
        public float4            rect;
        public LightmapChartMode mode;
        /// <summary>Orders charts that need cells of the same size, so the layout depends only on the charts themselves</summary>
        public ulong             key;
    }

    /// <summary>Where a chart went in its mesh's lightmap layout</summary>
    internal struct LightmapChartPlacement
    {
        /// <summary>Where the chart's lowest corner goes, in texels</summary>
        public float2            origin;
        /// <summary>Texels per unit of the model; for a single texel, what scales the chart down into it</summary>
        public float             scale;
        /// <summary>The chart's coordinates are swapped, so that it lies on its long side</summary>
        public bool              rotated;
        public LightmapChartMode mode;

        /// <summary>Where a point of the chart goes in the layout, in texels</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float2 Place(float2 point, float4 rect)
        {
            switch (mode)
            {
                case LightmapChartMode.Texels:
                {
                    var texels = (point - rect.xy) * scale;
                    return origin + (rotated ? texels.yx : texels);
                }
                case LightmapChartMode.SingleTexel:
                    // Centred in its texel, and no larger than it
                    return origin + 0.5f + (point - ((rect.xy + rect.zw) * 0.5f)) * scale;
                default:
                    return float2.zero;
            }
        }
    }

    /// <summary>
    /// Lays out the lightmap charts of one mesh in a square. Every chart gets a cell of whole texels with the padding around it,
    /// lying on its long side. The cells go in rows, tallest first, in the narrowest square they fit in. A chart without texels
    /// gets no cell. Since cells of the same size are ordered by their charts' keys, the same surfaces always get the same
    /// coordinates, so a bake stays valid when the mesh is built again.
    /// </summary>
    internal static class LightmapUVLayout
    {
        /// <summary>Larger than any lightmap, and small enough that a layout's area can't overflow</summary>
        public const int kMaxChartTexels = 1 << 16;

        struct Cell
        {
            public int2  size;
            public ulong key;
            public int   chart;
        }

        struct TallestFirst : IComparer<Cell>
        {
            public int Compare(Cell a, Cell b)
            {
                if (a.size.y != b.size.y) return b.size.y.CompareTo(a.size.y);
                if (a.size.x != b.size.x) return b.size.x.CompareTo(a.size.x);
                if (a.key    != b.key)    return a.key.CompareTo(b.key);
                return a.chart.CompareTo(b.chart);
            }
        }

        /// <summary>How many lightmap texels the surface's output flags give it</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LightmapChartMode ModeOf(SurfaceOutputFlags outputFlags)
        {
            if ((outputFlags & SurfaceOutputFlags.NoLightmap) != 0)
                return LightmapChartMode.None;
            if ((outputFlags & SurfaceOutputFlags.SingleLightmapTexel) != 0)
                return LightmapChartMode.SingleTexel;
            return LightmapChartMode.Texels;
        }

        /// <summary>
        /// Places every chart and returns the side of the square they fit in, in texels (0 when no chart has texels).
        /// A point of chart i goes to placements[i].Place(point, charts[i].rect) / side.
        /// </summary>
        public static int Layout(NativeArray<LightmapChart> charts, LightmapUVSettings settings, NativeArray<LightmapChartPlacement> placements)
        {
            settings = settings.Usable;
            var padding = (int)math.ceil(settings.paddingTexels);

            var cells = new NativeArray<Cell>(charts.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var cellCount = 0;
            for (int i = 0; i < charts.Length; i++)
            {
                var chart   = charts[i];
                var extent  = math.max(chart.rect.zw - chart.rect.xy, float2.zero);
                var placement = new LightmapChartPlacement { mode = chart.mode };
                int2 content;
                switch (chart.mode)
                {
                    case LightmapChartMode.Texels:
                    {
                        // A chart longer than any lightmap could hold gets fewer texels per unit, so it still fits its cell
                        var largest = math.cmax(extent) * settings.texelsPerUnit;
                        placement.scale = (largest > kMaxChartTexels) ? settings.texelsPerUnit * (kMaxChartTexels / largest) : settings.texelsPerUnit;
                        content = (int2)math.clamp(math.ceil(extent * placement.scale), new float2(1), new float2(kMaxChartTexels));
                        placement.rotated = content.y > content.x;
                        if (placement.rotated)
                            content = content.yx;
                        break;
                    }
                    case LightmapChartMode.SingleTexel:
                    {
                        var largest = math.cmax(extent);
                        placement.scale = (largest > 0) ? 1.0f / largest : 0.0f;
                        content = new int2(1, 1);
                        break;
                    }
                    default:
                        placements[i] = placement;
                        continue;
                }
                placements[i] = placement;
                cells[cellCount++] = new Cell { size = content + padding, key = chart.key, chart = i };
            }

            var side = 0;
            if (cellCount > 0)
            {
                var sorted = cells.GetSubArray(0, cellCount);
                sorted.Sort(new TallestFirst());
                side = Pack(sorted, out var positions);
                var halfPadding = padding * 0.5f;
                for (int c = 0; c < cellCount; c++)
                {
                    var chart = sorted[c].chart;
                    var placement = placements[chart];
                    placement.origin = (float2)positions[c] + halfPadding;
                    placements[chart] = placement;
                }
                positions.Dispose();
            }
            cells.Dispose();
            return side;
        }

        // The rows the (sorted) cells make at this width, and their total height
        static int Rows(NativeArray<Cell> cells, int width, NativeArray<int2> positions)
        {
            int x = 0, y = 0, rowHeight = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                var size = cells[i].size;
                if (x > 0 && x + size.x > width)
                {
                    y += rowHeight;
                    x = 0;
                    rowHeight = 0;
                }
                if (positions.IsCreated)
                    positions[i] = new int2(x, y);
                x += size.x;
                rowHeight = math.max(rowHeight, size.y);
            }
            return y + rowHeight;
        }

        // The narrowest width whose rows are no taller than it is wide
        static int Pack(NativeArray<Cell> cells, out NativeArray<int2> positions)
        {
            long area = 0;
            var widest = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                area  += (long)cells[i].size.x * cells[i].size.y;
                widest = math.max(widest, cells[i].size.x);
            }

            var none = default(NativeArray<int2>);
            var low  = math.max(widest, (int)math.ceil(math.sqrt((double)area)));
            var high = math.max(low, Rows(cells, low, none));
            while (low < high)
            {
                var middle = low + ((high - low) / 2);
                if (Rows(cells, middle, none) <= middle)
                    high = middle;
                else
                    low = middle + 1;
            }

            positions = new NativeArray<int2>(cells.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var height = Rows(cells, low, positions);
            return math.max(low, height);
        }
    }
}
