using Chisel.Core;
using UnityEngine;

namespace Chisel.Components
{
    [ExecuteInEditMode, HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [DisallowMultipleComponent, AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselTorusComponent : ChiselBranchGeneratorComponent<Core.ChiselTorus, ChiselTorusDefinition>
    {
        public const string kNodeTypeName = Core.ChiselTorusDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public float OuterDiameter
        {
            get { return definition.settings.outerDiameter; }
            set { if (definition.settings.outerDiameter == value) return; definition.settings.outerDiameter = value; OnValidateState(); }
        }

        // the diameter of its hole: the outer diameter less the tube's width on both sides
        public float InnerDiameter
        {
            get { return definition.settings.InnerDiameter; }
            set { if (definition.settings.InnerDiameter == value) return; definition.settings.InnerDiameter = value; OnValidateState(); }
        }

        public float TubeWidth
        {
            get { return definition.settings.tubeWidth; }
            set { if (definition.settings.tubeWidth == value) return; definition.settings.tubeWidth = value; OnValidateState(); }
        }

        public float TubeHeight
        {
            get { return definition.settings.tubeHeight; }
            set { if (definition.settings.tubeHeight == value) return; definition.settings.tubeHeight = value; OnValidateState(); }
        }

        public float TubeRotation
        {
            get { return definition.settings.tubeRotation; }
            set { if (definition.settings.tubeRotation == value) return; definition.settings.tubeRotation = value; OnValidateState(); }
        }

        public float StartAngle
        {
            get { return definition.settings.startAngle; }
            set { if (definition.settings.startAngle == value) return; definition.settings.startAngle = value; OnValidateState(); }
        }

        public float TotalAngle
        {
            get { return definition.settings.totalAngle; }
            set { if (definition.settings.totalAngle == value) return; definition.settings.totalAngle = value; OnValidateState(); }
        }

        public int HorizontalSegments
        {
            get { return definition.settings.horizontalSegments; }
            set { if (definition.settings.horizontalSegments == value) return; definition.settings.horizontalSegments = value; OnValidateState(); }
        }

        public int VerticalSegments
        {
            get { return definition.settings.verticalSegments; }
            set { if (definition.settings.verticalSegments == value) return; definition.settings.verticalSegments = value; OnValidateState(); }
        }

        public bool FitCircle
        {
            get { return definition.settings.fitCircle; }
            set { if (definition.settings.fitCircle == value) return; definition.settings.fitCircle = value; OnValidateState(); }
        }

        public uint SmoothingGroup
        {
            get { return definition.settings.smoothingGroup; }
            set { if (value == definition.settings.smoothingGroup) return; definition.settings.smoothingGroup = value; OnValidateState(); }
        }
        #endregion
    }
}
