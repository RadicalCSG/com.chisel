using System;
using System.Collections.Generic;
using Chisel.Components;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;
using UnityEngine.Pool;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif

namespace Chisel.Editors
{

    public sealed class ChiselUnityEventsManager
    {
        [UnityEditor.InitializeOnLoadMethod]
        [RuntimeInitializeOnLoadMethod]
        public static void Initialize()
        {
            // Note that it's always safer to first unregister an event before 
            // assigning it, since this will avoid double assigning / leaking events 
            // whenever this code is, for whatever reason, run more than once.

            // Update loop
            UnityEditor.EditorApplication.update -= OnEditorApplicationUpdate;
            UnityEditor.EditorApplication.update += OnEditorApplicationUpdate;

            // Called after prefab instances in the scene have been updated.
            UnityEditor.PrefabUtility.prefabInstanceUpdated -= OnPrefabInstanceUpdated;
            UnityEditor.PrefabUtility.prefabInstanceUpdated += OnPrefabInstanceUpdated;

            // OnGUI events for every visible list item in the HierarchyWindow.
            UnityEditor.EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyWindowItemOnGUI;
            UnityEditor.EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;

            // Triggered when the hierarchy changes
            UnityEditor.EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            UnityEditor.EditorApplication.hierarchyChanged += OnHierarchyChanged;

            // Triggered when currently active/selected item has changed.
            UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
            UnityEditor.Selection.selectionChanged += OnSelectionChanged;

            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;


			// Triggered when changing visibility/picking in hierarchy
			UnityEditor.SceneVisibilityManager.visibilityChanged -= OnVisibilityChanged;
			UnityEditor.SceneVisibilityManager.visibilityChanged += OnVisibilityChanged;

			UnityEditor.SceneVisibilityManager.pickingChanged -= OnPickingChanged;
			UnityEditor.SceneVisibilityManager.pickingChanged += OnPickingChanged;


            // Callback that is triggered after an undo or redo was executed.
            UnityEditor.Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            UnityEditor.Undo.undoRedoPerformed += OnUndoRedoPerformed;

            UnityEditor.Undo.postprocessModifications -= OnPostprocessModifications;
            UnityEditor.Undo.postprocessModifications += OnPostprocessModifications;

            UnityEditor.Undo.willFlushUndoRecord -= OnWillFlushUndoRecord;
            UnityEditor.Undo.willFlushUndoRecord += OnWillFlushUndoRecord;

            UnityEditor.SceneView.beforeSceneGui -= OnBeforeSceneGUI;
            UnityEditor.SceneView.beforeSceneGui += OnBeforeSceneGUI;

            UnityEditor.SceneView.duringSceneGui -= OnDuringSceneGUI;
            UnityEditor.SceneView.duringSceneGui += OnDuringSceneGUI;

			UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
			UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
			
            ToolManager.activeToolChanged -= OnEditModeChanged;
			ToolManager.activeToolChanged += OnEditModeChanged;

			HandleUtility.UnregisterRenderPickingCallback(ChiselSelectionManager.PickingCallback);
			HandleUtility.RegisterRenderPickingCallback(ChiselSelectionManager.PickingCallback);


			// Triggered when currently active/selected item has changed.
			ChiselSurfaceSelectionManager.SelectionChanged -= OnSurfaceSelectionChanged;
            ChiselSurfaceSelectionManager.SelectionChanged += OnSurfaceSelectionChanged;
            ChiselSurfaceSelectionManager.HoverChanged -= OnSurfaceHoverChanged;
            ChiselSurfaceSelectionManager.HoverChanged += OnSurfaceHoverChanged;

            ChiselNodeHierarchyManager.NodeHierarchyReset -= OnHierarchyReset;
            ChiselNodeHierarchyManager.NodeHierarchyReset += OnHierarchyReset;

            ChiselNodeHierarchyManager.NodeHierarchyModified -= OnNodeHierarchyModified;
            ChiselNodeHierarchyManager.NodeHierarchyModified += OnNodeHierarchyModified;

            ChiselNodeHierarchyManager.TransformationChanged -= OnTransformationChanged;
            ChiselNodeHierarchyManager.TransformationChanged += OnTransformationChanged;

			ChiselModelManager.Instance.PostUpdateModels -= OnPostUpdateModels;
			ChiselModelManager.Instance.PostUpdateModels += OnPostUpdateModels;

			ChiselModelManager.Instance.PostReset -= OnPostResetModels;
			ChiselModelManager.Instance.PostReset += OnPostResetModels;


			//ChiselClickSelectionManager.Instance.OnReset();
			ChiselOutlineRenderer.Instance.OnReset();
        }

