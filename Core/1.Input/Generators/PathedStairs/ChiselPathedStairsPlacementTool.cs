using Unity.Mathematics;

namespace Chisel.Core
{
    // the path is drawn, and the stairs go up along it as high as drawn
    [ChiselPlacementTool(name: ChiselPathedStairsDefinition.kNodeTypeName, group: ChiselToolGroups.kStairs)]
    public sealed class ChiselPathedStairsPlacementTool : ChiselShapePlacementTool<ChiselPathedStairsDefinition>
    {
        public override void OnCreate(ref ChiselPathedStairsDefinition definition, Curve2D shape)
        {
            definition.shape = new Curve2D(shape);
        }

        public override void OnUpdate(ref ChiselPathedStairsDefinition definition, float height)
        {
            ref var bounds = ref definition.settings.stairs.bounds;
            var min = bounds.Min;
            var max = bounds.Max;
            min.y = math.min(0, height);
            max.y = math.max(0, height);
            bounds = BoundsExtensions.CreateAABB(min: min, max: max);
        }

        public override void OnPaint(IChiselHandleRenderer renderer, Curve2D shape, float height)
        {
            renderer.RenderShape(shape, height);
        }
    }
}
