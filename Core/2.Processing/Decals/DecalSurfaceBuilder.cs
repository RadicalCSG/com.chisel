using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    // The surface triangle a piece was cut from: what its pieces inherit.
    internal struct DecalTriangleSource
    {
        public float3  normal0, normal1, normal2;   // the render normals at its corners (smoothed or not)
        public double3 faceNormal;                  // unit, pointing out of its visible side
        public Vector4 entityID;                    // the select vertex id of the brush
    }

    // The triangles and vertices of one surface GenerateSurfaceTrianglesJob outputs. Vertices at the same
    // position are shared.
    internal struct DecalSurfaceOutput : IDisposable
    {
        public NativeList<int>          indices;
        public NativeList<float3>       colliderVertices;
        public NativeList<SelectVertex> selectVertices;
        public NativeList<RenderVertex> renderVertices;
        UnsafeHashMap<double3, int>     lookup;

        public readonly bool IsCreated => indices.IsCreated;
        public readonly bool IsEmpty   => !indices.IsCreated || indices.Length == 0;

        public static DecalSurfaceOutput Create(Allocator allocator)
        {
            return new DecalSurfaceOutput
            {
                indices          = new NativeList<int>(64, allocator),
                colliderVertices = new NativeList<float3>(32, allocator),
                selectVertices   = new NativeList<SelectVertex>(32, allocator),
                renderVertices   = new NativeList<RenderVertex>(32, allocator),
                lookup           = new UnsafeHashMap<double3, int>(32, allocator)
            };
        }

        public void Clear()
        {
            indices.Clear();
            colliderVertices.Clear();
            selectVertices.Clear();
            renderVertices.Clear();
            lookup.Clear();
        }

        // decalVolume == null for the surface itself: its texture coordinates are left for the caller.
        public unsafe void AddTriangle(in DecalPiece piece, in DecalTriangleSource source, DecalVolume* decalVolume, float offset)
        {
            var a = AddVertex(piece.v0, source, decalVolume, offset);
            var b = AddVertex(piece.v1, source, decalVolume, offset);
            var c = AddVertex(piece.v2, source, decalVolume, offset);
            if (a == b || b == c || c == a)
                return;
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
        }

        unsafe int AddVertex(in DecalClipVertex vertex, in DecalTriangleSource source, DecalVolume* decalVolume, float offset)
        {
            // Keyed on the position before any offset, so a lifted decal still shares its vertices
            if (lookup.TryGetValue(vertex.position, out var index))
                return index;

            var weights  = (float3)vertex.barycentric;
            var normal   = math.normalizesafe(weights.x * source.normal0 + weights.y * source.normal1 + weights.z * source.normal2,
                                              (float3)source.faceNormal);
            var position = (float3)vertex.position;
            if (offset > 0)
                position = (float3)(vertex.position + source.faceNormal * offset);
            var uv0 = decalVolume == null ? float2.zero : decalVolume->ProjectUV(vertex.position);

            index = colliderVertices.Length;
            lookup.Add(vertex.position, index);
            colliderVertices.Add(position);
            selectVertices.Add(new SelectVertex { position = position, entityID = source.entityID });
            renderVertices.Add(new RenderVertex { position = position, normal = normal, uv0 = uv0 });
            return index;
        }

        public void Dispose()
        {
            if (indices.IsCreated)          indices.Dispose();
            if (colliderVertices.IsCreated) colliderVertices.Dispose();
            if (selectVertices.IsCreated)   selectVertices.Dispose();
            if (renderVertices.IsCreated)   renderVertices.Dispose();
            if (lookup.IsCreated)           lookup.Dispose();
            this = default;
        }
    }

    internal unsafe struct DecalSurfaceBuilder : IDisposable
    {
        UnsafeList<DecalPiece> pieces;
        UnsafeList<DecalPiece> nextPieces;
        UnsafeList<DecalPiece> partPieces;
        UnsafeList<DecalPiece> partNext;
        // the polygon decals' parts, for the Build going on
        NativeArray<DecalPlanes> parts;

        // What remains of the surface itself, when an opaque decal reached it
        public DecalSurfaceOutput surface;
        // One per decal in the candidate list given to Build, in the same order
        public UnsafeList<DecalSurfaceOutput> decals;

        public static DecalSurfaceBuilder Create(Allocator allocator)
        {
            return new DecalSurfaceBuilder
            {
                pieces     = new UnsafeList<DecalPiece>(16, allocator),
                nextPieces = new UnsafeList<DecalPiece>(16, allocator),
                partPieces = new UnsafeList<DecalPiece>(16, allocator),
                partNext   = new UnsafeList<DecalPiece>(16, allocator),
                surface    = DecalSurfaceOutput.Create(allocator),
                decals     = new UnsafeList<DecalSurfaceOutput>(4, allocator)
            };
        }

        struct PieceSink : IDecalPieceSink
        {
            public UnsafeList<DecalPiece>* list;
            public int                     insideOwner;

            public void Add(in DecalPiece piece, bool inside)
            {
                var result = piece;
                if (inside && result.owner < 0)
                    result.owner = insideOwner;
                list->Add(result);
            }
        }

        struct OutputSink : IDecalPieceSink
        {
            public DecalSurfaceOutput*  output;
            public DecalTriangleSource* source;
            public DecalVolume*         volume;
            public float                offset;

            public void Add(in DecalPiece piece, bool inside)
            {
                if (inside)
                    output->AddTriangle(piece, *source, volume, offset);
            }
        }

        public bool Build(NativeList<int> triangles, NativeList<RenderVertex> renderVertices, NativeList<SelectVertex> selectVertices,
                          NativeArray<DecalVolume> volumes, NativeArray<DecalPlanes> parts, UnsafeList<int> candidates,
                          UnsafeList<byte> targeted, SurfaceDestinationFlags surfaceFlags)
        {
            this.parts = parts;
            surface.Clear();
            while (decals.Length < candidates.Length)
                decals.Add(DecalSurfaceOutput.Create(Allocator.Temp));
            for (int j = 0; j < candidates.Length; j++)
                decals.ElementAt(j).Clear();

            var volumePointer = (DecalVolume*)volumes.GetUnsafeReadOnlyPtr();
            var cut = false;
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                var r0 = renderVertices[triangles[t]];
                var r1 = renderVertices[triangles[t + 1]];
                var r2 = renderVertices[triangles[t + 2]];
                var p0 = (double3)r0.position;
                var p1 = (double3)r1.position;
                var p2 = (double3)r2.position;

                var faceNormal = math.normalizesafe(math.cross(p1 - p0, p2 - p0));
                if (math.dot(faceNormal, (double3)(r0.normal + r1.normal + r2.normal)) < 0)
                    faceNormal = -faceNormal;
                var source = new DecalTriangleSource
                {
                    normal0    = r0.normal,
                    normal1    = r1.normal,
                    normal2    = r2.normal,
                    faceNormal = faceNormal,
                    entityID   = selectVertices[triangles[t]].entityID
                };
                var centroid    = (p0 + p1 + p2) / 3.0;
                var triangleMin = (float3)math.min(p0, math.min(p1, p2));
                var triangleMax = (float3)math.max(p0, math.max(p1, p2));

                pieces.Clear();
                pieces.Add(new DecalPiece
                {
                    v0    = new DecalClipVertex { position = p0, barycentric = new double3(1, 0, 0) },
                    v1    = new DecalClipVertex { position = p1, barycentric = new double3(0, 1, 0) },
                    v2    = new DecalClipVertex { position = p2, barycentric = new double3(0, 0, 1) },
                    owner = -1
                });

                // Opaque decals, top first; a polygon part by part, all of it the same decal's
                for (int j = candidates.Length - 1; j >= 0; j--)
                {
                    var volume = volumePointer + candidates[j];
                    if (volume->IsTransparent || !volume->Overlaps(triangleMin, triangleMax))
                        continue;

                    var takes = targeted[j] != 0 &&
                                volume->GetDestinationFlags(surfaceFlags) != SurfaceDestinationFlags.None &&
                                volume->Accepts(faceNormal, centroid);
                    if (!volume->IsPolygon)
                        CutOpaque(volume, j, takes, &source);
                    else
                    {
                        var part = *volume;
                        for (int k = 0; k < volume->partCount; k++)
                        {
                            part.planes = parts[volume->partStart + k];
                            CutOpaque(&part, j, takes, &source);
                        }
                    }
                    cut = true;
                }

                for (int p = 0; p < pieces.Length; p++)
                {
                    var piece = pieces[p];
                    if (piece.owner < 0)
                        surface.AddTriangle(piece, source, null, 0);
                    else
                        decals.ElementAt(piece.owner).AddTriangle(piece, source, volumePointer + candidates[piece.owner], 0);
                }

                // Transparent decals, over whatever is below them
                for (int j = 0; j < candidates.Length; j++)
                {
                    var volume = volumePointer + candidates[j];
                    if (!volume->IsTransparent || targeted[j] == 0 || !volume->Overlaps(triangleMin, triangleMax) ||
                        volume->GetDestinationFlags(surfaceFlags) == SurfaceDestinationFlags.None ||
                        !volume->Accepts(faceNormal, centroid))
                        continue;

                    var sink = new OutputSink
                    {
                        output = (DecalSurfaceOutput*)decals.Ptr + j,
                        source = &source,
                        volume = volume,
                        offset = volume->surfaceOffset
                    };
                    for (int p = 0; p < pieces.Length; p++)
                    {
                        var piece = pieces[p];
                        // A piece an opaque decal above this one draws hides it
                        if (piece.owner > j)
                            continue;
                        if (!volume->Overlaps(piece.Min, piece.Max))
                            continue;
                        if (volume->IsPolygon)
                            AddInsidePolygon(volume, piece, j, ref sink);
                        else
                            DecalClipping.AddInside(*volume, piece, j, ref sink);
                    }
                }
            }
            return cut;
        }

        // Cuts the pieces by one opaque volume: what is inside becomes decal j's when it takes it. A piece already
        // decal j's (another part of its polygon took it) stays as it is.
        void CutOpaque(DecalVolume* volume, int j, bool takes, DecalTriangleSource* source)
        {
            var sink = new PieceSink { list = (UnsafeList<DecalPiece>*)UnsafeUtility.AddressOf(ref nextPieces), insideOwner = takes ? j : -1 };
            nextPieces.Clear();
            for (int p = 0; p < pieces.Length; p++)
            {
                var piece = pieces[p];
                if ((takes && piece.owner == j) || !volume->Overlaps(piece.Min, piece.Max))
                {
                    nextPieces.Add(piece);
                    continue;
                }
                if (DecalClipping.Split(*volume, piece, ref sink))
                    continue;

                // The cut failed: keep the piece, and draw the decal over it instead of in its place
                nextPieces.Add(piece);
                if (takes && piece.owner < 0)
                {
                    var overlay = new OutputSink
                    {
                        output = (DecalSurfaceOutput*)decals.Ptr + j,
                        source = source,
                        volume = volume,
                        offset = ChiselDecalSettings.kDefaultSurfaceOffset
                    };
                    DecalClipping.AddInside(*volume, piece, j, ref overlay);
                }
            }
            (pieces, nextPieces) = (nextPieces, pieces);
        }

        // Splits a piece by one part, sending what is inside to the output and keeping what is outside for the next part
        struct PartSink : IDecalPieceSink
        {
            public UnsafeList<DecalPiece>* outside;
            public OutputSink              inside;

            public void Add(in DecalPiece piece, bool isInside)
            {
                if (isInside)
                    inside.Add(piece, true);
                else
                    outside->Add(piece);
            }
        }

        // A transparent polygon over a piece: its parts one after another, each cutting what the ones before left
        void AddInsidePolygon(DecalVolume* volume, in DecalPiece piece, int j, ref OutputSink sink)
        {
            partPieces.Clear();
            partPieces.Add(piece);
            var part = *volume;
            var partOutput = sink;
            partOutput.volume = &part;
            for (int k = 0; k < volume->partCount && partPieces.Length > 0; k++)
            {
                part.planes = parts[volume->partStart + k];
                partNext.Clear();
                var split = new PartSink { outside = (UnsafeList<DecalPiece>*)UnsafeUtility.AddressOf(ref partNext), inside = partOutput };
                for (int p = 0; p < partPieces.Length; p++)
                {
                    var current = partPieces[p];
                    if (!part.Overlaps(current.Min, current.Max))
                    {
                        partNext.Add(current);
                        continue;
                    }
                    if (DecalClipping.Split(part, current, ref split))
                        continue;
                    // The cut failed: draw what is inside over it, and keep it for the other parts
                    DecalClipping.AddInside(part, current, j, ref partOutput);
                    partNext.Add(current);
                }
                (partPieces, partNext) = (partNext, partPieces);
            }
        }

        public void Dispose()
        {
            if (pieces.IsCreated)     pieces.Dispose();
            if (nextPieces.IsCreated) nextPieces.Dispose();
            if (partPieces.IsCreated) partPieces.Dispose();
            if (partNext.IsCreated)   partNext.Dispose();
            surface.Dispose();
            if (decals.IsCreated)
            {
                for (int i = 0; i < decals.Length; i++)
                    decals.ElementAt(i).Dispose();
                decals.Dispose();
            }
            this = default;
        }
    }
}
