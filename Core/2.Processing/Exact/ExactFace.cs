using System;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{

    struct ExactPolygonEdge
    {
        public ExactPlane  plane;
        public int         planeId;    // index into the builder's plane table; orientation is not part of identity
        public ExactVertex start;
    }

    struct ExactPolygonRange
    {
        public int offset;
        public int count;
        public bool IsEmpty { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => count < 3; }
    }

    // A brush touching the face's brush, as the face builder sees it.
    struct ExactTouchingBrush
    {
        public int  planeOffset;        // into the builder's plane table
        public int  planeCount;
        public int  nodeOrder;          // which brush, for the caller's bookkeeping
        public byte insideCategory;
        public byte alignedCategory;
        public byte reverseAlignedCategory;
        public bool routes;             // whether it has a lookup in the routing table
    }

    // A point on an output edge, with the exact value the triangulator works on and the float it is emitted as.
    struct ExactOutputVertex
    {
        public ExactVertex vertex;
        public float       x, y, z;
    }

    // An output edge: the drawn region is on its left, seen from the face normal. It lies on lines[line], and runs along
    // n(face) x n(line) when forward is 1, against it when 0.
    struct ExactOutputEdge
    {
        public int  from, to;
        public int  line;
        public byte forward;
    }

    struct ExactLine
    {
        public ExactPlane plane;        // representative: points on the line are (face, plane, other)
        public int        planeId;
    }

    // An edge of region `owner` (0 is the face itself) lies on line `line`; orientation +1 when the owner's edge plane has
    // the same inside as the line's representative, -1 when the opposite.
    struct ExactLineOwner
    {
        public int line;
        public int owner;
        public int orientation;
    }

    struct ExactRegion
    {
        public ExactPolygonRange polygon;
        public int               brush;     // index into the touching brushes; -1 for region 0, the face
        public byte              category;  // what the covered part carries into routing
    }

    struct ExactLineEvent
    {
        public ExactVertex point;
        public ExactPlane  plane;
        public int         planeId;
        public int         directionSign;
        public int         rank;        // position after sorting; equal points share a rank
        public int         vertexIndex; // output vertex, -1 until one is needed
    }

    // The interval of the current line inside one brush and the face: event indices, or -1 when there is none.
    struct ExactInterval
    {
        public int entry, exit;
        public bool IsEmpty { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => entry < 0; }
    }

    enum ExactFaceFailure : byte
    {
        None,
        InvalidPlane,           // a plane of the face's brush could not be represented (degenerate or out of range)
        FaceOutsideBounds,      // the brush's planes do not close the face within the representable world (+-2^24)
        DegenerateVertex,       // three planes that had to meet in a point did not
    }

    // Scratch memory for building faces, reused from face to face; one per job execution.
    struct ExactFaceBuilder : IDisposable
    {
        // Values per routing row: one destination per category (CategoryRoutingRow.Length).
        public const int kCategoryCount = (int)CategoryIndex.LastCategory + 1;

        // Plane table for the whole brush update: the brush's own face planes first, then every touching brush's, then,
        // for the duration of one face, its four bounding planes. A plane's id is its index here.
        public ExactList<ExactPlane>        planes;

        public ExactList<ExactPolygonEdge>  polygonPool;
        public ExactList<int>               signs;
        public ExactList<ExactRegion>       regions;
        public ExactList<ExactLine>         lines;
        public ExactList<ExactLineOwner>    owners;
        public ExactList<ExactLineEvent>    events;
        public ExactList<int>               order;
        public ExactList<ExactInterval>     intervals;          // per touching brush, for the current line
        public ExactList<int>               lookupRegion;       // per routing lookup: region index or -1
        public ExactList<int>               regionOrientation;  // per region, for the current line
        public ExactList<byte>              leftInside;         // per region: membership of the left side
        public ExactList<byte>              rightInside;

        public ExactList<ExactOutputVertex> vertices;
        public ExactList<ExactOutputEdge>   alignedEdges;       // SelfAligned
        public ExactList<ExactOutputEdge>   reverseAlignedEdges;// SelfReverseAligned

        public int  routingErrors;

        public static ExactFaceBuilder Create()
        {
            return new ExactFaceBuilder
            {
                planes              = new ExactList<ExactPlane>(64),
                polygonPool         = new ExactList<ExactPolygonEdge>(256),
                signs               = new ExactList<int>(64),
                regions             = new ExactList<ExactRegion>(16),
                lines               = new ExactList<ExactLine>(32),
                owners              = new ExactList<ExactLineOwner>(64),
                events              = new ExactList<ExactLineEvent>(64),
                order               = new ExactList<int>(64),
                intervals           = new ExactList<ExactInterval>(16),
                lookupRegion        = new ExactList<int>(16),
                regionOrientation   = new ExactList<int>(16),
                leftInside          = new ExactList<byte>(16),
                rightInside         = new ExactList<byte>(16),
                vertices            = new ExactList<ExactOutputVertex>(64),
                alignedEdges        = new ExactList<ExactOutputEdge>(64),
                reverseAlignedEdges = new ExactList<ExactOutputEdge>(16),
            };
        }

        public void Dispose()
        {
            planes.Dispose(); polygonPool.Dispose(); signs.Dispose(); regions.Dispose(); lines.Dispose(); owners.Dispose();
            events.Dispose(); order.Dispose(); intervals.Dispose(); lookupRegion.Dispose(); regionOrientation.Dispose();
            leftInside.Dispose(); rightInside.Dispose(); vertices.Dispose(); alignedEdges.Dispose(); reverseAlignedEdges.Dispose();
        }

        // ---- convex polygons --------------------------------------------------------------------------------------

        // Side of a polygon vertex, knowing which planes made it: a vertex lies on the planes it was made from, and saying
        // so without arithmetic is both faster and the answer arithmetic would give.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int VertexSide(in ExactPlane plane, int planeId, in ExactVertex vertex, int previousId, int currentId)
        {
            if (planeId == previousId || planeId == currentId)
                return 0;
            return ExactPredicates.Side(plane, vertex);
        }

        public ExactPolygonRange BoundingQuad(in ExactPlane face, int firstBoundId, ref ExactFaceFailure failure)
        {
            int dominant = face.DominantAxis();
            long dominantComponent = dominant == 0 ? face.a : (dominant == 1 ? face.b : face.c);
            ExactPredicates.ProjectionAxes(dominant, dominantComponent < 0, out int u, out int v);

            var bottom = WorldPlane(v, -1);
            var right  = WorldPlane(u, +1);
            var top    = WorldPlane(v, +1);
            var left   = WorldPlane(u, -1);
            var offset = polygonPool.Length;
            AddEdge(face, left,   bottom, firstBoundId + 0, ref failure);
            AddEdge(face, bottom, right,  firstBoundId + 1, ref failure);
            AddEdge(face, right,  top,    firstBoundId + 2, ref failure);
            AddEdge(face, top,    left,   firstBoundId + 3, ref failure);
            return new ExactPolygonRange { offset = offset, count = 4 };
        }

        static ExactPlane WorldPlane(int axis, int sign)
        {
            long s = sign * ExactPlane.kMaxNormal;
            return new ExactPlane(axis == 0 ? s : 0, axis == 1 ? s : 0, axis == 2 ? s : 0, -ExactPlane.kMaxW);
        }

        void AddEdge(in ExactPlane face, in ExactPlane previous, in ExactPlane plane, int planeId, ref ExactFaceFailure failure)
        {
            var start = ExactVertex.Intersect(face, previous, plane);
            if (!start.IsValid)
                failure = ExactFaceFailure.DegenerateVertex;
            polygonPool.Add(new ExactPolygonEdge { plane = plane, planeId = planeId, start = start });
        }

        public ExactPolygonRange Clip(ExactPolygonRange polygon, in ExactPlane face, in ExactPlane plane, int planeId, ref ExactFaceFailure failure)
        {
            if (polygon.IsEmpty)
                return polygon;
            int n = polygon.count;
            signs.Clear();
            int negative = 0, positive = 0;
            for (int j = 0; j < n; j++)
            {
                ref var edge = ref polygonPool[polygon.offset + j];
                int previousId = polygonPool[polygon.offset + (j + n - 1) % n].planeId;
                int s = VertexSide(plane, planeId, edge.start, previousId, edge.planeId);
                signs.Add(s);
                if (s < 0) negative++; else if (s > 0) positive++;
            }
            if (positive == 0)
                return polygon;
            if (negative == 0)
                return default;

            int exit = -1;
            for (int j = 0; j < n; j++)
            {
                int sj = signs[j], sn = signs[(j + 1) % n], snn = signs[(j + 2) % n];
                if (!(sj < 0 || sn < 0))
                    continue;
                bool nextKept = sn < 0 || snn < 0;
                if (sn > 0 || (sn == 0 && !nextKept))
                {
                    exit = j;
                    break;
                }
            }
            if (exit < 0)
            {
                failure = ExactFaceFailure.DegenerateVertex;
                return default;
            }
            int entry = (exit + 1) % n;
            while (!(signs[entry] < 0 || signs[(entry + 1) % n] < 0))
                entry = (entry + 1) % n;

            var offset    = polygonPool.Length;
            var exitPlane = polygonPool[polygon.offset + exit].plane;
            var cutStart  = ExactVertex.Intersect(face, exitPlane, plane);
            if (!cutStart.IsValid) failure = ExactFaceFailure.DegenerateVertex;
            polygonPool.Add(new ExactPolygonEdge { plane = plane, planeId = planeId, start = cutStart });

            var entryEdge  = polygonPool[polygon.offset + entry];
            var entryStart = ExactVertex.Intersect(face, plane, entryEdge.plane);
            if (!entryStart.IsValid) failure = ExactFaceFailure.DegenerateVertex;
            polygonPool.Add(new ExactPolygonEdge { plane = entryEdge.plane, planeId = entryEdge.planeId, start = entryStart });

            // the kept edges after the entry edge, up to and including the exit edge, keep their start vertices
            for (int j = (entry + 1) % n; entry != exit; j = (j + 1) % n)
            {
                polygonPool.Add(polygonPool[polygon.offset + j]);
                if (j == exit)
                    break;
            }
            return new ExactPolygonRange { offset = offset, count = polygonPool.Length - offset };
        }

        // ---- the face ---------------------------------------------------------------------------------------------

        public ExactFaceFailure BuildFace(int faceId, int faceCount,
                                          ref ExactList<ExactTouchingBrush> touching,
                                          ref ExactList<ushort> routingRows,
                                          ref ExactList<int> lookupStart, ref ExactList<int> lookupEnd,
                                          ref ExactList<int> lookupBrush)
        {
            var failure = ExactFaceFailure.None;
            vertices.Clear(); alignedEdges.Clear(); reverseAlignedEdges.Clear();
            regions.Clear(); lines.Clear(); owners.Clear(); polygonPool.Clear();

            var face = planes[faceId];
            if (!face.IsValid)
                return ExactFaceFailure.InvalidPlane;

            int boundBase = planes.Length;
            failure = BuildRegions(faceId, faceCount, boundBase, ref touching);
            if (failure != ExactFaceFailure.None || regions.Length == 0)
            {
                planes.Resize(boundBase);
                return failure;
            }

            // Routing lookups to regions.
            lookupRegion.Clear();
            for (int k = 0; k < lookupBrush.Length; k++)
            {
                int region = -1;
                int b = lookupBrush[k];
                if (b >= 0)
                {
                    for (int r = 1; r < regions.Length; r++)
                        if (regions[r].brush == b) { region = r; break; }
                }
                lookupRegion.Add(region);
            }

            // Every distinct line an edge of the face or of a region lies on, and who owns an edge there.
            for (int r = 0; r < regions.Length; r++)
            {
                var polygon = regions[r].polygon;
                for (int e = 0; e < polygon.count; e++)
                {
                    ref var edge = ref polygonPool[polygon.offset + e];
                    int line = -1, orientation = 0;
                    for (int l = 0; l < lines.Length; l++)
                    {
                        orientation = lines[l].planeId == edge.planeId ? ExactPredicates.SamePlane(lines[l].plane, edge.plane)
                                                                       : ExactPredicates.SameLine(face, lines[l].plane, edge.plane);
                        if (orientation != 0) { line = l; break; }
                    }
                    if (line < 0)
                    {
                        line = lines.Length;
                        orientation = 1;
                        lines.Add(new ExactLine { plane = edge.plane, planeId = edge.planeId });
                    }
                    owners.Add(new ExactLineOwner { line = line, owner = r, orientation = orientation });
                }
            }

            leftInside.Resize(regions.Length);
            rightInside.Resize(regions.Length);
            regionOrientation.Resize(regions.Length);
            for (int l = 0; l < lines.Length; l++)
            {
                var result = ProcessLine(l, faceId, ref touching, ref routingRows, ref lookupStart, ref lookupEnd);
                if (result != ExactFaceFailure.None) { failure = result; break; }
            }
            planes.Resize(boundBase);
            return failure;
        }

        public ExactFaceFailure FacePolygon(int faceId, int faceCount, int boundBase, out ExactPolygonRange polygon)
        {
            polygon = default;
            var failure = ExactFaceFailure.None;
            var face = planes[faceId];
            var quad = BoundingQuad(face, boundBase, ref failure);
            for (int e = 0; e < 4; e++)
                planes.Add(polygonPool[quad.offset + e].plane);

            var current = quad;
            for (int p = 0; p < faceCount && !current.IsEmpty; p++)
            {
                if (p == faceId)
                    continue;
                var other = planes[p];
                if (!other.IsValid)
                    return ExactFaceFailure.InvalidPlane;
                int same = ExactPredicates.SamePlane(face, other);
                if (same > 0 && p < faceId) return ExactFaceFailure.None;
                if (same < 0) return ExactFaceFailure.None;
                if (same > 0) continue;
                current = Clip(current, face, other, p, ref failure);
            }
            if (failure != ExactFaceFailure.None || current.IsEmpty)
                return failure;
            for (int e = 0; e < current.count; e++)
            {
                if (polygonPool[current.offset + e].planeId >= boundBase)
                    return ExactFaceFailure.FaceOutsideBounds;
            }
            polygon = current;
            return ExactFaceFailure.None;
        }

        public ExactFaceFailure PolytopeCorners(ref ExactList<ExactVertex> corners)
        {
            int first = corners.Length;
            int faceCount = planes.Length;
            var failure = ExactFaceFailure.None;
            for (int f = 0; f < faceCount; f++)
            {
                polygonPool.Clear();
                var result = FacePolygon(f, faceCount, faceCount, out var polygon);
                planes.Resize(faceCount);
                if (result != ExactFaceFailure.None)
                {
                    if (failure == ExactFaceFailure.None)
                        failure = result;
                    continue;
                }
                for (int e = 0; e < polygon.count; e++)
                {
                    var start = polygonPool[polygon.offset + e].start;
                    bool known = false;
                    for (int c = first; c < corners.Length && !known; c++)
                        known = ExactPredicates.SamePoint(corners[c], start);
                    if (!known)
                        corners.Add(start);
                }
            }
            polygonPool.Clear();
            return failure;
        }

        // Region 0 is the face; after it, one region per routing touching brush that covers part of the face.
        ExactFaceFailure BuildRegions(int faceId, int faceCount, int boundBase, ref ExactList<ExactTouchingBrush> touching)
        {
            var failure = FacePolygon(faceId, faceCount, boundBase, out var polygon);
            if (failure != ExactFaceFailure.None || polygon.IsEmpty)
                return failure;
            var face = planes[faceId];
            regions.Add(new ExactRegion { polygon = polygon, brush = -1, category = 0 });

            for (int t = 0; t < touching.Length; t++)
            {
                ref var brush = ref touching[t];
                if (!brush.routes)
                    continue;
                // Face to face with this face? Then it lies on one side of it and only its other planes cut the face.
                int coplanar = 0;
                for (int p = 0; p < brush.planeCount; p++)
                {
                    int same = ExactPredicates.SamePlane(face, planes[brush.planeOffset + p]);
                    if (same == 0) continue;
                    if (coplanar != 0 && coplanar != same) { coplanar = 2; break; }   // flat brush: covers nothing
                    coplanar = same;
                }
                if (coplanar == 2)
                    continue;
                var region = regions[0].polygon;
                for (int p = 0; p < brush.planeCount && !region.IsEmpty; p++)
                {
                    int id = brush.planeOffset + p;
                    var plane = planes[id];
                    if (ExactPredicates.SamePlane(face, plane) != 0)
                        continue;
                    region = Clip(region, face, plane, id, ref failure);
                }
                if (failure != ExactFaceFailure.None)
                    return failure;
                if (region.IsEmpty)
                    continue;
                var category = coplanar > 0 ? brush.alignedCategory : (coplanar < 0 ? brush.reverseAlignedCategory : brush.insideCategory);
                regions.Add(new ExactRegion { polygon = region, brush = t, category = category });
            }
            return ExactFaceFailure.None;
        }

        // ---- one line ---------------------------------------------------------------------------------------------

        // A candidate end of an interval: kept outside the event list until the interval is known not to be empty, so an
        // event is only ever a point where the line really enters or leaves something.
        struct Candidate
        {
            public ExactVertex point;
            public ExactPlane  plane;
            public int         planeId;
            public int         directionSign;
            public bool        valid;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int CompareCandidates(in Candidate a, in Candidate b)
        {
            if (a.planeId == b.planeId)
                return 0;
            return ExactPredicates.CompareAlongLine(a.point, b.plane, b.directionSign);
        }

        bool Bounds(in ExactPlane face, in ExactLine line, int offset, int count, bool skipLinePlanes, in ExactVertex reference,
                    ref Candidate entry, ref Candidate exit, ref ExactFaceFailure failure)
        {
            for (int p = 0; p < count; p++)
            {
                int id = offset + p;
                var plane = planes[id];
                int direction = ExactPredicates.DirectionSign(plane, face, line.plane);
                if (direction == 0)
                {
                    // parallel to the line: the line lies in it (no constraint), or wholly on one side of it
                    if (skipLinePlanes && ExactPredicates.SameLine(face, line.plane, plane) != 0)
                        continue;
                    if (ExactPredicates.Side(plane, reference) > 0)
                        return false;
                    continue;
                }
                var point = ExactVertex.Intersect(face, line.plane, plane);
                if (!point.IsValid) { failure = ExactFaceFailure.DegenerateVertex; continue; }
                var candidate = new Candidate { point = point, plane = plane, planeId = id, directionSign = direction, valid = true };
                if (direction < 0)
                {
                    if (!entry.valid || CompareCandidates(candidate, entry) > 0)
                        entry = candidate;
                }
                else
                {
                    if (!exit.valid || CompareCandidates(candidate, exit) < 0)
                        exit = candidate;
                }
            }
            return true;
        }

        int AddEvent(in Candidate candidate)
        {
            events.Add(new ExactLineEvent
            {
                point = candidate.point, plane = candidate.plane, planeId = candidate.planeId,
                directionSign = candidate.directionSign, vertexIndex = -1
            });
            return events.Length - 1;
        }

        ExactFaceFailure ProcessLine(int lineIndex, int faceId,
                                     ref ExactList<ExactTouchingBrush> touching,
                                     ref ExactList<ushort> routingRows,
                                     ref ExactList<int> lookupStart, ref ExactList<int> lookupEnd)
        {
            var failure = ExactFaceFailure.None;
            var face = planes[faceId];
            var line = lines[lineIndex];
            events.Clear();

            var facePolygon = regions[0].polygon;
            var faceEntry = new Candidate(); var faceExit = new Candidate();
            for (int e = 0; e < facePolygon.count; e++)
            {
                ref var edge = ref polygonPool[facePolygon.offset + e];
                int direction = ExactPredicates.DirectionSign(edge.plane, face, line.plane);
                if (direction == 0)
                    continue;
                var point = ExactVertex.Intersect(face, line.plane, edge.plane);
                if (!point.IsValid) return ExactFaceFailure.DegenerateVertex;
                var candidate = new Candidate { point = point, plane = edge.plane, planeId = edge.planeId, directionSign = direction, valid = true };
                if (direction < 0) { if (!faceEntry.valid || CompareCandidates(candidate, faceEntry) > 0) faceEntry = candidate; }
                else               { if (!faceExit.valid  || CompareCandidates(candidate, faceExit)  < 0) faceExit  = candidate; }
            }
            if (!faceEntry.valid || !faceExit.valid)
                return ExactFaceFailure.None;       // the face is bounded, so this means the line misses it
            for (int e = 0; e < facePolygon.count; e++)
            {
                ref var edge = ref polygonPool[facePolygon.offset + e];
                if (ExactPredicates.DirectionSign(edge.plane, face, line.plane) != 0)
                    continue;
                if (ExactPredicates.SameLine(face, line.plane, edge.plane) != 0)
                    continue;
                if (ExactPredicates.Side(edge.plane, faceEntry.point) > 0)
                    return ExactFaceFailure.None;   // the line runs outside the face
            }
            if (CompareCandidates(faceEntry, faceExit) >= 0)
                return ExactFaceFailure.None;       // touches the face in one point at most: no segment
            var faceInterval = new ExactInterval { entry = AddEvent(faceEntry), exit = AddEvent(faceExit) };

            // Every touching brush's interval on the line, within the face. These are the points every face along this
            // line agrees on.
            intervals.Clear();
            for (int t = 0; t < touching.Length; t++)
            {
                ref var brush = ref touching[t];
                var entry = faceEntry; var exit = faceExit;
                bool meets = Bounds(face, line, brush.planeOffset, brush.planeCount, false, faceEntry.point, ref entry, ref exit, ref failure);
                if (failure != ExactFaceFailure.None)
                    return failure;
                // Bounds only ever moves the entry later and the exit earlier, so this is the interval within the face.
                if (!meets || CompareCandidates(entry, exit) > 0)
                {
                    intervals.Add(new ExactInterval { entry = -1, exit = -1 });
                    continue;
                }
                int entryEvent = entry.planeId == faceEntry.planeId ? faceInterval.entry : AddEvent(entry);
                int exitEvent  = exit.planeId  == faceExit.planeId  ? faceInterval.exit  : AddEvent(exit);
                intervals.Add(new ExactInterval { entry = entryEvent, exit = exitEvent });
            }

            // Sort the events along the line and give equal points one rank.
            order.Clear();
            for (int i = 0; i < events.Length; i++)
                order.Add(i);
            for (int i = 1; i < order.Length; i++)
            {
                int item = order[i];
                int j = i - 1;
                while (j >= 0 && CompareEvents(item, order[j]) < 0)
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = item;
            }
            int rank = 0;
            for (int i = 0; i < order.Length; i++)
            {
                if (i > 0 && CompareEvents(order[i], order[i - 1]) != 0)
                    rank++;
                events[order[i]].rank = rank;
            }
            int faceEntryRank = events[faceInterval.entry].rank;
            int faceExitRank  = events[faceInterval.exit].rank;

            // Which side of this line each region lies on, where it has an edge here (0: no edge on this line).
            for (int r = 0; r < regions.Length; r++)
                regionOrientation[r] = 0;
            for (int o = 0; o < owners.Length; o++)
                if (owners[o].line == lineIndex)
                    regionOrientation[owners[o].owner] = owners[o].orientation;
            // The face's own edge on this line puts the face on one side only; the other side is not surface at all.
            bool leftInFace  = regionOrientation[0] >= 0;
            bool rightInFace = regionOrientation[0] <= 0;

            // The elementary segments inside the face, in order along the line (whose negative side is its left).
            for (int i = 0; i + 1 < order.Length; i++)
            {
                int a = order[i], b = order[i + 1];
                int rankA = events[a].rank, rankB = events[b].rank;
                if (rankA == rankB || rankA < faceEntryRank || rankB > faceExitRank)
                    continue;

                for (int r = 1; r < regions.Length; r++)
                {
                    var interval = intervals[regions[r].brush];
                    bool covers = !interval.IsEmpty && events[interval.entry].rank <= rankA && events[interval.exit].rank >= rankB;
                    int orientation = regionOrientation[r];
                    // a region with an edge on this line lies on the side where that edge plane is negative
                    leftInside[r]  = (byte)(covers && orientation >= 0 ? 1 : 0);
                    rightInside[r] = (byte)(covers && orientation <= 0 ? 1 : 0);
                }

                int leftCategory  = leftInFace  ? Route(ref leftInside,  ref routingRows, ref lookupStart, ref lookupEnd) : -1;
                int rightCategory = rightInFace ? Route(ref rightInside, ref routingRows, ref lookupStart, ref lookupEnd) : -1;
                if (leftCategory == rightCategory)
                    continue;
                Emit(leftCategory, rightCategory, (int)CategoryIndex.SelfAligned,        a, b, lineIndex, ref alignedEdges);
                Emit(leftCategory, rightCategory, (int)CategoryIndex.SelfReverseAligned, a, b, lineIndex, ref reverseAlignedEdges);
            }
            return failure;
        }

        // Order of two events along the current line.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int CompareEvents(int i, int j)
        {
            ref var ei = ref events[i];
            ref var ej = ref events[j];
            if (ei.planeId == ej.planeId)
                return 0;
            return ExactPredicates.CompareAlongLine(ei.point, ej.plane, ej.directionSign);
        }

        // A drawn category on exactly one side makes the segment an edge, running so the drawn side is on its left.
        void Emit(int leftCategory, int rightCategory, int category, int a, int b, int line, ref ExactList<ExactOutputEdge> edges)
        {
            bool left = leftCategory == category, right = rightCategory == category;
            if (left == right)
                return;
            int from = VertexOf(a), to = VertexOf(b);
            if (left)
                edges.Add(new ExactOutputEdge { from = from, to = to, line = line, forward = 1 });
            else
                edges.Add(new ExactOutputEdge { from = to, to = from, line = line, forward = 0 });
        }

        int VertexOf(int eventIndex)
        {
            ref var e = ref events[eventIndex];
            if (e.vertexIndex >= 0)
                return e.vertexIndex;
            // an event at the same point on this line may already have one
            for (int i = 0; i < events.Length; i++)
            {
                if (i != eventIndex && events[i].rank == e.rank && events[i].vertexIndex >= 0)
                {
                    e.vertexIndex = events[i].vertexIndex;
                    return e.vertexIndex;
                }
            }
            float x = ExactPredicates.RoundToFloat(e.point.X, e.point.W);
            float y = ExactPredicates.RoundToFloat(e.point.Y, e.point.W);
            float z = ExactPredicates.RoundToFloat(e.point.Z, e.point.W);
            // the same point reached along another line: equal points round to equal floats, so only those are compared
            for (int v = 0; v < vertices.Length; v++)
            {
                ref var existing = ref vertices[v];
                if (existing.x != x || existing.y != y || existing.z != z)
                    continue;
                if (ExactPredicates.SamePoint(existing.vertex, e.point))
                {
                    e.vertexIndex = v;
                    return v;
                }
            }
            e.vertexIndex = vertices.Length;
            vertices.Add(new ExactOutputVertex { vertex = e.point, x = x, y = y, z = z });
            return e.vertexIndex;
        }

        // The category routing gives a piece of the face, from its membership in each region.
        int Route(ref ExactList<byte> inside, ref ExactList<ushort> routingRows, ref ExactList<int> lookupStart, ref ExactList<int> lookupEnd)
        {
            int input = 0;
            for (int k = 0; k < lookupStart.Length; k++)
            {
                int row = lookupStart[k] + input;
                if (row < lookupStart[k] || row >= lookupEnd[k])
                {
                    // the same as PerformCSGJob: a lookup without a row for this input leaves the input as it is
                    routingErrors++;
                    continue;
                }
                int region = lookupRegion[k];
                int category = region >= 0 && inside[region] != 0 ? regions[region].category : (int)CategoryIndex.Outside;
                input = routingRows[row * kCategoryCount + category];
            }
            return input;
        }
    }
}
