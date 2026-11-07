using UnityEngine;
using UnityEditor;
using Chisel.Core;

namespace Chisel.Editors
{
	[CustomEditor(typeof(ChiselProjectSettings))]
	public class ChiselProjectSettingsEditor : Editor
	{
		class SettingsContent
		{
			public readonly static GUIContent kMaterialSection = EditorGUIUtility.TrTextContent("Materials");

			public readonly static GUIContent kDefaultMaterialsSection	= EditorGUIUtility.TrTextContent("Default");

			public readonly static GUIContent kDefaultFloorMaterial		= EditorGUIUtility.TrTextContent("Floor");
			public readonly static GUIContent kDefaultRiserMaterial		= EditorGUIUtility.TrTextContent("Riser");
			public readonly static GUIContent kDefaultTreadMaterial		= EditorGUIUtility.TrTextContent("Tread");
			public readonly static GUIContent kDefaultStepNoseMaterial	= EditorGUIUtility.TrTextContent("Step Nose");
			public readonly static GUIContent kDefaultStepSidesMaterial	= EditorGUIUtility.TrTextContent("Step Sides");
			public readonly static GUIContent kDefaultColumnMaterial	= EditorGUIUtility.TrTextContent("Column");
			public readonly static GUIContent kDefaultTreadBottomMaterial = EditorGUIUtility.TrTextContent("Tread Bottom");
			public readonly static GUIContent kDefaultWallMaterial		= EditorGUIUtility.TrTextContent("Wall");
			public readonly static GUIContent kDefaultPhysicMaterial	= EditorGUIUtility.TrTextContent("Physics");
			public readonly static GUIContent kForceShadowOnlyMaterial	= EditorGUIUtility.TrTextContent("Force Shadow-Only");


			public readonly static GUIContent kVisualizationSurfaceMaterialsSection = EditorGUIUtility.TrTextContent("Surface Visualization");

			public readonly static GUIContent kHiddenSurfacesMaterial		   = EditorGUIUtility.TrTextContent("User Hidden");
			public readonly static GUIContent kDiscardedSurfacesMaterial	   = EditorGUIUtility.TrTextContent("Removed By Chisel");
			public readonly static GUIContent kShadowCastingSurfacesMaterial   = EditorGUIUtility.TrTextContent("Shadow-Casting");
			public readonly static GUIContent kShadowOnlySurfacesMaterial	   = EditorGUIUtility.TrTextContent("Shadow-Only");
			public readonly static GUIContent kShadowReceivingSurfacesMaterial = EditorGUIUtility.TrTextContent("Shadow-Receiving");
			public readonly static GUIContent kCollisionSurfacesMaterial	   = EditorGUIUtility.TrTextContent("Collision");

			public readonly static GUIContent kHotspotSection      = EditorGUIUtility.TrTextContent("Hotspot Mapping");
			public readonly static GUIContent kHotspotMaxRepeats   = EditorGUIUtility.TrTextContent("Max repeats", "The most repeats a border strip, a repeating tile or a bordered tile's interior lays on one surface; more and it stretches instead. A layout can have its own");
			public readonly static GUIContent kHotspotMaxTriangles = EditorGUIUtility.TrTextContent("Max triangles", "The most triangles a surface's cut may draw; more and its interior's repeats are left out, then the cut. A layout can have its own");

			public readonly static GUIContent kHiddenFoldersSection = EditorGUIUtility.TrTextContent("Hidden folders", "Folders whose materials the Surface Browser leaves out, with everything under them");
		};

		SerializedProperty m_DefaultFloorMaterialProp;
		SerializedProperty m_DefaultRiserMaterialProp;
		SerializedProperty m_DefaultTreadMaterialProp;
		SerializedProperty m_DefaultStepNoseMaterialProp;
		SerializedProperty m_DefaultStepSidesMaterialProp;
		SerializedProperty m_DefaultColumnMaterialProp;
		SerializedProperty m_DefaultTreadBottomMaterialProp;
		SerializedProperty m_DefaultWallMaterialProp;
		SerializedProperty m_DefaultPhysicMaterialProp;
		SerializedProperty m_DefaultForceShadowOnlyMaterialProp;

		SerializedProperty m_HiddenSurfacesMaterialProp;
		SerializedProperty m_ShadowCastingSurfacesMaterialProp;
		SerializedProperty m_ShadowOnlySurfacesMaterialProp;
		SerializedProperty m_ShadowReceivingSurfacesMaterialProp;
		SerializedProperty m_CollisionOnlySurfacesMaterialProp;
		SerializedProperty m_DiscardedSurfacesMaterialProp;

