using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // What the surface tools remember for this user in this project: favorite and recently used materials and decal
    // images, and the active material. An item is an asset's GUID, and for a decal image of a sheet its tile ("guid:tile")
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class ChiselSurfaceBrowserPrefs
    {
        public const int kMaxRecent = 20;

        const string kFavoritesKey = "Chisel.SurfaceBrowser.Favorites";
        const string kRecentKey    = "Chisel.SurfaceBrowser.Recent";
        const string kActiveKey    = "Chisel.SurfaceBrowser.ActiveMaterial";

        // per project: preferences are shared by every project on the machine
        static string Key(string name) { return name + "." + Application.dataPath.GetHashCode().ToString("x8"); }

        public static string ItemOf(UnityEngine.Object asset, int tile = 0)
        {
            if (asset == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long _))
                return null;
            return tile != 0 ? guid + ":" + tile : guid;
        }

        // what was read of each list, as stored: read again only when what is stored differs
        static readonly Dictionary<string, (string stored, List<string> items, HashSet<string> set)> s_Read = new();

        /// <summary>Changes when the favorites or the recently used change</summary>
        public static int Version { get; private set; }

        static (List<string> items, HashSet<string> set) ReadCached(string name)
        {
            var stored = EditorPrefs.GetString(Key(name), string.Empty);
            if (s_Read.TryGetValue(name, out var read) && read.stored == stored)
                return (read.items, read.set);
            var items = new List<string>(stored.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            s_Read[name] = (stored, items, new HashSet<string>(items));
            Version++;
            return (items, s_Read[name].set);
        }

        static List<string> Read(string name) { return new List<string>(ReadCached(name).items); }

        static void Write(string name, List<string> items)
        {
            EditorPrefs.SetString(Key(name), string.Join(";", items));
            Version++;
        }

        public static IReadOnlyList<string> Favorites => ReadCached(kFavoritesKey).items;

        public static bool IsFavorite(string item) { return !string.IsNullOrEmpty(item) && ReadCached(kFavoritesKey).set.Contains(item); }

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
        public static IReadOnlyList<string> Recent => ReadCached(kRecentKey).items;

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

        /// <summary>The material last picked in the Surface Browser, which new brushes get; none once deselected</summary>
        public static Material ActiveMaterial
        {
            get
            {
                var guid = EditorPrefs.GetString(Key(kActiveKey), string.Empty);
                return string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            }
            set { EditorPrefs.SetString(Key(kActiveKey), ItemOf(value) ?? string.Empty); }
        }

        // where they're kept, so a test can put back what was there
        internal static string[] StorageKeys => new[] { Key(kFavoritesKey), Key(kRecentKey), Key(kActiveKey) };
    }
}
