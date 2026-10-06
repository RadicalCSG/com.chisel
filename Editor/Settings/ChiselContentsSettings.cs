using System.Collections.Generic;
using System.IO;

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

using Chisel.Core;

namespace Chisel.Editors
{
	public static class ChiselContentsSettings
	{
		public const string kFolder    = ChiselContentsList.kProjectFolder;
		public const string kAssetPath = ChiselContentsList.kProjectAssetPath;

		public static ChiselContentsList Create()
		{
			var list = ChiselContentsList.MakeProjectList(ChiselContentsList.CreateProjectAsset());
			SceneView.RepaintAll();
			return list;
		}

		// Makes this the project's list: the settings point to it, a build preloads it, and the trees are rebuilt
		// when it has a different number of types than the list they were built with.
		public static void Use(ChiselContentsList list)
		{
			ChiselContentsList.MakeProjectList(list);
			SceneView.RepaintAll();
		}

		// After any change to the list
		public static void Applied()
		{
			CompactHierarchyManager.ApplyContentsList();
			SceneView.RepaintAll();
		}

		public static bool IsPreloaded(ChiselContentsList list)     => ChiselContentsList.IsPreloaded(list);
		public static void EnsurePreloaded(ChiselContentsList list) => ChiselContentsList.EnsurePreloaded(list);
	}

	public static class ChiselContentsListGUI
	{
		class Content
		{
			public readonly static GUIContent kAppend      = EditorGUIUtility.TrTextContent("Add Type");
			public readonly static GUIContent kRemoveLast  = EditorGUIUtility.TrTextContent("Remove Last");
			public readonly static GUIContent kExplanation = EditorGUIUtility.TrTextContent(
				"Every brush is made of one of these types. Solid is built in and always first. A face inside a brush " +
				"of its own type, or inside Solid, is removed; every other interface is kept.\n" +
				"Brushes store the index, so types can be renamed and added, and only the last one removed.");
		}

		static string s_NewName = "";

		public static void OnGUI(ChiselContentsList list)
		{
			EditorGUILayout.LabelField(Content.kExplanation, EditorStyles.wordWrappedMiniLabel);
			EditorGUILayout.Space();

			for (int index = 0; index < list.Count; index++)
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(24));
					if (index == ChiselContentsList.kSolidIndex)
					{
						using (new EditorGUI.DisabledScope(true))
							EditorGUILayout.TextField(list.NameOf(index));
						continue;
					}

