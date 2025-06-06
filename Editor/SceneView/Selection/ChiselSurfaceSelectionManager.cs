﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Chisel.Core;
using Chisel.Components;
using UnityEngine;
using UnityEditor;
using UnityEngine.Pool;

namespace Chisel.Editors
{
    [Serializable]
    public class ChiselSurfaceSelection : ISingletonData
    {
        public HashSet<SurfaceReference> selectedSurfaces = new();
        public SurfaceReference[] selectedSurfacesArray;
        
        [NonSerialized]
        public HashSet<SurfaceReference> hoverSurfaces = new();
        
        public void OnAfterDeserialize()
        {
            selectedSurfaces.Clear();
            if (selectedSurfacesArray != null)
                selectedSurfaces.AddRange(selectedSurfacesArray);
        }

        public void OnBeforeSerialize()
        {
            selectedSurfacesArray = selectedSurfaces.ToArray();
        }

        internal void Clean()
        {
            var destroyedSurfaces = ListPool<SurfaceReference>.Get();
            destroyedSurfaces.Clear();
            foreach (var surface in selectedSurfaces)
                if (surface.node == null)
                    destroyedSurfaces.Add(surface);
            foreach (var surface in destroyedSurfaces)
                selectedSurfaces.Remove(surface);

            destroyedSurfaces.Clear();
            foreach (var surface in hoverSurfaces)
                if (surface.node == null)
                    destroyedSurfaces.Add(surface);
            foreach (var surface in destroyedSurfaces)
                hoverSurfaces.Remove(surface);

            destroyedSurfaces.Clear();
            if (selectedSurfacesArray != null)
            {
                foreach (var surface in selectedSurfacesArray)
                    if (surface.node == null)
                        destroyedSurfaces.Add(surface);
            }
            if (destroyedSurfaces.Count > 0)
            {
                var items = selectedSurfacesArray.ToList();
                foreach (var surface in destroyedSurfaces)
                    items.Remove(surface);
                selectedSurfacesArray = items.ToArray();
            }
            ListPool<SurfaceReference>.Release(destroyedSurfaces);
        }
    }
    
    public class ChiselSurfaceSelectionManager : SingletonManager<ChiselSurfaceSelection, ChiselSurfaceSelectionManager>
    {
        public static Action SelectionChanged;
        public static Action HoverChanged;

        protected override void Initialize() 
        {
            UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
            UnityEditor.Selection.selectionChanged += OnSelectionChanged;
        }
        
        protected override void Shutdown()
        {
            UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
        }

        public static bool HaveSelection
        {
            get { return Data.selectedSurfaces.Count > 0; }
        }

        public static HashSet<SurfaceReference> Selection 
        {
            get { return Data.selectedSurfaces; }
        }
        
        public static HashSet<SurfaceReference> Hovered
        {
            get { return Data.hoverSurfaces; }
        }

        public static HashSet<ChiselNodeComponent> SelectedNodes 
        {
            get
            {
                var selectedSurfaces    = Data.selectedSurfaces;
                var uniqueNodes			= new HashSet<ChiselNodeComponent>();

                foreach (var selectedSurface in selectedSurfaces)
                {
                    if (selectedSurface.node == null)
                        continue;
                    uniqueNodes.Add(selectedSurface.node);
                }
                return uniqueNodes;
            }
        }
        
        public static HashSet<GameObject> SelectedGameObjects
        {
            get
            {
                var selectedSurfaces	= Data.selectedSurfaces;
                var uniqueNodes			= new HashSet<GameObject>();

                foreach (var selectedSurface in selectedSurfaces)
                {
                    if (selectedSurface.node == null)
                        continue;
                    uniqueNodes.Add(selectedSurface.node.gameObject);
                }
                return uniqueNodes;
            }
        }

        public static bool IsSelected(SurfaceReference surface)
        {
            return Data.selectedSurfaces.Contains(surface);
        }

        public static bool IsAnySelected(IEnumerable<SurfaceReference> selection)
        {
            foreach(var surface in selection)
            {
                if (Data.selectedSurfaces.Contains(surface))
                    return true;
            }
            return false;
        }


        public static bool Hover(HashSet<SurfaceReference> surfaces)
        {
            if (!HoverInternal(surfaces))
                return false;
            if (HoverChanged != null)
                HoverChanged.Invoke();
            return true;
        }

