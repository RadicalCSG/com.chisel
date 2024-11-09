using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Chisel.Components;

using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Chisel.Editors
{
	public sealed class ChiselDrawModes
	{
		const string kChiselSection	= "Chisel";

		static readonly (string, DrawModeFlags)[] s_DrawModes = new[]
		{
			("Shadow Only Surfaces",		DrawModeFlags.HideRenderables | DrawModeFlags.ShowShadowOnly),
			("Collider Surfaces",			DrawModeFlags.HideRenderables | DrawModeFlags.ShowColliders),
			("Shadow Casting Surfaces",		DrawModeFlags.HideRenderables | DrawModeFlags.ShowShadowCasters | DrawModeFlags.ShowShadowOnly),
			("Shadow Receiving Surfaces",	DrawModeFlags.HideRenderables | DrawModeFlags.ShowShadowReceivers),
			("Hidden Surfaces",				DrawModeFlags.ShowShadowOnly | DrawModeFlags.ShowDiscarded | DrawModeFlags.ShowUserHidden)
		};
		static readonly Dictionary<string, DrawModeFlags> s_DrawModeLookup = new Dictionary<string, DrawModeFlags>();


		static bool drawModesInitialized = false;
		static void SetupDrawModes()
		{
			if (drawModesInitialized)
				return;

			s_DrawModeLookup.Clear();
			SceneView.ClearUserDefinedCameraModes();
			foreach (var item in s_DrawModes)
            {
				s_DrawModeLookup[item.Item1] = item.Item2;
				SceneView.AddCameraMode(item.Item1, kChiselSection);
			}

			drawModesInitialized = true;
		}

		static readonly HashSet<Camera> s_KnownCameras = new();

		private static void CustomOnPostRender(ScriptableRenderContext context, Camera camera) { DrawWithCamera(camera); }

		// TODO: Maybe have a manager to handle rendering for all models at the same time?
		// TODO:       -> can we still do lightmaps / navmeshes?
		// TODO: use commandbuffers instead?
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void DrawWithCamera(Camera camera)
		{
            if (!camera)
                return;
			if (s_KnownCameras.Contains(camera))
			{
				var drawModeFlags = ChiselUnityVisibilityManager.BeginDrawModeForCamera(camera);
				ChiselModelManager.Instance.OnRenderModels(camera, drawModeFlags);
				ChiselUnityVisibilityManager.EndDrawModeForCamera();
            } else
			{
				ChiselModelManager.Instance.OnRenderModels(camera, DrawModeFlags.None);
			}
		}

		[InitializeOnLoadMethod]
		static void OnProjectLoadedInEditor()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			SceneManager.sceneLoaded += OnSceneLoaded;
			ChiselModelManager.Instance.PostUpdateModels -= OnPostUpdateModels;
			ChiselModelManager.Instance.PostUpdateModels += OnPostUpdateModels;
		}

		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			ChiselModelManager.Instance.InitializeOnLoad(scene); // <- ensures selection works (rendering partial meshes hides regular meshes)
		}

		private static void OnPostUpdateModels()
		{
			ChiselUnityVisibilityManager.Update(); // <- ensures selection works (rendering partial meshes hides regular meshes)
		}

        [PostProcessScene(1)]
		public static void OnPostprocessScene()
		{
			ChiselModelManager.Instance.HideDebugVisualizationSurfaces();
		}

		static bool initialized = false;
		static void Initialize()
		{
			if (initialized)
				return;
			initialized = true;
			if (GraphicsSettings.defaultRenderPipeline == null)
			{
				RenderPipelineManager.beginCameraRendering -= CustomOnPostRender;
				Camera.onPreCull -= DrawWithCamera;
				Camera.onPreCull += DrawWithCamera;
			}
			else
			{
				Camera.onPreCull -= DrawWithCamera;
				RenderPipelineManager.beginCameraRendering -= CustomOnPostRender;
				RenderPipelineManager.beginCameraRendering += CustomOnPostRender;
			}
		}

		public static void HandleDrawMode(SceneView sceneView)
		{
			ChiselDrawModes.SetupDrawModes();

			Initialize();

			var camera = sceneView.camera;
			if (camera == null)
				return;

			s_KnownCameras.Add(camera);

			var desiredDrawModeFlags = DrawModeFlags.Default;
			if (sceneView.cameraMode.drawMode == DrawCameraMode.UserDefined)
			{
				if (s_DrawModeLookup.TryGetValue(sceneView.cameraMode.name, out var flags))
					desiredDrawModeFlags = flags;
			}
			var prevDrawMode = ChiselUnityVisibilityManager.GetCameraDrawMode(camera);
			if (prevDrawMode != desiredDrawModeFlags)
			{
				ChiselUnityVisibilityManager.SetCameraDrawMode(camera, desiredDrawModeFlags);
			}
		}
	}
}