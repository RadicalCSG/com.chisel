using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    // What a face was built from, of one brush that can reach it (ExactFaceCache.Reaches), in the order the brushes touch the
    // face's brush: a face built from the same brushes, seen the same way, is built the same. The face builder reads of the
    // brush only its planes (by its generation), what it gives each part of the face and its routing lookup: which brush
    // it is is not part of it.
    struct ExactFaceKey : IEquatable<ExactFaceKey>
    {
        public ulong         generation;        // ExactBrush.generation: its planes
        public byte          insideCategory;    // the categories it gives the face's parts (ExactTouchingBrush)
        public byte          alignedCategory;
        public byte          reverseAlignedCategory;
        public byte          routes;
        public int           lookup;            // its lookup in the face's brush's routing table, -1 for none

        public bool Equals(ExactFaceKey other)
        {
            return generation == other.generation &&
                   insideCategory == other.insideCategory && alignedCategory == other.alignedCategory &&
                   reverseAlignedCategory == other.reverseAlignedCategory && routes == other.routes && lookup == other.lookup;
        }
    }

    // One face of the brush as its last build wrote it
    struct ExactFaceRecord
    {
        public int  keyStart, keyCount;     // into ExactFaceCache.keys
        public int  dataStart, dataCount;   // into ExactFaceCache.data: what it wrote to the output (ExactCSGJob.EmitFace)
        public int  routingErrors;          // ExactCSGStat.RoutingOutOfRange
        public byte replayable;             // 0 for a face not kept: one routed through the tables of brushes it gives way to
        public byte faceFailure;            // ExactFaceFailure
        public byte triangulationFailure0;  // ExactTriangulationFailure of its first two parts
        public byte triangulationFailure1;
    }

    // What ExactCSGJob built of one brush's faces at the last update that built the brush, and what each face was built
    // from. A face whose key is the same is written again as it was, not built: its own brush, its routing table and the
    // brushes that can reach it are all it is built from (ExactFaceBuilder.BuildFace).
    struct ExactFaceCache : IDisposable
    {
        public CompactNodeID           nodeID;          // which brush: the cache is by node order, which another brush can take
        public ulong                   generation;      // the brush's ExactBrush.generation
        public byte                    collectDiscarded;
        public UnsafeList<ushort>      routingRows;     // its routing table as level 0 of ExactRouting has it
        public UnsafeList<int2>        routingLookups;  // each lookup's rows, start and end
        public UnsafeList<ExactFaceRecord> faces;       // by surface
        public UnsafeList<ExactFaceKey> keys;
        public UnsafeList<int>         data;

        public bool IsCreated => faces.IsCreated;

        public static ExactFaceCache Create(Allocator allocator)
        {
            return new ExactFaceCache
            {
                routingRows    = new UnsafeList<ushort>(64, allocator),
                routingLookups = new UnsafeList<int2>(16, allocator),
                faces          = new UnsafeList<ExactFaceRecord>(8, allocator),
                keys           = new UnsafeList<ExactFaceKey>(32, allocator),
                data           = new UnsafeList<int>(256, allocator),
            };
        }

        // A copy with every list as long as it needs to be, no longer: what is kept between updates
        public ExactFaceCache Copy(Allocator allocator)
        {
            var copy = new ExactFaceCache
            {
                nodeID           = nodeID,
                generation       = generation,
                collectDiscarded = collectDiscarded,
                routingRows      = new UnsafeList<ushort>(math.max(1, routingRows.Length), allocator),
                routingLookups   = new UnsafeList<int2>(math.max(1, routingLookups.Length), allocator),
                faces            = new UnsafeList<ExactFaceRecord>(math.max(1, faces.Length), allocator),
                keys             = new UnsafeList<ExactFaceKey>(math.max(1, keys.Length), allocator),
                data             = new UnsafeList<int>(math.max(1, data.Length), allocator),
            };
            copy.routingRows.AddRange(routingRows);
            copy.routingLookups.AddRange(routingLookups);
            copy.faces.AddRange(faces);
            copy.keys.AddRange(keys);
            copy.data.AddRange(data);
            return copy;
        }

        public void Dispose()
        {
            if (routingRows.IsCreated)    routingRows.Dispose();
            if (routingLookups.IsCreated) routingLookups.Dispose();
            if (faces.IsCreated)          faces.Dispose();
            if (keys.IsCreated)           keys.Dispose();
            if (data.IsCreated)           data.Dispose();
            this = default;
        }

        // Whether brush `touching` can change face `face` of `own`: its bounds meet the face's, and no plane of it has every
        // corner of the face strictly outside. One that can't makes no region of the face (ExactFaceBuilder.Coverage misses
        // it) and has no interval on any line through it (the line's segment lies in the face, so Separated): it adds
        // nothing to the face, and a lookup to it routes as Outside, as one to no brush does.
        public static bool Reaches(ref ExactBrush own, int face, in ExactTouchingBrush touching, ref ExactList<ExactPlane> planes)
        {
            var faceBounds = own.faceBounds[face];
            var bounds     = touching.bounds;
            if (!(math.all(bounds.Min <= faceBounds.Max) && math.all(bounds.Max >= faceBounds.Min)))
                return false;
            var range = own.faces[face];
            for (int p = 0; p < touching.planeCount; p++)
            {
                var plane = planes[touching.planeOffset + p];
                bool allOutside = true;
                for (int e = 0; e < range.y && allOutside; e++)
                    allOutside = ExactPredicates.Side(plane, own.faceEdges[range.x + e].start) > 0;
                if (allOutside && range.y > 0)
                    return false;
            }
            return true;
        }
    }
}
