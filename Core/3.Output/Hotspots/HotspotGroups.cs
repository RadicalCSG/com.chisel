using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    // What matching a surface again together with others takes (HotspotGroups): its layout, frame and own settings
    public struct HotspotSurfaceSource
    {
        public int    layout;       // ChiselHotspotStore's id (index + 1); 0: never grouped
        public float3 right, up, normal;
        public uint   seed;         // hashed with the surface's own
        public int    pinned, pinnedSheet;
        public float  density;
        public float2 offset;
        public bool   world;        // repeats on a grid fixed in the tree (the texture not locked to the object)
    }

    // A group matched on its own (HotspotGroups.Match): its sections until they are put in the model's flat lists
    internal struct GroupMatch
    {
        public HotspotGroups.Group group;
        public bool valid, hasSections;
        public UnsafeList<HotspotSectionLine> lines;
        public UnsafeList<HotspotSectionHalfPlane> halfPlanes;
        public UnsafeList<HotspotSectionCell> cells;
        // what it was made from (keyed: with a cache); owned: its sections are its own, not the cache's
        public uint4 key;
        public bool keyed, owned;

        public void Dispose()
        {
            if (lines.IsCreated)      lines.Dispose();
            if (halfPlanes.IsCreated) halfPlanes.Dispose();
            if (cells.IsCreated)      cells.Dispose();
        }
    }

    // A group's match, kept with the tree while its inputs stay the same (HotspotGroups.Match)
    internal struct GroupMatchEntry
    {
        public GroupMatch match;
        public bool used;
    }

    // What HotspotGroups.Prepare finds, read by every group's match
    internal struct FindState
    {
        public NativeList<int2>      candidates;
        public NativeList<float4x4>  unfold;
        public NativeList<int>       memberStart, memberOf, leads;
        public NativeList<GroupMatch> matches;
        // what every match reads (InputsKey)
        public NativeReference<uint4> inputs;
        // each surface's part in the groups (HotspotGroups.KindOf), when worked out brush by brush before Prepare
        // (StartKinds, KindsOf): kinds from kindStart[brush]
        public NativeList<int>  kindStart;
        public NativeList<byte> kinds;
        // and what of each candidate grouping reads (HotspotGroups.SurfaceHash), next to its kind
        public NativeList<uint4> hashes;

        public static FindState Create(Allocator allocator)
        {
            return new FindState
            {
                candidates  = new NativeList<int2>(64, allocator),
                unfold      = new NativeList<float4x4>(64, allocator),
                memberStart = new NativeList<int>(64, allocator),
                memberOf    = new NativeList<int>(64, allocator),
                leads       = new NativeList<int>(16, allocator),
                matches     = new NativeList<GroupMatch>(16, allocator),
                inputs      = new NativeReference<uint4>(allocator),
                kindStart   = new NativeList<int>(64, allocator),
                kinds       = new NativeList<byte>(64, allocator),
                hashes      = new NativeList<uint4>(64, allocator)
            };
        }

        internal static void FreeMatches(NativeList<GroupMatch> matches)
        {
            for (int i = 0; i < matches.Length; i++)
            {
                if (matches[i].owned)
                    matches[i].Dispose();
            }
        }

        public void Clear()
        {
            FreeMatches(matches);
            candidates.Clear(); unfold.Clear(); memberStart.Clear(); memberOf.Clear(); leads.Clear(); matches.Clear();
        }

        public void Dispose()
        {
            if (matches.IsCreated) { FreeMatches(matches); matches.Dispose(); }
            if (candidates.IsCreated)  candidates.Dispose();
            if (unfold.IsCreated)      unfold.Dispose();
            if (memberStart.IsCreated) memberStart.Dispose();
            if (memberOf.IsCreated)    memberOf.Dispose();
            if (leads.IsCreated)       leads.Dispose();
            if (inputs.IsCreated)      inputs.Dispose();
            if (kindStart.IsCreated)   kindStart.Dispose();
            if (kinds.IsCreated)       kinds.Dispose();
            if (hashes.IsCreated)      hashes.Dispose();
        }

        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle dependency)
        {
            if (matches.IsCreated)
                dependency = Unity.Jobs.IJobExtensions.Schedule(new FreeGroupMatchesJob { matches = matches }, dependency);
            var handles = new NativeArray<Unity.Jobs.JobHandle>(10, Allocator.Temp);
            for (int i = 0; i < handles.Length; i++)
                handles[i] = dependency;
            if (matches.IsCreated)     handles[0] = matches.Dispose(dependency);
            if (candidates.IsCreated)  handles[1] = candidates.Dispose(dependency);
            if (unfold.IsCreated)      handles[2] = unfold.Dispose(dependency);
            if (memberStart.IsCreated) handles[3] = memberStart.Dispose(dependency);
            if (memberOf.IsCreated)    handles[4] = memberOf.Dispose(dependency);
            if (leads.IsCreated)       handles[5] = leads.Dispose(dependency);
            if (inputs.IsCreated)      handles[6] = inputs.Dispose(dependency);
            if (kindStart.IsCreated)   handles[7] = kindStart.Dispose(dependency);
            if (kinds.IsCreated)       handles[8] = kinds.Dispose(dependency);
            if (hashes.IsCreated)      handles[9] = hashes.Dispose(dependency);
            var all = Unity.Jobs.JobHandle.CombineDependencies(handles);
            handles.Dispose();
            return all;
        }
    }

    // A surface of the tree, by its brush and its index: what a kept grouping is kept by
    internal struct SurfaceKey : System.IEquatable<SurfaceKey>
    {
        public CompactNodeID brush;
        public int surface;
        public readonly bool Equals(SurfaceKey other) => brush == other.brush && surface == other.surface;
        public override readonly int GetHashCode() => (int)math.hash(new uint2(brush.Hash(), (uint)surface));
    }

    // Each candidate's index by its key: its brush by node id, then flat by the brush's first surface
    internal struct CandidateIndex
    {
        UnsafeHashMap<CompactNodeID, int> brushOf;
        UnsafeList<int> surfaceStart;
        UnsafeList<int> at;
        public int Count;

        public CandidateIndex(NativeList<BrushData> brushes, NativeArray<int2> candidates, Allocator allocator)
        {
            brushOf      = new UnsafeHashMap<CompactNodeID, int>(math.max(1, brushes.Length), allocator);
            surfaceStart = new UnsafeList<int>(brushes.Length + 1, allocator);
            var total = 0;
            for (int b = 0; b < brushes.Length; b++)
            {
                brushOf.TryAdd(brushes[b].brushIndexOrder.compactNodeID, b);
                surfaceStart.Add(total);
                total += brushes[b].brushRenderBuffer.IsCreated ? brushes[b].brushRenderBuffer.Value.surfaces.Length : 0;
            }
            surfaceStart.Add(total);
            at = new UnsafeList<int>(math.max(1, total), allocator);
            at.Resize(total);
            for (int i = 0; i < total; i++)
                at[i] = -1;
            Count = 0;
            for (int c = 0; c < candidates.Length; c++)
            {
                var index = surfaceStart[candidates[c].x] + candidates[c].y;
                if (at[index] >= 0)
                    continue;
                at[index] = c;
                Count++;
            }
        }

        public readonly bool HasBrush(CompactNodeID brush) => brushOf.ContainsKey(brush);

        public readonly bool TryGetValue(SurfaceKey key, out int candidate)
        {
            candidate = -1;
            if (!brushOf.TryGetValue(key.brush, out var b) || key.surface < 0 || key.surface >= surfaceStart[b + 1] - surfaceStart[b])
                return false;
            candidate = at[surfaceStart[b] + key.surface];
            return candidate >= 0;
        }

        public readonly bool ContainsKey(SurfaceKey key) => TryGetValue(key, out _);

        public void Dispose()
        {
            brushOf.Dispose(); surfaceStart.Dispose(); at.Dispose();
        }
    }

    // A candidate as the last update grouped it
    internal struct KeptCandidate
    {
        public uint4 hash;
        public SurfaceKey lead;
        public float4x4 unfold;
        // its corners, each once (as grouping compares them: + 0.0f)
        public UnsafeList<float3> corners;
    }

    // What the last update's grouping found (HotspotGroups.PrepareIncremental), kept with the tree so the next one groups
    // again only what its edits can have changed (IncrementalHotspotGroups.md)
    internal struct HotspotGroupMemory
    {
        public NativeParallelHashMap<SurfaceKey, KeptCandidate> candidates;
        // each corner of a candidate -> the candidates that have it
        public NativeParallelMultiHashMap<float3, SurfaceKey> cornerIndex;
        // the lead of each group of two or more -> its members (the lead among them)
        public NativeParallelMultiHashMap<SurfaceKey, SurfaceKey> members;
        // the brushes, in order, and how many surfaces each had; what every candidate's grouping reads (GroupInputsKey)
        public NativeList<CompactNodeID> order;
        public NativeParallelHashMap<CompactNodeID, int> surfaceCount;
        public NativeReference<uint4> inputs;
        public NativeReference<bool> valid;
        // what the last update did: grouped everything again (full), or this many candidates (dirty)
        public NativeReference<bool> lastFull;
        public NativeReference<int> lastDirty;

        public static HotspotGroupMemory Create(Allocator allocator)
        {
            return new HotspotGroupMemory
            {
                candidates  = new NativeParallelHashMap<SurfaceKey, KeptCandidate>(64, allocator),
                cornerIndex = new NativeParallelMultiHashMap<float3, SurfaceKey>(256, allocator),
                members     = new NativeParallelMultiHashMap<SurfaceKey, SurfaceKey>(64, allocator),
                order       = new NativeList<CompactNodeID>(64, allocator),
                surfaceCount = new NativeParallelHashMap<CompactNodeID, int>(64, allocator),
                inputs      = new NativeReference<uint4>(allocator),
                valid       = new NativeReference<bool>(allocator),
                lastFull    = new NativeReference<bool>(allocator),
                lastDirty   = new NativeReference<int>(allocator)
            };
        }

        public void Clear()
        {
            foreach (var pair in candidates)
            {
                if (pair.Value.corners.IsCreated)
                    pair.Value.corners.Dispose();
            }
            candidates.Clear();
            cornerIndex.Clear();
            members.Clear();
            order.Clear();
            surfaceCount.Clear();
            valid.Value = false;
        }

        public void Dispose()
        {
            if (candidates.IsCreated)
            {
                foreach (var pair in candidates)
                {
                    if (pair.Value.corners.IsCreated)
                        pair.Value.corners.Dispose();
                }
                candidates.Dispose();
            }
            if (cornerIndex.IsCreated) cornerIndex.Dispose();
            if (members.IsCreated)     members.Dispose();
            if (order.IsCreated)       order.Dispose();
            if (surfaceCount.IsCreated) surfaceCount.Dispose();
            if (inputs.IsCreated)      inputs.Dispose();
            if (valid.IsCreated)       valid.Dispose();
            if (lastFull.IsCreated)    lastFull.Dispose();
            if (lastDirty.IsCreated)   lastDirty.Dispose();
        }
    }

    [Unity.Burst.BurstCompile]
    struct FreeGroupMatchesJob : Unity.Jobs.IJob
    {
        public NativeList<GroupMatch> matches;
        public void Execute() { FindState.FreeMatches(matches); }
    }

    // Coplanar neighbouring surfaces, of any brush in the model, with the same hotspot layout and frame are connected:
    // mapped as one surface on one tile, with the first one's settings, the border only along their outline together. By hand
    // (HotspotLink) two touching surfaces are connected whatever their frames, or kept apart; the newest link counts.
    // Connected surfaces that are not coplanar are unfolded about the edge they share into the first one's plane, so the
    // texture wraps round, each by a rigid map (unfoldOf). A link can also put a border along the edge two surfaces share,
    // or leave it out (Borders). A sliver, narrower than a texel of its layout, shows no tile: it is mapped with the
    // neighbour it shares its longest edge with.
    internal static class HotspotGroups
    {
        // Every update also groups everything again (Prepare) and logs where the incremental result differs
        public static bool VerifyIncremental;

        // Normals and frames agree this closely (the cosine), and the surfaces share at least two corners
        const float kSameDirection = 1 - 1e-6f;
        // A corner two members have, unfolded by each, is one point when they put it this close (squared)
        const float kSamePoint = 1e-10f;
        // A surface narrower than this many texels of its layout (at its density) is a sliver
        const float kSliverTexels = 1;

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
            // the material of the other sheet its tile is on (HotspotTileData.sheet); 0: its own
            public ulong               sheetMaterial;
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
            var state = FindState.Create(Allocator.Temp);
            try
            {
                Prepare(brushes, tiles, layouts, links, ref state);
                var matches = state.matches.AsArray();
                for (int i = 0; i < matches.Length; i++)
                    matches[i] = Match(state.leads[i], brushes.AsArray(), tiles, layouts, tileSides, bordersOf, state);
                Assemble(ref state, ref groupOf, ref unfoldOf, groups, lines, halfPlanes, cells);
            }
            finally
            {
                state.Dispose();
            }
        }

        const byte kCandidate = 1, kSliver = 2;

        // A surface's part in the groups: none (0), a candidate (1), or a sliver too narrow to show a tile (2)
        public static byte KindOf(ref ChiselSurfaceRenderBuffer surface, NativeArray<HotspotLayoutData> layouts)
        {
            if (!layouts.IsCreated || !(surface.hotspotSource.layout > 0 && surface.hotspotSource.layout <= layouts.Length &&
                                        surface.indices.Length >= 3 && surface.decalEntityID == 0))
                return 0;
            var source = surface.hotspotSource;
            var texels = layouts[source.layout - 1].pixelsPerUnit * (source.density > 0 ? source.density : 1);
            return Width(ref surface.colliderVertices, source) * texels < kSliverTexels ? kSliver : kCandidate;
        }

        // Room for each surface's kind (KindsOf), brush by brush
        public static void StartKinds(NativeList<BrushData> brushes, ref FindState state)
        {
            state.kindStart.ResizeUninitialized(brushes.Length + 1);
            var total = 0;
            for (int b = 0; b < brushes.Length; b++)
            {
                state.kindStart[b] = total;
                total += brushes[b].brushRenderBuffer.IsCreated ? brushes[b].brushRenderBuffer.Value.surfaces.Length : 0;
            }
            state.kindStart[brushes.Length] = total;
            state.kinds.ResizeUninitialized(total);
            state.hashes.ResizeUninitialized(total);
        }

        // The kinds of a brush's surfaces (KindOf), and the hashes of its candidates (SurfaceHash); each brush is independent
        // of the others
        public static void KindsOf(int b, NativeArray<BrushData> brushes, NativeArray<HotspotLayoutData> layouts, NativeArray<int> kindStart, NativeArray<byte> kinds,
                                   NativeArray<uint4> hashes)
        {
            if (!brushes[b].brushRenderBuffer.IsCreated)
                return;
            ref var surfaces = ref brushes[b].brushRenderBuffer.Value.surfaces;
            for (int s = 0; s < surfaces.Length; s++)
            {
                var kind = KindOf(ref surfaces[s], layouts);
                kinds[kindStart[b] + s]  = kind;
                hashes[kindStart[b] + s] = kind == 0 ? default : SurfaceHash(ref surfaces[s], kind);
            }
        }

        // VerifyIncremental: a brush's kept kinds and hashes are what KindsOf makes of it now
        public static void VerifyKeptKinds(int b, NativeArray<BrushData> brushes, NativeArray<HotspotLayoutData> layouts, NativeArray<int> kindStart, NativeArray<byte> kinds,
                                           NativeArray<uint4> hashes)
        {
            ref var surfaces = ref brushes[b].brushRenderBuffer.Value.surfaces;
            for (int s = 0; s < surfaces.Length; s++)
            {
                var kind = KindOf(ref surfaces[s], layouts);
                var hash = kind == 0 ? default : SurfaceHash(ref surfaces[s], kind);
                if (kinds[kindStart[b] + s] != kind || !hashes[kindStart[b] + s].Equals(hash))
                {
                    UnityEngine.Debug.LogError($"Kept hotspot kind of brush {b}, surface {s} differs from what its buffer gives now");
                    return;
                }
            }
        }

        // What grouping reads of a candidate that is its own: its kind, its frame and settings, its corners
        public static unsafe uint4 SurfaceHash(ref ChiselSurfaceRenderBuffer surface, byte kind)
        {
            var source = surface.hotspotSource;
            var buffer = new UnsafeAppendBuffer(128, 16, Allocator.Temp);
            buffer.Add(kind);
            buffer.Add(source.layout); buffer.Add(source.right); buffer.Add(source.up); buffer.Add(source.normal);
            buffer.Add(source.seed); buffer.Add(source.pinned); buffer.Add(source.pinnedSheet); buffer.Add(source.density);
            buffer.Add(source.offset); buffer.Add(source.world ? 1 : 0);
            buffer.Add(surface.colliderVertices.Length);
            buffer.Add(surface.colliderVertices.Length == 0 ? default : xxHash3.Hash128(surface.colliderVertices.GetUnsafePtr(), (long)surface.colliderVertices.Length * UnsafeUtility.SizeOf<float3>()));
            var hash = xxHash3.Hash128(buffer.Ptr, buffer.Length);
            buffer.Dispose();
            return hash;
        }

        // What every candidate's grouping reads: the layouts and the links
        public static unsafe uint4 GroupInputsKey(NativeArray<HotspotLayoutData> layouts, NativeArray<HotspotLink> links)
        {
            var parts = stackalloc uint4[2];
            parts[0] = layouts.IsCreated && layouts.Length > 0 ? xxHash3.Hash128(layouts.GetUnsafeReadOnlyPtr(), (long)layouts.Length * UnsafeUtility.SizeOf<HotspotLayoutData>()) : default;
            parts[1] = links.IsCreated && links.Length > 0     ? xxHash3.Hash128(links.GetUnsafeReadOnlyPtr(), (long)links.Length * UnsafeUtility.SizeOf<HotspotLink>()) : default;
            return xxHash3.Hash128(parts, 2 * 16);
        }

        // Everything Find does but matching the groups (Match): the candidates, who is grouped with whom, how each is
        // unfolded, and the groups to match (state.leads)
        public static void Prepare(NativeList<BrushData> brushes, NativeArray<HotspotTileData> tiles, NativeArray<HotspotLayoutData> layouts,
                                   NativeArray<HotspotLink> links, ref FindState state)
        {
            state.Clear();
            if (!tiles.IsCreated || !layouts.IsCreated)
                return;
            var candidates = Candidates(brushes, layouts, state, out var firstSliver);
            if (candidates.Length < 2)
            {
                candidates.Dispose();
                return;
            }
            var root   = new NativeArray<int>(candidates.Length, Allocator.Temp);
            var unfold = new NativeArray<float4x4>(candidates.Length, Allocator.Temp);
            GroupCandidates(brushes, candidates.AsArray(), firstSliver, links, root, unfold);
            Compose(candidates.AsArray(), root, unfold, ref state);
            root.Dispose();
            unfold.Dispose();
            candidates.Dispose();
        }

        // The candidates in candidate order: the slivers last (from firstSliver on), so a group always has a surface that
        // shows its tile first
        static NativeList<int2> Candidates(NativeList<BrushData> brushes, NativeArray<HotspotLayoutData> layouts, in FindState state, out int firstSliver)
        {
            var candidates = new NativeList<int2>(64, Allocator.Temp);
            var slivers    = new NativeList<int2>(16, Allocator.Temp);
            var known      = state.kindStart.Length == brushes.Length + 1;
            for (int b = 0; b < brushes.Length; b++)
            {
                if (!brushes[b].brushRenderBuffer.IsCreated)
                    continue;
                ref var surfaces = ref brushes[b].brushRenderBuffer.Value.surfaces;
                for (int s = 0; s < surfaces.Length; s++)
                {
                    var kind = known ? state.kinds[state.kindStart[b] + s] : KindOf(ref surfaces[s], layouts);
                    if (kind == kSliver)
                        slivers.Add(new int2(b, s));
                    else if (kind == kCandidate)
                        candidates.Add(new int2(b, s));
                }
            }
            firstSliver = candidates.Length;
            candidates.AddRange(slivers.AsArray());
            slivers.Dispose();
            return candidates;
        }

        // Prepare, grouping again only the candidates the edits since the last update can have changed the groups of
        // (IncrementalHotspotGroups.md): its result is Prepare's. memory: what the last update found, kept with the tree.
        // keptBrushes: the brushes whose kinds and hashes are the last update's (HotspotStageMemory.keptKinds; none when not given)
        public static void PrepareIncremental(NativeList<BrushData> brushes, NativeArray<HotspotTileData> tiles, NativeArray<HotspotLayoutData> layouts,
                                              NativeArray<HotspotLink> links, ref FindState state, ref HotspotGroupMemory memory, NativeArray<bool> keptBrushes = default)
        {
            state.Clear();
            memory.lastFull.Value  = true;
            memory.lastDirty.Value = 0;
            if (!tiles.IsCreated || !layouts.IsCreated)
            {
                memory.Clear();
                return;
            }
            var candidates = Candidates(brushes, layouts, state, out var firstSliver);
            var count      = candidates.Length;
            var known      = state.kindStart.Length == brushes.Length + 1;
            var keys       = new NativeArray<SurfaceKey>(count, Allocator.Temp);
            var hashes     = new NativeArray<uint4>(count, Allocator.Temp);
            for (int c = 0; c < count; c++)
            {
                var at = candidates[c];
                keys[c] = new SurfaceKey { brush = brushes[at.x].brushIndexOrder.compactNodeID, surface = at.y };
                hashes[c] = known ? state.hashes[state.kindStart[at.x] + at.y]
                                  : SurfaceHash(ref brushes[at.x].brushRenderBuffer.Value.surfaces[at.y], c < firstSliver ? kCandidate : kSliver);
            }
            var inputs = GroupInputsKey(layouts, links);
            var root   = new NativeArray<int>(count, Allocator.Temp);
            var unfold = new NativeArray<float4x4>(count, Allocator.Temp);
            var indexOf  = new CandidateIndex(brushes, candidates.AsArray(), Allocator.Temp);
            var dirty    = new NativeArray<bool>(count, Allocator.Temp);
            var newCorners = new NativeArray<UnsafeList<float3>>(count, Allocator.Temp);
            var gone     = new NativeList<SurfaceKey>(16, Allocator.Temp);
            var full = !memory.valid.Value || !memory.inputs.Value.Equals(inputs) || indexOf.Count != count || !SameOrder(brushes, memory.order);
            if (!full)
            {
                GoneCandidates(brushes, keptBrushes, indexOf, ref memory, gone);
                full = !Regroup(brushes, candidates.AsArray(), firstSliver, links, keys, hashes, indexOf, keptBrushes, gone, dirty, newCorners, root, unfold, ref memory);
            }
            if (full)
            {
                for (int c = 0; c < count; c++)
                {
                    root[c] = c;
                    unfold[c] = float4x4.identity;
                    dirty[c] = true;
                }
                if (count >= 2)
                    GroupCandidates(brushes, candidates.AsArray(), firstSliver, links, root, unfold);
            }
            if (count >= 2)
                Compose(candidates.AsArray(), root, unfold, ref state);
            Remember(full, brushes, candidates.AsArray(), keys, hashes, gone, dirty, newCorners, root, unfold, inputs, ref memory);
            for (int c = 0; c < count; c++)
            {
                if (newCorners[c].IsCreated)
                    newCorners[c].Dispose();
            }
            newCorners.Dispose(); dirty.Dispose(); indexOf.Dispose(); gone.Dispose(); unfold.Dispose(); root.Dispose(); hashes.Dispose(); keys.Dispose();
            candidates.Dispose();
        }

        // The brushes kept from the last update are in the same order (what each group's lead and its tree's ties read)
        static bool SameOrder(NativeList<BrushData> brushes, NativeList<CompactNodeID> order)
        {
            var at = new UnsafeHashMap<CompactNodeID, int>(math.max(1, brushes.Length), Allocator.Temp);
            for (int b = 0; b < brushes.Length; b++)
                at.TryAdd(brushes[b].brushIndexOrder.compactNodeID, b);
            var last = -1;
            var same = true;
            for (int i = 0; i < order.Length && same; i++)
            {
                if (!at.TryGetValue(order[i], out var b))
                    continue;
                same = b > last;
                last = b;
            }
            at.Dispose();
            return same;
        }

        // A candidate's corners, each once
        static UnsafeList<float3> CornersOf(NativeList<BrushData> brushes, int2 at, Allocator allocator)
        {
            ref var vertices = ref brushes[at.x].brushRenderBuffer.Value.surfaces[at.y].colliderVertices;
            var corners = new UnsafeList<float3>(vertices.Length, allocator);
            var seen = new UnsafeHashSet<float3>(math.max(1, vertices.Length), Allocator.Temp);
            for (int v = 0; v < vertices.Length; v++)
            {
                var p = vertices[v] + 0.0f;
                if (seen.Add(p))
                    corners.Add(p);
            }
            seen.Dispose();
            return corners;
        }

        // The kept candidates that are not candidates any more: a kept brush has the candidates it had, so only the other
        // brushes' surfaces, and those of the brushes that are gone, are looked at
        static void GoneCandidates(NativeList<BrushData> brushes, NativeArray<bool> keptBrushes, in CandidateIndex indexOf, ref HotspotGroupMemory memory,
                                   NativeList<SurfaceKey> gone)
        {
            for (int b = 0; b < brushes.Length; b++)
            {
                if (keptBrushes.IsCreated && b < keptBrushes.Length && keptBrushes[b])
                    continue;
                GoneOf(brushes[b].brushIndexOrder.compactNodeID, indexOf, ref memory, gone);
            }
            for (int i = 0; i < memory.order.Length; i++)
            {
                if (!indexOf.HasBrush(memory.order[i]))
                    GoneOf(memory.order[i], indexOf, ref memory, gone);
            }
        }

        static void GoneOf(CompactNodeID brush, in CandidateIndex indexOf, ref HotspotGroupMemory memory, NativeList<SurfaceKey> gone)
        {
            if (!memory.surfaceCount.TryGetValue(brush, out var surfaces))
                return;
            for (int s = 0; s < surfaces; s++)
            {
                var key = new SurfaceKey { brush = brush, surface = s };
                if (memory.candidates.ContainsKey(key) && !indexOf.ContainsKey(key))
                    gone.Add(key);
            }
        }

        // The dirty candidates (changed, their neighbours, their kept groups, the neighbours of their slivers) grouped
        // again; the others as they were kept. False when that is not to be done (most of the model is dirty, or what is
        // kept does not hold together): everything is grouped again then.
        static bool Regroup(NativeList<BrushData> brushes, NativeArray<int2> candidates, int firstSliver, NativeArray<HotspotLink> links,
                            NativeArray<SurfaceKey> keys, NativeArray<uint4> hashes, in CandidateIndex indexOf, NativeArray<bool> keptBrushes, NativeList<SurfaceKey> gone,
                            NativeArray<bool> dirty, NativeArray<UnsafeList<float3>> newCorners, NativeArray<int> root, NativeArray<float4x4> unfold, ref HotspotGroupMemory memory)
        {
            var count  = candidates.Length;
            var kept   = memory.candidates;
            var index  = memory.cornerIndex;
            var groups = memory.members;
            var queue  = new NativeList<int>(64, Allocator.Temp);
            var changed = new NativeList<int>(16, Allocator.Temp);
            var candidateIndex = indexOf;
            try
            {
                void Mark(int c)
                {
                    if (dirty[c])
                        return;
                    dirty[c] = true;
                    queue.Add(c);
                }
                void MarkKey(SurfaceKey key)
                {
                    if (candidateIndex.TryGetValue(key, out var c))
                        Mark(c);
                }
                void MarkGroupOf(SurfaceKey key)
                {
                    if (!kept.TryGetValue(key, out var was))
                        return;
                    MarkKey(was.lead);
                    if (groups.TryGetFirstValue(was.lead, out var member, out var it))
                    {
                        do MarkKey(member);
                        while (groups.TryGetNextValue(out member, ref it));
                    }
                }
                void MarkAround(float3 corner)
                {
                    if (index.TryGetFirstValue(corner, out var key, out var it))
                    {
                        do MarkKey(key);
                        while (index.TryGetNextValue(out key, ref it));
                    }
                }

                // what changed: new, different (a kept brush's candidates are what they were); gone (GoneCandidates)
                for (int c = 0; c < count; c++)
                {
                    if (keptBrushes.IsCreated && keptBrushes[candidates[c].x])
                        continue;
                    if (!kept.TryGetValue(keys[c], out var was) || !was.hash.Equals(hashes[c]))
                        changed.Add(c);
                }
                if (changed.Length == 0 && gone.Length == 0)
                {
                    for (int c = 0; c < count; c++)
                    {
                        var was = kept[keys[c]];
                        if (!indexOf.TryGetValue(was.lead, out var lead))
                            return false;
                        root[c]   = lead;
                        unfold[c] = was.unfold;
                    }
                    memory.lastDirty.Value = 0;
                    memory.lastFull.Value  = false;
                    return true;
                }

                // their neighbours before, and their kept groups
                for (int i = 0; i < changed.Length; i++)
                {
                    var c = changed[i];
                    Mark(c);
                    MarkGroupOf(keys[c]);
                    if (kept.TryGetValue(keys[c], out var was))
                    {
                        for (int k = 0; k < was.corners.Length; k++)
                            MarkAround(was.corners[k]);
                    }
                }
                for (int i = 0; i < gone.Length; i++)
                {
                    MarkGroupOf(gone[i]);
                    var was = kept[gone[i]];
                    for (int k = 0; k < was.corners.Length; k++)
                        MarkAround(was.corners[k]);
                }

                // the corner index as it is now
                for (int i = 0; i < changed.Length; i++)
                {
                    var c = changed[i];
                    if (kept.TryGetValue(keys[c], out var was))
                    {
                        for (int k = 0; k < was.corners.Length; k++)
                            index.Remove(was.corners[k], keys[c]);
                    }
                }
                for (int i = 0; i < gone.Length; i++)
                {
                    var was = kept[gone[i]];
                    for (int k = 0; k < was.corners.Length; k++)
                        index.Remove(was.corners[k], gone[i]);
                }
                for (int i = 0; i < changed.Length; i++)
                {
                    var c = changed[i];
                    newCorners[c] = CornersOf(brushes, candidates[c], Allocator.Persistent);
                    var corners = newCorners[c];
                    for (int k = 0; k < corners.Length; k++)
                        index.Add(corners[k], keys[c]);
                }

                // their neighbours now
                for (int i = 0; i < changed.Length; i++)
                {
                    var corners = newCorners[changed[i]];
                    for (int k = 0; k < corners.Length; k++)
                        MarkAround(corners[k]);
                }

                // every dirty candidate's kept group, and a dirty sliver's neighbours now, until nothing is added
                for (int q = 0; q < queue.Length; q++)
                {
                    var c = queue[q];
                    MarkGroupOf(keys[c]);
                    if (c < firstSliver)
                        continue;
                    if (newCorners[c].IsCreated)
                    {
                        var corners = newCorners[c];
                        for (int k = 0; k < corners.Length; k++)
                            MarkAround(corners[k]);
                    } else
                    {
                        var corners = kept[keys[c]].corners;
                        for (int k = 0; k < corners.Length; k++)
                            MarkAround(corners[k]);
                    }
                }

                var dirtyCount = queue.Length;
                memory.lastDirty.Value = dirtyCount;
                if (dirtyCount * 2 > count)
                    return false;

                // the dirty candidates grouped by themselves, in their candidate order
                var local       = new NativeArray<int2>(dirtyCount, Allocator.Temp);
                var globalOf    = new NativeArray<int>(dirtyCount, Allocator.Temp);
                var localRoot   = new NativeArray<int>(dirtyCount, Allocator.Temp);
                var localUnfold = new NativeArray<float4x4>(dirtyCount, Allocator.Temp);
                var localFirstSliver = 0;
                var n = 0;
                for (int c = 0; c < count; c++)
                {
                    if (!dirty[c])
                        continue;
                    if (c < firstSliver)
                        localFirstSliver++;
                    local[n]    = candidates[c];
                    globalOf[n] = c;
                    n++;
                }
                if (n > 0)
                    GroupCandidates(brushes, local, localFirstSliver, links, localRoot, localUnfold);
                for (int i = 0; i < n; i++)
                {
                    root[globalOf[i]]   = globalOf[localRoot[i]];
                    unfold[globalOf[i]] = localUnfold[i];
                }
                local.Dispose(); globalOf.Dispose(); localRoot.Dispose(); localUnfold.Dispose();

                // the others as they were kept: a clean candidate's lead is clean (its kept group is clean)
                for (int c = 0; c < count; c++)
                {
                    if (dirty[c])
                        continue;
                    var was = kept[keys[c]];
                    if (!indexOf.TryGetValue(was.lead, out var lead) || dirty[lead])
                        return false;
                    root[c]   = lead;
                    unfold[c] = was.unfold;
                }
                memory.lastFull.Value = false;
                return true;
            }
            finally
            {
                queue.Dispose(); changed.Dispose();
            }
        }

        // What this update found, kept for the next: everything again (full), or the dirty candidates and the gone ones
        static void Remember(bool full, NativeList<BrushData> brushes, NativeArray<int2> candidates, NativeArray<SurfaceKey> keys, NativeArray<uint4> hashes,
                             NativeList<SurfaceKey> gone, NativeArray<bool> dirty, NativeArray<UnsafeList<float3>> newCorners, NativeArray<int> root, NativeArray<float4x4> unfold,
                             uint4 inputs, ref HotspotGroupMemory memory)
        {
            var count = candidates.Length;
            var size  = new NativeArray<int>(count, Allocator.Temp);
            for (int c = 0; c < count; c++)
                size[root[c]]++;
            if (full)
            {
                memory.Clear();
                for (int c = 0; c < count; c++)
                {
                    var corners = newCorners[c].IsCreated ? newCorners[c] : CornersOf(brushes, candidates[c], Allocator.Persistent);
                    newCorners[c] = default;
                    memory.candidates.TryAdd(keys[c], new KeptCandidate { hash = hashes[c], lead = keys[root[c]], unfold = unfold[c], corners = corners });
                    for (int k = 0; k < corners.Length; k++)
                        memory.cornerIndex.Add(corners[k], keys[c]);
                }
            } else
            {
                // the gone ones
                for (int i = 0; i < gone.Length; i++)
                {
                    var was = memory.candidates[gone[i]];
                    memory.members.Remove(was.lead);
                    if (was.corners.IsCreated)
                        was.corners.Dispose();
                    memory.candidates.Remove(gone[i]);
                }
                // the dirty ones' old groups (all of them dirty) forgotten, then everything they are now
                for (int c = 0; c < count; c++)
                {
                    if (dirty[c] && memory.candidates.TryGetValue(keys[c], out var was))
                        memory.members.Remove(was.lead);
                }
                for (int c = 0; c < count; c++)
                {
                    if (!dirty[c])
                        continue;
                    var entry = memory.candidates.TryGetValue(keys[c], out var was) ? was : default;
                    if (newCorners[c].IsCreated)
                    {
                        if (entry.corners.IsCreated)
                            entry.corners.Dispose();
                        entry.corners = newCorners[c];
                        newCorners[c] = default;
                    }
                    entry.hash   = hashes[c];
                    entry.lead   = keys[root[c]];
                    entry.unfold = unfold[c];
                    memory.candidates[keys[c]] = entry;
                }
            }
            for (int c = 0; c < count; c++)
            {
                if ((full || dirty[c]) && size[root[c]] >= 2)
                    memory.members.Add(keys[root[c]], keys[c]);
            }
            size.Dispose();
            memory.order.Clear();
            memory.surfaceCount.Clear();
            for (int b = 0; b < brushes.Length; b++)
            {
                memory.order.Add(brushes[b].brushIndexOrder.compactNodeID);
                memory.surfaceCount.TryAdd(brushes[b].brushIndexOrder.compactNodeID, brushes[b].brushRenderBuffer.IsCreated ? brushes[b].brushRenderBuffer.Value.surfaces.Length : 0);
            }
            memory.inputs.Value = inputs;
            memory.valid.Value  = true;
        }

        // The same result as Prepare's: false and the first difference when it is not
        public static bool SameAs(in FindState a, in FindState b, out FixedString512Bytes difference)
        {
            difference = default;
            if (a.candidates.Length != b.candidates.Length) { difference.Append((FixedString64Bytes)"candidates: "); difference.Append(a.candidates.Length); difference.Append((FixedString32Bytes)" vs "); difference.Append(b.candidates.Length); return false; }
            for (int c = 0; c < a.candidates.Length; c++)
            {
                if (!a.candidates[c].Equals(b.candidates[c])) { difference.Append((FixedString64Bytes)"candidate "); difference.Append(c); return false; }
                if (!math.all(math.asuint(a.unfold[c].c0) == math.asuint(b.unfold[c].c0)) || !math.all(math.asuint(a.unfold[c].c1) == math.asuint(b.unfold[c].c1)) ||
                    !math.all(math.asuint(a.unfold[c].c2) == math.asuint(b.unfold[c].c2)) || !math.all(math.asuint(a.unfold[c].c3) == math.asuint(b.unfold[c].c3)))
                { difference.Append((FixedString64Bytes)"unfold of candidate "); difference.Append(c); return false; }
                if (a.memberOf[c] != b.memberOf[c]) { difference.Append((FixedString64Bytes)"memberOf at "); difference.Append(c); return false; }
            }
            if (a.memberStart.Length != b.memberStart.Length) { difference.Append((FixedString64Bytes)"memberStart length"); return false; }
            for (int i = 0; i < a.memberStart.Length; i++)
            {
                if (a.memberStart[i] != b.memberStart[i]) { difference.Append((FixedString64Bytes)"memberStart at "); difference.Append(i); return false; }
            }
            if (a.leads.Length != b.leads.Length) { difference.Append((FixedString64Bytes)"leads: "); difference.Append(a.leads.Length); difference.Append((FixedString32Bytes)" vs "); difference.Append(b.leads.Length); return false; }
            for (int i = 0; i < a.leads.Length; i++)
            {
                if (a.leads[i] != b.leads[i]) { difference.Append((FixedString64Bytes)"lead "); difference.Append(i); return false; }
            }
            return true;
        }

        // Who is grouped with whom among these candidates (in candidate order, the slivers from firstSliver on): each one's
        // root (its group's smallest candidate), and how it is unfolded into its group's first plane. Reads nothing of a
        // candidate that is not among them.
        static void GroupCandidates(NativeList<BrushData> brushes, NativeArray<int2> candidates, int firstSliver, NativeArray<HotspotLink> links,
                          NativeArray<int> root, NativeArray<float4x4> unfold)
        {
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
            // each sliver's neighbour that shows a tile with the longest edge in common (candidate, the edge's length)
            var sliverTo = new NativeArray<float2>(candidates.Length - firstSliver, Allocator.Temp);
            for (int i = 0; i < sliverTo.Length; i++)
                sliverTo[i] = new float2(-1, 0);
            using (var sharedPairs = shared.GetKeyValueArrays(Allocator.Temp))
            {
                var pairs = sharedPairs.Keys;
                for (int i = 0; i < pairs.Length; i++)
                {
                    if (sharedPairs.Values[i] < 2)
                        continue;
                    var a = SourceOf(brushes, candidates[pairs[i].x]);
                    var b = SourceOf(brushes, candidates[pairs[i].y]);
                    var coplanar  = a.layout == b.layout && math.dot(a.normal, b.normal) >= kSameDirection;
                    // the same directions and the same units (a frame measures in its brush's own)
                    var sameFrame = SameAxis(a.right, b.right) && SameAxis(a.up, b.up);
                    var together  = coplanar && sameFrame;
                    var priority  = int.MaxValue;
                    var byHand    = linked.TryGetValue(pairs[i], out var link) && link.y != 0;
                    if (byHand)
                    {
                        together = a.layout == b.layout && link.y > 0;
                        priority = link.x;
                    }
                    // a sliver beside a surface that shows a tile: the longest edge they share counts
                    if (!together && !byHand && a.layout == b.layout && (pairs[i].x < firstSliver) != (pairs[i].y < firstSliver))
                    {
                        var sliver = math.max(pairs[i].x, pairs[i].y); var other = math.min(pairs[i].x, pairs[i].y);
                        var length = SharedEdge(brushes, candidates[sliver], candidates[other]);
                        var best = sliverTo[sliver - firstSliver];
                        if (length > best.y || (length == best.y && best.x >= 0 && other < best.x))
                            sliverTo[sliver - firstSliver] = new float2(other, length);
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
            // the slivers no surface that shows a tile took in: mapped with their neighbour
            for (int i = 0; i < sliverTo.Length; i++)
            {
                var sliver = firstSliver + i;
                var other  = (int)sliverTo[i].x;
                if (other < 0 || !(sliverTo[i].y > 0) || Root(sliver) < firstSliver)
                    continue;
                joined.Add(new int3(other, sliver, int.MaxValue));
                var ra = Root(other); var rb = Root(sliver);
                if (ra != rb)
                    parent[math.max(ra, rb)] = math.min(ra, rb);
            }
            sliverTo.Dispose();

            // each group of more than one: its members, matched as one surface on the first one's settings
            var size = new NativeArray<int>(candidates.Length, Allocator.Temp);
            for (int c = 0; c < candidates.Length; c++)
                size[Root(c)]++;

            // where a ring of connected surfaces closes, one edge keeps a seam: the oldest link's (the newest links have priority),
            // so the members are unfolded along the newest links that reach them all (a spanning tree)
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
            // the spanning tree's edges at each surface, in the order they were found
            var adjacencyStart = new NativeArray<int>(candidates.Length + 1, Allocator.Temp);
            for (int j = 0; j < tree.Length; j++)
            {
                adjacencyStart[tree[j].x + 1]++;
                adjacencyStart[tree[j].y + 1]++;
            }
            for (int c = 0; c < candidates.Length; c++)
                adjacencyStart[c + 1] += adjacencyStart[c];
            var adjacency = new NativeArray<int>(adjacencyStart[candidates.Length], Allocator.Temp);
            var adjacencyAt = new NativeArray<int>(candidates.Length, Allocator.Temp);
            for (int c = 0; c < candidates.Length; c++)
                adjacencyAt[c] = adjacencyStart[c];
            for (int j = 0; j < tree.Length; j++)
            {
                adjacency[adjacencyAt[tree[j].x]++] = tree[j].y;
                adjacency[adjacencyAt[tree[j].y]++] = tree[j].x;
            }
            adjacencyAt.Dispose();
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
                    for (int k = adjacencyStart[from]; k < adjacencyStart[from + 1]; k++)
                    {
                        var to = adjacency[k];
                        if (reached[to])
                            continue;
                        reached[to] = true;
                        queue.Add(to);
                        unfold[to] = math.mul(unfold[from], Hinge(brushes, candidates[from], candidates[to]));
                    }
                }
            }
            reached.Dispose();
            queue.Dispose();
            adjacency.Dispose();
            adjacencyStart.Dispose();
            tree.Dispose();
            joined.Dispose();
            for (int c = 0; c < candidates.Length; c++)
                root[c] = Root(c);
            size.Dispose();
            parent.Dispose();
        }

        // The state Match reads, from each candidate's root and unfold: the groups' members in candidate order, the leads
        // of the groups of two or more
        static void Compose(NativeArray<int2> candidates, NativeArray<int> root, NativeArray<float4x4> unfold, ref FindState state)
        {
            var size = new NativeArray<int>(candidates.Length, Allocator.Temp);
            for (int c = 0; c < candidates.Length; c++)
                size[root[c]]++;
            var memberStart = new NativeArray<int>(candidates.Length + 1, Allocator.Temp);
            for (int c = 0; c < candidates.Length; c++)
                memberStart[root[c] + 1]++;
            for (int c = 0; c < candidates.Length; c++)
                memberStart[c + 1] += memberStart[c];
            var memberAt = new NativeArray<int>(candidates.Length, Allocator.Temp);
            var memberOf = new NativeArray<int>(candidates.Length, Allocator.Temp);
            for (int c = 0; c < candidates.Length; c++)
                memberAt[c] = memberStart[c];
            for (int c = 0; c < candidates.Length; c++)
                memberOf[memberAt[root[c]]++] = c;
            memberAt.Dispose();
            for (int c = 0; c < candidates.Length; c++)
            {
                state.candidates.Add(candidates[c]);
                state.unfold.Add(unfold[c]);
                state.memberOf.Add(memberOf[c]);
            }
            for (int c = 0; c <= candidates.Length; c++)
                state.memberStart.Add(memberStart[c]);
            for (int lead = 0; lead < candidates.Length; lead++)
            {
                if (root[lead] == lead && size[lead] >= 2)
                    state.leads.Add(lead);
            }
            state.matches.Resize(state.leads.Length, NativeArrayOptions.ClearMemory);
            memberStart.Dispose();
            memberOf.Dispose();
            size.Dispose();
        }

        // One group matched as one surface on its first member's settings; each is independent of the others
        public static GroupMatch Match(int lead, NativeArray<BrushData> brushes, NativeArray<HotspotTileData> tiles, NativeArray<HotspotLayoutData> layouts,
                                       NativeArray<float4> tileSides, in UnsafeParallelMultiHashMap<int2, HotspotBorderCorner> bordersOf, in FindState state,
                                       NativeParallelHashMap<uint4, GroupMatchEntry> cache = default, uint4 inputs = default)
        {
            var result      = default(GroupMatch);
            var candidates  = state.candidates;
            var unfold      = state.unfold;
            if (cache.IsCreated)
            {
                result.key   = MatchKey(lead, brushes, bordersOf, state, inputs);
                result.keyed = true;
                if (cache.TryGetValue(result.key, out var entry))
                {
                    var kept = entry.match;
                    kept.owned = false;
                    return kept;
                }
            }
            result.owned = true;
            var members     = new NativeList<int>(16, Allocator.Temp);
            var positions   = new NativeList<float3>(64, Allocator.Temp);
            var indices     = new NativeList<int>(64, Allocator.Temp);
            var rules       = new NativeList<HotspotBorderCorner>(8, Allocator.Temp);
            var memberRules = new NativeList<HotspotBorderCorner>(8, Allocator.Temp);
            // where each corner was unfolded to first: the edge two members share is the same in both
            var placed      = new UnsafeHashMap<float3, float3>(64, Allocator.Temp);
            float3 Place(float3 p, float4x4 map)
            {
                p += 0.0f;
                var q = math.mul(map, new float4(p, 1)).xyz;
                if (placed.TryGetValue(p, out var first) && math.distancesq(first, q) <= kSamePoint * math.max(1, math.lengthsq(q)))
                    return first;
                placed.TryAdd(p, q);
                return q;
            }
            try
            {
                for (int k = state.memberStart[lead]; k < state.memberStart[lead + 1]; k++)
                {
                    var c = state.memberOf[k];
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
                var choice  = HotspotMatcher.Choose(tiles, layout, shape, source.seed, source.pinned, density: density, pinnedSheet: source.pinnedSheet);
                if (choice.tile < 0)
                    return result;
                var tile  = tiles[choice.tile];
                var sheetMaterial = 0UL;
                if (tile.sheet > 0)
                {
                    layout = HotspotMatcher.SheetOf(tile, layouts, layout);
                    sheetMaterial = layout.material;
                }
                var group = new Group
                {
                    uvMatrix = HotspotMatcher.Mapping(tile, layout, shape, choice, source.right, source.up),
                    right    = source.right,
                    up       = source.up,
                    layout   = source.layout - 1,
                    variant  = choice.variant,
                    sheetMaterial = sheetMaterial
                };
                if (tile.border > 0 || choice.Repeated || math.any(source.offset != 0))
                    group.input = HotspotSections.Input(tile, layout, shape, choice, source.right, source.up, density, source.offset, source.world);
                group.info = HotspotDiagnostics.Info(tile, layout, choice, group.input, source.pinned != 0 && tile.id == source.pinned && tile.sheetIndex == source.pinnedSheet, density);
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
                        result.hasSections = true;
                        result.lines      = new UnsafeList<HotspotSectionLine>(sections.lines.Length, Allocator.Persistent);
                        result.halfPlanes = new UnsafeList<HotspotSectionHalfPlane>(sections.halfPlanes.Length, Allocator.Persistent);
                        result.cells      = new UnsafeList<HotspotSectionCell>(sections.cells.Length, Allocator.Persistent);
                        for (int i = 0; i < sections.lines.Length; i++)      result.lines.Add(sections.lines[i]);
                        for (int i = 0; i < sections.halfPlanes.Length; i++) result.halfPlanes.Add(sections.halfPlanes[i]);
                        for (int i = 0; i < sections.cells.Length; i++)      result.cells.Add(sections.cells[i]);
                    }
                    sections.Dispose();
                }
                result.group = group;
                result.valid = true;
                return result;
            }
            finally
            {
                members.Dispose(); positions.Dispose(); indices.Dispose(); rules.Dispose(); memberRules.Dispose(); placed.Dispose();
            }
        }

        // Everything Match reads of a group: its first member's settings, and each member's corners, triangles, unfold and
        // borders given by hand; inputs: what every match reads (InputsKey)
        static unsafe uint4 MatchKey(int lead, NativeArray<BrushData> brushes, in UnsafeParallelMultiHashMap<int2, HotspotBorderCorner> bordersOf,
                                     in FindState state, uint4 inputs)
        {
            var buffer = new UnsafeAppendBuffer(1024, 16, Allocator.Temp);
            var rules  = new NativeList<HotspotBorderCorner>(8, Allocator.Temp);
            try
            {
                buffer.Add(inputs);
                var source = SourceOf(brushes, state.candidates[lead]);
                buffer.Add(source.layout); buffer.Add(source.right); buffer.Add(source.up); buffer.Add(source.normal);
                buffer.Add(source.seed); buffer.Add(source.pinned); buffer.Add(source.pinnedSheet); buffer.Add(source.density);
                buffer.Add(source.offset); buffer.Add(source.world ? 1 : 0);
                for (int k = state.memberStart[lead]; k < state.memberStart[lead + 1]; k++)
                {
                    var c = state.memberOf[k];
                    var at = state.candidates[c];
                    ref var surface = ref brushes[at.x].brushRenderBuffer.Value.surfaces[at.y];
                    buffer.Add(state.unfold[c]);
                    buffer.Add(surface.colliderVertices.Length);
                    buffer.Add(surface.colliderVertices.GetUnsafePtr(), surface.colliderVertices.Length * UnsafeUtility.SizeOf<float3>());
                    buffer.Add(surface.indices.Length);
                    buffer.Add(surface.indices.GetUnsafePtr(), surface.indices.Length * sizeof(int));
                    BordersOf(at, bordersOf, rules);
                    buffer.Add(rules.Length);
                    for (int i = 0; i < rules.Length; i++)
                    {
                        buffer.Add(rules[i].position); buffer.Add(rules[i].end); buffer.Add(rules[i].segment ? 1 : 0);
                        buffer.Add(rules[i].rule); buffer.Add(rules[i].border);
                    }
                }
                return xxHash3.Hash128(buffer.Ptr, buffer.Length);
            }
            finally
            {
                rules.Dispose();
                buffer.Dispose();
            }
        }

        // What every group's match reads: the tiles, the layouts and the shaped tiles' sides
        public static unsafe uint4 InputsKey(NativeArray<HotspotTileData> tiles, NativeArray<HotspotLayoutData> layouts, NativeArray<float4> tileSides)
        {
            var parts = stackalloc uint4[3];
            parts[0] = tiles.IsCreated && tiles.Length > 0         ? xxHash3.Hash128(tiles.GetUnsafeReadOnlyPtr(), (long)tiles.Length * UnsafeUtility.SizeOf<HotspotTileData>()) : default;
            parts[1] = layouts.IsCreated && layouts.Length > 0     ? xxHash3.Hash128(layouts.GetUnsafeReadOnlyPtr(), (long)layouts.Length * UnsafeUtility.SizeOf<HotspotLayoutData>()) : default;
            parts[2] = tileSides.IsCreated && tileSides.Length > 0 ? xxHash3.Hash128(tileSides.GetUnsafeReadOnlyPtr(), (long)tileSides.Length * UnsafeUtility.SizeOf<float4>()) : default;
            return xxHash3.Hash128(parts, 3 * 16);
        }

        // The matched groups in the model's order, their sections in the flat lists, and which group each member is in
        public static void Assemble(ref FindState state, ref UnsafeHashMap<int2, int> groupOf, ref UnsafeHashMap<int2, float4x4> unfoldOf, NativeList<Group> groups,
                                    NativeList<HotspotSectionLine> lines, NativeList<HotspotSectionHalfPlane> halfPlanes, NativeList<HotspotSectionCell> cells,
                                    NativeParallelHashMap<uint4, GroupMatchEntry> cache = default)
        {
            groupOf.Clear();
            unfoldOf.Clear();
            groups.Clear();
            for (int i = 0; i < state.matches.Length; i++)
            {
                var match = state.matches[i];
                if (!match.valid)
                    continue;
                var group = match.group;
                if (match.hasSections)
                {
                    group.sectionStart = new int3(lines.Length, halfPlanes.Length, cells.Length);
                    group.sectionCount = new int3(match.lines.Length, match.halfPlanes.Length, match.cells.Length);
                    unsafe
                    {
                        lines.AddRange(match.lines.Ptr, match.lines.Length);
                        halfPlanes.AddRange(match.halfPlanes.Ptr, match.halfPlanes.Length);
                        cells.AddRange(match.cells.Ptr, match.cells.Length);
                    }
                }
                var index = groups.Length;
                groups.Add(group);
                var lead = state.leads[i];
                for (int k = state.memberStart[lead]; k < state.memberStart[lead + 1]; k++)
                {
                    var c = state.memberOf[k];
                    groupOf.TryAdd(state.candidates[c], index);
                    if (!state.unfold[c].Equals(float4x4.identity))
                        unfoldOf.TryAdd(state.candidates[c], state.unfold[c]);
                }
            }
            if (!cache.IsCreated)
                return;
            // the new matches go to the cache, which then owns their sections
            for (int i = 0; i < state.matches.Length; i++)
            {
                var match = state.matches[i];
                if (!match.keyed)
                    continue;
                if (cache.TryGetValue(match.key, out var entry))
                {
                    entry.used = true;
                    cache[match.key] = entry;
                    continue;
                }
                if (!match.owned)
                    continue;
                var stored = match;
                stored.owned = false;
                cache.TryAdd(match.key, new GroupMatchEntry { match = stored, used = true });
                match.owned = false;
                state.matches[i] = match;
            }
            // and forgets the matches no group used
            using var gone = new NativeList<uint4>(Allocator.Temp);
            foreach (var pair in cache)
            {
                if (!pair.Value.used)
                    gone.Add(pair.Key);
            }
            for (int i = 0; i < gone.Length; i++)
            {
                if (cache.TryGetValue(gone[i], out var dead))
                    dead.match.Dispose();
                cache.Remove(gone[i]);
            }
            using var left = cache.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < left.Length; i++)
            {
                var entry = cache[left[i]];
                entry.used = false;
                cache[left[i]] = entry;
            }
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
            sections.texel           = group.input.pixelsPerUnit > 0 ? 1.0 / group.input.pixelsPerUnit : 0;
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

        // How long the edge two surfaces share is: the two shared corners furthest apart (0: none)
        static float SharedEdge(NativeList<BrushData> brushes, int2 a, int2 b)
        {
            ref var first  = ref brushes[a.x].brushRenderBuffer.Value.surfaces[a.y];
            ref var second = ref brushes[b.x].brushRenderBuffer.Value.surfaces[b.y];
            var corners = new UnsafeHashSet<float3>(first.colliderVertices.Length, Allocator.Temp);
            for (int v = 0; v < first.colliderVertices.Length; v++)
                corners.Add(first.colliderVertices[v] + 0.0f);
            var longest = 0f;
            for (int i = 0; i < second.colliderVertices.Length; i++)
            {
                var p = second.colliderVertices[i] + 0.0f;
                if (!corners.Contains(p))
                    continue;
                for (int k = i + 1; k < second.colliderVertices.Length; k++)
                {
                    var r = second.colliderVertices[k] + 0.0f;
                    if (corners.Contains(r))
                        longest = math.max(longest, math.distance(p, r));
                }
            }
            corners.Dispose();
            return longest;
        }

        // A surface's narrowest width in its plane: of every direction two of its corners give, the least extent across it
        // (the narrowest width of a convex outline lies across one of its edges)
        static float Width(ref BlobArray<float3> corners, in HotspotSurfaceSource source)
        {
            var right = math.normalizesafe(source.right); var up = math.normalizesafe(source.up);
            var count = corners.Length;
            if (count < 3)
                return 0;
            var points = new NativeArray<float2>(count, Allocator.Temp);
            for (int v = 0; v < count; v++)
                points[v] = new float2(math.dot(corners[v], right), math.dot(corners[v], up));
            // the narrowest direction lies along an edge of the corners' hull, and across it the farthest corner of the hull
            // only moves on as the edges go round (rotating calipers)
            var hull = Hull(points);
            var h = hull.Length;
            var width = float.PositiveInfinity;
            if (h == 2)
                width = 0;
            else if (h > 2)
            {
                var far = 1;
                for (int e = 0; e < h && width > 0; e++)
                {
                    var i = hull[e]; var j = hull[(e + 1) % h];
                    var d = points[j] - points[i];
                    var length = math.length(d);
                    if (!(length > 0))
                        continue;
                    d /= length;
                    float Across(int k) => d.x * (points[hull[k]].y - points[i].y) - d.y * (points[hull[k]].x - points[i].x);
                    for (int step = 0; step < h && Across((far + 1) % h) >= Across(far); step++)
                        far = (far + 1) % h;
                    width = math.min(width, Across(far));
                }
            }
            hull.Dispose();
            points.Dispose();
            return float.IsPositiveInfinity(width) ? 0 : width;
        }

        struct ByXY : System.Collections.Generic.IComparer<int>
        {
            public NativeArray<float2> points;
            public readonly int Compare(int a, int b)
            {
                var p = points[a]; var q = points[b];
                return p.x != q.x ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y);
            }
        }

        // The corners of the points' convex hull, in order (Andrew's monotone chain)
        static NativeList<int> Hull(NativeArray<float2> points)
        {
            var order = new NativeArray<int>(points.Length, Allocator.Temp);
            for (int i = 0; i < order.Length; i++)
                order[i] = i;
            order.Sort(new ByXY { points = points });
            var hull = new NativeList<int>(points.Length + 1, Allocator.Temp);
            float Cross(int o, int a, int b) => (points[a].x - points[o].x) * (points[b].y - points[o].y) - (points[a].y - points[o].y) * (points[b].x - points[o].x);
            for (int k = 0; k < order.Length; k++)
            {
                while (hull.Length >= 2 && Cross(hull[hull.Length - 2], hull[hull.Length - 1], order[k]) <= 0)
                    hull.RemoveAt(hull.Length - 1);
                hull.Add(order[k]);
            }
            var lower = hull.Length + 1;
            for (int k = order.Length - 2; k >= 0; k--)
            {
                while (hull.Length >= lower && Cross(hull[hull.Length - 2], hull[hull.Length - 1], order[k]) <= 0)
                    hull.RemoveAt(hull.Length - 1);
                hull.Add(order[k]);
            }
            if (hull.Length > 1)
                hull.RemoveAt(hull.Length - 1);
            order.Dispose();
            return hull;
        }

        static bool SameAxis(float3 a, float3 b)
        {
            var la = math.length(a); var lb = math.length(b);
            return la > 0 && lb > 0 && math.dot(a, b) >= kSameDirection * la * lb && math.abs(la - lb) <= (1 - kSameDirection) * la;
        }

        static HotspotSurfaceSource SourceOf(NativeList<BrushData> brushes, int2 at)
        {
            return brushes[at.x].brushRenderBuffer.Value.surfaces[at.y].hotspotSource;
        }

        static HotspotSurfaceSource SourceOf(NativeArray<BrushData> brushes, int2 at)
        {
            return brushes[at.x].brushRenderBuffer.Value.surfaces[at.y].hotspotSource;
        }
    }
}
