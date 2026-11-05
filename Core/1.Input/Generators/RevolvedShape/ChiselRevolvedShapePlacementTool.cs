using UnityEngine;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselRevolvedShapeDefinition.kNodeTypeName, group: ChiselToolGroups.kFreeForm)]
    public sealed class ChiselRevolvedShapePlacementTool : ChiselBoundsPlacementTool<ChiselRevolvedShapeDefinition>
    {
        public int revolveSegments = ChiselRevolvedShape.DefaultSettings.revolveSegments;

        [ToggleFlags(includeFlags: (int)PlacementFlags.GenerateFromCenterY)]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterXZ;
        public override PlacementFlags PlacementFlags => placement;

        public override void OnCreate(ref ChiselRevolvedShapeDefinition definition)
        {
            definition.settings.revolveSegments = revolveSegments;
        }

        // its default shape, made as wide and high as drawn
        public override void OnUpdate(ref ChiselRevolvedShapeDefinition definition, Bounds bounds)
        {
            definition.shape = FitShape(ChiselRevolvedShapeDefinition.DefaultShape, bounds);
        }

        // the shape's x is its distance from the axis it turns around, its y its height: from -1..1 to as wide as the
        // bounds and from 0 to their height
        public static Curve2D FitShape(Curve2D shape, Bounds bounds)
        {
            var radius = Mathf.Abs(bounds.size[(int)Axis.X]) * 0.5f;
            var height = bounds.size[(int)Axis.Y];
            var fitted = new Curve2D(shape);
            for (int i = 0; i < fitted.controlPoints.Length; i++)
            {
                var position = fitted.controlPoints[i].position;
                fitted.controlPoints[i].position = new Vector2(position.x * radius, (position.y + 1) * 0.5f * height);
            }
            return fitted;
        }

        public override void OnPaint(IChiselHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderCylinder(bounds, revolveSegments);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
