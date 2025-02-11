using Chisel.Core;
using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif

namespace Chisel.Editors
{
    // TODO: make it possible to 'F' focus on selected surfaces
    // TODO: hovering on surfaces in inspector should highlight in scene

    //[EditorTool("Chisel " + kToolName + " Tool", typeof(ChiselNode))]
    sealed class ChiselUVRotateTool : ChiselEditToolBase
    {
        const string kToolName = "UV Rotate";
		public override string ToolName => kToolName;


		const float kMinRotateDiameter = 1.0f;

        // TODO: see if we can get rid of these statics
		static Vector3 s_FromWorldVector = default;
		static bool s_HaveRotateStartAngle = false;
		static float s_RotateAngle = 0;


		public static bool IsActive() { return ToolManager.activeToolType == typeof(ChiselUVRotateTool); }

        #region Keyboard Shortcut
        const string kEditModeShotcutName = kToolName + " Mode";
        [Shortcut(ChiselKeyboardDefaults.ShortCutEditModeBase + kEditModeShotcutName, ChiselKeyboardDefaults.SwitchToUVRotateMode, displayName = kEditModeShotcutName)]
        public static void ActivateTool() { ToolManager.SetActiveTool<ChiselUVRotateTool>(); }
        #endregion

        #region Activate/Deactivate
        public override void OnActivate()
        {
            base.OnActivate();
            ChiselUVToolCommon.Instance.OnActivate();
            ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Surface;
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            ChiselUVToolCommon.Instance.OnDeactivate();
        }
        #endregion

        public override SnapSettings ToolUsedSnappingModes { get { return Chisel.Editors.SnapSettings.AllUV; } }

        readonly static int kSurfaceEditModeHash		= "SurfaceRotateEditMode".GetHashCode();
        readonly static int kSurfaceRotateHash			= "SurfaceRotate".GetHashCode();
        
        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var defaultID = GUIUtility.GetControlID(kSurfaceEditModeHash, FocusType.Passive, dragArea);
            HandleUtility.AddDefaultControl(defaultID);

            var selectionType	= ChiselRectSelectionManager.GetCurrentSelectionType();
            var repaint			= ChiselUVToolCommon.SurfaceSelection(dragArea, selectionType);            
            repaint = SurfaceRotateTool(selectionType, dragArea) || repaint; 
            
            // Set cursor depending on selection type and/or active tool
            {
                MouseCursor cursor; 
                switch (selectionType)
                {
                    default: cursor = MouseCursor.RotateArrow; break;
                    case SelectionType.Additive:    cursor = MouseCursor.ArrowPlus; break;
                    case SelectionType.Subtractive: cursor = MouseCursor.ArrowMinus; break;
                }
                EditorGUIUtility.AddCursorRect(dragArea, cursor);
            }
            
            // Repaint the scene-views if we need to
            if (repaint &&
                // avoid infinite loop
                Event.current.type != EventType.Layout &&
                Event.current.type != EventType.Repaint)
                SceneView.RepaintAll();
        }

        #region Surface Rotate Tool
        static void RotateSurfacesInWorldSpace(Vector3 center, Vector3 normal, float rotateAngle)
        {
            // Get the rotation on that plane, around 'worldStartPosition'
            var worldspaceRotation = MathExtensions.RotateAroundAxis(center, normal, rotateAngle);

            Undo.RecordObjects(ChiselUVToolCommon.s_SelectedNodes, "Rotate UV coordinates");
            for (int i = 0; i < ChiselUVToolCommon.s_SelectedSurfaceReferences.Length; i++)
            {
                var rotationInPlaneSpace = ChiselUVToolCommon.s_SelectedSurfaceReferences[i].WorldSpaceToPlaneSpace(worldspaceRotation);

                // TODO: Finish this. If we have multiple surfaces selected, we want other non-aligned surfaces to move/rotate in a nice way
                //		 last thing we want is that these surfaces are rotated in such a way that the uvs are rotated into infinity.
                //		 ideally the rotation would change into a translation on 90 angles, think selecting all surfaces on a cylinder 
                //	     and rotating the cylinder cap. You would want the sides to move with the rotation and not actually rotate themselves.
                var rotateToPlane = Quaternion.FromToRotation(rotationInPlaneSpace.GetColumn(2), Vector3.forward);
                var fixedRotation = Matrix4x4.TRS(Vector3.zero, rotateToPlane, Vector3.one) * rotationInPlaneSpace;

                ChiselUVToolCommon.s_SelectedSurfaceReferences[i].PlaneSpaceTransformUV(fixedRotation, ChiselUVToolCommon.s_SelectedUVMatrices[i]);
            }
        }

