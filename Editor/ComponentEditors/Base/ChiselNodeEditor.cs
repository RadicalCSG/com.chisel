using System;
using System.Collections.Generic;
using Chisel.Core;
using Chisel.Components;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Pool;

namespace Chisel.Editors
{
    public abstract class ChiselNodeEditorBase : Editor
	{
		protected const string kGameObjectMenuPath              = "GameObject/Chisel/";
		protected const int    kGameObjectMenuPriority          = 1;

		protected const string kGameObjectMenuModelPath         = kGameObjectMenuPath + "Create ";
		protected const int    kGameObjectMenuModelPriority     = -5;
		protected const string kGameObjectMenuCompositePath     = kGameObjectMenuPath + "Create Composites/";
		protected const int    kGameObjectMenuCompositePriority = -4;
		protected const string kGameObjectMenuNodePath          = kGameObjectMenuPath + "Create Generators/";
		protected const int    kGameObjectMenuNodePriority      = -3;
		protected const string kGameObjectMenuOperationPath     = kGameObjectMenuPath + "Set Operation To/";
		protected const int    kGameObjectMenuOperationPriority = -1;

		const string kDefaultCompositeName = "Composite";

        // Ugly hack around stupid Unity issue
        static bool s_DelayedUndoAllChanges = false;
        public static void UndoAllChanges()
        {
            s_DelayedUndoAllChanges = true;
        }

        public static void HandleCancelEvent()
        {
            if (s_DelayedUndoAllChanges)
            {
                s_DelayedUndoAllChanges = false;
                Undo.RevertAllInCurrentGroup();
            }
        }

		static void MenuSetOperationTo(MenuCommand menuCommand, CSGOperationType operationType)
        {
            var context     = (menuCommand.context as GameObject);
            var gameObject  = (context == null) ? Selection.activeGameObject : context;
            if (!gameObject)
                return;

            var generator = gameObject.GetComponent<ChiselGeneratorComponent>();
            if (generator && generator.Operation != operationType)
            {
                Undo.RecordObject(generator, "Modified Operation");
                generator.Operation = operationType;
            }
            var composite = gameObject.GetComponent<ChiselCompositeComponent>();
            if (composite && composite.Operation != operationType)
            {
                Undo.RecordObject(generator, "Modified Operation");
                composite.Operation = operationType;
            }
        }

        static bool MenuValidateSetOperationTo(MenuCommand menuCommand)
        {
            var context     = (menuCommand.context as GameObject);
            var gameObject  = (context == null) ? Selection.activeGameObject : context;
            if (!gameObject)
                return false;

            return gameObject.GetComponent<ChiselGeneratorComponent>() ||
                   gameObject.GetComponent<ChiselCompositeComponent>();
        }


		[MenuItem(kGameObjectMenuOperationPath + nameof(CSGOperationType.Additive), false, kGameObjectMenuOperationPriority)] protected static void SetAdditiveOperation(MenuCommand menuCommand) { MenuSetOperationTo(menuCommand, CSGOperationType.Additive); }
		[MenuItem(kGameObjectMenuOperationPath + nameof(CSGOperationType.Additive), true)] protected static bool ValidateAdditiveOperation(MenuCommand menuCommand) { return MenuValidateSetOperationTo(menuCommand); }
		[MenuItem(kGameObjectMenuOperationPath + nameof(CSGOperationType.Subtractive), false, kGameObjectMenuOperationPriority)] protected static void SetSubtractiveOperation(MenuCommand menuCommand) { MenuSetOperationTo(menuCommand, CSGOperationType.Subtractive); }
		[MenuItem(kGameObjectMenuOperationPath + nameof(CSGOperationType.Subtractive), true)] protected static bool ValidateSubtractiveOperation(MenuCommand menuCommand) { return MenuValidateSetOperationTo(menuCommand); }
		[MenuItem(kGameObjectMenuOperationPath + nameof(CSGOperationType.Intersecting), false, kGameObjectMenuOperationPriority)] protected static void SetIntersectingOperation(MenuCommand menuCommand) { MenuSetOperationTo(menuCommand, CSGOperationType.Intersecting); }
		[MenuItem(kGameObjectMenuOperationPath + nameof(CSGOperationType.Intersecting), true)] protected static bool ValidateIntersectingOperation(MenuCommand menuCommand) { return MenuValidateSetOperationTo(menuCommand); }

