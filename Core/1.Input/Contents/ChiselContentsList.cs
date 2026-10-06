using System;

using UnityEngine;

namespace Chisel.Core
{
	public sealed class ChiselContentsList : ScriptableObject
	{
		public const int    kSolidIndex  = 0;
		public const string kSolidName   = "Solid";
		// One byte per brush in the compact tree, and a handful of types is all this is for.
		public const int    kMaxEntries  = 32;

		const string kDefaultName = "Default Contents";

		[SerializeField] string[] names = new[] { kSolidName };

		public int Count
		{
			get
			{
				if (names == null || names.Length < 1)
					return 1;
				return Mathf.Min(names.Length, kMaxEntries);
			}
		}

		public string NameOf(int index)
		{
			if (index == kSolidIndex)
				return kSolidName;
			if (names == null || index < 0 || index >= names.Length)
				return $"<missing {index}>";
			return string.IsNullOrEmpty(names[index]) ? $"<unnamed {index}>" : names[index];
		}

		// The index of the entry with this name, ignoring case, or -1
		public int IndexOf(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				return -1;
			name = name.Trim();
			for (int index = 0; index < Count; index++)
			{
				if (string.Equals(NameOf(index), name, StringComparison.OrdinalIgnoreCase))
					return index;
			}
			return -1;
		}

		// A brush whose index is past the end of the list builds as Solid, and its inspector says so.
		public bool IsValidIndex(int index) => index >= 0 && index < Count;
		public int  Resolve(int index)      => IsValidIndex(index) ? index : kSolidIndex;

		// Solid always wins a coplanar tie; between two other types the earlier entry wins, which with
		// indices is simply the lower one.
		public static bool WinsTie(int contents, int against)
		{
			if (contents == against)        return false;
			if (contents == kSolidIndex)    return true;
			if (against  == kSolidIndex)    return false;
			return contents < against;
		}

		public string[] Names
		{
			get
			{
				var result = new string[Count];
				for (int i = 0; i < result.Length; i++)
					result[i] = NameOf(i);
				return result;
			}
		}

		void OnEnable()
		{
#if !UNITY_EDITOR
			if (s_Instance == null)
				s_Instance = this;
#endif
			Validate();
		}

		void OnValidate() { Validate(); }

		void Validate()
		{
			if (names == null || names.Length < 1)
			{
				names = new[] { kSolidName };
				return;
			}
			// Solid is pinned: it stays first and keeps its name whatever the inspector did.
			if (names[0] != kSolidName)
				names[0] = kSolidName;
			if (names.Length > kMaxEntries)
				Array.Resize(ref names, kMaxEntries);
		}

		// A list that lives in memory only, for tests. The first name is ignored: Solid is pinned.
		internal static ChiselContentsList Create(params string[] names)
		{
			var list = CreateInstance<ChiselContentsList>();
			list.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
			list.name = "Contents";
			list.names = (string[])names.Clone();
			list.Validate();
			return list;
		}

		static ChiselContentsList s_Instance;
		// Whether s_Instance was handed in through the setter (the settings page, or a test) rather than found
		static bool s_Assigned;

		public static ChiselContentsList Instance
		{
			get
			{
				if (s_Instance != null && (s_Assigned || !s_Instance.IsDefault))
					return s_Instance;
#if UNITY_EDITOR
				var projectList = ChiselProjectSettings.ContentsList;
				if (projectList != null)
					return s_Instance = projectList;
#endif
				if (s_Instance == null)
					s_Instance = FindOrCreateDefault();
				return s_Instance;
			}
			set
			{
				s_Instance = value;
				s_Assigned = value != null;
			}
		}

		// Whether this is the stand-in a project without a list gets
		public bool IsDefault => hideFlags.HasFlag(HideFlags.DontSave) && name == kDefaultName;

		// A stand-in made before a script reload is still loaded: reuse it rather than leave another one behind
		static ChiselContentsList FindOrCreateDefault()
		{
			foreach (var list in Resources.FindObjectsOfTypeAll<ChiselContentsList>())
			{
				if (list != null && list.IsDefault)
					return list;
			}
			var created = CreateInstance<ChiselContentsList>();
			created.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
			created.name = kDefaultName;
			return created;
		}

#if UNITY_EDITOR
		public const string kProjectFolder    = "Assets/Chisel";
		public const string kProjectAssetPath = kProjectFolder + "/ChiselContentsList.asset";

		// Editing the list, for the project settings UI and importers. Deliberately narrow: no insert, no
		// move, and removal only from the end, because brushes store indices.
		public bool CanRemoveLast => Count > 1;

		public int Append(string name)
		{
			if (Count >= kMaxEntries)
				return -1;
			var index = Count;
			Array.Resize(ref names, index + 1);
			names[index] = string.IsNullOrWhiteSpace(name) ? $"Contents {index}" : name.Trim();
			UnityEditor.EditorUtility.SetDirty(this);
			return index;
		}

		public bool Rename(int index, string name)
		{
			if (index <= kSolidIndex || index >= Count || string.IsNullOrWhiteSpace(name))
				return false;
			names[index] = name.Trim();
			UnityEditor.EditorUtility.SetDirty(this);
			return true;
		}

		public bool RemoveLast()
		{
			if (!CanRemoveLast)
				return false;
			Array.Resize(ref names, Count - 1);
			UnityEditor.EditorUtility.SetDirty(this);
			return true;
		}

		public static int FindOrAppendInProject(string name)
		{
			var list = ChiselProjectSettings.ContentsList;
			if (list == null)
				list = MakeProjectList(CreateProjectAsset());

			var index = list.IndexOf(name);
			if (index >= 0)
				return index;
			index = list.Append(name);
			if (index < 0)
				return -1;
			UnityEditor.AssetDatabase.SaveAssetIfDirty(list);
			CompactHierarchyManager.ApplyContentsList();
			return index;
		}

		// A new list asset at the default path, or next to it when that is taken
		public static ChiselContentsList CreateProjectAsset()
		{
			if (!UnityEditor.AssetDatabase.IsValidFolder(kProjectFolder))
				UnityEditor.AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(kProjectFolder));
			var list = CreateInstance<ChiselContentsList>();
			UnityEditor.AssetDatabase.CreateAsset(list, UnityEditor.AssetDatabase.GenerateUniqueAssetPath(kProjectAssetPath));
			UnityEditor.AssetDatabase.SaveAssets();
			return list;
		}

		public static ChiselContentsList MakeProjectList(ChiselContentsList list)
		{
			var previous = s_Instance;
			ChiselProjectSettings.ContentsList = list;
			Instance = list;
			if (previous != null && previous != list && previous.IsDefault)
				DestroyImmediate(previous);
			EnsurePreloaded(list);
			CompactHierarchyManager.ApplyContentsList();
			return list;
		}

		public static bool IsPreloaded(ChiselContentsList list)
		{
			if (list == null)
				return false;
			foreach (var asset in UnityEditor.PlayerSettings.GetPreloadedAssets())
			{
				if (asset == list)
					return true;
			}
			return false;
		}

		public static void EnsurePreloaded(ChiselContentsList list)
		{
			if (list == null || IsPreloaded(list))
				return;
			var preloaded = new System.Collections.Generic.List<UnityEngine.Object>(UnityEditor.PlayerSettings.GetPreloadedAssets());
			preloaded.RemoveAll(asset => asset == null || asset is ChiselContentsList);
			preloaded.Add(list);
			UnityEditor.PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
		}
#endif
	}
}
