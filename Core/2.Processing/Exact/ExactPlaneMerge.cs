using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    // Each brush makes its planes from its own corners snapped to the grid (ExactBrushes.AddPlanes). Faces of different
    // brushes that lie in one plane in float then lie in one exact plane only where their corners snap onto it, which on a
    // slanted plane they generally don't: two brushes meant to meet in a face would meet a hair apart, and both would draw
    // it. As a map compiler keeps one table of planes for every brush (qbsp's add_or_find_plane, with a tolerance on the
    // normal and the distance), faces of different brushes that touch and are one plane in float (their float planes
    // round, ExactPlane.Quantize, to within one unit of each other: a float plane can round either way) form a group, and
    // every face of a group takes the same plane, so they meet exactly. A group is never split: a face left with its own
    // plane would meet the others a hair apart again. The planes depend only on the brushes, not on their order.
    static class ExactPlaneMerge
    {
        // How far apart (in grid steps, 1 / ExactPlane.kPointScale m) the boxes round two faces' snapped corners may be and
        // the faces still touch: corners of different brushes meant to be one point can snap to neighbouring grid points
        public const double kTouch = 4;

        public struct Face
        {
            public int    brush;        // into the brushes given
            public int    face;
            public ExactPlane key;      // its float plane rounded (ExactPlane.Quantize)
            public double3 min, max;    // the box round its snapped corners, in grid steps
            public double extent;       // its diagonal, squared

            public bool Better(in Face other, in ExactPlane plane, in ExactPlane otherPlane)
            {
                if (extent != other.extent)
                    return extent > other.extent;
                return Less(Canonical(plane), Canonical(otherPlane));
            }
        }

        // One offset step of a rounded plane (ExactPlane.Quantize), in the units of its w
        const double kOffsetStep = ExactPlane.kNormalScale / ExactPlane.kOffsetScale * ExactPlane.kPointScale;

        // Whether two rounded float planes are one plane, facing either way: each normal component and the offset within one
        // step of rounding
        public static bool OnePlane(in ExactPlane p, in ExactPlane q)
        {
            return Close(p, q) || Close(p, q.Flipped());
        }

        static bool Close(in ExactPlane p, in ExactPlane q)
        {
            return math.abs(p.a - q.a) < 2 && math.abs(p.b - q.b) < 2 && math.abs(p.c - q.c) < 2 &&
                   math.abs(Int128.Sub(p.w, q.w).ToDouble()) <= kOffsetStep;
        }

        // The plane facing the way its first non-zero normal component is positive, so a plane and its opposite compare equal
        static ExactPlane Canonical(in ExactPlane plane)
        {
            bool flip = plane.a != 0 ? plane.a < 0 : (plane.b != 0 ? plane.b < 0 : plane.c < 0);
            return flip ? plane.Flipped() : plane;
        }

        static bool Less(in ExactPlane p, in ExactPlane q)
        {
            if (p.a != q.a) return p.a < q.a;
            if (p.b != q.b) return p.b < q.b;
            if (p.c != q.c) return p.c < q.c;
            return Int128.Sub(p.w, q.w).Sign() < 0;
        }

        // The plane facing the way the face's own plane does
        public static ExactPlane Facing(in ExactPlane plane, in ExactPlane own)
        {
            double dot = (double)plane.a * own.a + (double)plane.b * own.b + (double)plane.c * own.c;
            return dot < 0 ? plane.Flipped() : plane;
        }

        // Whether faces i and j are of different brushes, their boxes meet and they are one plane: then they are one group
        public static bool Joins(in Face fi, in Face fj)
        {
            if (fi.brush == fj.brush)
                return false;
            if (math.any(fj.min > fi.max + kTouch) || math.any(fi.min > fj.max + kTouch))
                return false;
            return OnePlane(fi.key, fj.key);
        }

        // ---- the index of faces --------------------------------------------------------------------------------------

        // Faces are found by cell: their rounded plane (ExactPlane.Quantize) with kPlaneShift bits of each of a, b, c and
        // of the offset in steps taken off, and the place on a grid of kGridShift bits of grid steps (32 m). A face goes in
        // the cells of its own plane its box is in; it is looked for in the cells of its plane one rounding step either
        // way, facing either way, that its box widened by kTouch is in. Two faces that are one group always share one.
        public struct CellKey : IEquatable<CellKey>
        {
            public long a, b, c, w;
            public int  x, y, z;

            public bool Equals(CellKey other) => a == other.a && b == other.b && c == other.c && w == other.w &&
                                                 x == other.x && y == other.y && z == other.z;

            public override int GetHashCode()
            {
                return (int)(math.hash(new int4((int)a, (int)b, (int)c, (int)w ^ (int)(w >> 32))) ^
                             math.hash(new int3(x, y, z)) * 0x9E3779B1u);
            }
        }

        const int  kPlaneShift  = 6;
        const int  kOffsetShift = 20;       // one offset step of a rounded plane's w is 2^20 (kOffsetStep)
        const int  kGridShift   = 19;       // 2^19 grid steps of 1 / 16384 m

        // The plane's cell edges lie half a cell off the multiples of its width, so the round values of axis-aligned planes
        // (0, a whole normal, whole offsets) are inside a cell, not on an edge
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static long PlaneCell(long value) => (value + (1L << (kPlaneShift - 1))) >> kPlaneShift;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static int GridCell(double coordinate) => (int)math.floor(coordinate / (1 << kGridShift));

        // a, b, c and the offset in steps of a rounded plane: |w| is below 2^58 (ExactPlane.kMaxDistance kOffsetScale
        // steps), so it is a long
        static void PlaneValues(in ExactPlane key, bool flipped, out long a, out long b, out long c, out long offset)
        {
            long w = (long)key.w.lo;
            long sign = flipped ? -1 : 1;
            a = sign * key.a; b = sign * key.b; c = sign * key.c;
            offset = (sign * w) >> kOffsetShift;
        }

        // The cells a face is put in
        public static void HomeCells(in Face face, ref NativeList<CellKey> cells)
        {
            cells.Clear();
            PlaneValues(face.key, false, out long a, out long b, out long c, out long offset);
            var key = new CellKey { a = PlaneCell(a), b = PlaneCell(b), c = PlaneCell(c), w = PlaneCell(offset) };
            AddGridCells(key, face.min, face.max, ref cells);
        }

        // The cells a face's group mates are in
        public static void SearchCells(in Face face, ref NativeList<CellKey> cells)
        {
            cells.Clear();
            var min = face.min - kTouch;
            var max = face.max + kTouch;
            for (int flip = 0; flip < 2; flip++)
            {
                PlaneValues(face.key, flip == 1, out long pa, out long pb, out long pc, out long offset);
                long a0 = PlaneCell(pa - 1), a1 = PlaneCell(pa + 1);
                long b0 = PlaneCell(pb - 1), b1 = PlaneCell(pb + 1);
                long c0 = PlaneCell(pc - 1), c1 = PlaneCell(pc + 1);
                long w0 = PlaneCell(offset - 1), w1 = PlaneCell(offset + 1);
                for (long a = a0; a <= a1; a++)
                for (long b = b0; b <= b1; b++)
                for (long c = c0; c <= c1; c++)
                for (long w = w0; w <= w1; w++)
                    AddGridCells(new CellKey { a = a, b = b, c = c, w = w }, min, max, ref cells);
            }
        }

        static void AddGridCells(CellKey key, double3 min, double3 max, ref NativeList<CellKey> cells)
        {
            int x0 = GridCell(min.x), x1 = GridCell(max.x);
            int y0 = GridCell(min.y), y1 = GridCell(max.y);
            int z0 = GridCell(min.z), z1 = GridCell(max.z);
            for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            for (int z = z0; z <= z1; z++)
            {
                key.x = x; key.y = y; key.z = z;
                cells.Add(key);
            }
        }

        // ---- grouping ------------------------------------------------------------------------------------------------

        // The faces of the brush's faces that take part: each with three corners or more, its own plane and its float plane
        public static void CollectFaces(ref ExactBrush brush, int brushIndex, ref NativeList<Face> faces)
        {
            if (brush.cornerCount <= 0)
                return;
            for (int f = 0; f < brush.faceRanges.Length && f < brush.ownPlanes.Length; f++)
            {
                var range = brush.faceRanges[f];
                if (range.y < 3 || !brush.ownPlanes[f].IsValid || f >= brush.floatPlanes.Length || !brush.floatPlanes[f].IsValid)
                    continue;
                double3 min = double.MaxValue, max = double.MinValue;
                for (int v = 0; v < range.y; v++)
                {
                    var point = brush.facePoints[range.x + v];
                    var position = new double3(point.x, point.y, point.z);
                    min = math.min(min, position);
                    max = math.max(max, position);
                }
                faces.Add(new Face { brush = brushIndex, face = f, key = brush.floatPlanes[f], min = min, max = max, extent = math.lengthsq(max - min) });
            }
        }

        // Every face reachable from the seeds through faces it joins (Joins) is visited once, group by group: each group is
        // given to visit as its faces (members), in the order found. alive tells which faces are there; index finds them.
        // Returns how many pairs of faces were compared.
        public static long Groups<TVisit>(NativeList<Face> faces, NativeList<int> alive, NativeParallelMultiHashMap<CellKey, int> index,
                                          NativeList<int> seeds, ref TVisit visit)
            where TVisit : struct, IGroupVisitor
        {
            var visited = new NativeBitArray(math.max(1, faces.Length), Allocator.Temp);
            var tested  = new NativeArray<int>(math.max(1, faces.Length), Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var members = new NativeList<int>(16, Allocator.Temp);
            var cells   = new NativeList<CellKey>(16, Allocator.Temp);
            long compared = 0;
            try
            {
                for (int i = 0; i < tested.Length; i++)
                    tested[i] = -1;
                for (int s = 0; s < seeds.Length; s++)
                {
                    int seed = seeds[s];
                    if (alive[seed] == 0 || visited.IsSet(seed))
                        continue;
                    members.Clear();
                    members.Add(seed);
                    visited.Set(seed, true);
                    for (int m = 0; m < members.Length; m++)
                    {
                        int i = members[m];
                        var fi = faces[i];
                        SearchCells(fi, ref cells);
                        for (int c = 0; c < cells.Length; c++)
                        {
                            if (!index.TryGetFirstValue(cells[c], out int j, out var iterator))
                                continue;
                            do
                            {
                                if (j == i || tested[j] == i || alive[j] == 0)
                                    continue;
                                tested[j] = i;
                                compared++;
                                if (visited.IsSet(j) || !Joins(fi, faces[j]))
                                    continue;
                                visited.Set(j, true);
                                members.Add(j);
                            } while (index.TryGetNextValue(out j, ref iterator));
                        }
                    }
                    visit.Group(members);
                }
            }
            finally
            {
                visited.Dispose();
                tested.Dispose();
                members.Dispose();
                cells.Dispose();
            }
            return compared;
        }

        public interface IGroupVisitor
        {
            void Group(NativeList<int> members);
        }

        static void AddToIndex(NativeParallelMultiHashMap<CellKey, int> index, in Face face, int slot, ref NativeList<CellKey> cells)
        {
            HomeCells(face, ref cells);
            for (int c = 0; c < cells.Length; c++)
                index.Add(cells[c], slot);
        }

        static void RemoveFromIndex(NativeParallelMultiHashMap<CellKey, int> index, in Face face, int slot, ref NativeList<CellKey> cells)
        {
            HomeCells(face, ref cells);
            for (int c = 0; c < cells.Length; c++)
                index.Remove(cells[c], slot);
        }

        struct ParentOfGroup : IGroupVisitor
        {
            public NativeArray<int> parent;
            public void Group(NativeList<int> members)
            {
                int smallest = members[0];
                for (int m = 1; m < members.Length; m++)
                    smallest = math.min(smallest, members[m]);
                for (int m = 0; m < members.Length; m++)
                    parent[members[m]] = smallest;
            }
        }

        // Joins the faces of different brushes whose boxes meet and that are one plane (Joins) into groups: parent[i] is the
        // smallest face of i's group. Returns how many pairs of faces it compared.
        public static long Unite(NativeList<Face> faces, NativeArray<int> parent)
        {
            var index = new NativeParallelMultiHashMap<CellKey, int>(math.max(1, faces.Length * 2), Allocator.Temp);
            var alive = new NativeList<int>(faces.Length, Allocator.Temp);
            var seeds = new NativeList<int>(faces.Length, Allocator.Temp);
            var cells = new NativeList<CellKey>(16, Allocator.Temp);
            try
            {
                for (int i = 0; i < faces.Length; i++)
                {
                    parent[i] = i;
                    alive.Add(1);
                    seeds.Add(i);
                    AddToIndex(index, faces[i], i, ref cells);
                }
                var visit = new ParentOfGroup { parent = parent };
                return Groups(faces, alive, index, seeds, ref visit);
            }
            finally
            {
                index.Dispose();
                alive.Dispose();
                seeds.Dispose();
                cells.Dispose();
            }
        }

        // The plane every face of a group takes: through the group's snapped corners (ExactBrushes.PlaneThrough, which
        // depends only on which corners there are), or, where those don't make one, the largest face's own. brushes gives
        // each face's brush by its Face.brush.
        public static ExactPlane GroupPlane(NativeList<Face> faces, NativeList<int> members, NativeArray<BlobAssetReference<ExactBrush>> brushes,
                                           ref ExactList<ExactPoint> points)
        {
            points.Clear();
            int largest = -1;
            for (int m = 0; m < members.Length; m++)
            {
                var face = faces[members[m]];
                ref var brush = ref brushes[face.brush].Value;
                var range = brush.faceRanges[face.face];
                for (int v = 0; v < range.y; v++)
                    points.Add(brush.facePoints[range.x + v]);
                if (largest < 0 || face.Better(faces[largest], brush.ownPlanes[face.face],
                                               brushes[faces[largest].brush].Value.ownPlanes[faces[largest].face]))
                    largest = members[m];
            }
            var plane = ExactBrushes.PlaneThrough(ref points, 0, points.Length);
            if (!plane.IsValid)
                plane = brushes[faces[largest].brush].Value.ownPlanes[faces[largest].face];
            return plane;
        }

        struct PlanesOfGroup : IGroupVisitor
        {
            public NativeList<Face> faces;
            public NativeArray<BlobAssetReference<ExactBrush>> brushes;
            public NativeList<ExactPlane> planes;
            public NativeList<int> offsets;
            public ExactList<ExactPoint> points;

            public void Group(NativeList<int> members)
            {
                if (members.Length < 2)
                    return;
                var plane = GroupPlane(faces, members, brushes, ref points);
                for (int m = 0; m < members.Length; m++)
                {
                    var face = faces[members[m]];
                    planes[offsets[face.brush] + face.face] = Facing(plane, brushes[face.brush].Value.ownPlanes[face.face]);
                }
            }
        }

        // The planes each brush keeps: brushes[b]'s planes at offsets[b] in planes, one for each of its own. A brush
        // without volume keeps its own.
        public static void Merge(NativeArray<BlobAssetReference<ExactBrush>> brushes, NativeList<ExactPlane> planes, NativeList<int> offsets)
        {
            planes.Clear();
            offsets.Clear();
            var faces = new NativeList<Face>(math.max(1, brushes.Length * 6), Allocator.Temp);
            var points = new ExactList<ExactPoint>(64);
            try
            {
                for (int b = 0; b < brushes.Length; b++)
                {
                    offsets.Add(planes.Length);
                    if (!brushes[b].IsCreated)
                        continue;
                    ref var brush = ref brushes[b].Value;
                    for (int p = 0; p < brush.ownPlanes.Length; p++)
                        planes.Add(brush.ownPlanes[p]);
                    CollectFaces(ref brush, b, ref faces);
                }
                if (faces.Length < 2)
                    return;
                var index = new NativeParallelMultiHashMap<CellKey, int>(math.max(1, faces.Length * 2), Allocator.Temp);
                var alive = new NativeList<int>(faces.Length, Allocator.Temp);
                var seeds = new NativeList<int>(faces.Length, Allocator.Temp);
                var cells = new NativeList<CellKey>(16, Allocator.Temp);
                try
                {
                    for (int i = 0; i < faces.Length; i++)
                    {
                        alive.Add(1);
                        seeds.Add(i);
                        AddToIndex(index, faces[i], i, ref cells);
                    }
                    var visit = new PlanesOfGroup { faces = faces, brushes = brushes, planes = planes, offsets = offsets, points = points };
                    Groups(faces, alive, index, seeds, ref visit);
                }
                finally
                {
                    index.Dispose();
                    alive.Dispose();
                    seeds.Dispose();
                    cells.Dispose();
                }
            }
            finally
            {
                faces.Dispose();
                points.Dispose();
            }
        }

        // ---- merging again what changed -------------------------------------------------------------------------------

        // A brush, as the last merge saw it
        public struct MemoryBrush
        {
            public CompactNodeID id;
            public IndexOrder    indexOrder;
            // the brush's ExactBrush when the merge last saw it: only compared once it has been replaced, never read
            public BlobAssetReference<ExactBrush> blob;
            // its faces: slots firstFace up to firstFace + capacity, faceCount of them used
            public int  firstFace, faceCount, capacity;
            public int  stamp;
            // the merge changed its planes, so ExactMergedRebuildJob replaces its ExactBrush with the same faces
            public bool rebuiltByMerge;
            public bool alive;
        }

        struct ResultOfGroup : IGroupVisitor
        {
            public NativeList<Face> faces;
            public NativeArray<BlobAssetReference<ExactBrush>> blobs;     // by memory brush
            public NativeParallelHashMap<int, ExactPlane> planeOfFace;   // by slot
            public NativeList<int> next;
            public NativeHashSet<int> brushes;
            public ExactList<ExactPoint> points;
            public int visited;

            public void Group(NativeList<int> members)
            {
                visited += members.Length;
                var plane = members.Length < 2 ? default : GroupPlane(faces, members, blobs, ref points);
                for (int m = 0; m < members.Length; m++)
                {
                    int slot = members[m];
                    var face = faces[slot];
                    var own  = blobs[face.brush].Value.ownPlanes[face.face];
                    planeOfFace[slot] = members.Length < 2 ? own : Facing(plane, own);
                    next[slot] = members[(m + 1) % members.Length];
                    brushes.Add(face.brush);
                }
            }
        }

        // Merges the planes of the brushes again, where they can have changed since the last merge (memory): the groups
        // the changed brushes' faces were in, and the groups their faces are in now. changed: the brushes built again
        // since then (ExactInputJob). Fills merged with the brushes whose planes change, with those planes.
        public static void MergeAgain(ref ExactPlaneMergeMemory memory, NativeList<IndexOrder> brushes, NativeList<BlobAssetReference<ExactBrush>> exactBrushCache,
                                      NativeList<IndexOrder> changed,
                                      NativeList<IndexOrder> mergedOrders, NativeList<ExactPlane> mergedPlanes, NativeList<int> mergedOffsets)
        {
            mergedOrders.Clear();
            mergedPlanes.Clear();
            mergedOffsets.Clear();
            var rebuilt = new NativeHashSet<int>(math.max(1, changed.Length), Allocator.Temp);
            var dirty   = new NativeList<int>(64, Allocator.Temp);
            var seeds   = new NativeList<int>(64, Allocator.Temp);
            var cells   = new NativeList<CellKey>(16, Allocator.Temp);
            var newFaces = new NativeList<Face>(16, Allocator.Temp);
            try
            {
                for (int r = 0; r < changed.Length; r++)
                    rebuilt.Add(changed[r].nodeOrder);

                int stamp = ++memory.stamp.Value;
                bool full = !memory.valid.Value || memory.freeFaces.Value > memory.faces.Length / 2 + 4096;
                if (full)
                    memory.Clear();
                for (int b = 0; b < brushes.Length; b++)
                {
                    var indexOrder = brushes[b];
                    var blob = exactBrushCache[indexOrder.nodeOrder];
                    bool isDirty;
                    if (!memory.brushOf.TryGetValue(indexOrder.compactNodeID, out int brush))
                    {
                        brush = memory.brushes.Length;
                        memory.brushes.Add(new MemoryBrush { id = indexOrder.compactNodeID, firstFace = memory.faces.Length });
                        memory.brushOf.Add(indexOrder.compactNodeID, brush);
                        isDirty = true;
                    } else
                    {
                        var known = memory.brushes[brush];
                        isDirty = rebuilt.Contains(indexOrder.nodeOrder) || (known.blob != blob && !known.rebuiltByMerge);
                    }
                    var entry = memory.brushes[brush];
                    entry.indexOrder     = indexOrder;
                    entry.blob           = blob;
                    entry.stamp          = stamp;
                    entry.rebuiltByMerge = false;
                    entry.alive          = true;
                    memory.brushes[brush] = entry;
                    if (isDirty)
                        dirty.Add(brush);
                }
                if (!full && dirty.Length * 2 > brushes.Length + 16)
                {
                    // most brushes changed: merging everything again costs less than taking them out one by one
                    memory.valid.Value = false;
                    MergeAgain(ref memory, brushes, exactBrushCache, changed, mergedOrders, mergedPlanes, mergedOffsets);
                    return;
                }

                // brushes no longer in the tree go, their faces with them
                if (memory.brushOf.Count() > brushes.Length)
                {
                    for (int brush = 0; brush < memory.brushes.Length; brush++)
                    {
                        var entry = memory.brushes[brush];
                        if (!entry.alive || entry.stamp == stamp)
                            continue;
                        if (!RemoveFaces(ref memory, brush, ref seeds, ref cells))
                        {
                            MergeAllAgain(ref memory, brushes, exactBrushCache, changed, mergedOrders, mergedPlanes, mergedOffsets);
                            return;
                        }
                        entry = memory.brushes[brush];
                        memory.freeFaces.Value += entry.capacity;
                        entry.capacity = 0;
                        entry.alive = false;
                        entry.blob  = default;
                        memory.brushes[brush] = entry;
                        memory.brushOf.Remove(entry.id);
                    }
                }

                // the changed brushes' faces as they were, with their groups, out; their faces as they are in
                for (int d = 0; d < dirty.Length; d++)
                {
                    int brush = dirty[d];
                    if (!RemoveFaces(ref memory, brush, ref seeds, ref cells))
                    {
                        MergeAllAgain(ref memory, brushes, exactBrushCache, changed, mergedOrders, mergedPlanes, mergedOffsets);
                        return;
                    }
                    var entry = memory.brushes[brush];
                    newFaces.Clear();
                    if (entry.blob.IsCreated)
                        CollectFaces(ref entry.blob.Value, brush, ref newFaces);
                    if (newFaces.Length > entry.capacity)
                    {
                        memory.freeFaces.Value += entry.capacity;
                        entry.firstFace = memory.faces.Length;
                        entry.capacity  = newFaces.Length;
                        for (int f = 0; f < newFaces.Length; f++)
                        {
                            memory.faces.Add(default);
                            memory.next.Add(-1);
                        }
                    }
                    entry.faceCount = newFaces.Length;
                    memory.brushes[brush] = entry;
                    for (int f = 0; f < newFaces.Length; f++)
                    {
                        int slot = entry.firstFace + f;
                        memory.faces[slot] = newFaces[f];
                        memory.next[slot]  = slot;
                        AddToIndex(memory.cells, newFaces[f], slot, ref cells);
                        seeds.Add(slot);
                    }
                }

                var blobs = new NativeArray<BlobAssetReference<ExactBrush>>(math.max(1, memory.brushes.Length), Allocator.Temp);
                var planeOfFace = new NativeParallelHashMap<int, ExactPlane>(math.max(1, seeds.Length * 2), Allocator.Temp);
                var affected = new NativeHashSet<int>(math.max(1, dirty.Length * 2), Allocator.Temp);
                var points = new ExactList<ExactPoint>(64);
                var alive = new NativeList<int>(memory.next.Length, Allocator.Temp);
                try
                {
                    for (int brush = 0; brush < memory.brushes.Length; brush++)
                        blobs[brush] = memory.brushes[brush].blob;
                    for (int slot = 0; slot < memory.next.Length; slot++)
                        alive.Add(memory.next[slot] >= 0 ? 1 : 0);
                    var visit = new ResultOfGroup { faces = memory.faces, blobs = blobs, planeOfFace = planeOfFace, next = memory.next,
                                                    brushes = affected, points = points };
                    Groups(memory.faces, alive, memory.cells, seeds, ref visit);
                    memory.lastMerged.Value = visit.visited;
                    memory.lastFull.Value   = full;

                    // the brushes with a face in a group merged again: their planes as merged, the rest as they were
                    foreach (int brush in affected)
                    {
                        var entry = memory.brushes[brush];
                        if (!entry.blob.IsCreated)
                            continue;
                        ref var exact = ref entry.blob.Value;
                        int offset = mergedPlanes.Length;
                        for (int p = 0; p < exact.planes.Length; p++)
                            mergedPlanes.Add(exact.planes[p]);
                        for (int f = 0; f < entry.faceCount; f++)
                        {
                            int slot = entry.firstFace + f;
                            if (planeOfFace.TryGetValue(slot, out var plane))
                                mergedPlanes[offset + memory.faces[slot].face] = plane;
                        }
                        bool planesChanged = false;
                        for (int p = 0; p < exact.planes.Length && !planesChanged; p++)
                        {
                            var now = exact.planes[p]; var then = mergedPlanes[offset + p];
                            planesChanged = now.a != then.a || now.b != then.b || now.c != then.c || !Int128.Equals(now.w, then.w);
                        }
                        if (!planesChanged)
                        {
                            mergedPlanes.Resize(offset, NativeArrayOptions.UninitializedMemory);
                            continue;
                        }
                        mergedOrders.Add(entry.indexOrder);
                        mergedOffsets.Add(offset);
                        entry.rebuiltByMerge = true;
                        memory.brushes[brush] = entry;
                    }
                    memory.valid.Value = true;
                }
                finally
                {
                    blobs.Dispose();
                    planeOfFace.Dispose();
                    affected.Dispose();
                    points.Dispose();
                    alive.Dispose();
                }
            }
            finally
            {
                rebuilt.Dispose();
                dirty.Dispose();
                seeds.Dispose();
                cells.Dispose();
                newFaces.Dispose();
            }
        }

        static void MergeAllAgain(ref ExactPlaneMergeMemory memory, NativeList<IndexOrder> brushes, NativeList<BlobAssetReference<ExactBrush>> exactBrushCache,
                                  NativeList<IndexOrder> changed,
                                  NativeList<IndexOrder> mergedOrders, NativeList<ExactPlane> mergedPlanes, NativeList<int> mergedOffsets)
        {
            Debug.LogError("Chisel: the plane merge memory lost track of a group of faces; every face is merged again.");
            memory.valid.Value = false;
            MergeAgain(ref memory, brushes, exactBrushCache, changed, mergedOrders, mergedPlanes, mergedOffsets);
        }

        // Takes the brush's faces out of the memory, and out of their rings, so the rings left hold only faces still in
        // it; the faces of their groups become seeds, as their groups can change. False when a ring does not lead back to
        // its face: the memory is broken and has to be made again.
        static bool RemoveFaces(ref ExactPlaneMergeMemory memory, int brush, ref NativeList<int> seeds, ref NativeList<CellKey> cells)
        {
            var entry = memory.brushes[brush];
            for (int f = 0; f < entry.faceCount; f++)
            {
                int slot = entry.firstFace + f;
                if (memory.next[slot] < 0)
                    continue;
                int previous = slot, steps = 0;
                for (int m = memory.next[slot]; m != slot; m = memory.next[m])
                {
                    if (m < 0 || ++steps > memory.faces.Length)
                        return false;
                    seeds.Add(m);
                    previous = m;
                }
                memory.next[previous] = memory.next[slot];
                memory.next[slot]     = -1;
                RemoveFromIndex(memory.cells, memory.faces[slot], slot, ref cells);
            }
            entry.faceCount = 0;
            memory.brushes[brush] = entry;
            return true;
        }
    }

    // What the last plane merge found (ExactPlaneMerge.MergeAgain), kept with the tree so the next one merges again only the
    // groups its edits can have changed: each brush's faces, their groups (as rings: next), and an index of the faces by
    // cell (ExactPlaneMerge.CellKey)
    internal struct ExactPlaneMergeMemory
    {
        public NativeList<ExactPlaneMerge.MemoryBrush>                         brushes;
        public NativeParallelHashMap<CompactNodeID, int>                       brushOf;
        public NativeList<ExactPlaneMerge.Face>                                faces;      // Face.brush: index into brushes
        // the next face of its group, round the group back to itself; -1 for a free slot
        public NativeList<int>                                                 next;
        public NativeParallelMultiHashMap<ExactPlaneMerge.CellKey, int>        cells;
        public NativeReference<int>                                            stamp;
        public NativeReference<int>                                            freeFaces;
        public NativeReference<bool>                                           valid;
        // what the last merge did: merged everything again (full), and how many faces it grouped again
        public NativeReference<bool>                                           lastFull;
        public NativeReference<int>                                            lastMerged;

        public static ExactPlaneMergeMemory Create(Allocator allocator)
        {
            return new ExactPlaneMergeMemory
            {
                brushes    = new NativeList<ExactPlaneMerge.MemoryBrush>(64, allocator),
                brushOf    = new NativeParallelHashMap<CompactNodeID, int>(64, allocator),
                faces      = new NativeList<ExactPlaneMerge.Face>(256, allocator),
                next       = new NativeList<int>(256, allocator),
                cells      = new NativeParallelMultiHashMap<ExactPlaneMerge.CellKey, int>(256, allocator),
                stamp      = new NativeReference<int>(allocator),
                freeFaces  = new NativeReference<int>(allocator),
                valid      = new NativeReference<bool>(allocator),
                lastFull   = new NativeReference<bool>(allocator),
                lastMerged = new NativeReference<int>(allocator)
            };
        }

        public void Clear()
        {
            brushes.Clear();
            brushOf.Clear();
            faces.Clear();
            next.Clear();
            cells.Clear();
            freeFaces.Value = 0;
            valid.Value = false;
        }

        public void Dispose()
        {
            if (brushes.IsCreated)    brushes.Dispose();
            if (brushOf.IsCreated)    brushOf.Dispose();
            if (faces.IsCreated)      faces.Dispose();
            if (next.IsCreated)       next.Dispose();
            if (cells.IsCreated)      cells.Dispose();
            if (stamp.IsCreated)      stamp.Dispose();
            if (freeFaces.IsCreated)  freeFaces.Dispose();
            if (valid.IsCreated)      valid.Dispose();
            if (lastFull.IsCreated)   lastFull.Dispose();
            if (lastMerged.IsCreated) lastMerged.Dispose();
        }
    }

    // Which brushes' planes the merge changes (ExactPlaneMerge.MergeAgain), with their new planes; those are built again,
    // so are added to the brushes this update rebuilds
    [BurstCompile(CompileSynchronously = true)]
    struct ExactPlaneMergeJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                           allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<ExactBrush>>       exactBrushCache;    // by node order

        // Read / Write
        [NoAlias] public NativeList<IndexOrder>                                     rebuildTreeBrushIndexOrders;
        [NoAlias] public ExactPlaneMergeMemory                                      memory;

        // Write
        [NoAlias] public NativeList<IndexOrder>                                     mergedBrushIndexOrders;
        [NoAlias] public NativeList<ExactPlane>                                     mergedPlanes;       // by merged brush, at mergedPlaneOffsets
        [NoAlias] public NativeList<int>                                            mergedPlaneOffsets;

        public void Execute()
        {
            var changed = new NativeList<IndexOrder>(math.max(1, rebuildTreeBrushIndexOrders.Length), Allocator.Temp);
            var rebuilt = new NativeHashSet<int>(math.max(1, rebuildTreeBrushIndexOrders.Length), Allocator.Temp);
            try
            {
                for (int r = 0; r < rebuildTreeBrushIndexOrders.Length; r++)
                {
                    changed.Add(rebuildTreeBrushIndexOrders[r]);
                    rebuilt.Add(rebuildTreeBrushIndexOrders[r].nodeOrder);
                }
                ExactPlaneMerge.MergeAgain(ref memory, allTreeBrushIndexOrders, exactBrushCache, changed,
                                           mergedBrushIndexOrders, mergedPlanes, mergedPlaneOffsets);
                for (int m = 0; m < mergedBrushIndexOrders.Length; m++)
                {
                    if (rebuilt.Add(mergedBrushIndexOrders[m].nodeOrder))
                        rebuildTreeBrushIndexOrders.Add(mergedBrushIndexOrders[m]);
                }
            }
            finally
            {
                changed.Dispose();
                rebuilt.Dispose();
            }
        }
    }

    // The brushes the merge changed, built again with their new planes
    [BurstCompile(CompileSynchronously = true)]
    unsafe struct ExactMergedRebuildJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                           mergedBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<ExactPlane>                           mergedPlanes;
        [NoAlias, ReadOnly] public NativeList<int>                                  mergedPlaneOffsets;

        // Read / Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<int>                                           stats;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<BlobAssetReference<ExactBrush>>                 exactBrushCache;    // by node order
        // the tree's brush generations (ExactBrushes.SetGeneration)
        [NativeDisableUnsafePtrRestriction] public long*                            generationCounter;

        // Write
        [NoAlias, WriteOnly] public NativeList<BlobAssetReference<ExactBrush>>.ParallelWriter disposeList;

        public unsafe void Execute(int index)
        {
            int nodeOrder = mergedBrushIndexOrders[index].nodeOrder;
            int offset = mergedPlaneOffsets[index];
            int count  = (index + 1 < mergedPlaneOffsets.Length ? mergedPlaneOffsets[index + 1] : mergedPlanes.Length) - offset;
            var builder = ExactFaceBuilder.Create();
            var corners = new ExactList<ExactVertex>(32);
            try
            {
                builder.planes.Clear();
                for (int p = 0; p < count; p++)
                    builder.planes.Add(mergedPlanes[offset + p]);
                var previous = exactBrushCache[nodeOrder];
                var brush = ExactBrushes.Rebuild(ref previous.Value, ref builder, ref corners, out var failure);
                ExactBrushes.SetGeneration(brush, previous, generationCounter);
                if (failure != ExactCSGStat.Count)
                {
                    var counters = (int*)stats.GetUnsafePtr();
                    Interlocked.Increment(ref counters[(int)failure]);
                }
                disposeList.AddNoResize(previous);
                exactBrushCache[nodeOrder] = brush;
            }
            finally
            {
                builder.Dispose();
                corners.Dispose();
            }
        }
    }
}
