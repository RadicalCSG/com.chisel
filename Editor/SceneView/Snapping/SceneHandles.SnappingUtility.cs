using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    public static class SnappingUtility
    {
        readonly static Vector3 kUnitXVector3 = new(1, 0, 0);
        readonly static Vector3 kUnitYVector3 = new(0, 1, 0);
        readonly static Vector3 kUnitZVector3 = new(0, 0, 1);
        const float	kMinimumSnapDistance = 0.00001f;


        public static float WorldPointToDistance(Vector3 currentPosition, Vector3 direction)
        {
            return Vector3.Dot(direction, currentPosition);
        }

        public static Vector3 DistanceToWorldPoint(float distance, Vector3 direction)
        {
            return (direction * distance);
        }
         
        public static float WorldPointToDistance(Vector3 currentPosition, Vector3 direction, Vector3 origin)
        {
            return WorldPointToDistance(currentPosition - origin, direction);
        }

        public static Vector3 DistanceToWorldPoint(float distance, Vector3 direction, Vector3 origin)
        {
            return DistanceToWorldPoint(distance, direction) + origin;
        }

        public static Vector3 WorldPointToDistances(Vector3 currentPosition, Vector3 axisX, Vector3 axisY, Vector3 axisZ)
        {
            return new Vector3(Vector3.Dot(axisX, currentPosition),
                               Vector3.Dot(axisY, currentPosition),
                               Vector3.Dot(axisZ, currentPosition));
        }

        public static Vector3 WorldPointToDistances(Vector3 currentPosition, Vector3 axisX, Vector3 axisY, Vector3 axisZ, Vector3 origin)
        {
            return WorldPointToDistances(currentPosition - origin, axisX, axisY, axisZ);
        }

        public static Vector3 DistancesToWorldPoint(Vector3 distances, Vector3 axisX, Vector3 axisY, Vector3 axisZ)
        {
            return	(axisX * distances.x) +
                    (axisY * distances.y) +
                    (axisZ * distances.z);
        }

        public static Vector3 DistancesToWorldPoint(Vector3 distances, Vector3 axisX, Vector3 axisY, Vector3 axisZ, Vector3 origin)
        {
            return DistancesToWorldPoint(distances, axisX, axisY, axisZ) + origin;
        }


        public static float SnapValue(float currentPosition, float snapping)
        {
            if (Mathf.Abs(snapping) < kMinimumSnapDistance)
                return currentPosition;
            return Mathf.Round(currentPosition / snapping) * snapping;
        }







        public static float SnapPoint1D(float currentPosition, float snapping, float origin)
        {
            return SnapValue(currentPosition - origin, snapping) + origin;
        }

        public static float SnapDistancePoint1D(float currentPosition, float snapping, float origin = 0.0f)
        {
            return SnapPoint1D(currentPosition, snapping, origin) - currentPosition;
        }

        public static float SnapDistanceExtents1D(Extents1D currentExtents, float snapping, float origin = 0.0f)
        {
            var snapMin = SnapPoint1D(currentExtents.min, snapping, origin);
            var snapMax = SnapPoint1D(currentExtents.max, snapping, origin);
            var deltaMin = snapMin - currentExtents.min;
            var deltaMax = snapMax - currentExtents.max;
            return (Mathf.Abs(deltaMin) < Mathf.Abs(deltaMax)) ? deltaMin : deltaMax;
        }

        public static Extents1D SnapExtents1D(Extents1D currentExtents, float snapping, float origin = 0.0f)
        {
            return currentExtents + SnapDistanceExtents1D(currentExtents, snapping, origin);
        }
        public static float SnapDistanceExtentsRay(Extents1D currentExtents, float snapping, Vector3 direction, Vector3 origin)
        {
            var deltaOrigin = WorldPointToDistance(origin, direction);
            return SnapDistanceExtents1D(currentExtents, snapping, deltaOrigin);
        }
        public static Extents1D SnapExtentsRay(Extents1D currentExtents, float snapping, Vector3 direction, Vector3 origin)
        {
            return currentExtents + SnapDistanceExtentsRay(currentExtents, snapping, direction, origin);
        }



        public static Vector3 SnapPoint3D(Vector3 currentPosition, Vector3 snapping, Vector3 axisX, Vector3 axisY, Vector3 axisZ, Vector3 origin, Axes enabledAxes = Axes.XYZ)
        {
            var currentDistances = WorldPointToDistances(currentPosition, axisX, axisY, axisZ, origin);
            if ((enabledAxes & Axes.X) > 0) currentDistances.x = SnapValue(currentDistances.x, snapping.x);
            if ((enabledAxes & Axes.Y) > 0) currentDistances.y = SnapValue(currentDistances.y, snapping.y);
            if ((enabledAxes & Axes.Z) > 0) currentDistances.z = SnapValue(currentDistances.z, snapping.z);
            return DistancesToWorldPoint(currentDistances, axisX, axisY, axisZ, origin);
        }

        public static Vector3 SnapDistancePoint3D(Vector3 currentPosition, Vector3 snapping, Vector3 axisX, Vector3 axisY, Vector3 axisZ, Vector3 origin, Axes enabledAxes = Axes.XYZ)
        {
            return SnapPoint3D(currentPosition, snapping, axisX, axisY, axisZ, origin, enabledAxes) - currentPosition;
        }


        public static float SnapDistancePointRay(Vector3 currentPosition, float snapping, Vector3 direction, Vector3 origin)
        {
            var currentDistanceAlongAxis = WorldPointToDistance(currentPosition, direction, origin);
            var snappedDistanceAlongAxis = SnapValue(currentDistanceAlongAxis, snapping);
            return snappedDistanceAlongAxis;
        }
        
        public static Vector3 SnapPointRay(Vector3 currentPosition, float snapping, Vector3 direction, Vector3 origin)
        {
            var snappedDistanceAlongAxis = SnapDistancePointRay(currentPosition, snapping, direction, origin);
            return DistanceToWorldPoint(snappedDistanceAlongAxis, direction, origin);
        }



        public static float SnapDistancePointRay(Vector3 currentPosition, float snapping, Ray ray)
        {
            return SnapDistancePointRay(currentPosition, snapping, ray.direction, ray.origin);
        }

        public static Vector3 SnapPointRay(Vector3 currentPosition, float snapping, Ray ray)
        {
            return SnapPointRay(currentPosition, snapping, ray.direction, ray.origin);
        }



        public static float Quantize(float value)
        {
            value = (float)(System.Math.Round(((double)value * (double)10000.0)) / (double)10000.0);
            return value;
        }
        
        public static Vector3 Quantize(Vector3 value)
        {
            value.x = Quantize(value.x);
            value.y = Quantize(value.y);
            value.z = Quantize(value.z);
            return value;
        }

        
        public static Vector3 PerformAxisLocking(Vector3 startPosition, Vector3 currentPosition)
        {
            var locking		= Snapping.AxisLocking;
            var deltaAxisX	= Vector3.Dot(kUnitXVector3, locking[0] ? startPosition : currentPosition);
            var deltaAxisY	= Vector3.Dot(kUnitYVector3, locking[1] ? startPosition : currentPosition);
            var deltaAxisZ	= Vector3.Dot(kUnitZVector3, locking[2] ? startPosition : currentPosition);
            
            return ((kUnitXVector3 * deltaAxisX) + (kUnitYVector3 * deltaAxisY) + (kUnitZVector3 * deltaAxisZ));
        }

        
        public static Vector3 PerformAxisLocking(Vector3 startPosition, Vector3 currentPosition, Axis axis)
        {
            var locking		= Snapping.AxisLocking;
            
            var deltaAxisX	= Vector3.Dot(kUnitXVector3, ((axis == Axis.X) && locking[0]) ? startPosition : currentPosition);
            var deltaAxisY	= Vector3.Dot(kUnitYVector3, ((axis == Axis.Y) && locking[1]) ? startPosition : currentPosition);
            var deltaAxisZ	= Vector3.Dot(kUnitZVector3, ((axis == Axis.Z) && locking[2]) ? startPosition : currentPosition);
            
            return ((kUnitXVector3 * deltaAxisX) + (kUnitYVector3 * deltaAxisY) + (kUnitZVector3 * deltaAxisZ));
        }
    }
}
