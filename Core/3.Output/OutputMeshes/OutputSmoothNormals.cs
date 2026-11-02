using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    // The last step of the output, after the cleanup: the vertices of the surfaces that are smoothed (a smoothing group,
    // or the model's smoothing angle) that are at one position, and smoothed together, get one normal. A surface's normals
    // are as smoothing at its brush polygon's corners made them, in between interpolated, so however a surface is cut its
    // shading stays; but where the CSG cut two surfaces differently, a corner of one can lie on the other between its
    // corners, and their normals there differ. Each takes the average of the different normals there of every vertex it
    // is smoothed with: of the same smoothing group and entity (a generator's brushes), or within the model's smoothing
    // angle of it. A vertex with nothing else at its position keeps its normal.
    // A surface's back (SurfaceOutputFlags.DoubleSided) faces away from its front, so it is never smoothed with it.
    internal static class OutputSmoothNormals
    {
        // Positions this close (in metres) are one
        const float kWeldDistance = 1e-3f;

        struct Corner
        {
            public float3 position;
            public float3 normal;
            public float3 surfaceNormal;
            public ulong  entity;
            public uint   group;
            public int    brush;
            public int    surface;
        }

        static int3 KeyOf(float3 position) => (int3)math.round(position / kWeldDistance);

        static bool Drawn(ref ChiselSurfaceRenderBuffer surface) => (surface.destinationFlags & SurfaceDestinationFlags.Renderable) != 0;

        // Whether a surface's normals are made here
        static bool Smoothed(ref ChiselSurfaceRenderBuffer surface, bool byAngle)
        {
            return Drawn(ref surface) && surface.indices.Length >= 3 && (surface.smoothingGroup != 0 || byAngle);
        }

        // The surface's normal as it is drawn (zero when it has no area)
        static float3 SurfaceNormal(ref ChiselSurfaceRenderBuffer surface)
        {
            var sum = float3.zero;
            ref var indices  = ref surface.indices;
            ref var vertices = ref surface.renderVertices;
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                var a = vertices[indices[i]].position; var b = vertices[indices[i + 1]].position; var c = vertices[indices[i + 2]].position;
                sum += math.cross(b - a, c - a);
            }
            // the normals it was given say which way it faces
            var given = float3.zero;
            for (int v = 0; v < vertices.Length; v++)
                given += vertices[v].normal;
            var normal = math.normalizesafe(sum);
            return math.dot(normal, given) < 0 ? -normal : normal;
        }


        // The brushes whose normals changed get a buffer with the new ones (in patched as well, to be disposed of)
        public static void Smooth(NativeList<BrushData> brushes, NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patched, float smoothingAngle)
        {
            var byAngle     = smoothingAngle > 0.0001f;
            var angleCos    = byAngle ? math.cos(math.radians(smoothingAngle)) : 2.0f;
            var count       = 0;
            for (int b = 0; b < brushes.Length; b++)
            {
                var buffer = brushes[b].brushRenderBuffer;
                if (!buffer.IsCreated)
                    continue;
                ref var surfaces = ref buffer.Value.surfaces;
                for (int s = 0; s < surfaces.Length; s++)
                {
                    if (Smoothed(ref surfaces[s], byAngle))
                        count += surfaces[s].indices.Length;
                }
            }
            if (count == 0)
                return;

            // every vertex of every drawn surface, by its position
            using var corners = new NativeParallelMultiHashMap<int3, Corner>(count, Allocator.Temp);
            using var normals = new NativeList<float3>(16, Allocator.Temp);
            for (int b = 0; b < brushes.Length; b++)
            {
                var buffer = brushes[b].brushRenderBuffer;
                if (!buffer.IsCreated)
                    continue;
                ref var root     = ref buffer.Value;
                ref var surfaces = ref root.surfaces;
                for (int s = 0; s < surfaces.Length; s++)
                {
                    ref var surface = ref surfaces[s];
                    if (!Drawn(ref surface) || surface.indices.Length < 3)
                        continue;
                    var surfaceNormal = SurfaceNormal(ref surface);
                    if (math.all(surfaceNormal == 0))
                        continue;
                    ref var vertices = ref surface.renderVertices;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        corners.Add(KeyOf(vertices[v].position), new Corner
                        {
                            position = vertices[v].position, normal = vertices[v].normal, surfaceNormal = surfaceNormal,
                            entity = root.entityID, group = surface.smoothingGroup, brush = b, surface = s
                        });
                    }
                }
            }

            using var render = new NativeList<RenderVertex>(Allocator.Temp);
            using var changedSurfaces = new NativeList<int>(Allocator.Temp);
            using var newVertices = new NativeList<RenderVertex>(Allocator.Temp);
            using var newStarts = new NativeList<int>(Allocator.Temp);
            for (int b = 0; b < brushes.Length; b++)
            {
                var buffer = brushes[b].brushRenderBuffer;
                if (!buffer.IsCreated)
                    continue;
                ref var root     = ref buffer.Value;
                ref var surfaces = ref root.surfaces;
                changedSurfaces.Clear();
                newVertices.Clear();
                newStarts.Clear();
                for (int s = 0; s < surfaces.Length; s++)
                {
                    ref var surface = ref surfaces[s];
                    if (!Smoothed(ref surface, byAngle))
                        continue;
                    var surfaceNormal = SurfaceNormal(ref surface);
                    if (math.all(surfaceNormal == 0))
                        continue;
                    ref var vertices = ref surface.renderVertices;
                    var start = newVertices.Length;
                    var any = false;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        var vertex = vertices[v];
                        normals.Clear();
                        normals.Add(vertex.normal);
                        var cell = KeyOf(vertex.position);
                        for (int z = -1; z <= 1; z++)
                        for (int y = -1; y <= 1; y++)
                        for (int x = -1; x <= 1; x++)
                        {
                            if (!corners.TryGetFirstValue(cell + new int3(x, y, z), out var corner, out var iterator))
                                continue;
                            do
                            {
                                if (math.distancesq(corner.position, vertex.position) > kWeldDistance * kWeldDistance)
                                    continue;
                                var with = (corner.brush == b && corner.surface == s) ||
                                           (surface.smoothingGroup != 0 && corner.group == surface.smoothingGroup && corner.entity == root.entityID) ||
                                           math.dot(corner.surfaceNormal, surfaceNormal) >= angleCos;
                                if (!with)
                                    continue;
                                // a normal counts once, however many vertices have it
                                var known = false;
                                for (int n = 0; n < normals.Length && !known; n++)
                                    known = math.dot(normals[n], corner.normal) > 0.99999f;
                                if (!known)
                                    normals.Add(corner.normal);
                            } while (corners.TryGetNextValue(out corner, ref iterator));
                        }
                        var sum = float3.zero;
                        for (int n = 0; n < normals.Length; n++)
                            sum += normals[n];
                        var normal = math.normalizesafe(sum, vertex.normal);
                        if (math.dot(normal, surfaceNormal) < 0)
                            normal = vertex.normal;
                        if (math.distancesq(normal, vertex.normal) > 1e-10f)
                        {
                            any = true;
                            // the tangent turned with it, its handedness kept
                            var tangent = vertex.tangent.xyz - normal * math.dot(normal, vertex.tangent.xyz);
                            vertex.tangent = new float4(math.normalizesafe(tangent, vertex.tangent.xyz), vertex.tangent.w);
                            vertex.normal  = normal;
                        }
                        newVertices.Add(vertex);
                    }
                    if (!any)
                    {
                        newVertices.Resize(start, NativeArrayOptions.UninitializedMemory);
                        continue;
                    }
                    changedSurfaces.Add(s);
                    newStarts.Add(start);
                }
                if (changedSurfaces.Length == 0)
                    continue;
                var result = Rebuilt(buffer, changedSurfaces, newStarts, newVertices);
                patched.Add(result);
                var data = brushes[b];
                data.brushRenderBuffer = result;
                brushes[b] = data;
            }
        }

        // The buffer with the given surfaces' render vertices replaced (from newStarts in newVertices, as many as they had)
        static BlobAssetReference<ChiselBrushRenderBuffer> Rebuilt(BlobAssetReference<ChiselBrushRenderBuffer> source, NativeList<int> changed,
                                                                   NativeList<int> newStarts, NativeList<RenderVertex> newVertices)
        {
            ref var original = ref source.Value;
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root  = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
                root.entityID = original.entityID;
                var surfaces  = builder.Allocate(ref root.surfaces, original.surfaces.Length);
                for (int s = 0; s < original.surfaces.Length; s++)
                {
                    ref var from = ref original.surfaces[s];
                    ref var to   = ref surfaces[s];
                    to.surfaceIndex          = from.surfaceIndex;
                    to.baseSurfaceIndex      = from.baseSurfaceIndex;
                    to.decalEntityID         = from.decalEntityID;
                    to.decalStack            = from.decalStack;
                    to.decalFades            = from.decalFades;
                    to.decalHidesBorder      = from.decalHidesBorder;
                    to.paintSurface          = from.paintSurface;
                    to.smoothingGroup        = from.smoothingGroup;
                    to.destinationFlags      = from.destinationFlags;
                    to.destinationParameters = from.destinationParameters;
                    to.outputFlags           = from.outputFlags;
                    to.CopyHotspot(builder, ref from);
                    var at = changed.IndexOf(s);
                    if (at < 0)
                    {
                        to.CopyGeometry(builder, ref from);
                        continue;
                    }
                    to.vertexCount       = from.vertexCount;
                    to.indexCount        = from.indexCount;
                    to.geometryHashValue = from.geometryHashValue;
                    to.shapeHashValue    = from.shapeHashValue;
                    to.aabb              = from.aabb;
                    to.lightmapChart     = from.lightmapChart;
                    builder.Construct(ref to.indices,          ref from.indices);
                    builder.Construct(ref to.selectVertices,   ref from.selectVertices);
                    builder.Construct(ref to.colliderVertices, ref from.colliderVertices);
                    builder.Construct(ref to.needles,          ref from.needles);
                    var vertices = builder.Allocate(ref to.renderVertices, from.renderVertices.Length);
                    // the hash of how it is drawn takes the new normals, by xor: paint replaces its own part of it that way
                    var normalsHash = 0u;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        vertices[v] = newVertices[newStarts[at] + v];
                        normalsHash = math.hash(new uint2(normalsHash, math.hash(vertices[v].normal)));
                    }
                    to.surfaceHashValue = from.surfaceHashValue ^ normalsHash;
                }

                var queries = builder.Allocate(ref root.querySurfaces, original.querySurfaces.Length);
                using var list = new NativeList<ChiselQuerySurface>(Allocator.Temp);
                for (int q = 0; q < original.querySurfaces.Length; q++)
                {
                    ref var from = ref original.querySurfaces[q];
                    list.Clear();
                    for (int e = 0; e < from.surfaces.Length; e++)
                    {
                        var entry = from.surfaces[e];
                        entry.surfaceHashValue = surfaces[entry.surfaceIndex].surfaceHashValue;
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
    }
}
