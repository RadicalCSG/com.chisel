using UnityEditor;

namespace Chisel.Editors
{
    // Icons imported after the scripts asked for them (a new install): the scripts are loaded again, so what was made
    // without them (toolbars, overlays, tools) is made with them
    sealed class ChiselEditorResourcesImport : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!ChiselEditorResources.BringsMissingIcons(importedAssets))
                return;
            ChiselEditorResources.ClearCache();
            EditorUtility.RequestScriptReload();
        }
    }
}
