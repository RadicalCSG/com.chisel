using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Chisel.Editors
{
    // Cuts the selected brushes with a plane. The plane is placed by drawing a line on a surface: it goes through the
    // line, at right angles to the surface (in an orthographic view, along the view). Its three points can then be
    // dragged. The brushes are shown cut as soon as the line has length, made again whenever the plane or the mode
    // changes; Tab goes through what is kept: what is behind the plane, what is in front of it, or both, as two
    // brushes. Enter commits the cut, Escape puts the brushes back as they were.
    // The shortcut context is registered once and stays registered, as Unity keeps it through entering play mode.
    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    sealed class ChiselClipTool : ChiselEditToolBase
    {
        const string kToolName = "Clip";
        public override string ToolName => kToolName;

        public static bool IsActive() { return ToolManager.activeToolType == typeof(ChiselClipTool); }

        public override SnapSettings ToolUsedSnappingModes { get { return SnapSettings.AllGeometry; } }

        #region Keyboard Shortcuts
        const string kEditModeShotcutName = kToolName + " Mode";
        [Shortcut(ChiselKeyboardDefaults.ShortCutEditModeBase + kEditModeShotcutName, ChiselKeyboardDefaults.SwitchToClipEditMode, displayName = kEditModeShotcutName)]
        public static void ActivateTool() { ToolManager.SetActiveTool<ChiselClipTool>(); }

        internal const string kNextModeId = "Chisel/Clip/Next Mode";

        // the clip tool's own keys are only in use while it is, in the Scene view
        sealed class Context : IShortcutContext
        {
            public bool active => IsInUse(EditorWindow.focusedWindow is SceneView, IsActive());
        }

        static readonly Context s_Context = new();

        internal static bool IsInUse(bool inSceneView, bool clipToolActive) => inSceneView && clipToolActive;

        // the shortcut manager isn't there yet while the editor loads
        static ChiselClipTool() { EditorApplication.delayCall += () => ShortcutManager.RegisterContext(s_Context); }

        [Shortcut(kNextModeId, typeof(Context), ChiselKeyboardDefaults.CycleClipModeKey)]
        static void CycleMode() { SetMode(NextMode(ChiselEditorSettings.ClipMode)); }
        #endregion

        #region Modes
        internal static ChiselClipMode NextMode(ChiselClipMode mode)
        {
            return mode switch
            {
                ChiselClipMode.KeepBehind  => ChiselClipMode.KeepInFront,
                ChiselClipMode.KeepInFront => ChiselClipMode.Split,
                _                          => ChiselClipMode.KeepBehind
            };
        }

        internal static void SetMode(ChiselClipMode mode)
        {
            if (ChiselEditorSettings.ClipMode == mode)
                return;
            ChiselEditorSettings.ClipMode = mode;
            ChiselEditorSettings.Save();
            if (s_Active)
                s_Active.ShowCut();
            SceneView.RepaintAll();
        }
        #endregion

        #region Plane
        // the line drawn on the surface, and a point into the surface below its start: the plane goes through all three
        readonly Vector3[] points = new Vector3[3];
        // 0: nothing placed yet, 1: the line is being drawn, 2: the plane is placed
        int     placed;
        Plane   surface;
        Plane   clipPlane;
        // while the line is drawn, whether it has a plane yet: it has none while it has no length
        bool    lineHasPlane;

        internal bool HasPlane => placed == 2;
        // the plane is shown, and the brushes cut by it, from as soon as the line being drawn has one
        bool ShowsPlane => placed == 2 || (placed == 1 && lineHasPlane);

        /// <summary>The plane through the line from start to end, at right angles to the surface with that normal: the
        /// line, and a point depth into the surface below its start. False when the line has no length, or runs along
        /// the normal.</summary>
        internal static bool TryGetPlane(Vector3 start, Vector3 end, Vector3 surfaceNormal, float depth, out Plane plane)
        {
            plane = default;
            var along = end - start;
            if (along.sqrMagnitude < kMinLineLength * kMinLineLength ||
                Vector3.Cross(along.normalized, surfaceNormal.normalized).sqrMagnitude < 1e-6f)
                return false;
            plane = new Plane(start, end, start - surfaceNormal.normalized * depth);
            return true;
        }

        static bool TryGetPlane(Vector3 a, Vector3 b, Vector3 c, out Plane plane)
        {
            plane = new Plane(a, b, c);
            return Vector3.Cross(b - a, c - a).sqrMagnitude > 1e-10f;
        }

        /// <summary>The plane's third point, besides the line: of the corners of the cut the plane makes through the
        /// brushes, those below the surface the line is on (so the points make the plane facing the same way), the one
        /// seen from the camera furthest from the line's start; viewRay gives the ray from the camera through a point.
        /// Without one, depth below the line's start, into the surface.</summary>
        internal static Vector3 ThirdPoint(Vector3 start, Vector3 surfaceNormal, Plane plane, IReadOnlyList<ChiselClipPicking.Brush> brushes,
                                           System.Func<Vector3, Ray> viewRay)
        {
            var normal   = surfaceNormal.normalized;
            var best     = start - normal * kDepth;
            var furthest = 0f;
            for (int b = 0; b < brushes.Count; b++)
            {
                var corners = CutFace(brushes[b], plane);
                if (corners == null)
                    continue;
                foreach (var local in corners)
                {
                    var corner = brushes[b].toWorld.MultiplyPoint(local);
                    if (Vector3.Dot(corner - start, normal) > -kMinLineLength)
                        continue;
                    var distance = (corner - start).sqrMagnitude;
                    if (distance <= furthest || ChiselClipPicking.IsHidden(corner, viewRay(corner), brushes))
                        continue;
                    furthest = distance;
                    best     = corner;
                }
            }
            return best;
        }

        /// <summary>The corners of the face the plane, in the world, cuts through the brush, in the brush's space;
        /// null when it passes by.</summary>
        internal static Vector3[] CutFace(ChiselClipPicking.Brush brush, Plane worldPlane)
        {
            const int kCut = -1;
            var local = brush.toWorld.inverse.TransformPlane(worldPlane);
            var plane = new Unity.Mathematics.float4(local.normal, local.distance);
            if (brush.mesh.SideOf(plane) != BrushMesh.PlaneSide.Both)
                return null;
            var part = new BrushMesh(brush.mesh);
            if (!part.Clip(plane, kCut))
                return null;
            for (int p = 0; p < part.polygons.Length; p++)
            {
                var polygon = part.polygons[p];
                if (polygon.descriptionIndex != kCut)
                    continue;
                var corners = new Vector3[polygon.edgeCount];
                for (int e = 0; e < polygon.edgeCount; e++)
                    corners[e] = part.vertices[part.halfEdges[polygon.firstEdge + e].vertexIndex];
                return corners;
            }
            return null;
        }

        const float kMinLineLength = 0.001f;
        // how far below the line its third point is put, at first
        const float kDepth = 1;

        // the brushes go back to how they were, unless the cut was committed
        void LetGoOfPlane()
        {
            cut.Cancel();
            placed       = 0;
            lineHasPlane = false;
            previewDirty = true;
            if (GUIUtility.hotControl == s_PlaneID)
                GUIUtility.hotControl = 0;
            SceneView.RepaintAll();
        }

        void StartLine(Vector3 point, ChiselBrushComponent[] targets)
        {
            points[0] = points[1] = point;
            placed       = 1;
            lineHasPlane = false;
            cut.Begin(targets);
        }

        // the straight cut through the line drawn so far, and the brushes cut by it; while the line has no plane, the
        // brushes as they were
        void UpdateLinePlane()
        {
            lineHasPlane = TryGetPlane(points[0], points[1], surface.normal, kDepth, out clipPlane);
            if (!lineHasPlane)
            {
                cut.Hide();
                previewDirty = true;
                return;
            }
            ShowCut();
        }

        // the line is done: it can be committed as it is, or the plane turned by its points, the third one put now;
        // let go of when the line has no plane
        void PlacePlane()
        {
            UpdateLinePlane();
            if (!lineHasPlane)
            {
                LetGoOfPlane();
                return;
            }
            points[2] = ThirdPoint(points[0], surface.normal, clipPlane, cut.Originals, s_ViewRay);
            placed    = 2;
        }
        #endregion

        #region Targets
        // the selected brushes, those under selected objects included
        static ChiselBrushComponent[] Targets()
        {
            return Selection.GetFiltered<ChiselBrushComponent>(SelectionMode.Deep | SelectionMode.Editable);
        }

        // the brushes shown cut, as the plane and the mode are now
        readonly ChiselClipPreview cut = new();

        void ShowCut()
        {
            if (!ShowsPlane)
                return;
            cut.Show(clipPlane, ChiselEditorSettings.ClipMode);
            previewDirty = true;
        }

        void Commit()
        {
            if (!HasPlane || cut.Originals.Count == 0)
                return;
            var left = cut.Commit();

            var selection = new List<Object>(left.Count);
            foreach (var brush in left)
                selection.Add(brush.gameObject);
            Selection.objects = selection.ToArray();
            LetGoOfPlane();
        }

        // the overlay's buttons
        internal static void CommitActive() { if (s_Active) s_Active.Commit(); }
        internal static void CancelActive() { if (s_Active) s_Active.LetGoOfPlane(); }
        internal static bool ActiveHasPlane => s_Active && s_Active.HasPlane;
        #endregion

        static ChiselClipTool s_Active;

        public override void OnActivate()
        {
            base.OnActivate();
            s_Active = this;
            ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
            LetGoOfPlane();
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            Undo.undoRedoPerformed -= OnUndoRedo;
            Selection.selectionChanged -= OnSelectionChanged;
            if (s_Active == this)
                s_Active = null;
            LetGoOfPlane();
        }

        // an undo or redo from elsewhere while the cut is shown takes it away, or makes it a step of its own: the tool
        // lets go of it, without undoing anything more
        void OnUndoRedo()
        {
            if (cut.IsReverting)
                return;
            if (cut.IsShown)
            {
                cut.Forget();
                LetGoOfPlane();
            }
            previewDirty = true;
        }

        // the cut stays on the brushes it was begun on: showing it changes the selection, as the brushes it removes go
        void OnSelectionChanged() { previewDirty = true; }

        static readonly int kPlaneHash = "ChiselClipPlane".GetHashCode();
        static int s_PlaneID;

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var evt = Event.current;
            s_PlaneID = GUIUtility.GetControlID(kPlaneHash, FocusType.Keyboard);

            switch (evt.type)
            {
                case EventType.KeyDown:
                {
                    if ((evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && HasPlane)
                    {
                        Commit();
                        evt.Use();
                    } else
                    if (evt.keyCode == ChiselKeyboardDefaults.kCancelKey && (placed > 0 || GUIUtility.hotControl == 0))
                        evt.Use();
                    break;
                }
                case EventType.KeyUp:
                {
                    if (evt.keyCode != ChiselKeyboardDefaults.kCancelKey)
                        break;
                    evt.Use();
                    if (placed > 0)
                    {
                        LetGoOfPlane();
                        break;
                    }
                    if (GUIUtility.hotControl == 0)
                    {
                        Selection.activeTransform = null;
                        GUIUtility.ExitGUI(); // avoids a nullreference exception in sceneview
                    }
                    break;
                }
            }

            // drawn first, so the points are drawn over it
            if (evt.type == EventType.Repaint)
                DrawPlaneAndPreview();

            if (HasPlane)
                DoPointHandles();
            else
                DoDrawLine(sceneView, evt);
        }

        #region Drawing the line
        // how near the mouse, in points, a vertex or an edge is picked
        const float kSnapDistance = 8;

        // the brushes points are picked on: those cut, else the one under the mouse; the brush each one is
        static readonly List<ChiselClipPicking.Brush> s_PickBrushes = new();
        static readonly List<ChiselBrushComponent>    s_PickOwners  = new();

        static readonly System.Func<Vector3, Vector2> s_ToGUI = position => HandleUtility.WorldToGUIPoint(position);
        // the ray from the scene view's camera through a point
        static readonly System.Func<Vector3, Ray> s_ViewRay = position => HandleUtility.GUIPointToWorldRay(HandleUtility.WorldToGUIPoint(position));

        static void GatherPickBrushes(ChiselBrushComponent[] brushes)
        {
            s_PickBrushes.Clear();
            s_PickOwners.Clear();
            foreach (var brush in brushes)
            {
                var outline = brush.definition?.BrushOutline;
                if (outline?.vertices == null || outline.planes == null || outline.halfEdgePolygonIndices == null)
                    continue;
                s_PickBrushes.Add(new ChiselClipPicking.Brush(brush.GlobalTransformation, outline));
                s_PickOwners.Add(brush);
            }
        }

        /// <summary>The point a line starts at: on the brushes (a vertex, an edge, or a face, see ChiselClipPicking), else
        /// on the surface underMouse gives, only asked for then. On a face it is snapped to the grid, kept on the face; on
        /// an edge, snapped along it. brush is the index of the brush it is on, -1 when it isn't on one of them.</summary>
        internal static bool TryGetPoint(Ray ray, IReadOnlyList<ChiselClipPicking.Brush> brushes, System.Func<Vector3, Vector2> toGUI, Vector2 mousePosition,
                                         System.Func<PlaneIntersection> underMouse, out Vector3 point, out Plane surface, out int brush)
        {
            brush = -1;
            if (ChiselClipPicking.TryPick(ray, brushes, toGUI, mousePosition, kSnapDistance, out var pick))
            {
                brush   = pick.brush;
                surface = new Plane(pick.normal, pick.point);
                point   = Snapped(pick, surface);
                return true;
            }
            var hit = underMouse();
            if (hit == null)
            {
                point   = default;
                surface = default;
                return false;
            }
            surface = hit.plane;
            point   = SnapOnSurface(hit.point, surface);
            return true;
        }

        // The point under the mouse, on the brushes cut, else on what is nearest under the mouse as the scene view shows
        // it (a brush, a generator, a mesh), else on the grid. With nothing selected, the brush under the mouse is the one
        // cut, so it is picked on as well.
        static bool TryGetPointUnderMouse(Camera camera, Vector2 mousePosition, out Vector3 point, out Plane surface, out ChiselBrushComponent brush)
        {
            var targets = Targets();
            PlaneIntersection underMouse = null;
            if (targets.Length == 0)
            {
                underMouse = ChiselClickSelectionManager.GetPlaneIntersection(mousePosition);
                if (underMouse?.node is ChiselBrushComponent brushUnderMouse)
                    targets = new[] { brushUnderMouse };
            }
            GatherPickBrushes(targets);

            var ray   = HandleUtility.GUIPointToWorldRay(mousePosition);
            var found = TryGetPoint(ray, s_PickBrushes, s_ToGUI, mousePosition,
                                    () => underMouse ??= ChiselClickSelectionManager.GetPlaneIntersection(mousePosition),
                                    out point, out surface, out var picked);
            brush = picked >= 0 ? s_PickOwners[picked] : underMouse?.node as ChiselBrushComponent;

            // in an orthographic view the plane goes along the view
            if (found && camera.orthographic)
                surface = new Plane(-camera.transform.forward, point);
            return found;
        }

        static Vector3 Snapped(ChiselClipPicking.Pick pick, Plane surface)
        {
            return pick.kind switch
            {
                ChiselClipPicking.Kind.Vertex => pick.point,
                ChiselClipPicking.Kind.Edge   => SnapOnEdge(pick.point, pick.edgeStart, pick.edgeEnd),
                _                             => SnapOnSurface(pick.point, surface)
            };
        }

        static Vector3 SnapOnSurface(Vector3 point, Plane surface)
        {
            if (!Snapping.GridSnappingActive)
                return point;
            var snapped = Snapping.SnapPoint(point);
            return snapped - surface.normal * surface.GetDistanceToPoint(snapped);
        }

        // the grid snapped point's nearest on the edge
        static Vector3 SnapOnEdge(Vector3 point, Vector3 start, Vector3 end)
        {
            if (!Snapping.GridSnappingActive)
                return point;
            var snapped = Snapping.SnapPoint(point);
            var along   = end - start;
            var length  = along.sqrMagnitude;
            if (length <= 0)
                return start;
            return start + along * Mathf.Clamp01(Vector3.Dot(snapped - start, along) / length);
        }

        void DoDrawLine(SceneView sceneView, Event evt)
        {
            var camera = sceneView.camera;
            switch (evt.GetTypeForControl(s_PlaneID))
            {
                case EventType.Layout:
                {
                    if (!SceneHandles.InCameraOrbitMode)
                        HandleUtility.AddDefaultControl(s_PlaneID);
                    break;
                }
                case EventType.MouseMove:
                {
                    if (placed == 1)
                        UpdateLineEnd(camera, evt.mousePosition);
                    SceneView.RepaintAll();
                    break;
                }
                case EventType.MouseDown:
                {
                    if (evt.button != 0 || evt.alt || HandleUtility.nearestControl != s_PlaneID || GUIUtility.hotControl != 0)
                        break;
                    if (placed == 1)
                    {
                        // the line's end, clicked rather than dragged to
                        UpdateLineEnd(camera, evt.mousePosition);
                        PlacePlane();
                        evt.Use();
                        break;
                    }
                    if (!TryGetPointUnderMouse(camera, evt.mousePosition, out var point, out surface, out var brush))
                        break;
                    // with nothing selected, the brush clicked on is cut
                    var targets = Targets();
                    if (targets.Length == 0 && brush)
                    {
                        Selection.activeGameObject = brush.gameObject;
                        targets = new[] { brush };
                    }
                    StartLine(point, targets);
                    GUIUtility.hotControl = s_PlaneID;
                    evt.Use();
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (GUIUtility.hotControl != s_PlaneID)
                        break;
                    UpdateLineEnd(camera, evt.mousePosition);
                    evt.Use();
                    break;
                }
                case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl != s_PlaneID || evt.button != 0)
                        break;
                    GUIUtility.hotControl = 0;
                    evt.Use();
                    // dragged out: the line is done; clicked: its end is the next click
                    if ((points[1] - points[0]).sqrMagnitude >= kMinLineLength * kMinLineLength)
                        PlacePlane();
                    break;
                }
            }
        }

        // a vertex or an edge near the mouse, else on the surface the line was started on; picked on the brushes as they
        // were when the line was started, not as they are shown cut; the plane and the cut follow the line
        void UpdateLineEnd(Camera camera, Vector2 mousePosition)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Vector3 end;
            if (ChiselClipPicking.TryPick(ray, cut.Originals, s_ToGUI, mousePosition, kSnapDistance, out var pick) &&
                pick.kind != ChiselClipPicking.Kind.Face)
                end = Snapped(pick, surface);
            else if (surface.Raycast(ray, out var distance))
                end = SnapOnSurface(ray.GetPoint(distance), surface);
            else
                return;
            if (end.Equals(points[1]))
                return;
            points[1] = end;
            UpdateLinePlane();
            SceneView.RepaintAll();
        }
        #endregion

        #region Editing the plane
        static readonly int kPointHash = "ChiselClipPoint".GetHashCode();

        /// <summary>Moves one of the plane's points to where the mouse is, found as a line's start is (see TryGetPoint):
        /// over the brushes, else over the surface under the mouse; plane goes through the points then. False, and the
        /// point kept, when nothing is under the mouse or the points would no longer make a plane.</summary>
        internal static bool TryDragPoint(Vector3[] points, int index, Ray ray, IReadOnlyList<ChiselClipPicking.Brush> brushes,
                                          System.Func<Vector3, Vector2> toGUI, Vector2 mousePosition, System.Func<PlaneIntersection> underMouse,
                                          out Plane plane)
        {
            plane = default;
            if (!TryGetPoint(ray, brushes, toGUI, mousePosition, underMouse, out var moved, out _, out _))
                return false;
            var previous = points[index];
            points[index] = moved;
            if (TryGetPlane(points[0], points[1], points[2], out plane))
                return true;
            points[index] = previous;
            return false;
        }

        void DoPointHandles()
        {
            var evt = Event.current;
            for (int i = 0; i < points.Length; i++)
            {
                var id   = GUIUtility.GetControlID(kPointHash, FocusType.Passive);
                var size = HandleUtility.GetHandleSize(points[i]) * HandleRendering.kPointScale;
                switch (evt.GetTypeForControl(id))
                {
                    case EventType.Layout:
                    {
                        Handles.DotHandleCap(id, points[i], Quaternion.identity, size, EventType.Layout);
                        break;
                    }
                    case EventType.MouseMove:
                    {
                        if (HandleUtility.nearestControl == id)
                            HandleUtility.Repaint();
                        break;
                    }
                    case EventType.MouseDown:
                    {
                        if (evt.button != 0 || evt.alt || HandleUtility.nearestControl != id || GUIUtility.hotControl != 0)
                            break;
                        GUIUtility.hotControl = id;
                        evt.Use();
                        break;
                    }
                    case EventType.MouseDrag:
                    {
                        if (GUIUtility.hotControl != id)
                            break;
                        // picked on the brushes as they were, not as they are shown cut
                        var mousePosition = evt.mousePosition;
                        if (TryDragPoint(points, i, HandleUtility.GUIPointToWorldRay(mousePosition), cut.Originals, s_ToGUI, mousePosition,
                                         () => ChiselClickSelectionManager.GetPlaneIntersection(mousePosition), out var plane) &&
                            !(plane.normal.Equals(clipPlane.normal) && plane.distance == clipPlane.distance))
                        {
                            clipPlane = plane;
                            ShowCut();
                            SceneView.RepaintAll();
                        }
                        evt.Use();
                        break;
                    }
                    case EventType.MouseUp:
                    {
                        if (GUIUtility.hotControl != id || evt.button != 0)
                            break;
                        GUIUtility.hotControl = 0;
                        evt.Use();
                        break;
                    }
                    case EventType.Repaint:
                    {
                        var hovered = HandleUtility.nearestControl == id && GUIUtility.hotControl == 0;
                        var color   = hovered ? Handles.preselectionColor : Handles.selectedColor;
                        using (new Handles.DrawingScope(color))
                            Handles.DotHandleCap(id, points[i], Quaternion.identity, size, EventType.Repaint);
                        break;
                    }
                }
            }
        }
        #endregion

        #region Preview
        // the plane, filled where it cuts through the brushes as they were, and what the cut removes, drawn faintly where
        // it was: the brushes themselves are shown as the cut leaves them
        bool previewDirty = true;
        readonly List<(Matrix4x4 toWorld, Vector3[] edges)>   removed  = new();
        readonly List<(Matrix4x4 toWorld, Vector3[] corners)> cutFaces = new();

        void UpdatePreview()
        {
            previewDirty = false;
            removed.Clear();
            cutFaces.Clear();
            if (!ShowsPlane)
                return;
            var mode = ChiselEditorSettings.ClipMode;
            foreach (var original in cut.Originals)
            {
                var corners = CutFace(original, clipPlane);
                if (corners != null)
                    cutFaces.Add((original.toWorld, corners));
                if (mode == ChiselClipMode.Split)
                    continue;
                var local = original.toWorld.inverse.TransformPlane(clipPlane);
                var plane = new Unity.Mathematics.float4(local.normal, local.distance);
                // the part behind this plane is removed
                AddRemoved(original, mode == ChiselClipMode.KeepBehind ? -plane : plane);
            }
        }

        void AddRemoved(ChiselClipPicking.Brush original, Unity.Mathematics.float4 plane)
        {
            var side = original.mesh.SideOf(plane);
            if (side == BrushMesh.PlaneSide.InFront)
                return;
            var part = original.mesh;
            if (side == BrushMesh.PlaneSide.Both)
            {
                part = new BrushMesh(part);
                part.Clip(plane, -1);
            }
            removed.Add((original.toWorld, Edges(part)));
        }

        // each edge once, as line segments
        static Vector3[] Edges(BrushMesh mesh)
        {
            var halfEdges = mesh.halfEdges;
            var lines     = new List<Vector3>(halfEdges.Length);
            for (int e = 0; e < halfEdges.Length; e++)
            {
                var twin = halfEdges[e].twinIndex;
                if (twin < e)
                    continue;
                lines.Add(mesh.vertices[halfEdges[e].vertexIndex]);
                lines.Add(mesh.vertices[halfEdges[twin].vertexIndex]);
            }
            return lines.ToArray();
        }

        void DrawPlaneAndPreview()
        {
            if (placed == 0)
                return;

            using (new Handles.DrawingScope(Handles.selectedColor))
            {
                Handles.DrawAAPolyLine(3, points[0], points[1]);
                // the plane's points are drawn as its handles
                if (!HasPlane)
                {
                    for (int i = 0; i < 2; i++)
                        Handles.DotHandleCap(0, points[i], Quaternion.identity, HandleUtility.GetHandleSize(points[i]) * HandleRendering.kPointScale, EventType.Repaint);
                }
            }
            if (!ShowsPlane)
                return;

            if (previewDirty)
                UpdatePreview();

            var color = Handles.selectedColor;
            var faint = new Color(color.r, color.g, color.b, color.a * 0.25f);
            foreach (var (toWorld, edges) in removed)
            {
                using (new Handles.DrawingScope(faint, toWorld))
                    Handles.DrawLines(edges);
            }
            var fill = new Color(color.r, color.g, color.b, 0.2f);
            foreach (var (toWorld, corners) in cutFaces)
            {
                using (new Handles.DrawingScope(fill, toWorld))
                    Handles.DrawAAConvexPolygon(corners);
            }
        }
        #endregion
    }

    /// <summary>The clip tool's modes and buttons, while it is the active tool</summary>
    [UnityEditor.Overlays.Overlay(typeof(SceneView), kOverlayTitle)]
    sealed class ChiselClipOverlay : UnityEditor.Overlays.IMGUIOverlay, UnityEditor.Overlays.ITransientOverlay
    {
        const string kOverlayTitle = "Chisel Clip";

        public bool visible => ChiselClipTool.IsActive();

        static readonly GUIContent[] kModes =
        {
            new("Keep Behind",   "Show the brushes keeping what is behind the plane (Tab goes to the next)"),
            new("Keep In Front", "Show the brushes keeping what is in front of the plane (Tab goes to the next)"),
            new("Split",         "Show each brush split into two (Tab goes to the next)")
        };
        static readonly GUIContent kCommit = new("Commit", "Keep the cut as it is shown (Enter)");
        static readonly GUIContent kCancel = new("Cancel", "Put the brushes back as they were (Escape)");

        public override void OnGUI()
        {
            var mode = GUILayout.Toolbar((int)ChiselEditorSettings.ClipMode, kModes);
            if (mode != (int)ChiselEditorSettings.ClipMode)
                ChiselClipTool.SetMode((ChiselClipMode)mode);
            using (new EditorGUI.DisabledScope(!ChiselClipTool.ActiveHasPlane))
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(kCommit))
                    ChiselClipTool.CommitActive();
                if (GUILayout.Button(kCancel))
                    ChiselClipTool.CancelActive();
            }
            if (!ChiselClipTool.ActiveHasPlane)
                EditorGUILayout.HelpBox("Draw a line on a surface: the brushes are cut along it, straight down into the surface.", MessageType.None);
            else
                EditorGUILayout.HelpBox("The brushes are shown cut. Commit (Enter) keeps the cut, Cancel (Escape) puts them back.", MessageType.None);
        }
    }
}
