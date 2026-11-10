using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Chisel.Core
{
    public struct SegmentVertex
    {
        public float2   position;
        public int      segmentIndex;
    }

    public struct ChiselCurve2DBlob
    {
        public struct Point
        {
            public float2 position;
            public float2 tangent1;
            public float2 tangent2;
            public ControlPointConstraint constraint1;
            public ControlPointConstraint constraint2;
        }

        public bool closed;
        public BlobArray<Point> controlPoints;

        static Point Convert(CurveControlPoint2D srcPoint)
        {
            return new Point
            {
                position = srcPoint.position,
                tangent1 = srcPoint.tangent1,
                tangent2 = srcPoint.tangent2,
                constraint1 = srcPoint.constraint1,
                constraint2 = srcPoint.constraint2
            };
        }

        static float2 PointOnBezier(float2 p0, float2 p1, float2 p2, float2 p3, float t)
        {
            return (1 - t) * (1 - t) * (1 - t) * p0 + 3 * t * (1 - t) * (1 - t) * p1 + 3 * t * t * (1 - t) * p2 + t * t * t * p3;
        }

        // which way the curve goes at t, not normalized
        static float2 DirectionOnBezier(float2 p0, float2 p1, float2 p2, float2 p3, float t)
        {
            return ((1 - t) * (1 - t) * (p1 - p0)) + (2 * t * (1 - t) * (p2 - p1)) + (t * t * (p3 - p2));
        }

        public void AddPathVertices(int shapeCurveSegments, NativeList<SegmentVertex> shapeVertices)
        {
            var length = controlPoints.Length;

            for (int i = 0; i < length; i++)
            {
                var index1 = i;
                var index2 = (i + 1) % length;
                var p1 = controlPoints[index1];
                var p2 = controlPoints[index2];
                var v1 = p1.position;
                var v2 = p2.position;

                if (shapeCurveSegments == 0 ||
                    (controlPoints[index1].constraint2 == ControlPointConstraint.Straight &&
                     controlPoints[index2].constraint1 == ControlPointConstraint.Straight))
                {
                    shapeVertices.Add(new SegmentVertex { position = v1, segmentIndex = i });
                    continue;
                }

                float2 v0, v3;

                if (p1.constraint2 != ControlPointConstraint.Straight)
                    v0 = v1 - p1.tangent2;
                else
                    v0 = v1;
                if (p2.constraint1 != ControlPointConstraint.Straight)
                    v3 = v2 - p2.tangent1;
                else
                    v3 = v2;

                shapeVertices.Add(new SegmentVertex { position = v1, segmentIndex = i });
                for (int n = 1; n < shapeCurveSegments; n++)
                {
                    shapeVertices.Add(new SegmentVertex { position = PointOnBezier(v1, v0, v3, v2, n / (float)shapeCurveSegments), segmentIndex = i });
                }
            }
        }

        const int kTurnSamples = 64;

        // The path with each curved span cut into as many parts as it needs for none to turn more than maxTurn
        // (radians), each turning as much as the others
        public UnsafeList<SegmentVertex> GetPathVerticesByTurn(float maxTurn, Allocator allocator)
        {
            var length = controlPoints.Length;
            var shapeVertices = new UnsafeList<SegmentVertex>(length * 4, allocator);
            var turns = new NativeArray<float>(kTurnSamples + 1, Allocator.Temp);
            for (int i = 0; i < length; i++)
            {
                var p1 = controlPoints[i];
                var p2 = controlPoints[(i + 1) % length];
                var v1 = p1.position;
                var v2 = p2.position;
                shapeVertices.Add(new SegmentVertex { position = v1, segmentIndex = i });
                if (p1.constraint2 == ControlPointConstraint.Straight &&
                    p2.constraint1 == ControlPointConstraint.Straight)
                    continue;

                var v0 = (p1.constraint2 != ControlPointConstraint.Straight) ? v1 - p1.tangent2 : v1;
                var v3 = (p2.constraint1 != ControlPointConstraint.Straight) ? v2 - p2.tangent1 : v2;

                // how far it has turned by each sample
                turns[0] = 0;
                var prevDirection = math.normalizesafe(DirectionOnBezier(v1, v0, v3, v2, 0));
                for (int s = 1; s <= kTurnSamples; s++)
                {
                    var direction = math.normalizesafe(DirectionOnBezier(v1, v0, v3, v2, s / (float)kTurnSamples));
                    var turn = 0.0f;
                    if (math.any(prevDirection != float2.zero) && math.any(direction != float2.zero))
                        turn = math.abs(math.atan2((prevDirection.x * direction.y) - (prevDirection.y * direction.x), math.dot(prevDirection, direction)));
                    turns[s] = turns[s - 1] + turn;
                    if (math.any(direction != float2.zero))
                        prevDirection = direction;
                }

                var parts = (int)math.ceil(turns[kTurnSamples] / maxTurn - 1e-4f);
                for (int n = 1, s = 1; n < parts; n++)
                {
                    var target = turns[kTurnSamples] * n / parts;
                    while (s < kTurnSamples && turns[s] < target)
                        s++;
                    var span = turns[s] - turns[s - 1];
                    var fraction = (span > 0) ? (target - turns[s - 1]) / span : 0;
                    var t = (s - 1 + fraction) / kTurnSamples;
                    shapeVertices.Add(new SegmentVertex { position = PointOnBezier(v1, v0, v3, v2, t), segmentIndex = i });
                }
            }
            turns.Dispose();
            return shapeVertices;
        }

        public static float CalculateOrientation(UnsafeList<SegmentVertex> vertices, Range range)
        {
            // Newell's algorithm to create a plane for concave polygons.
            // NOTE: doesn't work well for self-intersecting polygons
            var direction = 0.0f;
            var prevVertex = vertices[range.end - 1].position;
            for (int n = range.start; n < range.end; n++)
            {
                var currVertex = vertices[n].position;
                direction += (prevVertex.x - currVertex.x) * (prevVertex.y + currVertex.y);
                prevVertex = currVertex;
            }
            return direction;
        }

        /*
        public bool ConvexPartition(int curveSegments, out NativeList<SegmentVertex> polygonVerticesArray, out NativeList<int> polygonVerticesSegments, Allocator allocator)
        {
			using var shapeVertices = new NativeList<SegmentVertex>(Allocator.Temp);
			AddPathVertices(curveSegments, shapeVertices);

			polygonVerticesArray = new NativeList<SegmentVertex>(allocator);
			polygonVerticesSegments = new NativeList<int>(allocator);

			//Profiler.BeginSample("ConvexPartition");
			if (shapeVertices.Length == 3)
			{
				polygonVerticesArray.ResizeUninitialized(3);
				polygonVerticesArray[0] = shapeVertices[0];
				polygonVerticesArray[1] = shapeVertices[1];
				polygonVerticesArray[2] = shapeVertices[2];

				polygonVerticesSegments.ResizeUninitialized(1);
				polygonVerticesSegments[0] = polygonVerticesArray.Length;
			}
			else
			{
				if (!External.BayazitDecomposerBursted.ConvexPartition(shapeVertices,
																	   polygonVerticesArray,
																	   polygonVerticesSegments))
				{
					polygonVerticesArray.Dispose();
					polygonVerticesSegments.Dispose();
					polygonVerticesArray = default;
					polygonVerticesSegments = default;
					return false;
				}

				for (int i = 0; i < polygonVerticesSegments.Length; i++)
				{
					var range = new Range
					{
						start = i == 0 ? 0 : polygonVerticesSegments[i - 1],
						end = polygonVerticesSegments[i]
					};

					if (CalculateOrientation(polygonVerticesArray, range) < 0)
						External.BayazitDecomposerBursted.Reverse(polygonVerticesArray, range);
				}
			}
			//Profiler.EndSample();

			//Debug.Assert(polygonVerticesArray.Length == 0 || polygonVerticesArray.Length == polygonVerticesSegments[polygonVerticesSegments.Length - 1]);
			return true;
		}
        */

        public bool ConvexPartition(int curveSegments, out UnsafeList<SegmentVertex> polygonVerticesArray, out UnsafeList<int> polygonVerticesSegments,
                                    Allocator allocator = Allocator.Persistent) // Indirect
        {
			using var shapeVertices = new NativeList<SegmentVertex>(Allocator.Temp);
			AddPathVertices(curveSegments, shapeVertices);

			// an outline that crosses itself is split where it crosses, into loops that don't, each filled on its own
			using var loopVertices = new NativeList<SegmentVertex>(Allocator.Temp);
			using var loopEnds     = new NativeList<int>(Allocator.Temp);
			SplitAtCrossings(shapeVertices, loopVertices, loopEnds);

			polygonVerticesArray = new UnsafeList<SegmentVertex>(shapeVertices.Length * math.max(1, shapeVertices.Length / 2), allocator);//Allocator.Persistent
			polygonVerticesSegments = new UnsafeList<int>(shapeVertices.Length, allocator);//Allocator.Persistent
			using var loop = new NativeList<SegmentVertex>(Allocator.Temp);
			for (int l = 0; l < loopEnds.Length; l++)
			{
				var start = l == 0 ? 0 : loopEnds[l - 1];
				loop.Clear();
				for (int n = start; n < loopEnds[l]; n++)
					loop.Add(loopVertices[n]);
				if (loop.Length < 3)
					continue;
				if (loop.Length == 3)
				{
					polygonVerticesArray.Add(loop[0]);
					polygonVerticesArray.Add(loop[1]);
					polygonVerticesArray.Add(loop[2]);
					polygonVerticesSegments.Add(polygonVerticesArray.Length);
					continue;
				}
				if (!External.BayazitDecomposerBursted.ConvexPartition(loop,
																	   ref polygonVerticesArray,
																	   ref polygonVerticesSegments))
				{
					polygonVerticesArray.Dispose();
					polygonVerticesSegments.Dispose();
					polygonVerticesArray = default;
					polygonVerticesSegments = default;
					return false;
				}
			}
			if (polygonVerticesSegments.Length == 0)
			{
				polygonVerticesArray.Dispose();
				polygonVerticesSegments.Dispose();
				polygonVerticesArray = default;
				polygonVerticesSegments = default;
				return false;
			}

			for (int i = 0; i < polygonVerticesSegments.Length; i++)
			{
				var range = new Range
				{
					start = i == 0 ? 0 : polygonVerticesSegments[i - 1],
					end = polygonVerticesSegments[i]
				};

				if (CalculateOrientation(polygonVerticesArray, range) < 0)
					External.BayazitDecomposerBursted.Reverse(polygonVerticesArray, range);
			}
			//Profiler.EndSample();

			//Debug.Assert(polygonVerticesArray.Length == 0 || polygonVerticesArray.Length == polygonVerticesSegments[polygonVerticesSegments.Length - 1]);
			return true;
		}

        // Splits a closed outline where two of its edges cross, over and over, until no loop crosses itself. At a crossing
        // the outline becomes two loops that both start at the crossing point: one runs along the part between the two
        // edges, the other along the rest. Each new corner keeps the side of the edge it starts
        public static void SplitAtCrossings(NativeList<SegmentVertex> vertices, NativeList<SegmentVertex> loopVertices, NativeList<int> loopEnds)
        {
            using var pending = new NativeList<SegmentVertex>(Allocator.Temp);
            using var pendingEnds = new NativeList<int>(Allocator.Temp);
            pending.AddRange(vertices.AsArray());
            pendingEnds.Add(pending.Length);

            using var loop  = new NativeList<SegmentVertex>(Allocator.Temp);
            using var first = new NativeList<SegmentVertex>(Allocator.Temp);
            using var other = new NativeList<SegmentVertex>(Allocator.Temp);
            // every split adds one loop; an outline can't cross itself more often than its edges can pair up
            var maxLoops = math.max(1, vertices.Length * vertices.Length);
            var done = 0;
            while (pendingEnds.Length > 0)
            {
                var start = pendingEnds.Length == 1 ? 0 : pendingEnds[pendingEnds.Length - 2];
                var end   = pendingEnds[pendingEnds.Length - 1];
                loop.Clear();
                for (int n = start; n < end; n++)
                    loop.Add(pending[n]);
                pending.ResizeUninitialized(start);
                pendingEnds.RemoveAt(pendingEnds.Length - 1);

                var count = loop.Length;
                var split = false;
                if (count > 3 && done < maxLoops)
                {
                    for (int i = 0; i < count && !split; i++)
                    {
                        var a0 = loop[i].position;
                        var a1 = loop[(i + 1) % count].position;
                        // edges next to each other share a corner, they don't cross
                        for (int j = i + 2; j < count && !split; j++)
                        {
                            if (i == 0 && j == count - 1)
                                continue;
                            var b0 = loop[j].position;
                            var b1 = loop[(j + 1) % count].position;
                            if (!Crossing(a0, a1, b0, b1, out var point))
                                continue;

                            first.Clear();
                            first.Add(new SegmentVertex { position = point, segmentIndex = loop[i].segmentIndex });
                            for (int n = i + 1; n <= j; n++)
                                first.Add(loop[n]);
                            other.Clear();
                            other.Add(new SegmentVertex { position = point, segmentIndex = loop[j].segmentIndex });
                            for (int n = j + 1; n < count; n++)
                                other.Add(loop[n]);
                            for (int n = 0; n <= i; n++)
                                other.Add(loop[n]);

                            pending.AddRange(first.AsArray());
                            pendingEnds.Add(pending.Length);
                            pending.AddRange(other.AsArray());
                            pendingEnds.Add(pending.Length);
                            split = true;
                            done++;
                        }
                    }
                }
                if (split)
                    continue;
                loopVertices.AddRange(loop.AsArray());
                loopEnds.Add(loopVertices.Length);
            }
        }

        // Where two edges cross each other, strictly inside both
        static bool Crossing(float2 a0, float2 a1, float2 b0, float2 b1, out float2 point)
        {
            point = default;
            var da = a1 - a0;
            var db = b1 - b0;
            var denominator = da.x * db.y - da.y * db.x;
            if (math.abs(denominator) < 1e-9f)
                return false;
            var offset = b0 - a0;
            var t = (offset.x * db.y - offset.y * db.x) / denominator;
            var u = (offset.x * da.y - offset.y * da.x) / denominator;
            const float kEpsilon = 1e-5f;
            if (t <= kEpsilon || t >= 1 - kEpsilon || u <= kEpsilon || u >= 1 - kEpsilon)
                return false;
            point = a0 + da * t;
            return true;
        }

        public static BlobAssetReference<ChiselCurve2DBlob> Convert(Curve2D curve, Allocator allocator)
        {
			using var builder = new BlobBuilder(Allocator.Temp);
			ref var root = ref builder.ConstructRoot<ChiselCurve2DBlob>();
			root.closed = curve.closed;
			var srcControlPoints = curve.controlPoints;
			var dstControlPoints = builder.Allocate(ref root.controlPoints, srcControlPoints.Length);
			for (int i = 0; i < srcControlPoints.Length; i++)
				dstControlPoints[i] = Convert(srcControlPoints[i]);
			return builder.CreateBlobAssetReference<ChiselCurve2DBlob>(allocator);
		}
    }
}