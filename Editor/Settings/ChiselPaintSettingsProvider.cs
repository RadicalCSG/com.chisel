using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
	// The project's paint profiles: a list of the shaders that can be painted, and the modes of the one chosen
	static class ChiselPaintSettingsGUI
	{
		static readonly GUIContent kExplanation = EditorGUIUtility.TrTextContent(
			"Surfaces drawn with one of these shaders can be painted with the Chisel Paint tool, in its modes. " +
			"The shader shows the paint; Chisel writes it to the vertex attributes the modes name.");
		static readonly GUIContent kAddContent    = EditorGUIUtility.TrTextContent("Add Shader", "Makes surfaces drawn with this shader paintable");
		static readonly GUIContent kRemoveContent = EditorGUIUtility.TrTextContent("Remove", "Surfaces drawn with this shader can't be painted any more (their paint is kept)");
		static readonly GUIContent kModesContent  = EditorGUIUtility.TrTextContent("Modes", "What a surface drawn with the shader can be painted with");

		internal static int s_Selected;
		static SerializedObject s_Serialized;

		public static void OnGUI(ChiselPaintSettings settings)
		{
			EditorGUILayout.LabelField(kExplanation, EditorStyles.wordWrappedMiniLabel);
			EditorGUILayout.Space();

			if (s_Serialized == null || s_Serialized.targetObject != settings)
				s_Serialized = new SerializedObject(settings);
			s_Serialized.Update();
			var profilesProp = s_Serialized.FindProperty(nameof(ChiselPaintSettings.profiles));

			var added = (Shader)EditorGUILayout.ObjectField(kAddContent, null, typeof(Shader), false);
			if (added)
			{
				s_Selected = ChiselPaintSettingsProvider.Add(settings, added);
				s_Serialized.Update();
			}
			for (int i = 0; i < profilesProp.arraySize; i++)
			{
				var shader = profilesProp.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(ChiselPaintProfile.shader)).objectReferenceValue as Shader;
				using (new EditorGUILayout.HorizontalScope())
				{
					if (GUILayout.Toggle(i == s_Selected, shader ? shader.name : "(no shader)", EditorStyles.toolbarButton))
						s_Selected = i;
					if (GUILayout.Button(kRemoveContent, EditorStyles.miniButton, GUILayout.Width(60)))
					{
						profilesProp.DeleteArrayElementAtIndex(i);
						s_Selected = Mathf.Min(s_Selected, profilesProp.arraySize - 1);
						s_Serialized.ApplyModifiedProperties();
						ChiselPaintSettings.Changed();
						GUIUtility.ExitGUI();
					}
				}
			}

			EditorGUI.BeginChangeCheck();
			if (s_Selected >= 0 && s_Selected < profilesProp.arraySize)
			{
				EditorGUILayout.Space();
				var modesProp = profilesProp.GetArrayElementAtIndex(s_Selected).FindPropertyRelative(nameof(ChiselPaintProfile.modes));
				modesProp.isExpanded = true;
				EditorGUILayout.PropertyField(modesProp, kModesContent, true);
			}
			if (EditorGUI.EndChangeCheck())
			{
				s_Serialized.ApplyModifiedProperties();
				ChiselPaintSettings.Changed();
			}

			if (s_Selected >= 0 && s_Selected < settings.profiles.Count)
			{
				var profile = settings.profiles[s_Selected];
				if (profile.shader != null && settings.Find(profile.shader) != profile)
					EditorGUILayout.HelpBox("Another profile is for this shader too; only the first one is used.", MessageType.Warning);
				if (profile.FindOverlap(out var first, out var second))
					EditorGUILayout.HelpBox($"Modes {first} and {second} both write a component of {profile.modes[first].attribute}: one of them overwrites the other.", MessageType.Warning);
			}
		}
	}

	// Project Settings > Chisel > Paint
	class ChiselPaintSettingsProvider : SettingsProvider
	{
		public const string kPath = "Project/Chisel/Paint";

		ChiselPaintSettingsProvider(string path, SettingsScope scope) : base(path, scope) { }

		[SettingsProvider]
		public static SettingsProvider CreatePaintSettingsProvider()
		{
			return new ChiselPaintSettingsProvider(kPath, SettingsScope.Project)
			{
				label    = "Paint",
				keywords = new HashSet<string>(new[] { "chisel", "paint", "vertex", "color", "shader", "profile" })
			};
		}

		// The project's settings, with the package's test shaders in them
		public static ChiselPaintSettings CreateProjectAsset()
		{
			var folder = System.IO.Path.GetDirectoryName(ChiselPaintSettings.kAssetPath).Replace('\\', '/');
			if (!AssetDatabase.IsValidFolder(folder))
				AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
			var settings = ScriptableObject.CreateInstance<ChiselPaintSettings>();
			var color = Shader.Find("Chisel/Test/Paint Color");
			if (color)
				settings.profiles.Add(new ChiselPaintProfile { shader = color, modes = { ChiselPaintMode.Color("Color") } });
			var value = Shader.Find("Chisel/Test/Paint Value");
			if (value)
				settings.profiles.Add(new ChiselPaintProfile { shader = value, modes = { ChiselPaintMode.Value("Value", ChiselPaintAttribute.TexCoord2, ChiselPaintComponents.X) } });
			AssetDatabase.CreateAsset(settings, AssetDatabase.GenerateUniqueAssetPath(ChiselPaintSettings.kAssetPath));
			AssetDatabase.SaveAssets();
			ChiselPaintSettings.Project = settings;
			return settings;
		}

		// The index of the shader's profile, added when it has none
		public static int Add(ChiselPaintSettings settings, Shader shader)
		{
			var index = settings.profiles.FindIndex(profile => profile != null && profile.shader == shader);
			if (index >= 0)
				return index;
			Undo.RecordObject(settings, "Make Shader Paintable");
			settings.profiles.Add(new ChiselPaintProfile { shader = shader });
			EditorUtility.SetDirty(settings);
			ChiselPaintSettings.Changed();
			return settings.profiles.Count - 1;
		}

		// Opens the page on the shader's profile, adding one when it has none
		public static void Configure(Shader shader)
		{
			var settings = ChiselPaintSettings.Project;
			if (!settings)
				settings = CreateProjectAsset();
			if (shader)
				ChiselPaintSettingsGUI.s_Selected = Add(settings, shader);
			SettingsService.OpenProjectSettings(kPath);
		}

		public override void OnGUI(string searchContext)
		{
			EditorGUIUtility.labelWidth = 120;
			EditorGUILayout.Space();
			var settings = ChiselPaintSettings.Project;
			if (!settings)
			{
				EditorGUILayout.HelpBox("This project has no paint settings, so no surface can be painted.", MessageType.Info);
				if (GUILayout.Button("Create Paint Settings", GUILayout.Width(200)))
					CreateProjectAsset();
				return;
			}
			ChiselPaintSettingsGUI.OnGUI(settings);
		}
	}

	[CustomEditor(typeof(ChiselPaintSettings))]
	class ChiselPaintSettingsEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			var settings = (ChiselPaintSettings)target;
			if (settings != ChiselPaintSettings.Project)
				EditorGUILayout.HelpBox("These aren't the project's paint settings, so nothing uses them.", MessageType.Info);
			if (GUILayout.Button("Open In Project Settings"))
				SettingsService.OpenProjectSettings(ChiselPaintSettingsProvider.kPath);
			ChiselPaintSettingsGUI.OnGUI(settings);
		}
	}
}
