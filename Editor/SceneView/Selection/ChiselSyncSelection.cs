﻿using System.Collections.Generic;
using System.Linq;
using Chisel.Core;
using Chisel.Components;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public class ChiselSyncSelection : ScriptableObject, ISerializationCallbackReceiver
    {
        static ChiselSyncSelection s_Instance;
        public static ChiselSyncSelection Instance
        {
            get
            {
                if (s_Instance)
                    return s_Instance;

				var foundInstances = UnityEngine.Object.FindObjectsByType<ChiselSyncSelection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                if (foundInstances == null ||
                    foundInstances.Length == 0)
                {
                    s_Instance = ScriptableObject.CreateInstance<ChiselSyncSelection>();					
                    s_Instance.hideFlags = HideFlags.HideAndDontSave;
                    return s_Instance;
                }
                               
                s_Instance = foundInstances[0];
                return s_Instance;
            }
        }

        [SerializeField] CSGTreeBrush[] selectedBrushesArray;
        readonly HashSet<CSGTreeBrush> selectedBrushesLookup = new HashSet<CSGTreeBrush>();


        // TODO: can we make this work across domain reloads?
        public void OnBeforeSerialize()
        {
            selectedBrushesArray = selectedBrushesLookup.ToArray();
        }

        public void OnAfterDeserialize()
        {
            selectedBrushesLookup.Clear();
            if (selectedBrushesArray != null)
            {
                foreach (var brush in selectedBrushesArray)
                    selectedBrushesLookup.Add(brush);
            }
        } 


        
        public static void ClearBrushVariants(CSGTreeBrush brush)
        {
            Undo.RecordObject(ChiselSyncSelection.Instance, "ClearBrushVariants variant");
            var node = Resources.InstanceIDToObject(brush.InstanceID) as ChiselNodeComponent;
            if (node) node.hierarchyItem.SetBoundsDirty();
            var modified = false;
            if (modified)
                ChiselOutlineRenderer.Instance.OnSelectionChanged();
        }

        public static void DeselectBrushVariant(CSGTreeBrush brush)
        {
            Undo.RecordObject(ChiselSyncSelection.Instance, "Deselected brush variant");
            var node = Resources.InstanceIDToObject(brush.InstanceID) as ChiselNodeComponent;
            if (node) node.hierarchyItem.SetBoundsDirty();
            var selectedBrushesLookup = Instance.selectedBrushesLookup;
            var modified = selectedBrushesLookup.Remove(brush);
            if (modified)
                ChiselOutlineRenderer.Instance.OnSelectionChanged();
        }

        public static void SelectBrushVariant(CSGTreeBrush brush, bool uniqueSelection = false)
        {
            Undo.RecordObject(ChiselSyncSelection.Instance, "Selected brush variant");
            var node = Resources.InstanceIDToObject(brush.InstanceID) as ChiselNodeComponent;
            if (node) node.hierarchyItem.SetBoundsDirty();
            var selectedBrushesLookup = Instance.selectedBrushesLookup;
            var modified = false;/*
            if (uniqueSelection)
            {
                foreach (var variant in brush.AllSynchronizedVariants)
                {
                    if (variant != brush)
                        modified = selectedBrushesLookup.Remove(variant) || modified;
                }
            }*/
            modified = selectedBrushesLookup.Add(brush);
            if (modified)
                ChiselOutlineRenderer.Instance.OnSelectionChanged();
        }

        public static bool IsBrushVariantSelected(CSGTreeBrush brush)
        {
            if (Instance.selectedBrushesLookup.Contains(brush))
                return true;

            return false;
        }
        
        public static bool GetSelectedVariantsOfBrush(CSGTreeBrush brush, List<CSGTreeBrush> selectedVariants)
        {
            selectedVariants.Clear();
            var selectedBrushesLookup = Instance.selectedBrushesLookup;
            selectedVariants.Add(brush);
            return selectedVariants.Count > 0;
        }


        public static void GetSelectedVariantsOfBrushOrSelf(CSGTreeBrush brush, List<CSGTreeBrush> selectedVariants)
        {
            selectedVariants.Clear();
            var selectedBrushesLookup = Instance.selectedBrushesLookup;
            selectedVariants.Add(brush);
            if (selectedVariants.Count > 0)
                return;
            selectedVariants.Add(brush);
        }

        public static IEnumerable<CSGTreeBrush> GetSelectedVariantsOfBrushOrSelf(CSGTreeBrush brush)
        {
            yield return brush;
        }

        public static bool IsAnyBrushVariantSelected(CSGTreeBrush brush)
        {
            return Instance.selectedBrushesLookup.Contains(brush);
        }

        public static bool IsAnyBrushVariantSelected(IEnumerable<CSGTreeBrush> brushes)
        {
            foreach (var variant in brushes)
            {
                if (Instance.selectedBrushesLookup.Contains(variant))
                    return true;
            }
            return false;
        }
    }
}
