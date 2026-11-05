using UnityEngine;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselTorusDefinition.kNodeTypeName, group: ChiselToolGroups.kBasePrimitives)]
    public sealed class ChiselTorusPlacementTool : ChiselBoundsPlacementTool<ChiselTorusDefinition>
    {
        public int horizontalSegments   = ChiselTorus.DefaultSettings.horizontalSegments;
        public int verticalSegments     = ChiselTorus.DefaultSettings.verticalSegments;

        [ToggleFlags(includeFlags: (int)PlacementFlags.GenerateFromCenterY)]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterXZ;
        public override PlacementFlags PlacementFlags => placement;
        public override bool CenteredOnY => true;

        public override void OnCreate(ref ChiselTorusDefinition definition)
        {
            definition.settings.horizontalSegments = horizontalSegments;
            definition.settings.verticalSegments   = verticalSegments;
        }

        // the height drawn is the tube's, and its tube is as wide as it is high, as far as it fits
        public override void OnUpdate(ref ChiselTorusDefinition definition, Bounds bounds)
        {
            var diameter = Mathf.Abs(bounds.size[(int)Axis.X]);
            var height   = Mathf.Abs(bounds.size[(int)Axis.Y]);
            definition.settings.outerDiameter = diameter;
            definition.settings.tubeHeight    = height;
            definition.settings.tubeWidth     = Mathf.Min(height, diameter * 0.5f);
        }

        public override void OnPaint(IChiselHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderCylinder(bounds, horizontalSegments);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
