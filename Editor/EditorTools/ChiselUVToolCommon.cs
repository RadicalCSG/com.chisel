using System;
using System.Collections.Generic;
using System.Linq;
using Chisel.Core;
using Chisel.Components;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using System.Buffers;
using UnityEngine.Pool;

namespace Chisel.Editors
{
    sealed class ChiselSelectedSurface : ScriptableObject
    {
        public const string kSurfaceName = nameof(Surface);
        
        public ChiselSurface  Surface;
        [NonSerialized] public SurfaceReference SurfaceReference;

        public static ChiselSelectedSurface Create(SurfaceReference surfaceReference)
        {
            var container = ScriptableObject.CreateInstance<ChiselSelectedSurface>();
            container.SurfaceReference = surfaceReference;
            container.Surface          = surfaceReference.Surface;
            container.hideFlags        = HideFlags.DontSave;
            return container;
        }

        public void Load()
        {
			Surface.surfaceDetails = SurfaceReference.SurfaceDetails;
        }

        public void Store()
        {
            SurfaceReference.SurfaceDetails = Surface.surfaceDetails;
        }
    }

    sealed class ChiselUVToolCommon : ScriptableObject
    {
        #region Instance
        static ChiselUVToolCommon s_Instance;
        public static ChiselUVToolCommon Instance
        {
            get
            {
                if (s_Instance)
                    return s_Instance;

                s_Instance = ScriptableObject.CreateInstance<ChiselUVToolCommon>();
                s_Instance.hideFlags = HideFlags.HideAndDontSave;
                return s_Instance;
            }
        }
        #endregion

        readonly static int kSurfaceDragSelectionHash = "SurfaceDragSelection".GetHashCode();

        internal static bool InEditCameraMode	{ get { return (Tools.viewTool == ViewTool.Pan || Tools.viewTool == ViewTool.None); } }
        internal static bool ToolIsDragging		{ get; set; }
        internal static bool MouseIsDown	    { get; set; }


        public void OnActivate()
        {
            ChiselSurfaceSelectionManager.SelectionChanged -= UpdateSurfaceSelection;
            ChiselSurfaceSelectionManager.SelectionChanged += UpdateSurfaceSelection;

            Reset();
            UpdateSurfaceSelection();
            ChiselSurfaceOverlay.Show();
        }

        public void OnDeactivate()
        {
            ChiselSurfaceSelectionManager.SelectionChanged -= UpdateSurfaceSelection;
            Reset();
            DestroyOldSurfaces();
            ChiselSurfaceOverlay.Hide();
        }

        void Reset()
        {
            if (serializedObject != null)
            {
                serializedObject.Dispose();
                serializedObject = null;
            }
			surfaceFlagsProp = null;
			UV0Prop = null;
			hotspotProp = null;
			initialized = false;
        }

        void DestroyOldSurfaces()
        {
            if (surfaces != null &&
                surfaces.Length > 0)
            {
                foreach (var surface in surfaces)
                    UnityEngine.Object.DestroyImmediate(surface);
            }
            surfaces = Array.Empty<ChiselSelectedSurface>();
        }

        void UpdateSurfaceSelection()
        {
            DestroyOldSurfaces();
            surfaces = (from reference in ChiselSurfaceSelectionManager.Selection select ChiselSelectedSurface.Create(reference)).ToArray();
            var undoableObjectList = (from surface in surfaces select (UnityEngine.Object)surface.SurfaceReference.node).ToList();                
            undoableObjects = undoableObjectList.ToArray();

            if (surfaces.Length == 0)
            {
                Reset();
                return;
            }

            serializedObject = new SerializedObject(surfaces);
            var surfaceProp = serializedObject.FindProperty(ChiselSelectedSurface.kSurfaceName);
			var surfaceDetailsProp = surfaceProp.FindPropertyRelative(ChiselSurface.kSurfaceDetailsName);
			if (surfaceDetailsProp != null)
            {
                surfaceFlagsProp     = surfaceDetailsProp.FindPropertyRelative(SurfaceDetails.kDetailFlagsName);
                UV0Prop              = surfaceDetailsProp.FindPropertyRelative(SurfaceDetails.kUV0Name);
                hotspotProp          = surfaceDetailsProp.FindPropertyRelative(nameof(SurfaceDetails.hotspot));
			}
			chiselMaterialProp = surfaceProp.FindPropertyRelative(ChiselSurface.kChiselMaterialName);
            initialized = true;
        }

        void UpdateAllSurfaces()
        {
            if (surfaces == null)
                return;
            foreach(var surface in surfaces)
                surface.Load();
        }

        bool initialized = false;
        SerializedObject serializedObject;
		SerializedProperty chiselMaterialProp;

		SerializedProperty surfaceFlagsProp;
        SerializedProperty UV0Prop;
        SerializedProperty hotspotProp;
        [SerializeField] UnityEngine.Object[]    undoableObjects = Array.Empty<UnityEngine.Object>();
        [SerializeField] ChiselSelectedSurface[] surfaces        = Array.Empty<ChiselSelectedSurface>();

		readonly static int kSliderHash = (nameof(ChiselUVToolCommon) + "Slider").GetHashCode();

		const float kSpacing = 2;

		class Styles { public readonly GUIStyle background = new("CurveEditorBackground"); };
		static Styles s_Styles;

		public static float DefaultMaterialSurfaceDestinationFlagsHeight
		{
			get
			{
				var physicsMaterialHeight = EditorGUIUtility.singleLineHeight;
				var renderMaterialHeight = EditorGUIUtility.singleLineHeight;
				var surfaceDestinationFlagsHeight = SurfaceDestinationFlagsPropertyDrawer.DefaultHeight;
				return physicsMaterialHeight + renderMaterialHeight + kSpacing + surfaceDestinationFlagsHeight;
			}
		}

		static void GetAllUnityObjects(SerializedProperty prop, List<UnityEngine.Object> output)
		{
			Debug.Assert(prop.propertyType == SerializedPropertyType.ObjectReference);
			var propertyPath = prop.propertyPath;
			foreach (var target in prop.serializedObject.targetObjects)
			{
				if (target == null)
					continue;
				var targetSerializedObject = new SerializedObject(target);
				if (targetSerializedObject == null)
					continue;
				var targetProp = targetSerializedObject.FindProperty(propertyPath);
				if (targetProp == null)
					continue;
				var obj = targetProp.objectReferenceValue;
                if (obj == null)
                    continue;
				output.Add(obj);
			}
		}

		static IEnumerable<T> IterateOverPropertyUnityObjects<T>(SerializedProperty prop)
            where T : UnityEngine.Object
		{
            Debug.Assert(prop.propertyType == SerializedPropertyType.ObjectReference);
            var propertyPath = prop.propertyPath;
			foreach (var target in prop.serializedObject.targetObjects)
			{
				if (target == null)
					continue;
				var targetSerializedObject = new SerializedObject(target);
				if (targetSerializedObject == null)
					continue;
				var targetRenderMaterialProp = targetSerializedObject.FindProperty(propertyPath);
				if (targetRenderMaterialProp == null)
                    continue;
				yield return targetRenderMaterialProp.objectReferenceValue as T;
			}
		}

		static IEnumerable<T> IterateOverPropertyClasses<T>(SerializedProperty prop)
			where T : class
		{
			Debug.Assert(prop.propertyType == SerializedPropertyType.ManagedReference);
			var propertyPath = prop.propertyPath;
			foreach (var target in prop.serializedObject.targetObjects)
			{
				if (target == null)
					continue;
				var targetSerializedObject = new SerializedObject(target);
				if (targetSerializedObject == null)
					continue;
				var targetRenderMaterialProp = targetSerializedObject.FindProperty(propertyPath);
				if (targetRenderMaterialProp == null)
					continue;
				yield return targetRenderMaterialProp.managedReferenceValue as T;
			}
		}

		public static void UpdateChiselMaterials(SerializedProperty prop)
		{
			bool modified = false;
			Debug.Assert(prop.propertyType == SerializedPropertyType.Generic);
            if (prop.serializedObject.targetObjects.Length == 1)
            {
                if (prop.boxedValue is ChiselMaterial)
                {
                    var chiselMaterial = (ChiselMaterial)prop.boxedValue;
                    if (chiselMaterial.Update())
                    {
                        prop.boxedValue = chiselMaterial;
                        prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                        modified = true;
                    }
                }
            } else
            {
                var propertyPath = prop.propertyPath;
                foreach (var target in prop.serializedObject.targetObjects)
                {
                    if (target == null)
                        continue;
                    var targetSerializedObject = new SerializedObject(target);
                    if (targetSerializedObject == null)
                        continue;
                    var targetProp = targetSerializedObject.FindProperty(propertyPath);
                    if (targetProp == null)
                        continue;
                    if (targetProp.boxedValue is not ChiselMaterial)
                        continue;
                    var chiselMaterial = (ChiselMaterial)targetProp.boxedValue;
                    if (chiselMaterial.Update())
                    {
                        targetProp.boxedValue = chiselMaterial;
                        targetProp.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                        modified = true;
                    }
                }
            }
            if (modified)
            {
				prop.serializedObject.UpdateIfRequiredOrScript();
			}
		}

