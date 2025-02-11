using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
	// TODO: cleanup
	// TODO: find all places that have an IsProSkin check, and make them more consistent
	public static class ChiselSceneGUIStyle
    {
        public const float kTopBarHeight = 22;
        public const float kBottomBarHeight = 22;
        public const float kFloatFieldWidth = 60;

        const int kIconSize = 16;
        const int kOffsetToText = 3;

        static GUIStyle s_ToolbarStyle;
        //static GUIStyle s_WindowStyle;
        //static GUIStyle s_ToggleStyle;
        //static GUIStyle s_ButtonStyle;
		static GUIStyle s_InspectorLabel;
		static GUIStyle s_InspectorSelectedLabel;


        public static GUIStyle InspectorLabel => s_InspectorLabel;
        public static GUIStyle InspectorSelectedLabel => s_InspectorSelectedLabel;

        public static bool IsInitialized { get; private set; }
        static bool s_IsProSkin = true;
        static bool s_PrevIsProSkin = false;

        public static GUISkin GetSceneSkin()
        {
            s_IsProSkin = EditorGUIUtility.isProSkin;
            if (s_IsProSkin)
            {
                return EditorGUIUtility.GetBuiltinSkin(EditorSkin.Scene);
            } else
                return EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
        }

        public static void Update()
        {
            s_IsProSkin = EditorGUIUtility.isProSkin;
            if (s_ToolbarStyle != null && s_PrevIsProSkin == s_IsProSkin)
                return;

            IsInitialized = true;
            s_InspectorLabel = new GUIStyle(GUI.skin.label);
            s_InspectorLabel.padding = new RectOffset(kIconSize + kOffsetToText, 0, 0, 0);

            s_InspectorSelectedLabel = new GUIStyle(s_InspectorLabel);
            if (!s_IsProSkin)
            {
                s_InspectorSelectedLabel.normal.textColor = Color.white;
                s_InspectorSelectedLabel.onNormal.textColor = Color.white;
            }

			//s_WindowStyle = new GUIStyle(GUI.skin.window);

			s_ToolbarStyle = new GUIStyle(GUI.skin.window)
			{
				fixedHeight = kBottomBarHeight,
				padding = new RectOffset(2, 6, 0, 1),
				contentOffset = Vector2.zero
			};
            /*
			s_ToggleStyle = new GUIStyle(EditorStyles.toolbarButton)
			{
				fixedHeight = kBottomBarHeight - 2,
				margin = new RectOffset(0, 0, 1, 0)
			};

			s_ButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
			{
				fixedHeight = kBottomBarHeight - 2,
				margin = new RectOffset(0, 0, 1, 0)
			};
            */
			s_PrevIsProSkin = s_IsProSkin;
            ChiselEditorSettings.Load(); // <- put somewhere else
        }
    }
}
