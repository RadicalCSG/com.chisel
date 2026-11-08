using System;
using System.Collections.Generic;

using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    public enum LineMode
    {
        ZTest,
        NoZTest,
    }

    public interface IChiselHandleRenderer
    {
        Matrix4x4 matrix { get; set; }
        Matrix4x4 inverseMatrix { get; }
        Color color { get; set; }
        bool disabled { get; }
        bool backfaced { get; set; }
        bool IsIn2DMode { get; }

        Color GetStateColor(Color baseColor, bool hasFocus, bool isBackfaced);

        void RenderBox(Bounds bounds);
        void RenderBoxMeasurements(Bounds bounds);
        void RenderCylinder(Bounds bounds, int segments);
        void RenderDistanceMeasurement(Vector3 from, Vector3 to);
        void RenderDistanceMeasurement(Vector3 from, Vector3 to, float forceValue, string name = null);
        // with a second length on a line under the first in its label
        void RenderDistanceMeasurement(Vector3 from, Vector3 to, float forceValue, string name, float secondValue, string secondName);
        // a text in the scene, next to a point on the side it's put toward
        void RenderLabel(Vector3 position, Vector3 toward, string text);
        void RenderShape(Curve2D shape, float height);

        void DrawLine(Vector3 from, Vector3 to, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawContinuousLines(Vector3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawContinuousLines(float3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawContinuousLines(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawContinuousLines(float3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawLineLoop(Vector3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawLineLoop(float3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawLineLoop(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawLineLoop(float3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
    }

    public static class IChiselHandleRendererExtensions
	{

		public static bool DoBoundsHandle(this IChiselHandles handles, ref MinMaxAABB box, Vector3? snappingSteps = null, string undoMessage = null, bool fixedBottom = false)
		{
			var bounds = box.ToBounds();
			var result = handles.DoBoundsHandle(ref bounds, snappingSteps, undoMessage, fixedBottom);
			box = bounds.ToMinMaxAABB();
			return result;
		}

		public static bool DoTurnHandle(this IChiselHandles handles, ref MinMaxAABB box, string undoMessage = null)
		{
			var bounds = box.ToBounds();
			var result = handles.DoTurnHandle(ref bounds, undoMessage);
			box = bounds.ToMinMaxAABB();
			return result;
		}

		public static bool DoBoxMeasurements(this IChiselHandles handles, ref MinMaxAABB box, string undoMessage = null)
		{
			var bounds = box.ToBounds();
			if (!handles.DoBoxMeasurements(ref bounds, undoMessage))
				return false;
			box = bounds.ToMinMaxAABB();
			return true;
		}

		// A circle's diameter, along a line off it so it isn't in front of the shape; can be typed in. A bottom circle has it on
		// the side nearest the camera looking down on the circle and furthest looking up at it, a top circle the reverse
		public static void DoDiameterMeasurement(this IChiselHandles handles, ref float diameter, Vector3 center, bool isTop, string name = "Diameter")
		{
			var camera = Camera.current;
			if (camera == null || diameter <= 0)
				return;
			var toCamera = camera.orthographic ? -handles.inverseMatrix.MultiplyVector(camera.transform.forward)
			                                   : handles.inverseMatrix.MultiplyPoint(camera.transform.position) - center;
			var lookingDown = toCamera.y >= 0;
			toCamera.y = 0;
			var distance = camera.orthographic ? float.PositiveInfinity : toCamera.magnitude;
			toCamera = toCamera.sqrMagnitude > 1e-8f ? toCamera.normalized : Vector3.forward;
			var onFarSide = lookingDown == isTop;
			if (onFarSide)
				toCamera = -toCamera;
			var radius   = diameter * 0.5f;
			var nearest  = center + toCamera * radius;
			var awayFrom = (toCamera + (isTop ? Vector3.up : Vector3.down)).normalized;
			var along    = Vector3.Cross(Vector3.up, toCamera);
			// on the far side as wide as the circle looks: its ends on the lines from the camera that just touch the circle, on
			// the horizon
			var halfWidth = radius;
			if (onFarSide && !float.IsInfinity(distance) && distance > radius)
			{
				var touchSide = radius * Mathf.Sqrt(1 - (radius * radius) / (distance * distance));
				var touchAt   = (radius * radius) / distance;
				halfWidth     = touchSide * (distance + radius) / (distance - touchAt);
			}
			handles.DoDistanceMeasurement(ref diameter, nearest - along * halfWidth, nearest + along * halfWidth, "Change Diameter", offsetDirection: awayFrom, name: name);
		}

		// One diameter for a cylinder's two circles of the same size: on the one nearest the camera, or the bottom one with the
		// camera in between them; or, when asked, with the camera past either, the one furthest from it
		public static void DoDiameterMeasurement(this IChiselHandles handles, ref float diameter, Vector3 topCenter, Vector3 bottomCenter, bool furthest = false, string name = "Diameter")
		{
			var camera = Camera.current;
			if (camera == null || diameter <= 0)
				return;
			var cameraY = handles.inverseMatrix.MultiplyPoint(camera.transform.position).y;
			var aboveTop    = cameraY > topCenter.y;
			var belowBottom = cameraY < bottomCenter.y;
			var useTop      = furthest ? belowBottom : aboveTop;
			handles.DoDiameterMeasurement(ref diameter, useTop ? topCenter : bottomCenter, useTop, name);
		}

		// The height of something round, along its outline on the right as seen from the camera, just outside it; can be typed in
		public static bool DoHeightMeasurement(this IChiselHandles handles, ref float height, Vector3 bottomCenter, Vector3 topCenter, float radius, string undoMessage = "Change Height")
		{
			var camera = Camera.current;
			if (camera == null || radius <= 0)
				return false;
			var middle   = (bottomCenter + topCenter) * 0.5f;
			var toCamera = camera.orthographic ? -handles.inverseMatrix.MultiplyVector(camera.transform.forward)
			                                   : handles.inverseMatrix.MultiplyPoint(camera.transform.position) - middle;
			toCamera.y = 0;
			var distance = camera.orthographic ? float.PositiveInfinity : toCamera.magnitude;
			toCamera = toCamera.sqrMagnitude > 1e-8f ? toCamera.normalized : Vector3.forward;
			var side = Vector3.Cross(Vector3.up, toCamera);
			if (Vector3.Dot(side, handles.inverseMatrix.MultiplyVector(camera.transform.right)) < 0)
				side = -side;
			// where the line from the camera just touches the circle
			var outline = side * radius;
			if (!float.IsInfinity(distance) && distance > radius)
				outline = toCamera * (radius * radius / distance) + side * (radius * Mathf.Sqrt(1 - (radius * radius) / (distance * distance)));
			var from = new Vector3(bottomCenter.x + outline.x, bottomCenter.y, bottomCenter.z + outline.z);
			var to   = new Vector3(bottomCenter.x + outline.x, topCenter.y,    bottomCenter.z + outline.z);
			return handles.DoDistanceMeasurement(ref height, from, to, undoMessage, offsetDirection: new Vector3(outline.x, 0, outline.z).normalized, name: "Height");
		}

		// Whether a side of a flat convex loop at an end of a shape is behind where the loop's outline turns, seen from inside
		// the shape: past the end's plane on the side away from outward
		public static bool IsEdgeBehind(this IChiselHandles handles, Vector3 from, Vector3 to, Vector3 center, Vector3 outward)
		{
			var camera = Camera.current;
			if (camera == null || outward == Vector3.zero)
				return false;
			outward = outward.normalized;
			Vector3 onPlane;
			if (camera.orthographic)
			{
				var view = handles.inverseMatrix.MultiplyVector(camera.transform.forward);
				if (Vector3.Dot(view, outward) <= 0)
					return false;
				var flat = -view - outward * Vector3.Dot(-view, outward);
				if (flat.sqrMagnitude < 1e-8f)
					return false;
				onPlane = center + flat.normalized * 1e6f;
			} else
			{
				var cameraPosition = handles.inverseMatrix.MultiplyPoint(camera.transform.position);
				var height = Vector3.Dot(cameraPosition - center, outward);
				if (height >= 0)
					return false;
				onPlane = cameraPosition - outward * height;
			}
			var middle = (from + to) * 0.5f;
			var side   = Vector3.Cross(to - from, outward);
			if (Vector3.Dot(side, middle - center) < 0)
				side = -side;
			return Vector3.Dot(onPlane - middle, side) <= 0;
		}

		// The two points on a flat circle around the up axis where the lines from the camera just touch it
		public static bool TryGetHorizonPoints(this IChiselHandles handles, Vector3 center, float radius, out Vector3 left, out Vector3 right)
		{
			left = right = center;
			var camera = Camera.current;
			if (camera == null || radius <= 0)
				return false;
			var toCamera = camera.orthographic ? -handles.inverseMatrix.MultiplyVector(camera.transform.forward)
			                                   : handles.inverseMatrix.MultiplyPoint(camera.transform.position) - center;
			toCamera.y = 0;
			var distance = camera.orthographic ? float.PositiveInfinity : toCamera.magnitude;
			if (toCamera.sqrMagnitude < 1e-8f || distance <= radius)
				return false;
			toCamera.Normalize();
			var side   = Vector3.Cross(Vector3.up, toCamera);
			var toward = float.IsInfinity(distance) ? 0 : radius * radius / distance;
			var across = float.IsInfinity(distance) ? radius : radius * Mathf.Sqrt(1 - (radius * radius) / (distance * distance));
			left  = center + toCamera * toward - side * across;
			right = center + toCamera * toward + side * across;
			return true;
		}

		// A line on the horizon of something round around the up axis, from its bottom to its top, dragged out or in to
		// resize it; returns how much its diameter changes. While it has focus it's lit up, with its radius where the mouse is
		public static float DoHorizonLineHandle(this IChiselHandles handles, Vector3 bottom, Vector3 top, Vector3 axisPoint, string radiusName = "Radius", float innerRadius = 0, string innerName = null)
		{
			var outward = bottom - axisPoint; outward.y = 0;
			if (outward.sqrMagnitude < 1e-8f)
				return 0;
			outward.Normalize();
			var change = 0.0f;
			if (handles.DoEdgeHandle1DOffset(out Vector3 moved, Axis.X, bottom, top, outward, undoMessage: "Change Diameter"))
				change = 2 * Vector3.Dot(moved, outward);
			if (!handles.lastHandleHadFocus)
				return change;

			var prevColor = handles.color;
			handles.color = handles.GetStateColor(prevColor, hasFocus: true, isBackfaced: false);
			handles.DrawLine(bottom, top, lineMode: LineMode.NoZTest, thickness: 3.0f);
			handles.color = prevColor;

			// at the height where the mouse is along the line
			var ray   = handles.mouseRay;
			var along = top - bottom;
			var cross = Vector3.Cross(along, ray.direction);
			var t     = 0.5f;
			if (cross.sqrMagnitude > 1e-8f)
				t = Mathf.Clamp01(Vector3.Dot(Vector3.Cross(ray.origin - bottom, ray.direction), cross) / cross.sqrMagnitude);
			var at     = bottom + along * t;
			var onAxis = new Vector3(axisPoint.x, at.y, axisPoint.z);
			RenderRadius(handles, onAxis, at, Vector3.Distance(onAxis, at), radiusName, innerRadius, innerName);
			return change;
		}

		// A radius; with something inside the circle, what's between the two, like a step's width, on a second line of its label
		static void RenderRadius(IChiselHandles handles, Vector3 center, Vector3 onCircle, float radius, string radiusName, float innerRadius, string innerName)
		{
			if (string.IsNullOrEmpty(innerName) || radius <= innerRadius)
				handles.RenderDistanceMeasurement(center, onCircle, radius, radiusName);
			else
				handles.RenderDistanceMeasurement(center, onCircle, radius, radiusName, radius - innerRadius, innerName);
		}

		// A circle to drag; while it has focus, its radius
		public static void DoDraggableCircle(this IChiselHandles handles, ref float diameter, Vector3 center, IChiselEllipsoidHandle circle, string radiusName = "Radius", Vector3 outward = default, float innerRadius = 0, string innerName = null)
		{
			circle.Center    = center;
			circle.Normal    = Vector3.up;
			circle.Outward   = outward;
			circle.DiameterX = diameter;
			circle.DiameterZ = diameter;
			handles.DoDistanceHandle(ref diameter, center, Vector3.up, circle);
			diameter = math.abs(diameter);
			if (handles.lastHandleHadFocus && handles.TryGetClosestPoint(circle, out var closest, interpolate: false))
				RenderRadius(handles, center, closest, diameter * 0.5f, radiusName, innerRadius, innerName);
		}

        public static void RenderBox(this IChiselHandleRenderer renderer, MinMaxAABB bounds)           { renderer.RenderBox(bounds.ToBounds()); }
        public static void RenderBoxMeasurements(this IChiselHandleRenderer renderer, MinMaxAABB bounds)   { renderer.RenderBoxMeasurements(bounds.ToBounds()); }
        public static void RenderCylinder(this IChiselHandleRenderer renderer, MinMaxAABB bounds, int segments) { renderer.RenderCylinder(bounds.ToBounds(), segments); }
        
        public static void RenderDistanceMeasurement(this IChiselHandleRenderer renderer, float3 from, float3 to) { renderer.RenderDistanceMeasurement((Vector3)from, (Vector3)to); }
        public static void RenderDistanceMeasurement(this IChiselHandleRenderer renderer, float3 from, float3 to, float forceValue) { renderer.RenderDistanceMeasurement((Vector3)from, (Vector3)to, forceValue); }
        
        public static void DrawLine(this IChiselHandleRenderer renderer, float3 from, float3 to, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { renderer.DrawLine((Vector3)from, (Vector3)to, lineMode, thickness, dashSize); }

    }

    public interface IChiselHandle
    {
        Color       Color       { get; set; }
        bool TryGetClosestPoint(out Vector3 closestPoint, bool interpolate = true);
    }
    
    public interface IChiselCircleHandle : IChiselHandle
    {
        Vector3     Center      { get; set; }
        Vector3     Normal      { get; set; }
        float       Diameter    { get; set; }
        float       StartAngle  { get; set; }
        float       TotalAngles { get; set; }
        float       DashSize    { get; set; }

        Vector3 GetPointAtDegree(float degree);
    }
    
    public interface IChiselEllipsoidHandle : IChiselHandle
    {
        Vector3     Center      { get; set; }
        Vector3     Normal      { get; set; }
        float       DiameterX   { get; set; }
        float       DiameterZ   { get; set; }
        float       Rotation    { get; set; }
        float       StartAngle  { get; set; }
        float       TotalAngles { get; set; }
        float       DashSize    { get; set; }
        // the side of the circle that's outside the shape it's an end of; seen from the other side, the part of it behind
        // where its outline turns is fainter. Zero for a circle that isn't an end of something
        Vector3     Outward     { get; set; }

        Vector3 GetPointAtDegree(float degree);
    }

    public interface IChiselLineHandle : IChiselHandle
    {
        Vector3     From        { get; set; }
        Vector3     To          { get; set; }
        float       DashSize    { get; set; }
    }

    public interface IChiselPolygonHandle : IChiselHandle
    {
        Vector3[]   Vertices    { get; set; }
        float       DashSize    { get; set; }
    }

    public interface IChiselLineLoopHandle : IChiselHandle
    {
        Vector3[]   Vertices    { get; set; }
        int         Offset      { get; set; }
        int         Count       { get; set; }
        float       DashSize    { get; set; }
    }

    public interface IChiselNormalHandle : IChiselHandle
    {
        Vector3     Origin      { get; set; }
        Vector3     Normal      { get; set; }
        float       DashSize    { get; set; }
    }


    public interface IChiselHandleAllocation
    {
        IChiselCircleHandle CreateCircleHandle(Vector3 center, Vector3 normal, float diameter, Color color, float startAngle = 0, float angles = 360, float dashSize = 0);
        IChiselCircleHandle CreateCircleHandle(Vector3 center, Vector3 normal, float diameter, float startAngle = 0, float angles = 360, float dashSize = 0);

        IChiselEllipsoidHandle CreateEllipsoidHandle(Vector3 center, Vector3 normal, float diameterX, float diameterZ, Color color, float rotation = 0, float startAngle = 0, float angles = 360, float dashSize = 0);
        IChiselEllipsoidHandle CreateEllipsoidHandle(Vector3 center, Vector3 normal, float diameterX, float diameterZ, float rotation = 0, float startAngle = 0, float angles = 360, float dashSize = 0);

        IChiselLineHandle CreateLineHandle(Vector3 from, Vector3 to, Color color, float dashSize = 0, bool highlightOnly = false);
        IChiselLineHandle CreateLineHandle(Vector3 from, Vector3 to, float dashSize = 0, bool highlightOnly = false);

        IChiselLineLoopHandle CreateLineLoopHandle(Vector3[] vertices, Color color, float dashSize = 0);
        IChiselLineLoopHandle CreateLineLoopHandle(Vector3[] vertices, float dashSize = 0);

        IChiselLineLoopHandle CreateLineLoopHandle(Vector3[] vertices, int offset, int count, Color color, float dashSize = 0);
        IChiselLineLoopHandle CreateLineLoopHandle(Vector3[] vertices, int offset, int count, float dashSize = 0);

        IChiselNormalHandle CreateNormalHandle(Vector3 origin, Vector3 normal, Color color, float dashSize = 0);
        IChiselNormalHandle CreateNormalHandle(Vector3 origin, Vector3 normal, float dashSize = 0);
    }
        
    public interface IChiselHandles : IChiselHandleRenderer, IChiselHandleAllocation
    {
        bool modified { get; }
        bool lastHandleHadFocus { get; }
        bool lastHandleIsDragged { get; }

        Vector3 moveSnappingSteps { get; }

        object generatorState { get; set; }

        bool TryGetClosestPoint(IChiselHandle[] handles, out Vector3 closestPoint, bool interpolate = true);
        bool TryGetClosestPoint(IChiselHandle handle, out Vector3 closestPoint, bool interpolate = true);
        // the ray under the mouse, in the space handles are in
        Ray mouseRay { get; }
        // a measurement is being typed in: measurements aren't hidden then, so it isn't either
        bool isEditingMeasurement { get; }
        void DoRenderHandles(IChiselHandle[] handles);

        bool DoSlider1DHandle(ref Vector3 position, Vector3 direction, IChiselHandle[] handles, float snappingStep = 0, string undoMessage = null);
        bool DoSlider1DHandle(ref Vector3 position, Vector3 direction, IChiselHandle handle, float snappingStep = 0, string undoMessage = null);
        bool DoSlider1DHandle(ref float distance, Vector3 center, Vector3 direction, IChiselHandle[] handles, float snappingStep = 0, string undoMessage = null);
        bool DoSlider1DHandle(ref float distance, Vector3 center, Vector3 direction, IChiselHandle handle, float snappingStep = 0, string undoMessage = null);
        bool DoCircleRotationHandle(ref float angle, Vector3 center, Vector3 normal, IChiselHandle handle, string undoMessage = null);
        bool DoCircleRotationHandle(ref float angle, Vector3 center, Vector3 normal, IChiselHandle[] handles, string undoMessage = null);
        bool DoDistanceHandle(ref float distance, Vector3 center, Vector3 normal, IChiselHandle handle, string undoMessage = null);
        bool DoDistanceHandle(ref float distance, Vector3 center, Vector3 normal, IChiselHandle[] handles, string undoMessage = null);


        bool IsSufaceBackFaced(Vector3 point, Vector3 normal);

        bool DoBoundsHandle(ref Bounds bounds, Vector3? snappingSteps = null, string undoMessage = null, bool fixedBottom = false);
        bool DoShapeHandle(ref Curve2D shape, string undoMessage = null);
        bool DoShapeHandle(ref Curve2D shape, Matrix4x4 transformation, string undoMessage = null);
        bool DoRadiusHandle(ref float outerRadius, Vector3 direction, Vector3 position, bool renderDisc = true, string undoMessage = null);
        bool DoRadius2DHandle(ref Vector3 radius1, ref Vector3 radius2, Vector3 center, Vector3 up, float minRadius1 = 0, float minRadius2 = 0, float maxRadius1 = float.PositiveInfinity, float maxRadius2 = float.PositiveInfinity, bool renderDisc = true, string undoMessage = null);
        bool DoRadius2DHandle(ref Vector3 radius, Vector3 center, Vector3 up, float minRadius = 0, float maxRadius = float.PositiveInfinity, bool renderDisc = true, string undoMessage = null);
        bool DoDirectionHandle(ref Vector3 position, Vector3 direction, float snappingStep = 0, string undoMessage = null);
        bool DoRotatableLineHandle(ref float angle, Vector3 origin, float diameter, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, string undoMessage = null, float lineStart = 0);
        // an angle around an axis, at zero along zeroDirection: a knob to drag around a ring just outside the radius,
        // with the angle, and its name, beside the knob while it's hovered or dragged
        bool DoAngleRingHandle(ref float angle, Vector3 center, Vector3 axis, Vector3 zeroDirection, float radius, string undoMessage = null, string name = null);
        bool DoTurnHandle(ref Bounds bounds, string undoMessage = null);
        // the bounds' lengths, each of which can be clicked and typed in
        bool DoBoxMeasurements(ref Bounds bounds, string undoMessage = null);
        // a length between two points, which can be clicked and typed in; a direction keeps it off them, the same on screen
        bool DoDistanceMeasurement(ref float distance, Vector3 from, Vector3 to, string undoMessage = null, Vector3 offsetDirection = default, string name = null);

        bool DoEdgeHandle1D(out float offset, Axis axis, Vector3 from, Vector3 to, float snappingStep = 0, float handleSize = 0, string undoMessage = null, float thickness = 3.0f);
        bool DoEdgeHandle1DOffset(out Vector3 offset, Axis axis, Vector3 from, Vector3 to, Vector3 direction, float snappingStep = 0, float handleSize = 0, bool renderLine = true, string undoMessage = null, float thickness = 3.0f);
        bool DoEdgeHandle1DOffset(out float offset, Axis axis, Vector3 from, Vector3 to, float snappingStep = 0, float handleSize = 0, bool renderLine = true, string undoMessage = null);


        // shown at a point of the generator, it moves the whole of it, as the generator's transform
        bool DoMoveGizmo(Vector3 point);
        bool DoPathPointHandle(ref ChiselPathPoint pathPoint, string undoMessage = null);
        bool DoPositionHandle(ref Vector3 position, Quaternion rotation, string undoMessage = null);
        bool DoRotationHandle(ref Quaternion rotation, Vector3 position, string undoMessage = null);
        bool DoPlanarScaleHandle(ref Vector2 scale2D, Vector3 position, Quaternion rotation, string undoMessage = null);
    }

    public enum MessageDestination
    {
        Hierarchy,
        Inspector
    }

    public interface IChiselMessageHandler
    {
		MessageDestination Destination { get; }
		void SetTitle(string name, UnityEngine.Object reference);
        void Warning(string message, Action buttonAction, string buttonText);
        void Warning(string message);
	}

    public interface IChiselMessageProvider
    {
		void GetMessages(IChiselMessageHandler messageHandler);
	}

    public static class ChiselMessages
	{
		public static void ShowMessages(UnityEngine.Object[] targets, IChiselMessageHandler messageHandler)
		{
			if (targets.Length == 1)
			{
                var target = targets[0];
				if (target is not IChiselMessageProvider generator)
					return;
				if (generator is MonoBehaviour component && !component.isActiveAndEnabled)
					return;
				generator?.GetMessages(messageHandler);
				return;
			}
			foreach (var target in targets)
			{
				if (target is not IChiselMessageProvider generator)
					continue;
                if (target is MonoBehaviour component && !component.isActiveAndEnabled)
                    continue;
				messageHandler.SetTitle(target.name, target);
				generator.GetMessages(messageHandler);
			}
		}

		public static void ShowMessages(List<UnityEngine.Object> targets, IChiselMessageHandler messageHandler)
		{
			if (targets.Count == 1)
			{
				var target = targets[0];
				if (target is not IChiselMessageProvider generator)
					return;
				if (generator is MonoBehaviour component && !component.isActiveAndEnabled)
					return;
				generator.GetMessages(messageHandler);
				return;
			}
			foreach (var target in targets)
			{
				if (target is not IChiselMessageProvider generator)
					continue;
				if (generator is MonoBehaviour component && !component.isActiveAndEnabled)
					continue;
				messageHandler.SetTitle(target.name, target);
				generator.GetMessages(messageHandler);
			}
		}
	}
}