		private static void OnHierarchyChanged()
        {
            ChiselNodeHierarchyManager.CheckOrderOfChildNodesModifiedOfNonNodeGameObject();
        }

        private static void OnPickingChanged()
		{
			ChiselUnityVisibilityManager.SetDirty();
        }

        private static void OnVisibilityChanged()
		{
			ChiselUnityVisibilityManager.SetDirty();
		}

        private static void OnActiveSceneChanged(Scene prevScene, Scene newScene)
        {
            ChiselModelManager.Instance.OnActiveSceneChanged(prevScene, newScene);
        }

        static void OnTransformationChanged()
        {
            ChiselOutlineRenderer.Instance.OnTransformationChanged();
        }


        static void OnBeforeSceneGUI(SceneView sceneView)
        {
            Profiler.BeginSample("OnBeforeSceneGUI");
            ChiselDrawModes.HandleDrawMode(sceneView);
            Profiler.EndSample();
        }

        static void OnDuringSceneGUI(SceneView sceneView)
        {
            Profiler.BeginSample("OnDuringSceneGUI");
            // Workaround where Unity stops redrawing sceneview after a second, which makes hovering over edge visualization stop working
            if (Event.current.type == EventType.MouseMove)
                sceneView.Repaint();

            var prevSkin = GUI.skin;
            GUI.skin = ChiselSceneGUIStyle.GetSceneSkin();
            try
            {
                ChiselSceneGUIStyle.Update();
                ChiselGridSettings.GridOnSceneGUI(sceneView);
                ChiselOutlineRenderer.Instance.OnSceneGUI(sceneView);

                if (EditorWindow.mouseOverWindow == sceneView || // This helps prevent weird issues with overlapping sceneviews + avoid some performance issues with multiple sceneviews open
                    (Event.current.type != EventType.MouseMove && Event.current.type != EventType.Layout))
                {
                    ChiselDragAndDropManager.Instance.OnSceneGUI(sceneView);
					ChiselRectSelectionManager.OnSceneGUI(sceneView);
                }
            }
            finally
            {
                GUI.skin = prevSkin;
            }
            Profiler.EndSample();
        }

        private static void OnEditModeChanged()//IChiselToolMode prevEditMode, IChiselToolMode newEditMode)
        {
            ChiselOutlineRenderer.Instance.OnEditModeChanged();
            if (Tools.current != Tool.Custom)
            {
                ChiselGeneratorManager.ActivateTool(null);
            }
            ChiselGeneratorManager.ActivateTool(ChiselGeneratorManager.GeneratorMode);
        }

        private static void OnSelectionChanged()
        {
            //ChiselClickSelectionManager.Instance.OnSelectionChanged();
            ChiselOutlineRenderer.Instance.OnSelectionChanged();
        }

        private static void OnSurfaceSelectionChanged()
        {
            ChiselOutlineRenderer.Instance.OnSurfaceSelectionChanged();
        }

        private static void OnSurfaceHoverChanged()
        {
            ChiselOutlineRenderer.Instance.OnSurfaceHoverChanged();
        }


        private static void OnPostUpdateModels()
        {
            ChiselOutlineRenderer.Instance.OnGeneratedMeshesChanged();
        }

        private static void OnPostResetModels()
        {
            ChiselOutlineRenderer.Instance.OnReset();
        }