		[MenuItem("CONTEXT/" + kGameObjectMenuOperationPath + nameof(CSGOperationType.Additive), false, kGameObjectMenuOperationPriority)] protected static void SetAdditiveOperationContext(MenuCommand menuCommand) { MenuSetOperationTo(menuCommand, CSGOperationType.Additive); }
		[MenuItem("CONTEXT/" + kGameObjectMenuOperationPath + nameof(CSGOperationType.Additive), true)] protected static bool ValidateAdditiveOperationContext(MenuCommand menuCommand) { return MenuValidateSetOperationTo(menuCommand); }
		[MenuItem("CONTEXT/" + kGameObjectMenuOperationPath + nameof(CSGOperationType.Subtractive), false, kGameObjectMenuOperationPriority)] protected static void SetSubtractiveOperationContext(MenuCommand menuCommand) { MenuSetOperationTo(menuCommand, CSGOperationType.Subtractive); }
		[MenuItem("CONTEXT/" + kGameObjectMenuOperationPath + nameof(CSGOperationType.Subtractive), true)] protected static bool ValidateSubtractiveOperationContext(MenuCommand menuCommand) { return MenuValidateSetOperationTo(menuCommand); }
		[MenuItem("CONTEXT/" + kGameObjectMenuOperationPath + nameof(CSGOperationType.Intersecting), false, kGameObjectMenuOperationPriority)] protected static void SetIntersectingOperationContext(MenuCommand menuCommand) { MenuSetOperationTo(menuCommand, CSGOperationType.Intersecting); }
		[MenuItem("CONTEXT/" + kGameObjectMenuOperationPath + nameof(CSGOperationType.Intersecting), true)] protected static bool ValidateIntersectingOperationContext(MenuCommand menuCommand) { return MenuValidateSetOperationTo(menuCommand); }



		static void MenuCreateComposite(MenuCommand menuCommand, CSGOperationType operationType)
		{
            // TODO: if we have multiple selected gameobjects, then those as children

			GameObject go = new($"{operationType} Composite");
			GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
			var generator = go.GetComponent<ChiselGeneratorComponent>();
			generator.Operation = operationType;
			Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
			Selection.activeObject = go;
		}

		[MenuItem(kGameObjectMenuCompositePath + nameof(CSGOperationType.Additive), false, kGameObjectMenuCompositePriority)] protected static void CreateAdditiveComposite(MenuCommand menuCommand) { MenuCreateComposite(menuCommand, CSGOperationType.Additive); }
        [MenuItem(kGameObjectMenuCompositePath + nameof(CSGOperationType.Subtractive), false, kGameObjectMenuCompositePriority)] protected static void CreatSubtractiveComposite(MenuCommand menuCommand) { MenuCreateComposite(menuCommand, CSGOperationType.Subtractive); }
        [MenuItem(kGameObjectMenuCompositePath + nameof(CSGOperationType.Intersecting), false, kGameObjectMenuCompositePriority)] protected static void CreateIntersectingComposite(MenuCommand menuCommand) { MenuCreateComposite(menuCommand, CSGOperationType.Intersecting); }


		protected static bool ValidateEncapsulateInCompositeInternal(GameObject[] gameObjects)
        {
            for (int i = 0; i < gameObjects.Length; i++)
            {
                if (gameObjects[i].GetComponent<ChiselGeneratorComponent>() ||
                    gameObjects[i].GetComponent<ChiselCompositeComponent>())
                    continue;
                return false;
            }
            return true;
        }



        [MenuItem(ChiselNodeEditorBase.kGameObjectMenuPath + "Encapsulate selection in Composite", false, kGameObjectMenuPriority)]
        protected static void EncapsulateInComposite(MenuCommand menuCommand)
        {
            var gameObjects = Selection.gameObjects;

            if (gameObjects == null || gameObjects.Length == 0 ||                
                // Workaround for "Unity calls each object in selection individually in menu item"
                menuCommand.context != gameObjects[0])
                return;

            if (!ValidateEncapsulateInCompositeInternal(gameObjects))
                return;

            // TODO: sort gameObjects by their siblingIndex / hierarchy position

            var childTransform      = gameObjects[0].transform;
            var childSiblingIndex   = childTransform.GetSiblingIndex();
            var childParent         = childTransform.parent;

            var composite           = ChiselComponentFactory.Create<ChiselCompositeComponent>(kDefaultCompositeName, childParent);
            var compositeGameObject = composite.gameObject;
            var compositeTransform  = composite.transform;
            compositeTransform.SetSiblingIndex(childSiblingIndex);
            Undo.RegisterCreatedObjectUndo(compositeGameObject, "Create " + compositeGameObject.name);

            for (int i = 0; i < gameObjects.Length; i++)
                Undo.SetTransformParent(gameObjects[i].transform, compositeTransform, "Moved GameObject under Composite");

            Selection.activeObject = compositeGameObject;

            // This forces the composite to be opened when we create it
            EditorGUIUtility.PingObject(compositeGameObject);
            for (int i = 0; i < gameObjects.Length; i++)
                EditorGUIUtility.PingObject(gameObjects[i]);
        }

        [MenuItem(ChiselNodeEditorBase.kGameObjectMenuPath + "Encapsulate selection in Composite", true)]
        protected static bool ValidateEncapsulateInComposite(MenuCommand menuCommand)
        {
            var gameObjects = Selection.gameObjects;
            if (gameObjects == null ||
                gameObjects.Length == 0)
                return false;

            return ValidateEncapsulateInCompositeInternal(gameObjects);
        }

        static bool s_InSceneSettingsContext = false;
		public static bool InSceneSettingsContext
        {
            get { return s_InSceneSettingsContext; }
			internal set { s_InSceneSettingsContext = value; }
		}
	}

