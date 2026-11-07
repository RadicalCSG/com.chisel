using Chisel.Core;
using Chisel.Components;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselPathedStairsComponent))]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed class ChiselPathedStairsEditor : ChiselGeneratorDefinitionEditor<ChiselPathedStairsComponent, ChiselPathedStairsDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselPathedStairsComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselPathedStairsComponent.kNodeTypeName); }

        protected override bool ShowsContents => false;

        const string kStairs = nameof(ChiselPathedStairs.stairs) + ".";

        static readonly GUIContent kStartSideContent         = new("Start",            "The guard rail where an open path starts");
        static readonly GUIContent kEndSideContent           = new("End",              "The guard rail where an open path ends");
        static readonly GUIContent kCornerSideContent        = new("Inner",            "The guard rail where two parts of the path meet, on the plane between them");
        // a closed path has only these, so there's nothing to tell them apart from
        static readonly GUIContent kAllSidesContent          = new("Placement",        "The guard rail where two parts of the path meet, on the plane between them");
        static readonly GUIContent kAllTopContent            = new("Side End",         "How far back the guard rails go beside the floor at the top");
        static readonly GUIContent kAllTopLengthContent      = new("Side End Length",  "How far back from the top step the guard rails go, when set");
        static readonly GUIContent kHeightContent            = new("Height",           "How high the stairs are, from the object's position to the top. The steps keep their size: as many as fit, with the base taking up what's left");
        static readonly GUIContent kDepthContent             = new("Depth",            "How deep the stairs are from the path, the floor at the top included");
        static readonly GUIContent kThicknessContent         = new("Thickness",        "How thick the guard rails are");
        static readonly GUIContent kHeightAboveContent       = new("Height Above",     "How high a guard rail above the steps reaches over them");
        static readonly GUIContent kDepthBelowContent        = new("Depth Below",      "How far a guard rail below the steps reaches under them");
        static readonly GUIContent kFrontOffsetContent       = new("Front Offset",     "How far the guard rails reach out past the steps' fronts");
        static readonly GUIContent kClosedContent            = new("Closed Loop",      "Whether the path goes round back to where it starts; then every guard rail is a corner's");
        static readonly GUIContent kControlPointsContent     = new("Path Points",      "The points the path goes through");
        static readonly GUIContent kStartEndTopContent       = new("Side End",         "How far back the guard rails at an open path's start and end go beside the floor at the top");
        static readonly GUIContent kStartEndTopLengthContent = new("Side End Length",  "How far back from the top step the guard rails at an open path's start and end go, when set");
        static readonly GUIContent kCornerTopContent         = new("Inner End",        "How far back the guard rails where two parts of the path meet go beside the floor at the top");
        static readonly GUIContent kCornerTopLengthContent   = new("Inner End Length", "How far back from the top step the guard rails where two parts of the path meet go, when set");

        // as the linear stairs each part of the path is: the steps with their settings under them, the guard rails
        // (only at an open path's ends) with theirs, then the rest
        protected override void OnInspector()
        {
            OnGroupedInspector(() =>
            {
                ChiselLinearStairsDiagram.Draw();
                DefinitionField(nameof(ChiselPathedStairsDefinition.shape) + "." + nameof(Curve2D.closed), kClosedContent);
                SettingsField(nameof(ChiselPathedStairs.curveSegments));
                DefinitionField(nameof(ChiselPathedStairsDefinition.shape) + "." + nameof(Curve2D.controlPoints), kControlPointsContent);
                // the stairs' height and depth, kept in their bounds; how wide they are is the path's
                BoundsSizeField(kStairs + nameof(ChiselLinearStairs.bounds), 1, kHeightContent);
                BoundsSizeField(kStairs + nameof(ChiselLinearStairs.bounds), 2, kDepthContent);
                SettingsField(kStairs + nameof(ChiselLinearStairs.stepHeight));
                SettingsField(kStairs + nameof(ChiselLinearStairs.stepDepth));
                SettingsField(kStairs + nameof(ChiselLinearStairs.treadHeight));
                SettingsField(kStairs + nameof(ChiselLinearStairs.nosingDepth));
                // the treads reach out past the sides only at an open path's start or end without a guard rail
                var openEnds = false;
                foreach (var target in targets)
                    openEnds |= target is ChiselPathedStairsComponent stairs && stairs.definition.shape != null && !stairs.definition.shape.closed &&
                                (stairs.definition.settings.stairs.leftSide  == StairsSideType.None ||
                                 stairs.definition.settings.stairs.rightSide == StairsSideType.None);
                SettingsField(kStairs + nameof(ChiselLinearStairs.nosingWidth), visible: openEnds);
                SettingsField(kStairs + nameof(ChiselLinearStairs.riserType));
                SettingsField(kStairs + nameof(ChiselLinearStairs.riserDepth));
                SettingsField(kStairs + nameof(ChiselLinearStairs.plateauHeight));
                EditorGUILayout.Space();

                ChiselStairsSideWallDiagram.Draw();

                EditorGUILayout.LabelField("Guard Rails", EditorStyles.boldLabel);
                // a closed path has no start or end; what all the guard rails share is shown when any of them is there
                bool anyOpen = false, anyStartEnd = false, anyInner = false;
                foreach (var target in targets)
                {
                    if (target is not ChiselPathedStairsComponent stairs || stairs.definition.shape == null)
                        continue;
                    var settings = stairs.definition.settings;
                    var open     = !stairs.definition.shape.closed;
                    anyOpen     |= open;
                    anyStartEnd |= open && (settings.stairs.leftSide != StairsSideType.None || settings.stairs.rightSide != StairsSideType.None);
                    anyInner    |= settings.cornerSide != StairsSideType.None;
                }
                var anyRail = anyStartEnd || anyInner;
                // drawn as they are here, not as the linear stairs' own fields, which hide with its sides and are named for them
                SettingsValueField(kStairs + nameof(ChiselLinearStairs.leftSide),  kStartSideContent, visible: anyOpen);
                SettingsValueField(kStairs + nameof(ChiselLinearStairs.rightSide), kEndSideContent,   visible: anyOpen);
                SettingsValueField(nameof(ChiselPathedStairs.cornerSide), anyOpen ? kCornerSideContent : kAllSidesContent);
                SettingsValueField(kStairs + nameof(ChiselLinearStairs.sideWidth),       kThicknessContent,   visible: anyRail);
                SettingsValueField(kStairs + nameof(ChiselLinearStairs.sideHeight),      kHeightAboveContent, visible: anyRail);
                SettingsValueField(kStairs + nameof(ChiselLinearStairs.sideDepth),       kDepthBelowContent,  visible: anyRail);
                SettingsValueField(kStairs + nameof(ChiselLinearStairs.sideFrontOffset), kFrontOffsetContent, visible: anyRail);
                SettingsValueField(kStairs + nameof(ChiselLinearStairs.topSide), kStartEndTopContent, visible: anyStartEnd);
                SettingsValueField(kStairs + nameof(ChiselLinearStairs.topSideLength), kStartEndTopLengthContent,
                                   visible: anyStartEnd && SettingsIs(kStairs + nameof(ChiselLinearStairs.topSide), (int)StairsTopSideType.Length));
                SettingsValueField(nameof(ChiselPathedStairs.cornerTopSide), anyOpen ? kCornerTopContent : kAllTopContent, visible: anyInner);
                SettingsValueField(nameof(ChiselPathedStairs.cornerTopSideLength), anyOpen ? kCornerTopLengthContent : kAllTopLengthContent,
                                   visible: anyInner && SettingsIs(nameof(ChiselPathedStairs.cornerTopSide), (int)StairsTopSideType.Length));
                EditorGUILayout.Space();
            });
        }
    }
}
