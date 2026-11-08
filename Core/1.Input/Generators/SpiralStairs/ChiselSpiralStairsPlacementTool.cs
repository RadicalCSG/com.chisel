using UnityEngine;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselSpiralStairsDefinition.kNodeTypeName, group: ChiselToolGroups.kStairs)]
    public sealed class ChiselSpiralStairsPlacementTool : ChiselBoundsPlacementTool<ChiselSpiralStairsDefinition>
    {
        public float    stepHeight      = ChiselSpiralStairs.DefaultSettings.stepHeight;
        public int      outerSegments   = ChiselSpiralStairs.DefaultSettings.outerSegments;
        
        [ToggleFlags(includeFlags: (int)(PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.GenerateFromCenterXZ | PlacementFlags.AlwaysFaceUp | PlacementFlags.SameLengthXZ;
        public override PlacementFlags PlacementFlags => placement;

        public override void OnCreate(ref ChiselSpiralStairsDefinition definition) 
        {
            definition.settings.stepHeight       = stepHeight;
            definition.settings.outerSegments    = outerSegments;
        }

        // the stairs are built around the frame's middle, up from it (the box drawn is around it): a box drawn out of a
        // wall, turned to stand up, can be drawn out either way
        public override void OnUpdate(ref ChiselSpiralStairsDefinition definition, Bounds bounds)
        {
            definition.settings.height			= bounds.size[(int)Axis.Y];
            definition.settings.outerDiameter	= Mathf.Abs(bounds.size[(int)Axis.X]);
        }

        public override void OnPaint(IChiselHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderCylinder(bounds, outerSegments);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
