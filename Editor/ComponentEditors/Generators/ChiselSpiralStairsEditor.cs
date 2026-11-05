using Chisel.Core;
using Chisel.Components;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselSpiralStairsComponent))]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed class ChiselSpiralStairsEditor : ChiselGeneratorDefinitionEditor<ChiselSpiralStairsComponent, ChiselSpiralStairsDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselSpiralStairsComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselSpiralStairsComponent.kNodeTypeName); }

        protected override bool ShowsContents => false;

        static readonly GUIContent kBottomOffsetContent = new("Bottom Offset", "How far above the object's position the bottom of the stairs is; dragging the bottom handle changes it");

        // each picture with the settings it shows under it, then the rest
        protected override void OnInspector()
        {
            OnGroupedInspector(() =>
            {
                ChiselSpiralStairsDiagram.Draw();
                SettingsField(nameof(ChiselSpiralStairs.outerDiameter));
                SettingsField(nameof(ChiselSpiralStairs.innerDiameter));
                SettingsField(nameof(ChiselSpiralStairs.startAngle));
                SettingsField(nameof(ChiselSpiralStairs.rotation));
                SettingsField(nameof(ChiselSpiralStairs.height));
                // only the height of the origin is used
                SettingsField(nameof(ChiselSpiralStairs.origin) + ".y", kBottomOffsetContent);
                SettingsField(nameof(ChiselSpiralStairs.outerSegments));
                SettingsField(nameof(ChiselSpiralStairs.innerSegments));
                SettingsField(nameof(ChiselSpiralStairs.centerType));
                SettingsField(nameof(ChiselSpiralStairs.columnThickness));
                SettingsField(nameof(ChiselSpiralStairs.columnSmoothingGroup));
                EditorGUILayout.Space();

                // the steps' own sizes, seen from the side; the riser's thickness only counts for thin risers
                var thinRisers = false;
                foreach (var target in targets)
                    thinRisers |= target is ChiselSpiralStairsComponent stairs && stairs.definition.settings.riserType == StairsRiserType.ThinRiser;
                ChiselLinearStairsDiagram.Draw(stepDepthAndBase: false, riserThickness: thinRisers);
                SettingsField(nameof(ChiselSpiralStairs.stepHeight));
                SettingsField(nameof(ChiselSpiralStairs.treadHeight));
                SettingsField(nameof(ChiselSpiralStairs.nosingDepth));
                SettingsField(nameof(ChiselSpiralStairs.nosingWidth));
                SettingsField(nameof(ChiselSpiralStairs.riserType));
                SettingsField(nameof(ChiselSpiralStairs.riserDepth));
                SettingsField(nameof(ChiselSpiralStairs.bottomSmoothingGroup));
                EditorGUILayout.Space();

                // the side walls, seen from the side, with whether there are any under it
                ChiselStairsSideWallDiagram.Draw(topFloor: false);
                EditorGUILayout.LabelField("Guard Rails", EditorStyles.boldLabel);
                SettingsField(nameof(ChiselSpiralStairs.outerSide));
                SettingsField(nameof(ChiselSpiralStairs.innerSide));
                SettingsField(nameof(ChiselSpiralStairs.sideWidth));
                SettingsField(nameof(ChiselSpiralStairs.sideHeight));
                SettingsField(nameof(ChiselSpiralStairs.sideDepth));
                SettingsField(nameof(ChiselSpiralStairs.sideFrontOffset));
                SettingsField(nameof(ChiselSpiralStairs.sideSmoothingGroup));
                EditorGUILayout.Space();
            });
        }
    }

    // What the side walls' values measure: the steps from the side with a wall beside them, above and below the line
    // through the fronts of the treads, and the wall seen end on for its thickness
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    static class ChiselStairsSideWallDiagram
    {
        const int kWidth  = 300;
        const int kHeight = 130;

        // in pixels, y going down: three steps climbing to the right, and the wall's heights
        const float kStepWidth = 46, kStepHeight = 22, kLeft = 34, kFloor = 122, kAbove = 30, kBelow = 22;
        // the wall's piece in front of the first step, and (with a floor at the top) the floor and how far the wall goes
        // along it
        const float kFront = 16, kTopFloor = 44, kTopWall = 28;
        // the end-on view: the side of a step, and the wall against it
        const float kEndLeft = 250, kEndStep = 22, kEndWall = 9, kEndTop = 34, kEndBottom = 104;

        const float kRight = kLeft + 3 * kStepWidth;
        const float kTop   = kFloor - 3 * kStepHeight;

        static readonly Texture2D[] s_Pictures = new Texture2D[2];
        static readonly bool[]      s_PicturesArePro = new bool[2];

        static GUIStyle s_Label;
        static GUIStyle Label => s_Label ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };

        // the line through the fronts of the treads
        static float Line(float x) { return kFloor - kStepHeight - (x - kLeft) * (kStepHeight / kStepWidth); }

        // with a floor at the top, how far the wall goes along it is shown too
        public static void Draw(bool topFloor = true)
        {
            // room above it for the names over its highest measures
            const float kLabelRoom = 20;
            var rect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 40, kHeight + 6 + kLabelRoom);
            if (Event.current.type != EventType.Repaint)
                return;
            var measure = EditorGUIUtility.isProSkin ? new Color(0.45f, 0.75f, 1f) : new Color(0.1f, 0.35f, 0.75f);
            var picture = new Rect(rect.center.x - kWidth * 0.5f, rect.y + kLabelRoom, kWidth, kHeight);
            GUI.DrawTexture(picture, Picture(topFloor));

            // how far the wall reaches out in front of the first step
            var frontTop = Line(kLeft) - kAbove;
            Horizontal(picture, kLeft - kFront, kLeft, frontTop - 6, measure);
            Text(picture, new Vector2(kLeft - kFront * 0.5f, frontTop - 16), "Front Offset", measure, right: false, centered: true);

            // how far back from the top step the wall goes beside the floor there
            if (topFloor)
            {
                Horizontal(picture, kRight, kRight + kTopWall, Line(kRight) - kAbove - 6, measure);
                Text(picture, new Vector2(kRight + kTopWall * 0.5f, Line(kRight) - kAbove - 16), "End Length", measure, right: false, centered: true);
            }

            // the height above the line, and how far below the steps the wall reaches
            var x = kLeft + kStepWidth * 2.4f;
            Vertical(picture, x, Line(x) - kAbove, Line(x), measure);
            Text(picture, new Vector2(x - 4, Line(x) - kAbove * 0.5f), "Height Above", measure, right: false);
            // straight on down from the line to the wall's bottom, its name beside it
            Vertical(picture, x, Line(x), Line(x) + kStepHeight + kBelow, measure);
            Text(picture, new Vector2(x + 4, Line(x) + (kStepHeight + kBelow) * 0.3f), "Depth Below", measure, right: true);

            // the thickness, over the wall seen end on
            var wallLeft = kEndLeft + kEndStep;
            Horizontal(picture, wallLeft, wallLeft + kEndWall, kEndTop - 6, measure);
            Text(picture, new Vector2(wallLeft + kEndWall * 0.5f, kEndTop - 16), "Thickness", measure, right: false, centered: true);
        }

        static void Vertical(Rect picture, float x, float from, float to, Color color)
        {
            EditorGUI.DrawRect(new Rect(picture.x + x, picture.y + from, 1, to - from + 1), color);
            EditorGUI.DrawRect(new Rect(picture.x + x - 3, picture.y + from, 7, 1), color);
            EditorGUI.DrawRect(new Rect(picture.x + x - 3, picture.y + to, 7, 1), color);
        }

        static void Horizontal(Rect picture, float from, float to, float y, Color color)
        {
            EditorGUI.DrawRect(new Rect(picture.x + from, picture.y + y, to - from + 1, 1), color);
            EditorGUI.DrawRect(new Rect(picture.x + from, picture.y + y - 3, 1, 7), color);
            EditorGUI.DrawRect(new Rect(picture.x + to, picture.y + y - 3, 1, 7), color);
        }

        static void Text(Rect picture, Vector2 at, string text, Color color, bool right, bool centered = false)
        {
            var content = new GUIContent(text);
            var size    = Label.CalcSize(content);
            var x       = centered ? at.x - size.x * 0.5f : right ? at.x : at.x - size.x;
            var rect    = new Rect(picture.x + x, picture.y + at.y - size.y * 0.5f, size.x, size.y);
            var back    = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f, 0.85f) : new Color(0.76f, 0.76f, 0.76f, 0.85f);
            EditorGUI.DrawRect(rect, back);
            var previous = Label.normal.textColor;
            Label.normal.textColor = color;
            Label.Draw(rect, content, false, false, false, false);
            Label.normal.textColor = previous;
        }

        // drawn once per skin, pixel by pixel, with and without a floor at the top
        static Texture2D Picture(bool topFloor)
        {
            var which = topFloor ? 1 : 0;
            if (s_Pictures[which] && s_PicturesArePro[which] == EditorGUIUtility.isProSkin)
                return s_Pictures[which];
            var isPro = s_PicturesArePro[which] = EditorGUIUtility.isProSkin;

            var text    = EditorStyles.label.normal.textColor;
            var step    = new Color(text.r, text.g, text.b, 0.18f);
            var line    = new Color(text.r, text.g, text.b, 0.65f);
            var measure = isPro ? new Color(0.45f, 0.75f, 1f) : new Color(0.1f, 0.35f, 0.75f);
            var wall    = new Color(measure.r, measure.g, measure.b, 0.22f);
            var pixels  = new Color[kWidth * kHeight];

            for (int py = 0; py < kHeight; py++)
            for (int px = 0; px < kWidth; px++)
            {
                // texture rows go up, the picture is worked out with y going down
                var x     = px + 0.5f;
                var y     = kHeight - 1 - py + 0.5f;
                var color = Color.clear;

                // the steps: each one solid down to the floor
                var right = kLeft + 3 * kStepWidth;
                if (x >= kLeft && x <= right && y <= kFloor)
                {
                    var index = Mathf.Min(2, Mathf.FloorToInt((x - kLeft) / kStepWidth));
                    var top   = kFloor - (index + 1) * kStepHeight;
                    if (y >= top)
                        color = step;
                    if (Mathf.Abs(y - top) < 0.7f || Mathf.Abs(x - (kLeft + index * kStepWidth)) < 0.7f && y >= top)
                        color = line;
                }
                // the floor at the top, solid down to the floor
                if (topFloor && x > right && x <= right + kTopFloor && y <= kFloor && y >= kTop)
                    color = (Mathf.Abs(y - kTop) < 0.7f || Mathf.Abs(x - (right + kTopFloor)) < 0.7f) ? line : step;
                if (Mathf.Abs(y - kFloor) < 0.7f && x >= 4 && x <= kEndLeft - 12)
                    color = line;

                // the wall, above the line through the fronts and from a step under it on down, over the steps it is
                // beside, never below the floor; straight on in front of the first step, and on from its end along the floor at the top
                // as far as it goes
                var alongTop = topFloor && x > right && x <= right + kTopWall;
                if ((x >= kLeft - kFront && x <= right) || alongTop)
                {
                    var l = alongTop ? Line(right) : (x < kLeft ? Line(kLeft) : Line(x));
                    var bottom = Mathf.Min(kFloor, l + kStepHeight + kBelow);
                    if (y >= l - kAbove && y <= bottom)
                        color = color.a > 0 ? Color.Lerp(color, new Color(wall.r, wall.g, wall.b, color.a + 0.1f), 0.5f) : wall;
                    if (Mathf.Abs(y - (l - kAbove)) < 0.7f || Mathf.Abs(y - bottom) < 0.7f)
                        color = measure;
                }
                // the line through the fronts of the treads, dashed
                if (x >= kLeft && x <= right && Mathf.Abs(y - Line(x)) < 0.7f && ((int)(x / 4) & 1) == 0)
                    color = line;

                // seen end on: the side of a step, and the wall against it reaching above and below
                var stepTop = kEndTop + kAbove;
                if (x >= kEndLeft && x < kEndLeft + kEndStep && y >= stepTop && y <= stepTop + kStepHeight)
                    color = (Mathf.Abs(y - stepTop) < 0.7f || Mathf.Abs(y - (stepTop + kStepHeight)) < 0.7f || Mathf.Abs(x - kEndLeft) < 0.7f) ? line : step;
                var wallLeft = kEndLeft + kEndStep;
                if (x >= wallLeft && x <= wallLeft + kEndWall && y >= kEndTop && y <= kEndBottom)
                    color = (Mathf.Abs(x - wallLeft) < 0.7f || Mathf.Abs(x - (wallLeft + kEndWall)) < 0.7f || Mathf.Abs(y - kEndTop) < 0.7f || Mathf.Abs(y - kEndBottom) < 0.7f) ? measure : wall;

                pixels[py * kWidth + px] = color;
            }

            if (!s_Pictures[which])
                s_Pictures[which] = new Texture2D(kWidth, kHeight, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            s_Pictures[which].SetPixels(pixels);
            s_Pictures[which].Apply();
            return s_Pictures[which];
        }
    }

    // What the spiral stairs' values measure: a view from above in the inspector
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    static class ChiselSpiralStairsDiagram
    {
        const int   kSize       = 140;          // the picture, in pixels
        const float kOuter      = 60;           // its radii, in pixels
        const float kInner      = 18;
        const float kStart      = 40;           // the angles shown, in degrees, clockwise from the top
        const float kTurn       = 270;
        const int   kSteps      = 9;

        static Texture2D s_Picture;
        static bool      s_PictureIsPro;

        static GUIStyle s_Label;
        static GUIStyle Label => s_Label ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };

        // The stairs from above: the steps around the open middle, where they start and how far they turn, and the two
        // diameters marked under it
        public static void Draw()
        {
            var rect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 40, kSize + 34);
            if (Event.current.type != EventType.Repaint)
                return;

            var measure = EditorGUIUtility.isProSkin ? new Color(0.45f, 0.75f, 1f) : new Color(0.1f, 0.35f, 0.75f);
            var picture = new Rect(rect.center.x - kSize * 0.5f, rect.y, kSize, kSize);
            GUI.DrawTexture(picture, Picture());

            var center = picture.center;
            // where the stairs start, and how far they turn
            var startAt = center + Direction(kStart) * (kOuter + 14);
            // the turn's name beside its arc on the left, away from the diameter under the picture
            var turnAt  = center + Direction(270) * (kOuter + 8);
            Text(startAt + new Vector2(30, 0),  "Start Angle", measure);
            Text(turnAt  + new Vector2(-37, 0), "Turn Angle", measure);

            // the two diameters under the picture
            var y = picture.yMax + 8;
            Horizontal(center.x - kOuter, center.x + kOuter, y, measure);
            Text(new Vector2(center.x, y + 9), "Outer Diameter", measure);
            Horizontal(center.x - kInner, center.x + kInner, center.y, measure);
            Text(new Vector2(center.x, center.y - 9), "Inner Diameter", measure);
        }

        // clockwise from the top, as seen from above
        static Vector2 Direction(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
        }

        static void Horizontal(float from, float to, float y, Color color)
        {
            EditorGUI.DrawRect(new Rect(from, y, to - from + 1, 1), color);
            EditorGUI.DrawRect(new Rect(from, y - 3, 1, 7), color);
            EditorGUI.DrawRect(new Rect(to, y - 3, 1, 7), color);
        }

        static void Text(Vector2 at, string text, Color color)
        {
            var content = new GUIContent(text);
            var size    = Label.CalcSize(content);
            var back    = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f, 0.85f) : new Color(0.76f, 0.76f, 0.76f, 0.85f);
            var rect    = new Rect(at.x - size.x * 0.5f, at.y - size.y * 0.5f, size.x, size.y);
            EditorGUI.DrawRect(rect, back);
            var previous = Label.normal.textColor;
            Label.normal.textColor = color;
            Label.Draw(rect, content, false, false, false, false);
            Label.normal.textColor = previous;
        }

        // drawn once per skin, pixel by pixel
        static Texture2D Picture()
        {
            if (s_Picture && s_PictureIsPro == EditorGUIUtility.isProSkin)
                return s_Picture;
            s_PictureIsPro = EditorGUIUtility.isProSkin;

            var text    = EditorStyles.label.normal.textColor;
            var step    = new Color(text.r, text.g, text.b, 0.18f);
            var line    = new Color(text.r, text.g, text.b, 0.65f);
            var measure = s_PictureIsPro ? new Color(0.45f, 0.75f, 1f) : new Color(0.1f, 0.35f, 0.75f);
            var pixels  = new Color[kSize * kSize];
            var center  = new Vector2(kSize * 0.5f, kSize * 0.5f);
            var perStep = kTurn / kSteps;

            for (int py = 0; py < kSize; py++)
            for (int px = 0; px < kSize; px++)
            {
                // texture rows go up, the picture is seen with y going down
                var p        = new Vector2(px + 0.5f, kSize - 1 - py + 0.5f) - center;
                var radius   = p.magnitude;
                var degrees  = (Mathf.Atan2(p.x, -p.y) * Mathf.Rad2Deg + 360) % 360;
                var turned   = (degrees - kStart + 360) % 360;
                var color    = Color.clear;

                var onSteps  = radius >= kInner && radius <= kOuter && turned <= kTurn;
                if (onSteps)
                {
                    // every other step a little darker, so they read apart
                    var index = Mathf.FloorToInt(turned / perStep);
                    color = step;
                    if ((index & 1) == 1)
                        color.a *= 1.6f;
                    // the edges between steps
                    var fromEdge = Mathf.Abs(turned - Mathf.Round(turned / perStep) * perStep) * Mathf.Deg2Rad * radius;
                    if (fromEdge < 0.7f)
                        color = line;
                }
                // the outside and the open middle
                if (Mathf.Abs(radius - kOuter) < 0.7f || Mathf.Abs(radius - kInner) < 0.7f)
                    color = line;
                // the start, from the center outwards, and the turn as an arc outside the steps
                if (radius > 0 && radius <= kOuter + 10 && Mathf.Abs(Mathf.DeltaAngle(degrees, kStart)) * Mathf.Deg2Rad * radius < 0.8f)
                    color = measure;
                if (Mathf.Abs(radius - (kOuter + 6)) < 0.8f && turned <= kTurn)
                    color = measure;
                pixels[py * kSize + px] = color;
            }

            if (!s_Picture)
                s_Picture = new Texture2D(kSize, kSize, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            s_Picture.SetPixels(pixels);
            s_Picture.Apply();
            return s_Picture;
        }
    }
}
