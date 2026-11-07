using System;
using System.Collections.Generic;
using Chisel.Core;

namespace Chisel.Editors
{
    // What the Surface Browser lists, kept apart from drawing it: the assets, filtered by search, quick access, folder
    // and label, with the folder tree and labels built from the ones that aren't hidden
    public sealed class ChiselSurfaceBrowserModel
    {
        public sealed class Item
        {
            public string   guid;
            public string   path;      // as the asset database has it, "Assets/..." or "Packages/..."
            public string   name;
            public string[] labels = Array.Empty<string>();

            public string Folder
            {
                get
                {
                    var slash = path.LastIndexOf('/');
                    return slash > 0 ? path.Substring(0, slash) : string.Empty;
                }
            }
        }

        public sealed class Folder
        {
            public string       path;
            public string       name;
            public int          depth;
            public int          count;      // its items and its subfolders'
            public bool         hidden;     // hidden itself, or through a folder above it
            public List<Folder> children = new();
        }

        public enum QuickAccess { All, Favorites, RecentlyUsed, InThisScene, UnusedInThisScene }

        // Something a material has or not (paintable, a skybox, hotspots); a filter asks for it, or for its absence
        public sealed class Trait
        {
            public string           name;
            public Func<Item, bool> has;
        }
        public enum TraitFilter { Any, With, Without }

        public readonly List<Trait> traits = new();
        readonly Dictionary<string, TraitFilter> traitFilters = new();

        public TraitFilter FilterOf(string trait)
        {
            return traitFilters.TryGetValue(trait, out var filter) ? filter : TraitFilter.Any;
        }

        public void SetFilter(string trait, TraitFilter filter)
        {
            if (filter == TraitFilter.Any) traitFilters.Remove(trait);
            else traitFilters[trait] = filter;
        }

        /// <summary>The next of Any, With, Without, round again</summary>
        public static TraitFilter Next(TraitFilter filter) { return (TraitFilter)(((int)filter + 1) % 3); }

        bool PassesTraits(Item item)
        {
            foreach (var trait in traits)
            {
                var filter = FilterOf(trait.name);
                if (filter != TraitFilter.Any && trait.has(item) != (filter == TraitFilter.With))
                    return false;
            }
            return true;
        }

        /// <summary>How many of the visible items have a trait</summary>
        public int CountWith(Trait trait)
        {
            var count = 0;
            foreach (var item in items)
            {
                if (IsVisible(item) && trait.has(item))
                    count++;
            }
            return count;
        }

        // what quick access asks of an item, given from outside: per user and per scene
        public Func<Item, bool> isFavorite   = _ => false;
        public Func<Item, bool> isInScene    = _ => false;
        public Func<IReadOnlyList<string>> recent = () => Array.Empty<string>();
        public Func<string, bool> isHidden   = ChiselProjectSettings.IsHidden;

        readonly List<Item> items = new();
        public IReadOnlyList<Item> Items => items;

        public void SetItems(IEnumerable<Item> newItems)
        {
            items.Clear();
            if (newItems != null)
                items.AddRange(newItems);
        }

        public bool IsVisible(Item item) { return !isHidden(item.path); }

        // A search: words that must all be in the name, and labels ("l:metal") the item must all have; case is ignored
        public struct Search
        {
            public List<string> words;
            public List<string> labels;
        }

        public static Search ParseSearch(string text)
        {
            var search = new Search { words = new List<string>(), labels = new List<string>() };
            if (string.IsNullOrWhiteSpace(text))
                return search;
            foreach (var part in text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.StartsWith("l:", StringComparison.OrdinalIgnoreCase))
                {
                    if (part.Length > 2)
                        search.labels.Add(part.Substring(2));
                } else
                    search.words.Add(part);
            }
            return search;
        }

