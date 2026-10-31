using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Chisel.Bootstrap
{
    // Chisel installed from its own git URL: the Package Manager doesn't install the git packages it needs, so this adds
    // Custom Asset Metadata when it's missing. The rest of Chisel compiles only once it's there (a define constraint on
    // CHISEL_ASSET_METADATA, which its version define sets); this assembly doesn't need it.
    [InitializeOnLoad]
    public static class ChiselBootstrap
    {
        public const string kMetadataName = "com.custom_asset_metadata";
        public const string kMetadataUrl  = "https://github.com/RadicalCSG/CustomAssetMetadata.git";

        const string kTriedKey = "ChiselBootstrap.Tried";

        static ListRequest listRequest;
        static AddRequest  addRequest;

        static ChiselBootstrap()
        {
            EditorApplication.delayCall += Start;
        }

        static void Start()
        {
            // once per session, so a failed install isn't tried again at every domain reload
            if (SessionState.GetBool(kTriedKey, false))
                return;
            SessionState.SetBool(kTriedKey, true);
            listRequest = Client.List(offlineMode: true, includeIndirectDependencies: true);
            EditorApplication.update += WaitForList;
        }

        static void WaitForList()
        {
            if (!listRequest.IsCompleted)
                return;
            EditorApplication.update -= WaitForList;
            if (listRequest.Status != StatusCode.Success)
            {
                Debug.LogError($"Chisel: couldn't list the project's packages: {listRequest.Error?.message}");
                return;
            }
            if (listRequest.Result.Any(package => package.name == kMetadataName))
                return;
            addRequest = Client.Add(kMetadataUrl);
            EditorApplication.update += WaitForAdd;
        }

        static void WaitForAdd()
        {
            if (!addRequest.IsCompleted)
                return;
            EditorApplication.update -= WaitForAdd;
            if (addRequest.Status != StatusCode.Success)
                Debug.LogError($"Chisel: couldn't install Custom Asset Metadata, which Chisel needs: {addRequest.Error?.message}\n" +
                               $"Git has to be installed and work from a command prompt; or add {kMetadataUrl} in the Package Manager. " +
                               "See https://github.com/RadicalCSG/com.chisel/blob/main/INSTALLATION.md");
        }
    }
}
