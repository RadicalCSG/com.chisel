using System;

using UnityEngine;

namespace Chisel.Core
{
#if UNITY_EDITOR
	[Serializable]
	[UnityEditor.FilePath(kSettingsFilePath, UnityEditor.FilePathAttribute.Location.ProjectFolder)]
#endif
	public sealed class ChiselProjectSettings
#if UNITY_EDITOR
		: UnityEditor.ScriptableSingleton<ChiselProjectSettings>
#else
		: ScriptableObject
#endif
	{
		public const string kMaterialsName    = nameof(materials);
		public const string kContentsListName = nameof(contentsList);

		[SerializeField] private ChiselPipelineMaterialsSet materials;

		// The project's contents list asset. This settings object only exists in the editor; a player gets the
		// list from its preloaded assets instead (see ChiselContentsList.Instance).
		[SerializeField] private ChiselContentsList contentsList;

		// Hotspot mapping's budgets for one surface, where its layout has none of its own (ChiselHotspotLayout.maxRepeats,
		// maxTriangles). A player has no project settings: it takes these defaults.
		public const int kDefaultHotspotMaxRepeats   = 4096;
		public const int kDefaultHotspotMaxTriangles = 65536;
		[SerializeField] private int hotspotMaxRepeats   = kDefaultHotspotMaxRepeats;
		[SerializeField] private int hotspotMaxTriangles = kDefaultHotspotMaxTriangles;
		public static int HotspotMaxRepeats
		{
			get { return instance.hotspotMaxRepeats > 0 ? instance.hotspotMaxRepeats : kDefaultHotspotMaxRepeats; }
			set { instance.hotspotMaxRepeats = Math.Max(1, value); }
		}
		public static int HotspotMaxTriangles
		{
			get { return instance.hotspotMaxTriangles > 0 ? instance.hotspotMaxTriangles : kDefaultHotspotMaxTriangles; }
			set { instance.hotspotMaxTriangles = Math.Max(1, value); }
		}
		// Folders whose materials and decals the surface tools leave out (models, characters, particles), and everything
		// under them; shared by everyone on the project. Paths as the asset database has them, without a trailing slash
		[SerializeField] private System.Collections.Generic.List<string> hiddenFolders = new();
		public static System.Collections.Generic.IReadOnlyList<string> HiddenFolders => instance.hiddenFolders ??= new();

		// A folder path, without a trailing slash and with forward slashes
		static string FolderKey(string path)
		{
			if (string.IsNullOrEmpty(path))
				return string.Empty;
			return path.Replace('\\', '/').TrimEnd('/');
		}

		static bool IsUnder(string path, string folder)
		{
			return path.Length > folder.Length ? path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase)
											   : string.Equals(path, folder, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Whether a folder or asset is in a hidden folder, or is one</summary>
		public static bool IsHidden(string path)
		{
			var key = FolderKey(path);
			if (key.Length == 0)
				return false;
			foreach (var folder in HiddenFolders)
			{
				if (IsUnder(key, folder))
					return true;
			}
			return false;
		}

		/// <summary>Hides a folder and everything under it; folders hidden under it are then hidden through it</summary>
		public static void HideFolder(string path)
		{
			var key = FolderKey(path);
			if (key.Length == 0 || IsHidden(key))
				return;
			RecordChange("Hide Folder");
			var folders = instance.hiddenFolders ??= new();
			folders.RemoveAll(folder => IsUnder(folder, key));
			folders.Add(key);
			folders.Sort(StringComparer.OrdinalIgnoreCase);
			SaveChange();
		}

		/// <summary>Shows a hidden folder again; a folder hidden through its parent is shown through its parent</summary>
		public static bool UnhideFolder(string path)
		{
			var key = FolderKey(path);
			var folders = instance.hiddenFolders ??= new();
			var index = folders.FindIndex(folder => string.Equals(folder, key, StringComparison.OrdinalIgnoreCase));
			if (index < 0)
				return false;
			RecordChange("Unhide Folder");
			folders.RemoveAt(index);
			SaveChange();
			return true;
		}

		// the folders hidden by default (Unity's packages, Chisel's debug materials) were added once; after that only the
		// user changes what is hidden
		[SerializeField] private bool defaultHiddenFoldersAdded;

		/// <summary>Hides the folders, once for the project; false when that was done before</summary>
		public static bool AddDefaultHiddenFolders(System.Collections.Generic.IEnumerable<string> paths)
		{
			if (instance.defaultHiddenFoldersAdded)
				return false;
			var folders = instance.hiddenFolders ??= new();
			foreach (var path in paths)
			{
				var key = FolderKey(path);
				if (key.Length == 0 || IsHidden(key))
					continue;
				folders.RemoveAll(folder => IsUnder(folder, key));
				folders.Add(key);
			}
			folders.Sort(StringComparer.OrdinalIgnoreCase);
			instance.defaultHiddenFoldersAdded = true;
			SaveChange();
			return true;
		}

		static void RecordChange(string name)
		{
#if UNITY_EDITOR
			UnityEditor.Undo.RecordObject(instance, name);
#endif
		}

		static void SaveChange()
		{
#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(instance);
			instance.Save();
#endif
		}

		public static ChiselContentsList ContentsList
		{
			get { return instance.contentsList; }
			set
			{
				if (instance.contentsList == value)
					return;
				instance.contentsList = value;
#if UNITY_EDITOR
				instance.Save();
#endif
			}
		}
		public static ChiselPipelineMaterialsSet Materials
		{
			get
			{
				instance.materials ??= ChiselDefaultMaterials.Defaults;
				return instance.materials;
			}
			set { instance.materials = value; }
		}


		public static Material DefaultMaterial                 { get { return DefaultWallMaterial; } }

		public static Material DefaultFloorMaterial		       { get { return Materials.defaultFloorMaterial; } set { Materials.defaultFloorMaterial = value; } }
        public static Material DefaultRiserMaterial		       { get { return Materials.defaultRiserMaterial; } set { Materials.defaultRiserMaterial = value; } }
		public static Material DefaultTreadMaterial		       { get { return Materials.defaultTreadMaterial; } set { Materials.defaultTreadMaterial = value; } }
		// a project's settings saved before these existed have none: they take the package's own
		public static Material DefaultStepNoseMaterial	       { get { return Materials.defaultStepNoseMaterial ? Materials.defaultStepNoseMaterial : ChiselDefaultMaterials.Defaults?.defaultStepNoseMaterial; } set { Materials.defaultStepNoseMaterial = value; } }
		public static Material DefaultStepSidesMaterial	       { get { return Materials.defaultStepSidesMaterial ? Materials.defaultStepSidesMaterial : ChiselDefaultMaterials.Defaults?.defaultStepSidesMaterial; } set { Materials.defaultStepSidesMaterial = value; } }
		public static Material DefaultColumnMaterial	       { get { return Materials.defaultColumnMaterial ? Materials.defaultColumnMaterial : ChiselDefaultMaterials.Defaults?.defaultColumnMaterial; } set { Materials.defaultColumnMaterial = value; } }
		public static Material DefaultTreadBottomMaterial      { get { return Materials.defaultTreadBottomMaterial ? Materials.defaultTreadBottomMaterial : ChiselDefaultMaterials.Defaults?.defaultTreadBottomMaterial; } set { Materials.defaultTreadBottomMaterial = value; } }
		public static Material DefaultWallMaterial		       { get { return Materials.defaultWallMaterial; } set { Materials.defaultWallMaterial = value; } }
		public static PhysicsMaterial DefaultPhysicsMaterial   { get { return Materials.defaultPhysicsMaterial; } set { Materials.defaultPhysicsMaterial = value; } }

		public static Material UserHiddenSurfacesMaterial      { get { return Materials.userHiddenSurfacesMaterial; } set { Materials.userHiddenSurfacesMaterial = value; } }
		public static Material DiscardedSurfacesMaterial	   { get { return Materials.discardedSurfacesMaterial; } set { Materials.discardedSurfacesMaterial = value; } }
		public static Material ShadowCastingSurfacesMaterial   { get { return Materials.shadowCastingSurfacesMaterial; } set { Materials.shadowCastingSurfacesMaterial = value; } }
		public static Material ShadowOnlySurfacesMaterial      { get { return Materials.shadowOnlySurfacesMaterial; } set { Materials.shadowOnlySurfacesMaterial = value; } }
		public static Material ForceShadowOnlySurfacesMaterial { get { return Materials.forceShadowOnlySurfacesMaterial; } set { Materials.forceShadowOnlySurfacesMaterial = value; } }
		public static Material ShadowReceivingSurfacesMaterial { get { return Materials.shadowReceivingSurfacesMaterial; } set { Materials.shadowReceivingSurfacesMaterial = value; } }
		public static Material CollisionSurfacesMaterial       { get { return Materials.collisionSurfacesMaterial; } set { Materials.collisionSurfacesMaterial = value; } }
		public static Material[] DebugVisualizationMaterials   { get { return Materials.DebugVisualizationMaterials; } }

		public void ResetMaterials() 
		{ 
			materials = ChiselDefaultMaterials.Defaults;
#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(this);
#endif
		}

#if UNITY_EDITOR
		public const string kSettingsFilePath = "ProjectSettings/Packages/com.chisel/ChiselSettings.asset";
		public void Reset() { ResetMaterials(); }
		public void OnValidate() { if (materials == null) { Reset(); } }
		public void Save() { Save(true); }
#else
		private static ChiselProjectSettings s_Instance;

		public static ChiselProjectSettings instance
		{
			get
			{
				if ((UnityEngine.Object)s_Instance == (UnityEngine.Object)null)
				{
					s_Instance = ScriptableObject.CreateInstance<ChiselProjectSettings>();
					s_Instance.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
					s_Instance.materials ??= ChiselDefaultMaterials.Defaults;
				}

				return s_Instance;
			}
		}
#endif
	}
}
