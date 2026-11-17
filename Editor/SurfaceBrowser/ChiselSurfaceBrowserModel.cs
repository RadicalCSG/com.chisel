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
            public bool     preset;    // a generator material preset, not a material

            // What the model learned of it (Learn), kept until it is marked stale: until it is known it isn't counted,
            // and a filter that asks about it leaves it out
            public bool     known;
            public bool     hidden;
            public bool     decal;
            public bool     setUp;
            public ulong    traitBits;   // by the index of the trait in traits
            internal bool   queued;
            internal bool   removed;

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

        // NotSetUp: no Chisel settings of its own yet, so surfaces using it take Chisel's defaults. In the presets' mode
        // only All, Favorites and RecentlyUsed apply
        public enum QuickAccess { All, Favorites, RecentlyUsed, InThisScene, UnusedInThisScene, Decals, NotSetUp }

        /// <summary>Whether a quick access list applies to presets (the others are about materials)</summary>
        public static bool AppliesToPresets(QuickAccess quick)
        {
            return quick == QuickAccess.All || quick == QuickAccess.Favorites || quick == QuickAccess.RecentlyUsed;
        }

        /// <summary>What is listed: generator material presets, else materials. Each mode leaves the other's items out
        /// of everything: the grid, counts, folders and labels</summary>
        public bool presets;

        bool InMode(Item item) { return item.preset == presets; }

        public Func<Item, bool> isDecal = _ => false;
        public Func<Item, bool> isSetUp = _ => true;

        // Something a material has or not (paintable, a skybox, hotspots); a filter asks for it, or for its absence
        public sealed class Trait
        {
            public string           name;
            public Func<Item, bool> has;
            // a plain trait: none of these (learned from what was learned of them, not asked again)
            internal List<Trait>    noneOf;
        }
        public enum TraitFilter { Any, With, Without }

        /// <summary>A plain material: one with none of the other traits</summary>
        public static Trait PlainTrait(IReadOnlyList<Trait> others)
        {
            var copy = new List<Trait>(others);
            return new Trait
            {
                name   = "Plain",
                has    = item => copy.TrueForAll(trait => !trait.has(item)),
                noneOf = copy
            };
        }

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

        // traits are a material's: a preset has none to filter on. One not learned yet passes only when no filter asks.
        bool PassesTraits(Item item)
        {
            if (item.preset)
                return true;
            for (int t = 0; t < traits.Count; t++)
            {
                var filter = FilterOf(traits[t].name);
                if (filter == TraitFilter.Any)
                    continue;
                if (!item.known || ((item.traitBits >> t & 1) != 0) != (filter == TraitFilter.With))
                    return false;
            }
            return true;
        }

        /// <summary>How many of the visible materials learned have a trait</summary>
        public int CountWith(Trait trait)
        {
            if (presets)
                return 0;
            var index = traits.IndexOf(trait);
            return index >= 0 && index < traitCounts.Count ? traitCounts[index] : 0;
        }

        // what quick access asks of an item, given from outside: per user and per scene
        public Func<Item, bool> isFavorite   = _ => false;
        public Func<Item, bool> isInScene    = _ => false;
        public Func<IReadOnlyList<string>> recent = () => Array.Empty<string>();
        public Func<string, bool> isHidden   = ChiselProjectSettings.IsHidden;
        // learns what an item's asset holds itself (its labels), before the rest is learned of it
        public Action<Item> learnAsset;

        readonly List<Item> items = new();
        readonly Dictionary<string, Item> byGuid = new();
        public IReadOnlyList<Item> Items => items;

        public Item ItemOf(string guid) { return guid != null && byGuid.TryGetValue(guid, out var item) ? item : null; }

        /// <summary>The items listed: each is learned afterwards, a batch at a time (LearnSome)</summary>
        public void SetItems(IEnumerable<Item> newItems)
        {
            foreach (var item in items)
                item.removed = true;
            items.Clear();
            byGuid.Clear();
            queue.Clear();
            hiddenByFolder.Clear();
            ClearCounts();
            if (newItems == null)
                return;
            foreach (var item in newItems)
            {
                item.known = item.queued = item.removed = false;
                items.Add(item);
                byGuid[item.guid] = item;
                MarkStale(item);
            }
        }

        /// <summary>An item added, or one listed already given its path, name and kind again (moved or renamed): it is
        /// learned again</summary>
        public void AddOrUpdate(Item item)
        {
            if (byGuid.TryGetValue(item.guid, out var listed))
            {
                Forget(listed);
                listed.path   = item.path;
                listed.name   = item.name;
                listed.preset = item.preset;
                MarkStale(listed);
                return;
            }
            item.known = item.queued = item.removed = false;
            items.Add(item);
            byGuid[item.guid] = item;
            MarkStale(item);
        }

        public void Remove(string guid)
        {
            if (!byGuid.TryGetValue(guid, out var item))
                return;
            Forget(item);
            item.removed = true;
            byGuid.Remove(guid);
            items.Remove(item);
        }

        public bool IsVisible(Item item) { return item.known ? !item.hidden : !HiddenPath(item); }

        // whether hidden folders hide an item, asked once per folder: hidden folders hide everything in them
        readonly Dictionary<string, bool> hiddenByFolder = new(StringComparer.Ordinal);

        bool HiddenPath(Item item)
        {
            var folder = item.Folder;
            if (!hiddenByFolder.TryGetValue(folder, out var hidden))
                hiddenByFolder[folder] = hidden = isHidden(item.path);
            return hidden;
        }

        #region Learning
        // the items to learn (again), in the order they were marked
        readonly Queue<Item> queue = new();
        // of the visible items learned: per mode (materials, presets) how many, and of the materials how many have each
        // trait, are decals and are not set up
        readonly int[] shownCounts = new int[2];
        readonly List<int> traitCounts = new();
        int decalCount, notSetUpCount;
        // the quick lists asked of the outside (favorites, recent, in the scene), counted once until they change
        readonly Dictionary<(bool, QuickAccess), int> listCounts = new();

        /// <summary>Whether items are waiting to be learned</summary>
        public bool Learning => queue.Count > 0;

        public void MarkStale(Item item)
        {
            if (item == null || item.queued || item.removed)
                return;
            item.queued = true;
            queue.Enqueue(item);
        }

        public void MarkStale(string guid) { MarkStale(ItemOf(guid)); }

        /// <summary>Every item is learned again, its folder's being hidden too</summary>
        public void MarkAllStale()
        {
            hiddenByFolder.Clear();
            foreach (var item in items)
                MarkStale(item);
        }

        /// <summary>The favorites, recently used or what the scene uses changed: they are counted again when asked</summary>
        public void QuickListsChanged() { listCounts.Clear(); }

        /// <summary>Learns items until the time (in seconds) is up, at least one; how many it learned</summary>
        public int LearnSome(double seconds)
        {
            var clock   = System.Diagnostics.Stopwatch.StartNew();
            var learned = 0;
            while (queue.Count > 0 && (learned == 0 || clock.Elapsed.TotalSeconds < seconds))
            {
                var item = queue.Dequeue();
                item.queued = false;
                if (item.removed)
                    continue;
                Learn(item);
                learned++;
            }
            return learned;
        }

        /// <summary>Learns every item again, all at once</summary>
        public void LearnAll()
        {
            MarkAllStale();
            while (queue.Count > 0)
                LearnSome(double.MaxValue);
        }

        void Learn(Item item)
        {
            Forget(item);
            if (traitCounts.Count != traits.Count)
            {
                // the traits changed: what was learned of them is not what they are now
                ClearCounts();
                foreach (var other in items)
                {
                    if (other != item && other.known)
                    {
                        other.known = false;
                        MarkStale(other);
                    }
                }
            }
            learnAsset?.Invoke(item);
            item.hidden    = HiddenPath(item);
            item.decal     = !item.preset && isDecal(item);
            item.setUp     = item.preset || isSetUp(item);
            item.traitBits = 0;
            if (!item.preset)
            {
                for (int t = 0; t < traits.Count; t++)
                {
                    if (traits[t].noneOf == null && traits[t].has(item))
                        item.traitBits |= 1ul << t;
                }
                for (int t = 0; t < traits.Count; t++)
                {
                    var noneOf = traits[t].noneOf;
                    if (noneOf == null)
                        continue;
                    var none = true;
                    foreach (var other in noneOf)
                    {
                        var o = traits.IndexOf(other);
                        none &= o < 0 || (item.traitBits >> o & 1) == 0;
                    }
                    if (none)
                        item.traitBits |= 1ul << t;
                }
            }
            item.known = true;
            Tally(item, 1);
            listCounts.Clear();
        }

        // takes what was learned of an item out of the counts
        void Forget(Item item)
        {
            if (!item.known)
                return;
            Tally(item, -1);
            item.known = false;
            listCounts.Clear();
        }

        void Tally(Item item, int sign)
        {
            if (item.hidden)
                return;
            shownCounts[item.preset ? 1 : 0] += sign;
            if (item.preset)
                return;
            for (int t = 0; t < traitCounts.Count; t++)
            {
                if ((item.traitBits >> t & 1) != 0)
                    traitCounts[t] += sign;
            }
            if (item.decal)  decalCount    += sign;
            if (!item.setUp) notSetUpCount += sign;
        }

        void ClearCounts()
        {
            shownCounts[0] = shownCounts[1] = 0;
            traitCounts.Clear();
            for (int t = 0; t < traits.Count; t++)
                traitCounts.Add(0);
            decalCount = notSetUpCount = 0;
            listCounts.Clear();
        }
        #endregion

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
                case QuickAccess.All:          return true;
            }
            // the others are about materials
            if (item.preset)
                return false;
            switch (quick)
            {
                case QuickAccess.InThisScene:  return isInScene(item);
                case QuickAccess.UnusedInThisScene: return !isInScene(item);
                case QuickAccess.Decals:       return item.known && item.decal;
                case QuickAccess.NotSetUp:     return item.known && !item.setUp;
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
                if (!IsVisible(item) || !InMode(item) || !InQuickAccess(item, quick) || !InFolders(item, folders) ||
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

        /// <summary>How many visible items learned are in a quick access list: kept as they are learned, the lists asked
        /// of the outside counted once until they change (QuickListsChanged)</summary>
        public int Count(QuickAccess quick)
        {
            var shown = shownCounts[presets ? 1 : 0];
            switch (quick)
            {
                case QuickAccess.All:      return shown;
                case QuickAccess.Decals:   return presets ? 0 : decalCount;
                case QuickAccess.NotSetUp: return presets ? 0 : notSetUpCount;
                case QuickAccess.UnusedInThisScene:
                    return presets ? 0 : shown - Count(QuickAccess.InThisScene);
            }
            if (listCounts.TryGetValue((presets, quick), out var cached))
                return cached;
            var count = 0;
            foreach (var item in items)
            {
                if (item.known && !item.hidden && InMode(item) && InQuickAccess(item, quick))
                    count++;
            }
            listCounts[(presets, quick)] = count;
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
                if (!InMode(item))
                    continue;
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
                if (!IsVisible(item) || !InMode(item))
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
