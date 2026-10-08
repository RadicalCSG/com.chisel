using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    // After the model's weld: the surfaces with hotspot sections are cut where their sections meet and given their uvs.
    // Only what is drawn is cut; the collider keeps the welded triangles (ChiselSurfaceRenderBuffer.physicsIndices).
    internal static class OutputHotspotSections
    {
        public static void CutModel(NativeList<BrushData> brushes, NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patched,
                                    NativeArray<HotspotTileData> tiles, NativeArray<HotspotLayoutData> layouts, NativeArray<HotspotLink> links)
        {
            // coplanar neighbours, of any brush, mapped as one surface (HotspotGroups)
            var groupOf    = new UnsafeHashMap<int2, int>(64, Allocator.Temp);
            var unfoldOf   = new UnsafeHashMap<int2, float4x4>(8, Allocator.Temp);
            var groups     = new NativeList<HotspotGroups.Group>(8, Allocator.Temp);
            var lines      = new NativeList<HotspotSectionLine>(32, Allocator.Temp);
            var halfPlanes = new NativeList<HotspotSectionHalfPlane>(64, Allocator.Temp);
            var cells      = new NativeList<HotspotSectionCell>(32, Allocator.Temp);
            // and the edges given a border or none by hand
            var bordersOf  = new UnsafeParallelMultiHashMap<int2, HotspotBorderCorner>(8, Allocator.Temp);
            HotspotGroups.Borders(brushes, links, ref bordersOf);
            HotspotGroups.Find(brushes, tiles, layouts, links, bordersOf, ref groupOf, ref unfoldOf, groups, lines, halfPlanes, cells);
            for (int b = 0; b < brushes.Length; b++)
            {
                if (!brushes[b].brushRenderBuffer.IsCreated ||
                    (!HasSections(ref brushes[b].brushRenderBuffer.Value) && !InGroup(ref brushes[b].brushRenderBuffer.Value, b, groupOf)))
                    continue;
                var copy = Cut(brushes[b].brushRenderBuffer, b, groupOf, unfoldOf, bordersOf, groups, lines, halfPlanes, cells);
                patched.Add(copy);
                var data = brushes[b];
                data.brushRenderBuffer = copy;
                brushes[b] = data;
            }
            groupOf.Dispose(); unfoldOf.Dispose(); bordersOf.Dispose(); groups.Dispose(); lines.Dispose(); halfPlanes.Dispose(); cells.Dispose();
        }

        static bool InGroup(ref ChiselBrushRenderBuffer buffer, int brush, UnsafeHashMap<int2, int> groupOf)
        {
            for (int s = 0; s < buffer.surfaces.Length; s++)
            {
                if (groupOf.ContainsKey(new int2(brush, s)))
                    return true;
            }
            return false;
        }

        static bool HasSections(ref ChiselBrushRenderBuffer buffer)
        {
            for (int s = 0; s < buffer.surfaces.Length; s++)
            {
                if (buffer.surfaces[s].sectionInput.count > 0 && buffer.surfaces[s].indices.Length > 0)
                    return true;
            }
            return false;
        }

        static BlobAssetReference<ChiselBrushRenderBuffer> Cut(BlobAssetReference<ChiselBrushRenderBuffer> source, int brush, UnsafeHashMap<int2, int> groupOf,
                                                               UnsafeHashMap<int2, float4x4> unfoldOf, in UnsafeParallelMultiHashMap<int2, HotspotBorderCorner> bordersOf,
                                                               NativeList<HotspotGroups.Group> groups, NativeList<HotspotSectionLine> groupLines,
                                                               NativeList<HotspotSectionHalfPlane> groupHalfPlanes, NativeList<HotspotSectionCell> groupCells)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var original = ref source.Value;
                ref var root     = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
                var surfaces     = builder.Allocate(ref root.surfaces, original.surfaces.Length);
                for (int s = 0; s < original.surfaces.Length; s++)
                {
                    ref var from = ref original.surfaces[s];
                    ref var to   = ref surfaces[s];
                    to.surfaceIndex          = from.surfaceIndex;
                    to.baseSurfaceIndex      = from.baseSurfaceIndex;
                    to.decalEntityID         = from.decalEntityID;
                    to.destinationFlags      = from.destinationFlags;
                    to.destinationParameters = from.destinationParameters;
                    to.outputFlags           = from.outputFlags;
                    if (from.indices.Length > 0 && groupOf.TryGetValue(new int2(brush, s), out var g))
                    {
                        var unfolded = unfoldOf.TryGetValue(new int2(brush, s), out var unfold);
                        MapInGroup(builder, ref from, ref to, groups[g], g, unfolded ? unfold : float4x4.identity, unfolded, groupLines, groupHalfPlanes, groupCells);
                        continue;
                    }
                    if (from.sectionInput.count == 0 || from.indices.Length == 0)
                    {
                        to.CopyGeometry(builder, ref from);
                        to.CopyHotspot(builder, ref from);
                        continue;
                    }

                    using var indices  = ToList(ref from.indices);
                    using var collider = ToList(ref from.colliderVertices);
                    using var select   = ToList(ref from.selectVertices);
                    using var render   = ToList(ref from.renderVertices);
                    var input    = from.sectionInput;
                    var sections = HotspotSections.Create(Allocator.Temp);
                    using var rules = new NativeList<HotspotBorderCorner>(4, Allocator.Temp);
                    HotspotGroups.BordersOf(new int2(brush, s), bordersOf, rules);
                    // around what the surface draws (its border, its interior), unless it is a triangle (exact already) or
                    // repeats
                    var built = input.count == 4 && !input.Repeated
                              ? sections.BuildConforming(input, indices, collider, rules.AsArray())
                              : sections.Build(input);
                    if (!built)
                    {
                        sections.Dispose();
                        to.CopyGeometry(builder, ref from);
                        to.CopyHotspot(builder, ref from);
                        continue;
                    }
                    var right  = input.right;
                    var up     = input.up;
                    var normal = math.normalizesafe(math.cross(up, right));
                    // cut once where the sections meet; triangle by triangle when that would not cover it once
                    if (!sections.CutOnce(right, up, normal, indices, collider, select, render))
                        sections.Split(right, up, normal, indices, collider, select, render);
                    var repeats = InteriorRepeats(sections, input, right, up, normal, indices, collider, select, render);
                    sections.AssignUVs(indices, collider, select, render, right, up, repeats);
                    repeats.Dispose();
                    sections.Dispose();
                    // a surface whose cut went wrong is drawn uncut: one bad point would hide the whole model (its
                    // bounds)
                    if (!Finite(collider, render))
                    {
                        to.CopyGeometry(builder, ref from);
                        to.CopyHotspot(builder, ref from);
                        continue;
                    }
                    to.Store(builder, indices, collider, select, render);
                    // the collider keeps what the weld left, uncut
                    builder.Construct(ref to.physicsIndices,  ref from.indices);
                    builder.Construct(ref to.physicsVertices, ref from.colliderVertices);
                    to.CopySections(builder, ref from);
                }

                // The mesh queries' lists, with the cut surfaces' counts and hashes
                var queries = builder.Allocate(ref root.querySurfaces, original.querySurfaces.Length);
                using var list = new NativeList<ChiselQuerySurface>(Allocator.Temp);
                for (int q = 0; q < original.querySurfaces.Length; q++)
                {
                    ref var from = ref original.querySurfaces[q];
                    list.Clear();
                    for (int e = 0; e < from.surfaces.Length; e++)
                    {
                        var entry = from.surfaces[e];
                        ref var stored = ref surfaces[entry.surfaceIndex];
                        if (stored.vertexCount == 0 || stored.indexCount == 0)
                            continue;
                        entry.vertexCount       = stored.vertexCount;
                        entry.indexCount        = stored.indexCount;
                        entry.geometryHashValue = stored.geometryHashValue;
                        entry.surfaceHashValue  = stored.surfaceHashValue;
                        list.Add(entry);
                    }
                    queries[q].brushNodeID = from.brushNodeID;
                    builder.Construct(ref queries[q].surfaces, list);
                }
                root.surfaceOffset = original.surfaceOffset;
                root.surfaceCount  = original.surfaceCount;
                return builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        // A surface mapped with its group: cut with the group's sections (built from what the group draws together), or
        // given the group's tile as it is; one not in the group's first plane is unfolded into it for that (unfold), and
        // folded back, its own corners exactly where they were
        static void MapInGroup(BlobBuilder builder, ref ChiselSurfaceRenderBuffer from, ref ChiselSurfaceRenderBuffer to, in HotspotGroups.Group group, int groupIndex,
                               float4x4 unfold, bool unfolded,
                               NativeList<HotspotSectionLine> lines, NativeList<HotspotSectionHalfPlane> halfPlanes, NativeList<HotspotSectionCell> cells)
        {
            var indices  = ToList(ref from.indices);
            var collider = ToList(ref from.colliderVertices);
            var select   = ToList(ref from.selectVertices);
            var render   = ToList(ref from.renderVertices);
            var original = new UnsafeHashMap<float3, float3>(unfolded ? collider.Length : 1, Allocator.Temp);
            if (unfolded)
            {
                for (int v = 0; v < collider.Length; v++)
                {
                    var p = math.mul(unfold, new float4(collider[v], 1)).xyz;
                    original.TryAdd(p, collider[v]);
                    collider[v] = p;
                    var s = select[v]; s.position = p; select[v] = s;
                    var r = render[v]; r.position = p; render[v] = r;
                }
            }
            if (group.sectionCount.z > 0)
            {
                var sections = HotspotGroups.Sections(group, lines, halfPlanes, cells, Allocator.Temp);
                var normal = math.normalizesafe(math.cross(group.up, group.right));
                if (!sections.CutOnce(group.right, group.up, normal, indices, collider, select, render))
                    sections.Split(group.right, group.up, normal, indices, collider, select, render);
                var repeats = InteriorRepeats(sections, group.input, group.right, group.up, normal, indices, collider, select, render);
                sections.AssignUVs(indices, collider, select, render, group.right, group.up, repeats);
                repeats.Dispose();
                sections.Dispose();
            } else
                MeshAlgorithms.ComputeUVs(render, group.uvMatrix);
            if (unfolded)
            {
                var back = math.inverse(unfold);
                for (int v = 0; v < collider.Length; v++)
                {
                    var p = original.TryGetValue(collider[v], out var exact) ? exact : math.mul(back, new float4(collider[v], 1)).xyz;
                    collider[v] = p;
                    var s = select[v]; s.position = p; select[v] = s;
                    var r = render[v]; r.position = p; render[v] = r;
                }
            }
            original.Dispose();
            MeshAlgorithms.ComputeTangents(indices, render);
            if (!Finite(collider, render))
            {
                to.CopyGeometry(builder, ref from);
                to.CopyHotspot(builder, ref from);
            } else
            {
                to.Store(builder, indices, collider, select, render);
                builder.Construct(ref to.physicsIndices,  ref from.indices);
                builder.Construct(ref to.physicsVertices, ref from.colliderVertices);
                to.CopySections(builder, ref from);
                to.sectionInput = group.input;
                to.hotspot      = group.info;
                to.hotspot.group = groupIndex + 1;
            }
            indices.Dispose(); collider.Dispose(); select.Dispose(); render.Dispose();
        }

        // A bordered tile's interior repeated at the tile's density (BuildInteriorRepeats): what is drawn cut again, triangle
        // by triangle, where the repeats meet (the border's strips too, so no edge has a point on one side only)
        static HotspotSections InteriorRepeats(in HotspotSections sections, in HotspotSectionInput input, float3 right, float3 up, float3 normal,
                                               NativeList<int> indices, NativeList<float3> collider, NativeList<SelectVertex> select, NativeList<RenderVertex> render)
        {
            var repeats = HotspotSections.Create(Allocator.Temp);
            if (sections.interiorCell < 0 || !repeats.BuildInteriorRepeats(input, sections.openSides, sections.interiorToEdges))
            {
                repeats.cells.Clear();
                return repeats;
            }
            if (repeats.cells.Length > 1 && !repeats.CutOnce(right, up, normal, indices, collider, select, render, asTriangles: true))
                repeats.Split(right, up, normal, indices, collider, select, render);
            return repeats;
        }

        static bool Finite(NativeList<float3> positions, NativeList<RenderVertex> render)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                if (!math.all(math.isfinite(positions[i])))
                    return false;
            }
            for (int i = 0; i < render.Length; i++)
            {
                if (!math.all(math.isfinite(render[i].position)) || !math.all(math.isfinite(render[i].uv0)))
                    return false;
            }
            return true;
        }

        static NativeList<T> ToList<T>(ref BlobArray<T> array) where T : unmanaged
        {
            var list = new NativeList<T>(array.Length, Allocator.Temp);
            for (int i = 0; i < array.Length; i++)
                list.Add(array[i]);
            return list;
        }
    }
}
