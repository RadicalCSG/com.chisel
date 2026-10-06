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
		SerializedProperty hotspotLayoutProp;

		static readonly GUIContent kHotspotContents = new("Hotspot Layout", "Surfaces with this material take their uvs from the tile of this layout that fits them best");

		internal void OnEnable()
		{
			if (!target)
			{
				surfaceDestinationFlagsProp = null;
				outputFlagsProp = null;
				physicsMaterialProp = null;
				hotspotLayoutProp = null;
				return;
			}

			surfaceDestinationFlagsProp = serializedObject.FindProperty(ChiselSurfaceMetadata.kDestinationFlagsFieldName);
			outputFlagsProp = serializedObject.FindProperty(ChiselSurfaceMetadata.kOutputFlagsName);
			physicsMaterialProp = serializedObject.FindProperty(ChiselSurfaceMetadata.kPhysicsMaterialFieldName);
			hotspotLayoutProp = serializedObject.FindProperty(ChiselSurfaceMetadata.kHotspotLayoutFieldName);
		}

		internal void OnDisable()
		{
			surfaceDestinationFlagsProp = null;
			outputFlagsProp = null;
			physicsMaterialProp = null;
			hotspotLayoutProp = null;
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

		static Material MaterialOf(UnityEngine.Object metadata)
		{
			return AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(metadata)) as Material;
		}

		static ChiselHotspotLayout CreateLayoutFor(Material material)
		{
			var folder = material != null ? System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(material)) : "Assets";
			var name   = material != null ? material.name + " Hotspots" : "Hotspot Layout";
			var layout = CreateInstance<ChiselHotspotLayout>();
			layout.texture = material != null ? material.mainTexture as Texture2D : null;
			if (layout.texture != null)
				layout.size = new Vector2Int(layout.texture.width, layout.texture.height);
			layout.ResetCuts();
			AssetDatabase.CreateAsset(layout, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset"));
			return layout;
		}

		// The layout, a new one made from the material's texture, and the editor
		void HotspotGUI()
		{
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(hotspotLayoutProp, kHotspotContents);
			var changed = EditorGUI.EndChangeCheck();
			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();
				if (hotspotLayoutProp.objectReferenceValue == null)
				{
					if (!hotspotLayoutProp.hasMultipleDifferentValues && GUILayout.Button("New Layout", EditorStyles.miniButton))
					{
						hotspotLayoutProp.objectReferenceValue = CreateLayoutFor(MaterialOf(target));
						changed = true;
					}
				} else if (GUILayout.Button("Edit Layout", EditorStyles.miniButton))
					ChiselHotspotLayoutWindow.Open((ChiselHotspotLayout)hotspotLayoutProp.objectReferenceValue);
			}
			if (!changed)
				return;
			serializedObject.ApplyModifiedProperties();
			foreach (var metadata in targets)
				ChiselHotspotLayoutWindow.RefreshSurfacesUsing(MaterialOf(metadata));
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
					if (isRenderable && hotspotLayoutProp != null)
					{
						HotspotGUI();
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
