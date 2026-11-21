using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    public struct ChiselPathBlob
    {
        public struct Point
        {
            public float3       position;
            public quaternion   rotation;
            public float2       scale;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static float4x4 ToMatrix(float3 position, quaternion rotation, float2 scale)
            {
                return float4x4.TRS(position, math.inverse(rotation), new float3(scale.x,scale.y,-1)); 
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly float4x4 ToMatrix()
            {
                return ToMatrix(position, rotation, scale); 
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float4x4 Lerp(ref Point A, ref Point B, float t)
            {
                var position = MathExtensions.Lerp(A.position, B.position, t);
                var rotation = MathExtensions.Lerp(A.rotation, B.rotation, t);
                var scale    = MathExtensions.Lerp(A.scale, B.scale, t);
                return ToMatrix(position, rotation, scale);
            }
        }

        // the most a step of the path turns, in degrees
        const float kMaxStepDegrees = 5;

        // A matrix for each point of the path, and between two that turn apart, as many as it takes for no step to turn
        // more than kMaxStepDegrees (an even number of steps when the shape's aspect changes)
        public UnsafeList<float4x4> GetUnsafeMatrices(Allocator allocator)
        {
            var matrices = new UnsafeList<float4x4>(segments.Length, allocator) { segments[0].ToMatrix() };
            for (int s = 1; s < segments.Length; s++)
            {
                var pathPointA = segments[s - 1];
                var pathPointB = segments[s    ];

                if (math.all(pathPointA.position == pathPointB.position) &&
                    math.all(pathPointA.rotation.value == pathPointB.rotation.value) &&
                    math.all(pathPointA.scale == pathPointB.scale))
                    continue;

                // how far apart the two rotations are: the angle of the one that takes A's to B's
                var offset  = math.mul(pathPointB.rotation, math.inverse(pathPointA.rotation));
                var degrees = math.degrees(2 * math.acos(math.min(1, math.abs(offset.value.w))));
                var subSegments = math.max(1, (int)math.ceil(degrees / kMaxStepDegrees));

                if ((pathPointA.scale.x / pathPointA.scale.y) != (pathPointB.scale.x / pathPointB.scale.y) &&
                    (subSegments & 1) == 1)
                    subSegments += 1;

                for (int n = 1; n <= subSegments; n++)
                    matrices.Add(ChiselPathBlob.Point.Lerp(ref pathPointA, ref pathPointB, n / (float)subSegments));
            }
            return matrices;
        }


        public BlobArray<Point> segments;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Point Convert(ChiselPathPoint srcPoint)
        {
            return new Point
            {
                position = srcPoint.position,
                rotation = srcPoint.rotation,
                scale    = srcPoint.scale,
            };
        }

        public static BlobAssetReference<ChiselPathBlob> Convert(ChiselPath path, Allocator allocator)
        {
            path.UpgradeIfNecessary();
			using var builder = new BlobBuilder(Allocator.Temp);
			ref var root = ref builder.ConstructRoot<ChiselPathBlob>();
			var srcControlPoints = path.segments;
			var dstControlPoints = builder.Allocate(ref root.segments, srcControlPoints.Length);
			for (int i = 0; i < srcControlPoints.Length; i++)
				dstControlPoints[i] = Convert(srcControlPoints[i]);
			return builder.CreateBlobAssetReference<ChiselPathBlob>(allocator);
		}
    }
}