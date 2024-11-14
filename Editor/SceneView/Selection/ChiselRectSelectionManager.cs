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

        static ChiselRectSelection()
        {
            s_ReflectionSucceeded	= false;

            s_UnityRectSelectionType		= ReflectionExtensions.GetTypeByName("UnityEditor.RectSelection");
            if (s_UnityRectSelectionType == null)
			{
				Debug.LogError("Could not find UnityEditor.RectSelection");
				return;
			}

			s_UnityEnumSelectionType 		= ReflectionExtensions.GetTypeByName("UnityEditor.RectSelection+SelectionType");
            if (s_UnityEnumSelectionType == null)
			{
				Debug.LogError("Could not find UnityEditor.RectSelection+SelectionType");
				return;
			}

			s_RectSelectionField			= typeof(SceneView).GetField("m_RectSelection",			BindingFlags.NonPublic | BindingFlags.Instance);
            if (s_RectSelectionField == null)
            {
                Debug.LogError("Could not find SceneView.m_RectSelection");
                return;
            }

            s_RectSelectionIDField        = s_UnityRectSelectionType.GetField("k_RectSelectionID",  BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (s_RectSelectionIDField == null)
			{
				Debug.LogError("Could not find UnityEditor.RectSelection.k_RectSelectionID");
				return;
			}

			RectSelectionID       = 0;
            s_SelectStartPointField = s_UnityRectSelectionType.GetField("m_StartPoint",	        BindingFlags.NonPublic | BindingFlags.Instance);
            s_IsNearestControlField = s_UnityRectSelectionType.GetField("m_IsNearestControl",	BindingFlags.NonPublic | BindingFlags.Instance);
            s_SelectionStartField	  = s_UnityRectSelectionType.GetField("m_SelectionStart",	    BindingFlags.NonPublic | BindingFlags.Instance);
            s_LastSelectionField	  = s_UnityRectSelectionType.GetField("m_LastSelection",	    BindingFlags.NonPublic | BindingFlags.Instance);
            s_CurrentSelectionField = s_UnityRectSelectionType.GetField("m_CurrentSelection",	BindingFlags.NonPublic | BindingFlags.Instance);
            s_SelectMousePointField = s_UnityRectSelectionType.GetField("m_SelectMousePoint",	BindingFlags.NonPublic | BindingFlags.Instance);
            s_UpdateSelectionMethod = ReflectionExtensions.GetStaticMethod(s_UnityRectSelectionType, "UpdateSelection", new Type[] {
                                                                                typeof(Object[]),
                                                                                typeof(Object[]),
                                                                                s_UnityEnumSelectionType,
                                                                                typeof(bool)
                                                                            });

			s_SelectionTypeAdditive    = Enum.Parse(s_UnityEnumSelectionType, "Additive");
            s_SelectionTypeSubtractive = Enum.Parse(s_UnityEnumSelectionType, "Subtractive");
            s_SelectionTypeNormal		 = Enum.Parse(s_UnityEnumSelectionType, "Normal");
            
            s_ReflectionSucceeded = s_SelectStartPointField	   != null &&
                                  s_SelectionStartField	   != null &&
                                  s_LastSelectionField	   != null &&
                                  s_CurrentSelectionField	   != null &&
                                  s_SelectMousePointField	   != null &&
                                  s_UpdateSelectionMethod	   != null &&

                                  s_SelectionTypeAdditive	   != null &&
                                  s_SelectionTypeSubtractive != null &&
                                  s_SelectionTypeNormal	   != null;


            if (!s_ReflectionSucceeded)
            {
                Debug.LogError("Could not initialize rect selection properly");

				Debug.LogError($"selectStartPointField {s_SelectStartPointField}");
				Debug.LogError($"selectionStartField {s_SelectionStartField}");
				Debug.LogError($"currentSelectionField {s_CurrentSelectionField}");
				Debug.LogError($"selectMousePointField {s_SelectMousePointField}");

				Debug.LogError($"updateSelectionMethod {s_UpdateSelectionMethod}");
				Debug.LogError($"selectionTypeAdditive {s_SelectionTypeAdditive}");
				Debug.LogError($"selectionTypeSubtractive {s_SelectionTypeSubtractive}");
				Debug.LogError($"selectionTypeNormal {s_SelectionTypeNormal}");
			}
		}
    }

    // TODO: clean up, rename
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
                                var nodeComponent = Resources.InstanceIDToObject(treeNode.InstanceID) as ChiselNodeComponent;
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