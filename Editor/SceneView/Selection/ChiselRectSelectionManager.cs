using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Chisel.Core;
using Chisel.Components;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;

namespace Chisel.Editors
{
    [Serializable]
    public enum SelectionType { Replace, Additive, Subtractive };

#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class ChiselRectSelection
    {
        public static bool  Valid			{ get { return s_ReflectionSucceeded; } }
        public static int   RectSelectionID { get; private set; }
        
        static object s_RectSelection;
        static SceneView s_SceneView;
        public static SceneView SceneView
        {
            get
            {
                return s_SceneView;
            }
            set
            {
                if (s_SceneView == value)
                    return;
                s_SceneView = value;
                s_RectSelection = s_RectSelectionField.GetValue(s_SceneView);
                RectSelectionID = (int)s_RectSelectionIDField.GetValue(s_RectSelection);
            }
        }

        public static bool      RectSelecting       { get { return RectSelectionID != 0 && (GUIUtility.hotControl == RectSelectionID || HandleUtility.nearestControl == RectSelectionID); } }
        public static bool      IsNearestControl    { get { return (bool)s_IsNearestControlField.GetValue(s_RectSelection); } }
        public static Vector2	SelectStartPoint	{ get { return (Vector2)s_SelectStartPointField.GetValue(s_RectSelection); } }
        public static Vector2	SelectMousePoint	{ get { return (Vector2)s_SelectMousePointField.GetValue(s_RectSelection); } }
        public static Object[]	SelectionStart		{ get { return (Object[])s_SelectionStartField.GetValue(s_RectSelection); } set { s_SelectionStartField.SetValue(s_RectSelection, value); } }
        public static Object[]  CurrentSelection	{ get { return (Object[])s_CurrentSelectionField.GetValue(s_RectSelection); } set { s_CurrentSelectionField.SetValue(s_RectSelection, value); } }
        public static Dictionary<GameObject, bool>	LastSelection { get { return (Dictionary<GameObject, bool>)s_LastSelectionField.GetValue(s_RectSelection); } }

        public static void UpdateSelection(Object[] existingSelection, Object[] newObjects, SelectionType type)
        {
            object selectionType;
            switch (type)
            {
                default:						selectionType = s_SelectionTypeNormal; break;
                case SelectionType.Additive:	selectionType = s_SelectionTypeAdditive; break;
                case SelectionType.Subtractive:	selectionType = s_SelectionTypeSubtractive; break;
            }

            s_UpdateSelectionMethod.Invoke(s_RectSelection,
                new object[]
                {
                    existingSelection,
                    newObjects,
                    selectionType,
                    RectSelecting
                });
        }

        static readonly Type s_UnityRectSelectionType;
        static readonly Type s_UnityEnumSelectionType;

        static readonly object s_SelectionTypeAdditive;
        static readonly object s_SelectionTypeSubtractive;
        static readonly object s_SelectionTypeNormal;
            
        static readonly FieldInfo s_RectSelectionField;
        static readonly FieldInfo s_SelectStartPointField;
        static readonly FieldInfo s_IsNearestControlField;
        static readonly FieldInfo s_SelectMousePointField;
        static readonly FieldInfo s_SelectionStartField;
        static readonly FieldInfo s_LastSelectionField;
        static readonly FieldInfo s_CurrentSelectionField;
        
        static readonly FieldInfo s_RectSelectionIDField;

        static readonly MethodInfo s_UpdateSelectionMethod;
        
        static readonly bool s_ReflectionSucceeded = false;

        // Unity's rect selection in the scene view has no public API: it is read and driven through its internals
        const string kFeature = "selecting brushes with a rectangle";

        static ChiselRectSelection()
        {
            s_UnityRectSelectionType    = ChiselUnityInternals.Type("UnityEditor.RectSelection", kFeature);
            s_UnityEnumSelectionType    = ChiselUnityInternals.Type("UnityEditor.RectSelection+SelectionType", kFeature);
            s_RectSelectionField        = ChiselUnityInternals.Field(typeof(SceneView), "m_RectSelection", kFeature);
            s_RectSelectionIDField      = ChiselUnityInternals.Field(s_UnityRectSelectionType, "k_RectSelectionID", kFeature);
            s_SelectStartPointField     = ChiselUnityInternals.Field(s_UnityRectSelectionType, "m_StartPoint", kFeature);
            s_IsNearestControlField     = ChiselUnityInternals.Field(s_UnityRectSelectionType, "m_IsNearestControl", kFeature);
            s_SelectionStartField       = ChiselUnityInternals.Field(s_UnityRectSelectionType, "m_SelectionStart", kFeature);
            s_LastSelectionField        = ChiselUnityInternals.Field(s_UnityRectSelectionType, "m_LastSelection", kFeature);
            s_CurrentSelectionField     = ChiselUnityInternals.Field(s_UnityRectSelectionType, "m_CurrentSelection", kFeature);
            s_SelectMousePointField     = ChiselUnityInternals.Field(s_UnityRectSelectionType, "m_SelectMousePoint", kFeature);
            s_UpdateSelectionMethod     = ChiselUnityInternals.Method(s_UnityRectSelectionType, "UpdateSelection", kFeature,
                                                                      typeof(Object[]), typeof(Object[]), s_UnityEnumSelectionType, typeof(bool));
            s_SelectionTypeAdditive     = ChiselUnityInternals.EnumValue(s_UnityEnumSelectionType, "Additive", kFeature);
            s_SelectionTypeSubtractive  = ChiselUnityInternals.EnumValue(s_UnityEnumSelectionType, "Subtractive", kFeature);
            s_SelectionTypeNormal       = ChiselUnityInternals.EnumValue(s_UnityEnumSelectionType, "Normal", kFeature);

            s_ReflectionSucceeded = s_RectSelectionField        != null &&
                                    s_RectSelectionIDField      != null &&
                                    s_SelectStartPointField     != null &&
                                    s_IsNearestControlField     != null &&
                                    s_SelectionStartField       != null &&
                                    s_LastSelectionField        != null &&
                                    s_CurrentSelectionField     != null &&
                                    s_SelectMousePointField     != null &&
                                    s_UpdateSelectionMethod     != null &&
                                    s_SelectionTypeAdditive     != null &&
                                    s_SelectionTypeSubtractive  != null &&
                                    s_SelectionTypeNormal       != null;
        }
    }

