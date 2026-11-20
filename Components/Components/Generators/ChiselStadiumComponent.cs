using Chisel.Core;
using UnityEngine;

namespace Chisel.Components
{
    [ExecuteInEditMode, HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [DisallowMultipleComponent, AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselStadiumComponent : ChiselBrushGeneratorComponent<ChiselStadiumDefinition, Core.ChiselStadium>
    {
        public const string kNodeTypeName = Core.ChiselStadiumDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public float Width
        {
            get { return definition.settings.width; }
            set { if (definition.settings.width == value) return; definition.settings.width = value; OnValidateState(); }
        }

        public float Height
        {
            get { return definition.settings.height; }
            set { if (definition.settings.height == value) return; definition.settings.height = value; OnValidateState(); }
        }

        public float Length
        {
            get { return definition.settings.length; }
            set { if (definition.settings.length == value) return; definition.settings.length = value; OnValidateState(); }
        }

        public float TopLength
        {
            get { return definition.settings.topLength; }
            set { if (definition.settings.topLength == value) return; definition.settings.topLength = value; OnValidateState(); }
        }

        public float BottomLength
        {
            get { return definition.settings.bottomLength; }
            set { if (definition.settings.bottomLength == value) return; definition.settings.bottomLength = value; OnValidateState(); }
        }

        public int TopSides
        {
            get { return definition.settings.topSides; }
            set { if (definition.settings.topSides == value) return; definition.settings.topSides = value; OnValidateState(); }
        }

        public int BottomSides
        {
            get { return definition.settings.bottomSides; }
            set { if (definition.settings.bottomSides == value) return; definition.settings.bottomSides = value; OnValidateState(); }
        }
        #endregion
    }
}
