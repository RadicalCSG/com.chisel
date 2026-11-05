using UnityEngine;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselStadiumDefinition.kNodeTypeName, group: ChiselToolGroups.kBasePrimitives)]
    public sealed class ChiselStadiumPlacementTool : ChiselBoundsPlacementTool<ChiselStadiumDefinition>
    {
        public int topSides     = ChiselStadium.DefaultSettings.topSides;
        public int bottomSides  = ChiselStadium.DefaultSettings.bottomSides;

        [ToggleFlags(includeFlags: (int)(PlacementFlags.GenerateFromCenterY | PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.GenerateFromCenterXZ;
        public override PlacementFlags PlacementFlags => placement;
        public override bool AlongLongSide => true;

        public override void OnCreate(ref ChiselStadiumDefinition definition)
        {
            definition.settings.topSides    = topSides;
            definition.settings.bottomSides = bottomSides;
        }

        // its rounded ends are as long as half its width, so they are half circles
        public override void OnUpdate(ref ChiselStadiumDefinition definition, Bounds bounds)
        {
            var width  = Mathf.Abs(bounds.size[(int)Axis.X]);
            var length = Mathf.Abs(bounds.size[(int)Axis.Z]);
            var ends   = Mathf.Min(width * 0.5f, length * 0.5f);
            definition.settings.width        = width;
            definition.settings.height       = bounds.size[(int)Axis.Y];
            definition.settings.length       = length;
            definition.settings.topLength    = ends;
            definition.settings.bottomLength = ends;
        }

        public override void OnPaint(IChiselHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderBox(bounds);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
