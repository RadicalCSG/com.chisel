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

    // TODO: rewrite
    internal static class ChiselRectSelection
    {
        public static bool  Valid			{ get { return reflectionSucceeded; } }
        public static int   RectSelectionID { get; private set; }
        
        static object rectSelection;
        static SceneView sceneView;
        public static SceneView SceneView
        {
            get
            {
                return sceneView;
            }
            set
            {
                if (sceneView == value)
                    return;
                sceneView = value;
                rectSelection = rectSelectionField.GetValue(sceneView);
                RectSelectionID = (int)rectSelectionIDField.GetValue(rectSelection);
            }
        }

        public static bool      RectSelecting       { get { return RectSelectionID != 0 && (GUIUtility.hotControl == RectSelectionID || HandleUtility.nearestControl == RectSelectionID); } }
        public static bool      IsNearestControl    { get { return (bool)isNearestControlField.GetValue(rectSelection); } }
        public static Vector2	SelectStartPoint	{ get { return (Vector2)selectStartPointField.GetValue(rectSelection); } }
        public static Vector2	SelectMousePoint	{ get { return (Vector2)selectMousePointField.GetValue(rectSelection); } }
        public static Object[]	SelectionStart		{ get { return (Object[])selectionStartField.GetValue(rectSelection); } set { selectionStartField.SetValue(rectSelection, value); } }
        public static Object[]  CurrentSelection	{ get { return (Object[])currentSelectionField.GetValue(rectSelection); } set { currentSelectionField.SetValue(rectSelection, value); } }
        public static Dictionary<GameObject, bool>	LastSelection { get { return (Dictionary<GameObject, bool>)lastSelectionField.GetValue(rectSelection); } }

        public static void UpdateSelection(Object[] existingSelection, Object[] newObjects, SelectionType type)
        {
            object selectionType;
            switch (type)
            {
                default:						selectionType = selectionTypeNormal; break;
                case SelectionType.Additive:	selectionType = selectionTypeAdditive; break;
                case SelectionType.Subtractive:	selectionType = selectionTypeSubtractive; break;
            }

            updateSelectionMethod.Invoke(rectSelection,
                new object[]
                {
                    existingSelection,
                    newObjects,
                    selectionType,
                    RectSelecting
                });
        }

        static readonly Type unityRectSelectionType;
        static readonly Type unityEnumSelectionType;

        static readonly object selectionTypeAdditive;
        static readonly object selectionTypeSubtractive;
        static readonly object selectionTypeNormal;
            
        static readonly FieldInfo rectSelectionField;
        static readonly FieldInfo selectStartPointField;
        static readonly FieldInfo isNearestControlField;
        static readonly FieldInfo selectMousePointField;
        static readonly FieldInfo selectionStartField;
        static readonly FieldInfo lastSelectionField;
        static readonly FieldInfo currentSelectionField;
        
        static readonly FieldInfo rectSelectionIDField;

        static readonly MethodInfo updateSelectionMethod;
        
        static readonly bool reflectionSucceeded = false;

        static ChiselRectSelection()
        {
            reflectionSucceeded	= false;

            unityRectSelectionType		= ReflectionExtensions.GetTypeByName("UnityEditor.RectSelection");
            if (unityRectSelectionType == null)
			{
				Debug.LogError("Could not find UnityEditor.RectSelection");
				return;
			}

			unityEnumSelectionType 		= ReflectionExtensions.GetTypeByName("UnityEditor.RectSelection+SelectionType");
            if (unityEnumSelectionType == null)
			{
				Debug.LogError("Could not find UnityEditor.RectSelection+SelectionType");
				return;
			}

			rectSelectionField			= typeof(SceneView).GetField("m_RectSelection",			BindingFlags.NonPublic | BindingFlags.Instance);
            if (rectSelectionField == null)
            {
                Debug.LogError("Could not find SceneView.m_RectSelection");
                return;
            }

            rectSelectionIDField        = unityRectSelectionType.GetField("k_RectSelectionID",  BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (rectSelectionIDField == null)
			{
				Debug.LogError("Could not find UnityEditor.RectSelection.k_RectSelectionID");
				return;
			}

			RectSelectionID       = 0;
            selectStartPointField = unityRectSelectionType.GetField("m_StartPoint",	        BindingFlags.NonPublic | BindingFlags.Instance);
            isNearestControlField = unityRectSelectionType.GetField("m_IsNearestControl",	BindingFlags.NonPublic | BindingFlags.Instance);
            selectionStartField	  = unityRectSelectionType.GetField("m_SelectionStart",	    BindingFlags.NonPublic | BindingFlags.Instance);
            lastSelectionField	  = unityRectSelectionType.GetField("m_LastSelection",	    BindingFlags.NonPublic | BindingFlags.Instance);
            currentSelectionField = unityRectSelectionType.GetField("m_CurrentSelection",	BindingFlags.NonPublic | BindingFlags.Instance);
            selectMousePointField = unityRectSelectionType.GetField("m_SelectMousePoint",	BindingFlags.NonPublic | BindingFlags.Instance);
            updateSelectionMethod = ReflectionExtensions.GetStaticMethod(unityRectSelectionType, "UpdateSelection", new Type[] {
                                                                                typeof(Object[]),
                                                                                typeof(Object[]),
                                                                                unityEnumSelectionType,
                                                                                typeof(bool)
                                                                            });

			selectionTypeAdditive    = Enum.Parse(unityEnumSelectionType, "Additive");
            selectionTypeSubtractive = Enum.Parse(unityEnumSelectionType, "Subtractive");
            selectionTypeNormal		 = Enum.Parse(unityEnumSelectionType, "Normal");
            
            reflectionSucceeded = selectStartPointField	   != null &&
                                  selectionStartField	   != null &&
                                  lastSelectionField	   != null &&
                                  currentSelectionField	   != null &&
                                  selectMousePointField	   != null &&
                                  updateSelectionMethod	   != null &&

                                  selectionTypeAdditive	   != null &&
                                  selectionTypeSubtractive != null &&
                                  selectionTypeNormal	   != null;


            if (!reflectionSucceeded)
            {
                Debug.LogError("Could not initialize rect selection properly");

				Debug.LogError($"selectStartPointField {selectStartPointField}");
				Debug.LogError($"selectionStartField {selectionStartField}");
				Debug.LogError($"currentSelectionField {currentSelectionField}");
				Debug.LogError($"selectMousePointField {selectMousePointField}");

				Debug.LogError($"updateSelectionMethod {updateSelectionMethod}");
				Debug.LogError($"selectionTypeAdditive {selectionTypeAdditive}");
				Debug.LogError($"selectionTypeSubtractive {selectionTypeSubtractive}");
				Debug.LogError($"selectionTypeNormal {selectionTypeNormal}");
			}
		}
    }

    // TODO: clean up, rename
    internal static class ChiselRectSelectionManager
    {
        static HashSet<CSGTreeNode> rectFoundTreeNodes	= new();
        static readonly HashSet<GameObject> rectFoundGameObjects = new();
        static Vector2  prevStartGUIPoint;
        static Vector2  prevMouseGUIPoint;
        static Vector2  prevStartScreenPoint;
        static Vector2  prevMouseScreenPoint;


        static bool     rectClickDown       = false;
        static bool     mouseDragged        = false;
        static Vector2  clickMousePosition  = Vector2.zero;
        
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
                prevStartGUIPoint = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                prevMouseGUIPoint = prevStartGUIPoint;
                prevStartScreenPoint = Vector2.zero;
                prevMouseScreenPoint = Vector2.zero;
                rectFoundGameObjects.Clear();
                rectFoundTreeNodes.Clear();
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
                    if (prevStartGUIPoint != selectStartPoint)
                    {
                        prevStartGUIPoint		= selectStartPoint;
                        prevStartScreenPoint    = selectStartPoint;
                        needUpdate = true;
                    }
                    if (prevMouseGUIPoint != selectMousePoint)
                    {
                        prevMouseGUIPoint	 = selectMousePoint;
                        prevMouseScreenPoint = selectMousePoint;
                        needUpdate = true;
                    }
                    if (needUpdate)
                    {
                        var rect = ChiselCameraUtility.PointsToRect(prevStartScreenPoint, prevMouseScreenPoint);
                        if (rect.width > 3 && 
                            rect.height > 3)
                        { 
                            var frustum         = ChiselCameraUtility.GetCameraSubFrustum(Camera.current, rect);
                            var selectionType   = GetCurrentSelectionType();

                            if (selectionType == SelectionType.Replace)
                            {
                                rectFoundTreeNodes.Clear();
                                rectFoundGameObjects.Clear();
                            }

                            // TODO: modify this depending on debug rendermode
                            SurfaceDestinationFlags visibleLayerFlags = SurfaceDestinationFlags.Renderable;

                            // Find all the brushes (and it's gameObjects) that are inside the frustum
                            if (!ChiselSceneQuery.GetNodesInFrustum(frustum, UnityEditor.Tools.visibleLayers, visibleLayerFlags, ref rectFoundTreeNodes))
                            {
                                if (rectFoundGameObjects != null &&
                                    rectFoundGameObjects.Count > 0)
                                {
                                    rectFoundTreeNodes.Clear();
                                    rectFoundGameObjects.Clear();
                                    modified = true;
                                }
                            } else
                                modified = true;
            
                            foreach(var treeNode in rectFoundTreeNodes)
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
                                rectFoundGameObjects.Add(gameObject);
                            }
                        }
                    }

                    Object[] currentSelection = null;
                    var originalLastSelection	= ChiselRectSelection.LastSelection;
                    var originalSelectionStart	= ChiselRectSelection.SelectionStart;

                    if (originalLastSelection != null)
                    {
                        if (modified &&
                            rectFoundGameObjects != null &&
                            rectFoundGameObjects.Count > 0)
                        {
                            foreach (var obj in rectFoundGameObjects)
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
                prevStartGUIPoint = Vector2.zero;
                prevMouseGUIPoint = Vector2.zero;
                rectFoundGameObjects.Clear();
                rectFoundTreeNodes.Clear();
            }

            var evt = Event.current;
            switch (typeForControl)
            {
                case EventType.MouseDown:
                {
                    rectClickDown = (Event.current.button == 0 && areRectSelecting);
                    clickMousePosition = Event.current.mousePosition;
                    mouseDragged = false;
                    break;
                }
                case EventType.MouseUp:
                {
                    if (!mouseDragged)
                    {
                        if ((UnityEditor.HandleUtility.nearestControl != 0 || evt.button != 0) &&
                            (GUIUtility.keyboardControl != 0 || evt.button != 2))
                            break;
                        Event.current.Use();
                    }
                    rectClickDown = false;
                    mouseDragged = false;
                    break;
                }
                case EventType.MouseMove:
                {
                    rectClickDown = false;
                    mouseDragged = false;
                    break;
                }
                case EventType.MouseDrag:
                {
                    mouseDragged = true;
                    break;
                }
                case EventType.Used:
                {
                    if (!mouseDragged)
                    {
                        var delta = Event.current.mousePosition - clickMousePosition;
                        if (Mathf.Abs(delta.x) > 4 || Mathf.Abs(delta.y) > 4) { mouseDragged = true; }
                    }
                    if (mouseDragged || !rectClickDown || Event.current.button != 0 || ChiselRectSelection.RectSelecting) { rectClickDown = false; break; }

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