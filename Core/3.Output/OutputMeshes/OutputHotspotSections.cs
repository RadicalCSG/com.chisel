using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    // After the model's weld: the surfaces with hotspot sections are cut where their sections meet and given their uvs.
    // Only what is drawn is cut; the collider keeps the welded triangles (ChiselSurfaceRenderBuffer.physicsIndices).
    internal static class OutputHotspotSections
    {
        public static void CutModel(NativeList<BrushData> brushes, NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patched)
        {
            for (int b = 0; b < brushes.Length; b++)
            {
                if (!brushes[b].brushRenderBuffer.IsCreated || !HasSections(ref brushes[b].brushRenderBuffer.Value))
                    continue;
                var copy = Cut(brushes[b].brushRenderBuffer);
                patched.Add(copy);
                var data = brushes[b];
                data.brushRenderBuffer = copy;
                brushes[b] = data;
            }
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

        static BlobAssetReference<ChiselBrushRenderBuffer> Cut(BlobAssetReference<ChiselBrushRenderBuffer> source)
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
                    // around what the surface draws (its border, its interior), unless it is a triangle (exact already) or
                    // repeats
                    var built = input.count == 4 && !input.Repeated
                              ? sections.BuildConforming(input, indices, collider)
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
                    sections.Split(right, up, normal, indices, collider, select, render);
                    if (sections.Conforms)
                    {
                        sections.conformSize = input.textureSize;
                        HotspotSections.Subdivide(sections.SubdivisionLevels(indices, collider, right, up), indices, collider, select, render);
                        using var interiorUVs = sections.SolveInterior(indices, collider, right, up, Allocator.Temp);
                        sections.AssignUVs(indices, collider, select, render, right, up, interiorUVs);
                        MeshAlgorithms.ComputeTangents(indices, render);
                    } else
                        sections.AssignUVs(indices, collider, select, render, right, up);
                    sections.Dispose();
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

        static NativeList<T> ToList<T>(ref BlobArray<T> array) where T : unmanaged
        {
            var list = new NativeList<T>(array.Length, Allocator.Temp);
            for (int i = 0; i < array.Length; i++)
                list.Add(array[i]);
            return list;
        }
    }
}
