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

			public readonly static GUIContent kDefaultMaterialsSection = EditorGUIUtility.TrTextContent("Default");

			public readonly static GUIContent kDefaultFloorMaterial  = EditorGUIUtility.TrTextContent("Floor");
			public readonly static GUIContent kDefaultStepMaterial   = EditorGUIUtility.TrTextContent("Step");
			public readonly static GUIContent kDefaultTreadMaterial  = EditorGUIUtility.TrTextContent("Tread");
			public readonly static GUIContent kDefaultWallMaterial   = EditorGUIUtility.TrTextContent("Wall");
			public readonly static GUIContent kDefaultPhysicMaterial = EditorGUIUtility.TrTextContent("Physics");


			public readonly static GUIContent kVisualizationSurfaceMaterialsSection = EditorGUIUtility.TrTextContent("Surface Visualization");

			public readonly static GUIContent kHiddenSurfacesMaterial		   = EditorGUIUtility.TrTextContent("User Hidden");
			public readonly static GUIContent kDiscardedSurfacesMaterial	   = EditorGUIUtility.TrTextContent("Removed By Chisel");
			public readonly static GUIContent kShadowCastingSurfacesMaterial   = EditorGUIUtility.TrTextContent("Shadow-Casting");
			public readonly static GUIContent kShadowOnlySurfacesMaterial	   = EditorGUIUtility.TrTextContent("Shadow-Only");
			public readonly static GUIContent kShadowReceivingSurfacesMaterial = EditorGUIUtility.TrTextContent("Shadow-Receiving");
			public readonly static GUIContent kCollisionSurfacesMaterial	   = EditorGUIUtility.TrTextContent("Collision");
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

			GUILayout.Label(SettingsContent.kMaterialSection, EditorStyles.boldLabel);
			EditorGUILayout.Space();
			
			using (new EditorGUI.IndentLevelScope(1))
			{
				EditorGUILayout.LabelField(SettingsContent.kDefaultMaterialsSection, EditorStyles.boldLabel);
				using (new EditorGUI.IndentLevelScope(1))
				{
					EditorGUILayout.PropertyField(m_DefaultFloorMaterialProp, SettingsContent.kDefaultFloorMaterial);
					EditorGUILayout.PropertyField(m_DefaultStepMaterialProp, SettingsContent.kDefaultStepMaterial);
					EditorGUILayout.PropertyField(m_DefaultTreadMaterialProp, SettingsContent.kDefaultTreadMaterial);
					EditorGUILayout.PropertyField(m_DefaultWallMaterialProp, SettingsContent.kDefaultWallMaterial);
					EditorGUILayout.PropertyField(m_DefaultPhysicMaterialProp, SettingsContent.kDefaultPhysicMaterial);
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