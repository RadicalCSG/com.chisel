using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
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

        struct ByMinX : System.Collections.Generic.IComparer<Face>
        {
            public int Compare(Face x, Face y) => x.min.x.CompareTo(y.min.x);
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

        static int Find(NativeArray<int> parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        // Faces are only compared within a cell of rounded planes: kCellShift bits of each of a, b, c and of the offset in
        // steps, wide enough that most faces of one plane share a cell
        const int  kCellShift = 6;
        const long kCellMask  = (1L << kCellShift) - 1;
        const int  kOffsetShift = 20;       // one offset step of a rounded plane's w is 2^20 (kOffsetStep)

        // A face in a cell of planes, with its box along the axis the cell is swept on
        struct CellEntry
        {
            public long x, y, z, w;     // the cell
            public double lo, hi;
            public int face;
        }

        struct ByCellThenLo : System.Collections.Generic.IComparer<CellEntry>
        {
            public int Compare(CellEntry p, CellEntry q)
            {
                if (p.x != q.x) return p.x.CompareTo(q.x);
                if (p.y != q.y) return p.y.CompareTo(q.y);
                if (p.z != q.z) return p.z.CompareTo(q.z);
                if (p.w != q.w) return p.w.CompareTo(q.w);
                return p.lo.CompareTo(q.lo);
            }
        }

        // The axis a cell's faces are swept on: the one its planes' normals lean along least, so the faces spread along it
        static int SweepAxis(long x, long y, long z)
        {
            long ax = math.abs(x), ay = math.abs(y), az = math.abs(z);
            if (ax <= ay && ax <= az) return 0;
            return ay <= az ? 1 : 2;
        }

        // The cells a value of a plane goes in: its own, and the one next to it when it lies on the cell's edge, since a
        // plane one rounding step off (OnePlane) may lie across it. The edges lie half a cell off the multiples of the
        // cell's width, so the round values of axis-aligned planes (0, a whole normal, whole offsets) are not on one.
        static int Cells(long value, out long first)
        {
            value += 1L << (kCellShift - 1);
            long cell = value >> kCellShift;
            long within = value & kCellMask;
            first = within == 0 ? cell - 1 : cell;
            return within == 0 || within == kCellMask ? 2 : 1;
        }

        static void AddEntries(ref NativeList<CellEntry> entries, in Face face, int index, long a, long b, long c, long offset)
        {
            int nx = Cells(a, out long x0), ny = Cells(b, out long y0), nz = Cells(c, out long z0), nw = Cells(offset, out long w0);
            for (long x = x0; x < x0 + nx; x++)
            for (long y = y0; y < y0 + ny; y++)
            for (long z = z0; z < z0 + nz; z++)
            for (long w = w0; w < w0 + nw; w++)
            {
                int axis = SweepAxis(x, y, z);
                entries.Add(new CellEntry { x = x, y = y, z = z, w = w, lo = face.min[axis], hi = face.max[axis], face = index });
            }
        }

        // Joins the faces of different brushes whose boxes meet and that are one plane (OnePlane) into groups: parent[i]
        // ends up leading, through Find, to the smallest face of i's group. Returns how many pairs of faces it compared.
        //
        // Two faces that are one plane have keys (rounded planes, ExactPlane.Quantize) whose a, b, c and offset in steps
        // differ by at most one, facing the same way or the other. Each face goes in the cells of its key facing both ways
        // (Cells), so two such faces always share a cell, and only faces in one cell are compared, by a sweep along the
        // cell's axis: the same pairs are joined as by comparing every pair.
        public static long Unite(NativeList<Face> faces, NativeArray<int> parent)
        {
            for (int i = 0; i < faces.Length; i++)
                parent[i] = i;
            var entries = new NativeList<CellEntry>(faces.Length * 3, Allocator.Temp);
            long compared = 0;
            try
            {
                for (int i = 0; i < faces.Length; i++)
                {
                    var key = faces[i].key;
                    // |w| of a rounded plane is below 2^58 (ExactPlane.kMaxDistance kOffsetScale steps), so it is a long
                    long w = (long)key.w.lo;
                    AddEntries(ref entries, faces[i], i,  key.a,  key.b,  key.c,  w >> kOffsetShift);
                    AddEntries(ref entries, faces[i], i, -key.a, -key.b, -key.c, (-w) >> kOffsetShift);
                }
                entries.Sort(new ByCellThenLo());

                for (int start = 0; start < entries.Length;)
                {
                    int end = start + 1;
                    var first = entries[start];
                    while (end < entries.Length && entries[end].x == first.x && entries[end].y == first.y &&
                           entries[end].z == first.z && entries[end].w == first.w)
                        end++;
                    for (int i = start; i < end; i++)
                    {
                        var ei = entries[i];
                        for (int j = i + 1; j < end && entries[j].lo <= ei.hi + kTouch; j++)
                        {
                            compared++;
                            if (entries[j].face != ei.face)
                                Join(faces, parent, ei.face, entries[j].face);
                        }
                    }
                    start = end;
                }
            }
            finally
            {
                entries.Dispose();
            }
            return compared;
        }

        // Joins the groups of faces i and j when they are of different brushes, their boxes meet and they are one plane
        static void Join(NativeList<Face> faces, NativeArray<int> parent, int i, int j)
        {
            var fi = faces[i];
            var fj = faces[j];
            if (fi.brush == fj.brush)
                return;
            if (math.any(fj.min > fi.max + kTouch) || math.any(fi.min > fj.max + kTouch))
                return;
            if (!OnePlane(fi.key, fj.key))
                return;
            int ri = Find(parent, i), rj = Find(parent, j);
            if (ri != rj)
                parent[math.max(ri, rj)] = math.min(ri, rj);
        }

        // The planes each brush keeps: brushes[b]'s planes at offsets[b] in planes, one for each of its own. A brush
        // without volume keeps its own.
        public static void Merge(NativeArray<BlobAssetReference<ExactBrush>> brushes, NativeList<ExactPlane> planes, NativeList<int> offsets)
        {
            planes.Clear();
            offsets.Clear();
            var faces = new NativeList<Face>(math.max(1, brushes.Length * 6), Allocator.Temp);
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
                    if (brush.cornerCount <= 0)
                        continue;
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
                        faces.Add(new Face { brush = b, face = f, key = brush.floatPlanes[f], min = min, max = max, extent = math.lengthsq(max - min) });
                    }
                }
                if (faces.Length < 2)
                    return;
                faces.Sort(new ByMinX());

                var parent = new NativeArray<int>(faces.Length, Allocator.Temp);
                Unite(faces, parent);

                // the faces of each group, one group after the other
                var starts  = new NativeArray<int>(faces.Length + 1, Allocator.Temp);
                var cursor  = new NativeArray<int>(faces.Length, Allocator.Temp);
                var members = new NativeArray<int>(faces.Length, Allocator.Temp);
                for (int i = 0; i < faces.Length; i++)
                {
                    parent[i] = Find(parent, i);
                    starts[parent[i] + 1]++;
                }
                for (int i = 0; i < faces.Length; i++)
                    starts[i + 1] += starts[i];
                for (int i = 0; i < faces.Length; i++)
                    cursor[i] = starts[i];
                for (int i = 0; i < faces.Length; i++)
                    members[cursor[parent[i]]++] = i;

                // every face of a group takes one plane: through the group's snapped corners (ExactBrushes.PlaneThrough,
                // which depends only on which corners there are), or, where those don't make one, the largest face's own
                var points = new ExactList<ExactPoint>(64);
                try
                {
                    for (int root = 0; root < faces.Length; root++)
                    {
                        int first = starts[root], last = starts[root + 1];
                        if (last - first < 2)
                            continue;
                        points.Clear();
                        int largest = -1;
                        for (int m = first; m < last; m++)
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
                        for (int m = first; m < last; m++)
                        {
                            var face = faces[members[m]];
                            planes[offsets[face.brush] + face.face] = Facing(plane, brushes[face.brush].Value.ownPlanes[face.face]);
                        }
                    }
                }
                finally
                {
                    points.Dispose();
                }
                parent.Dispose();
                starts.Dispose();
                cursor.Dispose();
                members.Dispose();
            }
            finally
            {
                faces.Dispose();
            }
        }
    }

    // Which brushes' planes the merge changes (ExactPlaneMerge.Merge), with their new planes; those are built again, so
    // are added to the brushes this update rebuilds
    [BurstCompile(CompileSynchronously = true)]
    struct ExactPlaneMergeJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                           allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<BlobAssetReference<ExactBrush>>       exactBrushCache;    // by node order

        // Read / Write
        [NoAlias] public NativeList<IndexOrder>                                     rebuildTreeBrushIndexOrders;

        // Write
        [NoAlias] public NativeList<IndexOrder>                                     mergedBrushIndexOrders;
        [NoAlias] public NativeList<ExactPlane>                                     mergedPlanes;       // by merged brush, at mergedPlaneOffsets
        [NoAlias] public NativeList<int>                                            mergedPlaneOffsets;

        public void Execute()
        {
            mergedBrushIndexOrders.Clear();
            mergedPlanes.Clear();
            mergedPlaneOffsets.Clear();
            int brushCount = allTreeBrushIndexOrders.Length;
            var brushes = new NativeArray<BlobAssetReference<ExactBrush>>(brushCount, Allocator.Temp);
            var planes  = new NativeList<ExactPlane>(math.max(1, brushCount * 6), Allocator.Temp);
            var offsets = new NativeList<int>(math.max(1, brushCount), Allocator.Temp);
            var rebuilt = new NativeHashSet<int>(math.max(1, rebuildTreeBrushIndexOrders.Length), Allocator.Temp);
            try
            {
                for (int b = 0; b < brushCount; b++)
                    brushes[b] = exactBrushCache[allTreeBrushIndexOrders[b].nodeOrder];
                ExactPlaneMerge.Merge(brushes, planes, offsets);
                for (int r = 0; r < rebuildTreeBrushIndexOrders.Length; r++)
                    rebuilt.Add(rebuildTreeBrushIndexOrders[r].nodeOrder);

                for (int b = 0; b < brushCount; b++)
                {
                    if (!brushes[b].IsCreated)
                        continue;
                    ref var brush = ref brushes[b].Value;
                    int offset = offsets[b];
                    int count  = (b + 1 < brushCount ? offsets[b + 1] : planes.Length) - offset;
                    bool changed = count != brush.planes.Length;
                    for (int p = 0; p < count && !changed; p++)
                    {
                        var now = brush.planes[p]; var then = planes[offset + p];
                        changed = now.a != then.a || now.b != then.b || now.c != then.c || !Int128.Equals(now.w, then.w);
                    }
                    if (!changed)
                        continue;
                    mergedBrushIndexOrders.Add(allTreeBrushIndexOrders[b]);
                    mergedPlaneOffsets.Add(mergedPlanes.Length);
                    for (int p = 0; p < count; p++)
                        mergedPlanes.Add(planes[offset + p]);
                    if (rebuilt.Add(allTreeBrushIndexOrders[b].nodeOrder))
                        rebuildTreeBrushIndexOrders.Add(allTreeBrushIndexOrders[b]);
                }
            }
            finally
            {
                brushes.Dispose();
                planes.Dispose();
                offsets.Dispose();
                rebuilt.Dispose();
            }
        }
    }

    // The brushes the merge changed, built again with their new planes
    [BurstCompile(CompileSynchronously = true)]
    struct ExactMergedRebuildJob : IJobParallelForDefer
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
