using Chisel.Core;
using UnityEngine;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditor.EditorTools;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif

namespace Chisel.Editors
{
    // TODO: make it possible to 'F' focus on selected surfaces
    // TODO: hovering on surfaces in inspector should highlight in scene

    //[EditorTool("Chisel " + kToolName + " Tool", typeof(ChiselNode))]
    sealed class ChiselUVScaleTool : ChiselEditToolBase
    {
        const string kToolName = "UV Scale";
        public override string ToolName => kToolName;

		const float kMinScaleDiameter = 2.0f;

		// TODO: see if we can get rid of these statics
		static bool s_HaveScaleStartLength = false;
		static float s_CompareDistance = 0;
		public static bool IsActive() { return ToolManager.activeToolType == typeof(ChiselUVScaleTool); }

        #region Keyboard Shortcut
        const string kEditModeShotcutName = kToolName + " Mode";
        [Shortcut(ChiselKeyboardDefaults.ShortCutEditModeBase + kEditModeShotcutName, ChiselKeyboardDefaults.SwitchToUVScaleMode, displayName = kEditModeShotcutName)]
        public static void ActivateTool() { ToolManager.SetActiveTool<ChiselUVScaleTool>(); }
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

        readonly static int kSurfaceEditModeHash		= "SurfaceScaleEditMode".GetHashCode();
        readonly static int kSurfaceScaleHash			= "SurfaceScale".GetHashCode();
        
        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var defaultID = GUIUtility.GetControlID(kSurfaceEditModeHash, FocusType.Passive, dragArea);
            HandleUtility.AddDefaultControl(defaultID);

            var selectionType	= ChiselRectSelectionManager.GetCurrentSelectionType();
            var repaint			= ChiselUVToolCommon.SurfaceSelection(dragArea, selectionType);
            repaint = SurfaceScaleTool(selectionType,  dragArea) || repaint;

            // Set cursor depending on selection type and/or active tool
            {
                MouseCursor cursor;
                switch (selectionType)
                {
                    default: cursor = MouseCursor.ScaleArrow; break;
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


        #region Surface Scale Tool
        static void ScaleSurfacesInWorldSpace(Vector3 center, Vector3 normal, float scale)
        {
            if (float.IsNaN(scale) ||
                float.IsInfinity(scale) ||
                scale == 0.0f)
                return;

            // Get the rotation on that plane, around 'worldStartPosition'
            var worldspaceRotation = MathExtensions.ScaleFromPoint(center, normal, scale);

            Undo.RecordObjects(ChiselUVToolCommon.s_SelectedNodes, "Scale UV coordinates");
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

        private static bool SurfaceScaleTool(SelectionType selectionType, Rect dragArea)
        {
            var id = GUIUtility.GetControlID(kSurfaceScaleHash, FocusType.Keyboard, dragArea);
            if (!ChiselUVToolCommon.SurfaceToolBase(id, selectionType, dragArea))
                return false;

            bool needRepaint = false;            
            switch (Event.current.GetTypeForControl(id))
            {
                // TODO: support scaling texture using keyboard
                case EventType.Repaint:
                {
                    if (s_HaveScaleStartLength)
                    {
                        var toWorldVector   = ChiselUVToolCommon.s_WorldDragDeltaVector;
                        var magnitude       = toWorldVector.magnitude;
                        toWorldVector /= magnitude;
                        if (s_HaveScaleStartLength)
                        {
                            Handles.DrawDottedLine(ChiselUVToolCommon.s_WorldStartPosition, ChiselUVToolCommon.s_WorldStartPosition + (toWorldVector * s_CompareDistance), 4.0f);
                            Handles.DrawDottedLine(ChiselUVToolCommon.s_WorldStartPosition, ChiselUVToolCommon.s_WorldStartPosition + (toWorldVector * magnitude), 4.0f);
                        } else
                            Handles.DrawDottedLine(ChiselUVToolCommon.s_WorldStartPosition, ChiselUVToolCommon.s_WorldStartPosition + (toWorldVector * magnitude), 4.0f);
                    }
                    if (ChiselUVToolCommon.IsToolEnabled(id))
                    {
                        if (s_HaveScaleStartLength &&
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
                        s_HaveScaleStartLength = false;
                        ChiselUVToolCommon.s_PointHasSnapped = false;
                    }

                    var toWorldVector = ChiselUVToolCommon.s_WorldDragDeltaVector;
                    if (!s_HaveScaleStartLength)
                    {
                        var handleSize		= HandleUtility.GetHandleSize(ChiselUVToolCommon.s_WorldStartPosition);	
                        var minDiameterSqr	= handleSize * kMinScaleDiameter;
                        // Only start scaling when we've moved the cursor far away enough from the center of scale
                        if (toWorldVector.sqrMagnitude > minDiameterSqr)
                        {
                            // Switch to scaling mode, we have a center and a start distance to compare with, 
                            // from now on, when we move the mouse we change the scale relative to this first distance.
                            s_HaveScaleStartLength = true;
                            ChiselUVToolCommon.s_PointHasSnapped = false;
                            s_CompareDistance = toWorldVector.sqrMagnitude;
                        }
                    } else
                    {
                        // TODO: drag from one position to another -> texture should fit in between and tile accordingly, taking rotation into account
                        ScaleSurfacesInWorldSpace(ChiselUVToolCommon.s_WorldStartPosition, ChiselUVToolCommon.s_WorldDragPlane.normal, s_CompareDistance / toWorldVector.sqrMagnitude);
                    }
                    break;
                }
            }
            return needRepaint;
        }
        #endregion
    }
}
