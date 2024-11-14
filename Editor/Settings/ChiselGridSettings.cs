using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public static class ChiselGridSettings
    {
		readonly static ReflectedInstanceProperty<object> kSceneViewGridsProperty  = typeof(SceneView).GetProperty<object>("sceneViewGrids");
		readonly static ReflectedInstanceProperty<float>  kGridOpacityProperty     = ReflectionExtensions.GetProperty<float>("UnityEditor.SceneViewGrid", "gridOpacity");
		readonly static ReflectedInstanceProperty<int>    kGridAxisProperty        = ReflectionExtensions.GetProperty<int>("UnityEditor.SceneViewGrid", "gridAxis");

        public readonly static ReflectedProperty<Vector3> kSize              = ReflectionExtensions.GetStaticProperty<Vector3>("UnityEditor.GridSettings", "size");

        
        internal static void GridOnSceneGUI(SceneView sceneView)
        {
            if (sceneView.showGrid)
            {
                sceneView.showGrid = false;
                ChiselEditorSettings.Load();
                ChiselEditorSettings.ShowGrid = true;
                ChiselEditorSettings.Save();
            }

            var sceneViewGrids = kSceneViewGridsProperty?.GetValue(sceneView);
            GridRenderer.Opacity = kGridOpacityProperty?.GetValue(sceneViewGrids) ?? 1.0f;

            var activeTransform = Selection.activeTransform;

            if (Tools.pivotRotation == PivotRotation.Local && activeTransform)
            {
                var rotation = Tools.handleRotation;
                var center = (activeTransform && activeTransform.parent) ? activeTransform.parent.position : Vector3.zero;

				Chisel.Editors.Grid.DefaultGrid.GridToWorldSpace = Matrix4x4.TRS(center, rotation, Vector3.one);
            } else
            {
                var gridAxis = kGridAxisProperty?.GetValue(sceneViewGrids) ?? 1;
                switch (gridAxis)
                {
                    case 0: Chisel.Editors.Grid.DefaultGrid.GridToWorldSpace = Chisel.Editors.Grid.XYPlane; break;
                    case 1: Chisel.Editors.Grid.DefaultGrid.GridToWorldSpace = Chisel.Editors.Grid.XZPlane; break;
                    case 2: Chisel.Editors.Grid.DefaultGrid.GridToWorldSpace = Chisel.Editors.Grid.YZPlane; break;
                }
            }

            if (Event.current.type != EventType.Repaint)
                return;


            if (ChiselEditorSettings.ShowGrid)
            {
                var grid = Chisel.Editors.Grid.HoverGrid;
                if (grid != null)
                {
                    grid.Spacing = Chisel.Editors.Grid.DefaultGrid.Spacing;
                }
                else
                {
                    grid = Chisel.Editors.Grid.ActiveGrid;
                }
                grid.Render(sceneView);
            }

            if (Chisel.Editors.Grid.DebugGrid != null)
            {
				//static ReflectedInstanceProperty<object> sceneViewGrids = typeof(SceneView).GetProperty<object>("sceneViewGrids");
				//static ReflectedInstanceProperty<float> gridOpacity = ReflectionExtensions.GetProperty<float>("SceneViewGrid", "gridOpacity");
				Chisel.Editors.Grid.DebugGrid.Render(sceneView);
			}
        }
    }
}