		void OnEnable()
		{
			if (serializedObject == null ||
				serializedObject.targetObject == null)
				return;

			var materialsProp = serializedObject.FindProperty(ChiselProjectSettings.kMaterialsName);
			if (materialsProp == null)
				return;
			 
			m_DefaultFloorMaterialProp  = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kDefaultFloorMaterialName);
			m_DefaultRiserMaterialProp  = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kDefaultRiserMaterialName);
			m_DefaultTreadMaterialProp  = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kDefaultTreadMaterialName);
			m_DefaultStepNoseMaterialProp = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kDefaultStepNoseMaterialName);
			m_DefaultStepSidesMaterialProp = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kDefaultStepSidesMaterialName);
			m_DefaultColumnMaterialProp = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kDefaultColumnMaterialName);
			m_DefaultTreadBottomMaterialProp = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kDefaultTreadBottomMaterialName);
			m_DefaultWallMaterialProp   = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kDefaultWallMaterialName);
			m_DefaultPhysicMaterialProp = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kDefaultPhysicMaterialName);
			m_DefaultForceShadowOnlyMaterialProp = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kDefaultForceShadowOnlyMaterialName);
			

			m_HiddenSurfacesMaterialProp          = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kUserHiddenSurfacesMaterialName);
			m_DiscardedSurfacesMaterialProp       = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kDiscardedSurfacesMaterialName);
			m_ShadowCastingSurfacesMaterialProp   = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kShadowCastingSurfacesMaterialName);
			m_ShadowOnlySurfacesMaterialProp      = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kShadowOnlySurfacesMaterialName);
			m_ShadowReceivingSurfacesMaterialProp = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kShadowReceivingSurfacesMaterialName);
			m_CollisionOnlySurfacesMaterialProp   = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kCollisionSurfacesMaterialName);
		}
		 
		public override void OnInspectorGUI() 
		{
			if (serializedObject == null)
				return;
			serializedObject.Update();
			switch (section)
			{
				case Section.Materials:     MaterialsGUI(); break;
				case Section.Hotspots:      HotspotsGUI(); break;
				case Section.SurfaceBrowser:
					GUILayout.Label(SettingsContent.kHiddenFoldersSection, EditorStyles.boldLabel);
					HiddenFoldersGUI();
					break;
			}
			if (serializedObject.ApplyModifiedProperties())
			{
				ChiselProjectSettings.instance.Save();
			}
		}

		void MaterialsGUI()
		{
			{
				EditorGUILayout.LabelField(SettingsContent.kDefaultMaterialsSection, EditorStyles.boldLabel);
				using (new EditorGUI.IndentLevelScope(1))
				{
					EditorGUILayout.PropertyField(m_DefaultFloorMaterialProp, SettingsContent.kDefaultFloorMaterial);
					EditorGUILayout.PropertyField(m_DefaultRiserMaterialProp, SettingsContent.kDefaultRiserMaterial);
					EditorGUILayout.PropertyField(m_DefaultTreadMaterialProp, SettingsContent.kDefaultTreadMaterial);
					EditorGUILayout.PropertyField(m_DefaultStepNoseMaterialProp, SettingsContent.kDefaultStepNoseMaterial);
					EditorGUILayout.PropertyField(m_DefaultStepSidesMaterialProp, SettingsContent.kDefaultStepSidesMaterial);
					EditorGUILayout.PropertyField(m_DefaultColumnMaterialProp, SettingsContent.kDefaultColumnMaterial);
					EditorGUILayout.PropertyField(m_DefaultTreadBottomMaterialProp, SettingsContent.kDefaultTreadBottomMaterial);
					EditorGUILayout.PropertyField(m_DefaultWallMaterialProp, SettingsContent.kDefaultWallMaterial);
					EditorGUILayout.PropertyField(m_DefaultPhysicMaterialProp, SettingsContent.kDefaultPhysicMaterial);
					EditorGUILayout.PropertyField(m_DefaultForceShadowOnlyMaterialProp, SettingsContent.kForceShadowOnlyMaterial);	
				}
				EditorGUILayout.Space();

				EditorGUILayout.LabelField(SettingsContent.kVisualizationSurfaceMaterialsSection, EditorStyles.boldLabel);
				using (new EditorGUI.IndentLevelScope(1))
				{
					EditorGUILayout.PropertyField(m_HiddenSurfacesMaterialProp, SettingsContent.kHiddenSurfacesMaterial);
					EditorGUILayout.PropertyField(m_DiscardedSurfacesMaterialProp, SettingsContent.kDiscardedSurfacesMaterial);

					EditorGUILayout.PropertyField(m_ShadowCastingSurfacesMaterialProp, SettingsContent.kShadowCastingSurfacesMaterial);
					EditorGUILayout.PropertyField(m_ShadowOnlySurfacesMaterialProp, SettingsContent.kShadowOnlySurfacesMaterial);
					EditorGUILayout.PropertyField(m_ShadowReceivingSurfacesMaterialProp, SettingsContent.kShadowReceivingSurfacesMaterial);
					EditorGUILayout.PropertyField(m_CollisionOnlySurfacesMaterialProp, SettingsContent.kCollisionSurfacesMaterial);
				}
				EditorGUILayout.Space();

				if (GUILayout.Button("Reset materials to defaults"))
				{
					ChiselProjectSettings.instance.ResetMaterials();
				}
			}
		}

		static void HotspotsGUI()
		{
			{
				EditorGUI.BeginChangeCheck();
				var maxRepeats   = EditorGUILayout.IntField(SettingsContent.kHotspotMaxRepeats,   ChiselProjectSettings.HotspotMaxRepeats);
				var maxTriangles = EditorGUILayout.IntField(SettingsContent.kHotspotMaxTriangles, ChiselProjectSettings.HotspotMaxTriangles);
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(ChiselProjectSettings.instance, "Hotspot Budgets");
					ChiselProjectSettings.HotspotMaxRepeats   = maxRepeats;
					ChiselProjectSettings.HotspotMaxTriangles = maxTriangles;
					ChiselProjectSettings.instance.Save();
					ChiselHotspotStore.SettingsChanged();
				}
			}
		}

		readonly System.Collections.Generic.HashSet<string> expandedFolders = new() { "Assets" };

		// The project's folders as a tree, each with a toggle that hides it; a folder hidden through its parent shows
		// as hidden and can only be shown through its parent
		void HiddenFoldersGUI()
		{
			FolderRowGUI("Assets", "Assets", 0, false);
			FolderRowGUI("Packages", "Packages", 0, false);
		}

		void FolderRowGUI(string path, string name, int depth, bool parentHidden)
		{
			var subFolders = AssetDatabase.GetSubFolders(path);
			var hiddenItself = !parentHidden && ChiselProjectSettings.IsHidden(path);
			var rect = EditorGUILayout.GetControlRect();
			var foldRect = new Rect(rect.x + depth * 14, rect.y, 14, rect.height);
			var expanded = expandedFolders.Contains(path);
			if (subFolders.Length > 0)
			{
				var newExpanded = EditorGUI.Foldout(foldRect, expanded, GUIContent.none, true);
				if (newExpanded != expanded)
				{
					if (newExpanded) expandedFolders.Add(path); else expandedFolders.Remove(path);
					expanded = newExpanded;
				}
			}
			var toggleRect = new Rect(foldRect.xMax, rect.y, 18, rect.height);
			using (new EditorGUI.DisabledScope(parentHidden))
			{
				var hide = EditorGUI.Toggle(toggleRect, new GUIContent(string.Empty, "Hide this folder and everything in it"), parentHidden || hiddenItself);
				if (!parentHidden && hide != hiddenItself)
				{
					if (hide) ChiselProjectSettings.HideFolder(path);
					else ChiselProjectSettings.UnhideFolder(path);
				}
			}
			GUI.Label(new Rect(toggleRect.xMax + 2, rect.y, rect.xMax - toggleRect.xMax - 2, rect.height), new GUIContent(name, path));

			if (!expanded)
				return;
			var hidden = parentHidden || ChiselProjectSettings.IsHidden(path);
			foreach (var sub in subFolders)
				FolderRowGUI(sub, sub.Substring(sub.LastIndexOf('/') + 1), depth + 1, hidden);
		}

		public enum Section { Materials, Hotspots, SurfaceBrowser }
		public Section section;

		public const string kRootPath           = "Project/Chisel";
		public const string kMaterialsPath      = kRootPath + "/Materials";
		public const string kHotspotsPath       = kRootPath + "/Hotspot Mapping";
		public const string kSurfaceBrowserPath = kRootPath + "/Surface Browser";

		static SettingsProvider SectionProvider(string path, Section section)
		{
			return new AssetSettingsProvider(path, () =>
			{
				var editor = (ChiselProjectSettingsEditor)CreateEditor(ChiselProjectSettings.instance);
				editor.section = section;
				return editor;
			}, SettingsProvider.GetSearchKeywordsFromGUIContentProperties<SettingsContent>());
		}

		[SettingsProvider] static SettingsProvider CreateMaterialsProvider()      { return SectionProvider(kMaterialsPath, Section.Materials); }
		[SettingsProvider] static SettingsProvider CreateHotspotsProvider()       { return SectionProvider(kHotspotsPath, Section.Hotspots); }
		[SettingsProvider] static SettingsProvider CreateSurfaceBrowserProvider() { return SectionProvider(kSurfaceBrowserPath, Section.SurfaceBrowser); }

		[SettingsProvider]
		static SettingsProvider CreateRootProvider()
		{
			return new SettingsProvider(kRootPath, SettingsScope.Project)
			{
				guiHandler = _ => EditorGUILayout.LabelField("Chisel's project settings are on the pages under this one.", EditorStyles.wordWrappedLabel)
			};
		}
	}
}