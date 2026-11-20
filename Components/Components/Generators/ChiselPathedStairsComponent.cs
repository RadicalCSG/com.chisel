using Chisel.Core;
using UnityEngine;

namespace Chisel.Components
{
    [ExecuteInEditMode, HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [DisallowMultipleComponent, AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselPathedStairsComponent : ChiselBranchGeneratorComponent<Core.ChiselPathedStairs, ChiselPathedStairsDefinition>
    {
        public const string kNodeTypeName = Core.ChiselPathedStairsDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public Curve2D Shape
        {
            get { return definition.shape; }
            set { if (value == definition.shape) return; definition.shape = value; OnValidateState(); }
        }

        // how high the stairs are; how wide they are is the path's
        public float Height
        {
            get { return definition.settings.stairs.Height; }
            set { if (definition.settings.stairs.Height == value) return; definition.settings.stairs.Height = value; OnValidateState(); }
        }

        // how deep the stairs are from the path, the floor at the top included
        public float Depth
        {
            get { return definition.settings.stairs.Depth; }
            set { if (definition.settings.stairs.Depth == value) return; definition.settings.stairs.Depth = value; OnValidateState(); }
        }

        public float StepHeight
        {
            get { return definition.settings.stairs.stepHeight; }
            set { if (definition.settings.stairs.stepHeight == value) return; definition.settings.stairs.stepHeight = value; OnValidateState(); }
        }

        public float StepDepth
        {
            get { return definition.settings.stairs.stepDepth; }
            set { if (definition.settings.stairs.stepDepth == value) return; definition.settings.stairs.stepDepth = value; OnValidateState(); }
        }

        public float TreadHeight
        {
            get { return definition.settings.stairs.treadHeight; }
            set { if (definition.settings.stairs.treadHeight == value) return; definition.settings.stairs.treadHeight = value; OnValidateState(); }
        }

        public float NosingDepth
        {
            get { return definition.settings.stairs.nosingDepth; }
            set { if (definition.settings.stairs.nosingDepth == value) return; definition.settings.stairs.nosingDepth = value; OnValidateState(); }
        }

        // how far the treads reach out past the sides, only at an open path's start or end without a guard rail
        public float NosingWidth
        {
            get { return definition.settings.stairs.nosingWidth; }
            set { if (definition.settings.stairs.nosingWidth == value) return; definition.settings.stairs.nosingWidth = value; OnValidateState(); }
        }

        public StairsRiserType RiserType
        {
            get { return definition.settings.stairs.riserType; }
            set { if (definition.settings.stairs.riserType == value) return; definition.settings.stairs.riserType = value; OnValidateState(); }
        }

        public float RiserDepth
        {
            get { return definition.settings.stairs.riserDepth; }
            set { if (definition.settings.stairs.riserDepth == value) return; definition.settings.stairs.riserDepth = value; OnValidateState(); }
        }

        public float PlateauHeight
        {
            get { return definition.settings.stairs.plateauHeight; }
            set { if (definition.settings.stairs.plateauHeight == value) return; definition.settings.stairs.plateauHeight = value; OnValidateState(); }
        }

        // the guard rail where an open path starts
        public StairsSideType StartSide
        {
            get { return definition.settings.stairs.leftSide; }
            set { if (definition.settings.stairs.leftSide == value) return; definition.settings.stairs.leftSide = value; OnValidateState(); }
        }

        // the guard rail where an open path ends
        public StairsSideType EndSide
        {
            get { return definition.settings.stairs.rightSide; }
            set { if (definition.settings.stairs.rightSide == value) return; definition.settings.stairs.rightSide = value; OnValidateState(); }
        }

        // the guard rail where two parts of the path meet; a closed path's only ones
        public StairsSideType CornerSide
        {
            get { return definition.settings.cornerSide; }
            set { if (definition.settings.cornerSide == value) return; definition.settings.cornerSide = value; OnValidateState(); }
        }

        // how thick the guard rails are
        public float SideWidth
        {
            get { return definition.settings.stairs.sideWidth; }
            set { if (definition.settings.stairs.sideWidth == value) return; definition.settings.stairs.sideWidth = value; OnValidateState(); }
        }

        // how high a guard rail above the steps reaches over them
        public float SideHeight
        {
            get { return definition.settings.stairs.sideHeight; }
            set { if (definition.settings.stairs.sideHeight == value) return; definition.settings.stairs.sideHeight = value; OnValidateState(); }
        }

        // how far a guard rail below the steps reaches under them
        public float SideDepth
        {
            get { return definition.settings.stairs.sideDepth; }
            set { if (definition.settings.stairs.sideDepth == value) return; definition.settings.stairs.sideDepth = value; OnValidateState(); }
        }

        // how far the guard rails reach out past the steps' fronts
        public float SideFrontOffset
        {
            get { return definition.settings.stairs.sideFrontOffset; }
            set { if (definition.settings.stairs.sideFrontOffset == value) return; definition.settings.stairs.sideFrontOffset = value; OnValidateState(); }
        }

        // how far back the guard rails at an open path's start and end go beside the floor at the top
        public StairsTopSideType TopSide
        {
            get { return definition.settings.stairs.topSide; }
            set { if (definition.settings.stairs.topSide == value) return; definition.settings.stairs.topSide = value; OnValidateState(); }
        }

        public float TopSideLength
        {
            get { return definition.settings.stairs.topSideLength; }
            set { if (definition.settings.stairs.topSideLength == value) return; definition.settings.stairs.topSideLength = value; OnValidateState(); }
        }

        // how far back the guard rails where two parts of the path meet go beside the floor at the top
        public StairsTopSideType CornerTopSide
        {
            get { return definition.settings.cornerTopSide; }
            set { if (definition.settings.cornerTopSide == value) return; definition.settings.cornerTopSide = value; OnValidateState(); }
        }

        public float CornerTopSideLength
        {
            get { return definition.settings.cornerTopSideLength; }
            set { if (definition.settings.cornerTopSideLength == value) return; definition.settings.cornerTopSideLength = value; OnValidateState(); }
        }
        #endregion
    }
}