        static bool HasLabel(Item item, string label)
        {
            foreach (var itemLabel in item.labels)
            {
                if (string.Equals(itemLabel, label, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static bool Matches(Item item, in Search search)
        {
            foreach (var word in search.words)
            {
                if (item.name.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }
            foreach (var label in search.labels)
            {
                if (!HasLabel(item, label))
                    return false;
            }
            return true;
        }

        // in any of the folders, or anywhere when none are given
        static bool InFolders(Item item, IReadOnlyList<string> folders)
        {
            if (folders == null || folders.Count == 0)
                return true;
            foreach (var folder in folders)
            {
                if (item.path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        bool InQuickAccess(Item item, QuickAccess quick)
        {
            switch (quick)
            {
                case QuickAccess.Favorites:    return isFavorite(item);
                case QuickAccess.RecentlyUsed: return Contains(recent(), item.guid);
                case QuickAccess.InThisScene:  return isInScene(item);
                case QuickAccess.UnusedInThisScene: return !isInScene(item);
                default:                       return true;
            }
        }

        static bool Contains(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                    return true;
            }
            return false;
        }

        public enum Sort { Name, Folder, UseInScene }

        /// <summary>The items shown: not hidden, in the quick access list, folder and label chosen, and matching the
        /// search. Recently used keeps its own order</summary>
        public List<Item> Filter(string searchText, QuickAccess quick, IReadOnlyList<string> folders = null, string label = null,
                                 Sort sort = Sort.Name, Func<Item, int> useInScene = null)
        {
            var search = ParseSearch(searchText);
            var result = new List<Item>();
            foreach (var item in items)
            {
                if (!IsVisible(item) || !InQuickAccess(item, quick) || !InFolders(item, folders) ||
                    (!string.IsNullOrEmpty(label) && !HasLabel(item, label)) || !PassesTraits(item) || !Matches(item, search))
                    continue;
                result.Add(item);
            }
            if (quick == QuickAccess.RecentlyUsed)
            {
                var order = recent();
                result.Sort((a, b) => IndexOf(order, a.guid).CompareTo(IndexOf(order, b.guid)));
                return result;
            }
            switch (sort)
            {
                case Sort.Folder:
                    result.Sort((a, b) => { var c = string.Compare(a.Folder, b.Folder, StringComparison.OrdinalIgnoreCase); return c != 0 ? c : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase); });
                    break;
                case Sort.UseInScene when useInScene != null:
                    result.Sort((a, b) => { var c = useInScene(b).CompareTo(useInScene(a)); return c != 0 ? c : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase); });
                    break;
                default:
                    result.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                    break;
            }
            return result;
        }

        static int IndexOf(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                    return i;
            }
            return int.MaxValue;
        }

        public int Count(QuickAccess quick)
        {
            var count = 0;
            foreach (var item in items)
            {
                if (IsVisible(item) && InQuickAccess(item, quick))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// The folders holding items, as trees under "Assets" and "Packages": each with how many items it and its
        /// subfolders hold. Hidden folders are left out, or with includeHidden kept and marked (their items counted)
        /// </summary>
        public List<Folder> BuildFolders(bool includeHidden)
        {
            var roots  = new List<Folder>();
            var byPath = new Dictionary<string, Folder>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                var hidden = !IsVisible(item);
                if (hidden && !includeHidden)
                    continue;
                var folderPath = item.Folder;
                if (folderPath.Length == 0)
                    continue;
                var parts = folderPath.Split('/');
                Folder parent = null;
                var path = string.Empty;
                for (int i = 0; i < parts.Length; i++)
                {
                    path = i == 0 ? parts[0] : path + "/" + parts[i];
                    if (!byPath.TryGetValue(path, out var folder))
                    {
                        folder = new Folder { path = path, name = parts[i], depth = i, hidden = isHidden(path) };
                        byPath.Add(path, folder);
                        if (parent == null) roots.Add(folder); else parent.children.Add(folder);
                    }
                    // a shown folder's count leaves hidden items out; a hidden one counts its own
                    if (!hidden || folder.hidden)
                        folder.count++;
                    parent = folder;
                }
            }
            SortFolders(roots);
            // Assets before Packages, whatever else
            roots.Sort((a, b) => RootOrder(a.name).CompareTo(RootOrder(b.name)));
            return roots;
        }

        /// <summary>How many of the assets each folder holds, its subfolders' included</summary>
        public static Dictionary<string, int> CountPerFolder(IEnumerable<string> assetPaths)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in assetPaths)
            {
                for (var slash = path.LastIndexOf('/'); slash > 0; slash = path.LastIndexOf('/', slash - 1))
                {
                    var folder = path.Substring(0, slash);
                    counts.TryGetValue(folder, out var count);
                    counts[folder] = count + 1;
                }
            }
            return counts;
        }

        /// <summary>The display name of the package an asset or folder is in ("Chisel 3D Level Editor"), null outside packages</summary>
        public static string PackageNameOf(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                return null;
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
            return info != null && !string.IsNullOrEmpty(info.displayName) ? info.displayName : null;
        }

        /// <summary>A folder's name as shown: a package's own folder by its display name</summary>
        public static string FolderLabel(string path, string name)
        {
            var slashes = 0;
            foreach (var c in path)
                if (c == '/') slashes++;
            return slashes == 1 ? PackageNameOf(path) ?? name : name;
        }

        static int RootOrder(string name) { return name == "Assets" ? 0 : name == "Packages" ? 1 : 2; }

        static void SortFolders(List<Folder> folders)
        {
            folders.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            foreach (var folder in folders)
                SortFolders(folder.children);
        }

        /// <summary>The labels the visible items carry, by name ignoring case, with how many carry each, sorted</summary>
        public List<KeyValuePair<string, int>> Labels()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var names  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                if (!IsVisible(item))
                    continue;
                foreach (var label in item.labels)
                {
                    counts.TryGetValue(label, out var count);
                    counts[label] = count + 1;
                    if (!names.ContainsKey(label))
                        names[label] = label;
                }
            }
            var result = new List<KeyValuePair<string, int>>();
            foreach (var pair in counts)
                result.Add(new KeyValuePair<string, int>(names[pair.Key], pair.Value));
            result.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        /// <summary>A label as it should be added: an existing one of the same name in any case is reused</summary>
        public string ExistingLabelFor(string label)
        {
            foreach (var pair in Labels())
            {
                if (string.Equals(pair.Key, label, StringComparison.OrdinalIgnoreCase))
                    return pair.Key;
            }
            return label;
        }
    }
}
