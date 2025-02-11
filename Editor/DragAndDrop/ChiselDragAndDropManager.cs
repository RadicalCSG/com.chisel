using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public sealed class ChiselDragAndDropManager : ScriptableObject
    {
        static ChiselDragAndDropManager s_instance;
        public static ChiselDragAndDropManager Instance
        {
            get
            {
                if (s_instance)
                    return s_instance;

                var foundInstances = UnityEngine.Object.FindObjectsByType<ChiselDragAndDropManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
				if (foundInstances == null ||
                    foundInstances.Length == 0)
                {
                    s_instance = ScriptableObject.CreateInstance<ChiselDragAndDropManager>();
                    s_instance.hideFlags = HideFlags.HideAndDontSave;
                    return s_instance;
                }

                s_instance = foundInstances[0];
                return s_instance;
            }
        }

        static IChiselDragAndDropOperation s_DragAndDropOperation;

        readonly static int kChiselDragAndDropManagerHash = "ChiselDragAndDropManager".GetHashCode();

        public void OnSceneGUI(SceneView sceneView)
        {
            int id = GUIUtility.GetControlID(kChiselDragAndDropManagerHash, FocusType.Keyboard);
            switch (Event.current.type)
            {
                case EventType.ValidateCommand:
                {
                    // TODO: implement
                    // "Copy", "Cut", "Paste", "Delete", "SoftDelete", "Duplicate", "FrameSelected", "FrameSelectedWithLock", "SelectAll", "Find" and "FocusProjectWindow".
                    //Debug.Log(Event.current.commandName);
                    break;
                }
                case EventType.DragUpdated:
                {
                    if (s_DragAndDropOperation == null &&
                        DragAndDrop.activeControlID == 0)
                    {
                        s_DragAndDropOperation = ChiselDragAndDropMaterial.AcceptDrag();
                        if (s_DragAndDropOperation != null)
                            DragAndDrop.activeControlID = id;
                    }

                    if (s_DragAndDropOperation != null)
                    {
                        s_DragAndDropOperation.UpdateDrag();
                        DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                        DragAndDrop.activeControlID = id;
                        Event.current.Use();
                    }
                    break;
                }
                case EventType.DragPerform:
                {
                    if (s_DragAndDropOperation != null)
                    {
                        s_DragAndDropOperation.PerformDrag();
                        s_DragAndDropOperation = null;
                        DragAndDrop.AcceptDrag();
                        DragAndDrop.activeControlID = 0;
                        Event.current.Use();
                    }
                    break;
                }
                case EventType.DragExited:
                {
                    if (s_DragAndDropOperation != null)
                    {
                        s_DragAndDropOperation.CancelDrag();
                        s_DragAndDropOperation = null;
                        DragAndDrop.activeControlID = 0;
                        HandleUtility.Repaint();
                        Event.current.Use();
                    }
                    break;
                }
            }
        }
    }
}
