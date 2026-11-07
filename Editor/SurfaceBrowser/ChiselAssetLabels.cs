using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // An asset's Unity labels, changed so the change can be undone: Unity's undo doesn't cover labels, so the asset and
    // its labels are kept on an object Undo records, and an undo or redo sets the asset's labels back from it
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    [InitializeOnLoad]
    public static class ChiselAssetLabels
    {
        sealed class State : ScriptableObject
        {
            public string   guid;
            public string[] labels = Array.Empty<string>();
        }

        static State s_State;

        static State GetState()
        {
            if (s_State == null)
            {
                s_State = ScriptableObject.CreateInstance<State>();
                s_State.hideFlags = HideFlags.HideAndDontSave;
            }
            return s_State;
        }

        static ChiselAssetLabels()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        static void OnUndoRedo()
        {
            if (s_State == null || string.IsNullOrEmpty(s_State.guid))
                return;
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(s_State.guid));
            if (asset == null)
                return;
            var labels = s_State.labels ?? Array.Empty<string>();
            if (!Same(AssetDatabase.GetLabels(asset), labels))
                AssetDatabase.SetLabels(asset, labels);
        }

        static bool Same(string[] a, string[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }

        public static string[] Get(UnityEngine.Object asset)
        {
            return asset != null ? AssetDatabase.GetLabels(asset) : Array.Empty<string>();
        }

        /// <summary>Sets an asset's labels, undoably</summary>
        public static void Set(UnityEngine.Object asset, string[] labels, string undoName = "Change Labels")
        {
            if (asset == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long _))
                return;
            var current = AssetDatabase.GetLabels(asset);
            labels ??= Array.Empty<string>();
            if (Same(current, labels))
                return;
            var state = GetState();
            // what it is now, so undoing goes back to it
            state.guid   = guid;
            state.labels = current;
            Undo.RecordObject(state, undoName);
            state.labels = (string[])labels.Clone();
            AssetDatabase.SetLabels(asset, labels);
        }

        /// <summary>Adds a label, unless the asset has it already in any case; existing is how the project spells it</summary>
        public static void Add(UnityEngine.Object asset, string label, string existing = null)
        {
            label = (existing ?? label)?.Trim();
            if (string.IsNullOrEmpty(label))
                return;
            var labels = new List<string>(Get(asset));
            if (labels.Exists(l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase)))
                return;
            labels.Add(label);
            Set(asset, labels.ToArray(), "Add Label");
        }

        public static void Remove(UnityEngine.Object asset, string label)
        {
            var labels = new List<string>(Get(asset));
            if (labels.RemoveAll(l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase)) == 0)
                return;
            Set(asset, labels.ToArray(), "Remove Label");
        }
    }
}