    public abstract class ChiselNodeEditor<T> : ChiselNodeEditorBase
        where T : ChiselNodeComponent
    {
        public virtual Bounds OnGetFrameBounds() { return CalculateBounds(targets); }
        public virtual bool HasFrameBounds() { if (!target) return false; return true; }

        public static Bounds CalculateBounds(UnityEngine.Object[] targets)
        {
            var bounds = new Bounds();
            foreach (var target in targets)
            {
                var node = target as ChiselNodeComponent;
                if (!node)
                    continue;

                node.hierarchyItem.EncapsulateBounds(ref bounds);
            }
            return bounds;
        }

        public static Vector3[] CalculateGridBounds(ChiselNodeComponent[] targetNodes)
        {
            if (targetNodes == null)
                return new[] { Vector3.zero };

            var worldToGridSpace = Grid.ActiveGrid.WorldToGridSpace;
            var gridToWorldSpace = Grid.ActiveGrid.GridToWorldSpace;
            var bounds = new Bounds();
            foreach (var node in targetNodes)
            {
                if (!node)
                    continue;

                node.hierarchyItem.EncapsulateBounds(ref bounds, worldToGridSpace);
            }

            if (bounds.extents.sqrMagnitude == 0)
                return new[] { Vector3.zero };

            var min = bounds.min;
            var max = bounds.max;
            var points = new[]
            {
                Vector3.zero,
                gridToWorldSpace.MultiplyPoint(new Vector3(min.x, min.y, min.z)),
                gridToWorldSpace.MultiplyPoint(new Vector3(max.x, min.y, min.z)),
                gridToWorldSpace.MultiplyPoint(new Vector3(min.x, max.y, min.z)),
                gridToWorldSpace.MultiplyPoint(new Vector3(max.x, max.y, min.z)),
                gridToWorldSpace.MultiplyPoint(new Vector3(min.x, min.y, max.z)),
                gridToWorldSpace.MultiplyPoint(new Vector3(max.x, min.y, max.z)),
                gridToWorldSpace.MultiplyPoint(new Vector3(min.x, max.y, max.z)),
                gridToWorldSpace.MultiplyPoint(new Vector3(max.x, max.y, max.z)),
            };
            
            return points;
        }

        static Vector3[] s_SelectionBoundPoints;
        static bool      s_GridBoundsDirty = true;

        void ResetGridBounds()
        {
            s_GridBoundsDirty = true;
        }

        void UpdateGridBounds()
        {
            if (!s_GridBoundsDirty)
                return;
            s_SelectionBoundPoints = CalculateGridBounds(targetNodes);
            s_GridBoundsDirty = false;
        }

        protected void OnShapeChanged(T generator)
        {
            if (generator != null)
                generator.UpdateGeneratorNodes();
            ResetGridBounds();
        }

        public static void ForceUpdateNodeContents(SerializedObject serializedObject)
        {
            serializedObject.ApplyModifiedProperties();
            foreach (var target in serializedObject.targetObjects)
            {
                var node = target as ChiselModelComponent;
                if (!node)
                    continue;
#if UNITY_EDITOR
                node.RenderSettings.SetDirty();
#endif
                ChiselNodeHierarchyManager.NotifyContentsModified(node);
                node.SetDirty();
            }
        }

        // This method is used by Component specific classes (e.g. ChiselBoxEditor) 
        // to create a menu Item that creates a gameObject with a specific component (e.g. ChiselBox)
        // Since MenuItems can only be created with attributes, and strings must be constant, 
        // we can only do this from place where we specifically know which component the menu is for.
        protected static void CreateAsGameObjectMenuCommand(MenuCommand menuCommand, string name)
        {
            T component;
            // If we use the command object on a gameobject in the hierarchy, choose that gameobject
            // Otherwise: choose the activeModel (if available)
            var context             = (menuCommand.context as GameObject);
            var typeIsModel         = (typeof(T) == typeof(ChiselModelComponent));
			var defaultParent       = typeIsModel ? null : ((ChiselModelManager.Instance.ActiveModel != null) ? ChiselModelManager.Instance.ActiveModel.gameObject : null);
            var parentGameObject    = (context != null) ? context : defaultParent;
            var parentTransform     = (parentGameObject == null) ? null : parentGameObject.transform;

            // If we used the command object on a generator, choose it's parent to prevent us from 
            // adding a generator as a child to a generator
            if (parentTransform &&
                parentTransform.GetComponent<ChiselGeneratorComponent>())
            {
                parentTransform = parentTransform.parent;
                parentGameObject = (parentTransform == null) ? null : parentTransform.gameObject;
            }

            // Create the gameobject
            if (parentTransform)
                component = ChiselComponentFactory.Create<T>(name, parentTransform);
            else
                component = ChiselComponentFactory.Create<T>(name);

            var gameObject  = component.gameObject;
            GameObjectUtility.SetParentAndAlign(gameObject, parentGameObject);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + gameObject.name);

            // Find the appropriate model to make active after we created the generator
            ChiselModelComponent model;
            if (!typeIsModel)
            {
                model = gameObject.GetComponentInParent<ChiselModelComponent>();
                // If we don't have a parent model, create one and put the generator underneath it
                if (!model)
                {
                    model = ChiselModelManager.Instance.CreateNewModel(gameObject.transform.parent);
                    
                    // Make sure we create the model at the exact same location as the generator
                    var modelGameObject     = model.gameObject;
                    var modelTransform      = model.transform;
                    var childSiblingIndex   = modelTransform.GetSiblingIndex();
                    modelTransform.SetSiblingIndex(childSiblingIndex);

                    Undo.RegisterCreatedObjectUndo(modelGameObject, "Create " + modelGameObject.name);
                    MoveTargetsUnderModel(new[] { component }, model);
                }
            } else
                model = component as ChiselModelComponent;

            // Set the active model before we select the gameobject, otherwise we'll be selecting the model instead
            ChiselModelManager.Instance.ActiveModel = model;
            Selection.activeObject = gameObject;
        }

