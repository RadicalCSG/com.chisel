using UnityEngine;
using UnityEditor;
using System;
using Chisel.Core;
using UnityEngine.Profiling;

namespace Chisel.Editors
{
	[CustomEditor(typeof(ChiselSurfaceMetadata))]
	public sealed class ChiselSurfaceMetadataEditor : Editor
	{
		static readonly GUIContent   kLightmapContents = new("Lightmap", "How many lightmap texels the surfaces with this material get");
		static readonly GUIContent[] kLightmapOptions  =
		{
			new("By Size", "Texels in proportion to the surface's size"),
			new("One Texel", "One texel: for a surface that gives off light, whose own lighting doesn't show, but whose light a bake takes from its texels"),
			new("None", "No texels: for a surface that is never lit by a lightmap, as an unlit one")
		};

		SerializedProperty surfaceDestinationFlagsProp;
		SerializedProperty outputFlagsProp;
		SerializedProperty physicsMaterialProp;

		internal void OnEnable()
		{
			if (!target)
			{
				surfaceDestinationFlagsProp = null;
				outputFlagsProp = null;
				physicsMaterialProp = null;
				return;
			}

			surfaceDestinationFlagsProp = serializedObject.FindProperty(ChiselSurfaceMetadata.kDestinationFlagsFieldName);
			outputFlagsProp = serializedObject.FindProperty(ChiselSurfaceMetadata.kOutputFlagsName);
			physicsMaterialProp = serializedObject.FindProperty(ChiselSurfaceMetadata.kPhysicsMaterialFieldName);
		}

		internal void OnDisable()
		{
			surfaceDestinationFlagsProp = null;
			outputFlagsProp = null;
			physicsMaterialProp = null;
		}

		void OnDestroy() { OnDisable(); }

        public static void GetSurfaceDestinationFlags(SerializedProperty surfaceDestinationFlagsProp, out bool isRenderable, out bool isCollidable)
		{
			isCollidable = true;
			isRenderable = true;
			if (!surfaceDestinationFlagsProp.hasMultipleDifferentValues)
			{
				SurfaceDestinationFlags flags = (SurfaceDestinationFlags)surfaceDestinationFlagsProp.enumValueFlag;
				isCollidable = (flags & SurfaceDestinationFlags.Collidable) == SurfaceDestinationFlags.Collidable;
				isRenderable = (flags & SurfaceDestinationFlags.Renderable) == SurfaceDestinationFlags.Renderable;
			}
		}

		// The lightmap texels the surfaces get (SurfaceOutputFlags.NoLightmap, SingleLightmapTexel), as one choice
		void LightmapGUI()
		{
			var flags = (SurfaceOutputFlags)outputFlagsProp.intValue;
			var current = ((flags & SurfaceOutputFlags.NoLightmap) != 0) ? 2 : ((flags & SurfaceOutputFlags.SingleLightmapTexel) != 0) ? 1 : 0;
			EditorGUI.showMixedValue = outputFlagsProp.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			var chosen = EditorGUILayout.Popup(kLightmapContents, current, kLightmapOptions);
			if (EditorGUI.EndChangeCheck())
			{
				flags &= ~(SurfaceOutputFlags.NoLightmap | SurfaceOutputFlags.SingleLightmapTexel);
				if (chosen == 1) flags |= SurfaceOutputFlags.SingleLightmapTexel;
				if (chosen == 2) flags |= SurfaceOutputFlags.NoLightmap;
				outputFlagsProp.intValue = (int)flags;
			}
			EditorGUI.showMixedValue = false;
		}

		public override void OnInspectorGUI()
		{
			Profiler.BeginSample("OnInspectorGUI");
			try
			{
				EditorGUI.BeginChangeCheck();
				{
					EditorGUILayout.PropertyField(surfaceDestinationFlagsProp, true);
					GetSurfaceDestinationFlags(surfaceDestinationFlagsProp, out var isRenderable, out var isCollidable);
					if (isRenderable && outputFlagsProp != null)
					{
						LightmapGUI();
					}
					if (isCollidable)
					{
						EditorGUILayout.PropertyField(physicsMaterialProp, true);
					}
				}
				if (EditorGUI.EndChangeCheck())
				{
					// TODO: rebuild models if material is used within a model
					serializedObject.ApplyModifiedProperties();
				}
			}
			catch (ExitGUIException) { }
			catch (Exception ex) { Debug.LogException(ex); }
			Profiler.EndSample();
		}
	}
}