        private static bool SurfaceRotateTool(SelectionType selectionType, Rect dragArea)
        {
            var id = GUIUtility.GetControlID(kSurfaceRotateHash, FocusType.Keyboard, dragArea);
            if (!ChiselUVToolCommon.SurfaceToolBase(id, selectionType, dragArea))
                return false;
            
            bool needRepaint = false;            
            if (!ChiselUVToolCommon.IsToolEnabled(id))
            {
                needRepaint = s_HaveRotateStartAngle;
                s_HaveRotateStartAngle = false;
                ChiselUVToolCommon.s_PointHasSnapped = false;
            }
            
            switch (Event.current.GetTypeForControl(id))
            {
                // TODO: support rotating texture using keyboard?
                case EventType.Repaint:
                {
                    if (s_HaveRotateStartAngle)
                    {
                        var toWorldVector   = ChiselUVToolCommon.s_WorldDragDeltaVector;
                        var magnitude       = toWorldVector.magnitude;
                        toWorldVector /= magnitude;

                        // TODO: need a nicer visualization here, show delta rotation, angles etc.
                        Handles.DrawWireDisc(ChiselUVToolCommon.s_WorldStartPosition, ChiselUVToolCommon.s_WorldProjectionPlane.normal, magnitude);
                        if (s_HaveRotateStartAngle)
                        {
                            var snappedToWorldVector = Quaternion.AngleAxis(s_RotateAngle, ChiselUVToolCommon.s_WorldDragPlane.normal) * s_FromWorldVector;
                            Handles.DrawDottedLine(ChiselUVToolCommon.s_WorldStartPosition, ChiselUVToolCommon.s_WorldStartPosition + (s_FromWorldVector      * magnitude), 4.0f);
                            Handles.DrawDottedLine(ChiselUVToolCommon.s_WorldStartPosition, ChiselUVToolCommon.s_WorldStartPosition + (snappedToWorldVector * magnitude), 4.0f);
                        } else
                            Handles.DrawDottedLine(ChiselUVToolCommon.s_WorldStartPosition, ChiselUVToolCommon.s_WorldStartPosition + (toWorldVector * magnitude), 4.0f);
                    }
                    if (ChiselUVToolCommon.IsToolEnabled(id))
                    {
                        if (s_HaveRotateStartAngle &&
                            ChiselUVToolCommon.s_PointHasSnapped)
                        {
                            ChiselUVToolCommon.RenderIntersectionPoint();
                            ChiselUVToolCommon.RenderSnapEvent();
                        }
                    } 
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (!ChiselUVToolCommon.IsToolEnabled(id))
                        break;
                    
                    if (ChiselUVToolCommon.StartToolDragging())
                    {
                        s_HaveRotateStartAngle = false;
                        ChiselUVToolCommon.s_PointHasSnapped = false;
                    }

                    var toWorldVector = ChiselUVToolCommon.s_WorldDragDeltaVector;
                    if (!s_HaveRotateStartAngle)
                    {
                        var handleSize		= HandleUtility.GetHandleSize(ChiselUVToolCommon.s_WorldStartPosition);	
                        var minDiameterSqr	= handleSize * kMinRotateDiameter;
                        // Only start rotating when we've moved the cursor far away enough from the center of rotation
                        if (toWorldVector.sqrMagnitude > minDiameterSqr)
                        {
                            // Switch to rotation mode, we have a center and a start angle to compare with, 
                            // from now on, when we move the mouse we change the rotation angle relative to this first angle.
                            s_HaveRotateStartAngle = true;
                            ChiselUVToolCommon.s_PointHasSnapped = false;
                            s_FromWorldVector = toWorldVector.normalized;
                            s_RotateAngle = 0;

                            // We override the snapping settings to only allow snapping against vertices & edges, 
                            // we do this only after we have our starting vector, so that when we rotate we're not constantly
                            // snapping against the grid when we really just want to be able to snap against the current rotation step.
                            // On the other hand, we do want to be able to snap against vertices ..
                            ChiselUVToolCommon.s_ToolSnapOverrides = SnapSettings.UVGeometryVertices | SnapSettings.UVGeometryEdges; 
                        }
                    } else
                    {
                        // Get the angle between 'from' and 'to' on the plane we're dragging over
                        s_RotateAngle = MathExtensions.SignedAngle(s_FromWorldVector, toWorldVector.normalized, ChiselUVToolCommon.s_WorldDragPlane.normal);
                        
                        // If we snapped against something, ignore angle snapping
                        if (!ChiselUVToolCommon.s_PointHasSnapped) s_RotateAngle = ChiselUVToolCommon.SnapAngle(s_RotateAngle);

                        RotateSurfacesInWorldSpace(ChiselUVToolCommon.s_WorldStartPosition, ChiselUVToolCommon.s_WorldDragPlane.normal, -s_RotateAngle); // TODO: figure out why this is reversed
                    }
                    break;
                }
            }
            return needRepaint;
        }
        #endregion
    }
}
