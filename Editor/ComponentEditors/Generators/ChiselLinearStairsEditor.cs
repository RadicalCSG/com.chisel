using Chisel.Core;
using Chisel.Components;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselLinearStairsComponent))]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed class ChiselLinearStairsEditor : ChiselGeneratorDefinitionEditor<ChiselLinearStairsComponent, ChiselLinearStairsDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselLinearStairsComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselLinearStairsComponent.kNodeTypeName); }

        protected override bool ShowsContents => false;

        // the steps with the settings they show under them, the side walls with theirs, then the rest
        protected override void OnInspector()
        {
            OnGroupedInspector(() =>
            {
                ChiselLinearStairsDiagram.Draw();
                SettingsField(nameof(ChiselLinearStairs.bounds));
                SettingsField(nameof(ChiselLinearStairs.stepHeight));
                SettingsField(nameof(ChiselLinearStairs.stepDepth));
                SettingsField(nameof(ChiselLinearStairs.treadHeight));
                SettingsField(nameof(ChiselLinearStairs.nosingDepth));
                SettingsField(nameof(ChiselLinearStairs.nosingWidth));
                SettingsField(nameof(ChiselLinearStairs.riserType));
                SettingsField(nameof(ChiselLinearStairs.riserDepth));
                SettingsField(nameof(ChiselLinearStairs.plateauHeight));
                EditorGUILayout.Space();

                ChiselStairsSideWallDiagram.Draw();

                EditorGUILayout.LabelField("Guard Rails", EditorStyles.boldLabel);
                SettingsField(nameof(ChiselLinearStairs.leftSide));
                SettingsField(nameof(ChiselLinearStairs.rightSide));
                SettingsField(nameof(ChiselLinearStairs.sideWidth));
                SettingsField(nameof(ChiselLinearStairs.sideHeight));
                SettingsField(nameof(ChiselLinearStairs.sideDepth));
                SettingsField(nameof(ChiselLinearStairs.sideFrontOffset));
                SettingsField(nameof(ChiselLinearStairs.topSide));
                SettingsField(nameof(ChiselLinearStairs.topSideLength), visible: SettingsIs(nameof(ChiselLinearStairs.topSide), (int)StairsTopSideType.Length));
                EditorGUILayout.Space();
            });
        }
    }

    // What the stairs' values measure: a side view in the inspector
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    static class ChiselLinearStairsDiagram
    {
        const float kHeight = 150;

        static GUIStyle s_Label;
        static GUIStyle Label => s_Label ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };

        // A side view of three steps on a base, with their treads and risers, each measure marked. Spiral stairs have no
        // step depth or base of their own to set, so those can be left out, and the riser only when it is thin
        public static void Draw(bool stepDepthAndBase = true, bool riserThickness = true)
        {
            var rect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 40, kHeight);
            if (Event.current.type != EventType.Repaint)
                return;

            var text     = EditorStyles.label.normal.textColor;
            var shape    = new Color(text.r, text.g, text.b, 0.15f);
            var outline  = new Color(text.r, text.g, text.b, 0.6f);
            var measure  = ChiselEditorColors.Measurement;

            // in pixels: the steps climb to the right
            const float stepDepth = 54, stepHeight = 26, tread = 6, nosing = 9, riser = 5;
            var baseHeight = stepDepthAndBase ? 22 : 0;
            var floor = rect.yMax - 18;
            // the steps in the middle, as the other pictures are, with room on the left for the labels there
            var left  = Mathf.Max(rect.x + 70, rect.center.x - 1.5f * stepDepth);

            {
                for (int i = 0; i < 3; i++)
                {
                    var x0 = left + i * stepDepth;
                    var top = floor - baseHeight - (i + 1) * stepHeight;
                    // the solid under the step, down to the floor
                    EditorGUI.DrawRect(new Rect(x0, top, (3 - i) * stepDepth, floor - top), shape);
                    // the riser at its front, and the tread on top, reaching over the step below
                    EditorGUI.DrawRect(new Rect(x0, top, riser, stepHeight), outline);
                    EditorGUI.DrawRect(new Rect(x0 - nosing, top - tread, stepDepth + nosing, tread), outline);
                }
                s_Color = outline;
                // the floor just past the steps, as in the other pictures
                Line(left - nosing - 30, floor, left + 3 * stepDepth + 30, floor);

                s_Color = measure;
                // the second step: its height at its front, its depth over it
                var x1 = left + stepDepth; var top1 = floor - baseHeight - 2 * stepHeight;
                Vertical(x1 - nosing - 14, top1 - tread, top1 - tread + stepHeight, "Step Height", -1);
                if (stepDepthAndBase)
                    Horizontal(x1, x1 + stepDepth, top1 - tread - 12, "Step Depth", toTheLeft: true);
                // the top step's tread: its thickness at the back, its overhang at the front
                var x2 = left + 2 * stepDepth; var top2 = floor - baseHeight - 3 * stepHeight;
                Vertical(x2 + stepDepth + 10, top2 - tread, top2, "Tread Thickness", +1);
                Horizontal(x2 - nosing, x2, top2 - tread - 12, "Tread Front Overhang");
                // the base under the lowest step, and the riser's thickness
                if (stepDepthAndBase)
                    Vertical(left - nosing - 14, floor - baseHeight, floor, "Base Height", -1);
                if (riserThickness)
                    Horizontal(left, left + riser, floor - baseHeight - stepHeight * 0.5f, "Riser Thickness", toTheLeft: true);
            }
        }

        static Color s_Color;

        // only straight lines are drawn: as one pixel wide rects, which the inspector always draws (Handles there not always)
        static void Line(float x0, float y0, float x1, float y1)
        {
            if (x0 == x1) EditorGUI.DrawRect(new Rect(x0, Mathf.Min(y0, y1), 1, Mathf.Abs(y1 - y0) + 1), s_Color);
            else          EditorGUI.DrawRect(new Rect(Mathf.Min(x0, x1), y0, Mathf.Abs(x1 - x0) + 1, 1), s_Color);
        }

        static void Vertical(float x, float from, float to, string name, int side)
        {
            Line(x, from, x, to);
            Line(x - 3, from, x + 3, from);
            Line(x - 3, to, x + 3, to);
            var size = Label.CalcSize(new GUIContent(name));
            var at   = new Rect(side < 0 ? x - size.x - 4 : x + 4, (from + to - size.y) * 0.5f, size.x, size.y);
            Label.Draw(at, name, false, false, false, false);
        }

        static void Horizontal(float from, float to, float y, string name, bool toTheLeft = false)
        {
            Line(from, y, to, y);
            Line(from, y - 3, from, y + 3);
            Line(to, y - 3, to, y + 3);
            var size = Label.CalcSize(new GUIContent(name));
            var at   = toTheLeft ? new Rect(from - size.x - 4, y - size.y * 0.5f, size.x, size.y)
                                 : new Rect((from + to - size.x) * 0.5f, y - size.y - 2, size.x, size.y);
            Label.Draw(at, name, false, false, false, false);
        }
    }
}
