#if UNITY_EDITOR
using UnityEngine;

using UnityEditor;
using UnityEditor.SceneManagement;
using System.Runtime.CompilerServices;

namespace Chisel.Components
{
    // TODO: get rid of all statics

    // TODO: fix adding "generated" gameObject causing TransformChanged events that dirty model, which rebuilds components
    // TODO: Modifying a lightmap index *should also be undoable*
    public sealed class ChiselUnityUVGenerationManager
	{
        const float kGenerateUVDelayTime = 1.0f;
        static bool haveUVsToUpdate = false;

        public static void ForceUpdateDelayedUVGeneration()
        {
            haveUVsToUpdate = true;
        }

        public static bool NeedUVGeneration(ChiselModelComponent model)
        {
            haveUVsToUpdate = false;

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
            if (!haveUVsToUpdate && !force)
                return;

            float currentTime = Time.realtimeSinceStartup;

            haveUVsToUpdate = false;
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
                        haveUVsToUpdate = true;
                }
            }
        }

        public static void ClearLightmapData(GameObjectState state, ChiselRenderObjects renderable)
        {
            var lightmapStatic = (state.staticFlags & StaticEditorFlags.ContributeGI) == StaticEditorFlags.ContributeGI;
            if (lightmapStatic)
            {
                renderable.meshRenderer.realtimeLightmapIndex = -1;
                renderable.meshRenderer.lightmapIndex = -1;
                renderable.uvLightmapUpdateTime = Time.realtimeSinceStartup;
                haveUVsToUpdate = true;
            }
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

            // TODO: can we avoid creating a temporary Mesh? if not; make sure ChiselSharedUnityMeshManager is handled correctly

            var oldUV			= sharedMesh.uv;
            var oldNormals		= sharedMesh.normals;
            var oldTangents		= sharedMesh.tangents;
            var oldTriangles	= sharedMesh.triangles;

            var tempMesh = new Mesh
            {
                vertices	= oldVertices,
                normals		= oldNormals,
                uv			= oldUV,
                tangents	= oldTangents,
                triangles	= oldTriangles
            };
            
            var lightmapGenerationTime = EditorApplication.timeSinceStartup;
            Unwrapping.GenerateSecondaryUVSet(tempMesh, param);
            lightmapGenerationTime = EditorApplication.timeSinceStartup - lightmapGenerationTime; 
            
            // TODO: make a nicer text here
            Debug.Log("Generating lightmap UVs (by Unity) for the mesh '" + sharedMesh.name + "' of the Model named \"" + model.name +"\"\n"+
                      "\tUV generation in " + (lightmapGenerationTime* 1000) + " ms\n", model);

            // Modify the original mesh, since it is shared
            sharedMesh.Clear(keepVertexLayout: true);
            sharedMesh.vertices  = tempMesh.vertices;
            sharedMesh.normals   = tempMesh.normals;
            sharedMesh.tangents  = tempMesh.tangents;
            sharedMesh.uv        = tempMesh.uv;
            sharedMesh.uv2       = tempMesh.uv2;	    // static lightmaps
            sharedMesh.uv3       = tempMesh.uv3;        // real-time lightmaps
            sharedMesh.triangles = tempMesh.triangles;
            SetHasLightmapUVs(sharedMesh, true);

            renderable.meshFilter.sharedMesh = null;
            renderable.meshFilter.sharedMesh = sharedMesh;
            EditorSceneManager.MarkSceneDirty(model.gameObject.scene);
        }
    }
}
#endif
