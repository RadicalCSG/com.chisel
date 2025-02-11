using System.Collections.Generic;
using System.Linq;
using Chisel.Core;
using Chisel.Components;
using Unity.Collections;
using UnityEngine;
using UnityEditor;

namespace Chisel.Editors
{
// TODO: rebuild this using new API / proper treeview
#if true
    // This window is a helper window to see what the CSG tree looks like internally
    sealed class ChiselInternalHierarchyView : EditorWindow
	{
		Dictionary<CSGTreeNode, bool> openNodes = new();
		readonly static List<ChiselInternalHierarchyView> s_Windows = new();

		static ChiselInternalHierarchyView window;

		//const int kIconWidth = 20;
		const int kScrollWidth = 20;
		const int kItemIndent = 20;
		const int kPadding = 2;
		static Vector2 s_ScrollPos;


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

			s_Styles = new Styles
			{
				emptyItem = new GUIStyle(EditorStyles.foldout)
			};

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

		sealed class StackItem
		{
			public StackItem(CSGTreeNode[] _children, float _xpos = 0) { children = _children; index = 0; count = children.Length; xpos = _xpos; }
			public int index;
			public int count;
			public float xpos;
			public CSGTreeNode[] children;
		}

		readonly static List<StackItem> s_ItemStack = new();


		ChiselInternalHierarchyView()
        {
            s_Windows.Add(this);
        }

        public void Awake()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;

            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnHierarchyChanged()
        {
            this.Repaint();
        }

        public void OnSelectionChanged()
        {
            this.Repaint();
        }

        void OnDestroy()
        {
            s_Windows.Remove(this);
        }

        public static void RepaintAll()
        {
            foreach (var window in s_Windows)
            {
                if (window)
                    window.Repaint();
            }
        }

        [MenuItem("Chisel DEBUG/Internal Chisel Hierarchy")]
        static void Create()
        {
            window = (ChiselInternalHierarchyView)EditorWindow.GetWindow(typeof(ChiselInternalHierarchyView), false, "Internal Chisel Hierarchy");
            window.autoRepaintOnSceneChange = true;
        }

        static int GetVisibleItems(CSGTreeNode[] hierarchyItems, ref Dictionary<CSGTreeNode, bool> openNodes)
        {
            if (hierarchyItems == null)
                return 0;

            int totalCount = hierarchyItems.Length;
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

                var child = children[i];
                bool isOpen;
                if (!openNodes.TryGetValue(child, out isOpen))
                {
                    isOpen = true;
                    openNodes[child] = true;
                }
                if (isOpen)
                {
                    if (children[i].Valid)
                    {
                        var childCount = children[i].Count;
                        if (childCount > 0)
                        {
                            totalCount += childCount;
                            s_ItemStack.Add(new StackItem(ChildrenToArray(children[i])));
                            goto ContinueOnNextStackItem;
                        }
                    }
                }
            }
            s_ItemStack.RemoveAt(s_ItemStack.Count - 1);
            goto ContinueOnNextStackItem;
        }

        static CSGTreeNode[] ChildrenToArray(CSGTreeNode node)
        {
            var children = new CSGTreeNode[node.Count];
            for (int i = 0; i < node.Count; i++)
                children[i] = node[i];
            return children;
        }

        static void AddFoldOuts(ref Rect itemRect, ref Rect visibleArea, CSGTreeNode[] hierarchyItems, HashSet<int> selectedInstanceIDs, ref Dictionary<CSGTreeNode, bool> openNodes)
        {
            if (hierarchyItems == null || hierarchyItems.Length == 0)
                return;

            var defaultColor = GUI.color;
            AddFoldOuts(ref itemRect, ref visibleArea, hierarchyItems, selectedInstanceIDs, defaultColor, ref openNodes);
            GUI.color = defaultColor;
        }

