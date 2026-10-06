using Chisel.Components;

using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

namespace Chisel.Editors
{
	// defaultDisplay: true so it is actually visible when it first appears - an overlay that is not
	// asked for defaults to hidden, and has to be found in the scene view's Overlays menu (`) first.
	[Overlay(typeof(SceneView), kOverlayId, kOverlayTitle, defaultDisplay: true)]
	public class ChiselHelperSurfacesOverlay : IMGUIOverlay
	{
		const string kOverlayId    = "chisel-helper-surfaces";
		const string kOverlayTitle = "Chisel Helper Surfaces";

		readonly static (GUIContent content, DrawModeFlags flag)[] kHelperSurfaces = new[]
		{
			(new GUIContent("User Hidden",      "Surfaces explicitly set to not be visible"),        DrawModeFlags.ShowUserHidden),
			(new GUIContent("Shadow Casting",   "Surfaces that are rendered and cast shadows"),      DrawModeFlags.ShowShadowCasters),
			(new GUIContent("Shadow Only",      "Surfaces that cast shadows but are not rendered"),  DrawModeFlags.ShowShadowOnly),
			(new GUIContent("Shadow Receiving", "Surfaces that receive shadows"),                    DrawModeFlags.ShowShadowReceivers),
			(new GUIContent("Collision",        "Surfaces that are part of a collider"),             DrawModeFlags.ShowColliders),
			(new GUIContent("Discarded",        "Surfaces removed by the CSG process"),              DrawModeFlags.ShowDiscarded)
		};

		readonly static GUIContent kHideRenderables = new("Hide Renderables", "Hide the regular geometry, so only the helper surfaces above are drawn");
		readonly static GUIContent kNone            = new("None",  "Switch off every helper surface");
		readonly static GUIContent kAll             = new("All",   "Switch on every helper surface");

		const DrawModeFlags kAllHelperSurfaces = DrawModeFlags.ShowUserHidden |
												 DrawModeFlags.ShowShadowCasters |
												 DrawModeFlags.ShowShadowOnly |
												 DrawModeFlags.ShowShadowReceivers |
												 DrawModeFlags.ShowColliders |
												 DrawModeFlags.ShowDiscarded;

		public override void OnGUI()
		{
			var flags = ChiselEditorSettings.HelperSurfaceFlags;

			// A Chisel camera mode overrides these toggles entirely, so show them disabled rather
			// than letting the user change settings that visibly do nothing.
			var sceneView  = SceneView.lastActiveSceneView;
			var overridden = sceneView != null &&
							 sceneView.cameraMode.drawMode == DrawCameraMode.UserDefined &&
							 ChiselDrawModes.IsChiselCameraMode(sceneView.cameraMode.name);

			EditorGUI.BeginChangeCheck();
			using (new EditorGUI.DisabledScope(overridden))
			{
				foreach (var (content, flag) in kHelperSurfaces)
				{
					var isSet = (flags & flag) != DrawModeFlags.None;
					if (EditorGUILayout.ToggleLeft(content, isSet))
						flags |= flag;
					else
						flags &= ~flag;
				}

				EditorGUILayout.Space();

				var hideRenderables = (flags & DrawModeFlags.HideRenderables) != DrawModeFlags.None;
				if (EditorGUILayout.ToggleLeft(kHideRenderables, hideRenderables))
					flags |= DrawModeFlags.HideRenderables;
				else
					flags &= ~DrawModeFlags.HideRenderables;

				using (new EditorGUILayout.HorizontalScope())
				{
					if (GUILayout.Button(kNone, EditorStyles.miniButtonLeft))
						flags = DrawModeFlags.Default;
					if (GUILayout.Button(kAll, EditorStyles.miniButtonRight))
						flags |= kAllHelperSurfaces;
				}
			}
			if (EditorGUI.EndChangeCheck())
			{
				ChiselEditorSettings.HelperSurfaceFlags = flags;
				ChiselEditorSettings.Save();
				// The flags are only picked up when a scene view refreshes its camera draw mode.
				SceneView.RepaintAll();
			}

			if (overridden)
				EditorGUILayout.HelpBox($"Overridden by the '{sceneView.cameraMode.name}' camera mode.", MessageType.Info);
		}
	}
}
