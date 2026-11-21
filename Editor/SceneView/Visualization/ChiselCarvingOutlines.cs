using Chisel.Core;
using UnityEditor;
using UnityEditor.ShortcutManagement;

namespace Chisel.Editors
{
    // Subtractive and intersecting brushes carve the model, so on their own they can't be seen: their outlines can be
    // drawn whether they're selected or not (ChiselOutlineRenderer), switched in the Chisel Helper Surfaces overlay or
    // with a shortcut
    public static class ChiselCarvingOutlines
    {
        const string kShortcutBase = "Chisel/Show/";

        public static bool AnyShown => ChiselEditorSettings.ShowSubtractiveOutlines || ChiselEditorSettings.ShowIntersectingOutlines;

        public static bool Shows(CSGOperationType operation)
        {
            switch (operation)
            {
                case CSGOperationType.Subtractive:  return ChiselEditorSettings.ShowSubtractiveOutlines;
                case CSGOperationType.Intersecting: return ChiselEditorSettings.ShowIntersectingOutlines;
                default:                            return false;
            }
        }

        public static void Show(CSGOperationType operation, bool show)
        {
            if (Shows(operation) == show)
                return;
            switch (operation)
            {
                case CSGOperationType.Subtractive:  ChiselEditorSettings.ShowSubtractiveOutlines  = show; break;
                case CSGOperationType.Intersecting: ChiselEditorSettings.ShowIntersectingOutlines = show; break;
                default:                            return;
            }
            ChiselEditorSettings.Save();
            ChiselOutlineRenderer.OnCarvingOutlinesChanged();
            SceneView.RepaintAll();
        }

        [Shortcut(kShortcutBase + "Subtractive Outlines", typeof(SceneView))]
        static void ToggleSubtractive() { Show(CSGOperationType.Subtractive, !Shows(CSGOperationType.Subtractive)); }

        [Shortcut(kShortcutBase + "Intersecting Outlines", typeof(SceneView))]
        static void ToggleIntersecting() { Show(CSGOperationType.Intersecting, !Shows(CSGOperationType.Intersecting)); }
    }
}
