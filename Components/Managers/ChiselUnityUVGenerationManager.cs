#if UNITY_EDITOR
using UnityEngine;

using UnityEditor;
using UnityEditor.SceneManagement;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System;
using Unity.Collections;
using System.Linq;

namespace Chisel.Components
{
    // TODO: Modifying a lightmap index *should also be undoable*
    public static class ChiselUnityUVGenerationManager
	{
        const float kGenerateUVDelayTime = 1.0f;
        static bool s_HaveUVsToUpdate = false;

        public static void ForceUpdateDelayedUVGeneration()
        {
            s_HaveUVsToUpdate = true;
        }

        public static bool NeedUVGeneration(ChiselModelComponent model)
        {
            if (!model)
                return false;

            var staticFlags = GameObjectUtility.GetStaticEditorFlags(model.gameObject);
            if ((staticFlags & StaticEditorFlags.ContributeGI) != StaticEditorFlags.ContributeGI)
                return false;

            if (!HasLightmapUVs(model.generated.renderables))
                return true;
            return false;
        }
        
        public static void DelayedUVGeneration(bool force = false)
        {
            if (!s_HaveUVsToUpdate && !force)
                return;

            float currentTime = Time.realtimeSinceStartup;

            s_HaveUVsToUpdate = false;
            foreach (var model in ChiselModelManager.Instance.Models)
            {
                if (!model)
                    continue;

                var staticFlags = GameObjectUtility.GetStaticEditorFlags(model.gameObject);
                var lightmapStatic = (staticFlags & StaticEditorFlags.ContributeGI) == StaticEditorFlags.ContributeGI;
                if ((!model.AutoRebuildUVs && !force) || !lightmapStatic)
                    continue;

                var renderables = model.generated.renderables;
                if (renderables == null)
                    continue;

                for (int i = 0; i < renderables.Length; i++)
                {
                    var renderable  = renderables[i];
                    if (renderable == null || 
                        renderable.invalid ||
                        (!force && renderable.uvLightmapUpdateTime == 0))
                        continue;

                    if (force || 
                        (currentTime - renderable.uvLightmapUpdateTime) > kGenerateUVDelayTime)
					{
						renderable.uvLightmapUpdateTime = 0;
                        GenerateLightmapUVsForInstance(model, renderable, force);
                    } else
                        s_HaveUVsToUpdate = true;
                }
            }
        }

        public static bool ClearLightmapData(GameObjectState state, ChiselRenderObjects renderable)
        {
            var lightmapStatic = (state.staticFlags & StaticEditorFlags.ContributeGI) == StaticEditorFlags.ContributeGI;
            if (!lightmapStatic)
                return false;
			
			renderable.meshRenderer.realtimeLightmapIndex = -1;
            renderable.meshRenderer.lightmapIndex = -1;
            renderable.uvLightmapUpdateTime = Time.realtimeSinceStartup;
            s_HaveUVsToUpdate = true;
            return true;
        }

		// FIXME: Hacky way to store that a mesh has lightmap UV created
		// Note: tried storing this in name of mesh, but getting the current mesh name allocates a lot of memory 
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static bool HasLightmapUVs(UnityEngine.Mesh sharedMesh)
        {
            if (!sharedMesh)
                return true;
            return (sharedMesh.hideFlags & HideFlags.NotEditable) == HideFlags.NotEditable;
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static bool HasLightmapUVs(ChiselRenderObjects renderable)
		{
			if (renderable == null ||
				renderable.invalid)
				return false;
            return HasLightmapUVs(renderable.sharedMesh);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static bool HasLightmapUVs(ChiselRenderObjects[] renderables)
		{
			if (renderables == null)
				return false;

			for (int i = 0; i < renderables.Length; i++)
			{
				if (HasLightmapUVs(renderables[i]))
					return true;
			}
			return false;
		}

		public static void SetHasLightmapUVs(UnityEngine.Mesh sharedMesh, bool haveLightmapUVs)
        {
            HideFlags hideFlags     = sharedMesh.hideFlags;
            HideFlags newHideFlags  = hideFlags;
            if (!haveLightmapUVs)
            {
                newHideFlags &= ~HideFlags.NotEditable;
            } else
            {
                newHideFlags |= HideFlags.NotEditable;
            }

            if (newHideFlags == hideFlags)
                return;
            sharedMesh.hideFlags = newHideFlags;
        }

        private static void GenerateLightmapUVsForInstance(ChiselModelComponent model, ChiselRenderObjects renderable, bool force = false)
        {
			// Avoid light mapping multiple times, when the same mesh is used on multiple MeshRenderers
			if (!force && HasLightmapUVs(renderable.sharedMesh))
                return;

            if (renderable == null ||
                !renderable.meshFilter ||
                !renderable.meshRenderer)
                return;
            
            UnwrapParam.SetDefaults(out UnwrapParam param);
            var uvSettings = model.RenderSettings.uvGenerationSettings;
            param.angleError	= Mathf.Clamp(uvSettings.angleError,       SerializableUnwrapParam.minAngleError, SerializableUnwrapParam.maxAngleError);
            param.areaError		= Mathf.Clamp(uvSettings.areaError,        SerializableUnwrapParam.minAreaError,  SerializableUnwrapParam.maxAreaError );
            param.hardAngle		= Mathf.Clamp(uvSettings.hardAngle,        SerializableUnwrapParam.minHardAngle,  SerializableUnwrapParam.maxHardAngle );
            param.packMargin	= Mathf.Clamp(uvSettings.packMarginPixels, SerializableUnwrapParam.minPackMargin, SerializableUnwrapParam.maxPackMargin) / 256.0f;

            var sharedMesh      = renderable.sharedMesh;

            var oldVertices		= sharedMesh.vertices;
            if (oldVertices.Length == 0)
                return;

			var lightmapGenerationTime = EditorApplication.timeSinceStartup;

            bool success = Unwrapping.GenerateSecondaryUVSet(sharedMesh, param);

			lightmapGenerationTime = EditorApplication.timeSinceStartup - lightmapGenerationTime; 
            
            // TODO: make a nicer text here
            Debug.Log("Generating lightmap UVs (by Unity) for the mesh '" + sharedMesh.name + "' of the Model named \"" + model.name +"\"\n"+
                      "\tUV generation in " + (lightmapGenerationTime* 1000) + " ms\n", renderable.container);
            if (!success)
                Debug.LogError("Lightmap generation, internal unity functionality, failed for the mesh '" + sharedMesh.name + "' of the Model named \"" + model.name + "\"", renderable.container);

            SetHasLightmapUVs(sharedMesh, success);

            if (success)
            {
                renderable.meshFilter.sharedMesh = null;
                renderable.meshFilter.sharedMesh = sharedMesh;
                EditorSceneManager.MarkSceneDirty(model.gameObject.scene);
            }
        }
    }
}
#endif
