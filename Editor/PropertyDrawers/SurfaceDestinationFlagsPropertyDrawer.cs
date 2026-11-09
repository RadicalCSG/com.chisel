using System;
using UnityEngine;
using UnityEditor;
using Chisel.Core;
using Unity.Mathematics;

namespace Chisel.Editors
{
    [CustomPropertyDrawer(typeof(SurfaceDestinationFlags))]
    public sealed class SurfaceDestinationFlagsPropertyDrawer : PropertyDrawer
    {
        private readonly static GUIContent	kVisibleContent			= new("Visible", "When set, the surface will be visible, otherwise it will not be rendered.");
		private readonly static GUIContent	kCollidableContent		= new("Collidable", "When set, the surface will be part of the generated collider, otherwise it will not be part of the collider.");
		private readonly static GUIContent  kShadowCastingContent	= new("Cast Shadows", "When set, the surface cast shadows on other surfaces, otherwise light will pass through it. Note that the surface does not need to be visible to block light.");
		private readonly static GUIContent  kShadowReceivingContent	= new("Receive Shadows", "When set, the surface have shadows cast onto it, as long as the surface is visible. This cannot be disabled in deferred rendering modes.");
		private readonly static GUIContent  kExcludedFromGIContent	= new("Exclude From GI", "When set, the surface is left out of baked global illumination. A surface that doesn't cast shadows always is.");
		
        public static float DefaultHeight
        {
            get
            {
                return (EditorGUI.GetPropertyHeight(SerializedPropertyType.Boolean, GUIContent.none) * 3);
            }
        }

        const float kToggleWidth = 18;
        static float LineHeight => EditorGUI.GetPropertyHeight(SerializedPropertyType.Boolean, GUIContent.none);

        static float LeftTextWidth  => math.max(EditorStyles.label.CalcSize(kShadowCastingContent).x, EditorStyles.label.CalcSize(kVisibleContent).x);
        static float RightTextWidth => math.max(EditorStyles.label.CalcSize(kShadowReceivingContent).x, EditorStyles.label.CalcSize(kCollidableContent).x);

        /// <summary>Whether the toggles fit two to a row in the field's width; otherwise each gets its own row</summary>
        internal static bool FitsTwoColumns(float fieldWidth)
        {
            return fieldWidth >= LeftTextWidth + RightTextWidth + kToggleWidth * 2;
        }

        internal static float HeightFor(float fieldWidth)
        {
            return LineHeight * (FitsTwoColumns(fieldWidth) ? 3 : 5);
        }

        // Two toggles to a row when they fit and the rows were made for that, one to a row otherwise
        internal static bool TwoColumnsIn(Rect position) => FitsTwoColumns(position.width) && position.height < LineHeight * 4;

        // How high the toggles are drawn in this rect
        internal static float UsedHeight(Rect position) => LineHeight * (TwoColumnsIn(position) ? 3 : 5);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // the field's width isn't given here; the view's, less the label and margins, is close to it
            return HeightFor(EditorGUIUtility.currentViewWidth - EditorGUIUtility.labelWidth - 24);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            bool prevShowMixedValue			= EditorGUI.showMixedValue;
            bool deferredRenderingPath		= ChiselEditorUtility.IsUsingDeferredRenderingPath();
            EditorGUI.showMixedValue        = prevShowMixedValue || property.hasMultipleDifferentValues;
            try
            { 
                var surfaceDestinationFlags = (SurfaceDestinationFlags)property.enumValueFlag;

                bool isCollidable		= (surfaceDestinationFlags & SurfaceDestinationFlags.Collidable) != 0;
                bool isRenderable		= (surfaceDestinationFlags & SurfaceDestinationFlags.Renderable) != 0;
                bool isShadowCasting	= (surfaceDestinationFlags & SurfaceDestinationFlags.ShadowCasting) != 0;
                bool isShadowReceiving	= (surfaceDestinationFlags & SurfaceDestinationFlags.ShadowReceiving) != 0;
                bool isExcludedFromGI	= (surfaceDestinationFlags & SurfaceDestinationFlags.ExcludedFromGlobalIllumination) != 0;
				//bool isDoubleSided	= (surfaceDestinationFlags & SurfaceDestinationFlags.DoubleSided) != 0;
				//bool isSkybox	        = (surfaceDestinationFlags & SurfaceDestinationFlags.ReplaceWithSkybox) != 0;
				//bool noLightmap	    = (surfaceDestinationFlags & SurfaceDestinationFlags.NoLightmap) != 0;
				//bool transparent  	= (surfaceDestinationFlags & SurfaceDestinationFlags.Transparent) != 0;

				// Draw label
				position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Keyboard), label);
                
