using UnityEditor;
using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    public sealed partial class SceneHandles
    {
        public static Color		PreSelectionColor       { get; private set; } = new(201f / 255, 200f / 255, 144f / 255, 0.89f);
        public static Color		StaticColor				{ get; private set; } = new(.5f, .5f, .5f, 0f);
        //public static Color	DisabledHandleColor	    { get; private set; } = new(.5f, .5f, .5f, .5f);
        public static float		StaticBlend				{ get; private set; } = 0.6f;
        public static float		BackfaceAlphaMultiplier { get; private set; } = 0.3f;
        
        public static Color		Color					{ get { return UnityEditor.Handles.color; } set { UnityEditor.Handles.color = value; } }
		
		public static Color		HandleColor				{ get; private set; } = new(Mathf.LinearToGammaSpace(61f / 255f), Mathf.LinearToGammaSpace(200f / 255f), Mathf.LinearToGammaSpace(255f / 255f), 0.89f);
		public static Color		MeasureColor			{ get; private set; } = new(Mathf.LinearToGammaSpace(0.788f), Mathf.LinearToGammaSpace(0.784f), Mathf.LinearToGammaSpace(0.565f), 0.890f);

        public static Color		SelectedColor           { get { return UnityEditor.Handles.selectedColor; } }
        public static Color		SecondaryColor			{ get { return UnityEditor.Handles.secondaryColor; } }
        public static Color		CenterColor				{ get { return UnityEditor.Handles.centerColor; } }

        public static Color		XAxisColor				{ get { return UnityEditor.Handles.xAxisColor; } }
        public static Color		YAxisColor				{ get { return UnityEditor.Handles.yAxisColor; } }
        public static Color		ZAxisColor				{ get { return UnityEditor.Handles.zAxisColor; } }


        public static Color ToActiveColorSpace(Color color)
        {
            return (QualitySettings.activeColorSpace == ColorSpace.Linear) ? color.linear : color;
        }

        public static Color StateColor(Color color, bool isDisabled = false, bool isSelected = false, bool isPreSelected = false)
        {
            return	ToActiveColorSpace((isDisabled || Disabled) ? Color.Lerp(color, StaticColor, StaticBlend) : 
                                       (isSelected) ? SceneHandles.SelectedColor : 
                                       (isPreSelected) ? SceneHandles.PreSelectionColor : color);
        }

        public static Color MultiplyTransparency(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, color.a * alpha);
        }
    }
}
