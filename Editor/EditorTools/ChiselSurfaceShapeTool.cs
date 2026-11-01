using System.Collections.Generic;
using Chisel.Components;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Chisel.Editors
{
    /// <summary>
    /// Draws a shape on the surface under the mouse, snapped to its grid: click its corners, and click the first one again
    /// to close it. The shape becomes a polygon decal of the surface's brush, limited to that surface, with the surface's
    /// material and texture coordinates until it is given its own. Escape drops the shape being drawn.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    sealed class ChiselSurfaceShapeTool : EditorTool
    {
        static GUIContent s_ToolbarIcon;
        public override GUIContent toolbarIcon => s_ToolbarIcon ??= new GUIContent(ChiselEditorResources.LoadIconImages("free_draw")?[0],
                                                                                   "Chisel Surface Shape Tool: draw a shape on a surface, a region of it with its own material");

        struct Hit
        {
            public bool                valid;
            public ChiselNodeComponent brush;
            public int                 surfaceIndex;
            public Vector3             normal;
            public Material            material;
        }

        // in the drawing grid's space (PointDrawing), the last one following the mouse
        static List<Vector3> s_Points = new();
        // the surface the first corner is on
        static Hit s_Surface;
        static readonly List<ChiselIntersection> s_Intersections = new();

        public override void OnActivated()          { ClearDrawing(); }
        public override void OnWillBeDeactivated()  { ClearDrawing(); }

        static void ClearDrawing()
        {
            s_Points.Clear();
            s_Surface = default;
            PointDrawing.Reset();
        }

        static Hit Pick(Vector2 mousePosition)
        {
            ChiselSelectionManager.GetOverlappingObjects(mousePosition, s_Intersections);
            foreach (var intersection in s_Intersections)
            {
                if (intersection.treeNode is not ChiselGeneratorComponent generator || intersection.model == null ||
                    !intersection.brushIntersection.brush.Valid)
                    continue;
                var surfaceIndex = ChiselTrimDecals.SurfaceOf(intersection.brushIntersection.brush, intersection.brushIntersection.surfaceIndex);
                var surfaces     = generator.SurfaceDefinition?.surfaces;
                if (surfaceIndex < 0 || surfaces == null || surfaceIndex >= surfaces.Length)
                    continue;
                return new Hit
                {
                    valid        = true,
                    brush        = generator,
                    surfaceIndex = surfaceIndex,
                    normal       = intersection.worldPlane.normal,
                    material     = surfaces[surfaceIndex].RenderMaterial
                };
            }
            return default;
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView sceneView)
                return;
            var dragArea = sceneView.position;
            dragArea.position = Vector2.zero;

            var evt = Event.current;
            if (s_Points.Count <= 1 && (evt.type == EventType.MouseMove || evt.type == EventType.MouseDown))
                s_Surface = Pick(evt.mousePosition);

            PointDrawing.PointDrawHandle(dragArea, ref s_Points, out var transformation, out _, releaseOnMouseUp: true, SceneHandles.OutlinedDotHandleCap);
            SnapToSurfaceOutline(transformation);

            if (evt.type == EventType.Repaint && s_Points.Count > 1)
            {
                var line = new Vector3[s_Points.Count];
                for (int i = 0; i < s_Points.Count; i++)
                    line[i] = transformation.MultiplyPoint3x4(s_Points[i]);
                Handles.color = Handles.selectedColor;
                Handles.DrawAAPolyLine(3, line);
            }

            // closed: the corner before the one following the mouse is on the first
            var count = s_Points.Count;
            if (count > 4 && (s_Points[count - 2] - s_Points[0]).sqrMagnitude < PointDrawing.kDistanceEpsilon)
            {
                var outline = new List<Vector3>(count - 2);
                for (int i = 0; i < count - 2; i++)
                    outline.Add(transformation.MultiplyPoint3x4(s_Points[i]));
                var surface = s_Surface;
                ClearDrawing();
                if (surface.valid)
                    Create(surface.brush, surface.surfaceIndex, surface.material, surface.normal, outline, transformation.GetColumn(0));
                GUIUtility.ExitGUI();
            }
        }

        static readonly List<Vector3> s_SurfaceOutline = new();
        // How near (in pixels) the surface's corners and edges pull the corner being placed onto them
        const float kSnapPixels = 10;

        // The corner following the mouse onto the surface's corner or edge it is near: a corner on an edge stays on it
        static void SnapToSurfaceOutline(Matrix4x4 transformation)
        {
            if (s_Points.Count == 0 || !s_Surface.valid ||
                !ChiselSurfaceShapes.SurfaceOutline(s_Surface.brush, s_Surface.surfaceIndex, s_SurfaceOutline, out _))
                return;
            var last  = s_Points.Count - 1;
            var world = transformation.MultiplyPoint3x4(s_Points[last]);
            var reach = HandleUtility.GetHandleSize(world) * kSnapPixels / 80.0f;
            if (!ChiselSurfaceShapes.NearestOnOutline(s_SurfaceOutline, world, reach, reach * 1.5f, out var snapped))
                return;
            s_Points[last] = transformation.inverse.MultiplyPoint3x4(snapped);
        }

        // right: the direction on the surface the decal's X takes, its drawing grid's
        internal static ChiselDecalComponent Create(ChiselNodeComponent brush, int surfaceIndex, Material material, Vector3 normal, List<Vector3> outline,
                                                    Vector3 right = default)
        {
            var decal = ChiselDecalComponent.CreateOnSurface(brush, surfaceIndex, material, normal, outline, right);
            if (decal == null)
                return null;
            Undo.RegisterCreatedObjectUndo(decal.gameObject, "Draw Surface Shape");
            return decal;
        }
    }
}
