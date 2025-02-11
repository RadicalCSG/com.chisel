using System.Collections.Generic;
using System.Text;
using Chisel.Components;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // This window is a helper window to see what the CSG tree looks like internally, on the managed side
    sealed class ChiselManagedHierarchyView : EditorWindow
	{
		static ChiselManagedHierarchyView s_Window;
		readonly static List<ChiselManagedHierarchyView> s_Windows = new();

		class Styles
		{
			public GUIStyle emptyItem;
			public GUIStyle emptySelected;
			public GUIStyle foldOut;
			public GUIStyle foldOutSelected;

			public GUIStyle emptyLabelItem;
			public GUIStyle emptyLabelSelected;
			public GUIStyle foldOutLabel;
			public GUIStyle foldOutLabelSelected;

			public Color backGroundColor;
		};

		static Styles s_Styles;

		static void UpdateStyles()
		{
            if (s_Styles != null)
                return;

			s_Styles = new Styles();
			s_Styles.emptyItem = new GUIStyle(EditorStyles.foldout);

			s_Styles.emptyItem.active.background = null;
			s_Styles.emptyItem.hover.background = null;
			s_Styles.emptyItem.normal.background = null;
			s_Styles.emptyItem.focused.background = null;

			s_Styles.emptyItem.onActive.background = null;
			s_Styles.emptyItem.onHover.background = null;
			s_Styles.emptyItem.onNormal.background = null;
			s_Styles.emptyItem.onFocused.background = null;

			s_Styles.emptySelected = new GUIStyle(s_Styles.emptyItem);
			s_Styles.emptySelected.normal = s_Styles.emptySelected.active;
			s_Styles.emptySelected.onNormal = s_Styles.emptySelected.onActive;


			s_Styles.emptyLabelItem = new GUIStyle(EditorStyles.label);
			s_Styles.emptyLabelSelected = new GUIStyle(s_Styles.emptyLabelItem);
			s_Styles.emptyLabelSelected.normal = s_Styles.emptyLabelSelected.active;
			s_Styles.emptyLabelSelected.onNormal = s_Styles.emptyLabelSelected.onActive;


			s_Styles.foldOut = new GUIStyle(EditorStyles.foldout);
			s_Styles.foldOut.focused = s_Styles.foldOut.normal;
			s_Styles.foldOut.active = s_Styles.foldOut.normal;
			s_Styles.foldOut.onNormal = s_Styles.foldOut.normal;
			s_Styles.foldOut.onActive = s_Styles.foldOut.normal;

			s_Styles.foldOutSelected = new GUIStyle(EditorStyles.foldout);
			s_Styles.foldOutSelected.normal = s_Styles.foldOutSelected.active;
			s_Styles.foldOutSelected.onNormal = s_Styles.foldOutSelected.onActive;



			s_Styles.foldOutLabel = new GUIStyle(EditorStyles.label);
			s_Styles.foldOutLabel.active = s_Styles.foldOutLabel.normal;
			s_Styles.foldOutLabel.onActive = s_Styles.foldOutLabel.onNormal;

			s_Styles.foldOutLabelSelected = new GUIStyle(EditorStyles.label);
			s_Styles.foldOutLabelSelected.normal = s_Styles.foldOutLabelSelected.active;
			s_Styles.foldOutLabelSelected.onNormal = s_Styles.foldOutLabelSelected.onActive;

			s_Styles.backGroundColor = s_Styles.foldOutLabelSelected.onNormal.textColor;
			s_Styles.backGroundColor.a = 0.5f;

			GUIStyleState selected = s_Styles.foldOutLabelSelected.normal;
			selected.textColor = Color.white;
			s_Styles.foldOutSelected.normal = selected;
			s_Styles.foldOutSelected.onNormal = selected;
			s_Styles.foldOutSelected.active = selected;
			s_Styles.foldOutSelected.onActive = selected;
			s_Styles.foldOutSelected.focused = selected;
			s_Styles.foldOutSelected.onFocused = selected;

			s_Styles.foldOutLabelSelected.normal = selected;
			s_Styles.foldOutLabelSelected.onNormal = selected;
			s_Styles.foldOutLabelSelected.active = selected;
			s_Styles.foldOutLabelSelected.onActive = selected;
			s_Styles.foldOutLabelSelected.focused = selected;
			s_Styles.foldOutLabelSelected.onFocused = selected;

			s_Styles.emptyLabelSelected.normal = selected;
			s_Styles.emptyLabelSelected.onNormal = selected;
			s_Styles.emptyLabelSelected.active = selected;
			s_Styles.emptyLabelSelected.onActive = selected;
			s_Styles.emptyLabelSelected.focused = selected;
			s_Styles.emptyLabelSelected.onFocused = selected;




			s_Styles.emptyItem.active = s_Styles.emptyItem.normal;
			s_Styles.emptyItem.onActive = s_Styles.emptyItem.onNormal;
		}


		const int kItemHeight = 20;
		const int kScrollWidth = 20;
		const int kItemIndent = 20;
		//const int kIconWidth = 20;
		const int kPadding = 2;
		static Vector2 s_ScrollPos;

		sealed class StackItem
		{
			public StackItem(List<ChiselHierarchyItem> _children, float _xpos = 0) { children = _children; index = 0; count = children.Count; xpos = _xpos; }
			public int index;
			public int count;
			public float xpos;
			public List<ChiselHierarchyItem> children;
		}
		readonly static List<StackItem> s_ItemStack = new();

		ChiselManagedHierarchyView()
        {
            s_Windows.Add(this);
        }

        void OnDestroy()
        {
            s_Windows.Remove(this);
        }

        public static void RepaintAll()
        {
            // Prevent infinite loops
            if (Event.current != null &&
                Event.current.type == EventType.Repaint)
                return;
            foreach (var window in s_Windows)
            {
                if (window)
                    window.Repaint();
            }
        }

        [MenuItem("Chisel DEBUG/Managed Chisel Hierarchy")]
        static void Create()
        {
            s_Window = (ChiselManagedHierarchyView)EditorWindow.GetWindow(typeof(ChiselManagedHierarchyView), false, "Managed Chisel Hierarchy");
            s_Window.autoRepaintOnSceneChange = true;
        }

        static int GetVisibleItems(Dictionary<int, ChiselSceneHierarchy> sceneHierarchies)
        {
            if (sceneHierarchies == null || sceneHierarchies.Count == 0)
                return 0;

            int totalCount = 0;
            foreach (var item in sceneHierarchies)
            {
                totalCount += 1; // scene foldout itself
                s_ItemStack.Clear();
                totalCount += GetVisibleItems(item.Value.RootItems);
            }
            return totalCount;
        }
        
        static int GetVisibleItems(List<ChiselHierarchyItem> hierarchyItems)
        {
            if (hierarchyItems == null)
                return 0;

            int totalCount = hierarchyItems.Count;
            s_ItemStack.Add(new StackItem(hierarchyItems));

            ContinueOnNextStackItem:
            if (s_ItemStack.Count == 0)
                return totalCount;

            var currentStackItem = s_ItemStack[s_ItemStack.Count - 1];
            var children = currentStackItem.children;

            while (currentStackItem.index < currentStackItem.count)
            {
                int i = currentStackItem.index;
                currentStackItem.index++;
                if (children[i] == null)
                    continue;

                if (children[i].IsOpen && children[i].Children != null && children[i].Children.Count > 0)
                {
                    totalCount += children[i].Children.Count;
                    s_ItemStack.Add(new StackItem(children[i].Children));
                    //totalCount += GetVisibleItems(hierarchyItems[i].Children);
                    goto ContinueOnNextStackItem;
                }
            }
            s_ItemStack.RemoveAt(s_ItemStack.Count - 1);
            goto ContinueOnNextStackItem;
        }

        static void AddFoldOuts(ref Rect itemRect, ref Rect visibleArea, HashSet<Transform> selectedTransforms, Dictionary<int, ChiselSceneHierarchy> sceneHierarchies)
        {
            if (sceneHierarchies == null || sceneHierarchies.Count == 0)
                return;

            var defaultColor = GUI.color;
            foreach (var item in sceneHierarchies)
            {
                var scene = item.Value.Scene;
                if (itemRect.Overlaps(visibleArea))
                {
                    var name = scene.name;
                    if (string.IsNullOrEmpty(name))
                        EditorGUI.LabelField(itemRect, "Untitled");
                    else
                        EditorGUI.LabelField(itemRect, name);
                }
                itemRect.y += kItemHeight;
                itemRect.x += kItemIndent;
                s_ItemStack.Clear();
                AddFoldOuts(ref itemRect, ref visibleArea, selectedTransforms, item.Value.RootItems);
                itemRect.x -= kItemIndent;
            }
            GUI.color = defaultColor;
        }

        static void AddFoldOuts(ref Rect itemRect, ref Rect visibleArea, HashSet<Transform> selectedTransforms, List<ChiselHierarchyItem> hierarchyItems)
        {
            if (hierarchyItems == null)
                return;
            s_ItemStack.Add(new StackItem(hierarchyItems, itemRect.x));

            ContinueOnNextStackItem:
            if (s_ItemStack.Count == 0)
                return;

            var prevBackgroundColor = GUI.backgroundColor;
            var currentStackItem = s_ItemStack[s_ItemStack.Count - 1];
            var children = currentStackItem.children;
            itemRect.x = currentStackItem.xpos;
            while (currentStackItem.index < currentStackItem.count)
            {
                int i = currentStackItem.index; currentStackItem.index++;
                if (itemRect.y > visibleArea.yMax)
                {
                    GUI.backgroundColor = prevBackgroundColor;
                    return;
                }
                if (itemRect.y > visibleArea.yMin)
                {
                    var name = NameForTreeNode(children[i]);
                    var childCount = (children[i].Children == null) ? 0 : children[i].Children.Count;
                    var selected = selectedTransforms.Contains(children[i].Transform);

                    var foldOutStyle = (childCount > 0) ? s_Styles.foldOut : s_Styles.emptyItem;
                    var labelStyle = (childCount > 0) ?
                                            (selected ? s_Styles.foldOutLabelSelected : s_Styles.foldOutLabel) :
                                            (selected ? s_Styles.emptyLabelSelected : s_Styles.emptyLabelItem);

                    //				GUI.enabled = children[i].Enabled;

                    const float labelOffset = 14;

                    if (selected)
                    {
                        GUI.backgroundColor = s_Styles.backGroundColor;
                        var extended = itemRect;
                        extended.x = 0;
                        extended.height -= 4;
                        GUI.Box(extended, GUIContent.none);
                    } else
                        GUI.backgroundColor = prevBackgroundColor;
                    EditorGUI.BeginChangeCheck();
                    var foldOutRect = itemRect;
                    foldOutRect.width = labelOffset;
                    var labelRect = itemRect;
                    labelRect.x += labelOffset;
                    labelRect.width -= labelOffset;
                    children[i].IsOpen = EditorGUI.Foldout(foldOutRect, children[i].IsOpen, string.Empty, true, foldOutStyle);
                    if (EditorGUI.EndChangeCheck() ||
                        GUI.Button(labelRect, name, labelStyle))
                    {
                        Selection.activeTransform = children[i].Transform;
                    }
                }
                itemRect.y += kItemHeight;

                if (children[i].IsOpen && children[i].Children != null && children[i].Children.Count > 0)
                {
                    s_ItemStack.Add(new StackItem(children[i].Children, itemRect.x + kItemIndent));
                    goto ContinueOnNextStackItem;
                }
            }
            s_ItemStack.RemoveAt(s_ItemStack.Count - 1);
            goto ContinueOnNextStackItem;
        }

        static StringBuilder stringBuilder = new StringBuilder();
        static string StringForSiblingIndices(ChiselHierarchyItem node, int index)
        {
            stringBuilder.Clear();
            for (;index < node.SiblingIndices.Count; index++)
            {
                int value = node.SiblingIndices[index];
                if (stringBuilder.Length != 0)
                    stringBuilder.Append(',');
                stringBuilder.Append(value);
            }
            return stringBuilder.ToString();
        }

        static string NameForTreeNode(ChiselHierarchyItem node)
        {
            var treeNode = node.Component.TopTreeNode;
            var instanceID = node.Component.GetInstanceID();
            var obj = node.Transform;
            var siblingIndices = StringForSiblingIndices(node, (node.Parent == null) ? 0 : node.Parent.SiblingIndices.Count);
            if (!obj)
                return $"[{siblingIndices}] <unknown> [{treeNode}:{instanceID}]";
            return $"[{siblingIndices}] {obj.name} [{treeNode}:{instanceID}]";
        }

        void OnGUI()
        {
            ChiselNodeHierarchyManager.Update();
            UpdateStyles();

            var selectedTransforms = new HashSet<Transform>();
            foreach (var transform in Selection.transforms)
                selectedTransforms.Add(transform);

            var totalCount = GetVisibleItems(ChiselNodeHierarchyManager.sceneHierarchies);

            var itemArea = position;
            itemArea.x = 0;
            itemArea.y = 0;

            var totalRect = position;
            totalRect.x = 0;
            totalRect.y = 0;
            totalRect.width = position.width - kScrollWidth;
            totalRect.height = (totalCount * kItemHeight) + (2 * kPadding);

            var itemRect = position;
            itemRect.x = 0;
            itemRect.y = kPadding;
            itemRect.height = kItemHeight;

            s_ScrollPos = GUI.BeginScrollView(itemArea, s_ScrollPos, totalRect);
            {
                Rect visibleArea = itemArea;
                visibleArea.x += s_ScrollPos.x;
                visibleArea.y += s_ScrollPos.y;

                AddFoldOuts(ref itemRect, ref visibleArea, selectedTransforms, ChiselNodeHierarchyManager.sceneHierarchies);
            }
            GUI.EndScrollView();
        }

    }
}