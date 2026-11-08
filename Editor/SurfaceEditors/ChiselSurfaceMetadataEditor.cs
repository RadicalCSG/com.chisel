using UnityEngine;
using UnityEditor;
using System;
using Chisel.Core;
using Chisel.Components;
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
		SerializedProperty hotspotVariantsProp;
		SerializedProperty hotspotSheetsProp;
		static readonly GUIContent kHotspotSheetsContents = new("Other Sheets", "Other materials with a hotspot layout of their own (a trim sheet, say) drawn with this one: matching picks from their tiles too, and a surface given one of theirs is given that material");
		static readonly GUIContent kHotspotVariantsContents = new("Variants", "Other materials drawn on the same layout (another texture of the same tiles): a surface is given this material or one of them, as a variation; Next Variation goes through them too");

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
			hotspotVariantsProp = serializedObject.FindProperty(ChiselSurfaceMetadata.kHotspotVariantsFieldName);
			hotspotSheetsProp = serializedObject.FindProperty(ChiselSurfaceMetadata.kHotspotSheetsFieldName);
			decalDefaultsProp = serializedObject.FindProperty(ChiselSurfaceMetadata.kDecalDefaultsFieldName);
			isDecalProp       = decalDefaultsProp?.FindPropertyRelative(nameof(ChiselDecalDefaults.isDecal));
		}

		SerializedProperty decalDefaultsProp;
		SerializedProperty isDecalProp;

		internal void OnDisable()
		{
			surfaceDestinationFlagsProp = null;
			outputFlagsProp = null;
			physicsMaterialProp = null;
			hotspotLayoutProp = null;
			hotspotVariantsProp = null;
			hotspotSheetsProp = null;
			decalDefaultsProp = null;
			isDecalProp = null;
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

		static readonly GUIContent kDoubleSidedContents = new("Double Sided", "The surfaces are drawn from behind too (a back face that is drawn but doesn't collide)");

		void DoubleSidedGUI()
		{
			var flags = (SurfaceOutputFlags)outputFlagsProp.intValue;
			EditorGUI.showMixedValue = outputFlagsProp.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			var doubleSided = EditorGUILayout.Toggle(kDoubleSidedContents, (flags & SurfaceOutputFlags.DoubleSided) != 0);
			if (EditorGUI.EndChangeCheck())
				outputFlagsProp.intValue = (int)(doubleSided ? flags | SurfaceOutputFlags.DoubleSided : flags & ~SurfaceOutputFlags.DoubleSided);
			EditorGUI.showMixedValue = false;
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
		// decal: a decal's images are the layout's tiles; variants and other sheets are only for surfaces
		void HotspotGUI(bool decal)
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
					ChiselHotspotLayoutWindow.Open((ChiselHotspotLayout)hotspotLayoutProp.objectReferenceValue, MaterialOf(target));
			}
			EditorGUILayout.Space(kButtonSpacing);
			if (!decal && hotspotVariantsProp != null && (hotspotLayoutProp.objectReferenceValue != null || hotspotLayoutProp.hasMultipleDifferentValues))
			{
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField(hotspotVariantsProp, kHotspotVariantsContents, true);
				changed |= EditorGUI.EndChangeCheck();
			}
			if (!decal && hotspotSheetsProp != null && (hotspotLayoutProp.objectReferenceValue != null || hotspotLayoutProp.hasMultipleDifferentValues))
			{
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField(hotspotSheetsProp, kHotspotSheetsContents, true);
				changed |= EditorGUI.EndChangeCheck();
			}
			if (!changed)
				return;
			serializedObject.ApplyModifiedProperties();
			foreach (var metadata in targets)
				ChiselHotspotLayoutWindow.RefreshSurfacesUsing(MaterialOf(metadata));
		}

		const float kButtonSpacing = 4;

		static readonly GUIContent kConfigurePaintContents = new("Configure Paintability Of Shader", "Opens Project Settings > Chisel > Paint on the paint profile of the material's shader, adding one when it has none");

		static readonly GUIContent kNeverPaintedContents = new("Never Paintable", "This shader is never painted: stop asking (Project Settings > Chisel > Paint lists such shaders)");

		/// <summary>Whether a material asks about its shader's paintability: not when the shader was said to be never painted</summary>
		internal static bool ShowsPaintability(Material material)
		{
			if (!material || !material.shader)
				return false;
			var settings = ChiselPaintSettings.Project;
			return !(settings && settings.IsNotPaintable(material.shader));
		}

		// Whether the material's shader can be painted, and the way to its paint profile; nothing for a shader said to
		// be never painted
		void PaintGUI()
		{
			var material = MaterialOf(target);
			if (!ShowsPaintability(material))
				return;
			var paintable = ChiselPaintProfiles.Find(material.shader) != null;
			EditorGUILayout.Space(kButtonSpacing);
			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.PrefixLabel(paintable ? "Paintable" : "Not Paintable");
				if (GUILayout.Button(kConfigurePaintContents, EditorStyles.miniButton))
					ChiselPaintSettingsProvider.Configure(material.shader);
				if (!paintable && GUILayout.Button(kNeverPaintedContents, EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
				{
					var settings = ChiselPaintSettings.Project ? ChiselPaintSettings.Project : ChiselPaintSettingsProvider.CreateProjectAsset();
					ChiselPaintSettingsProvider.SetNotPaintable(settings, material.shader, true);
					GUIUtility.ExitGUI();
				}
			}
		}

		static readonly GUIContent kDecalVisibleContents        = new("Visible", "The decals are drawn");
		static readonly GUIContent kDecalReceiveShadowsContents = new("Receive Shadows", "The decals take the shadows falling on them");
		static readonly GUIContent kDecalDefaultsContents       = new("Defaults for new decals", "What a new decal of this material starts from; each placed decal can still be changed on its own");
		static readonly GUIContent kShapeContents      = new("Shape");
		static readonly GUIContent kProjectionContents = new("Projection");
		static readonly GUIContent kReachContents      = new("Reach");
		static readonly GUIContent kBlendContents      = new("Blend", "Transparent: drawn over the surface. Opaque: replaces the surface where it covers it");
		static readonly GUIContent kTrimFillContents   = new("Trim fill");
		static readonly GUIContent kHeightContents     = new("Height", "The image's height; its width follows its aspect");
		static readonly GUIContent kDepthContents      = new("Depth", "How deep a drawn outline reaches");
		static readonly GUIContent[] kBlendOptions     = { new("Transparent"), new("Opaque") };

		// of the destination flags, the two a decal uses (ChiselDecalInstance.destinationFlags)
		void DecalFlagsGUI()
		{
			var flags = (SurfaceDestinationFlags)surfaceDestinationFlagsProp.enumValueFlag;
			EditorGUI.showMixedValue = surfaceDestinationFlagsProp.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			var visible  = EditorGUILayout.Toggle(kDecalVisibleContents, (flags & SurfaceDestinationFlags.Renderable) != 0);
			bool receive;
			using (new EditorGUI.DisabledScope(!visible))
				receive = EditorGUILayout.Toggle(kDecalReceiveShadowsContents, visible && (flags & SurfaceDestinationFlags.ShadowReceiving) != 0);
			if (EditorGUI.EndChangeCheck())
			{
				flags = visible ? flags | SurfaceDestinationFlags.Renderable : flags & ~SurfaceDestinationFlags.Renderable;
				flags = receive ? flags | SurfaceDestinationFlags.ShadowReceiving : flags & ~SurfaceDestinationFlags.ShadowReceiving;
				surfaceDestinationFlagsProp.intValue = (int)flags;
			}
			EditorGUI.showMixedValue = false;
		}

		static void DisabledReason(string reason)
		{
			using (new EditorGUI.IndentLevelScope(1))
				EditorGUILayout.LabelField(reason, EditorStyles.centeredGreyMiniLabel);
		}

		// every row is always shown; the ones that don't apply to the shape are disabled with a reason
		void DecalDefaultsGUI()
		{
			var shapeProp      = decalDefaultsProp.FindPropertyRelative(nameof(ChiselDecalDefaults.shape));
			var projectionProp = decalDefaultsProp.FindPropertyRelative(nameof(ChiselDecalDefaults.projection));
			var reachProp      = decalDefaultsProp.FindPropertyRelative(nameof(ChiselDecalDefaults.reach));
			var blendProp      = decalDefaultsProp.FindPropertyRelative(nameof(ChiselDecalDefaults.transparent));
			var trimFillProp   = decalDefaultsProp.FindPropertyRelative(nameof(ChiselDecalDefaults.trimFill));
			var heightProp     = decalDefaultsProp.FindPropertyRelative(nameof(ChiselDecalDefaults.height));

			EditorGUILayout.Space(kButtonSpacing);
			EditorGUILayout.LabelField(kDecalDefaultsContents, EditorStyles.boldLabel);

			// a decal material is placed as a box or a trim; a polygon is drawn with a surface's own material
			EditorGUI.showMixedValue = shapeProp.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			var newShape = (ChiselDecalShape)EditorGUILayout.EnumPopup(kShapeContents, (ChiselDecalShape)shapeProp.enumValueIndex,
				value => (ChiselDecalShape)value != ChiselDecalShape.Polygon, false);
			EditorGUI.showMixedValue = false;
			var shapeChanged = EditorGUI.EndChangeCheck();
			if (shapeChanged)
				shapeProp.enumValueIndex = (int)newShape;
			if (shapeChanged)
			{
				// changing the shape keeps the projection one it takes
				var shape = (ChiselDecalShape)shapeProp.enumValueIndex;
				var projection = (ChiselDecalProjection)projectionProp.enumValueIndex;
				if (shape == ChiselDecalShape.Polygon)
					projectionProp.enumValueIndex = (int)ChiselDecalProjection.Polygon;
				else if (shape == ChiselDecalShape.Box && projection == ChiselDecalProjection.Polygon)
					projectionProp.enumValueIndex = (int)ChiselDecalProjection.Orthographic;
			}

			var mixedShape = shapeProp.hasMultipleDifferentValues;
			var current    = (ChiselDecalShape)shapeProp.enumValueIndex;
			var trim       = !mixedShape && current == ChiselDecalShape.Trim;
			var polygon    = !mixedShape && current == ChiselDecalShape.Polygon;
			using (new EditorGUI.DisabledScope(trim || polygon))
			{
				EditorGUI.showMixedValue = projectionProp.hasMultipleDifferentValues;
				EditorGUI.BeginChangeCheck();
				var projection = (ChiselDecalProjection)EditorGUILayout.EnumPopup(kProjectionContents, (ChiselDecalProjection)projectionProp.enumValueIndex,
					value => ChiselDecalDefaults.AllowsProjection(current, (ChiselDecalProjection)value), false);
				if (EditorGUI.EndChangeCheck())
					projectionProp.enumValueIndex = (int)projection;
				EditorGUI.showMixedValue = false;
			}
			if (trim)         DisabledReason("Not used by Trim");
			else if (polygon) DisabledReason("Locked to Polygon");

			var omni = !mixedShape && current == ChiselDecalShape.Box && !projectionProp.hasMultipleDifferentValues &&
					   (ChiselDecalProjection)projectionProp.enumValueIndex == ChiselDecalProjection.Omni;
			using (new EditorGUI.DisabledScope(!omni))
				EditorGUILayout.PropertyField(reachProp, kReachContents);
			if (!omni) DisabledReason("Only for Box with Omni");

			EditorGUI.showMixedValue = blendProp.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			var blend = EditorGUILayout.Popup(kBlendContents, blendProp.boolValue ? 0 : 1, kBlendOptions);
			if (EditorGUI.EndChangeCheck())
				blendProp.boolValue = blend == 0;
			EditorGUI.showMixedValue = false;

			using (new EditorGUI.DisabledScope(!trim))
				EditorGUILayout.PropertyField(trimFillProp, kTrimFillContents);
			if (!trim) DisabledReason("Only for Trim");

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(heightProp, polygon ? kDepthContents : kHeightContents);
			if (EditorGUI.EndChangeCheck() && heightProp.floatValue < ChiselDecalDefaults.kMinHeight)
				heightProp.floatValue = ChiselDecalDefaults.kMinHeight;
		}

		float lastWidth;

		// the flags take more rows when the space is narrow; the width is only known when painting, so the last one is used
		void DestinationFlagsGUI()
		{
			var widthRect = EditorGUILayout.GetControlRect(false, 0);
			if (Event.current.type == EventType.Repaint)
			{
				if (!Mathf.Approximately(lastWidth, widthRect.width))
				{
					lastWidth = widthRect.width;
					Repaint();
				}
			}
			var fieldWidth = lastWidth > 0 ? lastWidth - EditorGUIUtility.labelWidth : EditorGUIUtility.currentViewWidth - EditorGUIUtility.labelWidth - 24;
			var rect = EditorGUILayout.GetControlRect(true, SurfaceDestinationFlagsPropertyDrawer.HeightFor(fieldWidth));
			EditorGUI.PropertyField(rect, surfaceDestinationFlagsProp, true);
		}

		static readonly GUIContent kIsDecalContents = new("Use as decal", "The material is placed as decals (listed in the Surface Browser's Decals tab), not put on surfaces: only the settings decals use are shown");

		/// <summary>Whether the edited material is a decal material (mixed counts as not)</summary>
		public bool IsDecal => isDecalProp != null && !isDecalProp.hasMultipleDifferentValues && isDecalProp.boolValue;

		public override void OnInspectorGUI()
		{
			Profiler.BeginSample("OnInspectorGUI");
			try
			{
				serializedObject.Update();
				EditorGUI.BeginChangeCheck();
				{
					if (isDecalProp != null)
						EditorGUILayout.PropertyField(isDecalProp, kIsDecalContents);
					if (IsDecal)
					{
						// a decal is only drawn: whether it shows and takes shadows, its images and its defaults
						DecalFlagsGUI();
						if (hotspotLayoutProp != null)
							HotspotGUI(decal: true);
						DecalDefaultsGUI();
					} else
					{
						DestinationFlagsGUI();
						GetSurfaceDestinationFlags(surfaceDestinationFlagsProp, out var isRenderable, out var isCollidable);
						if (isRenderable && outputFlagsProp != null)
						{
							DoubleSidedGUI();
							LightmapGUI();
						}
						if (isCollidable)
						{
							EditorGUILayout.PropertyField(physicsMaterialProp, true);
						}
						if (isRenderable && hotspotLayoutProp != null)
						{
							HotspotGUI(decal: false);
						}
					}
				}
				if (EditorGUI.EndChangeCheck())
				{
					serializedObject.ApplyModifiedProperties();
					// the surfaces drawn with the material take its settings again; nothing else is rebuilt
					foreach (var metadata in targets)
						ChiselHotspotLayoutWindow.RefreshSurfacesUsing(MaterialOf(metadata));
					ChiselDecalManager.SetDirty();
				}
				if (!IsDecal)
					PaintGUI();
			}
			catch (ExitGUIException) { }
			catch (Exception ex) { Debug.LogException(ex); }
			Profiler.EndSample();
		}
	}
}
