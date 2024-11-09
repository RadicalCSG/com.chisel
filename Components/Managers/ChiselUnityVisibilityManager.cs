using System.Collections.Generic;

using Chisel.Core;

using UnityEngine;

namespace Chisel.Components
{
    // TODO: get rid of all statics, properly dispose native memory

    // TODO: fix adding "generated" gameObject causing TransformChanged events that dirty model, which rebuilds components
    // TODO: Modifying a lightmap index *should also be undoable*
    public sealed class ChiselUnityVisibilityManager
    {
        public static BrushVisibilityLookup brushVisibilityLookup = new();
		
		public static bool IsBrushVisible(CompactNodeID brushID)
		{
			return brushVisibilityLookup.IsBrushVisible(brushID);
		}

		public static void SetDirty()
		{
			updateVisibilityFlag = true;
		}

		static bool updateVisibilityFlag = true;
		public static void UpdateVisibility(bool force = false)
        {
            if (!updateVisibilityFlag && !force)
                return;

            updateVisibilityFlag = false;
            brushVisibilityLookup.UpdateVisibility(ChiselModelManager.Instance.Models);
		}

		public static void EnsureVisibilityInitialized(ChiselGeneratorComponent node)
		{
			if (brushVisibilityLookup.HasVisibilityInitialized(node))
				return;
			UpdateVisibility(node);
		}

        public static DrawModeFlags UpdateDebugVisualizationState(DrawModeFlags drawModeFlags, bool ignoreBrushVisibility = true)
        {
            foreach (var model in ChiselModelManager.Instance.Models)
            {
                if (!model || !model.isActiveAndEnabled || model.generated == null)
                    continue;
                model.generated.UpdateDebugVisualizationState(brushVisibilityLookup, drawModeFlags, ignoreBrushVisibility);
            }
            return drawModeFlags;
        }

        static readonly Dictionary<Camera, DrawModeFlags> s_CameraDrawMode = new();
        static bool s_IgnoreVisibility = false;

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