        private static void OnNodeHierarchyModified()
        {
            ChiselOutlineRenderer.Instance.OnReset();

            // Prevent infinite loops
            if (Event.current != null &&
                Event.current.type == EventType.Repaint)
                return;

            Editors.ChiselManagedHierarchyView.RepaintAll();

            // THIS IS SLOW! DON'T DO THIS
            //UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void OnHierarchyReset()
        {
            // Prevent infinite loops
            if (Event.current != null &&
                Event.current.type == EventType.Repaint)
                return;
            Editors.ChiselManagedHierarchyView.RepaintAll();
            //Editors.ChiselInternalHierarchyView.RepaintAll(); 
        }

        private static void OnPrefabInstanceUpdated(GameObject instance)
        {
            ChiselNodeHierarchyManager.OnPrefabInstanceUpdated(instance);
        }

        private static void OnEditorApplicationUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            try
            {
                ChiselNodeHierarchyManager.Update();
				ChiselModelManager.Instance.UpdateModels();
                ChiselNodeEditorBase.HandleCancelEvent();
            }
            catch (Exception ex) 
            {
                Debug.LogException(ex);
            }
        }

        private static void OnHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            Profiler.BeginSample("OnHierarchyWindowItemOnGUI");
            try
            {
                var obj = UnityEditor.EditorUtility.InstanceIDToObject(instanceID);
                if (!obj)
                    return;
                var gameObject = (GameObject)obj;

                // TODO: implement material drag & drop support for meshes

                var component = gameObject.GetComponent<ChiselNodeComponent>();
                if (!component)
                    return;
                Editors.ChiselHierarchyWindowManager.OnHierarchyWindowItemGUI(instanceID, component, selectionRect);
            }
            finally { Profiler.EndSample(); }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            ChiselNodeHierarchyManager.firstStart = false;
        }



        private static void OnUndoRedoPerformed()
        {
            //ProfileFrame("myLog2");

            //ChiselNodeHierarchyManager.firstStart = false;
            ChiselNodeHierarchyManager.UpdateAllTransformations();
            ChiselOutlineRenderer.Instance.OnTransformationChanged();
            ChiselOutlineRenderer.OnUndoRedoPerformed();

        }

		private static void OnWillFlushUndoRecord()
        {
            ChiselModelManager.Instance.OnWillFlushUndoRecord();
        }

        private static UnityEditor.UndoPropertyModification[] OnPostprocessModifications(UnityEditor.UndoPropertyModification[] modifications)
        {
            // Note: this is not always properly called 
            //			- when? can't remember? maybe prefab related?

            var modifiedNodes = HashSetPool<ChiselNodeComponent>.Get();
			var processedTransforms = HashSetPool<Transform>.Get();
            var childNodes = ListPool<ChiselNodeComponent>.Get();
			try
            {
                for (int i = 0; i < modifications.Length; i++)
                {
                    var currentValue = modifications[i].currentValue;
                    var transform = currentValue.target as Transform;
                    if (object.Equals(null, transform))
                        continue;

                    if (processedTransforms.Contains(transform))
                        continue;

                    var propertyPath = currentValue.propertyPath;
                    if (!propertyPath.StartsWith("m_Local"))
                        continue;

                    processedTransforms.Add(transform);

                    childNodes.Clear();
                    transform.GetComponentsInChildren<ChiselNodeComponent>(false, childNodes);
                    if (childNodes.Count == 0)
                        continue;
                    if (childNodes[0] is ChiselModelComponent)
                        continue;
                    for (int n = 0; n < childNodes.Count; n++)
                        modifiedNodes.Add(childNodes[n]);
                }
                if (modifiedNodes.Count > 0)
                {
                    ChiselNodeHierarchyManager.NotifyTransformationChanged(modifiedNodes);
                }
                return modifications;
            }
            finally
			{
				HashSetPool<ChiselNodeComponent>.Release(modifiedNodes);
				HashSetPool<Transform>.Release(processedTransforms);
				ListPool<ChiselNodeComponent>.Release(childNodes);
			}
        }
    }

}