        public static bool IsPartOfDefaultModel(UnityEngine.Object[] targetObjects)
        {
            if (targetObjects == null)
                return false;

            for (int i = 0; i < targetObjects.Length; i++)
            {
                ChiselNodeComponent node = targetObjects[i] as ChiselNodeComponent;
                if (Equals(node, null))
                {
                    var gameObject = targetObjects[i] as GameObject;
                    if (gameObject)
                        node = gameObject.GetComponent<ChiselNodeComponent>();
                }
                if (node)
                {
                    if (ChiselModelManager.Instance.IsDefaultModel(node.hierarchyItem.Model))
                        return true;
                }
            }
            return false;
        }


		readonly static string     kDefaultModelContents = "This node is not a child of a model, " +
                                                           "It is recommended that you explicitly add this node to a model.";
		readonly static GUIContent kCreateAndAddToModel  = new("Insert into new model");
		readonly static GUIContent kAddToActiveModel     = new("Move to active model");

        static void MoveTargetsUnderModel(UnityEngine.Object[] targetObjects, ChiselModelComponent model)
        {
            var modelTransform  = model.transform;
            var modelGameObject = model.gameObject;
            for (int t = 0; t < targetObjects.Length; t++)
            {
                var targetComponent = (targetObjects[t] as MonoBehaviour);
                var targetTransform = targetComponent.transform;
                Undo.SetTransformParent(targetTransform, modelTransform, "Moved GameObject under Model");
            }

            Selection.activeObject = modelGameObject;
            ChiselModelManager.Instance.ActiveModel = model;

            // This forces the model to be opened when we create it
            EditorGUIUtility.PingObject(modelGameObject);
            for (int t = 0; t < targetObjects.Length; t++)
                EditorGUIUtility.PingObject(targetObjects[t]);
        }

        public static void ShowDefaultModelMessage(UnityEngine.Object[] targetObjects)
        {
            if (!IsPartOfDefaultModel(targetObjects))
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox(kDefaultModelContents, MessageType.Warning);
            if (GUILayout.Button(kCreateAndAddToModel))
            {
                // TODO: sort gameObjects by their siblingIndex / hierarchy position, pick top one

                var childTransform      = (targetObjects[0] as MonoBehaviour).transform;
                var childSiblingIndex   = childTransform.GetSiblingIndex();
                var childParent         = childTransform.parent;

                var model               = ChiselModelManager.Instance.CreateNewModel(childParent);
                var modelGameObject     = model.gameObject;
                var modelTransform      = model.transform;
                modelTransform.SetSiblingIndex(childSiblingIndex);
                Undo.RegisterCreatedObjectUndo(modelGameObject, "Create " + modelGameObject.name);
                MoveTargetsUnderModel(targetObjects, model);
            }
            if (ChiselModelManager.Instance.ActiveModel && GUILayout.Button(kAddToActiveModel))
            {
                // TODO: sort gameObjects by their siblingIndex / hierarchy position

                MoveTargetsUnderModel(targetObjects, ChiselModelManager.Instance.ActiveModel);
            }
            EditorGUILayout.EndVertical();
        }

        public void CheckForTransformationChanges(SerializedObject serializedObject)
        {
            if (Event.current.type == EventType.Layout)
            {
                var modifiedNodes = HashSetPool<ChiselNodeComponent>.Get();
                try
                {
                    modifiedNodes.Clear();
                    foreach (var target in serializedObject.targetObjects)
                    {
                        var node = target as ChiselNodeComponent;
                        if (!node)
                            continue;

                        var transform = node.transform;

                        // TODO: probably not a good idea to use these matrices for this, since it calculates this all the way up the transformation tree
                        var curLocalToWorldMatrix = transform.localToWorldMatrix;
                        var oldLocalToWorldMatrix = node.hierarchyItem.LocalToWorldMatrix;
                        if (curLocalToWorldMatrix.m00 != oldLocalToWorldMatrix.m00 ||
                            curLocalToWorldMatrix.m01 != oldLocalToWorldMatrix.m01 ||
                            curLocalToWorldMatrix.m02 != oldLocalToWorldMatrix.m02 ||
                            curLocalToWorldMatrix.m03 != oldLocalToWorldMatrix.m03 ||

                            curLocalToWorldMatrix.m10 != oldLocalToWorldMatrix.m10 ||
                            curLocalToWorldMatrix.m11 != oldLocalToWorldMatrix.m11 ||
                            curLocalToWorldMatrix.m12 != oldLocalToWorldMatrix.m12 ||
                            curLocalToWorldMatrix.m13 != oldLocalToWorldMatrix.m13 ||

                            curLocalToWorldMatrix.m20 != oldLocalToWorldMatrix.m20 ||
                            curLocalToWorldMatrix.m21 != oldLocalToWorldMatrix.m21 ||
                            curLocalToWorldMatrix.m22 != oldLocalToWorldMatrix.m22 ||
                            curLocalToWorldMatrix.m23 != oldLocalToWorldMatrix.m23 //||

                            //curLocalToWorldMatrix.m30 != oldLocalToWorldMatrix.m30 ||
                            //curLocalToWorldMatrix.m31 != oldLocalToWorldMatrix.m31 ||
                            //curLocalToWorldMatrix.m32 != oldLocalToWorldMatrix.m32 ||
                            //curLocalToWorldMatrix.m33 != oldLocalToWorldMatrix.m33
                            )
                        {
                            node.hierarchyItem.LocalToWorldMatrix = curLocalToWorldMatrix;
                            node.hierarchyItem.WorldToLocalMatrix = transform.worldToLocalMatrix;
                            modifiedNodes.Add(node);
                        }
                    }
                    if (modifiedNodes.Count > 0)
                    {
                        ChiselNodeHierarchyManager.NotifyTransformationChanged(modifiedNodes);
                        ResetGridBounds(); // TODO: should only do this when rotating, not when moving
                    }
                }
                finally
                {
                    HashSetPool<ChiselNodeComponent>.Release(modifiedNodes);
                }
            }
        }

