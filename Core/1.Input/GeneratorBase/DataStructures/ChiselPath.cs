using System;
using System.Linq;

using UnityEngine;

namespace Chisel.Core
{
    // TODO: turn into an asset so we can share it between multiple generators??
    [Serializable]
    public class ChiselPath
    {
        const int kLatestVersion = 1;
        [UnityEngine.SerializeField] int version = 0;

        public ChiselPath() { }

        public ChiselPath(ChiselPath other)
        {
            this.segments = other.segments.ToArray();
        }

        public ChiselPath(ChiselPathPoint[] points)
        {
            this.segments = points.ToArray();
        }

        public ChiselPathPoint[]  segments;

        public readonly static ChiselPath Default = new( new[]
        {
            new ChiselPathPoint(Vector3.zero),
            new ChiselPathPoint(ChiselPathPoint.DefaultDirection)
        });


		public void UpgradeIfNecessary()
        {
            if (version == kLatestVersion)
                return;

            version = kLatestVersion;
            if (this.segments == null ||
                this.segments.Length == 0)
                return;

            for (int i = 0; i < this.segments.Length; i++)
                this.segments[i].rotation = ChiselPathPoint.DefaultRotation;
        }
	}


	[Serializable]
	public struct ChiselPathPoint
	{
		static Vector3 kDefaultDirection = Vector3.up;
		public static Vector3 DefaultDirection { get { return kDefaultDirection; } }


		static Quaternion kDefaultRotation = Quaternion.LookRotation(kDefaultDirection);
		public static Quaternion DefaultRotation { get { return kDefaultRotation; } }

		public ChiselPathPoint(Vector3 position, Quaternion rotation, Vector3 scale)
		{
			this.position = position;
			this.rotation = rotation;
			this.scale = scale;
		}

		public ChiselPathPoint(Vector3 position)
		{
			this.position = position;
			this.rotation = ChiselPathPoint.DefaultRotation;
			this.scale = Vector3.one;
		}

		[PositionValue] public Vector3 position;
		[EulerValue] public Quaternion rotation;
		[ScaleValue] public Vector2 scale;

		public readonly Matrix4x4 ToMatrix()
		{
			return ToMatrix(position, rotation, scale);
		}

		static Matrix4x4 ToMatrix(Vector3 position, Quaternion rotation, Vector2 scale)
		{
			return Matrix4x4.TRS(position, Quaternion.Inverse(rotation), new Vector3(scale.x, scale.y, -1));
		}

		public static Matrix4x4 Lerp(ref ChiselPathPoint A, ref ChiselPathPoint B, float t)
		{
			var position = MathExtensions.Lerp(A.position, B.position, t);
			var rotation = MathExtensions.Lerp(A.rotation, B.rotation, t);
			var scale = MathExtensions.Lerp(A.scale, B.scale, t);
			return ToMatrix(position, rotation, scale);
		}
	}
}
