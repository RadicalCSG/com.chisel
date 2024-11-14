using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Chisel.Core;

using UnityEngine;
using UnityEngine.Profiling;

namespace Chisel.Components
{
    // TODO: properly dispose native memory
    public static class ChiselUnityVisibilityManager
	{
#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
		static void StaticInitialize()
		{
			UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
			UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
		}

		static void OnBeforeAssemblyReload() 
        {
			s_BrushVisibilityLookup.Dispose();
			s_BrushVisibilityLookup = new();
		}
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		static void ResetState()
		{
			s_CameraDrawMode.Clear();
			s_IgnoreVisibility = false;
			s_UpdateVisibilityFlag = true; 
            s_BrushVisibilityLookup.Clear();
		}

		readonly static Dictionary<Camera, DrawModeFlags> s_CameraDrawMode = new();
		static bool s_UpdateVisibilityFlag = true;
		static bool s_IgnoreVisibility = false;

		static BrushVisibilityLookup s_BrushVisibilityLookup = new(); 

		public static ref BrushVisibilityLookup BrushVisibilityLookup { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return ref s_BrushVisibilityLookup; } }

		public static bool IsBrushVisible(CompactNodeID brushID)
		{
			return s_BrushVisibilityLookup.IsBrushVisible(brushID);
		}

		public static void SetDirty()
		{
			s_UpdateVisibilityFlag = true;
		}

		
		public static void UpdateVisibility(bool force = false)
        {
            if (!s_UpdateVisibilityFlag && !force)
                return;

            s_UpdateVisibilityFlag = false;
            s_BrushVisibilityLookup.UpdateVisibility(ChiselModelManager.Instance.Models);
		}

		public static void EnsureVisibilityInitialized(ChiselGeneratorComponent node)
		{
			if (s_BrushVisibilityLookup.HasVisibilityInitialized(node))
				return;
			UpdateVisibility(node);
		}

        public static DrawModeFlags UpdateDebugVisualizationState(DrawModeFlags drawModeFlags, bool ignoreBrushVisibility = true)
        {
            foreach (var model in ChiselModelManager.Instance.Models)
            {
                if (!model || !model.isActiveAndEnabled || model.generated == null)
                    continue;
				Profiler.BeginSample("UpdateDebugVisualizationState");
				model.generated.UpdateDebugVisualizationState(s_BrushVisibilityLookup, drawModeFlags, ignoreBrushVisibility);
				Profiler.EndSample();
			}
            return drawModeFlags;
        }

        public static void ResetCameraDrawMode(Camera camera)
        {
            s_CameraDrawMode.Remove(camera);
        }

        public static void SetCameraDrawMode(Camera camera, DrawModeFlags drawModeFlags)
        {
            s_CameraDrawMode[camera] = drawModeFlags;
        }

        public static DrawModeFlags GetCameraDrawMode(Camera camera)
        {
            if (!s_CameraDrawMode.TryGetValue(camera, out var drawModeFlags))
                drawModeFlags = DrawModeFlags.Default;
            return drawModeFlags;
        }

        public static DrawModeFlags BeginDrawModeForCamera(Camera camera = null, bool ignoreBrushVisibility = false)
		{
			s_IgnoreVisibility = true;
			if (camera == null)
                camera = Camera.current;
            var currentState = GetCameraDrawMode(camera);
            return UpdateDebugVisualizationState(currentState, s_IgnoreVisibility || ignoreBrushVisibility);
        }

        public static DrawModeFlags EndDrawModeForCamera()
		{
			s_IgnoreVisibility = false;
			return UpdateDebugVisualizationState(DrawModeFlags.Default, ignoreBrushVisibility: true);
        }

        public static void Update()
        {
            UpdateDebugVisualizationState(DrawModeFlags.Default, ignoreBrushVisibility: true);
        }
    }
}