        readonly static GUIContent kAddMaterialMetadataContent = new ("Add Chisel metadata to Material");


        readonly static List<UnityEngine.Object> s_AllChiselSurfaces = new();

		// TODO: move methods like this in a separate file
		public static void ShowMaterialProp(Rect position, SerializedProperty chiselMaterialProp)
		{
            var previewSize = position.height;
			var lineHeight = EditorGUIUtility.singleLineHeight;
			var renderMaterialPropRect = new Rect(position.x, position.y, position.width, lineHeight);
			var surfaceDestinationFlagsPropRect = new Rect(position.x, position.y + lineHeight + kSpacing, position.width, previewSize - lineHeight + kSpacing);

			var surfacePropRect = position;
			var physicsRect = position;

			var renderMaterialProp = chiselMaterialProp.FindPropertyRelative(ChiselMaterial.kMaterialFieldName);
			var showMaterial = (position.width > 260 && renderMaterialProp != null);
			if (showMaterial)
			{
				renderMaterialPropRect.xMin += previewSize + kSpacing;
				surfaceDestinationFlagsPropRect.xMin += previewSize + kSpacing;

				surfacePropRect.xMin += previewSize + kSpacing;
				surfacePropRect.yMin = renderMaterialPropRect.yMax;

				physicsRect.xMin += previewSize + kSpacing;
				physicsRect.yMin = surfacePropRect.yMax - lineHeight;
			} else
            if (renderMaterialProp != null)
			{
				surfacePropRect.yMin = renderMaterialPropRect.yMax;
				physicsRect.yMin = surfacePropRect.yMax - lineHeight;
			}

			SerializedProperty metadataProp = chiselMaterialProp.FindPropertyRelative(ChiselMaterial.kSurfaceMetadataFieldName);
			SerializedProperty surfaceDestinationFlagsProp = null, physicsMaterialProp = null;
			if (metadataProp != null)
			{
				if (metadataProp.serializedObject.targetObjects.Length == 1)
				{
                    if (metadataProp.objectReferenceValue != null)
                    {
                        var serializedObject = new SerializedObject(metadataProp.objectReferenceValue);
                        surfaceDestinationFlagsProp = serializedObject.FindProperty(ChiselSurfaceMetadata.kDestinationFlagsFieldName);
                        physicsMaterialProp = serializedObject.FindProperty(ChiselSurfaceMetadata.kPhysicsMaterialFieldName);
                    }
				} else
                { 
				    s_AllChiselSurfaces.Clear();
                    GetAllUnityObjects(metadataProp, s_AllChiselSurfaces);
                    if (s_AllChiselSurfaces.Count > 0)
				    {
					    var serializedObject = new SerializedObject(s_AllChiselSurfaces.ToArray());
                        serializedObject.UpdateIfRequiredOrScript();
                        surfaceDestinationFlagsProp = serializedObject.FindProperty(ChiselSurfaceMetadata.kDestinationFlagsFieldName);
                        physicsMaterialProp = serializedObject.FindProperty(ChiselSurfaceMetadata.kPhysicsMaterialFieldName);
                    }
                }
			}

			var prevIndent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;
			{
				if (renderMaterialProp != null) EditorGUI.PropertyField(renderMaterialPropRect, renderMaterialProp, GUIContent.none, false);
                if (renderMaterialProp != null)
                { 
				    if (surfaceDestinationFlagsProp != null)
				    {
                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUI.PropertyField(surfacePropRect, surfaceDestinationFlagsProp, GUIContent.none, true);
						    using (new EditorGUI.DisabledScope(true))
						    {
							    if (physicsMaterialProp != null)
							    {
								    EditorGUI.PropertyField(physicsRect, physicsMaterialProp, GUIContent.none, true);
							    } else
							    {
									EditorGUI.ObjectField(physicsRect, GUIContent.none, Core.ChiselProjectSettings.DefaultPhysicsMaterial, typeof(PhysicsMaterial), false);
							    }
						    }
					    }
                    } else
                    {
                        if (renderMaterialProp.objectReferenceValue != null)
                        {
                            if (GUI.Button(surfacePropRect, kAddMaterialMetadataContent))
                            {
                                foreach (var material in IterateOverPropertyUnityObjects<Material>(renderMaterialProp))
                                {
                                    if (material != null &&
                                        material.HasMetadataOfType<ChiselSurfaceMetadata>())
                                        continue;

                                    material.AddMetadataOfType<ChiselSurfaceMetadata>();
                                }
                            }
                        }
                    }
                }
			}
			EditorGUI.indentLevel = prevIndent;

			// Render material preview
			if (showMaterial)
			{
				var materialPreviewRect = new Rect(position.x, position.y, previewSize, previewSize);
				s_Styles ??= new Styles();
				OnPreviewGUI(materialPreviewRect, s_Styles.background, renderMaterialProp.objectReferenceValue as Material);
			}
		}

		static Vector2 s_PreviewDir = new(-120, 20);

		public static Vector2 Drag2D(Vector2 scrollPosition, Rect position)
		{
			int id = GUIUtility.GetControlID(kSliderHash, FocusType.Passive);
			Event evt = Event.current;
			switch (evt.GetTypeForControl(id))
			{
				case EventType.MouseDown:
					{
						if (position.Contains(evt.mousePosition) && position.width > 10)
						{
							GUIUtility.hotControl = id;
							evt.Use();
							EditorGUIUtility.SetWantsMouseJumping(1);
						}
						break;
					}
				case EventType.MouseDrag:
					{
						if (GUIUtility.hotControl != id)
							break;

						scrollPosition -= evt.delta * (evt.shift ? 3 : 1) / Mathf.Min(position.width, position.height) * 140.0f;
						scrollPosition.y = Mathf.Clamp(scrollPosition.y, -90, 90);
						evt.Use();
						break;
					}
				case EventType.MouseUp:
					{
						if (GUIUtility.hotControl == id)
							GUIUtility.hotControl = 0;
						EditorGUIUtility.SetWantsMouseJumping(0);
						break;
					}
			}
			return scrollPosition;
		}

