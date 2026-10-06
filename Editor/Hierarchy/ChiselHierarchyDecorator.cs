using System.Collections.Generic;
using System.Text;
using Chisel.Components;
using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Chisel.Editors
{
    [InitializeOnLoad]
    public static class ChiselHierarchyDecorator
    {
        const string kWarningBadgeName = "chisel-warning-badge";
        const string kWarningIconName  = "warning";

        // OverlayIcon covers the full 16x16 icon; the warning sits in a corner of it as a sub-icon.
        const float kWarningBadgeSize = 10;

        static readonly Color kActiveModelColor = new(0.4f, 0.8f, 1.0f);

        // Matches the alpha the old overlay used to fade disabled nodes.
        const float kInactiveOpacity = 0.25f;

        // NodeHierarchyModified covers edits; the poll is the safety net for state that changes without
        // it, such as the active model or a node being enabled/disabled.
        const double kPollInterval = 0.1;

        static Texture2D s_WarningImage;
        static double    s_NextPollTime;

        sealed class RowState
        {
            public ChiselNodeComponent node;
            public Texture             icon;
            public bool                hasWarning;
            public bool                isActiveModel;
            public bool                isEnabled;
        }

        static readonly Dictionary<HierarchyViewItem, RowState> s_BoundRows = new();
        static readonly List<HierarchyViewItem> s_Dropped = new();

        static ChiselHierarchyDecorator()
        {
            HierarchyWindow.BindViewItem   -= OnBindViewItem;
            HierarchyWindow.BindViewItem   += OnBindViewItem;
            HierarchyWindow.UnbindViewItem -= OnUnbindViewItem;
            HierarchyWindow.UnbindViewItem += OnUnbindViewItem;
            HierarchyWindow.GetTooltip     -= OnGetTooltip;
            HierarchyWindow.GetTooltip     += OnGetTooltip;

            ChiselNodeHierarchyManager.NodeHierarchyModified -= OnNodeHierarchyModified;
            ChiselNodeHierarchyManager.NodeHierarchyModified += OnNodeHierarchyModified;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        static bool TryGetChiselNode(HierarchyViewItem item, out ChiselNodeComponent node)
        {
            node = null;
            if (item.Handler is not HierarchyGameObjectHandler handler)
                return false;

            var gameObject = handler.GetGameObject(item.Node);
            if (!gameObject)
                return false;

            return gameObject.TryGetComponent(out node);
        }

        static bool IsActiveModel(ChiselNodeComponent node)
        {
            var model = node as ChiselModelComponent;
            if (ReferenceEquals(model, null))
                return false;
            return model == ChiselModelManager.Instance.ActiveModel;
        }

        // Returns true when anything the row draws has changed since it was last written.
        static bool Evaluate(RowState state)
        {
            var icon          = ChiselNodeDetailsManager.GetHierarchyIcon(state.node, out bool hasValidState);
            var image         = icon?.image;
            var hasWarning    = !hasValidState;
            var isActiveModel = IsActiveModel(state.node);
            var isEnabled     = state.node.isActiveAndEnabled;

            if (ReferenceEquals(image, state.icon) &&
                hasWarning    == state.hasWarning &&
                isActiveModel == state.isActiveModel &&
                isEnabled     == state.isEnabled)
                return false;

            state.icon          = image;
            state.hasWarning    = hasWarning;
            state.isActiveModel = isActiveModel;
            state.isEnabled     = isEnabled;
            return true;
        }

        static void ClearDecorations(HierarchyViewItem item)
        {
            if (item.Icon != null)
            {
                item.Icon.style.backgroundImage = StyleKeyword.Null;
                item.Icon.style.opacity         = StyleKeyword.Null;
            }

            item.OverlayIcon?.Q<VisualElement>(kWarningBadgeName)?.RemoveFromHierarchy();

            if (item.Name == null)
                return;
            item.Name.style.unityFontStyleAndWeight = StyleKeyword.Null;
            item.Name.style.color                   = StyleKeyword.Null;
        }

        static void AddWarningSubIcon(HierarchyViewItem item, float opacity)
        {
            if (item.OverlayIcon == null)
                return;

            s_WarningImage ??= ChiselEditorResources.GetIconContent(kWarningIconName)[0].image as Texture2D;
            if (s_WarningImage == null)
                return;

            var badge = new VisualElement { name = kWarningBadgeName };
            badge.style.position        = Position.Absolute;
            badge.style.right           = 0;
            badge.style.bottom          = 0;
            badge.style.width           = kWarningBadgeSize;
            badge.style.height          = kWarningBadgeSize;
            badge.style.opacity         = opacity;
            badge.style.backgroundImage = new StyleBackground(s_WarningImage);
            badge.pickingMode           = PickingMode.Ignore;
            item.OverlayIcon.Add(badge);
        }

        static void Apply(HierarchyViewItem item, RowState state)
        {
            ClearDecorations(item);

            if (state.isActiveModel && item.Name != null)
            {
                item.Name.style.unityFontStyleAndWeight = FontStyle.Bold;
                item.Name.style.color                   = kActiveModelColor;
            }

            var opacity = state.isEnabled ? 1.0f : kInactiveOpacity;

            // The boolean operation is the node's identity in the hierarchy, so it takes the main icon
            // slot; the per-component [Icon] shape icon still identifies the type everywhere else.
            if (item.Icon != null && state.icon != null)
            {
                item.Icon.style.backgroundImage = new StyleBackground(state.icon as Texture2D);
                item.Icon.style.opacity         = opacity;
            }

            if (state.hasWarning)
                AddWarningSubIcon(item, opacity);
        }

        static void OnBindViewItem(HierarchyWindow window, HierarchyView view, HierarchyViewItem item)
        {
            ClearDecorations(item);
            s_BoundRows.Remove(item);

            if (!TryGetChiselNode(item, out var node))
                return;

            var state = new RowState { node = node };
            Evaluate(state);
            Apply(item, state);
            s_BoundRows[item] = state;
        }

        static void OnUnbindViewItem(HierarchyWindow window, HierarchyView view, HierarchyViewItem item)
        {
            ClearDecorations(item);
            s_BoundRows.Remove(item);
        }

        static void OnNodeHierarchyModified()
        {
            RefreshBoundRows();
        }

        static void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup < s_NextPollTime)
                return;
            s_NextPollTime = EditorApplication.timeSinceStartup + kPollInterval;
            RefreshBoundRows();
        }

        // Only walks rows that are currently bound, which is the handful actually on screen.
        static void RefreshBoundRows()
        {
            if (s_BoundRows.Count == 0)
                return;

            foreach (var (item, state) in s_BoundRows)
            {
                // A row that lost its panel was recycled without an unbind; a destroyed node leaves a
                // state behind. Either way it is no longer ours to draw.
                if (item.panel == null || !state.node)
                {
                    s_Dropped.Add(item);
                    continue;
                }

                if (Evaluate(state))
                    Apply(item, state);
            }

            foreach (var item in s_Dropped)
                s_BoundRows.Remove(item);
            s_Dropped.Clear();
        }

        static void OnGetTooltip(HierarchyWindow window, HierarchyView view, HierarchyViewItem item, StringBuilder tooltip, bool filtering)
        {
            if (!TryGetChiselNode(item, out var node))
                return;

            var icon = ChiselNodeDetailsManager.GetHierarchyIcon(node, out bool hasValidState);

            if (IsActiveModel(node))
                AppendLine(tooltip, "Active model");

            if (!hasValidState && icon != null && !string.IsNullOrEmpty(icon.tooltip))
                AppendLine(tooltip, icon.tooltip);
        }

        static void AppendLine(StringBuilder tooltip, string text)
        {
            if (tooltip.Length > 0)
                tooltip.AppendLine();
            tooltip.Append(text);
        }
    }
}
