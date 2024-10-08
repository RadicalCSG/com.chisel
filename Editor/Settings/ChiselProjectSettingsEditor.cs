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
			public static readonly GUIContent MaterialSection = EditorGUIUtility.TrTextContent("Materials");

			public static readonly GUIContent DefaultMaterialsSection = EditorGUIUtility.TrTextContent("Default");

			public static readonly GUIContent DefaultFloorMaterial  = EditorGUIUtility.TrTextContent("Floor");
			public static readonly GUIContent DefaultStepMaterial   = EditorGUIUtility.TrTextContent("Step");
			public static readonly GUIContent DefaultTreadMaterial  = EditorGUIUtility.TrTextContent("Tread");
			public static readonly GUIContent DefaultWallMaterial   = EditorGUIUtility.TrTextContent("Wall");
			public static readonly GUIContent DefaultPhysicMaterial = EditorGUIUtility.TrTextContent("Physics");


			public static readonly GUIContent VisualizationSurfaceMaterialsSection = EditorGUIUtility.TrTextContent("Surface Visualization");

			public static readonly GUIContent HiddenSurfacesMaterial		  = EditorGUIUtility.TrTextContent("User Hidden");
			public static readonly GUIContent DiscardedSurfacesMaterial		  = EditorGUIUtility.TrTextContent("Removed By Chisel");
			public static readonly GUIContent ShadowCastingSurfacesMaterial   = EditorGUIUtility.TrTextContent("Shadow-Casting");
			public static readonly GUIContent ShadowOnlySurfacesMaterial	  = EditorGUIUtility.TrTextContent("Shadow-Only");
			public static readonly GUIContent ShadowReceivingSurfacesMaterial = EditorGUIUtility.TrTextContent("Shadow-Receiving");
			public static readonly GUIContent CollisionSurfacesMaterial		  = EditorGUIUtility.TrTextContent("Collision");
		};

		SerializedProperty m_DefaultFloorMaterialProp;
		SerializedProperty m_DefaultStepMaterialProp;
		SerializedProperty m_DefaultTreadMaterialProp;
		SerializedProperty m_DefaultWallMaterialProp;
		SerializedProperty m_DefaultPhysicMaterialProp;

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
			m_DefaultStepMaterialProp   = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kDefaultStepMaterialName);
			m_DefaultTreadMaterialProp  = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kDefaultTreadMaterialName);
			m_DefaultWallMaterialProp   = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kDefaultWallMaterialName);
			m_DefaultPhysicMaterialProp = materialsProp.FindPropertyRelative(ChiselPipelineMaterialsSet.kDefaultPhysicMaterialName);

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

			GUILayout.Label(SettingsContent.MaterialSection, EditorStyles.boldLabel);
			EditorGUILayout.Space();
			
			using (new EditorGUI.IndentLevelScope(1))
			{
				EditorGUILayout.LabelField(SettingsContent.DefaultMaterialsSection, EditorStyles.boldLabel);
				using (new EditorGUI.IndentLevelScope(1))
				{
					EditorGUILayout.PropertyField(m_DefaultFloorMaterialProp, SettingsContent.DefaultFloorMaterial);
					EditorGUILayout.PropertyField(m_DefaultStepMaterialProp, SettingsContent.DefaultStepMaterial);
					EditorGUILayout.PropertyField(m_DefaultTreadMaterialProp, SettingsContent.DefaultTreadMaterial);
					EditorGUILayout.PropertyField(m_DefaultWallMaterialProp, SettingsContent.DefaultWallMaterial);
					EditorGUILayout.PropertyField(m_DefaultPhysicMaterialProp, SettingsContent.DefaultPhysicMaterial);
				}
				EditorGUILayout.Space();

				EditorGUILayout.LabelField(SettingsContent.VisualizationSurfaceMaterialsSection, EditorStyles.boldLabel);
				using (new EditorGUI.IndentLevelScope(1))
				{
					EditorGUILayout.PropertyField(m_HiddenSurfacesMaterialProp, SettingsContent.HiddenSurfacesMaterial);
					EditorGUILayout.PropertyField(m_DiscardedSurfacesMaterialProp, SettingsContent.DiscardedSurfacesMaterial);

					EditorGUILayout.PropertyField(m_ShadowCastingSurfacesMaterialProp, SettingsContent.ShadowCastingSurfacesMaterial);
					EditorGUILayout.PropertyField(m_ShadowOnlySurfacesMaterialProp, SettingsContent.ShadowOnlySurfacesMaterial);
					EditorGUILayout.PropertyField(m_ShadowReceivingSurfacesMaterialProp, SettingsContent.ShadowReceivingSurfacesMaterial);
					EditorGUILayout.PropertyField(m_CollisionOnlySurfacesMaterialProp, SettingsContent.CollisionSurfacesMaterial);
				}
				EditorGUILayout.Space();

				if (GUILayout.Button("Reset materials to defaults"))
				{
					ChiselProjectSettings.instance.ResetMaterials();
				}
			}

			if (serializedObject.ApplyModifiedProperties())
			{
				ChiselProjectSettings.instance.Save();
			}
		}

		[SettingsProvider]
		public static SettingsProvider CreateMyCustomSettingsProvider()
		{
			var provider = AssetSettingsProvider.CreateProviderFromObject(
				"Project/Chisel", ChiselProjectSettings.instance,
				SettingsProvider.GetSearchKeywordsFromGUIContentProperties<SettingsContent>());
			return provider;
		}
	}
}