using System;
using System.Runtime.InteropServices;
using Vector3	= UnityEngine.Vector3;
using Plane		= UnityEngine.Plane;

namespace Chisel.Core
{
    /// <summary>
    /// This class defines an intersection into a specific surface of a brush
    /// </summary>
    [Serializable, StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ChiselSurfaceIntersection
    {
        public Plane    treePlane;
        public Vector3  treePlaneIntersection;

        public float	distance;

        public readonly static ChiselSurfaceIntersection None = new()
        {
            treePlane               = default,
            treePlaneIntersection   = default,
            distance                = float.NaN
        };
    };
}