		readonly static GUIContent kConvertToBrushesContent  = new("Convert to Brushes");
		readonly static GUIContent kConvertToBrushContent    = new("Convert to Brush");

        
        // TODO: put somewhere else
        internal static void ConvertIntoBrushesButton(Rect rect, SerializedObject serializedObject)
        {
            rect.height = 22;
            bool singular = false;
            bool multiple = false;
            foreach (var targetObject in serializedObject.targetObjects)
            {
                var node = targetObject as ChiselGeneratorComponent;
                if (!node)
                    continue;
                var topTreeNode = node.TopTreeNode;
                if (!topTreeNode.Valid)
                    continue;
                var count = topTreeNode.Count;
                singular = (count <= 1) || singular;
                multiple = (count > 1) || multiple;
            }
            if (multiple)
            {
                if (!GUI.Button(rect, kConvertToBrushesContent))
                    return;
            } else
            if (singular)
            {
                if (!GUI.Button(rect, kConvertToBrushContent))
                    return;
            } else
                return;

            bool modified = false;
            foreach (var targetObject in serializedObject.targetObjects)
            {
                var node = targetObject as ChiselGeneratorComponent;
                if (!node)
                    continue;

                modified = ConvertToBrushesButton.ConvertToBrushes(node) || modified;
            }

            if (modified)
                EditorGUIUtility.ExitGUI();
        }

        public void ShowInspectorHeader(SerializedProperty operationProp)
        {
            GUILayout.Space(3);
            const float kBottomPadding = 3;
            var rect = EditorGUILayout.GetControlRect(hasLabel: false, height: EditorGUIUtility.singleLineHeight + kBottomPadding);
            rect.yMax -= kBottomPadding;
            var buttonRect = rect;
            buttonRect.xMax -= ChiselCompositeGUI.GetOperationChoicesInternalWidth(showAuto: false);
            if (typeof(T) != typeof(ChiselBrushComponent))
            {
                ConvertIntoBrushesButton(buttonRect, serializedObject);
                ChiselCompositeGUI.ShowOperationChoicesInternal(rect, operationProp, showLabel: false);
            } else
            {
                ChiselCompositeGUI.ShowOperationChoicesInternal(rect, operationProp, showLabel: true);
            }
        }

        protected abstract void OnEditSettingsGUI(SceneView sceneView);

        ChiselNodeComponent[] targetNodes;


        protected virtual void InitInspector()
        {
            targetNodes = new ChiselNodeComponent[targets.Length];
            for (int i = 0; i < targets.Length; i++)
                targetNodes[i] = targets[i] as ChiselNodeComponent;
            ResetGridBounds();

            Grid.GridModified -= OnGridModified;
            Grid.GridModified += OnGridModified;

            ChiselNodeHierarchyManager.NodeHierarchyModified -= OnNodeHierarchyModified;
            ChiselNodeHierarchyManager.NodeHierarchyModified += OnNodeHierarchyModified;
        }

        protected virtual void ShutdownInspector()
        {
            Grid.GridModified -= OnGridModified;
            ChiselNodeHierarchyManager.NodeHierarchyModified -= OnNodeHierarchyModified;
        }

        private void OnNodeHierarchyModified()
        {
            ResetGridBounds();
            //Repaint();
        }

        private void OnGridModified()
        {
            ResetGridBounds();
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            Profiler.BeginSample("OnInspectorGUI");
            CheckForTransformationChanges(serializedObject);
            ShowDefaultModelMessage(serializedObject.targetObjects);
            Profiler.EndSample();
        }

