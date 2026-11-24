using System;
using System.Linq;
using System.Collections.Generic;
using Chisel.Core;
using Chisel.Components;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using UnityEngine.Pool;

namespace Chisel.Editors
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public class ChiselEditableOutline
    {
        public ChiselEditableOutline(ChiselBrushComponent brush)
        {
            this.brush = brush;
            this.brushMesh = new BrushMesh(this.brush.definition.BrushOutline);
            this.selection = ChiselTopologySelectionManager.Selection[this.brush][0];
            Rebuild();
        }

        const float kHighlightedEdgeThickness = 3.5f * 0.25f;
        const float kEdgeThickness            = 2.0f * 0.25f;
        const float kSoftEdgeDashSize         = 2.0f;


        public ChiselBrushComponent    brush;
        public BrushMesh               brushMesh;
        public ChiselTopologySelection selection;

        // These are temporary helpers to allow us to remap selection when modifying a brushMesh 
        // (so that selected vertices,edges, polygons still point to the 'correct' items after we removed something)
        public int[] vertexRemap;
        public int[] edgeRemap;
        public int[] polygonRemap;

		public List<int> tempPolygon1ToPolygon2 = new();


		#region Selection Methods
		public void SelectVertex(int vertexIndex, SelectionType selectionType)
        {
            Undo.RecordObject(ChiselTopologySelectionManager.Selection, "Selection modified");
            SelectVertex(brushMesh, selection, vertexIndex, selectionType);
            UpdateSelectionCenter();
        }

        public static void SelectVertex(BrushMesh brushMesh, ChiselTopologySelection selection, int vertexIndex, SelectionType selectionType)
        {
            if (vertexIndex < 0 ||
                brushMesh.vertices == null ||
                vertexIndex >= brushMesh.vertices.Length)
                return;

            bool additive = true;
            switch (selectionType)
            {
                case SelectionType.Replace: { additive = true; selection.Clear(); break; }
                case SelectionType.Additive: { additive = true; break; }
                case SelectionType.Subtractive: { additive = false; break; }
            }

            if (additive)
            {
                selection.selectedVertices.Add(vertexIndex);

                var halfEdges = brushMesh.halfEdges;
                for (int edgeIndex = 0; edgeIndex < halfEdges.Length; edgeIndex++)
                {
                    var twinIndex = halfEdges[edgeIndex].twinIndex;
                    if (halfEdges[edgeIndex].vertexIndex == vertexIndex)
                    {
                        if (selection.selectedVertices.Contains(halfEdges[twinIndex].vertexIndex))
                            selection.selectedEdges.Add(edgeIndex);
                    }
                    if (halfEdges[twinIndex].vertexIndex == vertexIndex)
                    {
                        if (selection.selectedVertices.Contains(halfEdges[edgeIndex].vertexIndex))
                            selection.selectedEdges.Add(edgeIndex);
                    }
                }

                var polygons = brushMesh.polygons;
                for (int polygonIndex = 0; polygonIndex < polygons.Length; polygonIndex++)
                {
                    if (!brushMesh.IsVertexIndexPartOfPolygon(polygonIndex, vertexIndex))
                        continue;

                    ref var polygon = ref polygons[polygonIndex];
                    var firstEdge = polygon.firstEdge;
                    var edgeCount = polygon.edgeCount;
                    var lastEdge = firstEdge + edgeCount;

                    bool allSelected = true;
                    for (int edgeIndex = firstEdge; edgeIndex < lastEdge; edgeIndex++)
                    {
                        var twinIndex = halfEdges[edgeIndex].twinIndex;
                        if (!selection.selectedEdges.Contains(edgeIndex) &&
                            !selection.selectedEdges.Contains(twinIndex))
                        {
                            allSelected = false;
                            break;
                        }
                    }
                    if (allSelected)
                        selection.selectedPolygons.Add(polygonIndex);
                }
            } else
            {
                selection.selectedVertices.Remove(vertexIndex);
                var allSelectedEdges    = selection.selectedEdges.ToArray();
                var allSelectedPolygons = selection.selectedPolygons.ToArray();
                var halfEdges           = brushMesh.halfEdges;
                foreach (var edgeIndex in allSelectedEdges)
                {
                    var twinIndex = halfEdges[edgeIndex].twinIndex;
                    if (halfEdges[edgeIndex].vertexIndex == vertexIndex) { selection.selectedEdges.Remove(edgeIndex); }
                    if (halfEdges[twinIndex].vertexIndex == vertexIndex) { selection.selectedEdges.Remove(edgeIndex); }
                }
                foreach (var polygonIndex in allSelectedPolygons)
                {
                    if (brushMesh.IsVertexIndexPartOfPolygon(polygonIndex, vertexIndex))
                    {
                        selection.selectedPolygons.Remove(polygonIndex);
                    }
                }
            }
        }

        public void SelectEdge(int currEdge, SelectionType selectionType)
        {
            Undo.RecordObject(ChiselTopologySelectionManager.Selection, "Selection modified");
            SelectEdge(brushMesh, selection, currEdge, selectionType);
            UpdateSelectionCenter();
        }

        public static void SelectEdge(BrushMesh brushMesh, ChiselTopologySelection selection, int currEdge, SelectionType selectionType)
        {
            if (currEdge < 0 ||
                brushMesh.halfEdges == null ||
                currEdge >= brushMesh.halfEdges.Length)
                return;

            var twinEdge        = brushMesh.halfEdges[currEdge].twinIndex;
            var currVertexIndex = brushMesh.halfEdges[currEdge].vertexIndex;
            var prevVertexIndex = brushMesh.halfEdges[twinEdge].vertexIndex;

            bool additive = true;
            switch (selectionType)
            {
                case SelectionType.Replace: { additive = true; selection.Clear(); break; }
                case SelectionType.Additive: { additive = true; break; }
                case SelectionType.Subtractive: { additive = false; break; }
            }

            if (additive)
            {
                selection.selectedVertices.Add(currVertexIndex);
                selection.selectedVertices.Add(prevVertexIndex);
                selection.selectedEdges.Add(currEdge);
                selection.selectedEdges.Add(twinEdge);

                var polygons    = brushMesh.polygons;
                var halfEdges   = brushMesh.halfEdges;
                for (int polygonIndex = 0; polygonIndex < polygons.Length; polygonIndex++)
                {
                    if (!brushMesh.IsEdgeIndexPartOfPolygon(polygonIndex, currEdge) &&
                        !brushMesh.IsEdgeIndexPartOfPolygon(polygonIndex, twinEdge))
                        continue;

                    ref var polygon = ref polygons[polygonIndex];
                    var firstEdge = polygon.firstEdge;
                    var edgeCount = polygon.edgeCount;
                    var lastEdge = firstEdge + edgeCount;

                    bool allSelected = true;
                    for (int edgeIndex = firstEdge; edgeIndex < lastEdge; edgeIndex++)
                    {
                        var twinIndex = halfEdges[edgeIndex].twinIndex;
                        if (!selection.selectedEdges.Contains(edgeIndex) &&
                            !selection.selectedEdges.Contains(twinIndex))
                        {
                            allSelected = false;
                            break;
                        }
                    }
                    if (allSelected)
                        selection.selectedPolygons.Add(polygonIndex);
                }
            } else
            {
                selection.selectedEdges.Remove(currEdge);
                selection.selectedEdges.Remove(twinEdge);

                // Deselect all polygons that have this edge
                var allSelectedPolygons = selection.selectedPolygons.ToArray();
                foreach (var polygonIndex in allSelectedPolygons)
                {
                    if (brushMesh.IsEdgeIndexPartOfPolygon(polygonIndex, currEdge) ||
                        brushMesh.IsEdgeIndexPartOfPolygon(polygonIndex, twinEdge))
                        selection.selectedPolygons.Remove(polygonIndex);
                }

                // Only deselect vertices if they're not selected by another selected edge or selected polygon
                if (!IsVertexIndirectlySelected(brushMesh, selection, currVertexIndex)) selection.selectedVertices.Remove(currVertexIndex);
                if (!IsVertexIndirectlySelected(brushMesh, selection, prevVertexIndex)) selection.selectedVertices.Remove(prevVertexIndex);
            }
        }

        public void SelectPolygon(int polygonIndex, SelectionType selectionType)
        {
            Undo.RecordObject(ChiselTopologySelectionManager.Selection, "Selection modified");
            SelectPolygon(brushMesh, selection, polygonIndex, selectionType);
            UpdateSelectionCenter();
        }

        public static void SelectPolygon(BrushMesh brushMesh, ChiselTopologySelection selection, int polygonIndex, SelectionType selectionType)
        {
            if (polygonIndex < 0 ||
                brushMesh.polygons == null ||
                polygonIndex >= brushMesh.polygons.Length)
                return;

            ref var polygon = ref brushMesh.polygons[polygonIndex];
            var firstEdge   = polygon.firstEdge;
            var edgeCount   = polygon.edgeCount;
            var lastEdge    = firstEdge + edgeCount;

            bool additive = true;
            switch (selectionType)
            {
                case SelectionType.Replace: { additive = true; selection.Clear(); break; }
                case SelectionType.Additive: { additive = true; break; }
                case SelectionType.Subtractive: { additive = false; break; }
            }

            if (additive)
            {
                selection.selectedPolygons.Add(polygonIndex);
                for (int e = firstEdge; e < lastEdge; e++)
                {
                    selection.selectedEdges.Add(e);
                    selection.selectedEdges.Add(brushMesh.halfEdges[e].twinIndex);
                    selection.selectedVertices.Add(brushMesh.halfEdges[e].vertexIndex);
                }
            } else
            {
                var halfEdges               = brushMesh.halfEdges;
                var halfEdgePolygonIndices  = brushMesh.halfEdgePolygonIndices;

                selection.selectedPolygons.Remove(polygonIndex);
                for (int edgeIndex = firstEdge; edgeIndex < lastEdge; edgeIndex++)
                {
                    var twinIndex = halfEdges[edgeIndex].twinIndex;
                    var twinPolygonIndex = halfEdgePolygonIndices[twinIndex];

                    selection.selectedEdges.Remove(edgeIndex);
                    selection.selectedEdges.Remove(twinIndex);
                    selection.selectedPolygons.Remove(twinPolygonIndex);
                }

                for (int edgeIndex = firstEdge; edgeIndex < lastEdge; edgeIndex++)
                {
                    var vertexIndex = halfEdges[edgeIndex].vertexIndex;
                    if (!IsVertexIndirectlySelected(brushMesh, selection, vertexIndex)) selection.selectedVertices.Remove(vertexIndex);
                }
            }
        }

        public static bool IsVertexIndirectlySelected(BrushMesh brushMesh, ChiselTopologySelection selection, int vertexIndex)
        {
            var halfEdges = brushMesh.halfEdges;
            // Check if vertex is part of edge
            foreach (var edgeIndex in selection.selectedEdges)
            {
                var twinIndex = halfEdges[edgeIndex].twinIndex;
                if (halfEdges[edgeIndex].vertexIndex == vertexIndex ||
                    halfEdges[twinIndex].vertexIndex == vertexIndex)
                    return true;
            }

            // Check if vertex is part of polygon
            foreach (var polygonIndex in selection.selectedPolygons)
            {
                if (brushMesh.IsVertexIndexPartOfPolygon(polygonIndex, vertexIndex))
                    return true;
            }
            return false;
        }

        void ClearSelection()
        {
            Undo.RecordObject(ChiselTopologySelectionManager.Selection, "Deselect all");
            selection.Clear();
        }

        void SelectAll()
        {
            Undo.RecordObject(ChiselTopologySelectionManager.Selection, "Select all");
            selection.Clear();

            for (int p = 0; p < brushMesh.polygons.Length; p++)
                selection.selectedPolygons.Add(p);

            for (int e = 0; e < brushMesh.halfEdges.Length; e++)
                selection.selectedEdges.Add(e);

            for (int v = 0; v < brushMesh.vertices.Length; v++)
                selection.selectedVertices.Add(v);
        }


        // Unity's scene view convention for a click: on its own it replaces the selection; Shift adds, or takes away the
        // active item (the one clicked last); the action key (Control, Command on macOS) adds, or takes away any selected
        // item, and wins from Shift
        internal static SelectionType ClickSelectionType(bool shift, bool actionKey, bool isSelected, bool isActive)
        {
            if (actionKey) return isSelected ? SelectionType.Subtractive : SelectionType.Additive;
            if (shift)     return isActive   ? SelectionType.Subtractive : SelectionType.Additive;
            return SelectionType.Replace;
        }

        bool IsActive(TopologyItemKind kind, int index)
        {
            if (selection.activeKind != kind)
                return false;
            if (selection.activeIndex == index)
                return true;
            // either half of an edge is the edge
            return kind == TopologyItemKind.Edge && index < brushMesh.halfEdges.Length &&
                   brushMesh.halfEdges[index].twinIndex == selection.activeIndex;
        }

        void SetActive(TopologyItemKind kind, int index, SelectionType selectionType)
        {
            if (selectionType != SelectionType.Subtractive)
                selection.SetActive(kind, index);
            else if (IsActive(kind, index))
                selection.SetActive(TopologyItemKind.None, -1);
        }

        internal void ClickVertex(int v, bool shift, bool actionKey, bool in2DMode = false, Vector3 cameraDirection = default)
        {
            var selectionType = ClickSelectionType(shift, actionKey, selection.selectedVertices.Contains(v), IsActive(TopologyItemKind.Vertex, v));
            if (in2DMode)
                SelectVertexViewAligned(v, cameraDirection, selectionType);
            else
                SelectVertex(v, selectionType);
            SetActive(TopologyItemKind.Vertex, v, selectionType);
        }

        internal void ClickEdge(int e, bool shift, bool actionKey)
        {
            var selectionType = ClickSelectionType(shift, actionKey, selection.selectedEdges.Contains(e), IsActive(TopologyItemKind.Edge, e));
            SelectEdge(e, selectionType);
            SetActive(TopologyItemKind.Edge, e, selectionType);
        }

        internal void ClickPolygon(int p, bool shift, bool actionKey)
        {
            var selectionType = ClickSelectionType(shift, actionKey, selection.selectedPolygons.Contains(p), IsActive(TopologyItemKind.Polygon, p));
            SelectPolygon(p, selectionType);
            SetActive(TopologyItemKind.Polygon, p, selectionType);
        }

        static public void RemapSelection(BrushMesh brushMesh, ChiselTopologySelection selection, int[] vertexRemap, int[] edgeRemap, int[] polygonRemap)
        {
            Undo.RecordObject(ChiselTopologySelectionManager.Selection, "Remapping selection");

            if (vertexRemap  == null &&
                edgeRemap    == null &&
                polygonRemap == null)
            {
                // since we don't have anything to remap, assume we didn't actually remove anything (in which case our selection *should* still be valid?
                return;
            }

            var halfEdges = brushMesh.halfEdges;

            var prevVertexSelection     = selection.selectedVertices.ToArray();
            var prevEdgeSelection       = selection.selectedEdges.ToArray();
            var prevPolygonSelection    = selection.selectedPolygons.ToArray();
            var activeKind              = selection.activeKind;
            var activeIndex             = selection.activeIndex;
            selection.Clear();
            selection.SetActive(activeKind, activeIndex);
            selection.RemapActive(TopologyItemKind.Vertex,  vertexRemap);
            selection.RemapActive(TopologyItemKind.Edge,    edgeRemap);
            selection.RemapActive(TopologyItemKind.Polygon, polygonRemap);


            if (vertexRemap != null)
            {
                for (int v = 0; v < prevVertexSelection.Length; v++)
                {
                    var vertexIndex = vertexRemap[prevVertexSelection[v]];
                    if (vertexIndex == -1)
                        continue;
                    SelectVertex(brushMesh, selection, vertexIndex, SelectionType.Additive);
                }
            }

            if (edgeRemap != null)
            {
                for (int e = 0; e < prevEdgeSelection.Length; e++)
                {
                    var edgeIndex = edgeRemap[prevEdgeSelection[e]];
                    if (edgeIndex == -1)
                        continue;
                    SelectEdge(brushMesh, selection, edgeIndex, SelectionType.Additive);
                }
            }

            if (polygonRemap != null)
            {
                for (int p = 0; p < prevPolygonSelection.Length; p++)
                {
                    var polygonIndex = polygonRemap[prevPolygonSelection[p]];
                    if (polygonIndex == -1)
                        continue;
                    SelectPolygon(brushMesh, selection, polygonIndex, SelectionType.Additive);
                }
            }
        }
        #endregion


        #region Modification Methods
        // The selected vertices removed: those of a selected edge or polygon too, so an edge goes with its vertices. Left
        // as it is when too few would be left, or all in one plane (BrushMesh.RemoveVertices)
        internal bool DeleteSelectedVertices()
        {
            if (selection.selectedVertices.Count == 0)
                return false;
            var removed = new HashSet<int>(selection.selectedVertices);
            var before  = new BrushMesh(brushMesh);
            if (!before.RemoveVertices(removed))
                return false;

            Undo.RecordObjects(new UnityEngine.Object[] { brush, ChiselTopologySelectionManager.Selection }, "Delete selected vertices");
            brushMesh.vertices               = before.vertices;
            brushMesh.halfEdges              = before.halfEdges;
            brushMesh.polygons               = before.polygons;
            brushMesh.planes                 = before.planes;
            brushMesh.halfEdgePolygonIndices = before.halfEdgePolygonIndices;
            brushMesh.halfEdgeUVs            = null;
            brushMesh.halfEdgeNormals        = null;
            ClearSelection();
            return true;
        }

        void MoveSelectedVertices(Vector3 offset)
        {
            if (offset.sqrMagnitude == 0)
                return;

            var vertices = brushMesh.vertices;
            Undo.RecordObject(brush, "Changed shape of Brush");
            foreach (var v in selection.selectedVertices)
                vertices[v] += (float3)offset;
            brushMesh.CalculatePlanes();
        }

        // The handle on the selected vertices while a brush is edited: Tool.Move, Tool.Rotate or Tool.Scale
        internal static Tool VertexHandle = Tool.Move;

        // The selected vertices where they were when a rotation or scale started, and its pivot: each step of a drag is
        // worked out from them, so the steps don't add up
        int[]    transformVertices;
        float3[] transformStart;
        float3   transformPivot;

        internal void BeginVertexTransform()
        {
            var count = selection.selectedVertices.Count;
            if (transformVertices == null || transformVertices.Length != count)
            {
                transformVertices = new int[count];
                transformStart    = new float3[count];
            }
            selection.selectedVertices.CopyTo(transformVertices);
            for (int i = 0; i < count; i++)
                transformStart[i] = brushMesh.vertices[transformVertices[i]];
            transformPivot = vertexSelectionCenter;
        }

        // the selected vertices turned about the pivot, from where they started
        internal void RotateSelectedVertices(Quaternion rotation)
        {
            if (transformVertices == null)
                return;
            var turn     = (quaternion)rotation;
            var vertices = brushMesh.vertices;
            Undo.RecordObject(brush, "Rotated vertices of Brush");
            for (int i = 0; i < transformVertices.Length; i++)
                vertices[transformVertices[i]] = transformPivot + math.mul(turn, transformStart[i] - transformPivot);
            brushMesh.CalculatePlanes();
        }

        // the selected vertices stretched from the pivot along the axes, from where they started
        internal void ScaleSelectedVertices(Vector3 scale, Quaternion axes)
        {
            if (transformVertices == null)
                return;
            var turn     = (quaternion)axes;
            var unturn   = math.inverse(turn);
            var stretch  = (float3)scale;
            var vertices = brushMesh.vertices;
            Undo.RecordObject(brush, "Scaled vertices of Brush");
            for (int i = 0; i < transformVertices.Length; i++)
                vertices[transformVertices[i]] = transformPivot + math.mul(turn, math.mul(unturn, transformStart[i] - transformPivot) * stretch);
            brushMesh.CalculatePlanes();
        }

        // The axes of the rotate and scale handles, in the brush's space: the brush's own for Local, the world's for Global
        internal static Quaternion VertexHandleAxes(PivotRotation pivotRotation, Matrix4x4 brushToWorld)
        {
            return pivotRotation == PivotRotation.Local ? Quaternion.identity : Quaternion.Inverse(brushToWorld.rotation);
        }

        void SplitPolygonBetweenTwoVertices(int polygonIndex, int vertexIndex1, int vertexIndex2)
        {
            Undo.RecordObjects(new UnityEngine.Object[] { brush, ChiselTopologySelectionManager.Selection }, "Split polygon between two vertices");
            var indexIn = brushMesh.FindPolygonEdgeByVertexIndex(polygonIndex, vertexIndex1);
            var indexOut = brushMesh.FindPolygonEdgeByVertexIndex(polygonIndex, vertexIndex2);

            brushMesh.SplitPolygon(polygonIndex, indexOut, indexIn);
            var edgeIndex = brushMesh.FindEdgeByVertexIndices(vertexIndex1, vertexIndex2);
            if (edgeIndex != -1)
            {
                SelectEdge(edgeIndex, SelectionType.Replace);
            } else
                ClearSelection();
        }

        void CombinePolygonsOnEdge(int edgeIndex)
        {
            ClearSelection();
            Undo.RecordObject(brush, "Combine polygons by removing edge");
            brushMesh.RemoveEdge(edgeIndex);
        }
        #endregion


        // Rebuild all mesh specific computations
        public void Rebuild()
        {
            tempPolygon1ToPolygon2.Clear();

			FindSoftEdges();

            if (!IsValidBrush())
                return;

            EnsureCorrectSizes();

            for (int p = 0; p < polygonCenters.Length; p++)
                polygonCenters[p] = brushMesh.GetPolygonCentroid(p);

            UpdateSelectionCenter();
        }

        // Whether the outline can be edited: whole topology only, so a brush whose shape is invalid (concave, flat) keeps
        // its handles and can be dragged back
        bool IsValidBrush()
        {
            if (brushMesh == null ||
                brushMesh.vertices == null || brushMesh.vertices.Length == 0 ||
                brushMesh.halfEdges == null || brushMesh.halfEdges.Length == 0 ||
                brushMesh.polygons == null || brushMesh.polygons.Length == 0)
                return false;
            if (!brushMesh.ValidateData(out _))
                return false;
            if (brushMesh.planes == null || brushMesh.planes.Length != brushMesh.polygons.Length)
                brushMesh.CalculatePlanes();
            return true;
        }


        struct SoftEdge
        {
            public int vertexIndex1;
            public int vertexIndex2;
            public int afterPolygonIndexA;
            public int afterPolygonIndexB;

            public Vector3 afterPolygonCenterA;
            public Vector3 afterPolygonCenterB;
            public Vector4 afterLocalPlaneA;
            public Vector4 afterLocalPlaneB;
        }

        public Color[]      vertexColors;
        public Color[]      edgeColors;
        public Color[]      polygonColors;
        public Color[]      softEdgeColors;

        public Vector3[]    polygonCenters;
        public Vector3      vertexSelectionCenter = Vector3.zero;

        static SoftEdge[]   softEdges;

        enum ItemState : byte
        {
            None,
            Frontfaced  = 1,

            Hovering    = 2,
            Active      = 4,
            Selected    = 8
        };

        // We don't want to allocate memory every frame, so we keep some 
        // temporary arrays and only resize them when necessary
        #region Allocations
        static ItemState[]  s_TempPolygonsState = null;
        static int[]        s_TempPolygonsIDs = null;
        static int          s_TempPolygonsIDCount = 0;
        static ItemState[]  s_TempVerticesState = null;
        static int[]        s_TempVerticesIDs = null;
        static int          s_TempVerticesIDCount = 0;
        static ItemState[]  s_TempEdgesState = null;
        static int[]        s_TempEdgesIDs = null;
        static int          s_TempEdgesIDCount = 0;
        static ItemState[]  s_TempSoftEdgesState = null;
        static int[]        s_TempSoftEdgesIDs = null;
        static int          s_TempSoftEdgesIDCount = 0;

        void EnsureCorrectSizes()
        {
            if (vertexColors == null ||
                vertexColors.Length != brushMesh.vertices.Length)
                vertexColors = new Color[brushMesh.vertices.Length];

            if (edgeColors == null ||
                edgeColors.Length != brushMesh.halfEdges.Length)
                edgeColors = new Color[brushMesh.halfEdges.Length];

            if (polygonColors == null ||
                polygonColors.Length != brushMesh.polygons.Length)
                polygonColors = new Color[brushMesh.polygons.Length];

            if (polygonCenters == null ||
                polygonCenters.Length != brushMesh.polygons.Length)
                polygonCenters = new Vector3[brushMesh.polygons.Length];

            if (softEdgeColors == null ||
                softEdgeColors.Length != softEdges.Length)
                softEdgeColors = new Color[softEdges.Length];
        }

        void EnsureTemporaryArraySize()
        {
            var polygons = brushMesh.polygons;
            var vertices = brushMesh.vertices;
            var halfEdges = brushMesh.halfEdges;

            if (s_TempPolygonsState == null ||
                s_TempPolygonsState.Length < polygons.Length)
            {
                s_TempPolygonsState = new ItemState[polygons.Length];
                s_TempPolygonsIDs = new int[polygons.Length];
            }

            if (s_TempVerticesState == null ||
                s_TempVerticesState.Length < vertices.Length)
            {
                s_TempVerticesState = new ItemState[vertices.Length];
                s_TempVerticesIDs = new int[vertices.Length];
            }

            if (s_TempEdgesState == null ||
                s_TempEdgesState.Length < halfEdges.Length)
            {
                s_TempEdgesState = new ItemState[halfEdges.Length];
                s_TempEdgesIDs = new int[halfEdges.Length];
            }

            if (s_TempSoftEdgesState == null ||
                s_TempSoftEdgesState.Length < softEdges.Length)
            {
                s_TempSoftEdgesState = new ItemState[softEdges.Length];
                s_TempSoftEdgesIDs = new int[softEdges.Length];
            }

            s_TempPolygonsIDCount = 0;
            s_TempVerticesIDCount = 0;
            s_TempEdgesIDCount = 0;
            s_TempSoftEdgesIDCount = 0;
        }
        #endregion

        #region UpdateControlIDs
        static SceneHandles.PositionHandleIDs s_TempPositionHandleIDs;

        internal static readonly int kPolygonHash = "Polygon".GetHashCode();
        internal static readonly int kVertexHash = "Vertex".GetHashCode();
        internal static readonly int kEdgeHash = "Edge".GetHashCode();
        internal static readonly int kSoftEdgeHash = "SoftEdge".GetHashCode();

        void UpdateControlIDs()
        {
            var polygons = brushMesh.polygons;
            var vertices = brushMesh.vertices;
            var halfEdges = brushMesh.halfEdges;

            SceneHandles.Initialize(ref s_TempPositionHandleIDs);

            s_TempPolygonsIDCount   = polygons.Length;
            s_TempVerticesIDCount   = vertices.Length;
            s_TempEdgesIDCount      = halfEdges.Length;
            s_TempSoftEdgesIDCount  = softEdges.Length;

            for (int p = 0; p < s_TempPolygonsIDCount; p++)
                s_TempPolygonsIDs[p] = GUIUtility.GetControlID(kPolygonHash, FocusType.Keyboard);

            for (int v = 0; v < s_TempVerticesIDCount; v++)
                s_TempVerticesIDs[v] = GUIUtility.GetControlID(kVertexHash, FocusType.Keyboard);

            for (int e = 0; e < s_TempEdgesIDCount; e++)
                s_TempEdgesIDs[e] = (halfEdges[e].twinIndex > e) ? 0 : GUIUtility.GetControlID(kEdgeHash, FocusType.Keyboard);

            for (int e = 0; e < s_TempSoftEdgesIDCount; e++)
                s_TempSoftEdgesIDs[e] = GUIUtility.GetControlID(kSoftEdgeHash, FocusType.Keyboard);
        }
        #endregion


        public void UpdateSelectionCenter()
        {
            vertexSelectionCenter = Vector3.zero;
            if (selection.selectedVertices.Count == 0)
                return;

            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;

            var vertices = brushMesh.vertices;
            foreach (var v in selection.selectedVertices)
            {
                min.x = Mathf.Min(min.x, vertices[v].x);
                min.y = Mathf.Min(min.y, vertices[v].y);
                min.z = Mathf.Min(min.z, vertices[v].z);

                max.x = Mathf.Max(max.x, vertices[v].x);
                max.y = Mathf.Max(max.y, vertices[v].y);
                max.z = Mathf.Max(max.z, vertices[v].z);
            }

            vertexSelectionCenter = (min + max) * 0.5f;
        }

		// Finds all edges that have been created when optimizing the outline mesh.
		// This is possible when not all vertices on a polygon actually lie on the same plane and we're forced to split that polygon
		#region FindSoftEdges
		void FindSoftEdges()
        {
            softEdges = new SoftEdge[0];
            //brushContainers.Clear();
            //if (!brush.GetUsedGeneratedBrushes(brushContainers))
            //    return;

            //var brushMeshes = (brushContainers == null || brushContainers.Count != 1) ? null : brushContainers[0].BrushMeshes;
            var afterBrushMesh = brush.BrushMesh;
            if (afterBrushMesh == null)
                return;

            var beforeBrushMesh = brushMesh;

            var afterHalfEdges  = afterBrushMesh.halfEdges;
            var beforeHalfEdges = beforeBrushMesh.halfEdges;

            // If before and after have the same number of edges, then we don't have any soft edges
            if (afterHalfEdges.Length == beforeHalfEdges.Length)
                return;


            var afterPolygons = afterBrushMesh.polygons;
            var beforePolygons = beforeBrushMesh.polygons;
            var afterHalfEdgePolygonIndices = afterBrushMesh.halfEdgePolygonIndices;
            var beforeHalfEdgePolygonIndices = beforeBrushMesh.halfEdgePolygonIndices;

            var afterPlanes     = afterBrushMesh.planes;
            //var afterVertices = afterBrushMesh.vertices;


            tempPolygon1ToPolygon2.Clear();
            for (int i = 0; i < afterPolygons.Length; i++)
                tempPolygon1ToPolygon2.Add(-1);

            // NOTE: assumes vertices are the same on both the original brushMesh and the optimized one
            var softEdgeList = new List<SoftEdge>();
            for (int afterEdgeA = 0; afterEdgeA < afterHalfEdges.Length; afterEdgeA++)
            {
                var afterPolygonIndexA = afterHalfEdgePolygonIndices[afterEdgeA];
                var afterPolygon = afterPolygons[afterPolygonIndexA];
                var afterEdgeB = afterPolygon.firstEdge + ((afterEdgeA + 1 - afterPolygon.firstEdge) % afterPolygon.edgeCount);
                var afterPolygonIndexB = afterHalfEdgePolygonIndices[afterHalfEdges[afterEdgeB].twinIndex];
                var afterVertexIndexA = afterHalfEdges[afterEdgeA].vertexIndex;
                var afterVertexIndexB = afterHalfEdges[afterEdgeB].vertexIndex;

                bool found = false;
                for (int beforeEdgeA = 0; beforeEdgeA < beforeHalfEdges.Length; beforeEdgeA++)
                {
                    var beforePolygonIndexA = beforeHalfEdgePolygonIndices[beforeEdgeA];
                    var beforePolygon = beforePolygons[beforePolygonIndexA];
                    var beforeEdgeB = beforePolygon.firstEdge + ((beforeEdgeA + 1 - beforePolygon.firstEdge) % beforePolygon.edgeCount);
                    var beforePolygonIndexB = beforeHalfEdgePolygonIndices[beforeHalfEdges[beforeEdgeB].twinIndex];
                    var beforeVertexIndexA = beforeHalfEdges[beforeEdgeA].vertexIndex;
                    var beforeVertexIndexB = beforeHalfEdges[beforeEdgeB].vertexIndex;

                    if (afterVertexIndexA != beforeVertexIndexA ||
                        afterVertexIndexB != beforeVertexIndexB)
                        continue;

                    tempPolygon1ToPolygon2[afterPolygonIndexA] = beforePolygonIndexA;
                    tempPolygon1ToPolygon2[afterPolygonIndexB] = beforePolygonIndexB;
                    found = true;
                    break;
                }

                if (found)
                    continue;

                // Skips half of the edges since they are identical
                if (afterVertexIndexA < afterVertexIndexB)
                    continue;

                softEdgeList.Add(new SoftEdge()
                {
                    vertexIndex1 = afterVertexIndexA,
                    vertexIndex2 = afterVertexIndexB,
                    afterPolygonIndexA = afterPolygonIndexA,
                    afterPolygonIndexB = afterPolygonIndexB,
                    afterPolygonCenterA = afterBrushMesh.GetPolygonCentroid(afterPolygonIndexA),
                    afterPolygonCenterB = afterBrushMesh.GetPolygonCentroid(afterPolygonIndexB),
                    afterLocalPlaneA = afterPlanes[afterPolygonIndexA],
                    afterLocalPlaneB = afterPlanes[afterPolygonIndexB]
                });
            }
            softEdges = softEdgeList.ToArray();
        }
        #endregion

        enum ClickState
        {
            None,
            Dragged,

            MouseDown,
            Clicked,
            DoubleClicked,
            MouseUp,

            Hover,


            DeselectAll,
            SelectAll,
            Delete
        }

        #region HandleClick

        void SetControl(Event evt, int id)
        {
            GUIUtility.hotControl = id;
            GUIUtility.keyboardControl = id;
            evt.Use();
            EditorGUIUtility.SetWantsMouseJumping((id == 0) ? 0 : 1);
        }

        bool CanUseMouseDown(Event evt, int id)
        {
            if (GUIUtility.hotControl != 0)
                return false;

            if ((UnityEditor.HandleUtility.nearestControl != id || evt.button != 0) &&
                (GUIUtility.keyboardControl != id || evt.button != 2))
                return false;
            return true;
        }

        bool CanUseMouseUp(Event evt, int id)
        {
            if (GUIUtility.hotControl != id || (evt.button != 0 && evt.button != 2))
                return false;
            return true;
        }

        const EventModifiers    kKeyModifiers       = EventModifiers.Shift | EventModifiers.Control | EventModifiers.Alt | EventModifiers.Command;
        
        const string    kSelectAllCommand       = "SelectAll";
        const string    kSoftDeleteCommand      = "SoftDelete";

        int         clickCount          = 0;


        ClickState HandleClick(int id, bool captureControl = false)
        {
            if (id == 0)
                return ClickState.None;

            var evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                case EventType.MouseMove:
                {
                    if (SceneHandleUtility.FocusControl != id)
                        break;

                    return ClickState.Hover;
                }

                case EventType.MouseDown:
                {
                    if (!CanUseMouseDown(evt, id))
                        break;

                    if (captureControl)
                        SetControl(evt, id);

                    // Unfortunately the clickCount is always 1 at mouseUp, so we need to remember it
                    clickCount = evt.clickCount;

                    return ClickState.MouseDown;
                }

                case EventType.MouseDrag:
                {
                    // Once we start dragging, we're no longer clicking, so set it to 0
                    clickCount = 0;
                    if (GUIUtility.hotControl != id)
                        break;

                    return ClickState.Dragged;
                }

                case EventType.MouseUp:
                {
                    if (!CanUseMouseUp(evt, id))
                        break;

                    if (captureControl)
                    {
                        SetControl(evt, 0);
                        Snapping.ActiveAxes = Axes.XYZ;
                        SceneView.RepaintAll();
                    }

                    switch (clickCount)
                    {
                        default: return ClickState.MouseUp;
                        case 1:  return ClickState.Clicked;
                        case 2:  return ClickState.DoubleClicked;
                    }
                }

                case EventType.KeyDown:
                {
                    if (evt.keyCode == ChiselKeyboardDefaults.kCancelKey && ((evt.modifiers & kKeyModifiers) == EventModifiers.None))
                        evt.Use();
                    break;
                }

                case EventType.KeyUp:
                {
                    if (evt.keyCode == ChiselKeyboardDefaults.kCancelKey && ((evt.modifiers & kKeyModifiers) == EventModifiers.None))
                    {
                        evt.Use();
                        return ClickState.DeselectAll;
                    }
                    break;
                }

                case EventType.ValidateCommand:
                {
                    if (evt.commandName == kSelectAllCommand ||
                        evt.commandName == kSoftDeleteCommand)
                        evt.Use();
                    break;
                }

                case EventType.ExecuteCommand:
                {
                    if (evt.commandName == kSelectAllCommand)
                    {
                        evt.Use();
                        return ClickState.SelectAll;
                    }
                    if (evt.commandName == kSoftDeleteCommand)
                    {
                        evt.Use();
                        return ClickState.Delete;
                    }
                    break;
                }
            }
            return ClickState.None;
        }
        #endregion

        #region Update Colors
        ItemState CalculateState(bool isHovering, bool isActive, bool isSelected)
        {
            return (isHovering ? ItemState.Hovering : ItemState.None) |
                   (isActive ? ItemState.Active : ItemState.None) |
                   (isSelected ? ItemState.Selected : ItemState.None);
        }

        ItemState CalculateState(bool isHovering, bool isActive, bool isSelected, bool isFrontFacing)
        {
            return (isHovering ? ItemState.Hovering : ItemState.None) |
                   (isActive ? ItemState.Active : ItemState.None) |
                   (isSelected ? ItemState.Selected : ItemState.None) |
                   (isFrontFacing ? ItemState.Frontfaced : ItemState.None);
        }

        bool IsPolygonFrontfacing(Vector3 polygonCenter, Vector4 localPlane, Vector3 cameraLocalPos, Vector3 cameraLocalForward, bool isCameraInsideOutline, bool isCameraOrthographic)
        {
            var localNormal = (Vector3)localPlane;
            if (isCameraInsideOutline ||
                localNormal.sqrMagnitude == 0)
                return false;

            var cosV = isCameraOrthographic ? Vector3.Dot(localNormal, cameraLocalForward) :
                                              Vector3.Dot(localNormal, (polygonCenter - cameraLocalPos));
            return (cosV < -0.0001f);
        }

        bool IsPointInsideOutline(Vector3 point)
        {
            //brushContainers.Clear();
            //if (!brush.GetUsedGeneratedBrushes(brushContainers))
            //    return false;

            //var brushMeshes = (brushContainers.Count != 1) ? null : brushContainers[0].BrushMeshes;
            var afterBrushMesh = brush.BrushMesh;// (brushMeshes == null) ? null : brushMeshes[0];
            if (afterBrushMesh == null)
                return false;

            return afterBrushMesh.IsInsideOrOn(point);
        }

        // Which faces, edges and vertices are shown, and so can be clicked or marquee selected: those of a face turned
        // towards the camera; every one when back faces are selectable too (ChiselEditorSettings.SelectBackFaces), or
        // when the camera is inside the brush, where it sees them all from behind
        bool[] polygonShown;
        bool[] edgeShown;
        bool[] vertexShown;
        // the vertices shown inside the marquee's rectangle
        bool[] vertexInMarquee;

        internal bool IsPolygonShown(int p) => polygonShown == null || p >= polygonShown.Length || polygonShown[p];
        internal bool IsEdgeShown(int e)    => edgeShown    == null || e >= edgeShown.Length    || edgeShown[e];
        internal bool IsVertexShown(int v)  => vertexShown  == null || v >= vertexShown.Length  || vertexShown[v];

        internal void UpdateVisibility(Vector3 cameraLocalPosition, Vector3 cameraLocalForward, bool orthographic)
        {
            var polygons  = brushMesh.polygons;
            var planes    = brushMesh.planes;
            var halfEdges = brushMesh.halfEdges;
            if (polygonShown == null || polygonShown.Length != polygons.Length)          polygonShown = new bool[polygons.Length];
            if (edgeShown    == null || edgeShown.Length    != halfEdges.Length)         edgeShown    = new bool[halfEdges.Length];
            if (vertexShown  == null || vertexShown.Length  != brushMesh.vertices.Length) vertexShown  = new bool[brushMesh.vertices.Length];
            Array.Clear(edgeShown, 0, edgeShown.Length);
            Array.Clear(vertexShown, 0, vertexShown.Length);

            var all = ChiselEditorSettings.SelectBackFaces || IsPointInsideOutline(cameraLocalPosition);
            for (int p = 0; p < polygons.Length; p++)
            {
                var shown = all || IsPolygonFrontfacing(polygonCenters[p], planes[p], cameraLocalPosition, cameraLocalForward, false, orthographic);
                polygonShown[p] = shown;
                if (!shown)
                    continue;
                var firstEdge = polygons[p].firstEdge;
                var lastEdge  = firstEdge + polygons[p].edgeCount;
                for (int e = firstEdge; e < lastEdge; e++)
                {
                    edgeShown[e] = true;
                    edgeShown[halfEdges[e].twinIndex] = true;
                    vertexShown[halfEdges[e].vertexIndex] = true;
                }
            }
        }

        // Vertices, edges and faces exactly behind each other on screen, as in an axis aligned orthographic view, are at
        // the same distance from the mouse; their pick distances get a bias of at most this many points, growing with
        // depth over the brush, so the nearest is picked
        internal const float kPickTieBias = 0.01f;

        Vector3 pickDepthDirection;
        float   pickDepthMin;
        float   pickDepthScale;

        // the direction in the brush's space whose dot product with a point orders it by depth as in the world
        internal static Vector3 LocalDepthDirection(Matrix4x4 localToWorld, Vector3 worldForward)
        {
            return localToWorld.transpose.MultiplyVector(worldForward);
        }

        internal void UpdatePickDepth(Vector3 localDepthDirection)
        {
            var vertices = brushMesh.vertices;
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            for (int v = 0; v < vertices.Length; v++)
            {
                var depth = Vector3.Dot(vertices[v], localDepthDirection);
                min = Mathf.Min(min, depth);
                max = Mathf.Max(max, depth);
            }
            pickDepthDirection = localDepthDirection;
            pickDepthMin       = min;
            pickDepthScale     = max > min ? kPickTieBias / (max - min) : 0;
        }

        internal float PickDistance(float distance, Vector3 localPosition)
        {
            var bias = (Vector3.Dot(localPosition, pickDepthDirection) - pickDepthMin) * pickDepthScale;
            return distance + Mathf.Clamp(bias, 0, kPickTieBias);
        }

        // The vertices shown inside the rectangle (in GUI points; toGUI gives a vertex's)
        void MarkVerticesInRect(Rect rect, Func<Vector3, Vector2> toGUI)
        {
            var vertices = brushMesh.vertices;
            if (vertexInMarquee == null || vertexInMarquee.Length != vertices.Length)
                vertexInMarquee = new bool[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
                vertexInMarquee[v] = IsVertexShown(v) && rect.Contains(toGUI(vertices[v]));
        }

        // The marked vertices selected, added or taken away, with the edges and faces all their vertices are of
        static void SelectVertices(BrushMesh brushMesh, ChiselTopologySelection selection, bool[] marked, SelectionType selectionType)
        {
            if (selectionType == SelectionType.Replace)
                selection.Clear();
            var each = selectionType == SelectionType.Subtractive ? SelectionType.Subtractive : SelectionType.Additive;
            for (int v = 0; v < marked.Length; v++)
            {
                if (marked[v])
                    SelectVertex(brushMesh, selection, v, each);
            }
        }

        internal void SelectVerticesInRect(Rect rect, SelectionType selectionType, Func<Vector3, Vector2> toGUI)
        {
            Undo.RecordObject(ChiselTopologySelectionManager.Selection, "Selection modified");
            MarkVerticesInRect(rect, toGUI);
            SelectVertices(brushMesh, selection, vertexInMarquee, selectionType);
            UpdateSelectionCenter();
        }

        // the selection as the marquee will leave it when released, shown while it is dragged
        ChiselTopologySelection marqueePreview;

        internal ChiselTopologySelection MarqueePreview(Rect rect, SelectionType selectionType, Func<Vector3, Vector2> toGUI)
        {
            MarkVerticesInRect(rect, toGUI);
            marqueePreview ??= new ChiselTopologySelection();
            marqueePreview.CopyFrom(selection);
            SelectVertices(brushMesh, marqueePreview, vertexInMarquee, selectionType);
            return marqueePreview;
        }

        void UpdateStateColors(Color[] colors, ItemState[] states, bool validState, Color backfacedColor, Color frontfacedColor, Color invalidColor)
        {
            for (int i = 0; i < colors.Length; i++)
            {
                var state = states[i];
                var isSelected = (state & ItemState.Selected) == ItemState.Selected;
                var isHovering = (state & ItemState.Hovering) == ItemState.Hovering;
                var isActive = (state & ItemState.Active) == ItemState.Active;
                var isBackfaced = (state & ItemState.Frontfaced) != ItemState.Frontfaced;
                var baseColor = (isBackfaced && !isActive && !isHovering) ? backfacedColor : frontfacedColor;
                colors[i] = !validState ? invalidColor : SceneHandles.StateColor(baseColor, SceneHandles.Disabled, isSelected: (isActive || isSelected), isPreSelected: isHovering);
            }
        }

        public bool UpdateColors(Camera camera)
        {
            if (!IsValidBrush())
                return false;

            EnsureTemporaryArraySize();
            UpdateControlIDs();

            // on every event: what is shown is what can be clicked
            UpdateVisibility(SceneHandles.InverseMatrix.MultiplyPoint(camera.transform.position),
                             SceneHandles.InverseMatrix.MultiplyVector(camera.transform.forward),
                             camera.orthographic);
            UpdatePickDepth(LocalDepthDirection(SceneHandles.Matrix, camera.transform.forward));

            if (Event.current.type != EventType.Repaint)
                return true;

            var validState = brush.definition.ValidState;
            var baseColors = BaseColors();

            var currentHotControl       = GUIUtility.hotControl;
            var currentFocusControl     = SceneHandleUtility.FocusControl;
            var currDisabled            = SceneHandles.Disabled;

            var cameraTransform         = camera.transform;
            var cameraLocalPos          = SceneHandles.InverseMatrix.MultiplyPoint(cameraTransform.position);
            var cameraLocalForward      = SceneHandles.InverseMatrix.MultiplyVector(cameraTransform.forward);
            var isCameraOrthographic    = camera.orthographic;
            var isCameraInsideOutline   = IsPointInsideOutline(cameraLocalPos);

            var polygons                = brushMesh.polygons;
            var planes                  = brushMesh.planes;
            var vertices                = brushMesh.vertices;
            var halfEdges               = brushMesh.halfEdges;
            var halfEdgePolygonIndices  = brushMesh.halfEdgePolygonIndices;

            Array.Clear(s_TempPolygonsState, 0, polygons.Length);
            Array.Clear(s_TempVerticesState, 0, vertices.Length);
            Array.Clear(s_TempEdgesState, 0, halfEdges.Length);
            Array.Clear(s_TempSoftEdgesState, 0, softEdges.Length);

            // while the marquee is dragged, the selection is shown as it will be when it is released
            var shownSelection = ChiselBrushMarquee.Dragging
                ? MarqueePreview(ChiselBrushMarquee.Rect, ChiselBrushMarquee.DraggingType, position => HandleUtility.WorldToGUIPoint(position))
                : selection;

            for (int v = 0; v < s_TempVerticesIDCount; v++)
            {
                var id = s_TempVerticesIDs[v];

                var isActive    = currentHotControl == id;
                var isHovering  = currentFocusControl == id;
                var isSelected  = shownSelection.selectedVertices.Contains(v);

                var vertexState = CalculateState(isHovering, isActive, isSelected);

                s_TempVerticesState[v] = vertexState;
            }

            for (int e = 0; e < halfEdges.Length; e++)
            {
                var id = s_TempEdgesIDs[e];
                if (id == 0)
                    continue;

                var twin        = halfEdges[e].twinIndex;
                var currVertex  = halfEdges[e].vertexIndex;
                var prevVertex  = halfEdges[twin].vertexIndex;

                var isActive    = (currentHotControl == id);
                var isHovering  = currentFocusControl == id;
                var isSelected  = shownSelection.selectedEdges.Contains(e);

                var edgeState   = CalculateState(isHovering, isActive, isSelected);

                s_TempVerticesState[currVertex] |= edgeState;
                s_TempVerticesState[prevVertex] |= edgeState;

                s_TempEdgesState[e] |= edgeState;
                s_TempEdgesState[twin] |= edgeState;
            }

            for (int p = 0; p < polygons.Length; p++)
            {
                var id              = s_TempPolygonsIDs[p];

                var isActive        = currentHotControl == id;
                var isSelected      = shownSelection.selectedPolygons.Contains(p);
                var isFrontFacing   = IsPolygonFrontfacing(polygonCenters[p], planes[p], cameraLocalPos, cameraLocalForward, isCameraInsideOutline, isCameraOrthographic);

                var firstEdge   = polygons[p].firstEdge;
                var edgeCount   = polygons[p].edgeCount;
                var lastEdge    = firstEdge + edgeCount;

                var isHovering      = currentFocusControl == id;

                var polygonState = CalculateState(isHovering, isActive, isSelected, isFrontFacing);

                for (int e = firstEdge; e < lastEdge; e++)
                {
                    var v = halfEdges[e].vertexIndex;
                    var twin = halfEdges[e].twinIndex;
                    s_TempVerticesState[v] |= polygonState;
                    s_TempEdgesState[e]    |= polygonState;
                    s_TempEdgesState[twin] |= polygonState;
                }

                s_TempPolygonsState[p] = polygonState;
            }

            for (int s = 0; s < softEdges.Length; s++)
            {
                var id = s_TempSoftEdgesIDs[s];

                var afterPolygonIndexA  = softEdges[s].afterPolygonIndexA;
                var afterPolygonIndexB  = softEdges[s].afterPolygonIndexB;
                var beforePolygonIndexA = tempPolygon1ToPolygon2[afterPolygonIndexA];
                var beforePolygonIndexB = tempPolygon1ToPolygon2[afterPolygonIndexB];

                // We cannot use the original polygon for backfacing since it's probably pointing in another direction
                var beforePolygonAState = (beforePolygonIndexA < 0 || beforePolygonIndexA >= polygons.Length) ? ItemState.None : (s_TempPolygonsState[beforePolygonIndexA] & ~ItemState.Frontfaced);
                var beforePolygonBState = (beforePolygonIndexB < 0 || beforePolygonIndexB >= polygons.Length) ? ItemState.None : (s_TempPolygonsState[beforePolygonIndexB] & ~ItemState.Frontfaced);

                var afterPolygonAFrontFacing = IsPolygonFrontfacing(softEdges[s].afterPolygonCenterA, softEdges[s].afterLocalPlaneA, cameraLocalPos, cameraLocalForward, isCameraInsideOutline, isCameraOrthographic);
                var afterPolygonBFrontFacing = IsPolygonFrontfacing(softEdges[s].afterPolygonCenterB, softEdges[s].afterLocalPlaneB, cameraLocalPos, cameraLocalForward, isCameraInsideOutline, isCameraOrthographic);

                var isHovering = currentFocusControl == id;
                var isActive   = currentHotControl == id;
                var isSelected = false; // impossible

                var softEdgeState = CalculateState(isHovering, isActive, isSelected, afterPolygonAFrontFacing || afterPolygonBFrontFacing);

                s_TempSoftEdgesState[s] = beforePolygonAState | beforePolygonBState | softEdgeState;
            }

            UpdateStateColors(polygonColors, s_TempPolygonsState, validState, baseColors.backfacedPolygon, baseColors.frontfacedPolygon, baseColors.invalid);
            UpdateStateColors(edgeColors, s_TempEdgesState, validState, baseColors.backfacedEdge, baseColors.frontfacedEdge, baseColors.invalid);
            UpdateStateColors(vertexColors, s_TempVerticesState, validState, baseColors.backfacedEdge, baseColors.frontfacedEdge, baseColors.invalid);
            UpdateStateColors(softEdgeColors, s_TempSoftEdgesState, validState, baseColors.backfacedEdge, baseColors.frontfacedEdge, baseColors.invalid);
            return true;
        }

        // The colors as set in Chisel's preferences, those facing away fainter, before selection and hovering change them
        internal static (Color frontfacedEdge, Color backfacedEdge, Color frontfacedPolygon, Color backfacedPolygon, Color invalid) BaseColors()
        {
            var edge    = ChiselColors.BrushEditorEdges.Value;
            var polygon = ChiselColors.BrushEditorPolygons.Value;
            var faint   = SceneHandles.BackfaceAlphaMultiplier;
            return (edge,    SceneHandles.MultiplyTransparency(edge, faint),
                    polygon, SceneHandles.MultiplyTransparency(polygon, faint),
                    ChiselColors.Invalid.Value);
        }
        #endregion


        const float kAlignmentEpsilon = 0.001f;

        bool HaveFocusOnPolygon
        {
            get
            {
                var currentFocusControl = SceneHandleUtility.FocusControl;
                for (int p = 0; p < s_TempPolygonsIDCount; p++)
                {
                    if (currentFocusControl != s_TempPolygonsIDs[p])
                        continue;

                    return true;
                }
                return false;
            }
        }

        // Do not use polygons when they are camera aligned, or when their normal is zero (zero area)
        bool IsPolygonCameraAligned(int polygonIndex, Vector3 cameraDirection, bool isOutlineInsideOut)
        {
            var normal = isOutlineInsideOut ? -brushMesh.planes[polygonIndex].xyz
                                            :  brushMesh.planes[polygonIndex].xyz;
            return math.lengthsq(normal) == 0 ||
                   math.abs(math.dot(cameraDirection, normal)) > 1 - kAlignmentEpsilon;
        }

        public void RenderOutline(Vector3 cameraDirection, bool isCameraOrtho, bool isOutlineInsideOut)
        {
            var evt = Event.current;
            if (evt.type != EventType.Repaint &&
                evt.type != EventType.Layout)
                return;

            var currentFocusControl     = SceneHandleUtility.FocusControl;

            var polygons    = brushMesh.polygons;
            var planes      = brushMesh.planes;
            var vertices    = brushMesh.vertices;
            var halfEdges   = brushMesh.halfEdges;

            // Render the vertices; those not shown aren't registered either, so can't be clicked
            for (int v = 0; v < vertices.Length; v++)
            {
                var id = s_TempVerticesIDs[v];
                if (!IsVertexShown(v))
                    continue;

                var position    = vertices[v];
                var handleSize  = UnityEditor.HandleUtility.GetHandleSize(position) * HandleRendering.kPointScale;

                // registered here rather than by the cap, nearest first
                if (evt.type == EventType.Layout)
                {
                    if (!SceneHandles.InCameraOrbitMode)
                        HandleUtility.AddControl(id, PickDistance(HandleUtility.DistanceToCircle(position, handleSize), position));
                    continue;
                }

                SceneHandles.Color = vertexColors[v];
                SceneHandles.OutlinedDotHandleCap(id, position, Quaternion.identity, handleSize, evt.type);
            }

            // Render half-edges, set cursor & register control
            for (int e = 0; e < halfEdges.Length; e++)
            {
                var id = s_TempEdgesIDs[e];
                if (id == 0) continue; // only handle half of the half-edges / avoid duplicate edges
                if (!IsEdgeShown(e)) continue;

                var twin        = halfEdges[e].twinIndex;
                var vertex1     = vertices[halfEdges[e].vertexIndex];
                var vertex2     = vertices[halfEdges[twin].vertexIndex];

                SceneHandles.Color = edgeColors[e];
                if (currentFocusControl == id)
                    ChiselOutlineRenderer.DrawLine(vertex1, vertex2, thickness: kHighlightedEdgeThickness);
                else
                    ChiselOutlineRenderer.DrawLine(vertex1, vertex2, thickness: kEdgeThickness);

                // Under certain circumstances, like when an edge is camera aligned in ortho mode, 
                // we want the edge to be treated as a polygon instead. We handle that here
                if (HandleEdgeAsPolygon(id, e, cameraDirection, isCameraOrtho))
                    continue;

                SceneHandles.DrawEdgeHandle(id, vertex1, vertex2, setCursor: true, renderEdge: false, setControl: false);
                if (evt.type == EventType.Layout && !SceneHandles.InCameraOrbitMode)
                    HandleUtility.AddControl(id, PickDistance(HandleUtility.DistanceToLine(vertex1, vertex2) * 0.5f, (vertex1 + vertex2) * 0.5f));
            }

            // Render polygon handles
            for (int p = 0; p < s_TempPolygonsIDCount; p++)
            {
                if (IsPolygonCameraAligned(p, cameraDirection, isOutlineInsideOut) || !IsPolygonShown(p))
                    continue;

                var id = s_TempPolygonsIDs[p];
                var polygonCenter   = polygonCenters[p];
                var handleSize      = UnityEditor.HandleUtility.GetHandleSize(polygonCenter);
                var pointSize       = handleSize * HandleRendering.kPointScale;
                var normal          = isOutlineInsideOut ? -planes[p].xyz : planes[p].xyz;

                var rotation = Quaternion.LookRotation(normal);
                // registered here rather than by the cap, as it does (its dot and normal line), nearest first
                if (evt.type == EventType.Layout)
                {
                    if (!SceneHandles.InCameraOrbitMode)
                    {
                        var normalEnd = polygonCenter + (rotation * Vector3.forward * pointSize * 10);
                        HandleUtility.AddControl(id, PickDistance(HandleUtility.DistanceToCircle(polygonCenter, pointSize), polygonCenter));
                        HandleUtility.AddControl(id, PickDistance(HandleUtility.DistanceToLine(polygonCenter, normalEnd), polygonCenter));
                    }
                    continue;
                }

                SceneHandles.Color = polygonColors[p];
                SceneHandles.NormalHandleCap(id, polygonCenter, rotation, pointSize, evt.type);
            }
        }


        // Soft edges are edges that do not exist in our source mesh, but have been created during the optimization process (due to non planar polygons)
        // By double clicking on the soft edges we can turn them into 'real edges', we can reverse the process by clicking on a real edge
        // This method returns true when the brushMesh has changed
        public bool HandleSoftEdges(List<int> tempPolygon1ToPolygon2)
        {
            for (int e = 0; e < s_TempEdgesIDCount; e++)
            {
                switch (HandleClick(s_TempEdgesIDs[e], captureControl: false))
                {
                    case ClickState.DoubleClicked:
                    {
                        // We double clicked on an existing edge and we turn it into a soft edge by 
                        // combining the polygons on both sides of the edge
                        CombinePolygonsOnEdge(e);
                        return true;
                    }
                }
            }

            var vertices = brushMesh.vertices;
            var currentFocusControl = SceneHandleUtility.FocusControl;

            for (int s = 0; s < softEdges.Length; s++)
            {
                var id          = s_TempSoftEdgesIDs[s];
                var vertex1     = vertices[softEdges[s].vertexIndex1];
                var vertex2     = vertices[softEdges[s].vertexIndex2];
                SceneHandles.Color = softEdgeColors[s];
                if (currentFocusControl == id)
                    ChiselOutlineRenderer.DrawLine(vertex1, vertex2, thickness: kHighlightedEdgeThickness, dashSize: kSoftEdgeDashSize);
                else
                    ChiselOutlineRenderer.DrawLine(vertex1, vertex2, thickness: kEdgeThickness, dashSize: kSoftEdgeDashSize);
                switch (HandleClick(id, captureControl: true))
                {
                    case ClickState.DeselectAll:    { ClearSelection(); break; }
                    case ClickState.SelectAll:      { SelectAll(); break; }
                    case ClickState.DoubleClicked:
                    {
                        // We double clicked on a soft edge and turn it into a real edge by splitting the polygon along this edge
                        var afterPolygonIndexA = softEdges[s].afterPolygonIndexA;
                        var beforePolygonIndexA = tempPolygon1ToPolygon2[afterPolygonIndexA];
                        SplitPolygonBetweenTwoVertices(beforePolygonIndexA, softEdges[s].vertexIndex1, softEdges[s].vertexIndex2);
                        return true;
                    }
                }
                SceneHandles.DrawEdgeHandle(id, vertex1, vertex2, setCursor: true, renderEdge: false, cursor: MouseCursor.ArrowPlus);
            }
            return false;
        }

        // This method returns true when the brushMesh has changed
        // Delete removes the selected vertices, whichever handle takes the command
        public bool HandleDeletion()
        {
            for (int v = 0; v < s_TempVerticesIDCount; v++)
            {
                switch (HandleClick(s_TempVerticesIDs[v], captureControl: false))
                {
                    case ClickState.Delete:
                    {
                        DeleteSelectedVertices();
                        return true;
                    }
                }
            }

            for (int e = 0; e < s_TempEdgesIDCount; e++)
            {
                switch (HandleClick(s_TempEdgesIDs[e], captureControl: false))
                {
                    case ClickState.Delete:
                    {
                        DeleteSelectedVertices();
                        return true;
                    }
                }
            }

            for (int p = 0; p < s_TempPolygonsIDCount; p++)
            {
                switch (HandleClick(s_TempPolygonsIDs[p], captureControl: false))
                {
                    case ClickState.Delete:
                    {
                        DeleteSelectedVertices();
                        return true;
                    }
                }
            }
            return false;
        }

        void SelectVertexViewAligned(int v, Vector3 cameraDirection, SelectionType currentSelectionType)
        {
            SelectVertex(v, currentSelectionType);
            if (currentSelectionType == SelectionType.Replace)
                currentSelectionType = SelectionType.Additive;
            var halfEdges               = brushMesh.halfEdges;
            var planes                  = brushMesh.planes;
            var halfEdgePolygonIndices  = brushMesh.halfEdgePolygonIndices;
            for (int edgeIndex = 0; edgeIndex < halfEdges.Length; edgeIndex++)
            {
                var vertexIndex = halfEdges[edgeIndex].vertexIndex;
                if (vertexIndex != v)
                    continue;

                var twinIndex = halfEdges[edgeIndex].twinIndex;

                var edgePolygonIndex = halfEdgePolygonIndices[edgeIndex];
                var edgeNormal = planes[edgePolygonIndex].xyz;
                var twinPolygonIndex = halfEdgePolygonIndices[twinIndex];
                var twinNormal = planes[twinPolygonIndex].xyz;

                // Check if both polygons on either side of edge are aligned with the view direction 
                // (which means the edge is as well)
                if (Mathf.Abs(Vector3.Dot(cameraDirection, edgeNormal)) > kAlignmentEpsilon ||
                    Mathf.Abs(Vector3.Dot(cameraDirection, twinNormal)) > kAlignmentEpsilon)
                    continue;

                // Select the other vertex on the edge as well since it's right behind our vertex
                SelectVertex(halfEdges[twinIndex].vertexIndex, currentSelectionType);
            }
        }

        public void HandleSelection(bool in2DMode, Vector3 cameraDirection)
        {
            var evt       = Event.current;
            var actionKey = ChiselRectSelectionManager.IsActionKeyHeld(evt);
            for (int v = 0; v < s_TempVerticesIDCount; v++)
            {
                switch (HandleClick(s_TempVerticesIDs[v], captureControl: false))
                {
                    case ClickState.DeselectAll:    { ClearSelection(); break; }
                    case ClickState.SelectAll:      { SelectAll(); break; }
                    case ClickState.Dragged:
                    {
                        // Select the vertex if we start dragging on an unselected vertex
                        if (!selection.selectedVertices.Contains(v))
                        {
                            if (in2DMode)
                            {
                                SelectVertexViewAligned(v, cameraDirection, SelectionType.Replace);
                            } else
                                SelectVertex(v, SelectionType.Replace);
                            selection.SetActive(TopologyItemKind.Vertex, v);
                        }
                        break;
                    }
                    case ClickState.Clicked:
                    {
                        ClickVertex(v, evt.shift, actionKey, in2DMode, cameraDirection);
                        break;
                    }
                }
            }

            for (int e = 0; e < s_TempEdgesIDCount; e++)
            {
                switch (HandleClick(s_TempEdgesIDs[e], captureControl: false))
                {
                    case ClickState.DeselectAll:    { ClearSelection(); break; }
                    case ClickState.SelectAll:      { SelectAll(); break; }
                    case ClickState.Dragged:
                    {
                        // Select the edge if we start dragging on an unselected edge
                        if (!selection.selectedEdges.Contains(e))
                        {
                            SelectEdge(e, SelectionType.Replace);
                            selection.SetActive(TopologyItemKind.Edge, e);
                        }
                        break;
                    }
                    case ClickState.Clicked:
                    {
                        ClickEdge(e, evt.shift, actionKey);
                        break;
                    }
                }
            }

            for (int p = 0; p < s_TempPolygonsIDCount; p++)
            {
                switch (HandleClick(s_TempPolygonsIDs[p], captureControl: false))
                {
                    case ClickState.DeselectAll:    { ClearSelection(); break; }
                    case ClickState.SelectAll:      { SelectAll(); break; }
                    case ClickState.Dragged:
                    {
                        // Select the polygon if we start dragging on an unselected polygon
                        if (!selection.selectedPolygons.Contains(p))
                        {
                            SelectPolygon(p, SelectionType.Replace);
                            selection.SetActive(TopologyItemKind.Polygon, p);
                        }
                        break;
                    }
                    case ClickState.Clicked:
                    {
                        ClickPolygon(p, evt.shift, actionKey);
                        break;
                    }
                }
            }
        }

        // When a polygon is aligned with the camera, and we're in ortho mode, pretend that the edges of that polygon are the entire polygon, 
        // so that when you move the edge, you move the entire polygon instead.
        // This method returns true when the brushMesh has changed
        public bool HandleEdgeAsPolygon(int id, int edgeIndex, Vector3 cameraDirection, bool isCameraOrtho)
        {
            if (!isCameraOrtho)
                return false;

            var evt = Event.current;

            var planes      = brushMesh.planes;
            var vertices    = brushMesh.vertices;
            var halfEdges   = brushMesh.halfEdges;
            var halfEdgePolygonIndices = brushMesh.halfEdgePolygonIndices;


            var twinIndex = halfEdges[edgeIndex].twinIndex;

            var edgePolygonIndex    = halfEdgePolygonIndices[edgeIndex];
            var edgeNormal          = planes[edgePolygonIndex].xyz;
            var twinPolygonIndex    = halfEdgePolygonIndices[twinIndex];
            var twinNormal          = planes[twinPolygonIndex].xyz;
            var alignedIndex = -1;

            // Check if the polygon is aligned with the view direction
            if      (Mathf.Abs(Vector3.Dot(cameraDirection, edgeNormal)) < kAlignmentEpsilon) alignedIndex = edgePolygonIndex;
            // Otherwise try the twin side of the edge instead
            else if (Mathf.Abs(Vector3.Dot(cameraDirection, twinNormal)) < kAlignmentEpsilon) alignedIndex = twinPolygonIndex;
            else return false;

            var vertex1 = vertices[halfEdges[edgeIndex].vertexIndex];
            var vertex2 = vertices[halfEdges[twinIndex].vertexIndex];

            // Handle edge cursor before we change the id
            if (evt.type == EventType.Repaint)
                SceneHandles.SetCursor(id, vertex1, vertex2);

            // Register this edge with the id for the polygon, so that when we interact with it, it'll use the handle code for the polygon instead.
            if (evt.type == EventType.Layout)
                UnityEditor.HandleUtility.AddControl(s_TempPolygonsIDs[alignedIndex], PickDistance(UnityEditor.HandleUtility.DistanceToLine(vertex1, vertex2) * 0.5f, (vertex1 + vertex2) * 0.5f));
            return true;
        }

        // The plane vertices, edges and faces are dragged on, picked by clicking a square of the move handle (XZ, XY or YZ,
        // in its axes, the grid's); None: each drags as it would without one. Cleared when the selection changes.
        internal static Axes WorkingPlane = Axes.None;

        internal static void OnSelectionChanged() => WorkingPlane = Axes.None;

        internal static bool TryGetWorkingPlane(Grid grid, out Vector3 normal, out Vector3 slideDir1, out Vector3 slideDir2)
        {
            var normalAxis = WorkingPlane switch
            {
                Axes.XZ => Axis.Y,
                Axes.XY => Axis.Z,
                Axes.YZ => Axis.X,
                _       => Axis.None
            };
            if (normalAxis == Axis.None)
            {
                normal = slideDir1 = slideDir2 = default;
                return false;
            }
            grid.GetTangentAxesForAxis(normalAxis, out slideDir1, out slideDir2);
            normal = normalAxis == Axis.X ? grid.Right : normalAxis == Axis.Y ? grid.Up : grid.Forward;
            return true;
        }

        // Dragged on the working plane; the controls are registered by RenderOutline
        static Vector3 WorkingPlaneOffset(int id, Vector3 position, Vector3 normal, Vector3 slideDir1, Vector3 slideDir2)
        {
            return SceneHandles.Slider2DHandle(id, position, Vector3.zero, normal, slideDir1, slideDir2, handleSize: -1, capFunction: null, axes: WorkingPlane) - position;
        }

        // Handle moving an edge along the grid aligned plane which the edge intersects with
        // This method returns true when the brushMesh has changed
        public bool HandleVertexMovementCameraAligned(Vector3 cameraDirection)
        {
            var vertices = brushMesh.vertices;
            for (int v = 0; v < vertices.Length; v++)
            {
                if (!IsVertexShown(v))
                    continue;
                var id          = s_TempVerticesIDs[v];
                var position    = vertices[v];
                var handleSize  = UnityEditor.HandleUtility.GetHandleSize(position) * HandleRendering.kPointScale;

                var offset = Vector3.zero;
                EditorGUI.BeginChangeCheck();   { offset = SceneHandles.Slider2DHandleOffset(id, position, cameraDirection, capFunction: null); }
                if (EditorGUI.EndChangeCheck()) { MoveSelectedVertices(offset); return true; }
            }
            return false;
        }


        // Handle moving a vertex in 3D: on the working plane; without one a vertex can't be dragged, as a point gives no
        // plane to map the mouse onto, so the click is only captured
        // This method returns true when the brushMesh has changed
        public bool HandleVertexMovement3D()
        {
            if (TryGetWorkingPlane(Grid.ActiveGrid, out var normal, out var slideDir1, out var slideDir2))
            {
                var vertices = brushMesh.vertices;
                for (int v = 0; v < vertices.Length; v++)
                {
                    if (!IsVertexShown(v))
                        continue;
                    var offset = Vector3.zero;
                    EditorGUI.BeginChangeCheck();   { offset = WorkingPlaneOffset(s_TempVerticesIDs[v], vertices[v], normal, slideDir1, slideDir2); }
                    if (EditorGUI.EndChangeCheck()) { MoveSelectedVertices(offset); return true; }
                }
                return false;
            }

            // Capture the click so we don't actually pass through to another control
            for (int v = 0; v < s_TempVerticesIDCount; v++)
                HandleClick(s_TempVerticesIDs[v], captureControl: true);
            return false;
        }

        // Handle moving the polygon along the grid plane that's closest to the polygon plane
        // This method returns true when the brushMesh has changed
        public bool HandlePolygonMovementCameraAligned(Vector3 cameraDirection, bool isOutlineInsideOut)
        {
            var planes = brushMesh.planes;

            for (int p = 0; p < s_TempPolygonsIDCount; p++)
            {
                if (IsPolygonCameraAligned(p, cameraDirection, isOutlineInsideOut) || !IsPolygonShown(p))
                    continue;

                var id      = s_TempPolygonsIDs[p];
                var normal  = isOutlineInsideOut ? -planes[p].xyz : planes[p].xyz;
                var offset  = Vector3.zero;
                EditorGUI.BeginChangeCheck();   { offset = SceneHandles.Slider2DHandleOffset(id, polygonCenters[p], normal, handleSize: -1); }
                if (EditorGUI.EndChangeCheck()) { MoveSelectedVertices(offset); return true; }
            }
            return false;
        }

        // Handle moving the polygon center in the direction of the normal, or on the working plane
        // This method returns true when the brushMesh has changed
        public bool HandlePolygonMovement3D(Vector3 cameraDirection, bool isOutlineInsideOut)
        {

            var planes = brushMesh.planes;
            var onWorkingPlane = TryGetWorkingPlane(Grid.ActiveGrid, out var planeNormal, out var slideDir1, out var slideDir2);

            for (int p = 0; p < s_TempPolygonsIDCount; p++)
            {
                if (IsPolygonCameraAligned(p, cameraDirection, isOutlineInsideOut) || !IsPolygonShown(p))
                    continue;

                var id      = s_TempPolygonsIDs[p];
                var normal  = isOutlineInsideOut ? -planes[p].xyz : planes[p].xyz;

                var offset = Vector3.zero;
                EditorGUI.BeginChangeCheck();
                {
                    offset = onWorkingPlane ? WorkingPlaneOffset(id, polygonCenters[p], planeNormal, slideDir1, slideDir2)
                                            : SceneHandles.Slider1DHandleOffset(id, polygonCenters[p], normal, handleSize: -1);
                }
                if (EditorGUI.EndChangeCheck()) { MoveSelectedVertices(offset); return true; }
            }
            return false;
        }

        // Handle moving an edge along the direction of itself
        // This method returns true when the brushMesh has changed
        public bool HandleEdgeMovementEdgeAligned(Vector3 cameraDirection, bool isCameraOrtho)
        {
            var vertices    = brushMesh.vertices;
            var halfEdges   = brushMesh.halfEdges;

            for (int e = 0; e < halfEdges.Length; e++)
            {
                var id = s_TempEdgesIDs[e];
                if (id == 0) continue; // only handle half of the half-edges / avoid duplicate edges
                if (!IsEdgeShown(e)) continue;

                var twin    = halfEdges[e].twinIndex;
                var vertex1 = vertices[halfEdges[e].vertexIndex];
                var vertex2 = vertices[halfEdges[twin].vertexIndex];

                // Render a dotted line along the current edge (but do not overlap it)
                if ((s_TempEdgesState[e] & (ItemState.Hovering | ItemState.Active)) != ItemState.None)
                {
                    var direction = math.length(vertex1 - vertex2) * 1000.0f;
                    SceneHandles.DrawDottedLine(vertex1 + direction, vertex1, 2.0f);
                    SceneHandles.DrawDottedLine(vertex2, vertex2 - direction, 2.0f);
                }

                var offset = Vector3.zero;
                EditorGUI.BeginChangeCheck();   { offset = SceneHandles.Slider1DHandleAlignedOffset(id, vertex1, vertex2); }
                if (EditorGUI.EndChangeCheck()) { MoveSelectedVertices(offset); return true; }
            }
            return false;
        }

        // Handle moving an edge along a grid aligned 2D plane that the edge intersects with, or the working plane
        // This method returns true when the brushMesh has changed
        public bool HandleEdgeMovement3D()
        {
            var vertices    = brushMesh.vertices;
            var halfEdges   = brushMesh.halfEdges;
            var onWorkingPlane = TryGetWorkingPlane(Grid.ActiveGrid, out _, out var slideDir1, out var slideDir2);

            for (int e = 0; e < halfEdges.Length; e++)
            {
                var id = s_TempEdgesIDs[e];
                if (id == 0) continue; // only handle half of the half-edges / avoid duplicate edges
                if (!IsEdgeShown(e)) continue;

                var twin    = halfEdges[e].twinIndex;
                var vertex1 = vertices[halfEdges[e].vertexIndex];
                var vertex2 = vertices[halfEdges[twin].vertexIndex];

                var offset = Vector3.zero;
                EditorGUI.BeginChangeCheck();
                {
                    offset = onWorkingPlane ? SceneHandles.Slider2DEdgeHandleOffset(id, vertex1, vertex2, slideDir1, slideDir2, WorkingPlane)
                                            : SceneHandles.Slider2DHandleTangentOffset(id, vertex1, vertex2);
                }
                if (EditorGUI.EndChangeCheck()) { MoveSelectedVertices(offset); return true; }
            }
            return false;
        }

        // This method returns true when the brushMesh has changed
        public bool HandleVertexMovementWithPositionHandle()
        {
            // If we're focusing on a position handle we don't want the position Handle to be enabled, so that when they overlap, 
            // you're not accidentally grabbing the wrong thing
            bool enablePositionHandle = !HaveFocusOnPolygon;

            using (new EditorGUI.DisabledScope(!enablePositionHandle))
            {
                // a click on a square picks the working plane, rather than locking axes for all handles
                s_TempPositionHandleIDs.SetActivePlane(WorkingPlane);
                var offset = Vector3.zero;
                EditorGUI.BeginChangeCheck();   { offset = SceneHandles.PositionHandleOffset(ref s_TempPositionHandleIDs, vertexSelectionCenter, lockAxesOnClick: false); }
                var changed = EditorGUI.EndChangeCheck();
                if (s_TempPositionHandleIDs.clickedPlane != Axes.None)
                {
                    WorkingPlane = s_TempPositionHandleIDs.clickedPlane;
                    SceneView.RepaintAll();
                }
                if (changed) { MoveSelectedVertices(offset); return true; }
            }
            return false;
        }

        Quaternion vertexHandleRotation = Quaternion.identity;
        Vector3    vertexHandleScale    = Vector3.one;

        // Before a drag the handle sits on the selection as it is now; once taken hold of it keeps what it started from
        Quaternion PrepareVertexTransformHandle()
        {
            var axes = VertexHandleAxes(Tools.pivotRotation, Handles.matrix);
            if (GUIUtility.hotControl == 0)
            {
                if (transformVertices == null || Event.current.type == EventType.MouseDown)
                    BeginVertexTransform();
                transformPivot       = vertexSelectionCenter;
                vertexHandleRotation = axes;
                vertexHandleScale    = Vector3.one;
            }
            return axes;
        }

        // This method returns true when the brushMesh has changed
        public bool HandleVertexRotation()
        {
            using (new EditorGUI.DisabledScope(HaveFocusOnPolygon))
            {
                var axes = PrepareVertexTransformHandle();
                EditorGUI.BeginChangeCheck();
                var rotation = Handles.RotationHandle(vertexHandleRotation, transformPivot);
                if (!EditorGUI.EndChangeCheck())
                    return false;
                vertexHandleRotation = rotation;
                RotateSelectedVertices(rotation * Quaternion.Inverse(axes));
                return true;
            }
        }

        // This method returns true when the brushMesh has changed
        public bool HandleVertexScale()
        {
            using (new EditorGUI.DisabledScope(HaveFocusOnPolygon))
            {
                var axes = PrepareVertexTransformHandle();
                EditorGUI.BeginChangeCheck();
                var scale = Handles.ScaleHandle(vertexHandleScale, transformPivot, axes, UnityEditor.HandleUtility.GetHandleSize(transformPivot));
                if (!EditorGUI.EndChangeCheck())
                    return false;
                vertexHandleScale = scale;
                ScaleSelectedVertices(scale, axes);
                return true;
            }
        }

        // This method returns true when the brushMesh has changed
        public bool DoHandles(IChiselHandles handles, bool isOutlineInsideOut)
        {
            var camera = Camera.current;
            if (!UpdateColors(camera))
                return false;

            var isCameraOrtho       = camera.orthographic;
            var cameraDirection     = camera.transform.forward;
            var in2DMode            = handles.IsIn2DMode;

            // Ortho mode

            if (HandleDeletion())
                return true;

            if (ChiselBrushMarquee.TryGetReleased(out var marqueeRect, out var marqueeType))
                SelectVerticesInRect(marqueeRect, marqueeType, position => HandleUtility.WorldToGUIPoint(position));

            HandleSelection(in2DMode, cameraDirection);

            if (HandleSoftEdges(tempPolygon1ToPolygon2))
                return true;

            if (HandleEdgeMovement3D())
                return true;

            if (HandlePolygonMovement3D(cameraDirection, isOutlineInsideOut))
                return true;

            if (in2DMode)
            {
                if (HandleVertexMovementCameraAligned(cameraDirection))
                    return true;
            } else
            {
                if (HandleVertexMovement3D())
                    return true;
            }

            if (!in2DMode &&
                selection.selectedVertices.Count > 0)
            {
                switch (VertexHandle)
                {
                    case Tool.Rotate: if (HandleVertexRotation()) return true; break;
                    case Tool.Scale:  if (HandleVertexScale())    return true; break;
                    default:          if (HandleVertexMovementWithPositionHandle()) return true; break;
                }
            }

            RenderOutline(cameraDirection, isCameraOrtho, isOutlineInsideOut);
            return false;
        }
    }
}