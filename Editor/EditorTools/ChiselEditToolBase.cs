using System;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
 
namespace Chisel.Editors
{
    abstract class ChiselEditToolBase : EditorTool, IChiselToolMode
    {
        // Serialize this value to set a default value in the Inspector.
        [SerializeField] internal Texture2D m_ToolIcon = null;
        [SerializeField] internal Texture2D m_ToolIconActive = null;
        [SerializeField] internal Texture2D m_ToolIconDark = null;
        [SerializeField] internal Texture2D m_ToolIconDarkActive = null;

        public abstract string ToolName { get; }
        
        public abstract SnapSettings ToolUsedSnappingModes { get; }

        public Texture2D Icon
        {
            get
            {
                var icon = m_ToolIcon;
                if (EditorGUIUtility.isProSkin)
                    icon = m_ToolIconDark;
                return icon;
            }
        }

        public Texture2D ActiveIcon
        {
            get
            {
                var icon = m_ToolIconActive;
                if (EditorGUIUtility.isProSkin)
                    icon = m_ToolIconDarkActive;
                return icon;
            }
        }

        public override GUIContent toolbarIcon => cachedToolbarContent;

        protected GUIContent cachedToolbarContent = new();
        public virtual GUIContent Content
        {
            get 
            {
                return new GUIContent()
                {
                    image   = Icon,
                    text    = $"Chisel {ToolName} Tool",
                    tooltip = $"Chisel {ToolName} Tool"
                };
            }
        }

        public GUIContent IconContent { get; private set; } = new GUIContent();
        public GUIContent ActiveIconContent { get; private set; } = new GUIContent();

        public void OnEnable()
        {
            s_LastSelectedTool = null; 
            EditorApplication.delayCall -= OnDelayedEnable;
            EditorApplication.delayCall += OnDelayedEnable;
        }

        void OnDisable()
        {
            EditorApplication.delayCall -= OnDelayedEnable;
            if (s_LastSelectedTool != null)
                s_LastSelectedTool.OnDeactivate();
        }

        public void Awake()
        {
            s_LastSelectedTool = null;
            UpdateIcon();
        }

        // Unity bug workaround
        void OnDelayedEnable()
        {
            EditorApplication.delayCall -= OnDelayedEnable;

            ToolNotActivatingBugWorkAround();
            UpdateIcon();
            SceneView.RepaintAll();
        }

        public void UpdateIcon()
        {
            var newContent = Content;
            cachedToolbarContent.image     = newContent.image;
            cachedToolbarContent.text      = newContent.text;
            cachedToolbarContent.tooltip   = newContent.tooltip;

            {
                var iconContent = IconContent;
                iconContent.image       = Icon;
                iconContent.tooltip     = ToolName;
            }

            {
                var activeIconContent = ActiveIconContent;
                activeIconContent.image     = ActiveIcon;
                activeIconContent.tooltip   = ToolName;
            }
        }


        static ChiselEditToolBase s_LastSelectedTool = null;
        static Type s_LastRememberedToolType = null;

        public static void ClearLastRememberedType()
        { 
            s_LastRememberedToolType = null; 
        }

        public void ToolNotActivatingBugWorkAround()
        {
            if (s_LastSelectedTool == null)
            {
                if (Tools.current != Tool.Custom &&
                    s_LastRememberedToolType != null)
                {
                    ToolManager.SetActiveTool(s_LastRememberedToolType);
                    s_LastRememberedToolType = null;
                } else
                if (ToolManager.activeToolType == this.GetType())
                {
                    OnActivate();
                }
            }
        }

        public override void OnToolGUI(EditorWindow window)
        {
            var matrix = Handles.matrix;
            if (s_LastSelectedTool == null ||
                s_LastSelectedTool != this)
            {
                if (s_LastSelectedTool != null)
                    s_LastSelectedTool.OnDeactivate();
                OnActivate();
            }
            var sceneView = window as SceneView;
            var dragArea = sceneView.position;
            dragArea.position = Vector2.zero;

            OnSceneGUI(sceneView, dragArea);

            Handles.matrix = matrix;
        }

        public virtual void OnActivate()
        {
            s_LastSelectedTool = this;
            s_LastRememberedToolType = this.GetType();
			Chisel.Editors.Snapping.SnapMask = ToolUsedSnappingModes;
            SnapSettingChanged?.Invoke();
        }

        public virtual void OnDeactivate()
        {
			Chisel.Editors.Snapping.SnapMask = Chisel.Editors.SnapSettings.All;
            SnapSettingChanged?.Invoke();
        }

        public static event Action SnapSettingChanged;

        public abstract void OnSceneGUI(SceneView sceneView, Rect dragArea);
    }
}
