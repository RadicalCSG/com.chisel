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

        // The inspector writes Elliptical straight into the settings: turned off, each cylinder that was elliptical keeps
        // the larger of its diameters, as ChiselCylinder.IsElliptical does
        internal void KeepLargerDiametersWhenTurnedRound()
        {
            var elliptical = SettingsProperty(nameof(ChiselCylinder.isElliptical));
            if (elliptical == null || elliptical.hasMultipleDifferentValues || elliptical.boolValue)
                return;

            // not applied yet, so the cylinders still hold what they were
            bool[] wasElliptical = null;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is ChiselCylinderComponent cylinder && cylinder.IsElliptical)
                {
                    wasElliptical ??= new bool[targets.Length];
                    wasElliptical[i] = true;
                }
            }
            if (wasElliptical == null)
                return;

            serializedObject.ApplyModifiedProperties();
            for (int i = 0; i < targets.Length; i++)
            {
                if (!wasElliptical[i])
                    continue;
                var cylinder = (ChiselCylinderComponent)targets[i];
                Undo.RecordObject(cylinder, "Elliptical");
                cylinder.definition.settings.KeepLargerDiameters();
                cylinder.OnValidate();
            }
            serializedObject.Update();
        }
    }
}