        public static bool Unhover(HashSet<SurfaceReference> surfaces)
        {
            if (!UnhoverInternal(surfaces))
                return false;
            if (HoverChanged != null)
                HoverChanged.Invoke();
            return true;
        }

        public static bool UnhoverAll()
        {
            if (!UnhoverAllInternal())
                return false;
            if (HoverChanged != null)
                HoverChanged.Invoke();
            return true;
        }




        static bool HoverInternal(HashSet<SurfaceReference> surfaces)
        {
            if (surfaces.Count == 0)
                return false;
            bool modified = Data.hoverSurfaces.AddRange(surfaces);
            return modified;
        }
        
        static bool UnhoverInternal(HashSet<SurfaceReference> surfaces)
        {
            if (surfaces.Count == 0)
                return false;
            bool modified = Data.hoverSurfaces.RemoveRange(surfaces);
            return modified;
        }

        static bool UnhoverAllInternal()
        {
            if (Data.hoverSurfaces.Count == 0)
                return false;
            
            Data.hoverSurfaces.Clear();
            return true;
        }



        public static bool Select(HashSet<SurfaceReference> surfaces)
        {
            if (!SelectInternal(surfaces))
                return false;
			SelectionChanged?.Invoke();
			return true;
        }

        public static bool Deselect(HashSet<SurfaceReference> surfaces)
        {
            if (!DeselectInternal(surfaces))
                return false;
			SelectionChanged?.Invoke();
			return true;
        }

        public static bool DeselectAll()
        {
            if (!DeselectAllInternal())
                return false;
			SelectionChanged?.Invoke();
			return true;
        }



        static bool SelectInternal(HashSet<SurfaceReference> surfaces)
        {
            if (surfaces.Count == 0)
                return false;
            RecordUndo("Modified Surface Selection");
            bool modified = Data.selectedSurfaces.AddRange(surfaces);
            return modified;
        }
        
        static bool DeselectInternal(HashSet<SurfaceReference> surfaces)
        {
            if (surfaces.Count == 0)
                return false;
            RecordUndo("Modified Surface Selection");
            bool modified = Data.selectedSurfaces.RemoveRange(surfaces);
            return modified;
        }