                EditorGUI.BeginChangeCheck();
                { 
                    // Don't make child fields be indented
                    var indent = EditorGUI.indentLevel;
                    EditorGUI.indentLevel = 0;

                    var toggleStyle     = EditorStyles.label;

                    Rect button1, button2, button3, button4, button5;
                    var line = position;
                    line.height = EditorGUIUtility.singleLineHeight;
                    // one toggle per row when they don't fit two to a row, or when the rows were made for that
                    if (TwoColumnsIn(position))
                    {
                        var textWidthLeft  = LeftTextWidth + kToggleWidth;
                        var textWidthRight = RightTextWidth + kToggleWidth;
                        var offset         = (position.width - (textWidthRight + textWidthLeft)) / 2;
                        textWidthLeft  += offset;
                        textWidthRight += offset;

                        button1 = line; button1.width = textWidthLeft;
                        button2 = line; button2.x += textWidthLeft; button2.width = textWidthRight;
                        button3 = button1; button3.y += LineHeight;
                        button4 = button2; button4.y += LineHeight;
                        button5 = line; button5.y += LineHeight * 2;
                    } else
                    {
                        button1 = line;
                        button2 = line; button2.y += LineHeight;
                        button3 = line; button3.y += LineHeight * 2;
                        button4 = line; button4.y += LineHeight * 3;
                        button5 = line; button5.y += LineHeight * 4;
                    }


					isRenderable	= EditorGUI.ToggleLeft(button1, kVisibleContent, isRenderable, toggleStyle);
					isCollidable    = EditorGUI.ToggleLeft(button2, kCollidableContent, isCollidable, toggleStyle);
                    isShadowCasting	= EditorGUI.ToggleLeft(button3, kShadowCastingContent, isShadowCasting, toggleStyle);
                    EditorGUI.BeginDisabledGroup(deferredRenderingPath || !isRenderable);
                    if	    (!isRenderable        ) EditorGUI.ToggleLeft(button4, kShadowReceivingContent, false, toggleStyle);
                    else if (deferredRenderingPath) EditorGUI.ToggleLeft(button4, kShadowReceivingContent, true,  toggleStyle);
                    else		 isShadowReceiving = EditorGUI.ToggleLeft(button4, kShadowReceivingContent, isShadowReceiving, toggleStyle);
                    EditorGUI.EndDisabledGroup();
                    // a surface that doesn't cast shadows is always left out (SurfaceDestinationFlags.Normalize)
                    EditorGUI.BeginDisabledGroup(!isShadowCasting);
                    if (!isShadowCasting) EditorGUI.ToggleLeft(button5, kExcludedFromGIContent, true, toggleStyle);
                    else                  isExcludedFromGI = EditorGUI.ToggleLeft(button5, kExcludedFromGIContent, isExcludedFromGI, toggleStyle);
                    EditorGUI.EndDisabledGroup();

					// Set indent back to what it was
					EditorGUI.indentLevel = indent;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    if (isRenderable)		surfaceDestinationFlags |=  SurfaceDestinationFlags.Renderable;
                    else					surfaceDestinationFlags &= ~SurfaceDestinationFlags.Renderable;
                
                    if (isShadowCasting)		surfaceDestinationFlags |=  SurfaceDestinationFlags.ShadowCasting;
                    else					surfaceDestinationFlags &= ~SurfaceDestinationFlags.ShadowCasting;
                
                    if (isShadowReceiving)	surfaceDestinationFlags |=  SurfaceDestinationFlags.ShadowReceiving;
                    else					surfaceDestinationFlags &= ~SurfaceDestinationFlags.ShadowReceiving;
                
                    if (isCollidable)		surfaceDestinationFlags |=  SurfaceDestinationFlags.Collidable;
                    else					surfaceDestinationFlags &= ~SurfaceDestinationFlags.Collidable;

                    if (isExcludedFromGI)	surfaceDestinationFlags |=  SurfaceDestinationFlags.ExcludedFromGlobalIllumination;
                    else					surfaceDestinationFlags &= ~SurfaceDestinationFlags.ExcludedFromGlobalIllumination;

					property.intValue = (int)surfaceDestinationFlags;
                }
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
            
            EditorGUI.showMixedValue = prevShowMixedValue;
            EditorGUI.EndProperty();
        }
    }
}
