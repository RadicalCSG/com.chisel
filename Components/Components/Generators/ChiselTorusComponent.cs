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

        // TODO: add all properties of ChiselTorusDefinition

        public uint SmoothingGroup
        {
            get { return definition.settings.smoothingGroup; }
            set { if (value == definition.settings.smoothingGroup) return; definition.settings.smoothingGroup = value; OnValidateState(); }
        }
    }
}
