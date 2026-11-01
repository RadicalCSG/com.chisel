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

        public override void OnActivated()          { Reset(); }
        public override void OnWillBeDeactivated()  { Reset(); }

        static void Reset()
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
                Reset();
                if (surface.valid)
                    Create(surface, outline);
                GUIUtility.ExitGUI();
            }
        }

        static void Create(Hit surface, List<Vector3> outline)
        {
            var decal = ChiselDecalComponent.CreateOnSurface(surface.brush, surface.surfaceIndex, surface.material, surface.normal, outline);
            if (decal == null)
                return;
            Undo.RegisterCreatedObjectUndo(decal.gameObject, "Draw Surface Shape");
        }
    }
}
