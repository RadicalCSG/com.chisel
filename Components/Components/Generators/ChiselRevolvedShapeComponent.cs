using Chisel.Core;
using UnityEngine;

namespace Chisel.Components
{
    [ExecuteInEditMode, HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [DisallowMultipleComponent, AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselRevolvedShapeComponent : ChiselBranchGeneratorComponent<Core.ChiselRevolvedShape, ChiselRevolvedShapeDefinition>
    {
        public const string kNodeTypeName = Core.ChiselRevolvedShapeDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public Curve2D Shape
        {
            get { return definition.shape; }
            set { if (value == definition.shape) return; definition.shape = value; OnValidateState(); }
        }

        // how many parts each curved span of its shape is made of
        public int CurveSegments
        {
            get { return definition.settings.curveSegments; }
            set { if (definition.settings.curveSegments == value) return; definition.settings.curveSegments = value; OnValidateState(); }
        }

        // how many parts it is made of going round
        public int RevolveSegments
        {
            get { return definition.settings.revolveSegments; }
            set { if (definition.settings.revolveSegments == value) return; definition.settings.revolveSegments = value; OnValidateState(); }
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
        #endregion
    }
}
