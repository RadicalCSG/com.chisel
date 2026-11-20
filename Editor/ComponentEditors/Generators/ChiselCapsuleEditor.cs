using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselCapsuleComponent))]
    public sealed class ChiselCapsuleEditor : ChiselGeneratorDefinitionEditor<ChiselCapsuleComponent, ChiselCapsuleDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselCapsuleComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselCapsuleComponent.kNodeTypeName); }

        protected override void OnInspector()
        {
            base.OnInspector();
            KeepLargerDiameterWhenTurnedRound();
        }

        // Elliptical turned off in the inspector keeps the larger diameter, as ChiselCapsule.IsElliptical does
        internal void KeepLargerDiameterWhenTurnedRound()
        {
            TurnOffThroughProperty(nameof(ChiselCapsule.isElliptical), capsule => capsule.IsElliptical,
                                   capsule => capsule.IsElliptical = false);
        }
    }
}
