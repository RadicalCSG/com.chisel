using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public static class ChiselGridSettings
    {
        /// <summary>The size of Unity's grid</summary>
        public static Vector3 Size
        {
            get => EditorSnapSettings.gridSize;
            set => EditorSnapSettings.gridSize = value;
        }

        internal static void GridOnSceneGUI(SceneView sceneView)
        {
            if (sceneView.showGrid)
            {
                sceneView.showGrid = false;
                ChiselEditorSettings.Load();
                ChiselEditorSettings.ShowGrid = true;
                ChiselEditorSettings.Save();
            }

            ChiselSceneViewGrid.Read(sceneView, out var gridOpacity, out var gridAxis);
            GridRenderer.Opacity = gridOpacity;

            var activeTransform = Selection.activeTransform;

            if (Tools.pivotRotation == PivotRotation.Local && activeTransform)
            {
                var rotation = Tools.handleRotation;
                var center = (activeTransform && activeTransform.parent) ? activeTransform.parent.position : Vector3.zero;

				Chisel.Editors.Grid.DefaultGrid.GridToWorldSpace = Matrix4x4.TRS(center, rotation, Vector3.one);
            } else
            {
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
				Chisel.Editors.Grid.DebugGrid.Render(sceneView);
			}
        }
    }

    // The scene view's grid opacity and plane have no public API: they are read from its internals
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    static class ChiselSceneViewGrid
    {
        const string kFeature = "following the scene view's grid opacity and plane";

        static readonly PropertyInfo s_SceneViewGrids;
        static readonly PropertyInfo s_GridOpacity;
        static readonly PropertyInfo s_GridAxis;

        public static bool Available => s_SceneViewGrids != null && s_GridOpacity != null && s_GridAxis != null;

        static ChiselSceneViewGrid()
        {
            var sceneViewGridType = ChiselUnityInternals.Type("UnityEditor.SceneViewGrid", kFeature);
            s_SceneViewGrids = ChiselUnityInternals.Property(typeof(SceneView), "sceneViewGrids", kFeature);
            s_GridOpacity    = ChiselUnityInternals.Property(sceneViewGridType, "gridOpacity", kFeature);
            s_GridAxis       = ChiselUnityInternals.Property(sceneViewGridType, "gridAxis", kFeature);
        }

        // The grid's opacity, and its plane (0: XY, 1: XZ, 2: YZ); fully opaque on XZ when they can't be read
        public static void Read(SceneView sceneView, out float opacity, out int axis)
        {
            opacity = 1.0f;
            axis    = 1;
            if (!Available || !sceneView)
                return;
            var grids = s_SceneViewGrids.GetValue(sceneView);
            if (grids == null)
                return;
            opacity = (float)s_GridOpacity.GetValue(grids);
            axis    = Convert.ToInt32(s_GridAxis.GetValue(grids));
        }
    }
}
