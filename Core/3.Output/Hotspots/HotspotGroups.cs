using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    // What matching a surface again together with others takes (HotspotGroups): its layout, frame and own settings
    public struct HotspotSurfaceSource
    {
        public int    layout;       // ChiselHotspotStore's id (index + 1); 0: never grouped
        public float3 right, up, normal;
        public uint   seed;         // hashed with the surface's own
        public int    pinned;
        public float  density;
        public float2 offset;
    }

    // Coplanar neighbouring surfaces, of any brush in the model, with the same hotspot layout and frame are connected
    //: mapped as one surface on one tile, with the first one's settings, the border only along their
    // outline together. By hand (HotspotLink) two touching coplanar surfaces are connected whatever their frames, or kept
    // apart; the newest link counts.
    internal static class HotspotGroups
    {
        // Normals and frames agree this closely (the cosine), and the surfaces share at least two corners
        const float kSameDirection = 1 - 1e-6f;

        public struct Group
        {
            public float4x4            uvMatrix;
            public HotspotSectionInput input;
            public HotspotSurfaceInfo  info;
            public float3              right, up;
            // its sections (when input has some), in the flat lists Find fills: where they start and how many there are
            public int3                sectionStart, sectionCount;
        }

        // The groups of the model's surfaces (groupOf: (brush, surface) -> group) and their sections, flat
        public static void Find(NativeList<BrushData> brushes, NativeArray<HotspotTileData> tiles, NativeArray<HotspotLayoutData> layouts, NativeArray<HotspotLink> links,
                                ref UnsafeHashMap<int2, int> groupOf, NativeList<Group> groups, NativeList<HotspotSectionLine> lines,
                                NativeList<HotspotSectionHalfPlane> halfPlanes, NativeList<HotspotSectionCell> cells)
        {
            groupOf.Clear();
            groups.Clear();
            if (!tiles.IsCreated || !layouts.IsCreated)
                return;
            var candidates = new NativeList<int2>(64, Allocator.Temp);
            for (int b = 0; b < brushes.Length; b++)
            {
                if (!brushes[b].brushRenderBuffer.IsCreated)
                    continue;
                ref var surfaces = ref brushes[b].brushRenderBuffer.Value.surfaces;
                for (int s = 0; s < surfaces.Length; s++)
                {
                    ref var surface = ref surfaces[s];
                    if (surface.hotspotSource.layout > 0 && surface.hotspotSource.layout <= layouts.Length &&
                        surface.indices.Length >= 3 && surface.decalEntityID == 0)
                        candidates.Add(new int2(b, s));
                }
            }
            if (candidates.Length < 2)
            {
                candidates.Dispose();
                return;
            }

            // who has each corner (each corner numbered, with each surface that has it, in order), and how many corners each
            // two share
            var cornerId = new UnsafeHashMap<float3, int>(candidates.Length * 8, Allocator.Temp);
            var having   = new NativeList<long>(candidates.Length * 8, Allocator.Temp);
            var seen     = new UnsafeHashSet<int>(16, Allocator.Temp);
            for (int c = 0; c < candidates.Length; c++)
            {
                ref var surface = ref brushes[candidates[c].x].brushRenderBuffer.Value.surfaces[candidates[c].y];
                seen.Clear();
                for (int v = 0; v < surface.colliderVertices.Length; v++)
                {
                    var p = surface.colliderVertices[v] + 0.0f;
                    if (!cornerId.TryGetValue(p, out var id))
                    {
                        id = cornerId.Count;
                        cornerId.Add(p, id);
                    }
                    if (seen.Add(id))
                        having.Add(((long)id << 32) | (uint)c);
                }
            }
            seen.Dispose();
            cornerId.Dispose();
            having.Sort();
            var shared = new UnsafeHashMap<int2, int>(candidates.Length * 4, Allocator.Temp);
            for (int start = 0; start < having.Length;)
            {
                var end = start + 1;
                while (end < having.Length && (having[end] >> 32) == (having[start] >> 32))
                    end++;
                for (int i = start; i < end; i++)
                {
                    for (int j = i + 1; j < end; j++)
                    {
                        var x = (int)(having[i] & 0xffffffff); var y = (int)(having[j] & 0xffffffff);
                        var pair = new int2(math.min(x, y), math.max(x, y));
                        shared.TryGetValue(pair, out var count);
                        shared.Remove(pair);
                        shared.Add(pair, count + 1);
                    }
                }
                start = end;
            }
            having.Dispose();

            // the links between candidates: the newest of each two (order, connected)
            var linked = new UnsafeHashMap<int2, int2>(math.max(1, links.IsCreated ? links.Length : 0), Allocator.Temp);
            if (links.IsCreated && links.Length > 0)
            {
                var brushOf = new UnsafeHashMap<CompactNodeID, int>(brushes.Length, Allocator.Temp);
                for (int b = 0; b < brushes.Length; b++)
                    brushOf.TryAdd(brushes[b].brushIndexOrder.compactNodeID, b);
                var candidateOf = new UnsafeHashMap<int2, int>(candidates.Length, Allocator.Temp);
                for (int c = 0; c < candidates.Length; c++)
                    candidateOf.TryAdd(candidates[c], c);
                for (int l = 0; l < links.Length; l++)
                {
                    var link = links[l];
                    if (!brushOf.TryGetValue(link.brushA, out var ba) || !brushOf.TryGetValue(link.brushB, out var bb) ||
                        !candidateOf.TryGetValue(new int2(ba, link.surfaceA), out var ca) || !candidateOf.TryGetValue(new int2(bb, link.surfaceB), out var cb) ||
                        ca == cb)
                        continue;
                    var pair = new int2(math.min(ca, cb), math.max(ca, cb));
                    if (linked.TryGetValue(pair, out var older) && older.x > link.order)
                        continue;
                    linked.Remove(pair);
                    linked.Add(pair, new int2(link.order, link.connect ? 1 : 0));
                }
                candidateOf.Dispose();
                brushOf.Dispose();
            }

            var parent = new NativeArray<int>(candidates.Length, Allocator.Temp);
            for (int c = 0; c < parent.Length; c++)
                parent[c] = c;
            int Root(int c)
            {
                while (parent[c] != c)
                {
                    parent[c] = parent[parent[c]];
                    c = parent[c];
                }
                return c;
            }
            using (var pairs = shared.GetKeyArray(Allocator.Temp))
            {
                for (int i = 0; i < pairs.Length; i++)
                {
                    if (shared[pairs[i]] < 2)
                        continue;
                    var a = SourceOf(brushes, candidates[pairs[i].x]);
                    var b = SourceOf(brushes, candidates[pairs[i].y]);
                    var coplanar  = a.layout == b.layout && math.dot(a.normal, b.normal) >= kSameDirection;
                    var sameFrame = math.dot(a.right, b.right) >= kSameDirection && math.dot(a.up, b.up) >= kSameDirection;
                    var together  = coplanar && sameFrame;
                    if (linked.TryGetValue(pairs[i], out var link))
                        together = coplanar && link.y != 0;
                    if (!together)
                        continue;
                    var ra = Root(pairs[i].x); var rb = Root(pairs[i].y);
                    if (ra != rb)
                        parent[math.max(ra, rb)] = math.min(ra, rb);   // the first candidate leads
                }
            }
            shared.Dispose();
            linked.Dispose();

            // each group of more than one: its members, matched as one surface on the first one's settings
            var size = new NativeArray<int>(candidates.Length, Allocator.Temp);
            for (int c = 0; c < candidates.Length; c++)
                size[Root(c)]++;
            var members   = new NativeList<int>(16, Allocator.Temp);
            var positions = new NativeList<float3>(64, Allocator.Temp);
            var indices   = new NativeList<int>(64, Allocator.Temp);
            for (int lead = 0; lead < candidates.Length; lead++)
            {
                if (Root(lead) != lead || size[lead] < 2)
                    continue;
                members.Clear(); positions.Clear(); indices.Clear();
                for (int c = lead; c < candidates.Length; c++)
                {
                    if (Root(c) != lead)
                        continue;
                    members.Add(c);
                    ref var surface = ref brushes[candidates[c].x].brushRenderBuffer.Value.surfaces[candidates[c].y];
                    var first = positions.Length;
                    for (int v = 0; v < surface.colliderVertices.Length; v++)
                        positions.Add(surface.colliderVertices[v]);
                    for (int i = 0; i < surface.indices.Length; i++)
                        indices.Add(first + surface.indices[i]);
                }
                var source  = SourceOf(brushes, candidates[lead]);
                var layout  = layouts[source.layout - 1];
                var density = source.density > 0 ? source.density : 1;
                var shape   = HotspotMatcher.ShapeOf(positions.AsArray(), source.right, source.up);
                var choice  = HotspotMatcher.Choose(tiles, layout, shape, source.seed, source.pinned, density: density);
                if (choice.tile < 0)
                    continue;
                var tile  = tiles[choice.tile];
                var group = new Group
                {
                    uvMatrix = HotspotMatcher.Mapping(tile, layout, shape, choice, source.right, source.up),
                    right    = source.right,
                    up       = source.up
                };
                if (tile.border > 0 || choice.Repeated || math.any(source.offset != 0))
                    group.input = HotspotSections.Input(tile, layout, shape, choice, source.right, source.up, density, source.offset);
                group.info = HotspotDiagnostics.Info(tile, layout, choice, group.input, source.pinned != 0 && tile.id == source.pinned, density);
                if (group.input.count > 0)
                {
                    // built once from what the group draws together, so the border runs only along its outline
                    var sections = HotspotSections.Create(Allocator.Temp);
                    var built = group.input.count == 4 && !group.input.Repeated
                              ? sections.BuildConforming(group.input, indices, positions)
                              : sections.Build(group.input);
                    if (built)
                    {
                        group.sectionStart = new int3(lines.Length, halfPlanes.Length, cells.Length);
                        group.sectionCount = new int3(sections.lines.Length, sections.halfPlanes.Length, sections.cells.Length);
                        lines.AddRange(sections.lines.AsArray());
                        halfPlanes.AddRange(sections.halfPlanes.AsArray());
                        cells.AddRange(sections.cells.AsArray());
                    }
                    sections.Dispose();
                }
                var index = groups.Length;
                groups.Add(group);
                for (int m = 0; m < members.Length; m++)
                    groupOf.TryAdd(candidates[members[m]], index);
            }
            members.Dispose(); positions.Dispose(); indices.Dispose();
            size.Dispose();
            parent.Dispose();
            candidates.Dispose();
        }

        // A group's sections, as Find stored them
        public static HotspotSections Sections(in Group group, NativeList<HotspotSectionLine> lines, NativeList<HotspotSectionHalfPlane> halfPlanes,
                                               NativeList<HotspotSectionCell> cells, Allocator allocator)
        {
            var sections = HotspotSections.Create(allocator);
            sections.lines.AddRange(lines.AsArray().GetSubArray(group.sectionStart.x, group.sectionCount.x));
            sections.halfPlanes.AddRange(halfPlanes.AsArray().GetSubArray(group.sectionStart.y, group.sectionCount.y));
            sections.cells.AddRange(cells.AsArray().GetSubArray(group.sectionStart.z, group.sectionCount.z));
            return sections;
        }

        static HotspotSurfaceSource SourceOf(NativeList<BrushData> brushes, int2 at)
        {
            return brushes[at.x].brushRenderBuffer.Value.surfaces[at.y].hotspotSource;
        }
    }
}