    // TODO: clean up, rename
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class ChiselRectSelectionManager
    {
        static HashSet<CSGTreeNode> s_RectFoundTreeNodes	= new();
        static readonly HashSet<GameObject> s_RectFoundGameObjects = new();
        static Vector2  s_PrevStartGUIPoint;
        static Vector2  s_PrevMouseGUIPoint;
        static Vector2  s_PrevStartScreenPoint;
        static Vector2  s_PrevMouseScreenPoint;

        static bool     s_RectClickDown       = false;
        static bool     s_MouseDragged        = false;
        static Vector2  s_ClickMousePosition  = Vector2.zero;
        
        // TODO: put somewhere else
        public static SelectionType GetCurrentSelectionType()
        {            
            var selectionType = SelectionType.Replace;
            // shift only
            if ( Event.current.shift && !EditorGUI.actionKey && !Event.current.alt) { selectionType = SelectionType.Additive; } else
            // action key only (Command on macOS, Control on Windows)
            if (!Event.current.shift &&  EditorGUI.actionKey && !Event.current.alt) { selectionType = SelectionType.Subtractive; } 
            return selectionType;
        }
        
        static bool RemoveGeneratedMeshesFromArray(ref Object[] selection)
        {
            var found = new List<Object>();
            for (int i = selection.Length - 1; i >= 0; i--)
            {
                var obj = selection[i];
                if (ChiselGeneratedObjects.IsObjectGenerated(obj))
                    continue;
                found.Add(obj);
            }
            if (selection.Length != found.Count)
            {
                selection = found.ToArray();
                return true;
            }
            return false;
        }

        internal static void OnSceneGUI(SceneView sceneview)
        {
            if (!ChiselRectSelection.Valid)
            {
                s_PrevStartGUIPoint = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                s_PrevMouseGUIPoint = s_PrevStartGUIPoint;
                s_PrevStartScreenPoint = Vector2.zero;
                s_PrevMouseScreenPoint = Vector2.zero;
                s_RectFoundGameObjects.Clear();
                s_RectFoundTreeNodes.Clear();
                return;
            }

            ChiselRectSelection.SceneView = sceneview;


            var rectSelectionID		= ChiselRectSelection.RectSelectionID;
            var hotControl			= GUIUtility.hotControl;
            var areRectSelecting	= rectSelectionID != 0 && hotControl == rectSelectionID;
            var typeForControl		= Event.current.GetTypeForControl(rectSelectionID);

            // check if we're rect-selecting
            if (areRectSelecting)
            {
                if ((typeForControl == EventType.Used || Event.current.commandName == "ModifierKeysChanged") && 
                    ChiselRectSelection.RectSelecting)
                {
                    var selectStartPoint = ChiselRectSelection.SelectStartPoint;
                    var selectMousePoint = ChiselRectSelection.SelectMousePoint;

                    // determine if our frustum changed since the last time
                    bool modified	= false;
                    bool needUpdate = false;
                    if (s_PrevStartGUIPoint != selectStartPoint)
                    {
                        s_PrevStartGUIPoint		= selectStartPoint;
                        s_PrevStartScreenPoint    = selectStartPoint;
                        needUpdate = true;
                    }
                    if (s_PrevMouseGUIPoint != selectMousePoint)
                    {
                        s_PrevMouseGUIPoint	 = selectMousePoint;
                        s_PrevMouseScreenPoint = selectMousePoint;
                        needUpdate = true;
                    }
                    if (needUpdate)
                    {
                        var rect = ChiselCameraUtility.PointsToRect(s_PrevStartScreenPoint, s_PrevMouseScreenPoint);
                        if (rect.width > 3 && 
                            rect.height > 3)
                        { 
                            var frustum         = ChiselCameraUtility.GetCameraSubFrustum(Camera.current, rect);
                            var selectionType   = GetCurrentSelectionType();

                            if (selectionType == SelectionType.Replace)
                            {
                                s_RectFoundTreeNodes.Clear();
                                s_RectFoundGameObjects.Clear();
                            }

                            // TODO: modify this depending on debug rendermode
                            SurfaceDestinationFlags visibleLayerFlags = SurfaceDestinationFlags.Renderable;

                            // Find all the brushes (and it's gameObjects) that are inside the frustum
                            if (!ChiselSceneQuery.GetNodesInFrustum(frustum, UnityEditor.Tools.visibleLayers, visibleLayerFlags, ref s_RectFoundTreeNodes))
                            {
                                if (s_RectFoundGameObjects != null &&
                                    s_RectFoundGameObjects.Count > 0)
                                {
                                    s_RectFoundTreeNodes.Clear();
                                    s_RectFoundGameObjects.Clear();
                                    modified = true;
                                }
                            } else
                                modified = true;
            
                            foreach(var treeNode in s_RectFoundTreeNodes)
                            {
                                var brush = (CSGTreeBrush)treeNode;
                                if (brush.Valid)
                                {
                                    switch (selectionType)
                                    {
                                        case SelectionType.Additive:
                                        {
                                            ChiselSyncSelection.SelectBrushVariant(brush, uniqueSelection: false);
                                            break;
                                        }
                                        case SelectionType.Subtractive:
                                        {
                                            ChiselSyncSelection.DeselectBrushVariant(brush);
                                            break;
                                        }
                                        default:
                                        {
                                            ChiselSyncSelection.SelectBrushVariant(brush, uniqueSelection: true);
                                            break;
                                        }
                                    }
                                }
                                var nodeComponent = Resources.EntityIdToObject(treeNode.EntityId) as ChiselNodeComponent;
                                if (!nodeComponent)
                                    continue;
                                var gameObject = nodeComponent.gameObject;
                                s_RectFoundGameObjects.Add(gameObject);
                            }
                        }
                    }

                    Object[] currentSelection = null;
                    var originalLastSelection	= ChiselRectSelection.LastSelection;
                    var originalSelectionStart	= ChiselRectSelection.SelectionStart;

                    if (originalLastSelection != null)
                    {
                        if (modified &&
                            s_RectFoundGameObjects != null &&
                            s_RectFoundGameObjects.Count > 0)
                        {
                            foreach (var obj in s_RectFoundGameObjects)
                            {
                                // if it hasn't already been added, add the obj
                                if (!originalLastSelection.ContainsKey(obj))
                                {
                                    originalLastSelection.Add(obj, false);
                                }
                            }

                            currentSelection = originalLastSelection.Keys.ToArray();
                            ChiselRectSelection.CurrentSelection = currentSelection;
                        } else
                        {
                            if (currentSelection == null || modified) { currentSelection = originalLastSelection.Keys.ToArray(); }
                        }
                    } else
                        currentSelection = null;
                    
                    if (RemoveGeneratedMeshesFromArray(ref originalSelectionStart))
                        modified = true;
                    
                    if (currentSelection != null && RemoveGeneratedMeshesFromArray(ref currentSelection))
                        modified = true;

                    if ((Event.current.commandName == "ModifierKeysChanged" || modified))
                    {
                        var foundObjects = currentSelection;

                        RemoveGeneratedMeshesFromArray(ref foundObjects);
                            
                        // calling static method UpdateSelection of RectSelection 
                        ChiselRectSelection.UpdateSelection(originalSelectionStart, foundObjects, GetCurrentSelectionType());
                    }
                }
                hotControl = GUIUtility.hotControl;
            }

            if (hotControl != rectSelectionID)
            {
                s_PrevStartGUIPoint = Vector2.zero;
                s_PrevMouseGUIPoint = Vector2.zero;
                s_RectFoundGameObjects.Clear();
                s_RectFoundTreeNodes.Clear();
            }

            var evt = Event.current;
            switch (typeForControl)
            {
                case EventType.MouseDown:
                {
                    s_RectClickDown = (Event.current.button == 0 && areRectSelecting);
                    s_ClickMousePosition = Event.current.mousePosition;
                    s_MouseDragged = false;
                    break;
                }
                case EventType.MouseUp:
                {
                    if (!s_MouseDragged)
                    {
                        if ((UnityEditor.HandleUtility.nearestControl != 0 || evt.button != 0) &&
                            (GUIUtility.keyboardControl != 0 || evt.button != 2))
                            break;
                        Event.current.Use();
                    }
                    s_RectClickDown = false;
                    s_MouseDragged = false;
                    break;
                }
                case EventType.MouseMove:
                {
                    s_RectClickDown = false;
                    s_MouseDragged = false;
                    break;
                }
                case EventType.MouseDrag:
                {
                    s_MouseDragged = true;
                    break;
                }
                case EventType.Used:
                {
                    if (!s_MouseDragged)
                    {
                        var delta = Event.current.mousePosition - s_ClickMousePosition;
                        if (Mathf.Abs(delta.x) > 4 || Mathf.Abs(delta.y) > 4) { s_MouseDragged = true; }
                    }
                    if (s_MouseDragged || !s_RectClickDown || Event.current.button != 0 || ChiselRectSelection.RectSelecting) { s_RectClickDown = false; break; }

                    Event.current.Use();
                    break;
                }
                case EventType.KeyUp:
                {
                    if (hotControl == 0 &&
                        Event.current.keyCode == ChiselKeyboardDefaults.kCancelKey)
                    {
                        if (GUIUtility.hotControl == 0 && // make sure we're not actively doing anything
                            Tools.current != Tool.Custom)
                        {
                            // This deselects everything and disables all tool modes
                            Selection.activeTransform = null;
                            Event.current.Use();
                        }
                    }
                    break;
                }
                case EventType.ValidateCommand:
                {
                    if (Event.current.commandName != "SelectAll")
                        break;
                    
                    Event.current.Use();
                    break;
                }
                case EventType.ExecuteCommand:
                {
                    if (Event.current.commandName != "SelectAll")
                        break;
                    
                    var transforms = new List<Object>();
                    for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                    {
                        var scene = SceneManager.GetSceneAt(sceneIndex);
                        if (!scene.isLoaded)
                            continue;
                        foreach (var gameObject in scene.GetRootGameObjects())
                        {
                            foreach (var transform in gameObject.GetComponentsInChildren<Transform>())
                            {
                                if ((transform.hideFlags & (HideFlags.NotEditable | HideFlags.HideInHierarchy)) == (HideFlags.NotEditable | HideFlags.HideInHierarchy))
                                    continue;
                                transforms.Add(transform.gameObject);
                            }
                        }
                    }

                    var foundObjects = transforms.ToArray();
                        
                    RemoveGeneratedMeshesFromArray(ref foundObjects);

                    Selection.objects = foundObjects;

                    Event.current.Use();
                    break;
                }
            }
        }
    }
}