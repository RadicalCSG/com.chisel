using System;
using UnityEditor;

namespace Chisel.Editors
{
    // Which assets were imported, deleted or moved, so the Surface Browser learns again only those
    sealed class ChiselSurfaceBrowserAssetChanges : AssetPostprocessor
    {
        /// <summary>imported, deleted, moved (to), moved from</summary>
        public static event Action<string[], string[], string[], string[]> Changed;

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            Changed?.Invoke(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
        }
    }
}