        static SceneHandles.PositionHandleIDs s_HandleIDs = new SceneHandles.PositionHandleIDs();
        static void OnMoveTool()
        {
            var position = Tools.handlePosition;
            var rotation = Tools.handleRotation;

            EditorGUI.BeginChangeCheck();
            // TODO: make this work with bounds!
            SceneHandles.Initialize(ref s_HandleIDs);
            s_SelectionBoundPoints[0] = position;
            var newPosition = SceneHandles.PositionHandle(ref s_HandleIDs, s_SelectionBoundPoints, position, rotation)[0];
            if (EditorGUI.EndChangeCheck())
            {
                var delta = newPosition - position;
                var transforms = Selection.transforms;
                if (transforms != null && transforms.Length > 0)
                {				
                    MoveTransformsTo(transforms, delta);
                }
            }

            if ((s_HandleIDs.combinedState & ControlState.Hot) == ControlState.Hot)
            {
                var handleSize = UnityEditor.HandleUtility.GetHandleSize(s_HandleIDs.originalPosition);
                SceneHandles.RenderBorderedCircle(s_HandleIDs.originalPosition, handleSize * 0.05f);
                var newHandleSize = UnityEditor.HandleUtility.GetHandleSize(newPosition);
                HandleRendering.DrawCameraAlignedCircle(newPosition, newHandleSize * 0.1f, Color.white, Color.black);
            }
        }

        protected void OnDefaultSceneTools()
        {
            UpdateGridBounds();

            // TODO: somehow make snapped controls work with *any* transform
            switch (Tools.current)
            {
                case Tool.Move:         Tools.hidden = true; OnMoveTool(); break;
                case Tool.Rotate:       Tools.hidden = false; break;// TODO: implement 
                case Tool.Scale:        Tools.hidden = false; break;// TODO: implement
                case Tool.Rect:         Tools.hidden = false; break;// TODO: implement
                case Tool.Transform:    Tools.hidden = false; break;// TODO: implement
                default:
                {
                    Tools.hidden = false;
                    break;
                }
            }
        }


        static void MoveTransformsTo(Transform[] transforms, Vector3 delta)
        {
            Undo.RecordObjects(transforms, "Move Transforms");
            foreach (var transform in transforms)
                transform.position += delta;
        }

