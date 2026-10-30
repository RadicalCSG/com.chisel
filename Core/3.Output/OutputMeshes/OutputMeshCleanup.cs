using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    // A brush's buffer as the cleanup left it, with what it was made from (OutputMeshCleanup)
    internal struct MeshCleanupEntry
    {
        public ulong key;
        // Null when there was nothing to take out
        public BlobAssetReference<ChiselBrushRenderBuffer> result;
        public bool used;
    }

    // A brush to clean up, and what the cleanup made of it
    internal struct MeshCleanupWork
    {
        public int brush;
        public ulong key;
        public BlobAssetReference<ChiselBrushRenderBuffer> result;
    }

    // At the end of the output, after decals, the model's weld and the hotspot cut: what the cuts added to what is drawn
    // without changing how it looks is taken out again. A vertex inside a surface (not on its outline, which its neighbours
    // share) is removed where the triangles around it are flat and all its attributes interpolate exactly across them,
    // and the hole is filled again; then an edge is flipped where that makes its two triangles less thin, again only where
    // everything interpolates exactly. The collider and the lightmap layout keep the triangles as they were. A painted
    // surface is left as it is: its paint is added per vertex when its mesh is made.
    internal static class OutputMeshCleanup
    {
        // How far a corner may be from the plane of the triangles around it, and how far an attribute may be from what
        // interpolating it gives
        const float kPlaneDistance   = 1e-4f;
        const float kAttributeError  = 1e-4f;
        // A flip has to make the thinner triangle's smallest angle this much larger
        const float kFlipGain        = 1.05f;
        // A vertex with more triangles around it than this is left
        const int   kMaxFan          = 64;        // The smallest angle a triangle the cleanup makes may have (radians): three corners on a line make none
        const float kMinAngle        = 1e-3f;

        // What the brush's buffer holds, to tell whether the cleanup's last result is still what it would make
        public static unsafe ulong KeyOf(BlobAssetReference<ChiselBrushRenderBuffer> buffer)
        {
            if (!buffer.IsCreated)
                return 0;
            // the size of a blob's data sits in the header in front of it (Unity.Entities BlobAssetHeader, 32 bytes, Length at 8)
            var data   = (byte*)buffer.GetUnsafePtr();
            var length = *(int*)(data - 32 + 8);
            if (length <= 0)
                return 0;
            var hash = xxHash3.Hash64(data, length);
            return ((ulong)hash.y << 32) | hash.x;
        }

        // The brushes to clean up (work); the others get what the cache kept for them
        public static void Plan(NativeList<BrushData> brushes, NativeParallelHashMap<CompactNodeID, MeshCleanupEntry> cache, NativeList<MeshCleanupWork> work)
        {
            work.Clear();
            var cached = cache.IsCreated;
            for (int b = 0; b < brushes.Length; b++)
            {
                var data = brushes[b];
                if (!data.brushRenderBuffer.IsCreated)
                    continue;
                var key    = KeyOf(data.brushRenderBuffer);
                var nodeID = data.brushIndexOrder.compactNodeID;
                if (cached && cache.TryGetValue(nodeID, out var entry) && entry.key == key)
                {
                    entry.used = true;
                    cache[nodeID] = entry;
                    if (entry.result.IsCreated)
                    {
                        data.brushRenderBuffer = entry.result;
                        brushes[b] = data;
                    }
                    continue;
                }
                work.Add(new MeshCleanupWork { brush = b, key = key });
            }
        }

        // One planned brush; each is independent of the others
        public static MeshCleanupWork CleanPlanned(MeshCleanupWork work, NativeArray<BrushData> brushes)
        {
            work.result = Clean(brushes[work.brush].brushRenderBuffer);
            return work;
        }

        // The results handed to the brushes and the cache, which forgets the brushes it wasn't asked for
        public static void Finish(NativeList<BrushData> brushes, NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> patched,
                                  NativeList<MeshCleanupWork> work, NativeParallelHashMap<CompactNodeID, MeshCleanupEntry> cache)
        {
            var cached = cache.IsCreated;
            for (int i = 0; i < work.Length; i++)
            {
                var item = work[i];
                var data = brushes[item.brush];
                if (cached)
                {
                    var nodeID = data.brushIndexOrder.compactNodeID;
                    if (cache.TryGetValue(nodeID, out var old) && old.result.IsCreated)
                        old.result.Dispose();
                    cache[nodeID] = new MeshCleanupEntry { key = item.key, result = item.result, used = true };
                } else if (item.result.IsCreated)
                    patched.Add(item.result);
                if (!item.result.IsCreated)
                    continue;
                data.brushRenderBuffer = item.result;
                brushes[item.brush] = data;
            }
            if (!cached)
                return;
            using var gone = new NativeList<CompactNodeID>(Allocator.Temp);
            foreach (var pair in cache)
            {
                if (!pair.Value.used)
                    gone.Add(pair.Key);
            }
            foreach (var id in gone)
            {
                if (cache.TryGetValue(id, out var dead) && dead.result.IsCreated)
                    dead.result.Dispose();
                cache.Remove(id);
            }
            using var left = cache.GetKeyArray(Allocator.Temp);
            foreach (var id in left)
            {
                var entry = cache[id];
                entry.used = false;
                cache[id] = entry;
            }
        }

        // The brush's buffer with its surfaces cleaned up; Null when none of them changed
        public static BlobAssetReference<ChiselBrushRenderBuffer> Clean(BlobAssetReference<ChiselBrushRenderBuffer> source)
        {
            if (!source.IsCreated)
                return default;
            ref var original = ref source.Value;
            var anyChanged = false;
            using var indices  = new NativeList<int>(Allocator.Temp);
            using var collider = new NativeList<float3>(Allocator.Temp);
            using var select   = new NativeList<SelectVertex>(Allocator.Temp);
            using var render   = new NativeList<RenderVertex>(Allocator.Temp);
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
                    to.paintSurface          = from.paintSurface;
                    to.destinationFlags      = from.destinationFlags;
                    to.destinationParameters = from.destinationParameters;
                    to.outputFlags           = from.outputFlags;
                    var cleaned = from.paintSurface < 0 && from.indices.Length >= 6;
                    if (cleaned)
                    {
                        Load(ref from, indices, collider, select, render);
                        cleaned = CleanSurface(indices, collider, select, render, from.physicsIndices.Length > 0);
                    }
                    if (!cleaned)
                    {
                        to.CopyGeometry(builder, ref from);
                        to.CopyHotspot(builder, ref from);
                        continue;
                    }
                    anyChanged = true;
                    to.Store(builder, indices, collider, select, render);
                    // the collider and the lightmap keep the triangles as they were
                    to.shapeHashValue = from.shapeHashValue;
                    to.lightmapChart  = from.lightmapChart;
                    if (from.physicsIndices.Length > 0)
                    {
                        builder.Construct(ref to.physicsIndices,  ref from.physicsIndices);
                        builder.Construct(ref to.physicsVertices, ref from.physicsVertices);
                    } else
                    {
                        builder.Construct(ref to.physicsIndices,  ref from.indices);
                        builder.Construct(ref to.physicsVertices, ref from.colliderVertices);
                    }
                    to.CopySections(builder, ref from);
                }
                if (!anyChanged)
                    return default;

                // The mesh queries' lists, with the cleaned surfaces' counts and hashes
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

        static void Load(ref ChiselSurfaceRenderBuffer from, NativeList<int> indices, NativeList<float3> collider, NativeList<SelectVertex> select, NativeList<RenderVertex> render)
        {
            indices.Clear(); collider.Clear(); select.Clear(); render.Clear();
            for (int i = 0; i < from.indices.Length; i++)          indices.Add(from.indices[i]);
            for (int i = 0; i < from.colliderVertices.Length; i++) collider.Add(from.colliderVertices[i]);
            for (int i = 0; i < from.selectVertices.Length; i++)   select.Add(from.selectVertices[i]);
            for (int i = 0; i < from.renderVertices.Length; i++)   render.Add(from.renderVertices[i]);
        }

        // Takes out of one surface what doesn't change how it looks; false when there was nothing. Its edges are only
        // flipped where it was cut (cut: by the hotspot sections) or something was taken out: the CSG's own triangles stay.
        public static bool CleanSurface(NativeList<int> indices, NativeList<float3> collider, NativeList<SelectVertex> select, NativeList<RenderVertex> render,
                                        bool cut = false)
        {
            var vertexCount = render.Length;
            if (indices.Length < 6 || indices.Length % 3 != 0 || collider.Length != vertexCount || select.Length != vertexCount)
                return false;
            for (int i = 0; i < indices.Length; i++)
            {
                if (indices[i] < 0 || indices[i] >= vertexCount)
                    return false;
            }
            var mesh = new SurfaceMesh(indices, collider, select, render);
            try
            {
                var changed = mesh.MergeEqualVertices();
                mesh.Connect();
                changed |= mesh.RemoveInteriorVertices();
                if (cut || changed)
                    changed |= mesh.FlipThinTriangles();
                if (changed)
                    mesh.Write(indices, collider, select, render);
                return changed;
            }
            finally
            {
                mesh.Dispose();
            }
        }

        // A surface's triangles, with which triangle each directed edge belongs to
        struct SurfaceMesh : IDisposable
        {
            NativeList<int3>   triangles;
            NativeList<bool>   alive;
            NativeHashMap<int2, int> edgeOf;
            // a triangle each vertex is a corner of; a vertex on an edge more than two triangles share is never moved
            NativeArray<int>   cornerOf;
            NativeArray<bool>  locked;
            NativeArray<int>   sameAs;

            readonly NativeList<float3>       collider;
            readonly NativeList<SelectVertex> select;
            readonly NativeList<RenderVertex> render;

            public SurfaceMesh(NativeList<int> indices, NativeList<float3> collider, NativeList<SelectVertex> select, NativeList<RenderVertex> render)
            {
                this.collider = collider;
                this.select   = select;
                this.render   = render;
                var triangleCount = indices.Length / 3;
                triangles = new NativeList<int3>(triangleCount, Allocator.Temp);
                alive     = new NativeList<bool>(triangleCount, Allocator.Temp);
                for (int t = 0; t < triangleCount; t++)
                {
                    triangles.Add(new int3(indices[t * 3], indices[t * 3 + 1], indices[t * 3 + 2]));
                    alive.Add(true);
                }
                edgeOf   = new NativeHashMap<int2, int>(indices.Length, Allocator.Temp);
                cornerOf = new NativeArray<int>(render.Length, Allocator.Temp);
                locked   = new NativeArray<bool>(render.Length, Allocator.Temp);
                sameAs   = new NativeArray<int>(render.Length, Allocator.Temp);
            }

            public void Dispose()
            {
                triangles.Dispose();
                alive.Dispose();
                edgeOf.Dispose();
                cornerOf.Dispose();
                locked.Dispose();
                sameAs.Dispose();
            }

            // Vertices that are the same in every way are one; a triangle that loses its area that way is dropped
            public bool MergeEqualVertices()
            {
                var changed = false;
                using var byHash = new NativeParallelMultiHashMap<uint, int>(render.Length, Allocator.Temp);
                for (int v = 0; v < render.Length; v++)
                {
                    sameAs[v] = v;
                    var hash = HashOf(v);
                    if (byHash.TryGetFirstValue(hash, out var other, out var iterator))
                    {
                        do
                        {
                            if (Same(v, other))
                            {
                                sameAs[v] = other;
                                changed = true;
                                break;
                            }
                        } while (byHash.TryGetNextValue(out other, ref iterator));
                    }
                    if (sameAs[v] == v)
                        byHash.Add(hash, v);
                }
                if (!changed)
                    return false;
                for (int t = 0; t < triangles.Length; t++)
                {
                    var triangle = triangles[t];
                    triangle = new int3(sameAs[triangle.x], sameAs[triangle.y], sameAs[triangle.z]);
                    triangles[t] = triangle;
                    if (triangle.x == triangle.y || triangle.y == triangle.z || triangle.z == triangle.x)
                        alive[t] = false;
                }
                return true;
            }

            readonly unsafe uint HashOf(int v)
            {
                var r = render[v];
                var s = select[v];
                var c = collider[v];
                return math.hash(new uint3(math.hash(&r, sizeof(RenderVertex)), math.hash(&s, sizeof(SelectVertex)), math.hash(c)));
            }

            readonly unsafe bool Same(int a, int b)
            {
                var ra = render[a];   var rb = render[b];
                var sa = select[a];   var sb = select[b];
                return UnsafeUtility.MemCmp(&ra, &rb, sizeof(RenderVertex)) == 0 &&
                       UnsafeUtility.MemCmp(&sa, &sb, sizeof(SelectVertex)) == 0 &&
                       math.all(collider[a] == collider[b]);
            }

            public void Connect()
            {
                for (int v = 0; v < cornerOf.Length; v++)
                    cornerOf[v] = -1;
                for (int t = 0; t < triangles.Length; t++)
                {
                    if (alive[t])
                        AddEdges(t);
                }
            }

            void AddEdges(int t)
            {
                var triangle = triangles[t];
                for (int c = 0; c < 3; c++)
                {
                    var from = triangle[c];
                    var to   = triangle[(c + 1) % 3];
                    cornerOf[from] = t;
                    if (!edgeOf.TryAdd(new int2(from, to), t))
                    {
                        locked[from] = true;
                        locked[to]   = true;
                    }
                }
            }

            void RemoveTriangle(int t)
            {
                var triangle = triangles[t];
                for (int c = 0; c < 3; c++)
                {
                    var edge = new int2(triangle[c], triangle[(c + 1) % 3]);
                    if (edgeOf.TryGetValue(edge, out var owner) && owner == t)
                        edgeOf.Remove(edge);
                }
                alive[t] = false;
            }

            int AddTriangle(int3 triangle)
            {
                var t = triangles.Length;
                triangles.Add(triangle);
                alive.Add(true);
                AddEdges(t);
                return t;
            }

            static int3 Starting(int3 triangle, int vertex)
            {
                if (triangle.y == vertex) return triangle.yzx;
                if (triangle.z == vertex) return triangle.zxy;
                return triangle;
            }

            // The triangles around a vertex and the vertices around it in order, when it is inside the surface
            readonly bool FanOf(int v, NativeList<int> fan, NativeList<int> ring)
            {
                fan.Clear();
                ring.Clear();
                var t = cornerOf[v];
                if (t < 0 || !alive[t] || !math.any(triangles[t] == v))
                    return false;
                var start = Starting(triangles[t], v);
                var first = start.y;
                var next  = start.z;
                fan.Add(t);
                ring.Add(first);
                while (true)
                {
                    if (ring.Length > kMaxFan)
                        return false;
                    if (!edgeOf.TryGetValue(new int2(v, next), out var across))
                        return false;
                    if (next == first)
                        return across == t;
                    ring.Add(next);
                    fan.Add(across);
                    next = Starting(triangles[across], v).z;
                }
            }

            public bool RemoveInteriorVertices()
            {
                var changed = false;
                using var fan     = new NativeList<int>(16, Allocator.Temp);
                using var ring    = new NativeList<int>(16, Allocator.Temp);
                using var points  = new NativeList<int>(16, Allocator.Temp);
                using var filled  = new NativeList<int3>(16, Allocator.Temp);
                using var flat    = new NativeList<float2>(16, Allocator.Temp);
                NativeArray<bool> removed;
                using var _removed = removed = new NativeArray<bool>(render.Length, Allocator.Temp);
                for (int pass = 0; pass < 4; pass++)
                {
                    var passChanged = false;
                    for (int v = 0; v < render.Length; v++)
                    {
                        if (removed[v] || locked[v] || sameAs[v] != v)
                            continue;
                        if (!FanOf(v, fan, ring) || ring.Length < 3 || HasRepeats(ring))
                            continue;
                        points.Clear();
                        points.AddRange(ring.AsArray());
                        points.Add(v);
                        if (!Interpolates(fan, points, out var frame))
                            continue;
                        flat.Clear();
                        for (int i = 0; i < ring.Length; i++)
                            flat.Add(frame.ToPlane(collider[ring[i]]));
                        if (!Fill(ring, flat, filled))
                            continue;
                        for (int i = 0; i < fan.Length; i++)
                            RemoveTriangle(fan[i]);
                        for (int i = 0; i < filled.Length; i++)
                            AddTriangle(filled[i]);
                        removed[v] = true;
                        passChanged = true;
                    }
                    changed |= passChanged;
                    if (!passChanged)
                        break;
                }
                return changed;
            }

            static bool HasRepeats(NativeList<int> ring)
            {
                for (int i = 0; i < ring.Length; i++)
                {
                    for (int j = i + 1; j < ring.Length; j++)
                    {
                        if (ring[i] == ring[j])
                            return true;
                    }
                }
                return false;
            }

            // A plane through the largest of the triangles, with 2D axes in it
            struct Frame
            {
                public float3 origin, normal, u, v;
                public readonly float2 ToPlane(float3 point) => new float2(math.dot(point - origin, u), math.dot(point - origin, v));
            }

            // Whether the triangles are flat and face the same way, and every attribute at the points is what interpolating
            // it over the largest of them gives
            readonly bool Interpolates(NativeList<int> fan, NativeList<int> points, out Frame frame)
            {
                frame = default;
                var largest = -1; var largestArea = 0.0f;
                for (int i = 0; i < fan.Length; i++)
                {
                    var triangle = triangles[fan[i]];
                    var area = math.length(math.cross(collider[triangle.y] - collider[triangle.x], collider[triangle.z] - collider[triangle.x]));
                    if (area > largestArea) { largestArea = area; largest = fan[i]; }
                }
                if (largest < 0 || !(largestArea > 1e-12f))
                    return false;
                var reference = triangles[largest];
                var p0 = collider[reference.x]; var p1 = collider[reference.y]; var p2 = collider[reference.z];
                frame.origin = p0;
                frame.normal = math.normalize(math.cross(p1 - p0, p2 - p0));
                frame.u      = math.normalize(p1 - p0);
                frame.v      = math.cross(frame.normal, frame.u);
                for (int i = 0; i < fan.Length; i++)
                {
                    var triangle = triangles[fan[i]];
                    var cross = math.cross(collider[triangle.y] - collider[triangle.x], collider[triangle.z] - collider[triangle.x]);
                    if (!(math.dot(cross, frame.normal) > 0))
                        return false;
                }
                var a = frame.ToPlane(p0); var b = frame.ToPlane(p1); var c = frame.ToPlane(p2);
                var denominator = Cross(b - a, c - a);
                var selected = select[reference.x];
                for (int i = 0; i < points.Length; i++)
                {
                    var p = points[i];
                    if (math.abs(math.dot(collider[p] - p0, frame.normal)) > kPlaneDistance)
                        return false;
                    if (!math.all((float4)select[p].entityID == (float4)selected.entityID))
                        return false;
                    var q  = frame.ToPlane(collider[p]);
                    var w1 = Cross(q - a, c - a) / denominator;
                    var w2 = Cross(b - a, q - a) / denominator;
                    var w0 = 1 - w1 - w2;
                    if (!Near(render[p], Interpolate(render[reference.x], render[reference.y], render[reference.z], w0, w1, w2)))
                        return false;
                }
                return true;
            }

            static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;

            static RenderVertex Interpolate(RenderVertex a, RenderVertex b, RenderVertex c, float wa, float wb, float wc)
            {
                return new RenderVertex
                {
                    position = a.position * wa + b.position * wb + c.position * wc,
                    normal   = a.normal   * wa + b.normal   * wb + c.normal   * wc,
                    tangent  = a.tangent  * wa + b.tangent  * wb + c.tangent  * wc,
                    uv0      = a.uv0      * wa + b.uv0      * wb + c.uv0      * wc,
                    uv1      = a.uv1      * wa + b.uv1      * wb + c.uv1      * wc
                };
            }

            static bool Near(float4 a, float4 b) => math.all(math.abs(a - b) <= kAttributeError * (1 + math.abs(a)));

            static bool Near(RenderVertex actual, RenderVertex interpolated)
            {
                return Near(new float4(actual.position, 0), new float4(interpolated.position, 0)) &&
                       Near(new float4(actual.normal, 0),   new float4(interpolated.normal, 0)) &&
                       Near(actual.tangent, interpolated.tangent) &&
                       Near(new float4(actual.uv0, actual.uv1), new float4(interpolated.uv0, interpolated.uv1));
            }

            // The polygon around a removed vertex filled with triangles: its fattest corner cut off first each time (ear
            // clipping), or else, where that leaves three corners in a line last, the fattest fan from one of its corners
            static bool Fill(NativeList<int> ring, NativeList<float2> points, NativeList<int3> filled)
            {
                if (Clip(ring, points, filled))
                    return true;
                using var fan = new NativeList<int3>(ring.Length, Allocator.Temp);
                var bestAngle = 0.0f;
                for (int apex = 0; apex < ring.Length; apex++)
                {
                    var angle = Fan(apex, ring, points, fan);
                    if (angle > bestAngle)
                    {
                        bestAngle = angle;
                        filled.Clear();
                        filled.AddRange(fan.AsArray());
                    }
                }
                return bestAngle > 0;
            }

            // The triangles from one corner to every edge it isn't on, and their smallest angle; 0 when one is no good
            static float Fan(int apex, NativeList<int> ring, NativeList<float2> points, NativeList<int3> fan)
            {
                fan.Clear();
                var smallest = float.MaxValue;
                for (int step = 1; step < ring.Length - 1; step++)
                {
                    var from = (apex + step) % ring.Length;
                    var to   = (apex + step + 1) % ring.Length;
                    if (!(Cross(points[from] - points[apex], points[to] - points[from]) > 0))
                        return 0;
                    var angle = SmallestAngle(points[apex], points[from], points[to]);
                    if (!(angle > kMinAngle))
                        return 0;
                    for (int other = 0; other < ring.Length; other++)
                    {
                        if (other != apex && other != from && other != to && InTriangle(points[other], points[apex], points[from], points[to]))
                            return 0;
                    }
                    smallest = math.min(smallest, angle);
                    fan.Add(new int3(ring[apex], ring[from], ring[to]));
                }
                return smallest;
            }

            static bool Clip(NativeList<int> ring, NativeList<float2> points, NativeList<int3> filled)
            {
                filled.Clear();
                using var left = new NativeList<int>(ring.Length, Allocator.Temp);
                for (int i = 0; i < ring.Length; i++)
                    left.Add(i);
                while (left.Length > 3)
                {
                    var best = -1; var bestAngle = 0.0f;
                    for (int i = 0; i < left.Length; i++)
                    {
                        var prev = left[(i + left.Length - 1) % left.Length];
                        var self = left[i];
                        var next = left[(i + 1) % left.Length];
                        if (!(Cross(points[self] - points[prev], points[next] - points[self]) > 0))
                            continue;
                        var blocked = false;
                        for (int j = 0; j < left.Length && !blocked; j++)
                        {
                            var other = left[j];
                            if (other == prev || other == self || other == next)
                                continue;
                            blocked = InTriangle(points[other], points[prev], points[self], points[next]);
                        }
                        if (blocked)
                            continue;
                        var angle = SmallestAngle(points[prev], points[self], points[next]);
                        if (angle > kMinAngle && angle > bestAngle) { bestAngle = angle; best = i; }
                    }
                    if (best < 0)
                        return false;
                    filled.Add(new int3(ring[left[(best + left.Length - 1) % left.Length]], ring[left[best]], ring[left[(best + 1) % left.Length]]));
                    left.RemoveAt(best);
                }
                if (!(Cross(points[left[1]] - points[left[0]], points[left[2]] - points[left[1]]) > 0) ||
                    !(SmallestAngle(points[left[0]], points[left[1]], points[left[2]]) > kMinAngle))
                    return false;
                filled.Add(new int3(ring[left[0]], ring[left[1]], ring[left[2]]));
                return true;
            }

            // On the triangle or inside it
            static bool InTriangle(float2 p, float2 a, float2 b, float2 c)
            {
                return Cross(b - a, p - a) >= 0 && Cross(c - b, p - b) >= 0 && Cross(a - c, p - c) >= 0;
            }

            static float SmallestAngle(float2 a, float2 b, float2 c)
            {
                return math.min(Angle(b - a, c - a), math.min(Angle(a - b, c - b), Angle(a - c, b - c)));
            }

            static float SmallestAngle(float3 a, float3 b, float3 c)
            {
                return math.min(Angle(b - a, c - a), math.min(Angle(a - b, c - b), Angle(a - c, b - c)));
            }

            static float Angle(float2 a, float2 b) => Angle(new float3(a, 0), new float3(b, 0));

            static float Angle(float3 a, float3 b)
            {
                var lengths = math.length(a) * math.length(b);
                if (!(lengths > 0))
                    return 0;
                return math.acos(math.clamp(math.dot(a, b) / lengths, -1, 1));
            }

            // Lawson's flips, for the smallest angle of each pair of triangles
            public bool FlipThinTriangles()
            {
                var changed = false;
                using var fan    = new NativeList<int>(2, Allocator.Temp);
                using var points = new NativeList<int>(4, Allocator.Temp);
                var budget = triangles.Length * 4;
                var flipped = true;
                while (flipped && budget > 0)
                {
                    flipped = false;
                    for (int t = 0; t < triangles.Length && budget > 0; t++)
                    {
                        if (!alive[t])
                            continue;
                        for (int c = 0; c < 3; c++)
                        {
                            var triangle = triangles[t];
                            var a = triangle[c];
                            var b = triangle[(c + 1) % 3];
                            var apex = triangle[(c + 2) % 3];
                            if (locked[a] || locked[b])
                                continue;
                            if (!edgeOf.TryGetValue(new int2(b, a), out var other) || other == t || !alive[other])
                                continue;
                            var opposite = Starting(triangles[other], b).z;
                            if (opposite == apex || edgeOf.ContainsKey(new int2(apex, opposite)) || edgeOf.ContainsKey(new int2(opposite, apex)))
                                continue;
                            var pa = collider[a]; var pb = collider[b]; var pc = collider[apex]; var pd = collider[opposite];
                            var before = math.min(SmallestAngle(pa, pb, pc), SmallestAngle(pb, pa, pd));
                            var after  = math.min(SmallestAngle(pa, pd, pc), SmallestAngle(pd, pb, pc));
                            if (!(after > before * kFlipGain) || !(after > kMinAngle))
                                continue;
                            fan.Clear(); fan.Add(t); fan.Add(other);
                            points.Clear(); points.Add(a); points.Add(b); points.Add(apex); points.Add(opposite);
                            if (!Interpolates(fan, points, out var frame))
                                continue;
                            // both new triangles have to face the way the old ones did (the four corners make a convex shape)
                            var normal = frame.normal;
                            if (!(math.dot(math.cross(pd - pa, pc - pa), normal) > 0) || !(math.dot(math.cross(pb - pd, pc - pd), normal) > 0))
                                continue;
                            RemoveTriangle(t);
                            RemoveTriangle(other);
                            AddTriangle(new int3(a, opposite, apex));
                            AddTriangle(new int3(opposite, b, apex));
                            budget--;
                            flipped = true;
                            changed = true;
                            break;
                        }
                    }
                }
                return changed;
            }

            // The triangles left, and only the vertices they use, in their old order
            public readonly void Write(NativeList<int> indices, NativeList<float3> collider, NativeList<SelectVertex> select, NativeList<RenderVertex> render)
            {
                NativeArray<int> newIndex;
                using var _newIndex = newIndex = new NativeArray<int>(render.Length, Allocator.Temp);
                for (int v = 0; v < newIndex.Length; v++)
                    newIndex[v] = -1;
                for (int t = 0; t < triangles.Length; t++)
                {
                    if (!alive[t])
                        continue;
                    var triangle = triangles[t];
                    newIndex[triangle.x] = 0; newIndex[triangle.y] = 0; newIndex[triangle.z] = 0;
                }
                var count = 0;
                for (int v = 0; v < newIndex.Length; v++)
                {
                    if (newIndex[v] < 0)
                        continue;
                    newIndex[v] = count;
                    collider[count] = collider[v];
                    select[count]   = select[v];
                    render[count]   = render[v];
                    count++;
                }
                collider.Resize(count, NativeArrayOptions.UninitializedMemory);
                select.Resize(count, NativeArrayOptions.UninitializedMemory);
                render.Resize(count, NativeArrayOptions.UninitializedMemory);
                indices.Clear();
                for (int t = 0; t < triangles.Length; t++)
                {
                    if (!alive[t])
                        continue;
                    var triangle = triangles[t];
                    indices.Add(newIndex[triangle.x]);
                    indices.Add(newIndex[triangle.y]);
                    indices.Add(newIndex[triangle.z]);
                }
            }
        }
    }
}