		// Clicked as the material's field is: once pings the material, twice opens it; a right click picks the preview's shape
		static bool PreviewClicks(Rect region, Material material)
		{
			var evt = Event.current;
			if (!region.Contains(evt.mousePosition))
				return false;
			if (evt.type == EventType.MouseDown && evt.button == 0 && evt.clickCount == 2 && material)
			{
				AssetDatabase.OpenAsset(material);
				evt.Use();
				GUIUtility.ExitGUI();
			}
			if (evt.type == EventType.MouseDown && evt.button == 0 && evt.clickCount == 1)
				s_PreviewClick = evt.mousePosition;
			if (evt.type == EventType.MouseUp && evt.button == 0 && s_PreviewClick.HasValue &&
				(evt.mousePosition - s_PreviewClick.Value).sqrMagnitude < 9 && material)
				EditorGUIUtility.PingObject(material);
			if (evt.type == EventType.MouseUp)
				s_PreviewClick = null;
			if (evt.type == EventType.ContextClick)
			{
				var menu = new GenericMenu();
				foreach (PreviewMesh mesh in System.Enum.GetValues(typeof(PreviewMesh)))
				{
					var chosen = mesh;
					menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(mesh.ToString())), PreviewTextureManager.Mesh == mesh, () => PreviewTextureManager.Mesh = chosen);
				}
				menu.ShowAsContext();
				evt.Use();
				return true;
			}
			return false;
		}

		static Vector2? s_PreviewClick;

		public static bool OnPreviewGUI(Rect region, GUIStyle background, Material material)
		{
			PreviewClicks(region, material);
			var newPreviewDir = Drag2D(s_PreviewDir, region);
			if (newPreviewDir != s_PreviewDir)
				s_PreviewDir = newPreviewDir;

			var texture = PreviewTextureManager.GetPreviewTexture(region, s_PreviewDir, material);
			if (!texture)
			{
				if (Event.current.type == EventType.Repaint)
					background.Draw(region, false, false, false, false);
				return false;
			}
			GUI.DrawTexture(region, texture, ScaleMode.StretchToFill, false);
			return true;
		}

		public void OnSceneSettingsGUI(SceneView sceneView)
        {
            if (!initialized)
                UpdateSurfaceSelection();
            else
                UpdateAllSurfaces();

            if (PreviewTextureManager.Update())
                sceneView.Repaint();

            if (surfaces.Length == 0)
            {
                GUILayout.Box("No surfaces selected. Please select a surface", GUILayout.ExpandWidth(true));
            }

            if (!initialized)
                return;

			serializedObject.UpdateIfRequiredOrScript();
            var selectedReferences = new List<SurfaceReference>(surfaces.Length);
            foreach (var item in surfaces)
                selectedReferences.Add(item.SurfaceReference);
            var wasWorld = ChiselTextureLockEdit.WorldSpaceOf(selectedReferences);
            EditorGUI.BeginChangeCheck();
			{
				UpdateChiselMaterials(chiselMaterialProp);

				var desiredHeight   = DefaultMaterialSurfaceDestinationFlagsHeight;
                var position        = EditorGUILayout.GetControlRect(false, desiredHeight);
				ShowMaterialProp(position, chiselMaterialProp);

				var prevLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 85;
                position = EditorGUILayout.GetControlRect(true, SurfaceFlagsPropertyDrawer.DefaultHeight);
                if (surfaceFlagsProp != null)
                    EditorGUI.PropertyField(position, surfaceFlagsProp, SurfaceDetailsPropertyDrawer.kSurfaceFlagsContents, true);

                // a hotspot material's placement in the rows a planar mapping has
                var layout = HotspotTransformGUI.LayoutOf(surfaces[0].Surface.RenderMaterial);
                if (layout != null && hotspotProp != null)
                {
                    position = EditorGUILayout.GetControlRect(true, HotspotTransformGUI.Height);
                    var references = new List<SurfaceReference>(surfaces.Length);
                    foreach (var item in surfaces)
                        references.Add(item.SurfaceReference);
                    var material = surfaces[0].Surface.RenderMaterial;
                    HotspotTransformGUI.OnGUI(position, hotspotProp, layout, true, ChiselHotspotGroupEdit.MostVariations(references),
                                              () => HotspotTransformGUI.TexturesOf(material, layout), () => ChiselHotspotGroupEdit.FitsOf(references[0], layout),
                                              references[0].descriptionIndex);
                    ConnectionGUI();
                } else
                {
                    position = EditorGUILayout.GetControlRect(true, UVMatrixPropertyDrawer.DefaultHeight);
                    if (UV0Prop != null)
                        EditorGUI.PropertyField(position, UV0Prop, SurfaceDetailsPropertyDrawer.kUV0Contents, true);
                }

                EditorGUIUtility.labelWidth = prevLabelWidth;
            }
            if (EditorGUI.EndChangeCheck() && undoableObjects.Length > 0)
            {
                Undo.RegisterCompleteObjectUndo(undoableObjects, undoableObjects.Length > 1 ? "Modified materials" : "Modified material");
                serializedObject.ApplyModifiedProperties();
                foreach (var item in surfaces)
                    item.Store();
                var edited = new List<SurfaceReference>(surfaces.Length);
                foreach (var item in surfaces)
                    edited.Add(item.SurfaceReference);
                // Lock texture to object toggled: the texture stays where it is
                if (ChiselTextureLockEdit.KeepLook(edited, wasWorld, undoableObjects.Length > 1 ? "Modified materials" : "Modified material"))
                {
                    foreach (var item in surfaces)
                        item.Load();
                    serializedObject.Update();
                }
                ChiselHotspotGroupEdit.Spread(edited, undoableObjects.Length > 1 ? "Modified materials" : "Modified material");
            }
        }

        static readonly GUIContent kHotspotConnectionContents = new("Connection", "How the two selected surfaces are mapped where they touch: as one surface (Connected), apart (Disconnected), or as touching coplanar surfaces are by themselves (Automatic)");
        static readonly string[]   kHotspotConnectionNames    = { "Automatic", "Connected", "Disconnected" };
        static readonly GUIContent kHotspotBorderContents     = new("Border", "The border along the edge the two selected surfaces share: none where they are mapped as one and a border where each has its own (Automatic), a border (Shown), or none (Hidden)");
        static readonly string[]   kHotspotBorderNames        = { "Automatic", "Shown", "Hidden" };

        // With two surfaces selected: whether they are mapped as one where they touch
        void ConnectionGUI()
        {
            if (surfaces.Length != 2)
                return;
            var a = surfaces[0].SurfaceReference; var b = surfaces[1].SurfaceReference;
            var model = ChiselHotspotLinks.ModelOfSurfaces(a, b);
            if (!model)
                return;
            var state  = ChiselHotspotLinks.StateOf(a, b);
            var chosen = (ChiselHotspotLinkState)EditorGUILayout.Popup(kHotspotConnectionContents, (int)state, kHotspotConnectionNames);
            if (chosen != state)
            {
                Undo.RecordObject(model, "Hotspot Connection");
                ChiselHotspotLinks.SetConnection(a, b, chosen);
                EditorUtility.SetDirty(model);
            }
            var border       = ChiselHotspotLinks.BorderOf(a, b);
            var chosenBorder = (ChiselHotspotBorderState)EditorGUILayout.Popup(kHotspotBorderContents, (int)border, kHotspotBorderNames);
            if (chosenBorder != border)
            {
                Undo.RecordObject(model, "Hotspot Border");
                ChiselHotspotLinks.SetBorder(a, b, chosenBorder);
                EditorUtility.SetDirty(model);
            }
        }

        
        #region Snapping

        internal static SnapSettings s_ToolSnapOverrides = SnapSettings.All;
        internal static SnapSettings CurrentSnapSettings { get { return Snapping.SnapSettings & s_ToolSnapOverrides; } }

        internal static bool s_PointHasSnapped     = false;
        internal static bool s_ForceVertexSnapping = false;

        const float kMinSnapDistance    = 0.5f;
        const float kDistanceEpsilon    = 0.0006f;
        const float kAlignmentEpsilon   = 1 - kDistanceEpsilon;


        struct UVSnapState
        {
            public SnapSettings   SnapCause;
            public Vector3          SnapPosition;
            public Vector3          StartEdge;
            public Vector3          EndEdge;
            public SnapAxis         SnapGridAxis;
        }

        static UVSnapState snapState = new UVSnapState();

        static void SnapGridIntersection(SnapSettings snapSettings, SurfaceReference surfaceReference, Vector3 intersectionPoint, float preferenceFactor, ref Vector3 snappedPoint, ref float bestDist, ref UVSnapState snapState)
        {
            if ((snapSettings & SnapSettings.UVGeometryGrid) == SnapSettings.None)
                return;

            var worldPlane = surfaceReference?.WorldPlane ?? null;
            if (worldPlane == null ||
                !worldPlane.HasValue)
                return;

            var worldPlaneValue = worldPlane.Value;

            var grid				= Chisel.Editors.Grid.DefaultGrid;
            var gridSnappedPoint	= Snapping.SnapPoint(intersectionPoint, grid);

            var xAxis       = grid.Right;
            var yAxis       = grid.Up;
            var zAxis       = grid.Forward;
            var snapAxis    = Axis.X | Axis.Y | Axis.Z;
            if (Mathf.Abs(Vector3.Dot(xAxis, worldPlaneValue.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.X;
            if (Mathf.Abs(Vector3.Dot(yAxis, worldPlaneValue.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.Y;
            if (Mathf.Abs(Vector3.Dot(zAxis, worldPlaneValue.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.Z;
            if (Mathf.Abs(worldPlaneValue.GetDistanceToPoint(gridSnappedPoint)) < kDistanceEpsilon)
            {
                var abs_dist = (gridSnappedPoint - intersectionPoint).sqrMagnitude * preferenceFactor;
                if (abs_dist < bestDist)
                {
                    bestDist = abs_dist;
                    snapState.SnapCause = SnapSettings.UVGeometryGrid;
                    snapState.SnapPosition = gridSnappedPoint;
                    snappedPoint = gridSnappedPoint;
                }
            } else
            {
                float dist;
                var ray = new Ray(gridSnappedPoint, xAxis);
                if ((snapAxis & Axis.X) != Axis.None && worldPlaneValue.SignedRaycast(ray, out dist))
                {
                    var planePoint = ray.GetPoint(dist);
                    var abs_dist = (planePoint - intersectionPoint).sqrMagnitude * preferenceFactor;
                    if (abs_dist < bestDist)
                    {
                        bestDist = abs_dist;
                        snapState.SnapCause = SnapSettings.UVGeometryGrid;
                        snapState.SnapPosition = planePoint;
                        snappedPoint = planePoint;
                    }
                }
                ray.direction = yAxis;
                if ((snapAxis & Axis.Y) != Axis.None && worldPlaneValue.SignedRaycast(ray, out dist))
                {
                    var planePoint = ray.GetPoint(dist);
                    var abs_dist = (planePoint - intersectionPoint).sqrMagnitude * preferenceFactor;
                    if (abs_dist < bestDist)
                    {
                        bestDist = abs_dist;
                        snapState.SnapCause = SnapSettings.UVGeometryGrid;
                        snapState.SnapPosition = planePoint;
                        snappedPoint = planePoint;
                    }
                }
                ray.direction = zAxis;
                if ((snapAxis & Axis.Z) != Axis.None && worldPlaneValue.SignedRaycast(ray, out dist))
                {
                    var planePoint = ray.GetPoint(dist);
                    var abs_dist = (planePoint - intersectionPoint).sqrMagnitude * preferenceFactor;
                    if (abs_dist < bestDist)
                    {
                        bestDist = abs_dist;
                        snapState.SnapCause = SnapSettings.UVGeometryGrid;
                        snapState.SnapPosition = planePoint;
                        snappedPoint = planePoint;
                    }
                }
            }
        }

        static bool SnapSurfaceVertices(SnapSettings snapSettings, SurfaceReference surfaceReference, Vector3 intersectionPoint, float preferenceFactor, ref Vector3 snappedPoint, ref float bestDist, ref UVSnapState snapState)
        {
            if (surfaceReference == null)
                return false;

            if ((snapSettings & SnapSettings.UVGeometryVertices) == SnapSettings.None)
                return false;

            var brushMeshBlob = surfaceReference.BrushMeshBlob;
            if (!brushMeshBlob.IsCreated)
                return false;

            ref var brushMesh = ref brushMeshBlob.Value;
            
            var localToWorldSpace	= surfaceReference.LocalToWorldSpace;
            
            var bestDistSqr			= float.PositiveInfinity;
            var bestVertex			= snappedPoint;

            // TODO: when we use new CSG algorithm, use its intersection loops instead
            var outline = ChiselOutlineRenderer.GetSurfaceWireframe(surfaceReference);
            if (outline != null)
            {
                var vertices = outline.Vertices;
            
                for (int v = 0; v < vertices.Length; v++)
                {
                    var worldSpaceVertex = localToWorldSpace.MultiplyPoint(vertices[v]);
                    var dist_sqr         = (worldSpaceVertex - intersectionPoint).sqrMagnitude;
                    if (dist_sqr < bestDistSqr)
                    {
                        bestDistSqr = dist_sqr;
                        bestVertex = worldSpaceVertex;
                    }
                }
            } else
            {
                Debug.Assert(surfaceReference.surfaceIndex >= 0 && surfaceReference.surfaceIndex < brushMesh.polygons.Length);

                var polygon             = brushMesh.polygons[surfaceReference.surfaceIndex];
                ref var edges           = ref brushMesh.halfEdges;
                ref var localVertices   = ref brushMesh.localVertices;
                var firstEdge           = polygon.firstEdge;
                var lastEdge            = firstEdge + polygon.edgeCount;
            
                for (int e = firstEdge; e < lastEdge; e++)
                {
                    var worldSpaceVertex = localToWorldSpace.MultiplyPoint(localVertices[edges[e].vertexIndex]);
                    var dist_sqr         = (worldSpaceVertex - intersectionPoint).sqrMagnitude;
                    if (dist_sqr < bestDistSqr)
                    {
                        bestDistSqr = dist_sqr;
                        bestVertex = worldSpaceVertex;
                    }
                }
            }

            if (bestDistSqr >= 0.25f)
                return false;

            var closestVertexDistance = bestDistSqr * preferenceFactor;
            if (closestVertexDistance < bestDist)
            {
                bestDist = closestVertexDistance;
                snapState.SnapCause = SnapSettings.UVGeometryVertices;
                snapState.SnapPosition = bestVertex;
                snappedPoint = bestVertex;
                return true;
            }
            return false;
        }

        static SnapAxis GetSnappedAxi(Grid grid, Vector3 planePoint)
        {
            var gridPlanePoint = planePoint - grid.Center;
            var axisX = Math.Abs(Vector3.Dot(gridPlanePoint, grid.Right  ) % Snapping.MoveSnappingSteps.x) < kDistanceEpsilon;
            var axisY = Math.Abs(Vector3.Dot(gridPlanePoint, grid.Up     ) % Snapping.MoveSnappingSteps.y) < kDistanceEpsilon;
            var axisZ = Math.Abs(Vector3.Dot(gridPlanePoint, grid.Forward) % Snapping.MoveSnappingSteps.z) < kDistanceEpsilon;

            return ((axisX) ? SnapAxis.X : SnapAxis.None) |
                   ((axisY) ? SnapAxis.Y : SnapAxis.None) |
                   ((axisZ) ? SnapAxis.Z : SnapAxis.None);
        }

        static bool SnapSurfaceEdges(SnapSettings snapSettings, SurfaceReference surfaceReference, Vector3 intersectionPoint, float preferenceFactor, ref Vector3 snappedPoint, ref float bestDist, ref UVSnapState snapState)
        {
            if (surfaceReference == null)
                return false;

            if ((snapSettings & SnapSettings.UVGeometryEdges) == SnapSettings.None)
                return false;

            var brushMeshBlob = surfaceReference.BrushMeshBlob;
            if (!brushMeshBlob.IsCreated)
                return false;

            ref var brushMesh = ref brushMeshBlob.Value;
            var localToWorldSpace	= surfaceReference.LocalToWorldSpace;

            Debug.Assert(surfaceReference.surfaceIndex >= 0 && surfaceReference.surfaceIndex < brushMesh.polygons.Length);

            var grid            = Chisel.Editors.Grid.DefaultGrid;
            var xAxis           = grid.Right;
            var yAxis           = grid.Up;
            var zAxis           = grid.Forward;
            var surfacePlane    = surfaceReference.WorldPlane.Value;

            var snapAxis    = Axis.X | Axis.Y | Axis.Z;
            if (Mathf.Abs(Vector3.Dot(xAxis, surfacePlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.X;
            if (Mathf.Abs(Vector3.Dot(yAxis, surfacePlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.Y;
            if (Mathf.Abs(Vector3.Dot(zAxis, surfacePlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.Z;

            ref var polygons                = ref brushMesh.polygons;
            var polygon                     = polygons[surfaceReference.surfaceIndex];
            ref var halfEdgePolygonIndices  = ref brushMesh.halfEdgePolygonIndices;
            ref var halfEdges               = ref brushMesh.halfEdges;
            ref var localVertices           = ref brushMesh.localVertices;
            ref var localPlanes             = ref brushMesh.localPlanes;
            var firstEdge                   = polygon.firstEdge;
            var lastEdge                    = firstEdge + polygon.edgeCount;

            bool found = false;
            if ((CurrentSnapSettings & SnapSettings.UVGeometryGrid) != SnapSettings.None)
            {
                var minSnappedPoint = snappedPoint - grid.Center;
                var dotX            = Vector3.Dot(minSnappedPoint, grid.Right);
                var dotY            = Vector3.Dot(minSnappedPoint, grid.Up);
                var dotZ            = Vector3.Dot(minSnappedPoint, grid.Forward);

                dotX = Mathf.Round(dotX / Snapping.MoveSnappingSteps.x) * Snapping.MoveSnappingSteps.x;
                dotY = Mathf.Round(dotY / Snapping.MoveSnappingSteps.y) * Snapping.MoveSnappingSteps.y;
                dotZ = Mathf.Round(dotZ / Snapping.MoveSnappingSteps.z) * Snapping.MoveSnappingSteps.z;

                var axisPlaneX  = new Plane(grid.Right,   -dotX);
                var axisPlaneY  = new Plane(grid.Up,      -dotY);
                var axisPlaneZ  = new Plane(grid.Forward, -dotZ);
                for (int e = firstEdge; e < lastEdge; e++)
                {
                    var twin            = halfEdges[e].twinIndex;
                    var polygonIndex    = halfEdgePolygonIndices[twin];

                    var surfaceIndex    = polygonIndex; // FIXME: throughout the code we're making assumptions about polygonIndices being the same as surfaceIndices, 
                                                        //        this needs to be fixed

                    var localPlaneVector = localPlanes[surfaceIndex];
                    var edgeLocalPlane   = new Plane(localPlaneVector.xyz, localPlaneVector.w);
                    var edgeWorldPlane   = localToWorldSpace.TransformPlane(edgeLocalPlane);
                    var edgeDirection    = Vector3.Cross(surfacePlane.normal, edgeWorldPlane.normal);

                    var edgeSnapAxis = snapAxis;
                    if (Mathf.Abs(Vector3.Dot(xAxis, edgeDirection)) >= kAlignmentEpsilon) edgeSnapAxis &= ~Axis.X;
                    if (Mathf.Abs(Vector3.Dot(yAxis, edgeDirection)) >= kAlignmentEpsilon) edgeSnapAxis &= ~Axis.Y;
                    if (Mathf.Abs(Vector3.Dot(zAxis, edgeDirection)) >= kAlignmentEpsilon) edgeSnapAxis &= ~Axis.Z;

                    if (edgeSnapAxis == Axis.None)
                        continue;

                    if ((edgeSnapAxis & Axis.X) != Axis.None)
                    {
                        var planePoint  = Chisel.Core.PlaneExtensions.Intersection(surfacePlane, edgeWorldPlane, axisPlaneX);
                        if (!float.IsNaN(planePoint.x) && !float.IsNaN(planePoint.y) && !float.IsNaN(planePoint.z) &&
                            Mathf.Abs(surfacePlane  .GetDistanceToPoint(planePoint)) < kDistanceEpsilon &&
                            Mathf.Abs(edgeWorldPlane.GetDistanceToPoint(planePoint)) < kDistanceEpsilon &&
                            Mathf.Abs(axisPlaneX    .GetDistanceToPoint(planePoint)) < kDistanceEpsilon)
                        { 
                            var abs_dist = (planePoint - intersectionPoint).sqrMagnitude * preferenceFactor;
                            if (abs_dist < bestDist)
                            {
                                bestDist = abs_dist;
                                snappedPoint = planePoint;
                                snapState.SnapCause     = SnapSettings.UVGeometryEdges;
                                snapState.SnapPosition  = planePoint;
                                snapState.StartEdge     = localToWorldSpace.MultiplyPoint(localVertices[brushMesh.halfEdges[firstEdge + ((e - firstEdge + polygon.edgeCount - 1) % polygon.edgeCount)].vertexIndex]);
                                snapState.EndEdge       = localToWorldSpace.MultiplyPoint(localVertices[brushMesh.halfEdges[e].vertexIndex]);                                
                                snapState.SnapGridAxis  = GetSnappedAxi(grid, planePoint);
                                found = true;
                            }
                        }
                    }
                    if ((edgeSnapAxis & Axis.Y) != Axis.None)
                    {
                        var planePoint  = Chisel.Core.PlaneExtensions.Intersection(surfacePlane, edgeWorldPlane, axisPlaneY);
                        if (!float.IsNaN(planePoint.x) && !float.IsNaN(planePoint.y) && !float.IsNaN(planePoint.z) &&
                            Mathf.Abs(surfacePlane  .GetDistanceToPoint(planePoint)) < kDistanceEpsilon &&
                            Mathf.Abs(edgeWorldPlane.GetDistanceToPoint(planePoint)) < kDistanceEpsilon &&
                            Mathf.Abs(axisPlaneY    .GetDistanceToPoint(planePoint)) < kDistanceEpsilon)
                        {
                            var abs_dist = (planePoint - intersectionPoint).sqrMagnitude * preferenceFactor;
                            if (abs_dist < bestDist)
                            {
                                bestDist = abs_dist;
                                snappedPoint = planePoint;
                                snapState.SnapCause     = SnapSettings.UVGeometryEdges;
                                snapState.SnapPosition  = planePoint;
                                snapState.StartEdge     = localToWorldSpace.MultiplyPoint(localVertices[brushMesh.halfEdges[firstEdge + ((e - firstEdge + polygon.edgeCount - 1) % polygon.edgeCount)].vertexIndex]);
                                snapState.EndEdge       = localToWorldSpace.MultiplyPoint(localVertices[brushMesh.halfEdges[e].vertexIndex]);
                                snapState.SnapGridAxis  = GetSnappedAxi(grid, planePoint);
                                found = true;
                            }
                        }
                    }
                    if ((edgeSnapAxis & Axis.Z) != Axis.None)
                    {
                        var planePoint  = Chisel.Core.PlaneExtensions.Intersection(surfacePlane, edgeWorldPlane, axisPlaneZ);
                        if (!float.IsNaN(planePoint.x) && !float.IsNaN(planePoint.y) && !float.IsNaN(planePoint.z) &&
                            Mathf.Abs(surfacePlane  .GetDistanceToPoint(planePoint)) < kDistanceEpsilon &&
                            Mathf.Abs(edgeWorldPlane.GetDistanceToPoint(planePoint)) < kDistanceEpsilon &&
                            Mathf.Abs(axisPlaneZ    .GetDistanceToPoint(planePoint)) < kDistanceEpsilon)
                        {
                            var abs_dist = (planePoint - intersectionPoint).sqrMagnitude * preferenceFactor;
                            if (abs_dist < bestDist)
                            {
                                bestDist = abs_dist;
                                snappedPoint = planePoint;
                                snapState.SnapCause     = SnapSettings.UVGeometryEdges;
                                snapState.SnapPosition  = planePoint;
                                snapState.StartEdge     = localToWorldSpace.MultiplyPoint(localVertices[brushMesh.halfEdges[firstEdge + ((e - firstEdge + polygon.edgeCount - 1) % polygon.edgeCount)].vertexIndex]);
                                snapState.EndEdge       = localToWorldSpace.MultiplyPoint(localVertices[brushMesh.halfEdges[e].vertexIndex]);
                                snapState.SnapGridAxis  = GetSnappedAxi(grid, planePoint);
                                found = true;
                            }
                        }
                    }
                }
            } else
            {
                var prevVertex = localToWorldSpace.MultiplyPoint(localVertices[halfEdges[lastEdge - 1].vertexIndex]);
                for (int e = firstEdge; e < lastEdge; e++)
                {
                    var localPlaneVector = localPlanes[halfEdgePolygonIndices[halfEdges[e].twinIndex]];
                    var edgeLocalPlane   = new Plane(localPlaneVector.xyz, localPlaneVector.w);
                    var edgeWorldPlane   = localToWorldSpace.TransformPlane(edgeLocalPlane);
                    
                    var currVertex      = localToWorldSpace.MultiplyPoint(localVertices[halfEdges[e].vertexIndex]);
                    var closestPoint    = Chisel.Core.MathExtensions.ProjectPointLine(intersectionPoint, prevVertex, currVertex);
                    var abs_dist        = (closestPoint - intersectionPoint).sqrMagnitude * preferenceFactor;
                    
                    if (abs_dist < bestDist)
                    {
                        bestDist = abs_dist;
                        snappedPoint = closestPoint;
                        snapState.SnapCause     = SnapSettings.UVGeometryEdges;
                        snapState.SnapPosition  = snappedPoint;
                        snapState.StartEdge     = prevVertex;
                        snapState.EndEdge       = currVertex;

                        found = true;
                    }

                    prevVertex = currVertex;
                }
            }
            return found;
        }

        static bool SelectedSnapIntersection(Vector2 mousePosition, Vector3 originalIntersectionPoint, out SurfaceReference surfaceReference, out Vector3 worldIntersectionPoint, ref UVSnapState snapState)
        {
            var bestDist = float.PositiveInfinity;

            worldIntersectionPoint = default;
            surfaceReference = null;

            foreach (var selectedSurface in ChiselSurfaceSelectionManager.Selection)
            {
                if (!selectedSurface.WorldPlane.HasValue)
                    continue;
                if (GetCurrentWorldClick(selectedSurface.WorldPlane.Value, mousePosition, out Vector3 currentWorldIntersection))
                {
                    currentWorldIntersection = SnapIntersection(currentWorldIntersection, selectedSurface, out s_PointHasSnapped, ref snapState);
                    if (s_PointHasSnapped)
                    {
                        var dist = (originalIntersectionPoint - currentWorldIntersection).sqrMagnitude;
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            worldIntersectionPoint = currentWorldIntersection;
                            surfaceReference = selectedSurface;
                        }
                    }
                }
            }
            return surfaceReference != null;
        }


        static Vector3 SnapIntersection(Vector3 intersectionPoint, SurfaceReference surfaceReference, out bool haveWeSnapped, ref UVSnapState snapState)
        {
            if (surfaceReference == null)
            {
                haveWeSnapped = false;
                return intersectionPoint;
            }

            // TODO: visualize what we're snapping against

            var bestDist			= float.PositiveInfinity;
            var snappedPoint		= intersectionPoint;
            var handleSize          = HandleUtility.GetHandleSize(intersectionPoint);
            // When holding V we force to ONLY and ALWAYS snap against vertices
            var snapSettings        = s_ForceVertexSnapping ? SnapSettings.UVGeometryVertices : CurrentSnapSettings;

            // snap to edges of surface that are closest to the intersection point
            SnapSurfaceEdges(snapSettings, surfaceReference, intersectionPoint, 1.25f, ref snappedPoint, ref bestDist, ref snapState);

            // Snap to closest point on grid
            SnapGridIntersection(snapSettings, surfaceReference, intersectionPoint, 1.5f, ref snappedPoint, ref bestDist, ref snapState);

            // snap to vertices of surface that are closest to the intersection point
            SnapSurfaceVertices(snapSettings, surfaceReference, intersectionPoint, 0.5f, ref snappedPoint, ref bestDist, ref snapState);
            SnapSurfaceVertices(snapSettings, surfaceReference, snappedPoint, 0.5f, ref snappedPoint, ref bestDist, ref snapState);

            // TODO: snap to UV space bounds (and more?)


            var gridSnappingenabled = (CurrentSnapSettings & SnapSettings.UVGeometryGrid) != SnapSettings.None;
            var minSnapDistance     = (s_ForceVertexSnapping || gridSnappingenabled) ? float.PositiveInfinity : (handleSize * kMinSnapDistance);

            if (bestDist < minSnapDistance)
            {
                haveWeSnapped = true;
                intersectionPoint = snappedPoint;
            } else
            {
                snapState.SnapCause = SnapSettings.None;
                haveWeSnapped = false;
            }

            return intersectionPoint;
        }

        internal static float SnapAngle(float rotatedAngle)
        {
            if (!Snapping.RotateSnappingEnabled)
                return rotatedAngle;
            return ((int)(rotatedAngle / Snapping.RotateSnappingStep)) * Snapping.RotateSnappingStep;
        }

        #endregion

        
        #region Tool Base
        static bool CanEnableTool(int id)
        {
            // Is another control enabled at the moment?
            if (GUIUtility.hotControl != 0)
                return false;
                        
            var evt = Event.current;
            // Is our tool currently the control nearest to the mouse?
            if ((UnityEditor.HandleUtility.nearestControl != id || evt.button != 0) &&
                (GUIUtility.keyboardControl != id || evt.button != 2))
                return false;
            return true;
        }

        internal static bool IsToolEnabled(int id)
        {
            // Is our control enabled at the moment?
            return GUIUtility.hotControl == id;
        }


        static void EnableTool(int id)
        {
            EditorGUIUtility.SetWantsMouseJumping(1);   // enable allowing the user to move the mouse over the bounds of the screen
            s_JumpedMousePosition = Event.current.mousePosition;  // remember the current mouse position so we can update it using Event.current.delta, 
                                                                // since mousePosition won't make sense any more when mouse jumping
            GUIUtility.hotControl = GUIUtility.keyboardControl = id; // set our tool as the active control
            Event.current.Use(); // make sure no-one else can use our event


            s_ToolSnapOverrides = SnapSettings.All;
            s_PointHasSnapped = false;
        }

        static void DisableTool()
        {
            EditorGUIUtility.SetWantsMouseJumping(0); // disable allowing the user to move the mouse over the bounds of the screen
            GUIUtility.hotControl = GUIUtility.keyboardControl = 0; // remove the active control so that the user can use another control
            Event.current.Use(); // make sure no-one else can use our event


            s_ToolSnapOverrides = SnapSettings.All;
            s_PointHasSnapped = false;
        }

        static void CancelToolInProgress()
        {
            DisableTool();
            Undo.RevertAllInCurrentGroup();
        }

        //[Shortcut(kToolShotcutName, ChiselKeyboardDefaults.FreeBuilderModeKey, ChiselKeyboardDefaults.FreeBuilderModeModifiers, displayName = kToolShotcutName)]
        //public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselExtrudedShapeGeneratorMode); }

        const float kMaxControlDistance = 3.0f;
        internal static bool SurfaceToolBase(int id, SelectionType selectionType, Rect dragArea)
        {
            // we only do tools when we do not use a modifier (shift, control etc.)
            if (selectionType != SelectionType.Replace)
                return false;

            var evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                {
                    // Unless something else is closer, make sure our tool is selected
                    HandleUtility.AddControl(id, kMaxControlDistance);
                    break;
                }

                case EventType.KeyDown:
                {
                    if (evt.keyCode == ChiselKeyboardDefaults.kCancelKey)
                    {
                        if (IsToolEnabled(id))
                        {
                            evt.Use();
                            break;
                        } else
                        {
                            if (ChiselSurfaceSelectionManager.HaveSelection)
                            {
                                evt.Use();
                                break;
                            }
                        }
                    }
                    if (!EditorGUIUtility.editingTextField)
                    {
                        if (evt.keyCode == KeyCode.V)
                        {
                            s_ForceVertexSnapping = true;
                            evt.Use();
                            break;
                        }
                    }
                    break;
                }
                case EventType.KeyUp:
                {
                    if (evt.keyCode == ChiselKeyboardDefaults.kCancelKey)
                    {
                        if (IsToolEnabled(id))
                        {
                            CancelToolInProgress();
                            evt.Use();
                            GUIUtility.ExitGUI(); // avoids a nullreference exception in sceneview
                            break;
                        } else
                        {
                            // TODO: this works, but something else is deselecting our gameobjects, disabling the editortool?
                            if (ChiselSurfaceSelectionManager.HaveSelection)
                            {
                                ChiselSurfaceSelectionManager.DeselectAll();
                                evt.Use();
                                GUIUtility.ExitGUI(); // avoids a nullreference exception in sceneview
                                break;
                            }
                        }
                    }
                    if (s_ForceVertexSnapping && evt.keyCode == KeyCode.V)
                    {
                        s_ForceVertexSnapping = false;
                        if (!EditorGUIUtility.editingTextField)
                            evt.Use();
                        break;
                    }
                    break;
                }

                case EventType.MouseMove:
                {
                    // In case we somehow missed a MouseUp event, we reset this bool
                    MouseIsDown = false;
                    break;
                }
                case EventType.MouseDown:
                {
                    // We can only use a tool when the mouse cursor is inside the draggable scene area
                    if (!dragArea.Contains(evt.mousePosition))
                        return false;

                    // We can only use a tool when we're hovering over a surfaces
                    if (s_HoverSurfaces == null || s_HoverSurfaces.Count == 0)
                        return false;

                    if (!CanEnableTool(id))
                        break;

                    // We want to be able to tell the difference between dragging and clicking,
                    // so we keep track if we dragged or not. In this case we haven't started dragging yet.
                    ToolIsDragging = false;
                    MouseIsDown = true;

                    EnableTool(id); 
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (!IsToolEnabled(id))
                        break;

                    if (!ToolIsDragging)
                    {
                        // If we haven't dragged the tool yet, check if the surface underneath 
                        // the mouse is selected or not, if it isn't: select it exclusively
                        if (!ChiselSurfaceSelectionManager.IsAnySelected(s_HoverSurfaces))
                            ClickSelection(dragArea, selectionType);
                    }

                    // In the tool specific code, calling StartToolDragging will set ToolIsDragging to true, 
                    // which will allow us to tell the difference between clicking and dragging.

                    break;
                }
                case EventType.MouseUp:
                {
                    if (!IsToolEnabled(id))
                        break;
                    
                    MouseIsDown = false;

                    // We want to be able to tell the difference between clicking and dragging, 
                    // so we use ToolIsDragging here to determine if we clicked.
                    if (!ToolIsDragging)
                    {
                        // If we clicked on the surface, instead of dragged it, just click select it
                        ClickSelection(dragArea, selectionType);
                    }

                    ToolIsDragging = false;

                    DisableTool();
                    ResetSelection();
                    break;
                }
                case EventType.Repaint:
                {
                    RenderIntersection();
                    break;
                }
            }
            return true;
        }

        static SurfaceReference     s_StartSurfaceReference;
        static Vector2              s_JumpedMousePosition;

        // The surface the drag started on
        internal static SurfaceReference StartSurfaceReference => s_StartSurfaceReference;

        // The next or previous variation for every selected hotspot surface that has another, from the keyboard
        // (editor workflow: cycle through the candidates)
        const string kNextVariationShortcutName     = "Next Hotspot Variation";
        const string kPreviousVariationShortcutName = "Previous Hotspot Variation";
        [Shortcut(ChiselKeyboardDefaults.ShortCutEditModeBase + kNextVariationShortcutName, typeof(SceneView), KeyCode.N, ShortcutModifiers.Alt, displayName = kNextVariationShortcutName)]
        static void NextHotspotVariation() { StepHotspotVariation(1, kNextVariationShortcutName); }

        [Shortcut(ChiselKeyboardDefaults.ShortCutEditModeBase + kPreviousVariationShortcutName, typeof(SceneView), KeyCode.N, ShortcutModifiers.Alt | ShortcutModifiers.Shift, displayName = kPreviousVariationShortcutName)]
        static void PreviousHotspotVariation() { StepHotspotVariation(-1, kPreviousVariationShortcutName); }

        static void StepHotspotVariation(int step, string undoName)
        {
            var selected = ChiselSurfaceSelectionManager.Selection;
            if (selected == null || selected.Count == 0)
                return;
            Undo.RecordObjects(ChiselSurfaceSelectionManager.SelectedNodes.ToArray(), undoName);
            foreach (var surface in selected)
            {
                if (!IsHotspotMapped(surface))
                    continue;
                var details = surface.SurfaceDetails;
                var variations = ChiselHotspotGroupEdit.VariationsOf(surface);
                if (variations >= 0 ? variations <= 1 : details.hotspot.pinnedTile != 0)
                    continue;
                details.hotspot.seed = HotspotTransformGUI.StepSeed(details.hotspot.seed, surface.descriptionIndex, variations, step);
                surface.SurfaceDetails = details;
            }
            ChiselHotspotGroupEdit.Spread(selected.ToList(), undoName);
            if (s_Instance)
                s_Instance.UpdateAllSurfaces();
        }

        // Whether a surface's material maps it with a hotspot layout
        internal static bool IsHotspotMapped(SurfaceReference surface)
        {
            var material = surface?.RenderMaterial;
            var metadata = material != null ? material.GetMetadataOfType<ChiselSurfaceMetadata>() : null;
            return metadata != null && metadata.hotspotLayout != null;
        }

        static bool GridSnapping => (CurrentSnapSettings & SnapSettings.UVGeometryGrid) != SnapSettings.None;

        // How a hotspot mapped surface's repeats lie, as it was last built (its render buffer's section input): the
        // texture's right and up in its model's space, one repeat along each in units, and its pixels; false when it has
        // no repeats to move (a tile fitted once)
        static bool HotspotRepeatsOf(SurfaceReference surface, out Matrix4x4 worldToModel, out Vector3 right, out Vector3 up, out Vector2 period, out Vector2 pixels)
        {
            worldToModel = Matrix4x4.identity; right = up = default; period = pixels = default;
            if (surface == null || !surface.node || !surface.brush.Valid)
                return false;
            var model = surface.node.hierarchyItem.Model;
            if (!model || !model.Node.Valid)
                return false;
            var lookups = ChiselTreeLookup.Value;
            if (!lookups.HasTree(model.Node))
                return false;
            var data = lookups[model.Node];
            data.lastJobHandle.Complete();
            if (!data.brushRenderBufferLookup.IsCreated ||
                !data.brushRenderBufferLookup.TryGetValue(CompactHierarchyManager.GetCompactNodeID(surface.brush), out var buffer) || !buffer.IsCreated)
                return false;
            ref var surfaces = ref buffer.Value.surfaces;
            if (surface.surfaceIndex < 0 || surface.surfaceIndex >= surfaces.Length)
                return false;
            var input = surfaces[surface.surfaceIndex].sectionInput;
            if (input.count != 4 || !(input.pixelsPerUnit > 0))
                return false;
            var across = math.length(input.t1 - input.t0); var along = math.length(input.t2 - input.t0);
            if (input.Repeated)
            {
                // the whole tile a repeat, as many as fit (the last one in part)
                var countX = math.max(1, input.repeatX - 1 + (input.lastRepeat.x > 0 ? input.lastRepeat.x : 1));
                var countY = math.max(1, input.repeatY - 1 + (input.lastRepeat.y > 0 ? input.lastRepeat.y : 1));
                period = new Vector2(math.length(input.f1 - input.f0) / countX, math.length(input.f2 - input.f0) / countY);
                pixels = new Vector2(across, along);
            } else
            {
                // a bordered tile's interior, repeated at the tile's density (HotspotSections.BuildInteriorRepeats)
                if (!(input.border > 0))
                    return false;
                pixels = new Vector2(math.max(1, across - 2 * input.border), math.max(1, along - 2 * input.border));
                period = pixels / input.pixelsPerUnit;
            }
            worldToModel = ChiselNodeHierarchyManager.FindModelTransformMatrixOfTransform(surface.node.transform).inverse;
            right = (Vector3)input.right;
            up    = (Vector3)input.up;
            return period.x > 0 && period.y > 0;
        }

        // A hotspot surface's texture moved along with the mouse (its offset, in repeats), to whole texels when snapping
        internal static void MoveHotspot(SurfaceReference surface, int index, Vector3 worldTranslation)
        {
            if (!HotspotRepeatsOf(surface, out var worldToModel, out var right, out var up, out var period, out var pixels))
                return;
            var translation = worldToModel.MultiplyVector(worldTranslation);
            var start   = s_SelectedHotspots[index];
            var details = surface.SurfaceDetails;
            var offset  = start.offset - new float2(Vector3.Dot(translation, right) / period.x, Vector3.Dot(translation, up) / period.y);
            if (GridSnapping)
                offset = math.round(offset * (float2)pixels) / (float2)pixels;
            details.hotspot.offset = offset - math.floor(offset);
            surface.SurfaceDetails = details;
            ChiselHotspotGroupEdit.Spread(new[] { surface }, "Move Hotspot");
        }

        // A hotspot surface's texture turned along with the mouse (degrees, about the drag plane's normal)
        internal static void RotateHotspot(SurfaceReference surface, int index, Vector3 worldNormal, float degrees)
        {
            var start   = s_SelectedHotspots[index];
            var details = surface.SurfaceDetails;
            // counter-clockwise seen from in front is a negative turn about the surface's normal
            var plane   = surface.WorldPlane;
            var facing  = plane.HasValue && Vector3.Dot(plane.Value.normal, worldNormal) < 0 ? -1 : 1;
            // a mirrored texture turns the other way
            if (start.mirror != start.mirrorY)
                facing = -facing;
            var rotation = start.rotation - degrees * facing;
            details.hotspot.rotation = rotation - 360 * Mathf.Floor(rotation / 360);
            surface.SurfaceDetails = details;
            ChiselHotspotGroupEdit.Spread(new[] { surface }, "Rotate Hotspot");
        }

        // A hotspot surface's texture scaled along with the mouse (its density the other way round), to powers of two
        // when snapping. squaredScale: the start's distance from the centre over the mouse's, squared (as the UV Scale
        // tool measures it)
        internal static void ScaleHotspot(SurfaceReference surface, int index, float squaredScale)
        {
            if (!(squaredScale > 0) || float.IsInfinity(squaredScale))
                return;
            var start   = s_SelectedHotspots[index];
            var details = surface.SurfaceDetails;
            var density = Mathf.Clamp(start.Density * Mathf.Sqrt(squaredScale), 1.0f / 64, 64);
            if (GridSnapping)
                density = Mathf.Pow(2, Mathf.Round(Mathf.Log(density, 2)));
            details.hotspot.density = density;
            surface.SurfaceDetails = details;
            ChiselHotspotGroupEdit.Spread(new[] { surface }, "Scale Hotspot");
        }

        internal static Plane       s_WorldDragPlane;
        internal static Plane       s_WorldProjectionPlane;
        internal static Vector3		s_WorldStartPosition;
        internal static Vector3		s_WorldIntersection;
        internal static Vector3     s_WorldDragDeltaVector;
        
        internal static ChiselNodeComponent[]	s_SelectedNodes;
        internal static UVMatrix[]			    s_SelectedUVMatrices;
        internal static SurfaceReference[]	    s_SelectedSurfaceReferences;
        internal static HotspotSurface[]        s_SelectedHotspots;     // the hotspot settings each had when the drag started

        internal static bool StartToolDragging()
        {
            s_JumpedMousePosition += Event.current.delta;
            Event.current.Use();
            if (ToolIsDragging)
            {
                UpdateDragVector();
                return false;
            }

            // We set ToolIsDragging to true to be able to tell the difference between dragging and clicking
            ToolIsDragging = true;

            // Find the intersecting surfaces
            s_StartSurfaceReference           = s_HoverSurfaceReference;

            s_SelectedSurfaceReferences	    = ChiselSurfaceSelectionManager.Selection.ToArray();

            // We need all the nodes for all the surfaces we're moving, so that we can record them for an undo
            s_SelectedNodes                   = ChiselSurfaceSelectionManager.SelectedNodes.ToArray();

            // We copy all the original surface uvMatrices, so we always apply rotations and transformations relatively to the original
            // This makes it easier to recover from edge cases and makes it more accurate, floating point wise.
            s_SelectedUVMatrices	 = new UVMatrix[s_SelectedSurfaceReferences.Length];
            s_SelectedHotspots   = new HotspotSurface[s_SelectedSurfaceReferences.Length];
            for (int i = 0; i < s_SelectedSurfaceReferences.Length; i++)
            {
                s_SelectedUVMatrices[i] = s_SelectedSurfaceReferences[i].UV0;
                s_SelectedHotspots[i]   = s_SelectedSurfaceReferences[i].SurfaceDetails.hotspot;
            }
            
            // Find the intersection point/plane in model space
            s_WorldStartPosition	 = s_HoverIntersection.Value.worldPlaneIntersection;
            s_WorldProjectionPlane = s_HoverIntersection.Value.worldPlane;
            s_WorldIntersection    = s_WorldStartPosition;

            // TODO: we want to be able to determine delta movement over a plane. Ideally it would match the position of the cursor perfectly.
            //		 unfortunately when moving the cursor towards the horizon of the plane, relative to the camera, the delta movement 
            //		 becomes too large or even infinity. Ideally we'd switch to a camera facing plane for these cases and determine movement in 
            //		 a less perfect way that would still allow the user to move or rotate things in a reasonable way.

            // more accurate for small movements
            s_WorldDragPlane		= s_WorldProjectionPlane;

			// TODO: (unfinished) prevents drag-plane from intersecting near plane (makes movement slow down to a singularity when further away from click position)
			//worldDragPlane	= new Plane(Camera.current.transform.forward, worldStartPosition); 

			// TODO: ideally we'd interpolate the configuration of the worldPlane between near and far configuration
			UpdateDragVector();
            return true;
        }

        private static Vector3 GetCurrentWorldClick(Vector2 mousePosition)
        {
            if (GetCurrentWorldClick(s_WorldDragPlane, mousePosition, out Vector3 currentWorldIntersection))
            {
                snapState.SnapCause = SnapSettings.None;
                currentWorldIntersection = SnapIntersection(currentWorldIntersection, s_StartSurfaceReference, out s_PointHasSnapped, ref snapState);
                return currentWorldIntersection;
            }
            return s_WorldStartPosition;
        }

        private static bool GetCurrentWorldClick(Plane plane, Vector2 mousePosition, out Vector3 currentWorldIntersection)
        {
            currentWorldIntersection = default;
            var mouseRay = HandleUtility.GUIPointToWorldRay(mousePosition);
            if (!plane.SignedRaycast(mouseRay, out float enter))
                return false;
            currentWorldIntersection = mouseRay.GetPoint(enter);
            return true;
        }

        static void UpdateDragVector()
        {
            s_WorldIntersection = GetCurrentWorldClick(s_JumpedMousePosition);
            var worldSpaceMovement = s_WorldIntersection - s_WorldStartPosition;

            MathExtensions.CalculateTangents(s_WorldDragPlane.normal, out var tangent, out var binormal);

            var deltaVector = tangent  * Vector3.Dot(tangent,  worldSpaceMovement) +
                              binormal * Vector3.Dot(binormal, worldSpaceMovement);

            if (Snapping.AxisLockX) deltaVector.x = 0;
            if (Snapping.AxisLockY) deltaVector.y = 0;
            if (Snapping.AxisLockZ) deltaVector.z = 0;

            s_WorldDragDeltaVector = deltaVector;
        }
        #endregion        
        
        
        #region Selection
        static bool ClickSelection(Rect dragArea, SelectionType selectionType)
        {
            return ChiselSurfaceSelectionManager.UpdateSelection(selectionType, s_HoverSurfaces);
        }
        
        internal static bool SurfaceSelection(Rect dragArea, SelectionType selectionType)
        {
            var id = GUIUtility.GetControlID(kSurfaceDragSelectionHash, FocusType.Keyboard, dragArea);
            
            bool repaint = false;

            var evt = Event.current;
            if (evt.type == EventType.MouseMove ||
                evt.type == EventType.MouseDown)
            {
                if (UpdateHoverSurfaces(evt.mousePosition, dragArea, selectionType, true))
                    repaint = true;
                
                if (!InEditCameraMode)
                    return repaint;
            }

            if (InEditCameraMode && !ToolIsDragging)
            {
                if (MouseIsDown) 
                    ChiselOutlineRenderer.VisualizationMode = VisualizationMode.None;
                else
                    ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Surface | VisualizationMode.Outline;
            } else
                ChiselOutlineRenderer.VisualizationMode = VisualizationMode.None;


            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                {
                    // we only do drag selection when we use a modifier (shift, control etc.)
                    if (selectionType == SelectionType.Replace)
                        break;

                    if (s_HoverSurfaces != null && s_HoverSurfaces.Count > 0)
                        HandleUtility.AddControl(id, 3.0f);
                    break;
                }
                case EventType.MouseDown:
                {
                    if (GUIUtility.hotControl != 0)
                        break;
                    
                    // we only do drag selection when we use a modifier (shift, control etc.)
                    if (selectionType == SelectionType.Replace)
                        break;
                    
                    if ((UnityEditor.HandleUtility.nearestControl != id || evt.button != 0) &&
                        (GUIUtility.keyboardControl != id || evt.button != 2))
                        break;
                    
                    GUIUtility.hotControl = GUIUtility.keyboardControl = id;
                    evt.Use();
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (GUIUtility.hotControl != id)
                        break;

                    UpdateHoverSurfaces(evt.mousePosition, dragArea, selectionType, false);
                    evt.Use();
                    break;
                }
                case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl != id || evt.button != 0)
                        break;
                    
                    GUIUtility.hotControl = 0;
                    GUIUtility.keyboardControl = 0;
                    evt.Use();

                    if (ChiselSurfaceSelectionManager.UpdateSelection(selectionType, s_HoverSurfaces))
                        repaint = true;

                    if (UpdateHoverSurfaces(evt.mousePosition, dragArea, selectionType, true))
                        repaint = true;
                    break;
                }
            }

            return repaint;
        }

        static void ResetSelection()
        {
            s_HoverIntersection = null;
            s_HoverSurfaceReference = null;
            s_SelectedSurfaceReferences = null;
            s_SelectedNodes = null;
            s_SelectedUVMatrices = null;
        }
        #endregion

        #region Hover Surfaces
        static ChiselIntersection? s_HoverIntersection;
        static SurfaceReference s_HoverSurfaceReference;

        readonly static HashSet<SurfaceReference> s_HoverSurfaces = new();

        internal static void RenderIntersectionPoint()
        {
            RenderIntersectionPoint(ChiselUVToolCommon.s_WorldIntersection);
        }

        static void RenderIntersectionPoint(Vector3 position)
        {
            SceneHandles.RenderBorderedCircle(position, HandleUtility.GetHandleSize(position) * 0.02f);
        }

        internal static void RenderSnapEvent()
        {
            if (snapState.SnapCause == SnapSettings.None)
                return;

            var prevMatrix = Handles.matrix;
            var prevColor = Handles.color;

            Handles.color = Color.white;
            switch (snapState.SnapCause)
            {
                case SnapSettings.UVGeometryEdges:
                {
                    ChiselOutlineRenderer.DrawLine(snapState.StartEdge, snapState.EndEdge, LineMode.NoZTest, 4, 0);
                    break;
                }
                case SnapSettings.UVGeometryVertices:
                {
                    HandleRendering.RenderVertexBox(snapState.SnapPosition);
                    break;
                }
            }
			// TODO: figure out how to handle this properly
			/*
            if (snapState.SnapGridAxis != SnapAxis.None)
            {
                var grid = Chisel.Editors.Grid.defaultGrid;
                if ((snapState.SnapGridAxis & SnapAxis.X) != SnapAxis.None)
                {
                    var direction = grid.Right * 1000.0f;
                    ChiselOutlineRenderer.DrawLine(snapState.SnapPosition - direction, snapState.SnapPosition + direction, LineMode.NoZTest, 1, 0);
                }
                if ((snapState.SnapGridAxis & SnapAxis.Y) != SnapAxis.None)
                {
                    var direction = grid.Up * 1000.0f;
                    ChiselOutlineRenderer.DrawLine(snapState.SnapPosition - direction, snapState.SnapPosition + direction, LineMode.NoZTest, 1, 0);
                }
                if ((snapState.SnapGridAxis & SnapAxis.Z) != SnapAxis.None)
                {
                    var direction = grid.Forward * 1000.0f;
                    ChiselOutlineRenderer.DrawLine(snapState.SnapPosition - direction, snapState.SnapPosition + direction, LineMode.NoZTest, 1, 0);
                }
            }
            */
			Handles.color = prevColor;
            Handles.matrix = prevMatrix;
        }
        
        static void RenderIntersection()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (ToolIsDragging)
                return;

            if (!s_HoverIntersection.HasValue)
                return;

            var position = s_HoverIntersection.Value.worldPlaneIntersection;
            RenderIntersectionPoint(position);
            RenderSnapEvent();
        }


        static bool UpdateHoverSurfaces(Vector2 mousePosition, Rect dragArea, SelectionType selectionType, bool clearHovering)
        {
			var tempSurfaces = ListPool<SurfaceReference>.Get();
			try
            {
                s_HoverIntersection = null;
                s_HoverSurfaceReference = null;

                bool modified = false;
                if (clearHovering || !InEditCameraMode)
                {
                    if (s_HoverSurfaces.Count != 0)
                    {
                        s_HoverSurfaces.Clear();
                        modified = true;
                    }
                }

                if (!dragArea.Contains(mousePosition))
                    return modified;

                if (!InEditCameraMode)
                    return modified;

				if (!ChiselClickSelectionManager.FindSurfaceReferences(mousePosition, false, tempSurfaces, out ChiselIntersection intersection, out SurfaceReference surfaceReference))
                {
                    modified = (s_HoverSurfaces != null) || modified;
                    s_HoverIntersection = null;
                    return modified;
                }

                bool isSurfaceSelected = ChiselSurfaceSelectionManager.IsSelected(surfaceReference);

                if (!float.IsInfinity(intersection.brushIntersection.surfaceIntersection.distance))
                {
                    snapState.SnapCause = SnapSettings.None;
                    if (!isSurfaceSelected && s_ForceVertexSnapping)
                    {
                        if (SelectedSnapIntersection(mousePosition, intersection.worldPlaneIntersection, out surfaceReference, out Vector3 worldIntersectionPoint, ref snapState))
                        {
                            isSurfaceSelected = true;
                            intersection.worldPlane             = surfaceReference.WorldPlane.Value;
                            intersection.worldPlaneIntersection = worldIntersectionPoint;
                            tempSurfaces.Clear();
                            tempSurfaces.Add(surfaceReference);
                        }
                    } else
                        intersection.worldPlaneIntersection = SnapIntersection(intersection.worldPlaneIntersection, surfaceReference, out s_PointHasSnapped, ref snapState);
                }

                if (isSurfaceSelected || !s_ForceVertexSnapping)
                {
                    s_HoverIntersection = intersection;
                    s_HoverSurfaceReference = surfaceReference;

                    if (tempSurfaces.Count == s_HoverSurfaces.Count)
                        modified = !s_HoverSurfaces.ContainsAll(tempSurfaces) || modified;
                    else
                        modified = true;

                    if (tempSurfaces.Count > 0)
                        s_HoverSurfaces.AddRange(tempSurfaces);
                } else
                {
                    modified = true;
                    s_HoverIntersection = null;
                    s_HoverSurfaceReference = null;
                }

                return modified;
            }
            finally
			{
				if (s_ForceVertexSnapping)
                    ChiselSurfaceSelectionManager.SetHovering(selectionType, null);
                else
                    ChiselSurfaceSelectionManager.SetHovering(selectionType, s_HoverSurfaces);
				ListPool<SurfaceReference>.Release(tempSurfaces);
			}
        }
        #endregion
    }
}