        public virtual void OnSceneGUI()
        {
            if (!target)
                return;

            if (Tools.current != Tool.Custom || !ChiselEditGeneratorTool.IsActive())
            {
                OnDefaultSceneTools();
                return;
            }
        }
    }

    public class ChiselDefaultGeneratorDetails : IChiselNodeDetails
    {
        const string kAdditiveIconName          = "csg_" + nameof(CSGOperationType.Additive);
        const string kSubtractiveIconName       = "csg_" + nameof(CSGOperationType.Subtractive);
        const string kIntersectingIconName      = "csg_" + nameof(CSGOperationType.Intersecting);

        public static GUIContent GetHierarchyIcon(CSGOperationType operation, string name)
        {
            return GetIconContent(operation, name)[0];
        }

		readonly static string[] kOperationIcons =
            {
                kAdditiveIconName,
                kSubtractiveIconName,
                kIntersectingIconName
            };

		readonly static string[] kOperationStrings =
            {
                nameof(CSGOperationType.Additive),
                nameof(CSGOperationType.Subtractive),
                nameof(CSGOperationType.Intersecting)
            };

		readonly static Dictionary<string, string>[] s_NamesWithOperations =
            {
                new(),// additive
                new(),
                new()
            };

        static string GetOperationName(int typeIndex, string name)
        {
            var namesWithOperation = s_NamesWithOperations[typeIndex];
            if (!namesWithOperation.TryGetValue(name, out var value))
                return $"{kOperationStrings[typeIndex]} {name}";
            return value;
        }

        public static Texture2D[] GetIcons(CSGOperationType operation, string name)
        {
            int typeIndex = (int)operation;
            if (typeIndex < 0 || typeIndex > s_NamesWithOperations.Length)
                typeIndex = 0;
            return ChiselEditorResources.LoadIconImages(kOperationIcons[typeIndex]);
        }

        public static GUIContent[] GetIconContent(CSGOperationType operation, string name)
        {
            int typeIndex = (int)operation;
            if (typeIndex < 0 || typeIndex > s_NamesWithOperations.Length)
                typeIndex = 0;
            return ChiselEditorResources.GetIconContent(kOperationIcons[typeIndex], GetOperationName(typeIndex, name));
        }
        
        public GUIContent GetHierarchyIconForGenericNode(ChiselNodeComponent node)
        {
            var brushGenerator = node as ChiselGeneratorComponent;
            if (brushGenerator != null)
                return GetHierarchyIcon(brushGenerator.Operation, node.ChiselNodeTypeName);

            var generator = node as ChiselGeneratorComponent;
            if (generator == null)
                return GUIContent.none;

            return GetHierarchyIcon(generator.Operation, node.ChiselNodeTypeName);
        }
    }

    public abstract class ChiselGeneratorEditor<T> : ChiselNodeEditor<T>
        where T : ChiselGeneratorComponent
    {
        protected void ResetDefaultInspector()
        {
            definitionSerializedProperty = null;
            s_Position = Vector2.zero;
            children.Clear();
        }

        // Note: name is the same for every generator, but is hidden inside a generic class, hence the use of ChiselBrushDefinition
        const string kDefinitionName = ChiselNodeGeneratorComponent<ChiselBrushDefinition>.kDefinitionName;
        
        List<SerializedProperty> children = new List<SerializedProperty>();
        SerializedProperty definitionSerializedProperty;
        protected void InitDefaultInspector()
        {
            ResetDefaultInspector();

            var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (iterator.name == kDefinitionName)
                        definitionSerializedProperty = iterator.Copy();
                } while (iterator.NextVisible(false));
            }

            if (definitionSerializedProperty == null)
                return;

            iterator = definitionSerializedProperty.Copy();
            if (iterator.NextVisible(true))
            {
                do
                {
                    children.Add(iterator.Copy());
                } while (iterator.NextVisible(false));
            }
        }

        protected void OnDefaultInspector()
        {
            EditorGUI.BeginChangeCheck();
            {
                for (int i = 0; i < children.Count; i++)
                    EditorGUILayout.PropertyField(children[i], true);
            }
            if (EditorGUI.EndChangeCheck())
            {
                OnTargetModifiedInInspector();
            }
        }

        protected void OnDefaultSettingsGUI(System.Object target, SceneView sceneView)
        {
            InSceneSettingsContext = true;
            try
            {
                if (serializedObject == null ||
                    !serializedObject.targetObject)
                    return;
                serializedObject.Update();
                EditorGUI.BeginChangeCheck();
                {
                    for (int i = 0; i < children.Count; i++)
                    {
                        EditorGUILayout.PropertyField(children[i], true);
                    }
                }
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    OnTargetModifiedInInspector();
                }
            }
            finally
            {
                InSceneSettingsContext = false;
            }
        }


        protected virtual void ResetInspector() { ResetDefaultInspector(); } 
        protected override void InitInspector() { base.InitInspector(); InitDefaultInspector(); }


        static Vector2 s_Position = Vector2.zero;

		protected override void OnEditSettingsGUI(SceneView sceneView)
        {
            if (Tools.current != Tool.Custom)
                return;

			GUILayoutUtility.GetRect(298, 0);

            // TODO: figure out how to make this work with multiple (different) editors when selecting a combination of nodes
            using (var scope = new EditorGUILayout.ScrollViewScope(s_Position, GUILayout.ExpandWidth(true), GUILayout.MaxHeight(150)))
            {
                OnDefaultSettingsGUI(target, sceneView);
                s_Position = scope.scrollPosition;
            }
            ShowInspectorHeader(operationProp);
        }

		readonly static ChiselComponentInspectorMessageHandler s_Warnings = new();

		Vector2 messagesScrollPosition = Vector2.zero;

		protected virtual void OnInspector() 
        {
            s_Warnings.StartWarnings(messagesScrollPosition);
			ChiselMessages.ShowMessages(targets, s_Warnings);
			messagesScrollPosition = s_Warnings.EndWarnings();

			OnDefaultInspector(); 
        }

        protected virtual void OnTargetModifiedInInspector() 
        {
            foreach(var target in targets)
                OnShapeChanged(target as T); 
        }
        protected virtual void OnTargetModifiedInScene(T generator) { OnShapeChanged(generator); }
        protected virtual bool OnGeneratorActive(T generator) { return generator.isActiveAndEnabled; }
        protected virtual void OnGeneratorSelected(T generator) { }
        protected virtual void OnGeneratorDeselected(T generator) { }
        protected abstract void OnScene(IChiselHandles handles, T generator);

        SerializedProperty operationProp;
        void Reset() { operationProp = null; ResetInspector(); }
        
        protected virtual void OnUndoRedoPerformed()
        {
            foreach (var target in targets)
            {
                var node = target as T;
                if (node != null)
                    node.UpdateGeneratorNodes();
            }
        }

        private HashSet<UnityEngine.Object> knownTargets = new HashSet<UnityEngine.Object>();
        private HashSet<UnityEngine.Object> validTargets = new HashSet<UnityEngine.Object>();

        void OnDisable()
        {
            UpdateSelection();
            UnityEditor.Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            PreviewTextureManager.CleanUp();
            Reset();
            Tools.hidden = false;

            ToolManager.activeToolChanged -= OnToolModeChanged;
            ShutdownInspector();
        }

        void OnEnable()
        {
            if (!target)
            {
                Profiler.BeginSample("Reset");
                Reset();
                Profiler.EndSample();
                return;
            }

            Profiler.BeginSample("Setup");
            ToolManager.activeToolChanged -= OnToolModeChanged;
            ToolManager.activeToolChanged += OnToolModeChanged;
            UnityEditor.Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            UnityEditor.Undo.undoRedoPerformed += OnUndoRedoPerformed;

            Profiler.BeginSample("FindProperty");
            operationProp = serializedObject.FindProperty(ChiselGeneratorComponent.kOperationFieldName);
            Profiler.EndSample();
            Profiler.EndSample();

            Profiler.BeginSample("UpdateSelection");
            UpdateSelection();
            Profiler.EndSample();

            Profiler.BeginSample("InitInspector");
            InitInspector();
            Profiler.EndSample();
        }

        void OnToolModeChanged()
        {
            if (Tools.current != Tool.Custom)
            {
                ChiselEditToolBase.ClearLastRememberedType();
                return;
            }
            if (!typeof(ChiselEditToolBase).IsAssignableFrom(ToolManager.activeToolType))
            {
                ChiselEditToolBase.ClearLastRememberedType();
            }
        }
        void UpdateSelection()
		{
            var foundObjects = HashSetPool<System.Object>.Get();
			var removeTargets = HashSetPool<T>.Get();
			var selectedGameObject = HashSetPool<GameObject>.Get();
			try
            {
                selectedGameObject.Clear();
                foreach (var item in Selection.gameObjects)
                    selectedGameObject.Add(item);
                removeTargets.Clear();
                foundObjects.Clear();
                foreach (var target in targets)
                {
                    if (!target)
                        continue;

                    foundObjects.Add(target);
                    if (!knownTargets.Add(target))
                        continue;

                    var generator = target as T;
                    if (!OnGeneratorActive(generator))
                        continue;

                    OnGeneratorSelected(target as T);
                    validTargets.Add(generator);
                }

                foreach (var knownTarget in knownTargets)
                {
                    if (!foundObjects.Contains(knownTarget))
                    {
                        var removeTarget = target as T;
                        if (validTargets.Contains(removeTarget))
                        {
                            s_Handles.generatorStateLookup.Remove(removeTarget);
                            OnGeneratorDeselected(removeTarget);
                            validTargets.Remove(removeTarget);
                        }
                        removeTargets.Add(removeTarget);
                    }
                    else
                    {
                        var removeTarget = target as T;
                        if (removeTarget == null ||
                            !selectedGameObject.Contains(removeTarget.gameObject))
                        {
                            s_Handles.generatorStateLookup.Remove(removeTarget);
                            OnGeneratorDeselected(removeTarget);
                            validTargets.Remove(removeTarget);
                            removeTargets.Add(removeTarget);
                        }
                    }
                }
                foundObjects.Clear();

                foreach (var removeTarget in removeTargets)
                    knownTargets.Remove(removeTarget);
                selectedGameObject.Clear();
                removeTargets.Clear();
                foundObjects.Clear();
            }
            finally
			{
				HashSetPool<System.Object>.Release(foundObjects);
				HashSetPool<T>.Release(removeTargets);
				HashSetPool<GameObject>.Release(selectedGameObject);

			}
        }

        public override void OnInspectorGUI()
        {
            if (!target)
                return;
            Profiler.BeginSample("OnInspectorGUI");
            serializedObject.Update();

            base.OnInspectorGUI();
            try
            {
                EditorGUI.BeginChangeCheck();
                {
                    ShowInspectorHeader(operationProp);
                    OnInspector();
                }
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                }
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }

            if (PreviewTextureManager.Update())
                Repaint();
            Profiler.EndSample();
        }

		readonly static ChiselEditorHandles s_Handles = new();

        public override void OnSceneGUI()
        {
            if (!target)
                return;

            if (Tools.current != Tool.Custom || !ChiselEditGeneratorTool.IsActive())
            {
                OnDefaultSceneTools();
                return;
            }

            // Skip some events, to prevent scalability issues (when selecting thousands of brushes at the same time)
            // Could happen when doing control-A (select all)
            switch (Event.current.type)
            {
                case EventType.MouseEnterWindow:
                case EventType.MouseLeaveWindow:
                case EventType.Ignore:
                case EventType.Used:
                    return;

                case EventType.MouseDown:
                case EventType.MouseUp:
                case EventType.MouseDrag:
                case EventType.DragExited:
                case EventType.DragPerform:
                case EventType.DragUpdated:
                {
                    // Mouse messages don't make sense when the mouse is not over the current window
                    if (SceneView.currentDrawingSceneView != EditorWindow.mouseOverWindow)
                        return;
                    break;
                }
            }

            var generator   = target as T;
            var sceneView   = SceneView.currentDrawingSceneView;
            
            // NOTE: allow invalid nodes to be edited to be able to recover from invalid state

            // NOTE: could loop over multiple instances from here, once we support that
            {
                using (new UnityEditor.Handles.DrawingScope(SceneHandles.HandleColor, generator.GlobalTransformation))
                {
                    s_Handles.Start(generator, sceneView);
                    {
                        if (GUIUtility.hotControl == 0)
                        {
                            if (!OnGeneratorActive(generator))
                            {
                                if (validTargets.Contains(generator))
                                {
                                    s_Handles.generatorStateLookup.Remove(generator);
                                    OnGeneratorDeselected(generator);
                                    validTargets.Remove(generator);
                                }
                                return;
                            }
                            if (!validTargets.Contains(generator))
                            {
                                s_Handles.generatorStateLookup.Remove(generator);
                                OnGeneratorDeselected(generator);
                                validTargets.Add(generator);
                            }
                        }

                        EditorGUI.BeginChangeCheck();
                        try
                        {
                            OnScene(s_Handles, generator);
                        }
                        finally
                        {
                            if (EditorGUI.EndChangeCheck())
                            {
                                generator.OnValidate();
                                OnTargetModifiedInScene(target as T);
                            }
                            s_Handles.End();
                        }
                    }
                }
            }
        }
    }
}
