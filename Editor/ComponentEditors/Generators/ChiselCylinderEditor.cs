using Chisel.Core;
using Chisel.Components;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselCylinderComponent))]
    public sealed class ChiselCylinderEditor : ChiselGeneratorDefinitionEditor<ChiselCylinderComponent, ChiselCylinderDefinition>
    {
        [MenuItem(kGameObjectMenuNodePath + ChiselCylinderComponent.kNodeTypeName, false, kGameObjectMenuNodePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselCylinderComponent.kNodeTypeName); }

        protected override void OnInspector()
        {
            base.OnInspector();
            KeepLargerDiametersWhenTurnedRound();
        }

        // Elliptical turned off in the inspector keeps the larger diameters, as ChiselCylinder.IsElliptical does
        internal void KeepLargerDiametersWhenTurnedRound()
        {
            TurnOffThroughProperty(nameof(ChiselCylinder.isElliptical), cylinder => cylinder.IsElliptical,
                                   cylinder => cylinder.IsElliptical = false);
        }
    }
}
