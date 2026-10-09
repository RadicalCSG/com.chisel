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
    // outline together. By hand (HotspotLink) two touching surfaces are connected whatever their frames, or kept apart;
    // the newest link counts. Connected surfaces that are not coplanar are unfolded about the edge they share into the
    // first one's plane (the texture wraps round), each by a rigid map (unfoldOf). A link can also put
    // a border along the edge two surfaces share, or leave it out (Borders).
    internal static class HotspotGroups
    {
        // Normals and frames agree this closely (the cosine), and the surfaces share at least two corners
        const float kSameDirection = 1 - 1e-6f;
        // A corner two members have, unfolded by each, is one point when they put it this close (squared)
        const float kSamePoint = 1e-10f;

        public struct Group
        {
            public float4x4            uvMatrix;
            public HotspotSectionInput input;
            public HotspotSurfaceInfo  info;
            public float3              right, up;
            // its sections (when input has some), in the flat lists Find fills: where they start and how many there are
            public int3                sectionStart, sectionCount;
            public int                 interiorCell;
            public bool                interiorToEdges;
            public int                 openSides;
            // its layout (index into the layouts) and which of its materials the seed picked (HotspotChoice.variant)
            public int                 layout, variant;
        }

        // The corners two surfaces share where the newest link between them puts a border along their edge or leaves it
        // out (bordersOf: (brush, surface) -> corner, both surfaces), for OutlineOf
        public static void Borders(NativeList<BrushData> brushes, NativeArray<HotspotLink> links, ref UnsafeParallelMultiHashMap<int2, HotspotBorderCorner> bordersOf)
        {
            bordersOf.Clear();
            if (!links.IsCreated || links.Length == 0)
                return;
            var brushOf = new UnsafeHashMap<CompactNodeID, int>(brushes.Length, Allocator.Temp);
            for (int b = 0; b < brushes.Length; b++)
                brushOf.TryAdd(brushes[b].brushIndexOrder.compactNodeID, b);
            // the newest link of each two surfaces: (brush, surface, brush, surface) -> link
            var newest = new UnsafeHashMap<int4, int>(links.Length, Allocator.Temp);
            for (int l = 0; l < links.Length; l++)
            {
                var link = links[l];
                // one edge of a surface by itself: the rule is the edge
                if (link.OfOneEdge)
                {
                    if (link.border != 0 && brushOf.TryGetValue(link.brushA, out var brush) && HasSurface(brushes, brush, link.surfaceA))
                        bordersOf.Add(new int2(brush, link.surfaceA), new HotspotBorderCorner { position = link.edgeFrom, end = link.edgeTo, segment = true,
                                                                                               rule = link.order, border = link.border > 0 ? 1 : -1 });
                    continue;
                }
                if (!brushOf.TryGetValue(link.brushA, out var ba) || !brushOf.TryGetValue(link.brushB, out var bb) ||
                    !HasSurface(brushes, ba, link.surfaceA) || !HasSurface(brushes, bb, link.surfaceB))
                    continue;
                var key = ba < bb || (ba == bb && link.surfaceA <= link.surfaceB) ? new int4(ba, link.surfaceA, bb, link.surfaceB)
                                                                                    : new int4(bb, link.surfaceB, ba, link.surfaceA);
                if (key.xy.Equals(key.zw) || (newest.TryGetValue(key, out var older) && links[older].order > link.order))
                    continue;
                newest.Remove(key);
                newest.Add(key, l);
            }
            brushOf.Dispose();
            var shared = new NativeList<float3>(8, Allocator.Temp);
            var others = new UnsafeHashSet<float3>(16, Allocator.Temp);
            using (var pairs = newest.GetKeyArray(Allocator.Temp))
            {
                for (int i = 0; i < pairs.Length; i++)
                {
                    var link = links[newest[pairs[i]]];
                    if (link.border == 0)
                        continue;
                    ref var a = ref brushes[pairs[i].x].brushRenderBuffer.Value.surfaces[pairs[i].y];
                    ref var b = ref brushes[pairs[i].z].brushRenderBuffer.Value.surfaces[pairs[i].w];
                    others.Clear();
                    for (int v = 0; v < b.colliderVertices.Length; v++)
                        others.Add(b.colliderVertices[v] + 0.0f);
                    shared.Clear();
                    for (int v = 0; v < a.colliderVertices.Length; v++)
                    {
                        var p = a.colliderVertices[v] + 0.0f;
                        if (others.Contains(p) && !shared.Contains(p))
                            shared.Add(p);
                    }
                    if (shared.Length < 2)
                        continue;
                    for (int c = 0; c < shared.Length; c++)
                    {
                        var corner = new HotspotBorderCorner { position = shared[c], rule = link.order, border = link.border > 0 ? 1 : -1 };
                        bordersOf.Add(pairs[i].xy, corner);
                        bordersOf.Add(pairs[i].zw, corner);
                    }
                }
            }
            others.Dispose();
            shared.Dispose();
            newest.Dispose();
        }

        static bool HasSurface(NativeList<BrushData> brushes, int brush, int surface)
        {
            return brushes[brush].brushRenderBuffer.IsCreated && surface >= 0 && surface < brushes[brush].brushRenderBuffer.Value.surfaces.Length &&
                   brushes[brush].brushRenderBuffer.Value.surfaces[surface].indices.Length >= 3;
        }

        // The corners of a surface's edges given a border or none by hand (Borders), unfolded by its map
        public static void BordersOf(int2 surface, in UnsafeParallelMultiHashMap<int2, HotspotBorderCorner> bordersOf, NativeList<HotspotBorderCorner> rules)
        {
            rules.Clear();
            if (!bordersOf.IsCreated || !bordersOf.TryGetFirstValue(surface, out var corner, out var iterator))
                return;
            do rules.Add(corner);
            while (bordersOf.TryGetNextValue(out corner, ref iterator));
        }

        // The groups of the model's surfaces (groupOf: (brush, surface) -> group; unfoldOf: how a surface not in its group's
        // first plane is unfolded into it) and their sections, flat; bordersOf: where links put or leave out a border (Borders)
        public static void Find(NativeList<BrushData> brushes, NativeArray<HotspotTileData> tiles, NativeArray<HotspotLayoutData> layouts, NativeArray<HotspotLink> links,
                                in UnsafeParallelMultiHashMap<int2, HotspotBorderCorner> bordersOf, ref UnsafeHashMap<int2, int> groupOf, ref UnsafeHashMap<int2, float4x4> unfoldOf, NativeList<Group> groups, NativeList<HotspotSectionLine> lines,
                                NativeList<HotspotSectionHalfPlane> halfPlanes, NativeList<HotspotSectionCell> cells, NativeArray<float4> tileSides = default)
        {
            groupOf.Clear();
            unfoldOf.Clear();
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

            // the links between candidates: the newest of each two (order, connection)
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
                    linked.Add(pair, new int2(link.order, link.connection));
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
            // (candidate, candidate, priority): the order of the link that connected them, or above every link by themselves
            var joined = new NativeList<int3>(candidates.Length, Allocator.Temp);
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
                    var priority  = int.MaxValue;
                    if (linked.TryGetValue(pairs[i], out var link) && link.y != 0)
                    {
                        together = a.layout == b.layout && link.y > 0;
                        priority = link.x;
                    }
                    if (!together)
                        continue;
                    joined.Add(new int3(pairs[i], priority));
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

            // where a ring of connected surfaces closes, one edge keeps a seam: the oldest link's (the design's newest-first
            // priority), so the members are unfolded along the newest links that reach them all (a spanning tree)
            joined.Sort(new NewestFirst());
            var tree       = new NativeList<int2>(joined.Length, Allocator.Temp);
            var treeParent = new NativeArray<int>(candidates.Length, Allocator.Temp);
            for (int c = 0; c < treeParent.Length; c++)
                treeParent[c] = c;
            int TreeRoot(int c)
            {
                while (treeParent[c] != c)
                {
                    treeParent[c] = treeParent[treeParent[c]];
                    c = treeParent[c];
                }
                return c;
            }
            for (int j = 0; j < joined.Length; j++)
            {
                var ra = TreeRoot(joined[j].x); var rb = TreeRoot(joined[j].y);
                if (ra == rb)
                    continue;
                treeParent[ra] = rb;
                tree.Add(joined[j].xy);
            }
            treeParent.Dispose();

            // each member unfolded into its group's first plane: from the first outwards, each about the edge it shares with
            // the one it was reached from
            var unfold  = new NativeArray<float4x4>(candidates.Length, Allocator.Temp);
            var reached = new NativeArray<bool>(candidates.Length, Allocator.Temp);
            var queue   = new NativeList<int>(16, Allocator.Temp);
            for (int c = 0; c < candidates.Length; c++)
                unfold[c] = float4x4.identity;
            for (int lead = 0; lead < candidates.Length; lead++)
            {
                if (Root(lead) != lead || size[lead] < 2)
                    continue;
                queue.Clear();
                queue.Add(lead);
                reached[lead] = true;
                for (int q = 0; q < queue.Length; q++)
                {
                    var from = queue[q];
                    for (int j = 0; j < tree.Length; j++)
                    {
                        var to = tree[j].x == from ? tree[j].y : (tree[j].y == from ? tree[j].x : -1);
                        if (to < 0 || reached[to])
                            continue;
                        reached[to] = true;
                        queue.Add(to);
                        unfold[to] = math.mul(unfold[from], Hinge(brushes, candidates[from], candidates[to]));
                    }
                }
            }
            reached.Dispose();
            queue.Dispose();
            tree.Dispose();
            joined.Dispose();
            var members   = new NativeList<int>(16, Allocator.Temp);
            var positions = new NativeList<float3>(64, Allocator.Temp);
            var indices   = new NativeList<int>(64, Allocator.Temp);
            var rules     = new NativeList<HotspotBorderCorner>(8, Allocator.Temp);
            var memberRules = new NativeList<HotspotBorderCorner>(8, Allocator.Temp);
            // where each corner was unfolded to first: the edge two members share is the same in both
            var placed    = new UnsafeHashMap<float3, float3>(64, Allocator.Temp);
            float3 Place(float3 p, float4x4 map)
            {
                p += 0.0f;
                var q = math.mul(map, new float4(p, 1)).xyz;
                if (placed.TryGetValue(p, out var first) && math.distancesq(first, q) <= kSamePoint * math.max(1, math.lengthsq(q)))
                    return first;
                placed.TryAdd(p, q);
                return q;
            }
            for (int lead = 0; lead < candidates.Length; lead++)
            {
                if (Root(lead) != lead || size[lead] < 2)
                    continue;
                members.Clear(); positions.Clear(); indices.Clear(); rules.Clear(); placed.Clear();
                for (int c = lead; c < candidates.Length; c++)
                {
                    if (Root(c) != lead)
                        continue;
                    members.Add(c);
                    ref var surface = ref brushes[candidates[c].x].brushRenderBuffer.Value.surfaces[candidates[c].y];
                    var first = positions.Length;
                    for (int v = 0; v < surface.colliderVertices.Length; v++)
                        positions.Add(Place(surface.colliderVertices[v], unfold[c]));
                    for (int i = 0; i < surface.indices.Length; i++)
                        indices.Add(first + surface.indices[i]);
                }
                for (int m = 0; m < members.Length; m++)
                {
                    BordersOf(candidates[members[m]], bordersOf, memberRules);
                    for (int i = 0; i < memberRules.Length; i++)
                    {
                        var corner = memberRules[i];
                        corner.position = Place(corner.position, unfold[members[m]]);
                        if (corner.segment)
                            corner.end = Place(corner.end, unfold[members[m]]);
                        rules.Add(corner);
                    }
                }
                var source  = SourceOf(brushes, candidates[lead]);
                var layout  = layouts[source.layout - 1];
                var density = source.density > 0 ? source.density : 1;
                var shape   = HotspotMatcher.ShapeOf(positions.AsArray(), source.right, source.up);
                HotspotMatcher.MaskOf(ref shape, positions.AsArray(), indices.AsArray(), source.right, source.up);
                var choice  = HotspotMatcher.Choose(tiles, layout, shape, source.seed, source.pinned, density: density);
                if (choice.tile < 0)
                    continue;
                var tile  = tiles[choice.tile];
                var group = new Group
                {
                    uvMatrix = HotspotMatcher.Mapping(tile, layout, shape, choice, source.right, source.up),
                    right    = source.right,
                    up       = source.up,
                    layout   = source.layout - 1,
                    variant  = choice.variant
                };
                if (tile.border > 0 || choice.Repeated || math.any(source.offset != 0))
                    group.input = HotspotSections.Input(tile, layout, shape, choice, source.right, source.up, density, source.offset);
                group.info = HotspotDiagnostics.Info(tile, layout, choice, group.input, source.pinned != 0 && tile.id == source.pinned, density);
                if (group.input.count > 0)
                {
                    // built once from what the group draws together, so the border runs only along its outline
                    var sections = HotspotSections.Create(Allocator.Temp);
                    var built = group.input.count == 4 && !group.input.Repeated
                              ? sections.BuildConforming(group.input, indices, positions, rules.AsArray(), tileSides)
                              : sections.Build(group.input);
                    if (built)
                    {
                        group.interiorCell    = sections.interiorCell;
                        group.interiorToEdges = sections.interiorToEdges;
                        group.openSides       = sections.openSides;
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
                {
                    groupOf.TryAdd(candidates[members[m]], index);
                    if (!unfold[members[m]].Equals(float4x4.identity))
                        unfoldOf.TryAdd(candidates[members[m]], unfold[members[m]]);
                }
            }
            members.Dispose(); positions.Dispose(); indices.Dispose(); rules.Dispose(); memberRules.Dispose(); placed.Dispose();
            size.Dispose();
            unfold.Dispose();
            parent.Dispose();
            candidates.Dispose();
        }

        // The newest link first (connected by themselves before any), then by candidate, so the order never depends on a
        // hash map's
        struct NewestFirst : System.Collections.Generic.IComparer<int3>
        {
            public readonly int Compare(int3 a, int3 b) =>
                a.z != b.z ? b.z.CompareTo(a.z) : (a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        }

        // A group's sections, as Find stored them
        public static HotspotSections Sections(in Group group, NativeList<HotspotSectionLine> lines, NativeList<HotspotSectionHalfPlane> halfPlanes,
                                               NativeList<HotspotSectionCell> cells, Allocator allocator)
        {
            var sections = HotspotSections.Create(allocator);
            sections.lines.AddRange(lines.AsArray().GetSubArray(group.sectionStart.x, group.sectionCount.x));
            sections.halfPlanes.AddRange(halfPlanes.AsArray().GetSubArray(group.sectionStart.y, group.sectionCount.y));
            sections.cells.AddRange(cells.AsArray().GetSubArray(group.sectionStart.z, group.sectionCount.z));
            sections.interiorCell    = group.interiorCell;
            sections.interiorToEdges = group.interiorToEdges;
            sections.openSides       = group.openSides;
            return sections;
        }

        // The rigid map that turns surface to about the edge it shares with surface from into from's plane (identity when they
        // are coplanar, or share no edge)
        static float4x4 Hinge(NativeList<BrushData> brushes, int2 from, int2 to)
        {
            var nFrom = math.normalizesafe(SourceOf(brushes, from).normal);
            var nTo   = math.normalizesafe(SourceOf(brushes, to).normal);
            if (math.dot(nFrom, nTo) >= kSameDirection)
                return float4x4.identity;
            ref var a = ref brushes[from.x].brushRenderBuffer.Value.surfaces[from.y];
            ref var b = ref brushes[to.x].brushRenderBuffer.Value.surfaces[to.y];
            var corners = new UnsafeHashSet<float3>(a.colliderVertices.Length, Allocator.Temp);
            for (int v = 0; v < a.colliderVertices.Length; v++)
                corners.Add(a.colliderVertices[v] + 0.0f);
            // the shared edge: the two shared corners furthest apart
            var q0 = float3.zero; var q1 = float3.zero; var longest = 0f;
            for (int i = 0; i < b.colliderVertices.Length; i++)
            {
                var p = b.colliderVertices[i] + 0.0f;
                if (!corners.Contains(p))
                    continue;
                for (int k = i + 1; k < b.colliderVertices.Length; k++)
                {
                    var r = b.colliderVertices[k] + 0.0f;
                    if (!corners.Contains(r) || !(math.distancesq(p, r) > longest))
                        continue;
                    longest = math.distancesq(p, r); q0 = p; q1 = r;
                }
            }
            corners.Dispose();
            if (!(longest > 0))
                return float4x4.identity;
            var axis  = math.normalize(q1 - q0);
            var angle = math.atan2(math.dot(axis, math.cross(nTo, nFrom)), math.dot(nTo, nFrom));
            return math.mul(float4x4.Translate(q0), math.mul(float4x4.AxisAngle(axis, angle), float4x4.Translate(-q0)));
        }

        static HotspotSurfaceSource SourceOf(NativeList<BrushData> brushes, int2 at)
        {
            return brushes[at.x].brushRenderBuffer.Value.surfaces[at.y].hotspotSource;
        }
    }
}
