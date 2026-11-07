using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // What the surface tools remember for this user in this project: favorite and recently used materials and decal
    // images. An item is an asset's GUID, and for a decal image of a sheet its tile ("guid:tile")
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class ChiselSurfaceBrowserPrefs
    {
        public const int kMaxRecent = 20;

        const string kFavoritesKey = "Chisel.SurfaceBrowser.Favorites";
        const string kRecentKey    = "Chisel.SurfaceBrowser.Recent";

        // per project: preferences are shared by every project on the machine
        static string Key(string name) { return name + "." + Application.dataPath.GetHashCode().ToString("x8"); }

        public static string ItemOf(UnityEngine.Object asset, int tile = 0)
        {
            if (asset == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long _))
                return null;
            return tile != 0 ? guid + ":" + tile : guid;
        }

        static List<string> Read(string name)
        {
            var value = EditorPrefs.GetString(Key(name), string.Empty);
            return new List<string>(value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
        }

        static void Write(string name, List<string> items)
        {
            EditorPrefs.SetString(Key(name), string.Join(";", items));
        }

        public static IReadOnlyList<string> Favorites => Read(kFavoritesKey);

        public static bool IsFavorite(string item) { return !string.IsNullOrEmpty(item) && Read(kFavoritesKey).Contains(item); }

        public static void SetFavorite(string item, bool favorite)
        {
            if (string.IsNullOrEmpty(item))
                return;
            var items = Read(kFavoritesKey);
            if (items.Contains(item) == favorite)
                return;
            if (favorite) items.Add(item); else items.Remove(item);
            Write(kFavoritesKey, items);
        }

        /// <summary>The most recently used first</summary>
        public static IReadOnlyList<string> Recent => Read(kRecentKey);

        /// <summary>Puts an item first in the recently used, keeping at most <see cref="kMaxRecent"/></summary>
        public static void MarkUsed(string item)
        {
            if (string.IsNullOrEmpty(item))
                return;
            var items = Read(kRecentKey);
            items.Remove(item);
            items.Insert(0, item);
            if (items.Count > kMaxRecent)
                items.RemoveRange(kMaxRecent, items.Count - kMaxRecent);
            Write(kRecentKey, items);
        }

        // where they're kept, so a test can put back what was there
        internal static string[] StorageKeys => new[] { Key(kFavoritesKey), Key(kRecentKey) };
    }
}
