using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

namespace Chisel.Editors
{
    [Overlay(typeof(SceneView), kOverlayTitle)]
    public class ChiselGeneratorSelectionOverlay : IMGUIOverlay
    {
        const string kOverlayTitle = "Chisel Active Generator";

        // the width it has to lay its buttons out in, as found the last time it was drawn; zero before that
        float m_AvailableWidth;

        // set while it's drawn, since the buttons' sizes come from GUI styles; it can be made as narrow as one button, or as wide as all of them in one row
        void UpdateSizeLimits()
        {
            var buttons = ChiselPlacementToolsSelectionWindow.ButtonCount();
            var height  = maxSize.y;
            minSize = new Vector2(ChiselPlacementToolsSelectionWindow.RowWidth(1), minSize.y);
            maxSize = new Vector2(ChiselPlacementToolsSelectionWindow.RowWidth(buttons), height);
        }

        public override void OnGUI()
        {
            UpdateSizeLimits();

            // the buttons rewrap to the width it has, which changes when it's resized, docked or the scene view is
            var columns = m_AvailableWidth > 0 ? ChiselPlacementToolsSelectionWindow.ColumnsThatFit(m_AvailableWidth)
                                               : ChiselPlacementToolsSelectionWindow.kToolsWide;
            EditorGUI.BeginChangeCheck();
            {
                ChiselPlacementToolsSelectionWindow.RenderGeneratorTools(columns);
            }
            if (EditorGUI.EndChangeCheck())
                ChiselEditorSettings.Save();

            // as wide as it is, without taking any height
            var available = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint && available.width > 1 && !Mathf.Approximately(available.width, m_AvailableWidth))
            {
                m_AvailableWidth = available.width;
                containerWindow?.Repaint();
            }
        }
    }
}