        static string NameForTreeNode(CSGTreeNode treeNode)
        {
            var instanceID = treeNode.InstanceID;
            var obj = (instanceID != 0) ? EditorUtility.InstanceIDToObject(instanceID) : null;
            string name;
            if (obj == null)
            {
                name = "<unknown>";
            } else
            {
                name =  obj.name;
            }
            if (treeNode.Type == CSGNodeType.Brush)
            {
                var brush = (CSGTreeBrush)treeNode;
                if (treeNode.Valid)
                    return $"{name} [{treeNode}:{instanceID}:{brush.BrushMesh.BrushMeshID}]";
                else
                    return $"{name} [{treeNode}:{instanceID}:{brush.BrushMesh.BrushMeshID}] (INVALID)";
            } else
            {
                if (treeNode.Valid)
                    return $"{name} [{treeNode}:{instanceID}]";
                else
                    return $"{name} [{treeNode}:{instanceID}] (INVALID)";
            }
        }

        static void AddFoldOuts(ref Rect itemRect, ref Rect visibleArea, CSGTreeNode[] hierarchyItems, HashSet<int> selectedInstanceIDs, Color defaultColor, ref Dictionary<CSGTreeNode, bool> openNodes)
        {
            if (hierarchyItems == null)
                return;
            s_ItemStack.Add(new StackItem(hierarchyItems, itemRect.x));

            ContinueOnNextStackItem:
            if (s_ItemStack.Count == 0)
            {
                return;
            }

            float kItemHeight = EditorGUIUtility.singleLineHeight;

            var prevColor = GUI.color;
            var prevBackgroundColor = GUI.backgroundColor;
            var currentStackItem = s_ItemStack[s_ItemStack.Count - 1];
            var children = currentStackItem.children;
            itemRect.x = currentStackItem.xpos;
            while (currentStackItem.index < currentStackItem.count)
            {
                int i = currentStackItem.index;
                currentStackItem.index++;
                if (itemRect.y > visibleArea.yMax)
                {
                    GUI.backgroundColor = prevBackgroundColor;
                    return;
                }

                var child       = children[i];
                var instanceID	= child.InstanceID;
                var childCount	= child.Count;
                if (itemRect.y > visibleArea.yMin)
                {
                    var name			= NameForTreeNode(child);
                    var selected		= selectedInstanceIDs.Contains(instanceID);
                    var labelStyle		= (childCount > 0) ?
                                            (selected ? s_Styles.foldOutLabelSelected : s_Styles.foldOutLabel) :
                                            (selected ? s_Styles.emptyLabelSelected : s_Styles.emptyLabelItem);


                    bool isOpen;
                    if (!openNodes.TryGetValue(child, out isOpen))
                        openNodes[child] = false;

                    const float labelOffset = 14;

                    if (selected)
                    {
                        GUI.backgroundColor = s_Styles.backGroundColor;
                        var extended = itemRect;
                        extended.x = 0;
                        GUI.Box(extended, GUIContent.none);
                    } else
                        GUI.backgroundColor = prevBackgroundColor;
                    EditorGUI.BeginChangeCheck();
                    var foldOutRect = itemRect;
                    foldOutRect.width = labelOffset;
                    var labelRect = itemRect;
                    labelRect.x += labelOffset;
                    labelRect.width -= labelOffset;
                    if (childCount > 0)
                        openNodes[child] = EditorGUI.Foldout(foldOutRect, isOpen, string.Empty, true, s_Styles.foldOut);

                    if (!child.Valid)
                        GUI.color = Color.red;

                    if (EditorGUI.EndChangeCheck() ||
                        GUI.Button(labelRect, name, labelStyle))
                    {
                        var obj = EditorUtility.InstanceIDToObject(instanceID);
                        if (!(obj is GameObject))
                        {
                            var mono = (obj as MonoBehaviour);
                            if (mono)
                                instanceID = mono.gameObject.GetInstanceID();
                        }
                        Selection.instanceIDs = new[] { instanceID };
                    }
                    if (!child.Valid)
                        GUI.color = prevColor;
                }
                itemRect.y += kItemHeight;

                if (openNodes[child])
                {
                    if (childCount > 0)
                    {
                        s_ItemStack.Add(new StackItem(ChildrenToArray(child), itemRect.x + kItemIndent));
                        goto ContinueOnNextStackItem;
                    }
                }
            }
            s_ItemStack.RemoveAt(s_ItemStack.Count - 1);
            goto ContinueOnNextStackItem;
        }


