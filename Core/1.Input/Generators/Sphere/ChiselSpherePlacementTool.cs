using System;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselSphereDefinition.kNodeTypeName, group: ChiselToolGroups.kBasePrimitives)]
    public sealed class ChiselSpherePlacementTool : ChiselBoundsPlacementTool<ChiselSphereDefinition>
    {
        public int  horizontalSegments  = ChiselSphere.DefaultSettings.horizontalSegments;
        public int  verticalSegments    = ChiselSphere.DefaultSettings.verticalSegments;

        [ToggleFlags]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.HeightEqualsXZ | PlacementFlags.GenerateFromCenterXZ |
                                            (ChiselSphere.DefaultSettings.generateFromCenter ? PlacementFlags.GenerateFromCenterY : (PlacementFlags)0);
        public override PlacementFlags PlacementFlags => placement;

        [NonSerialized] Vector3[] previewVertices;

        public override void OnCreate(ref ChiselSphereDefinition definition)
        {
            definition.settings.verticalSegments     = verticalSegments;
            definition.settings.horizontalSegments   = horizontalSegments;
            definition.settings.generateFromCenter   = false;
        }

        public override void OnUpdate(ref ChiselSphereDefinition definition, Bounds bounds)
        {
            definition.settings.diameterXYZ = bounds.size;
        }

        // the sphere that fills the box, outlined as when it's edited
        public override void OnPaint(IChiselHandleRenderer renderer, Bounds bounds)
        {
            var sides = math.max(horizontalSegments, 3);
            BrushMeshFactory.CreateSphereVertices(bounds.size, 0, false, sides, math.max(verticalSegments, 2), ref previewVertices);
            var matrix = renderer.matrix;
            renderer.matrix = matrix * Matrix4x4.Translate(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            ChiselSphereDefinition.DrawOutline(renderer, sides, previewVertices, LineMode.NoZTest);
            renderer.matrix = matrix;
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
