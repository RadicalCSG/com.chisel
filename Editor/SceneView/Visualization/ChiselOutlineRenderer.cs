using System.Linq;
using System.Collections.Generic;
using Chisel.Core;
using Chisel.Components;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public enum VisualizationMode
    {
        None = 0,
        Outline = 1,
        SimpleOutline = 2,
        Surface = 4
    }

#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed class ChiselOutlineRenderer : ScriptableObject
    {
        #region BrushOutline
        sealed class BrushOutline
        {
            public BrushOutline(Transform transform, CSGTreeBrush brush)
            {
                this.transform = transform;
                this.brush = brush;
            }
            public Transform    transform;
            public CSGTreeBrush brush;
            
            #region Comparison
            public static bool operator == (BrushOutline left, BrushOutline right) { return left.brush == right.brush; }
            public static bool operator != (BrushOutline left, BrushOutline right) { return left.brush != right.brush; }

            public override bool Equals(object obj) { if (!(obj is BrushOutline)) return false; var type = (BrushOutline)obj; return brush == type.brush; }
            public override int GetHashCode() { return brush.GetHashCode() ; }		
            #endregion
        }
        #endregion
        
        #region SurfaceOutline
        sealed class SurfaceOutline
        {
            public SurfaceOutline(Transform transform, SurfaceReference surface)
            {
                this.transform = transform;
                this.surface = surface;
            }
            public Transform		transform;
            public SurfaceReference surface;
            
            #region Comparison
            public static bool operator == (SurfaceOutline left, SurfaceOutline right) { return left.surface == right.surface; }
            public static bool operator != (SurfaceOutline left, SurfaceOutline right) { return left.surface != right.surface; }

            public override bool Equals(object obj) { if (!(obj is SurfaceOutline)) return false; var type = (SurfaceOutline)obj; return surface == type.surface; }
            public override int GetHashCode() { return surface.GetHashCode() ; }		
            #endregion
        }
        #endregion

        [System.NonSerialized] bool showSelectedSurfaces;

        /// <summary>Whether the selected surfaces' outlines show in every tool, not only the surface tools</summary>
        public static bool ShowSelectedSurfaces
        {
            get => Instance.showSelectedSurfaces;
            set
            {
                if (Instance.showSelectedSurfaces == value)
                    return;
                Instance.showSelectedSurfaces = value;
                s_UpdateSurfaceSelection = true;
                SceneView.RepaintAll();
            }
        }

        #region Instance
        static ChiselOutlineRenderer s_Instance;
        public static ChiselOutlineRenderer Instance
        {
            get
            {
                if (s_Instance)
                    return s_Instance;

				var foundInstances = UnityEngine.Object.FindObjectsByType<ChiselOutlineRenderer>(FindObjectsInactive.Exclude);
                if (foundInstances == null ||
                    foundInstances.Length == 0)
                {
                    s_Instance = ScriptableObject.CreateInstance<ChiselOutlineRenderer>();					
                    s_Instance.hideFlags = HideFlags.HideAndDontSave;
                    return s_Instance;
                }

                s_Instance = foundInstances[0];
                return s_Instance;
            }
        }
        #endregion

        ChiselRenderer	brushOutlineRenderer;
        ChiselRenderer	surfaceOutlineRenderer;

        readonly static Dictionary<SurfaceReference, ChiselWireframe> s_SurfaceOutlineCache = new();

		// NOTE: handle-renderers often take the orientation of the camera into account (for example: backfaced surfaces) so they need to be camera specific
		readonly Dictionary<Camera, ChiselRenderer>          handleRenderers        = new();

		readonly Dictionary<SurfaceOutline, ChiselWireframe> surfaceOutlines		= new();
        readonly Dictionary<SurfaceOutline, ChiselWireframe> surfaceOutlineFixes	= new();
        readonly HashSet<SurfaceOutline>	                 foundSurfaceOutlines	= new();
        readonly HashSet<SurfaceOutline>	                 removedSurfaces		= new();

        readonly Dictionary<BrushOutline, ChiselWireframe>   brushOutlines		    = new();
        readonly Dictionary<BrushOutline, ChiselWireframe>   brushOutlineFixes	    = new();
        readonly HashSet<CSGTreeBrush>		                 brushDirectlySelected	= new();
        readonly HashSet<CSGTreeBrush>		                 foundTreeBrushes		= new();
        readonly HashSet<BrushOutline>		                 foundBrushOutlines		= new();
        readonly HashSet<BrushOutline>		                 removedBrushes			= new();
        // the brushes of selected generators, and the subtractive or intersecting ones shown whether selected or not
        readonly HashSet<CSGTreeBrush>		                 brushSelected		    = new();
        readonly Dictionary<CSGTreeBrush, CSGOperationType>  brushCarving		    = new();
        // kept from one update to the next, so they're not made again every time
        readonly Dictionary<CSGTreeBrush, BrushOutline>      carvingOutlines	    = new();
        readonly HashSet<CSGTreeBrush>		                 generatorBrushes	    = new();

        // A generator built from several brushes, outlined as its shape (unless its pieces are shown), in its own space
        sealed class ShapeOutline
        {
            public Transform        modelTransform;
            public CSGTreeNode      node;
            public ChiselWireframe  wireframe;
            public bool             selected;
            public CSGOperationType carving;
            public bool             found;
        }
        readonly Dictionary<CSGTreeNode, ShapeOutline>       shapeOutlines	        = new();
        readonly List<CSGTreeNode>                           removedShapes	        = new();

        static bool IsOutlinedAsAShape(ChiselGeneratorComponent generator)
        {
            return !ChiselCarvingOutlines.BrushPiecesShown && generator.TopTreeNode.Valid && generator.TopTreeNode.Type == CSGNodeType.Branch;
        }

        void MarkShape(ChiselGeneratorComponent generator, Transform modelTransform, bool selected, CSGOperationType carving)
        {
            var node = generator.TopTreeNode;
            if (!shapeOutlines.TryGetValue(node, out var shape))
                shapeOutlines[node] = shape = new ShapeOutline { node = node };
            shape.modelTransform = modelTransform;
            shape.found          = true;
            if (selected) shape.selected = true;
            else          shape.carving  = carving;
        }

        static bool s_UpdateBrushSelection	= false;
        static bool s_UpdateBrushWireframe	= false;
        static bool s_UpdateBrushLineCache	= false;

        static bool s_UpdateSurfaceSelection = false;
        static bool s_UpdateSurfaceWireframe = false;
        static bool s_UpdateSurfaceLineCache = false;
        static bool s_ClearSurfaceCache	     = false;

        static VisualizationMode s_VisualizationMode = VisualizationMode.Outline;
        public static VisualizationMode VisualizationMode
        {
            get { return s_VisualizationMode; }
            set
            {
                s_VisualizationMode       = value;
                s_UpdateBrushWireframe	= true;
                s_UpdateSurfaceWireframe	= true;
            }
        }


        void OnEnable()
        {
            brushOutlineRenderer	= new ChiselRenderer();
            surfaceOutlineRenderer	= new ChiselRenderer();
            handleRenderers.Clear();
        }

        void OnDisable()
        {
            foreach(var item in handleRenderers)
                item.Value.Destroy();
            handleRenderers.Clear();
            brushOutlineRenderer	.Destroy();
            surfaceOutlineRenderer	.Destroy();

            brushOutlines	.Clear();
            surfaceOutlines	.Clear();
        }

        internal void OnReset()
        {
            Reset();
        }

        internal void OnEditModeChanged()
        {
            // Defer since we could potentially get several events before we actually render
            // also, not everything might be set up correctly just yet.
            s_UpdateBrushSelection = true;
            s_UpdateSurfaceSelection = true;
        }

        internal void OnSelectionChanged()
        {
            // Defer since we could potentially get several events before we actually render
            // also, not everything might be set up correctly just yet.
            s_UpdateBrushSelection = true;
            s_UpdateSurfaceSelection = true;
        }

        internal void OnSurfaceSelectionChanged()
        {
            // Defer since we could potentially get several events before we actually render
            s_UpdateSurfaceSelection = true;
        }
        
        internal void OnSurfaceHoverChanged()
        {
            // Defer since we could potentially get several events before we actually render
            s_UpdateSurfaceSelection = true;
        }
        
        static internal void OnUndoRedoPerformed()
        {
            s_ClearSurfaceCache = true;
        }


        internal void OnGeneratedMeshesChanged()
        {
            // Defer since we could potentially get several events before we actually render
            // also, not everything might be set up correctly just yet.
            s_UpdateBrushWireframe = true;
            s_UpdateSurfaceWireframe = true;
            s_ClearSurfaceCache = true;
            // a brush made again, or its operation changed; a generator made again from other nodes
            if (ChiselCarvingOutlines.AnyShown || shapeOutlines.Count > 0)
                s_UpdateBrushSelection = true;
        }

        internal static void OnOutlineOptionsChanged()
        {
            s_UpdateBrushSelection = true;
        }

        // The lines are cached with their colors
        internal static void OnColorsChanged()
        {
            s_UpdateBrushLineCache = true;
            s_UpdateSurfaceLineCache = true;
        }

        internal void OnTransformationChanged()
        {
            // Defer since we could potentially get several events before we actually render
            // also, not everything might be set up correctly just yet.
            s_UpdateBrushLineCache = true;
            s_UpdateSurfaceLineCache = true;
            s_ClearSurfaceCache = true;
        }


        public void Reset()
        {
            surfaceOutlineFixes.Clear();
            surfaceOutlines.Clear();
            foundSurfaceOutlines.Clear();
            removedSurfaces.Clear();

            brushOutlines.Clear();
            brushOutlineFixes.Clear();
            foundTreeBrushes.Clear();
            foundBrushOutlines.Clear();
            removedBrushes.Clear();
            brushSelected.Clear();
            brushCarving.Clear();
            carvingOutlines.Clear();
            shapeOutlines.Clear();

            s_UpdateBrushSelection = true;
            s_UpdateBrushWireframe = false;
            s_UpdateBrushLineCache = false;

            s_UpdateSurfaceSelection = true;
            s_UpdateSurfaceWireframe = false;
            s_UpdateSurfaceLineCache = false;
            s_ClearSurfaceCache = true;
        }

        void UpdateBrushSelection()
        {
            brushDirectlySelected.Clear();
            brushSelected.Clear();
            foreach (var shape in shapeOutlines.Values)
            {
                shape.found    = false;
                shape.selected = false;
                shape.carving  = CSGOperationType.Additive;
            }
            var objects = Selection.objects;
            if (objects.Length > 0)
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    var obj = objects[i];
                    ChiselNodeComponent[] nodes = null;
                    var gameObject = obj as GameObject;
                    if (!Equals(null, gameObject))
                    {
                        nodes = gameObject.GetComponentsInChildren<ChiselNodeComponent>();
                    } else
                    {
                        var behaviour = obj as Behaviour;
                        if (!Equals(null, behaviour))
                        {
                            nodes = behaviour.GetComponents<ChiselNodeComponent>();
                        }
                    }

                    if (nodes != null &&
                        nodes.Length > 0)
                    {
                        for (int n = 0; n < nodes.Length; n++)
                        {
                            var node = nodes[n] as ChiselGeneratorComponent;
                            if (node == null)
                                continue;
                            foundTreeBrushes.Clear();
							ChiselModelManager.Instance.GetAllTreeBrushes(node, foundTreeBrushes);
                            if (foundTreeBrushes.Count > 0)
                            {
                                var directSelected = (// if component is directly select
                                                      (gameObject == null) ||
                                                      // or when the component is part of the selected gameObject
                                                      (gameObject == node.gameObject)) &&
                                                      // if we find CSGTreeBrushes directly on this node, but this node
                                                      // can also have child nodes, then we assume the CSGTreeBrushes are generated
                                                      // and we don't want to show those as directly selected
                                                      !node.IsContainer;
                                var transform = ChiselNodeHierarchyManager.FindModelTransformOfTransform(node.hierarchyItem.Transform);
                                var asAShape  = IsOutlinedAsAShape(node);
                                if (asAShape)
                                    MarkShape(node, transform, selected: true, CSGOperationType.Additive);
                                foreach (var treeBrush in foundTreeBrushes)
                                {
                                    if (directSelected)
                                        brushDirectlySelected.Add(treeBrush);
                                    brushSelected.Add(treeBrush);
                                    if (asAShape)
                                        continue;
                                    var outline = new BrushOutline(transform, treeBrush);
                                    foundBrushOutlines.Add(outline);
                                }
                            }
                        }
                    }
                }
            }

            brushCarving.Clear();
            if (ChiselCarvingOutlines.AnyShown)
            {
                foreach (var sceneHierarchy in ChiselNodeHierarchyManager.sceneHierarchies.Values)
                {
                    foreach (var item in sceneHierarchy.RootItems)
                        FindCarvingBrushes(item, CSGOperationType.Additive);
                }
            }
            // the outlines of brushes no longer shown aren't kept
            if (carvingOutlines.Count > brushCarving.Count)
            {
                generatorBrushes.Clear();
                foreach (var brush in carvingOutlines.Keys)
                {
                    if (!brushCarving.ContainsKey(brush))
                        generatorBrushes.Add(brush);
                }
                foreach (var brush in generatorBrushes)
                    carvingOutlines.Remove(brush);
                generatorBrushes.Clear();
            }
            removedShapes.Clear();
            foreach (var pair in shapeOutlines)
            {
                if (!pair.Value.found || !pair.Key.Valid)
                    removedShapes.Add(pair.Key);
            }
            foreach (var node in removedShapes)
                shapeOutlines.Remove(node);
            removedShapes.Clear();

            if (foundBrushOutlines.Count == 0)
            {
                brushOutlines.Clear();
                brushOutlineRenderer.Clear();
            } else
            {
                foreach (var outline in brushOutlines.Keys)
                {
                    if (!foundBrushOutlines.Contains(outline) ||
                        !outline.brush.Valid ||
                        outline.brush.BrushMesh == BrushMeshInstance.InvalidInstance)
                        removedBrushes.Add(outline);
                }
                
                if (removedBrushes.Count > 0)
                {
                    foreach(var outline in removedBrushes)
                        brushOutlines.Remove(outline);
                }
                removedBrushes.Clear();
                
                foreach (var outline in foundBrushOutlines)
                {
                    if (brushOutlines.ContainsKey(outline))
                        continue;

                    if (!outline.brush.Valid ||
                        outline.brush.BrushMesh == BrushMeshInstance.InvalidInstance)
                        continue;
                    
                    var wireframe = ChiselWireframe.CreateWireframe(outline.brush);
                    brushOutlines[outline] = wireframe;
                }
            }
            
            foundBrushOutlines.Clear();
            s_UpdateBrushWireframe = true;
        }
        
        // The brushes of the generators under item that carve: each takes the operation of the nearest of it and the
        // composites above it that isn't additive, so a brush in a subtractive composite carves too. A generator's own
        // brushes (the parts it is built from) go with it, as they do when it is selected
        void FindCarvingBrushes(ChiselHierarchyItem item, CSGOperationType carving)
        {
            var component = item.Component;
            if (!component || !component.isActiveAndEnabled)
                return;
            switch (component)
            {
                case ChiselModelComponent:
                    carving = CSGOperationType.Additive;
                    break;
                case ChiselCompositeComponent composite:
                    if (composite.Operation != CSGOperationType.Additive)
                        carving = composite.Operation;
                    break;
                case ChiselGeneratorComponent generator:
                    if (generator.Operation != CSGOperationType.Additive)
                        carving = generator.Operation;
                    if (ChiselCarvingOutlines.Shows(carving))
                        AddCarvingOutlines(generator, carving);
                    break;
            }
            for (int c = 0; c < item.Children.Count; c++)
                FindCarvingBrushes(item.Children[c], carving);
        }

        void AddCarvingOutlines(ChiselGeneratorComponent generator, CSGOperationType carving)
        {
            generatorBrushes.Clear();
            ChiselModelManager.Instance.GetAllTreeBrushes(generator, generatorBrushes);
            if (generatorBrushes.Count == 0)
                return;
            var transform = ChiselNodeHierarchyManager.FindModelTransformOfTransform(generator.hierarchyItem.Transform);
            var asAShape  = IsOutlinedAsAShape(generator);
            if (asAShape)
                MarkShape(generator, transform, selected: false, carving);
            foreach (var brush in generatorBrushes)
            {
                brushCarving[brush] = carving;
                if (asAShape)
                    continue;
                if (carvingOutlines.TryGetValue(brush, out var outline))
                    outline.transform = transform;
                else
                    carvingOutlines[brush] = outline = new BrushOutline(transform, brush);
                foundBrushOutlines.Add(outline);
            }
            generatorBrushes.Clear();
        }

        void UpdateSurfaceSelection()
        {
            surfaceOutlines.Clear();
            ChiselSurfaceSelectionManager.Clean();
            var selection	= ChiselSurfaceSelectionManager.Selection;
            var hovered		= ChiselSurfaceSelectionManager.Hovered;
            if (selection.Count == 0 &&
                hovered.Count == 0)
            {
                surfaceOutlines.Clear();
                surfaceOutlineRenderer.Clear();
            } else
            {
                var allSurfaces = new HashSet<SurfaceReference>(selection);
                allSurfaces.AddRange(hovered);
                foreach (var outline in surfaceOutlines.Keys)
                {
                    var surface = outline.surface;
                    if (!allSurfaces.Contains(surface) ||
                        !surface.TreeBrush.Valid ||
                        surface.TreeBrush.BrushMesh == BrushMeshInstance.InvalidInstance)
                    {
                        removedSurfaces.Add(outline);
                    } else
                        allSurfaces.Remove(surface);
                }
                
                if (removedSurfaces.Count > 0)
                {
                    foreach(var outline in removedSurfaces)
                        surfaceOutlines.Remove(outline);
                }
                removedSurfaces.Clear();
                
                foreach (var surface in allSurfaces)
                {
                    var transform	= ChiselNodeHierarchyManager.FindModelTransformOfTransform(surface.node.hierarchyItem.Transform);
                    var outline		= new SurfaceOutline(transform, surface);
                    foundSurfaceOutlines.Add(outline);
                }
                
                foreach (var outline in foundSurfaceOutlines)
                {
                    if (!outline.surface.TreeBrush.Valid ||
                        outline.surface.TreeBrush.BrushMesh == BrushMeshInstance.InvalidInstance)
                        continue;
                    
                    var wireframe = GetSurfaceWireframe(outline.surface);
                    surfaceOutlines[outline] = wireframe;
                }
            }
            foundSurfaceOutlines.Clear();
            s_UpdateSurfaceWireframe = true;
        }

        static void CleanUpHandleRenderers()
        {
            var handleRenderers = Instance.handleRenderers;

            bool haveInvalidCameras = false;
            foreach (var camera in handleRenderers.Keys)
            {
                if (camera)
                    continue;
                haveInvalidCameras = true;
                break;
            }

            if (!haveInvalidCameras)
                return;

            var allCameras = handleRenderers.Keys.ToArray();
            foreach (var camera in allCameras)
            {
                if (camera)
                    continue;
                handleRenderers[camera].Destroy();
                handleRenderers.Remove(camera);
            }
        }

        static ChiselRenderer GetHandleRenderer(Camera camera)
        {
            var handleRenderers = Instance.handleRenderers;
			if (handleRenderers.TryGetValue(camera, out ChiselRenderer renderer))
				return renderer;

			CleanUpHandleRenderers();

            renderer = new ChiselRenderer();
            handleRenderers[camera] = renderer;
            return renderer;
        }
        
        static ChiselRenderer HandleRenderer { get { return GetHandleRenderer(Camera.current); } }
        #region Vector3 versions
        public static void DrawLine(Matrix4x4 transformation, Vector3 from, Vector3 to, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLine(transformation, from, to, color, lineMode, thickness, dashSize); }
        public static void DrawLine(Matrix4x4 transformation, Vector3 from, Vector3 to, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLine(transformation, from, to, lineMode, thickness, dashSize); }
        public static void DrawLine(Vector3 from, Vector3 to, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLine(from, to, color, lineMode, thickness, dashSize); }
        public static void DrawLine(Vector3 from, Vector3 to, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLine(from, to, lineMode, thickness, dashSize); }


        public static void DrawContinuousLines(Matrix4x4 transformation, Vector3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawContinuousLines(transformation, points, startIndex, length, color, lineMode, thickness, dashSize); }
        public static void DrawContinuousLines(Matrix4x4 transformation, Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawContinuousLines(transformation, points, startIndex, length, lineMode, thickness, dashSize); }
        public static void DrawContinuousLines(Vector3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawContinuousLines(points, startIndex, length, color, lineMode, thickness, dashSize); }
        public static void DrawContinuousLines(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawContinuousLines(points, startIndex, length, lineMode, thickness, dashSize); }

        public static void DrawLineLoop(Matrix4x4 transformation, List<Vector3> points, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLineLoop(transformation, points, 0, points.Count, color, lineMode, thickness, dashSize); }
        public static void DrawLineLoop(Matrix4x4 transformation, Vector3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLineLoop(transformation, points, startIndex, length, color, lineMode, thickness, dashSize); }
        public static void DrawLineLoop(Matrix4x4 transformation, Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLineLoop(transformation, points, startIndex, length, lineMode, thickness, dashSize); }
        public static void DrawLineLoop(Vector3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLineLoop(points, startIndex, length, color, lineMode, thickness, dashSize); }
        public static void DrawLineLoop(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLineLoop(points, startIndex, length, lineMode, thickness, dashSize); }
        
        public static void DrawPolygon(Matrix4x4 transformation, Vector3[] points, int[] indices, Color color) { HandleRenderer.DrawPolygon(transformation, points, indices, color); }
        public static void DrawPolygon(Matrix4x4 transformation, Vector3[] points, Color color) { HandleRenderer.DrawPolygon(transformation, points, color); }
        public static void DrawPolygon(Matrix4x4 transformation, List<Vector3> points, Color color) { HandleRenderer.DrawPolygon(transformation, points, color); }
        #endregion

        #region float3 versions
        public static void DrawLine(float4x4 transformation, float3 from, float3 to, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLine(transformation, from, to, color, lineMode, thickness, dashSize); }
        public static void DrawLine(float4x4 transformation, float3 from, float3 to, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLine(transformation, from, to, lineMode, thickness, dashSize); }
        public static void DrawLine(float3 from, float3 to, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLine(from, to, color, lineMode, thickness, dashSize); }
        public static void DrawLine(float3 from, float3 to, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLine(from, to, lineMode, thickness, dashSize); }


        public static void DrawContinuousLines(float4x4 transformation, float3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawContinuousLines(transformation, points, startIndex, length, color, lineMode, thickness, dashSize); }
        public static void DrawContinuousLines(float4x4 transformation, float3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawContinuousLines(transformation, points, startIndex, length, lineMode, thickness, dashSize); }
        public static void DrawContinuousLines(float3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawContinuousLines(points, startIndex, length, color, lineMode, thickness, dashSize); }
        public static void DrawContinuousLines(float3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawContinuousLines(points, startIndex, length, lineMode, thickness, dashSize); }

        public static void DrawLineLoop(float4x4 transformation, float3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLineLoop(transformation, points, startIndex, length, color, lineMode, thickness, dashSize); }
        public static void DrawLineLoop(float4x4 transformation, float3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLineLoop(transformation, points, startIndex, length, lineMode, thickness, dashSize); }
        public static void DrawLineLoop(float3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLineLoop(points, startIndex, length, color, lineMode, thickness, dashSize); }
        public static void DrawLineLoop(float3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { HandleRenderer.DrawLineLoop(points, startIndex, length, lineMode, thickness, dashSize); }
        
        public static void DrawPolygon(float4x4 transformation, float3[] points, int[] indices, Color color) { HandleRenderer.DrawPolygon(transformation, points, indices, color); }
        public static void DrawPolygon(float4x4 transformation, float3[] points, Color color) { HandleRenderer.DrawPolygon(transformation, points, color); }
        public static void DrawPolygon(float4x4 transformation, List<float3> points, Color color) { HandleRenderer.DrawPolygon(transformation, points, color); }
        #endregion

        void UpdateBrushWireframe()
        {
            foreach (var pair in brushOutlines)
            {
                var wireframe = pair.Value;
                var outline = pair.Key;
                var brush = outline.brush;
                if (wireframe == null)
                {
                    if (brush.Valid &&
                        brush.BrushMesh != BrushMeshInstance.InvalidInstance)
                        brushOutlineFixes[outline] = ChiselWireframe.CreateWireframe(brush);
                    else
                        brushOutlineFixes[outline] = null;
                    continue;
                } else
                {
                    if (!brush.Valid ||
                        brush.BrushMesh == BrushMeshInstance.InvalidInstance)
                    {
                        brushOutlineFixes[outline] = null;
                        continue;
                    }
                }
                
                if (!wireframe.Dirty)
                    continue;
                
                wireframe.UpdateWireframe();
            }
            foreach (var pair in brushOutlineFixes)
            {
                brushOutlines[pair.Key] = pair.Value;
            }
            brushOutlineFixes.Clear();

            // a shape is worked out again only when what it's made of changed (ChiselShapeOutline.Hash)
            foreach (var shape in shapeOutlines.Values)
            {
                if (!shape.node.Valid)
                    shape.wireframe = null;
                else if (shape.wireframe == null)
                    shape.wireframe = ChiselWireframe.CreateShapeWireframe(shape.node);
                else if (shape.wireframe.Dirty)
                    shape.wireframe.UpdateWireframe();
            }
            s_UpdateBrushLineCache = true;
        }

        public static ChiselWireframe GetSurfaceWireframe(SurfaceReference surface)
        {
            if (!s_SurfaceOutlineCache.TryGetValue(surface, out ChiselWireframe wireframe))
            {
                wireframe = ChiselWireframe.CreateWireframe(surface.TreeBrush, surface.surfaceIndex);
                s_SurfaceOutlineCache[surface] = wireframe;
            }
            return wireframe;
        }
        
        void UpdateSurfaceWireframe()
        {
            foreach (var pair in surfaceOutlines)
            {
                var wireframe	= pair.Value;
                var outline		= pair.Key;
                var surface		= outline.surface;
                var treeBrush	= surface.TreeBrush;
                if (wireframe == null)
                {
                    if (treeBrush.Valid &&
                        treeBrush.BrushMesh != BrushMeshInstance.InvalidInstance)
                        surfaceOutlineFixes[outline] = GetSurfaceWireframe(surface);
                    else
                        surfaceOutlineFixes[outline] = null;
                    continue;
                } else
                {
                    if (!treeBrush.Valid ||
                        treeBrush.BrushMesh == BrushMeshInstance.InvalidInstance)
                    {
                        surfaceOutlineFixes[outline] = null;
                        continue;
                    }
                }
                
                if (!wireframe.Dirty)
                    continue;
                
                wireframe.UpdateWireframe();
            }
            foreach (var pair in surfaceOutlineFixes)
            {
                surfaceOutlines[pair.Key] = pair.Value;
            }
            surfaceOutlineFixes.Clear();
            s_UpdateSurfaceLineCache = true;
        }

        // How a brush's outline is drawn: a selected generator's brushes in the outline color (the selected one, thicker,
        // in the UV tools), a carving brush that isn't selected in its operation's color; false when it isn't drawn
        internal bool TryGetOutlineColor(CSGTreeBrush brush, out Color color, out float thickness)
        {
            if (brushSelected.Contains(brush))
                return OutlineColor(true, CSGOperationType.Additive, out color, out thickness);
            if (!brushCarving.TryGetValue(brush, out var carving))
                carving = CSGOperationType.Additive;
            return OutlineColor(false, carving, out color, out thickness);
        }

        // the same for a generator's shape, as for all of its brushes
        static bool OutlineColor(bool selected, CSGOperationType carving, out Color color, out float thickness)
        {
            if (selected)
            {
                var uvTool = ChiselUVMoveTool.IsActive() || ChiselUVRotateTool.IsActive() || ChiselUVScaleTool.IsActive() || ChiselUVConnectTool.IsActive();
                color     = uvTool ? ColorManager.SelectedOutlineColor : ColorManager.UnselectedOutlineColor;
                thickness = uvTool ? 3.0f : 1.0f;
                return true;
            }
            if (carving != CSGOperationType.Additive)
            {
                color     = carving == CSGOperationType.Subtractive ? ColorManager.SubtractiveOutlineColor : ColorManager.IntersectingOutlineColor;
                thickness = 1.0f;
                return true;
            }
            color     = default;
            thickness = 0;
            return false;
        }

        internal bool HasOutline(CSGTreeBrush brush)
        {
            return brushOutlines.TryGetValue(new BrushOutline(null, brush), out var wireframe) && wireframe != null;
        }

        // the outline of a generator's shape, in its own space, when it's drawn as one
        internal ChiselWireframe ShapeOutlineOf(CSGTreeNode node)
        {
            return shapeOutlines.TryGetValue(node, out var shape) ? shape.wireframe : null;
        }

        // which outlines are drawn worked out now, rather than when the scene view is next drawn (which draws them): all
        // of it again, or only what has changed since, as the scene view does
        internal void UpdateBrushOutlinesNow(bool all = true)
        {
            if (all)
                s_UpdateBrushSelection = true;
            UpdateBrushOutlines();
        }

        void UpdateBrushOutlines()
        {
            if (s_UpdateBrushSelection)
            {
                s_UpdateBrushSelection = false;
                UpdateBrushSelection();
                s_UpdateBrushWireframe = true;
            }
            if (s_UpdateBrushWireframe)
            {
                s_UpdateBrushWireframe = false;
                UpdateBrushWireframe();
                s_UpdateBrushLineCache = true;
            }
        }

        void UpdateBrushState()
        {
            UpdateBrushOutlines();
            if (s_UpdateBrushLineCache)
            {
                s_UpdateBrushLineCache = false;
                brushOutlineRenderer.Begin();

                // the carving brushes first, so the selected ones are drawn over them
                for (int pass = 0; pass < 2; pass++)
                {
                    foreach (var pair in brushOutlines)
                    {
                        var wireframe = pair.Value;
                        if (wireframe == null)
                            continue;

                        var outline = pair.Key;
                        if (!outline.brush.Valid)
                            continue;

                        var brush = outline.brush;
                        if (brushSelected.Contains(brush) != (pass == 1))
                            continue;
                        if (!TryGetOutlineColor(brush, out var color, out var thickness))
                            continue;

                        var wireframeValue	= pair.Value;
                        var modelTransform	= outline.transform;
                        Matrix4x4 transformation;
                        if (modelTransform)
                            transformation = modelTransform.localToWorldMatrix * (Matrix4x4)brush.NodeToTreeSpaceMatrix;
                        else
                            transformation = brush.NodeToTreeSpaceMatrix;

                        if ((VisualizationMode & VisualizationMode.Outline) == VisualizationMode.Outline)
                            brushOutlineRenderer.DrawOutlines(transformation, wireframeValue, color, thickness: thickness, onlyInnerLines: false);

                        if ((VisualizationMode & VisualizationMode.SimpleOutline) == VisualizationMode.SimpleOutline)
                            brushOutlineRenderer.DrawSimpleOutlines(transformation, wireframeValue, brushSelected.Contains(brush) ? ColorManager.UnselectedOutlineColor : color);
                    }

                    foreach (var shape in shapeOutlines.Values)
                    {
                        if (shape.wireframe == null || !shape.node.Valid || shape.selected != (pass == 1))
                            continue;
                        if (!OutlineColor(shape.selected, shape.carving, out var color, out var thickness))
                            continue;
                        Matrix4x4 transformation = shape.node.NodeToTreeSpaceMatrix;
                        if (shape.modelTransform)
                            transformation = shape.modelTransform.localToWorldMatrix * transformation;

                        if ((VisualizationMode & VisualizationMode.Outline) == VisualizationMode.Outline)
                            brushOutlineRenderer.DrawOutlines(transformation, shape.wireframe, color, thickness: thickness, onlyInnerLines: false);

                        if ((VisualizationMode & VisualizationMode.SimpleOutline) == VisualizationMode.SimpleOutline)
                            brushOutlineRenderer.DrawSimpleOutlines(transformation, shape.wireframe, shape.selected ? ColorManager.UnselectedOutlineColor : color);
                    }
                }
                brushOutlineRenderer.End();
            }
        }

        void UpdateSurfaceState()
        {
            if (s_ClearSurfaceCache)
            {
                s_SurfaceOutlineCache.Clear();
                s_ClearSurfaceCache = false;
            }
            if (s_UpdateSurfaceSelection)
            {
                s_UpdateSurfaceSelection = false;
                UpdateSurfaceSelection();
                s_UpdateSurfaceWireframe = true;
            }
            if (s_UpdateSurfaceWireframe)
            {
                s_UpdateSurfaceWireframe = false;
                UpdateSurfaceWireframe();
                s_UpdateSurfaceLineCache = true;
            }
            if (s_UpdateSurfaceLineCache)
            {
                var selection	= ChiselSurfaceSelectionManager.Selection;
                var hovered		= ChiselSurfaceSelectionManager.Hovered;
                s_UpdateSurfaceLineCache = false;
                surfaceOutlineRenderer.Begin();
                foreach (var pair in surfaceOutlines)
                {
                    var wireframe = pair.Value;
                    if (wireframe == null)
                        continue;

                    var outline			= pair.Key;
                    var surface			= outline.surface;
                    if (hovered.Contains(surface))
                        continue;

                    var brush			= surface.TreeBrush;
                    if (!brush.Valid)
                        continue;
                        
                    var modelTransform	= outline.transform;

                    Matrix4x4 transformation;
                    if (modelTransform)
                        transformation = modelTransform.localToWorldMatrix * (Matrix4x4)brush.NodeToTreeSpaceMatrix;
                    else
                        transformation = brush.NodeToTreeSpaceMatrix;

                    if (selection.Contains(surface))
                        surfaceOutlineRenderer.DrawOutlines(transformation, wireframe, ColorManager.SelectedOutlineColor, thickness: 1.0f);
                }
                foreach (var pair in surfaceOutlines)
                {
                    var wireframe = pair.Value;
                    if (wireframe == null)
                        continue;

                    var outline			= pair.Key;
                    var surface			= outline.surface;
                    if (!hovered.Contains(surface))
                        continue;

                    var brush			= surface.TreeBrush;
                    if (!brush.Valid)
                        continue;
                        
                    var modelTransform	= outline.transform;

                    Matrix4x4 transformation;
                    if (modelTransform)
                        transformation = modelTransform.localToWorldMatrix * (Matrix4x4)brush.NodeToTreeSpaceMatrix;
                    else
                        transformation = brush.NodeToTreeSpaceMatrix;

                    if (selection.Contains(surface))
                    {
                        surfaceOutlineRenderer.DrawOutlines(transformation, wireframe, ColorManager.SelectedHoverOutlineColor, thickness: 1.0f);
                    } else
                        surfaceOutlineRenderer.DrawOutlines(transformation, wireframe, ColorManager.PreSelectedOutlineColor, thickness: 1.0f);
                }
                surfaceOutlineRenderer.End();
            }
        }

        int prevFocus = 0;

        public void OnSceneGUI(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var camera = sceneView.camera;

            if (Tools.current != Tool.Custom)
                VisualizationMode = VisualizationMode.Outline;

            // defer surface updates when it's not currently visible
            if ((VisualizationMode & VisualizationMode.Surface) == VisualizationMode.Surface)
            {
                UpdateSurfaceState();
                surfaceOutlineRenderer.RenderAll(camera);
            } else
            {
                if ((VisualizationMode & (VisualizationMode.Outline | VisualizationMode.SimpleOutline)) != VisualizationMode.None)
                {
                    UpdateBrushState();
                    brushOutlineRenderer.RenderAll(camera);
                }
                // the selected surfaces shown in any tool, while asked (a part picked in the Surface Browser)
                if (showSelectedSurfaces && ChiselSurfaceSelectionManager.Selection.Count > 0)
                {
                    UpdateSurfaceState();
                    surfaceOutlineRenderer.RenderAll(camera);
                }
            }

            var handleRenderer = GetHandleRenderer(camera);
            handleRenderer.End();
            handleRenderer.RenderAll(camera);
            handleRenderer.Begin();
            Measurements.PaintDeferredLabels();

            var focus = Chisel.Editors.SceneHandleUtility.FocusControl;
            if (prevFocus != focus)
            {
                prevFocus = focus;
                SceneView.RepaintAll();
            }
        }
    }
}