        static bool DeselectAllInternal()
        {
            if (Data.selectedSurfaces.Count == 0)
                return false;
            
            RecordUndo("Deselected All Surfaces");
            Data.selectedSurfaces.Clear();
            return true;
        }
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool GetAllSurfaces(ChiselNodeComponent chiselNode, CSGTreeNode node, CSGTreeBrush? findBrush, List<SurfaceReference> surfaces)
        {
            switch (node.Type)
            {
                case CSGNodeType.Brush:  return GetAllSurfaces(chiselNode, (CSGTreeBrush)node,  findBrush, surfaces);
                case CSGNodeType.Branch: return GetAllSurfaces(chiselNode, (CSGTreeBranch)node, findBrush, surfaces);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool GetAllSurfaces(ChiselNodeComponent chiselNode, CSGTreeBranch branch, CSGTreeBrush? findBrush, List<SurfaceReference> surfaces)
        {
            if (!branch.Valid)
                return false;

            for (int i = 0; i < branch.Count; i++)
            {
                var child = branch[i];
                if (GetAllSurfaces(chiselNode, child, findBrush, surfaces)) 
                    return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool GetAllSurfaces(ChiselNodeComponent chiselNode, CSGTreeBrush brush, CSGTreeBrush? findBrush, List<SurfaceReference> surfaces)
        {
            if (!brush.Valid)
                return false;

            if (findBrush.HasValue && findBrush.Value != brush)
                return true;

            var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
            if (!brushMeshBlob.IsCreated)
                return true;

            ref var brushMesh = ref brushMeshBlob.Value;
            for (int i = 0; i < brushMesh.polygons.Length; i++)
            {
                var surfaceIndex = i;
                var descriptionIndex = brushMesh.polygons[i].descriptionIndex;
                //surfaces.Add(new SurfaceReference(this, brushContainerAsset, 0, 0, i, surfaceID));
                surfaces.Add(new SurfaceReference(chiselNode, descriptionIndex, brush, surfaceIndex));
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetAllSurfaceReferences(ChiselNodeComponent chiselNode, CSGTreeBrush brush, List<SurfaceReference> surfaces)
        {
            if (!chiselNode || !chiselNode.TopTreeNode.Valid)
                return false;

            if (!GetAllSurfaces(chiselNode, chiselNode.TopTreeNode, brush, surfaces))
                return false;
            return surfaces.Count > 0;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetAllSurfaceReferences(ChiselNodeComponent chiselNode, List<SurfaceReference> surfaces)
        {
            if (!chiselNode || !chiselNode.TopTreeNode.Valid)
                return false;

            if (!GetAllSurfaces(chiselNode, chiselNode.TopTreeNode, null, surfaces))
                return false;

            return surfaces.Count > 0;
        }

        readonly static List<SurfaceReference> s_TempSurfaces = new List<SurfaceReference>();

        static void OnSelectionChanged()
        {
            var gameObjects			= new HashSet<GameObject>(UnityEditor.Selection.gameObjects);
            var usedGameObjects		= new HashSet<GameObject>();
            var newSelectedSurfaces = new HashSet<SurfaceReference>();
            foreach(var selection in Data.selectedSurfaces)
            {
                if (!selection.node)
                    continue;
                var gameObject = selection.node.gameObject;
                if (!gameObject ||
                    !gameObjects.Contains(gameObject))
                    continue;
                newSelectedSurfaces.Add(selection);
                usedGameObjects.Add(gameObject);
            }

            foreach(var gameObject in gameObjects)
            {
                if (usedGameObjects.Contains(gameObject))
                    continue;

                var chiselNode = gameObject.GetComponent<ChiselNodeComponent>();
                if (chiselNode == null || !(chiselNode is ChiselGeneratorComponent))
                    continue;

                s_TempSurfaces.Clear();
                if (!GetAllSurfaceReferences(chiselNode, s_TempSurfaces))
                    continue;

                // It's possible that the node has not (yet) been set up correctly ...
                if (s_TempSurfaces.Count == 0)
                    continue;

                newSelectedSurfaces.AddRange(s_TempSurfaces);
            }
            s_TempSurfaces.Clear();
            UpdateSelection(SelectionType.Replace, newSelectedSurfaces);
        }

        public static bool UpdateSelection(SelectionType selectionType, HashSet<SurfaceReference> surfaces)
        {
            bool modified = false;
            if (selectionType == SelectionType.Replace)
            {
                modified = DeselectAllInternal();
            }
            
            // TODO: handle replace in a single step (using 'Set') to detect modifications

            if (surfaces != null)
            { 
                if (selectionType == SelectionType.Subtractive)
                    modified = DeselectInternal(surfaces) || modified;
                else
                    modified = SelectInternal(surfaces) || modified;
            }

            UnityEditor.Selection.selectionChanged -= OnSelectionChanged;

            if (SelectedGameObjects.Count == 0)
            {
                // To prevent the EditorTool from exiting the moment we deselect all surfaces, we leave one object 'selected'
                var selected = UnityEditor.Selection.GetFiltered<ChiselNodeComponent>(SelectionMode.Deep | SelectionMode.Editable);
                if (selected.Length > 0)
                {
                    UnityEditor.Selection.activeObject = selected[0];
                } 
            } else
                UnityEditor.Selection.objects = SelectedGameObjects.ToArray();

            UnityEditor.Selection.selectionChanged += OnSelectionChanged;
            if (modified && SelectionChanged != null)
                SelectionChanged.Invoke();
            return modified;
        }

        // Hack to ensure we don't have objects that have been removed
        internal static void Clean()
        {
            Data.Clean();
        }

        public static bool SetHovering(SelectionType selectionType, HashSet<SurfaceReference> surfaces)
        {
            bool modified;
            if (surfaces != null)
            {
                switch (selectionType)
                {
                    default:
                    {
                        modified = Data.hoverSurfaces.Set(surfaces);
                        break;
                    }
                    case SelectionType.Subtractive:
                    {
                        modified = Data.hoverSurfaces.SetCommon(surfaces, Data.selectedSurfaces);
                        break;
                    }
                }
            } else
                modified = UnhoverAllInternal();

            if (modified && HoverChanged != null)
                HoverChanged.Invoke();
            return modified;
        }
    }

}
