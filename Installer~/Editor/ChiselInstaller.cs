using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Chisel.Installer
{
    // Installs Chisel and the packages it needs from git, which the Package Manager doesn't install for a package that
    // depends on them, then removes itself
    [InitializeOnLoad]
    public static class ChiselInstaller
    {
        public const string kInstallerName = "com.chisel.installer";
        public const string kChiselName    = "com.chisel";
        public const string kMetadataName  = "com.custom_asset_metadata";
        public const string kChiselUrl     = "https://github.com/RadicalCSG/com.chisel.git";
        public const string kMetadataUrl   = "https://github.com/RadicalCSG/CustomAssetMetadata.git";

        const string kTriedKey = "ChiselInstaller.Tried";

        static ListRequest          listRequest;
        static AddAndRemoveRequest  installRequest;

        static ChiselInstaller()
        {
            EditorApplication.delayCall += Install;
        }

        // What to add, given what the project has. Chisel from git without the metadata package is from before that was a
        // package of its own (it came inside Chisel), so it's updated along with it.
        public static string[] ToAdd(bool hasMetadata, bool hasChisel, bool chiselFromGit)
        {
            var add = new List<string>();
            if (!hasMetadata)
                add.Add(kMetadataUrl);
            if (!hasChisel || (!hasMetadata && chiselFromGit))
                add.Add(kChiselUrl);
            return add.ToArray();
        }

        static void Install()
        {
            // a copy someone works on, not one that was installed
            var self = PackageInfo.FindForAssembly(typeof(ChiselInstaller).Assembly);
            if (self == null || self.source == PackageSource.Local || self.source == PackageSource.Embedded)
                return;
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
                Debug.LogError($"Chisel Installer: couldn't list the project's packages: {listRequest.Error?.message}");
                return;
            }
            var packages = listRequest.Result.ToDictionary(package => package.name);
            var hasChisel = packages.TryGetValue(kChiselName, out var chisel);
            var add = ToAdd(packages.ContainsKey(kMetadataName), hasChisel, hasChisel && chisel.source == PackageSource.Git);
            installRequest = Client.AddAndRemove(add, new[] { kInstallerName });
            EditorApplication.update += WaitForInstall;
        }

        static void WaitForInstall()
        {
            if (!installRequest.IsCompleted)
                return;
            EditorApplication.update -= WaitForInstall;
            if (installRequest.Status != StatusCode.Success)
                Debug.LogError($"Chisel Installer: couldn't install Chisel: {installRequest.Error?.message}\n" +
                               "Git has to be installed and work from a command prompt, see " +
                               "https://github.com/RadicalCSG/com.chisel/blob/main/INSTALLATION.md");
        }
    }
}