        void OnGUI()
        {
            UpdateStyles();
            
            var selectedInstanceIDs = new HashSet<int>();

            foreach (var instanceID in Selection.instanceIDs)
            {
                var obj = EditorUtility.InstanceIDToObject(instanceID);
                var go = obj as GameObject;
                if (go != null)
                {
                    foreach(var no in go.GetComponents<ChiselNodeComponent>())
                    {
                        var instanceID_ = no.GetInstanceID();
                        selectedInstanceIDs.Add(instanceID_);
                    }
                }
            }
            
            float kItemHeight = EditorGUIUtility.singleLineHeight;

            using (var allTreeNodes = new NativeList<CSGTreeNode>(Allocator.Temp))
            { 
                CompactHierarchyManager.GetAllTreeNodes(allTreeNodes);

                using (var allTrees = new NativeList<CSGTree>(Allocator.Temp))
                {
                    CompactHierarchyManager.GetAllTrees(allTrees);
                    var allRootNodeList = new List<CSGTreeNode>();
                    for (int i = 0; i < allTrees.Length;i++)
                        allRootNodeList.Add(allTrees[i]); 

                    var allRootNodes = allRootNodeList.ToArray();

                    var totalCount = GetVisibleItems(allRootNodes, ref openNodes); 

                    var itemArea = position;
                    itemArea.x = 0;
                    itemArea.y = 0;
                    itemArea.height -= 200;

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
                
                        AddFoldOuts(ref itemRect, ref visibleArea, allRootNodes, selectedInstanceIDs, ref openNodes);
                    }
                    GUI.EndScrollView();
                    if (selectedInstanceIDs.Count == 1)
                    {
                        var instanceID = selectedInstanceIDs.First();
                        var obj = EditorUtility.InstanceIDToObject(instanceID) as ChiselNodeComponent;
                        if (obj)
                        {
                            var brush = obj as ChiselBrushComponent;
                            var composite = obj as ChiselCompositeComponent;
                            var model = obj as ChiselModelComponent;
                            CSGTreeNode node = CSGTreeNode.Invalid;
                            if (brush) node = brush.TopTreeNode;
                            else if (composite) node = composite.Node;
                            else if (model) node = model.Node;
                            else
                            {
                                for (int n = 0; n < allTreeNodes.Length; n++)
                                {
                                    if (allTreeNodes[n].InstanceID == instanceID)
                                    {
                                        node = allTreeNodes[n];
                                        break;
                                    }
                                }
                            }

                            if (node != CSGTreeNode.Invalid)
                            {
                                var labelArea = itemArea;
                                labelArea.x = 0;
                                labelArea.y = labelArea.height;
                                labelArea.height = kItemHeight;
                                GUI.Label(labelArea, $"Node: {node}"); labelArea.y += kItemHeight;
                                GUI.Label(labelArea, $"InstanceID: {node.InstanceID}"); labelArea.y += kItemHeight;
                                GUI.Label(labelArea, $"Operation: {node.Operation}"); labelArea.y += kItemHeight;
                                GUI.Label(labelArea, $"Valid: {node.Valid}"); labelArea.y += kItemHeight;
                                GUI.Label(labelArea, $"NodeType: {node.Type}"); labelArea.y += kItemHeight;
                                GUI.Label(labelArea, $"ChildCount: {node.Count}"); labelArea.y += kItemHeight;
                                if (node.Type != CSGNodeType.Tree)
                                {
                                    GUI.Label(labelArea, $"Parent: {node.Parent} valid: {node.Parent.Valid}"); labelArea.y += kItemHeight;
                                    GUI.Label(labelArea, $"Model: {node.Tree} valid: {node.Tree.Valid}"); labelArea.y += kItemHeight;
                                }
                                if (node.Type == CSGNodeType.Brush)
                                {
                                    var treeBrush = (CSGTreeBrush)node;
                                    var brushMeshInstance = treeBrush.BrushMesh;
                                    GUI.Label(labelArea, $"BrushMeshInstance: {brushMeshInstance.BrushMeshID} valid: {brushMeshInstance.Valid}"); labelArea.y += kItemHeight;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
#endif
}

