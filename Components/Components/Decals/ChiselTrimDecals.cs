using System.Collections.Generic;
using Chisel.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components
{
    /// <summary>
    /// A trim decal lies on the edge where two surfaces meet (surface A and B), its image split along its centre line: the
    /// lower half on A, the upper half on B, each laid flat on its own surface (Documentation~/Design/Decals.md). Each half
    /// is an orthographic box decal in the surface's plane, one side on the edge, drawn on every surface in its range.
    /// </summary>
    public static class ChiselTrimDecals
    {
        /// <summary>The second half's entity id: the decal's, with this bit (a decal is one entity per id)</summary>
        public const ulong kSecondHalf = 1ul << 62;
        // a trim sheet's image is a piece of its own along the edge for every image: its number in these bits
        const int   kPieceShift = 50;
        const ulong kPieceMask  = 0xFFFul << kPieceShift;
        const int   kMaxPieces  = (int)(kPieceMask >> kPieceShift) + 1;

        /// <summary>The entity id of a decal's piece (a tile, an image along a trim's edge): the decal's, with its number</summary>
        public static ulong PieceEntityID(ulong entityID, int piece) => entityID | ((ulong)piece << kPieceShift);

        /// <summary>How many pieces a decal can be</summary>
        public const int MaxPieces = kMaxPieces;

        /// <summary>The decal an entity id of a decal's piece belongs to</summary>
        public static ulong DecalEntityID(ulong entityID) => entityID & ~(kSecondHalf | kPieceMask);

        // how far the boxes reach either side of their surface
        const float kMargin = 0.01f;
        const float kEpsilon = 1e-4f;

        public struct Half
        {
            public ChiselDecalInstance decal;
            // none: like a box decal, a half draws on every surface in its range that faces it
            public ChiselDecalTarget[] targets;
        }

        // A surface of a brush, in the tree's space: its plane (unit normal, distance) and its outline
        struct Face
        {
            public float3 normal;
            public float  distance;
            public float3 centroid;
            public List<float3> outline;
        }

        static readonly HashSet<CSGTreeBrush> s_Brushes = new();

        /// <summary>
        /// A trim decal's pieces, in the space of the model its first surface is in (the model is returned too). Its targets
        /// are pairs of surfaces, an edge each, in order: a chain round corners. Per edge its two halves, or for an image in
        /// a trim sheet both halves of every image along it (A, B, A, B...). edgeFrom and edgeTo are the first edge's stretch.
        /// False when a pair isn't in the model, doesn't meet in a line, or shares no stretch of it.
        /// </summary>
        public static bool TryDescribe(ChiselDecalComponent decal, ulong entityID, ulong renderMaterial, SurfaceDestinationFlags destinationFlags,
                                       out ChiselModelComponent model, List<Half> halves, out float3 edgeFrom, out float3 edgeTo)
        {
            halves.Clear();
            edgeFrom = edgeTo = float3.zero;
            model = null;
            var targets = decal.Targets;
            // only the first edge: chains round corners are off until they are fixed (com.chisel#53)
            var edgeCount = math.min(1, targets.Count / 2);
            if (edgeCount < 1)
                return false;
            var settings = decal.Settings;
            var size = math.abs((float3)settings.size);
            if (!(size.x > kEpsilon) || !(size.y > kEpsilon))
                return false;
            var fill = decal.TrimFill;
            // one image is on the first edge only
            if (fill == ChiselTrimFill.Single)
                edgeCount = 1;

            var edges = new ChainEdge[edgeCount];
            for (int i = 0; i < edgeCount; i++)
            {
                // the first edge nearest to the decal (where it was picked), the others nearest to the edge before them
                var reference = i == 0 ? (float3)decal.transform.position
                                       : (float3)model.transform.localToWorldMatrix.MultiplyPoint3x4(edges[i - 1].start + edges[i - 1].direction * (edges[i - 1].length * 0.5f));
                var keep = i == 0 && decal.hasTrimLine;
                var edgeIndex = i == 0 ? decal.TrimEdge : -1;
                if (!TryEdge(targets[i * 2], targets[i * 2 + 1], edgeIndex, decal.TrimEdgeOnSecond, reference, keep, decal.trimLinePoint, decal.trimLineDirection, out var edgeModel, out edges[i]) ||
                    (model != null && edgeModel != model))
                    return false;
                model = edgeModel;
                if (i == 0)
                {
                    var toWorld = model.transform.localToWorldMatrix;
                    decal.hasTrimLine       = true;
                    decal.trimLinePoint     = toWorld.MultiplyPoint3x4(edges[0].start);
                    decal.trimLineDirection = toWorld.MultiplyVector(edges[0].direction).normalized;
                }
            }
            if (fill == ChiselTrimFill.Single)
                PlaceSingle(decal, model, size.x, ref edges[0]);
            Orient(edges);
            edgeFrom = edges[0].start;
            edgeTo   = edges[0].start + edges[0].direction * edges[0].length;

            // where two edges meet on a surface they share, its pieces are cut where the corner is halved
            // (a chain that closes on itself has a corner between its last edge and its first too)
            for (int i = 0; i < edgeCount && edgeCount > 1; i++)
            {
                var n = (i + 1) % edgeCount;
                if (n == 0 && edgeCount < 3)
                    break;
                if (math.distance(edges[i].start + edges[i].direction * edges[i].length, edges[n].start) > kCornerTolerance)
                    continue;
                for (int h = 0; h < 2; h++)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        if (SameSurface(targets[i * 2 + h], targets[n * 2 + k]))
                            Mitre(ref edges[i], h, ref edges[n], k);
                    }
                }
            }

            var total = 0.0f;
            foreach (var edge in edges)
                total += edge.length;
            // the image along the chain: images per metre, and where each edge starts in images
            var perMetre = fill switch
            {
                ChiselTrimFill.Repeat => 1 / size.x,
                ChiselTrimFill.Fit    => math.max(1, math.round(total / size.x)) / total,
                _                     => 1 / total
            };
            if (fill == ChiselTrimFill.Single)
                perMetre = 1 / edges[0].length;

            var rect  = decal.ImageRect;
            var sheet = rect != new Rect(0, 0, 1, 1);
            var piece = 0;
            var along = 0.0f;
            for (int i = 0; i < edgeCount; i++)
            {
                ref var edge = ref edges[i];
                // seen from the first surface, with the image's top towards the edge, the image reads along the edge's
                // direction only when that runs left to right; otherwise it runs from the far end (both halves alike)
                var backwards = math.dot(math.cross(edge.direction, -edge.acrossA), edge.faceA.normal) > 0;
                var uEnd = sheet ? 1 : total * perMetre;
                // the whole texture repeats by wrapping; an image in a trim sheet can't, so it is a piece for every image
                int pieces = 1;
                float pieceLength = edge.length;
                if (sheet && fill != ChiselTrimFill.Stretch && fill != ChiselTrimFill.Single)
                {
                    pieceLength = 1 / perMetre;
                    pieces = (int)math.ceil(edge.length / pieceLength - kEpsilon);
                }
                if (pieces < 1 || piece + pieces > kMaxPieces)
                    return false;
                for (int p = 0; p < pieces; p++, piece++)
                {
                    var offset = p * pieceLength;
                    var span   = math.min(pieceLength, edge.length - offset);
                    var shown  = span * perMetre;
                    // a trim sheet's image starts again on every edge; the whole texture runs on round the corners
                    var uStart = sheet ? 0 : (along + offset) * perMetre;
                    var id     = entityID | ((ulong)piece << kPieceShift);
                    var pieceStart = edge.start + edge.direction * offset;
                    var first  = p == 0;
                    var last   = p == pieces - 1;
                    if (backwards)
                    {
                        uStart = uEnd - uStart;
                        shown  = -shown;
                    }
                    halves.Add(MakeHalf(edge.faceA, edge.acrossA, targets[i * 2],     pieceStart, edge.direction, span, uStart, shown,
                                        first ? edge.slantStartA : 0, last ? edge.slantEndA : 0, rect, settings, id,               renderMaterial, destinationFlags, lower: true));
                    halves.Add(MakeHalf(edge.faceB, edge.acrossB, targets[i * 2 + 1], pieceStart, edge.direction, span, uStart, shown,
                                        first ? edge.slantStartB : 0, last ? edge.slantEndB : 0, rect, settings, id | kSecondHalf, renderMaterial, destinationFlags, lower: decal.TrimMirror));
                }
                along += edge.length;
            }
            return true;
        }

        const float kCornerTolerance = 1e-3f;

        // One edge of a trim: its surfaces, the stretch both reach, and per half the direction into its surface and how its
        // ends lean (x per y in the half's box, 0: square; a corner's mitre)
        struct ChainEdge
        {
            public Face   faceA, faceB;
            public float3 start, direction;
            public float  length;
            public float3 acrossA, acrossB;
            public float  slantStartA, slantEndA, slantStartB, slantEndB;
        }

        static bool TryEdge(ChiselDecalSurfaceTarget a, ChiselDecalSurfaceTarget b, int edgeIndex, bool edgeOfB, float3 worldReference, bool keep,
                            float3 worldKeepPoint, float3 worldKeepDirection, out ChiselModelComponent model, out ChainEdge edge)
        {
            edge = default;
            model = null;
            if (!TryFace(a, out var modelA, out var faceA) || !TryFace(b, out var modelB, out var faceB) || modelA != modelB)
                return false;
            model = modelA;

            var toModel   = model.transform.worldToLocalMatrix;
            var reference = (float3)toModel.MultiplyPoint3x4(worldReference);
            var kept      = new KeptLine { keep = keep, point = toModel.MultiplyPoint3x4(worldKeepPoint), direction = math.normalizesafe((float3)toModel.MultiplyVector(worldKeepDirection)) };
            float3 point, direction;
            var owner = edgeOfB ? faceB : faceA;
            if (edgeIndex >= 0 && edgeIndex < owner.outline.Count)
            {
                // the edge it was given
                point = owner.outline[edgeIndex];
                direction = owner.outline[(edgeIndex + 1) % owner.outline.Count] - point;
                if (math.lengthsq(direction) < kEpsilon * kEpsilon)
                    return false;
                direction = math.normalize(direction);
            } else if (!LineOf(faceA, faceB, reference, kept, out point, out direction, out _))
                return false;

            // where both surfaces reach it; when only one of them has an edge on the line (a wall standing on a floor),
            // that whole edge, as the floor beside it may be another brush
            float fromA = float.NegativeInfinity, toA = float.PositiveInfinity, fromB = float.NegativeInfinity, toB = float.PositiveInfinity;
            if (!Clip(faceA, point, direction, ref fromA, ref toA) || !Clip(faceB, point, direction, ref fromB, ref toB) ||
                math.min(toA, toB) - math.max(fromA, fromB) <= kEpsilon)
                return false;
            var edgeOnA = HasEdgeOn(faceA, point, direction);
            var edgeOnB = HasEdgeOn(faceB, point, direction);
            float from, to;
            if (edgeOnA && !edgeOnB)      { from = fromA; to = toA; }
            else if (edgeOnB && !edgeOnA) { from = fromB; to = toB; }
            else                          { from = math.max(fromA, fromB); to = math.min(toA, toB); }
            var start = point + direction * from;
            edge = new ChainEdge
            {
                faceA     = faceA,
                faceB     = faceB,
                start     = start,
                direction = direction,
                length    = to - from,
                acrossA   = Across(faceA, faceB, start, direction),
                acrossB   = Across(faceB, faceA, start, direction)
            };
            return true;
        }

        // One image round the decal's position on the edge (kept on the edge), its whole length near the ends
        static void PlaceSingle(ChiselDecalComponent decal, ChiselModelComponent model, float imageLength, ref ChainEdge edge)
        {
            if (imageLength >= edge.length)
                return;
            var position = (float3)model.transform.worldToLocalMatrix.MultiplyPoint3x4(decal.transform.position);
            var at   = math.clamp(math.dot(position - edge.start, edge.direction), 0, edge.length);
            var from = math.clamp(at - imageLength * 0.5f, 0, edge.length - imageLength);
            edge.start += edge.direction * from;
            edge.length = imageLength;
        }

        // Each edge running on from the one before it: its start at the end it shares with its neighbour
        static void Orient(ChainEdge[] edges)
        {
            for (int i = 0; i < edges.Length; i++)
            {
                ref var edge = ref edges[i];
                var end = edge.start + edge.direction * edge.length;
                bool flip;
                if (i > 0)
                {
                    var previous = edges[i - 1].start + edges[i - 1].direction * edges[i - 1].length;
                    flip = math.distance(end, previous) < math.distance(edge.start, previous);
                } else if (edges.Length > 1)
                {
                    var next = edges[1];
                    var nextEnd = next.start + next.direction * next.length;
                    var toNext   = math.min(math.distance(end, next.start), math.distance(end, nextEnd));
                    var fromNext = math.min(math.distance(edge.start, next.start), math.distance(edge.start, nextEnd));
                    flip = fromNext < toNext;
                } else
                    flip = false;
                if (!flip)
                    continue;
                edge.start     = end;
                edge.direction = -edge.direction;
            }
        }

        // The ends of two halves that meet at a corner on one surface (this edge's half h ends there, the next one's half k
        // starts there), cut along the line halving the corner; halves on two surfaces meet on their own edge, square
        static void Mitre(ref ChainEdge edge, int h, ref ChainEdge next, int k)
        {
            var across     = h == 0 ? edge.acrossA : edge.acrossB;
            var nextAcross = k == 0 ? next.acrossA : next.acrossB;
            var cut = across + nextAcross;
            if (math.lengthsq(cut) < kEpsilon * kEpsilon)
                return;
            cut = math.normalize(cut);
            var y     = math.dot(cut, across);
            var nextY = math.dot(cut, nextAcross);
            if (math.abs(y) < kEpsilon || math.abs(nextY) < kEpsilon)
                return;
            var slantEnd       = math.dot(cut, edge.direction) / y;
            var nextSlantStart = math.dot(cut, next.direction) / nextY;
            if (h == 0) edge.slantEndA = slantEnd; else edge.slantEndB = slantEnd;
            if (k == 0) next.slantStartA = nextSlantStart; else next.slantStartB = nextSlantStart;
        }

        static bool SameSurface(ChiselDecalSurfaceTarget a, ChiselDecalSurfaceTarget b) => a.brush == b.brush && a.surfaceIndex == b.surfaceIndex;

        // Across the edge, into the surface: the side its outline is on, or where it lies on both (a wall standing on a
        // floor), the side the other surface faces, as the other side is under the other's brush
        static float3 Across(in Face face, in Face other, float3 start, float3 direction)
        {
            var across = math.normalize(math.cross(face.normal, direction));
            float below = 0, above = 0;
            foreach (var vertex in face.outline)
            {
                var side = math.dot(vertex - start, across);
                below = math.min(below, side);
                above = math.max(above, side);
            }
            var bothSides = below < -kEpsilon && above > kEpsilon;
            // in one plane with the other face (which overlaps it), away from the other face, where this one shows
            var coplanar = math.dot(face.normal, other.normal) > kNearlyParallel;
            if (bothSides ? (coplanar ? math.dot(other.centroid - start, across) > 0 : math.dot(other.normal, across) < 0) : -below > above)
                across = -across;
            return across;
        }

        static Half MakeHalf(in Face face, float3 across, ChiselDecalSurfaceTarget surface, float3 start, float3 direction, float length,
                             float uStart, float shown, float slantStart, float slantEnd, Rect rect, ChiselDecalSettings settings,
                             ulong entityID, ulong renderMaterial, SurfaceDestinationFlags destinationFlags, bool lower)
        {
            var height = math.abs(settings.size.y);
            var origin = start + direction * (length * 0.5f);
            var decalToTree = new float4x4(new float4(direction, 0), new float4(across, 0), new float4(-face.normal, 0), new float4(origin, 1));

            // the image's v is 0.5 on the edge: down to 0 across A, up to 1 across B; u runs along the edge from uStart; the
            // image is the rect of the texture
            ChiselDecalSettings.TurnMapping(new float2(shown, lower ? -0.5f : 0.5f), new float2(uStart, 0.5f), settings.imageTurns, out var imageScale, out var imageOffset);
            var trimScale  = imageScale * (float2)rect.size;
            var trimOffset = (float2)rect.position + imageOffset * (float2)rect.size;
            var userScale  = math.all((float2)settings.uvScale == float2.zero) ? new float2(1) : (float2)settings.uvScale;
            var half = settings;
            half.center     = new float3(0, height * 0.25f, 0);
            half.size       = new float3(length, height * 0.5f, kMargin * 2);
            half.projection = ChiselDecalProjection.Orthographic;
            half.endSlant   = new float2(slantStart, slantEnd);
            // only the surfaces that face it, not ones standing across its range (a wall on the floor it lies on)
            half.maxAngle   = kHalfMaxAngle;
            half.uvScale    = userScale * trimScale;
            half.uvOffset   = (float2)settings.uvOffset + userScale * trimOffset;
            return new Half
            {
                decal = new ChiselDecalInstance
                {
                    entityID         = entityID,
                    decalToTree      = decalToTree,
                    settings         = half,
                    renderMaterial   = renderMaterial,
                    destinationFlags = destinationFlags
                },
                targets = null
            };
        }

        const float kHalfMaxAngle = 45;

        // The part of the line inside the face's outline, narrowing [from, to]
        static bool Clip(in Face face, float3 point, float3 direction, ref float from, ref float to)
        {
            var outline = face.outline;
            for (int i = 0; i < outline.Count; i++)
            {
                var v0 = outline[i];
                var v1 = outline[(i + 1) % outline.Count];
                var inward = math.cross(face.normal, v1 - v0);
                var scale  = math.length(inward);
                if (scale < kEpsilon)
                    continue;
                inward /= scale;
                if (math.dot(face.centroid - v0, inward) < 0)
                    inward = -inward;
                // dot(point + t direction - v0, inward) >= 0
                var slope  = math.dot(direction, inward);
                var offset = math.dot(point - v0, inward);
                if (math.abs(slope) < kEpsilon)
                {
                    // along an edge of the face: on it, up to kMargin outside (a neighbour nearly flush with it)
                    if (offset < -kMargin)
                        return false;
                    continue;
                }
                var t = -offset / slope;
                if (slope > 0) from = math.max(from, t);
                else           to   = math.min(to, t);
            }
            return from < to;
        }

        /// <summary>The surface (of its generator: the polygon's descriptionIndex) a decal target names for a brush's polygon</summary>
        public static int SurfaceOf(CSGTreeBrush brush, int polygonIndex)
        {
            var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
            if (!blob.IsCreated || polygonIndex < 0 || polygonIndex >= blob.Value.polygons.Length)
                return -1;
            return blob.Value.polygons[polygonIndex].descriptionIndex;
        }

        /// <summary>
        /// The edge of a brush's polygon nearest to a point (both in the tree's space): its ends, and the polygon of the
        /// same brush on its other side
        /// </summary>
        public static bool NearestEdge(CSGTreeBrush brush, int polygonIndex, float3 treePoint, out float3 from, out float3 to, out int otherPolygon)
        {
            return NearestEdge(brush, polygonIndex, treePoint, out from, out to, out otherPolygon, out _);
        }

        /// <summary>The same, with the edge's number along the polygon's outline</summary>
        public static bool NearestEdge(CSGTreeBrush brush, int polygonIndex, float3 treePoint, out float3 from, out float3 to, out int otherPolygon, out int edgeIndex)
        {
            from = to = float3.zero;
            otherPolygon = -1;
            edgeIndex = -1;
            var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
            if (!blob.IsCreated || polygonIndex < 0 || polygonIndex >= blob.Value.polygons.Length)
                return false;
            ref var mesh = ref blob.Value;
            ref var polygon = ref mesh.polygons[polygonIndex];
            var nodeToTree = brush.NodeToTreeSpaceMatrix;
            var nearest = float.PositiveInfinity;
            for (int e = 0; e < polygon.edgeCount; e++)
            {
                var edge = polygon.firstEdge + e;
                var next = polygon.firstEdge + (e + 1) % polygon.edgeCount;
                var a = math.transform(nodeToTree, mesh.localVertices[mesh.halfEdges[edge].vertexIndex]);
                var b = math.transform(nodeToTree, mesh.localVertices[mesh.halfEdges[next].vertexIndex]);
                var ab = b - a;
                var t = math.saturate(math.dot(treePoint - a, ab) / math.max(math.lengthsq(ab), 1e-12f));
                var distance = math.distancesq(treePoint, a + ab * t);
                if (distance >= nearest)
                    continue;
                nearest = distance;
                from = a;
                to = b;
                edgeIndex = e;
                // the polygon on the other side: the one across either half edge here that has both its vertices
                var vertexA = mesh.halfEdges[edge].vertexIndex;
                var vertexB = mesh.halfEdges[next].vertexIndex;
                otherPolygon = -1;
                for (int h = 0; h < 2 && otherPolygon < 0; h++)
                {
                    var twin = mesh.halfEdges[h == 0 ? edge : next].twinIndex;
                    if (twin < 0 || twin >= mesh.halfEdgePolygonIndices.Length)
                        continue;
                    var across = mesh.halfEdgePolygonIndices[twin];
                    if (across != polygonIndex && HasVertices(ref mesh, across, vertexA, vertexB))
                        otherPolygon = across;
                }
            }
            return nearest < float.PositiveInfinity;
        }

        static bool HasVertices(ref BrushMeshBlob mesh, int polygonIndex, int vertexA, int vertexB)
        {
            ref var polygon = ref mesh.polygons[polygonIndex];
            bool foundA = false, foundB = false;
            for (int e = 0; e < polygon.edgeCount; e++)
            {
                var vertex = mesh.halfEdges[polygon.firstEdge + e].vertexIndex;
                foundA |= vertex == vertexA;
                foundB |= vertex == vertexB;
            }
            return foundA && foundB;
        }

        /// <summary>Whether a brush's polygon's plane holds the line through these points (in the tree's space)</summary>
        public static bool PlaneHolds(CSGTreeBrush brush, int polygonIndex, float3 a, float3 b)
        {
            var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
            if (!blob.IsCreated || polygonIndex < 0 || polygonIndex >= blob.Value.localPlanes.Length)
                return false;
            var plane = math.mul(math.transpose(math.inverse(brush.NodeToTreeSpaceMatrix)), blob.Value.localPlanes[polygonIndex]);
            var length = math.length(plane.xyz);
            if (!(length > kEpsilon))
                return false;
            plane /= length;
            const float kOnPlane = kMargin;
            return math.abs(math.dot(plane.xyz, a) + plane.w) < kOnPlane && math.abs(math.dot(plane.xyz, b) + plane.w) < kOnPlane;
        }

        /// <summary>
        /// A surface of another brush in the model (of a generator that makes one brush) that lies along the edge from..to
        /// and goes on past it to beyond, facing against viewDirection: the surface that shows across an edge of the clicked
        /// brush. All in the model's space.
        /// </summary>
        public static bool SurfaceAcross(ChiselModelComponent model, ChiselNodeComponent clicked, float3 from, float3 to, float3 beyond,
                                         float3 viewDirection, out ChiselDecalSurfaceTarget target)
        {
            target = default;
            if (model == null)
                return false;
            var best = float.PositiveInfinity;
            foreach (var generator in model.GetComponentsInChildren<ChiselGeneratorComponent>())
            {
                if (generator == clicked || generator.hierarchyItem.Model != model || !generator.TopTreeNode.Valid ||
                    generator.TopTreeNode.Type != CSGNodeType.Brush)
                    continue;
                var brush = (CSGTreeBrush)generator.TopTreeNode;
                var blob  = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
                if (!blob.IsCreated)
                    continue;
                for (int p = 0; p < blob.Value.polygons.Length; p++)
                {
                    var surfaceIndex = blob.Value.polygons[p].descriptionIndex;
                    if (!TryBrushFace(brush, surfaceIndex, out var face) || math.dot(face.normal, viewDirection) >= 0 || !IsDrawn(model, brush, p))
                        continue;
                    if (math.abs(math.dot(face.normal, from) - face.distance) > kMargin || math.abs(math.dot(face.normal, to) - face.distance) > kMargin)
                        continue;
                    var onFace = beyond - face.normal * (math.dot(face.normal, beyond) - face.distance);
                    if (!Inside(face, onFace))
                        continue;
                    // of several, the one most in line with where the click went on to
                    var distance = math.distance(onFace, beyond);
                    if (distance >= best)
                        continue;
                    best = distance;
                    target = new ChiselDecalSurfaceTarget { brush = generator, surfaceIndex = surfaceIndex };
                }
            }
            return best < float.PositiveInfinity;
        }

        /// <summary>
        /// Whether the CSG drew any of a brush's polygon at its last build: false for a surface it discarded (inside
        /// another brush); true when the model hasn't been built
        /// </summary>
        public static bool IsDrawn(ChiselModelComponent model, CSGTreeBrush brush, int polygonIndex)
        {
            if (model == null || !model.Node.Valid || !brush.Valid)
                return true;
            var lookups = ChiselTreeLookup.Value;
            if (!lookups.HasTree(model.Node))
                return true;
            var data = lookups[model.Node];
            data.lastJobHandle.Complete();
            if (!data.brushRenderBufferLookup.IsCreated ||
                !data.brushRenderBufferLookup.TryGetValue(CompactHierarchyManager.GetCompactNodeID(brush), out var buffer) || !buffer.IsCreated)
                return true;
            ref var surfaces = ref buffer.Value.surfaces;
            return polygonIndex < 0 || polygonIndex >= surfaces.Length || surfaces[polygonIndex].indexCount > 0;
        }

        static bool Inside(in Face face, float3 point)
        {
            var outline = face.outline;
            for (int i = 0; i < outline.Count; i++)
            {
                var v0 = outline[i];
                var inward = math.cross(face.normal, outline[(i + 1) % outline.Count] - v0);
                if (math.lengthsq(inward) < kEpsilon * kEpsilon)
                    continue;
                if (math.dot(face.centroid - v0, inward) < 0)
                    inward = -inward;
                if (math.dot(point - v0, math.normalize(inward)) < -kEpsilon)
                    return false;
            }
            return true;
        }

        /// <summary>An edge a trim decal can lie on: its two surfaces, and the edge's middle in the model's space</summary>
        public struct Edge
        {
            public ChiselDecalSurfaceTarget a, b;
            public float3 middle;
            // the edge: its number along a's outline, or b's with edgeOnB (ChiselDecalComponent.SetTrimEdge)
            public int    edgeIndex;
            public bool   edgeOnB;
        }

        /// <summary>The render material of a target's surface (its id), 0 when it has none or doesn't exist</summary>
        public static ulong MaterialOf(ChiselDecalSurfaceTarget target)
        {
            if (target.brush is not ChiselGeneratorComponent generator || target.surfaceIndex < 0 ||
                !TryBrushOf(generator.TopTreeNode, target.surfaceIndex, out var brush, out var polygon))
                return 0;
            return BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh).Value.polygons[polygon].surface.parameters.parameter1;
        }

        /// <summary>
        /// Every edge in the model where a surface drawn with materialA meets one drawn with materialB, each once, the
        /// materialA surface first: edges of one brush, and where surfaces of two brushes meet (a wall standing on a floor).
        /// Only generators that make one brush.
        /// </summary>
        public static void EdgesBetween(ChiselModelComponent model, ulong materialA, ulong materialB, List<Edge> edges)
        {
            edges.Clear();
            if (model == null || materialA == 0 || materialB == 0)
                return;
            var seen = new HashSet<(ulong, int, int)>();
            foreach (var generator in model.GetComponentsInChildren<ChiselGeneratorComponent>())
            {
                if (generator.hierarchyItem.Model != model || !generator.TopTreeNode.Valid || generator.TopTreeNode.Type != CSGNodeType.Brush)
                    continue;
                var brush = (CSGTreeBrush)generator.TopTreeNode;
                var blob  = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
                if (!blob.IsCreated)
                    continue;
                ref var mesh = ref blob.Value;
                var nodeToTree = brush.NodeToTreeSpaceMatrix;
                var key = UnityEngine.EntityId.ToULong(generator.GetEntityId());
                for (int p = 0; p < mesh.polygons.Length; p++)
                {
                    if (mesh.polygons[p].surface.parameters.parameter1 != materialA)
                        continue;
                    ref var polygon = ref mesh.polygons[p];
                    for (int e = 0; e < polygon.edgeCount; e++)
                    {
                        var edge = polygon.firstEdge + e;
                        var next = polygon.firstEdge + (e + 1) % polygon.edgeCount;
                        var middle = (mesh.localVertices[mesh.halfEdges[edge].vertexIndex] + mesh.localVertices[mesh.halfEdges[next].vertexIndex]) * 0.5f;
                        if (!NearestEdge(brush, p, math.transform(nodeToTree, middle), out _, out _, out var other) || other < 0 ||
                            mesh.polygons[other].surface.parameters.parameter1 != materialB)
                            continue;
                        if (!seen.Add((key, math.min(p, other), math.max(p, other))))
                            continue;
                        edges.Add(new Edge
                        {
                            a         = new ChiselDecalSurfaceTarget { brush = generator, surfaceIndex = mesh.polygons[p].descriptionIndex },
                            b         = new ChiselDecalSurfaceTarget { brush = generator, surfaceIndex = mesh.polygons[other].descriptionIndex },
                            middle    = math.transform(nodeToTree, middle),
                            edgeIndex = e
                        });
                    }
                }
            }
            EdgesBetweenBrushes(model, materialA, materialB, edges);
        }

        // A surface of a generator's brush with one of the two materials, and its bounds
        struct MaterialFace
        {
            public ChiselGeneratorComponent generator;
            public int    surfaceIndex;
            public bool   isA, isB;
            public Face   face;
            public float3 min, max;
        }

        // Where a materialA surface of one brush meets a materialB surface of another, along an edge of either
        static void EdgesBetweenBrushes(ChiselModelComponent model, ulong materialA, ulong materialB, List<Edge> edges)
        {
            var faces = new List<MaterialFace>();
            foreach (var generator in model.GetComponentsInChildren<ChiselGeneratorComponent>())
            {
                if (generator.hierarchyItem.Model != model || !generator.TopTreeNode.Valid || generator.TopTreeNode.Type != CSGNodeType.Brush)
                    continue;
                var brush = (CSGTreeBrush)generator.TopTreeNode;
                var blob  = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
                if (!blob.IsCreated)
                    continue;
                for (int p = 0; p < blob.Value.polygons.Length; p++)
                {
                    var material = blob.Value.polygons[p].surface.parameters.parameter1;
                    if (material != materialA && material != materialB)
                        continue;
                    var surfaceIndex = blob.Value.polygons[p].descriptionIndex;
                    if (!TryBrushFace(brush, surfaceIndex, out var face))
                        continue;
                    float3 min = face.outline[0], max = face.outline[0];
                    foreach (var vertex in face.outline)
                    {
                        min = math.min(min, vertex);
                        max = math.max(max, vertex);
                    }
                    faces.Add(new MaterialFace { generator = generator, surfaceIndex = surfaceIndex, isA = material == materialA, isB = material == materialB,
                                                 face = face, min = min - kMargin, max = max + kMargin });
                }
            }

            var seen = new HashSet<(ulong, int, ulong, int)>();
            foreach (var a in faces)
            {
                if (!a.isA)
                    continue;
                var keyA = UnityEngine.EntityId.ToULong(a.generator.GetEntityId());
                foreach (var b in faces)
                {
                    if (!b.isB || b.generator == a.generator || math.any(a.min > b.max) || math.any(b.min > a.max))
                        continue;
                    var keyB = UnityEngine.EntityId.ToULong(b.generator.GetEntityId());
                    // each pair of surfaces once, also when both materials are the same
                    var key = keyA < keyB ? (keyA, a.surfaceIndex, keyB, b.surfaceIndex) : (keyB, b.surfaceIndex, keyA, a.surfaceIndex);
                    if (!Meet(a.face, b.face, out var middle, out var edgeIndex, out var edgeOnB) || !seen.Add(key))
                        continue;
                    edges.Add(new Edge
                    {
                        a         = new ChiselDecalSurfaceTarget { brush = a.generator, surfaceIndex = a.surfaceIndex },
                        b         = new ChiselDecalSurfaceTarget { brush = b.generator, surfaceIndex = b.surfaceIndex },
                        middle    = middle,
                        edgeIndex = edgeIndex,
                        edgeOnB   = edgeOnB
                    });
                }
            }
        }

        // Two faces of different brushes that meet: their planes cross on a line along an edge of either, both reach a
        // stretch of it, and each has some of itself in front of the other's plane (not all of it inside the other's brush)
        static bool Meet(in Face a, in Face b, out float3 middle, out int edgeIndex, out bool edgeOnB)
        {
            middle = default;
            edgeIndex = -1;
            edgeOnB = false;
            if (!LineOf(a, b, (a.centroid + b.centroid) * 0.5f, default, out var point, out var direction, out var coplanar))
                return false;
            float from = float.NegativeInfinity, to = float.PositiveInfinity;
            if (!Clip(a, point, direction, ref from, ref to) || !Clip(b, point, direction, ref from, ref to) || to - from <= kMargin)
                return false;
            if (!HasEdgeOn(a, point, direction) && !HasEdgeOn(b, point, direction))
                return false;
            if (!coplanar && (!InFrontOf(a, b) || !InFrontOf(b, a)))
                return false;
            middle = point + direction * ((from + to) * 0.5f);
            edgeIndex = EdgeOn(a, point, direction);
            if (edgeIndex < 0)
            {
                edgeIndex = EdgeOn(b, point, direction);
                edgeOnB = true;
            }
            return edgeIndex >= 0;
        }

        // The number of the face's outline edge that lies on the line, -1 when none does
        static int EdgeOn(in Face face, float3 point, float3 direction)
        {
            var outline = face.outline;
            for (int i = 0; i < outline.Count; i++)
            {
                if (math.length(math.cross(outline[i] - point, direction)) < kMargin &&
                    math.length(math.cross(outline[(i + 1) % outline.Count] - point, direction)) < kMargin)
                    return i;
            }
            return -1;
        }

        // A line to keep to: the one the trim lay on, while the reference point is still on it
        struct KeptLine
        {
            public bool   keep;
            public float3 point, direction;

            public readonly bool Holds(float3 from, float3 to)
            {
                return keep && math.length(math.cross(from - point, direction)) < kMargin && math.length(math.cross(to - point, direction)) < kMargin;
            }
        }

        // The line two faces meet on, nearest to the reference point: an edge they share (up to kMargin apart), or for two
        // faces in one plane (brushes side by side, or overlapping) an edge of the one that runs through the other; failing
        // that, where their planes cross
        static bool LineOf(in Face a, in Face b, float3 reference, KeptLine kept, out float3 point, out float3 direction, out bool coplanar)
        {
            coplanar = math.dot(a.normal, b.normal) > kNearlyParallel;
            var inPlane = coplanar && math.abs(a.distance - b.distance) <= kMargin;
            point = default;
            direction = default;
            var best = float.PositiveInfinity;
            var onKept = false;
            NearestSharedEdge(a, b, inPlane, reference, kept, ref onKept, ref best, ref point, ref direction);
            NearestSharedEdge(b, a, inPlane, reference, kept, ref onKept, ref best, ref point, ref direction);
            if (best < float.PositiveInfinity)
                return true;
            direction = math.cross(a.normal, b.normal);
            var lengthSq = math.lengthsq(direction);
            if (coplanar || lengthSq < kEpsilon * kEpsilon)
                return false;
            point = (math.cross(b.normal, direction) * a.distance + math.cross(direction, a.normal) * b.distance) / lengthSq;
            direction = math.normalize(direction);
            return true;
        }

        // Faces whose normals are closer than this (about 2.5 degrees) are nearly in one plane, where their planes cross is
        // no use
        const float kNearlyParallel = 0.999f;

        // The edge of the face, nearest to the reference point, that the other face shares, or that runs through the other
        // face when the two lie in one plane (not along its border)
        static void NearestSharedEdge(in Face face, in Face other, bool inPlane, float3 reference, KeptLine kept, ref bool onKept, ref float best,
                                      ref float3 point, ref float3 direction)
        {
            var outline = face.outline;
            for (int i = 0; i < outline.Count; i++)
            {
                var from = outline[i];
                var along = outline[(i + 1) % outline.Count] - from;
                var length = math.length(along);
                if (length < kEpsilon)
                    continue;
                along /= length;
                var candidate = SharesEdge(other, from, along, length);
                if (!candidate && inPlane && !HasEdgeOn(other, from, along))
                {
                    float start = 0, end = length;
                    candidate = Clip(other, from, along, ref start, ref end) && end - start > kMargin;
                }
                if (!candidate)
                    continue;
                var t = math.clamp(math.dot(reference - from, along), 0, length);
                var distance = math.distance(reference, from + along * t);
                // the line the trim lay on wins while the reference point is on it
                var isKept = distance < kMargin && kept.Holds(from, from + along * length);
                if (onKept && !isKept)
                    continue;
                if (distance >= best && !(isKept && !onKept))
                    continue;
                onKept |= isKept;
                best = distance;
                point = from;
                direction = along;
            }
        }

        // Whether an edge of the face lies on the line and overlaps its stretch [0, length] by more than kMargin
        static bool SharesEdge(in Face face, float3 point, float3 direction, float length)
        {
            var outline = face.outline;
            for (int i = 0; i < outline.Count; i++)
            {
                var v0 = outline[i];
                var v1 = outline[(i + 1) % outline.Count];
                if (math.length(math.cross(v0 - point, direction)) >= kMargin || math.length(math.cross(v1 - point, direction)) >= kMargin)
                    continue;
                var t0 = math.dot(v0 - point, direction);
                var t1 = math.dot(v1 - point, direction);
                if (math.min(length, math.max(t0, t1)) - math.max(0, math.min(t0, t1)) > kMargin)
                    return true;
            }
            return false;
        }

        static bool HasEdgeOn(in Face face, float3 point, float3 direction)
        {
            var outline = face.outline;
            for (int i = 0; i < outline.Count; i++)
            {
                if (math.length(math.cross(outline[i] - point, direction)) < kMargin &&
                    math.length(math.cross(outline[(i + 1) % outline.Count] - point, direction)) < kMargin)
                    return true;
            }
            return false;
        }

        // Some of the face lies in front of the other face's plane
        static bool InFrontOf(in Face face, in Face other)
        {
            foreach (var vertex in face.outline)
            {
                if (math.dot(other.normal, vertex) - other.distance > kMargin)
                    return true;
            }
            return false;
        }

        static bool TryBrushOf(CSGTreeNode node, int surfaceIndex, out CSGTreeBrush brush, out int polygon)
        {
            brush = default;
            polygon = -1;
            if (!node.Valid)
                return false;
            if (node.Type == CSGNodeType.Brush)
            {
                brush = (CSGTreeBrush)node;
                var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
                if (!blob.IsCreated)
                    return false;
                for (int p = 0; p < blob.Value.polygons.Length; p++)
                {
                    if (blob.Value.polygons[p].descriptionIndex == surfaceIndex)
                    {
                        polygon = p;
                        return true;
                    }
                }
                return false;
            }
            for (int i = 0; i < node.Count; i++)
            {
                if (TryBrushOf(node[i], surfaceIndex, out brush, out polygon))
                    return true;
            }
            return false;
        }

        // A target's surface, of the first brush of its component that has it (depth first)
        static bool TryFace(ChiselDecalSurfaceTarget target, out ChiselModelComponent model, out Face face)
        {
            face = default;
            model = null;
            if (target.brush == null || target.surfaceIndex < 0 || target.brush is not ChiselGeneratorComponent generator)
                return false;
            model = generator.hierarchyItem.Model;
            if (model == null)
                return false;
            return TryFace(generator.TopTreeNode, target.surfaceIndex, out face);
        }

        static bool TryFace(CSGTreeNode node, int surfaceIndex, out Face face)
        {
            face = default;
            if (!node.Valid)
                return false;
            if (node.Type == CSGNodeType.Brush)
                return TryBrushFace((CSGTreeBrush)node, surfaceIndex, out face);
            for (int i = 0; i < node.Count; i++)
            {
                if (TryFace(node[i], surfaceIndex, out face))
                    return true;
            }
            return false;
        }

        static bool TryBrushFace(CSGTreeBrush brush, int surfaceIndex, out Face face)
        {
            face = default;
            var blob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh);
            if (!blob.IsCreated)
                return false;
            ref var mesh = ref blob.Value;
            // a target's surface is the generator's (the polygon's descriptionIndex), as the decals' targets match it
            var polygonIndex = -1;
            for (int p = 0; p < mesh.polygons.Length && p < mesh.localPlanes.Length; p++)
            {
                if (mesh.polygons[p].descriptionIndex == surfaceIndex)
                {
                    polygonIndex = p;
                    break;
                }
            }
            if (polygonIndex < 0)
                return false;
            var nodeToTree = brush.NodeToTreeSpaceMatrix;
            var planeToTree = math.transpose(math.inverse(nodeToTree));
            var plane = math.mul(planeToTree, mesh.localPlanes[polygonIndex]);
            var normalLength = math.length(plane.xyz);
            if (!(normalLength > kEpsilon))
                return false;
            ref var polygon = ref mesh.polygons[polygonIndex];
            var outline = new List<float3>(polygon.edgeCount);
            var centroid = float3.zero;
            for (int e = 0; e < polygon.edgeCount; e++)
            {
                var vertex = math.transform(nodeToTree, mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e].vertexIndex]);
                outline.Add(vertex);
                centroid += vertex;
            }
            if (outline.Count < 3)
                return false;
            face = new Face
            {
                normal   = plane.xyz / normalLength,
                distance = -plane.w / normalLength,
                centroid = centroid / outline.Count,
                outline  = outline
            };
            return true;
        }
    }
}
