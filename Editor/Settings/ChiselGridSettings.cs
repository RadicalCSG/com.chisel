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
            var activeTransform = Selection.activeTransform;

            if (Tools.pivotRotation == PivotRotation.Local && activeTransform)
            {
                var rotation = Tools.handleRotation;
                var center = activeTransform.parent ? activeTransform.parent.position : Vector3.zero;

                Chisel.Editors.Grid.DefaultGrid.GridToWorldSpace = Matrix4x4.TRS(center, rotation, Vector3.one);
            } else
                Chisel.Editors.Grid.DefaultGrid.GridToWorldSpace = Matrix4x4.identity;

            ShowInUnityGrid(Chisel.Editors.Grid.HoverGrid ?? Chisel.Editors.Grid.ActiveGrid);

            if (Event.current.type != EventType.Repaint)
                return;

            if (Chisel.Editors.Grid.DebugGrid != null)
            {
                Chisel.Editors.Grid.DebugGrid.Render(sceneView);
            }
        }

        // Unity draws its grid where Chisel's grid is
        internal static void ShowInUnityGrid(Grid grid)
        {
            var position = grid.Center;
            var rotation = Quaternion.LookRotation(grid.Forward, grid.Up);
            if (EditorSnapSettings.gridPosition != position)
                EditorSnapSettings.gridPosition = position;
            if (EditorSnapSettings.gridRotation != rotation)
                EditorSnapSettings.gridRotation = rotation;
        }
    }
}
