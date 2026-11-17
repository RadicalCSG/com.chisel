using System.Collections.Generic;
using System.Buffers;
using UnityEngine;
using UnityEditor;
using Chisel.Core;
using System;

namespace Chisel.Editors
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class ChiselEditorResources
    {
        internal const string kLargeIconID      = "@2x";
        internal const string kIconPath         = "Icons/";
        internal const string kActiveIconID     = " on";
        internal const string kDarkIconID       = "d_";

        static ChiselEditorResources()
        {
            s_EditorPixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            s_IsProSkin = EditorGUIUtility.isProSkin;
        }

        // Read once, when the scripts load; Chisel reloads them when the skin changes (ChiselEditorSkinWatcher)
        static readonly float s_EditorPixelsPerPoint;
        static readonly bool s_IsProSkin;
        public static bool IsProSkin => s_IsProSkin;

        public static float ImageScale { get { return (s_EditorPixelsPerPoint > 1.0f) ? 2.0f : 1.0f; } }

        static Texture2D LoadImageFromResourcePaths(string name)
        {
            var path = FindImagePath(name);
            return path == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D LoadScaledTextureInternal(string name)
        {
            Texture2D image = null;
            var imagePixelsPerPoint = 1.0f;
            if (s_EditorPixelsPerPoint > 1.0f)
            {
                image = LoadImageFromResourcePaths(name + kLargeIconID);
                if (image != null)
                    imagePixelsPerPoint = 2.0f;
            }

            if (image == null)
                image = LoadImageFromResourcePaths(name);

            if (image == null)
                return null;

            if (!Mathf.Approximately(imagePixelsPerPoint, s_EditorPixelsPerPoint) &&    // scaling are different
                !Mathf.Approximately(s_EditorPixelsPerPoint % 1, 0))                    // screen scaling is non-integer
                image.filterMode = FilterMode.Bilinear;
            return image;
        }

        // An icon asked for that wasn't there (yet): on a new install the scripts run before the icons are imported, so
        // what was made without them is made again once they are (ChiselEditorResourcesImport)
        internal static bool s_AnyMissing;

        public static Texture2D[] LoadIconImages(ChiselIcon icon)
        {
            var name = icon.name;
            var nameID = HashLowerInvariant(name);
            if (!s_IconImagesLookup.TryGetValue(nameID, out var iconImages))
            {
                iconImages = LoadIconImagesInternal(name);
                if (iconImages == null)
                    s_AnyMissing = true;
                else
                    s_IconImagesLookup[nameID] = iconImages;
            }
            return iconImages;
        }

        public static GUIContent[] GetIconContent(ChiselIcon icon, string tooltip = null)
        {
            var name = icon.name;
            var nameID = HashLowerInvariant(name);
            var id = (nameID * 33) + (tooltip?.GetHashCode() ?? 0);
            if (!s_IconContentLookup.TryGetValue(id, out var contents))
            {
                contents = GetIconContentInternal(name, tooltip ?? string.Empty, out var found);
                if (found)
                    s_IconContentLookup[id] = contents;
            }
            return contents;
        }

        public static GUIContent[] GetIconContentWithName(ChiselIcon icon, string tooltip = null)
        {
            var name = icon.name;
            var nameID = HashLowerInvariant(name);
            var id = (nameID * 33) + (tooltip?.GetHashCode() ?? 0);
            if (!s_IconContentWithNameLookup.TryGetValue(id, out var contents))
            {
                contents = GetIconContentWithNameInternal(name, tooltip ?? string.Empty, out var found);
                if (found)
                    s_IconContentWithNameLookup[id] = contents;
            }
            return contents;
        }

        // Whether an import brings icons that were asked for before they were there
        internal static bool BringsMissingIcons(string[] importedAssets)
        {
            if (!s_AnyMissing || importedAssets == null)
                return false;
            foreach (var asset in importedAssets)
            {
                var path = FixSlashes(asset);
                if (!path.EndsWith(kImageExtension, StringComparison.OrdinalIgnoreCase))
                    continue;
                foreach (var folder in kSearchFolders)
                {
                    if (path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        readonly static Dictionary<string, Texture2D> s_ImagesLookup              = new();
        readonly static Dictionary<int, Texture2D[]>  s_IconImagesLookup          = new();
        readonly static Dictionary<int, GUIContent[]> s_IconContentLookup         = new();
        readonly static Dictionary<int, GUIContent[]> s_IconContentWithNameLookup = new();

        static Texture2D LoadImageInternal(string name)
        {
            name = FixSlashes(name);
            if (s_ImagesLookup.TryGetValue(name, out Texture2D image))
                return image;
            image = LoadScaledTextureInternal(name);
            if (!image)
                return image;
            s_ImagesLookup[name] = image;
            return image;
        }

        static int HashLowerInvariant(string name)
        {
            var length = name.Length;
            if (length == 0)
                return 0;

            var nameBuffer = ArrayPool<char>.Shared.Rent(length);

            for (int i = 0; i < length; i++)
            {
                var lowerChar = char.ToLowerInvariant(name[i + 0]);
                nameBuffer[i] = lowerChar;
            }

            int result;
            unchecked { result = (int)nameBuffer.Hash(); }
            ArrayPool<char>.Shared.Return(nameBuffer);
            return result;
        }

        // The names an icon's image is looked for by, in order: the dark skin's first, the active look's before the plain
        // one, falling back to the plain one
        static void IconImageNames(string name, bool dark, bool active, List<string> names)
        {
            names.Clear();
            var nameID = name;
            if (dark)
            {
                if (active) names.Add($@"{kIconPath}{kDarkIconID}{nameID}{kActiveIconID}");
                names.Add($@"{kIconPath}{kDarkIconID}{nameID}");
            }
            if (active) names.Add($@"{kIconPath}{nameID}{kActiveIconID}");
            names.Add($@"{kIconPath}{nameID}");
        }

        static Texture2D LoadIconImageInternal(string name, bool active)
        {
            var names = new List<string>(4);
            IconImageNames(name, s_IsProSkin, active, names);
            foreach (var imageName in names)
            {
                var result = LoadImageInternal(imageName);
                if (result != null)
                    return result;
            }
            return null;
        }

        // The asset an icon is loaded from in a skin and look, as the editor finds it, or null when there is none
        internal static string FindIconPath(ChiselIcon icon, bool dark, bool active)
        {
            var names = new List<string>(4);
            IconImageNames(icon.name, dark, active, names);
            foreach (var imageName in names)
            {
                var path = FindImagePath(imageName);
                if (path != null)
                    return path;
            }
            return null;
        }

        static Texture2D[] LoadIconImagesInternal(string name)
        {
            var iconImages = new[] { LoadIconImageInternal(name, false), LoadIconImageInternal(name, true) };
            if (iconImages[0] == null || iconImages[1] == null)
                iconImages = null;
            return iconImages;
        }

        static GUIContent[] GetIconContentInternal(string name, string tooltip, out bool found)
        {
			tooltip ??= string.Empty;

            GUIContent[] contents;
            var images = LoadIconImagesInternal(name);
            found = images != null;
            if (!found)
                s_AnyMissing = true;
            if (images == null)
                contents = new GUIContent[] { new GUIContent(L10n.Tr(name), L10n.Tr(tooltip)), new GUIContent(L10n.Tr(name), L10n.Tr(tooltip)) };
            else
                contents = new GUIContent[] { new GUIContent(images[0], L10n.Tr(tooltip)), new GUIContent(images[1], L10n.Tr(tooltip)) };
            return contents;
        }


        static GUIContent[] GetIconContentWithNameInternal(string name, string tooltip, out bool found)
        {
            tooltip ??= string.Empty;

            GUIContent[] contents;
            var images = LoadIconImagesInternal(name);
            found = images != null;
            if (!found)
                s_AnyMissing = true;
            if (images == null)
                contents = new GUIContent[] { new GUIContent(name, tooltip), new GUIContent(name, tooltip) };
            else
                contents = new GUIContent[] { new GUIContent(name, images[0], tooltip), new GUIContent(name, images[1], tooltip) };
            return contents;
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        public static void ClearCache()
        {
            s_AnyMissing = false;
            s_ImagePaths = null;
            s_ImagesLookup.Clear();
            s_IconImagesLookup.Clear();
            s_IconContentLookup.Clear();
            s_IconContentWithNameLookup.Clear();
        }


        #region Editor Resource Paths
        const string kImageExtension        = @".png";

        // Where Chisel's images are, the package's own first. By its Unity path, which the package has wherever it is
        // on disk, also before the package manager knows it (a new install)
        internal static readonly string[] kSearchFolders = { "Packages/com.chisel/Editor Resources", "Assets/Editor Resources" };

        // Every image in the search folders, by its path below its folder without the extension, in lower case: the path
        // it was imported at. Windows and macOS find a file whatever the case of its name, Linux only by its exact name,
        // so an image is only ever loaded by the path found here, never by one made up from the name it is asked for by
        static Dictionary<string, string> s_ImagePaths;

        static Dictionary<string, string> ImagePaths
        {
            get
            {
                if (s_ImagePaths != null)
                    return s_ImagePaths;
                var folders = new List<string>(kSearchFolders.Length);
                foreach (var folder in kSearchFolders)
                {
                    if (AssetDatabase.IsValidFolder(folder))
                        folders.Add(folder);
                }
                var assetPaths = new List<string>();
                if (folders.Count > 0)
                {
                    foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", folders.ToArray()))
                        assetPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
                }
                return s_ImagePaths = IndexImages(kSearchFolders, assetPaths);
            }
        }

        // The images among assetPaths by their path below the first of the folders they are in, without the extension
        // and in lower case. Of two whose paths differ only in case the first is kept; ChiselIconTests sees there are none
        internal static Dictionary<string, string> IndexImages(IReadOnlyList<string> folders, IEnumerable<string> assetPaths)
        {
            var index = new Dictionary<string, string>(StringComparer.Ordinal);
            var paths = new List<string>(assetPaths);
            foreach (var folder in folders)
            {
                var prefix = folder + "/";
                foreach (var assetPath in paths)
                {
                    var path = FixSlashes(assetPath);
                    if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                        !path.EndsWith(kImageExtension, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var key = path.Substring(prefix.Length, path.Length - prefix.Length - kImageExtension.Length).ToLowerInvariant();
                    index.TryAdd(key, path);
                }
            }
            return index;
        }

        // The path of the image asked for by a name below the search folders without its extension, whatever its case
        internal static string FindImagePath(string name)
        {
            return ImagePaths.TryGetValue(FixSlashes(name).ToLowerInvariant(), out var path) ? path : null;
        }

        static string FixSlashes(string path)
        {
            return path.Replace('\\', '/');
        }

        #endregion
    }
}