					EditorGUI.BeginChangeCheck();
					var edited = EditorGUILayout.DelayedTextField(list.NameOf(index));
					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(list, "Rename Contents Type");
						list.Rename(index, edited);
					}
				}
			}

			EditorGUILayout.Space();
			using (new EditorGUILayout.HorizontalScope())
			{
				s_NewName = EditorGUILayout.TextField(s_NewName);
				using (new EditorGUI.DisabledScope(list.Count >= ChiselContentsList.kMaxEntries))
				{
					if (GUILayout.Button(Content.kAppend, GUILayout.Width(90)))
					{
						Undo.RecordObject(list, "Add Contents Type");
						list.Append(s_NewName);
						s_NewName = "";
						GUI.FocusControl(null);
						ChiselContentsSettings.Applied();
					}
				}
				using (new EditorGUI.DisabledScope(!list.CanRemoveLast))
				{
					if (GUILayout.Button(Content.kRemoveLast, GUILayout.Width(110)))
					{
						Undo.RecordObject(list, "Remove Contents Type");
						list.RemoveLast();
						ChiselContentsSettings.Applied();
					}
				}
			}

			if (list.Count >= ChiselContentsList.kMaxEntries)
				EditorGUILayout.HelpBox($"The list is full at {ChiselContentsList.kMaxEntries} types.", MessageType.Info);
		}
	}

	// Project Settings > Chisel > Contents
	class ChiselContentsSettingsProvider : SettingsProvider
	{
		public const string kPath = "Project/Chisel/Contents";

		class Content
		{
			public readonly static GUIContent kList        = EditorGUIUtility.TrTextContent("Contents List", "The asset that holds the project's contents types");
			public readonly static GUIContent kCreate      = EditorGUIUtility.TrTextContent("Create Contents List");
			public readonly static GUIContent kPreloadFix  = EditorGUIUtility.TrTextContent("Add To Preloaded Assets");
			public readonly static GUIContent kNotPreloaded = EditorGUIUtility.TrTextContent(
				"This list is not in the preloaded assets, so a built player won't have it and every brush would rebuild as Solid.");
		}

		ChiselContentsSettingsProvider(string path, SettingsScope scope) : base(path, scope) { }

		[SettingsProvider]
		public static SettingsProvider CreateContentsSettingsProvider()
		{
			return new ChiselContentsSettingsProvider(kPath, SettingsScope.Project)
			{
				label    = "Contents",
				keywords = new HashSet<string>(new[] { "chisel", "contents", "solid", "glass", "water", "brush", "type" })
			};
		}

		public override void OnGUI(string searchContext)
		{
			EditorGUIUtility.labelWidth = 120;
			EditorGUILayout.Space();

			var list = ChiselProjectSettings.ContentsList;
			EditorGUI.BeginChangeCheck();
			var picked = (ChiselContentsList)EditorGUILayout.ObjectField(Content.kList, list, typeof(ChiselContentsList), allowSceneObjects: false);
			if (EditorGUI.EndChangeCheck() && picked != null)
				ChiselContentsSettings.Use(picked);

			if (list == null)
			{
				EditorGUILayout.HelpBox("This project has no contents list, so every brush is Solid.", MessageType.Info);
				if (GUILayout.Button(Content.kCreate, GUILayout.Width(200)))
					ChiselContentsSettings.Create();
				return;
			}

			EditorGUILayout.Space();
			ChiselContentsListGUI.OnGUI(list);

			if (!ChiselContentsSettings.IsPreloaded(list))
			{
				EditorGUILayout.Space();
				EditorGUILayout.HelpBox(Content.kNotPreloaded.text, MessageType.Warning);
				if (GUILayout.Button(Content.kPreloadFix, GUILayout.Width(220)))
					ChiselContentsSettings.EnsurePreloaded(list);
			}
		}
	}

	// The default inspector would let an entry be moved or deleted from the middle, which changes the type of
	// every brush with a later index, so the asset gets the same narrow editor as the settings page.
	[CustomEditor(typeof(ChiselContentsList))]
	class ChiselContentsListEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			var list = (ChiselContentsList)target;
			if (list != ChiselProjectSettings.ContentsList)
				EditorGUILayout.HelpBox("This isn't the project's contents list, so no brush uses it. Project Settings > Chisel > Contents picks the list.", MessageType.Info);
			ChiselContentsListGUI.OnGUI(list);
		}
	}

	// A build has to carry the list, or the player rebuilds every brush as Solid
	class ChiselContentsBuildPreprocess : IPreprocessBuildWithReport
	{
		public int callbackOrder => 0;

		public void OnPreprocessBuild(BuildReport report)
		{
			var list = ChiselProjectSettings.ContentsList;
			if (list == null)
				return;
			if (!ChiselContentsSettings.IsPreloaded(list))
			{
				Debug.LogWarning($"Adding the contents list {AssetDatabase.GetAssetPath(list)} to the preloaded assets, so the player builds brushes with their types", list);
				ChiselContentsSettings.EnsurePreloaded(list);
			}
		}
	}

	// The field every generator shows beside its operation: the type its brushes are made of, picked by name.
	public static class ChiselContentsGUI
	{
		readonly static GUIContent kLabel     = EditorGUIUtility.TrTextContent("Contents", "What the brushes of this generator are made of. A face inside a brush of its own type, or inside Solid, is removed; every other interface is kept.");
		const string kEditContents            = "Edit Contents…";

		public static void ShowContentsField(SerializedProperty contentsProp)
		{
			if (contentsProp == null)
				return;

			var list       = ChiselContentsList.Instance;
			var names      = list.Names;
			var index      = contentsProp.intValue;
			var mixed      = contentsProp.hasMultipleDifferentValues;
			var pastTheEnd = !mixed && !list.IsValidIndex(index);

			// The names, a placeholder for an index past the end so the field can show it, and a way to the list
			var options = new List<string>(names);
			var selected = index;
			if (pastTheEnd)
			{
				selected = options.Count;
				options.Add($"<missing type {index}>");
			}
			options.Add("");
			var editIndex = options.Count;
			options.Add(kEditContents);

			EditorGUI.showMixedValue = mixed;
			EditorGUI.BeginChangeCheck();
			var result = EditorGUILayout.Popup(kLabel, selected, options.ToArray());
			var changed = EditorGUI.EndChangeCheck();
			EditorGUI.showMixedValue = false;

			if (changed)
			{
				if (result == editIndex)
					SettingsService.OpenProjectSettings(ChiselContentsSettingsProvider.kPath);
				else if (result < names.Length)
					contentsProp.intValue = result;
			}

			if (pastTheEnd)
				EditorGUILayout.HelpBox($"Contents type {index} is past the end of the project's contents list, so these brushes build as Solid.", MessageType.Warning);
		}
	}
}
