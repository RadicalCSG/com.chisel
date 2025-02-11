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

        #region Instance
        static ChiselOutlineRenderer s_Instance;
        public static ChiselOutlineRenderer Instance
        {
            get
            {
                if (s_Instance)
                    return s_Instance;

				var foundInstances = UnityEngine.Object.FindObjectsByType<ChiselOutlineRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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

        internal void OnSyncedBrushesChanged()
        {
            // Defer since we could potentially get several events before we actually render
            // also, not everything might be set up correctly just yet.
            s_UpdateBrushSelection = true;
            s_UpdateSurfaceSelection = true;
            s_ClearSurfaceCache = true;
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
                                foreach (var treeBrush in foundTreeBrushes)
                                {
                                    if (directSelected)
                                        brushDirectlySelected.Add(treeBrush);
                                    var outline = new BrushOutline(transform, treeBrush);
                                    foundBrushOutlines.Add(outline);
                                }
                            }
                        }
                    }
                }
            }

            if (foundTreeBrushes.Count == 0)
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
                    
                    // TODO: should only re-create the wireframe when the brush has changed it's hashes
                    var wireframe = ChiselWireframe.CreateWireframe(outline.brush);
                    brushOutlines[outline] = wireframe;
                }
            }
            
            foundBrushOutlines.Clear();
            s_UpdateBrushWireframe = true;
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
            s_UpdateBrushLineCache = true;
        }

        // TODO: put somewhere else
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

        void UpdateBrushState()
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
            if (s_UpdateBrushLineCache)
            {
                s_UpdateBrushLineCache = false;
                brushOutlineRenderer.Begin();

                foreach (var pair in brushOutlines)
                {
                    var wireframe = pair.Value;
                    if (wireframe == null)
                        continue;

                    var outline = pair.Key;
                    if (!outline.brush.Valid)
                        continue;

                    // TODO: simplify this
                    var wireframeValue	= pair.Value;
                    var modelTransform	= outline.transform;
                    //var brushes		= outline.brush.AllSynchronizedVariants;
                    //var anySelected	= ChiselSyncSelection.IsAnyBrushVariantSelected(brushes);

                    //foreach (var brush in brushes)
                    var brush = outline.brush;
                    var anySelected = ChiselSyncSelection.IsBrushVariantSelected(brush);
                    {
                        Matrix4x4 transformation;
                        if (modelTransform)
                            transformation = modelTransform.localToWorldMatrix * (Matrix4x4)brush.NodeToTreeSpaceMatrix;
                        else
                            transformation = brush.NodeToTreeSpaceMatrix;

                        if ((VisualizationMode & VisualizationMode.Outline) == VisualizationMode.Outline)
                        {
                            var directSelect = (ChiselUVMoveTool.IsActive() || ChiselUVRotateTool.IsActive() || ChiselUVScaleTool.IsActive()) &&
                                               ((brush == outline.brush && !anySelected) || (anySelected && ChiselSyncSelection.IsBrushVariantSelected(brush)));

                            // TODO: tweak look of selection, figure out how to do backfaced lighting of edges, for clarity
                            // TODO: support selecting surfaces/edges/points (without showing the entire object selection)
                            if (directSelect)
                                brushOutlineRenderer.DrawOutlines(transformation, wireframeValue, ColorManager.kSelectedOutlineColor, thickness: 3.0f, onlyInnerLines: false);
                            else
                                brushOutlineRenderer.DrawOutlines(transformation, wireframeValue, ColorManager.kUnselectedOutlineColor, thickness: 1.0f, onlyInnerLines: false);// (ChiselEditModeManager.EditMode == CSGEditMode.ShapeEdit));
                        }
                            
                        if ((VisualizationMode & VisualizationMode.SimpleOutline) == VisualizationMode.SimpleOutline)
                        {
                            brushOutlineRenderer.DrawSimpleOutlines(transformation, wireframeValue, ColorManager.kUnselectedOutlineColor);
                        }
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
                        surfaceOutlineRenderer.DrawOutlines(transformation, wireframe, ColorManager.kSelectedOutlineColor, thickness: 1.0f);
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
                        surfaceOutlineRenderer.DrawOutlines(transformation, wireframe, ColorManager.kSelectedHoverOutlineColor, thickness: 1.0f);
                    } else
                        surfaceOutlineRenderer.DrawOutlines(transformation, wireframe, ColorManager.kPreSelectedOutlineColor, thickness: 1.0f);
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
            if ((VisualizationMode & (VisualizationMode.Outline | VisualizationMode.SimpleOutline)) != VisualizationMode.None)
            {
                UpdateBrushState();
                brushOutlineRenderer.RenderAll(camera);
            }

            var handleRenderer = GetHandleRenderer(camera);
            handleRenderer.End();
            handleRenderer.RenderAll(camera);
            handleRenderer.Begin();
            
            var focus = Chisel.Editors.SceneHandleUtility.FocusControl;
            if (prevFocus != focus)
            {
                prevFocus = focus;
                SceneView.RepaintAll();
            }
        }
    }
}
