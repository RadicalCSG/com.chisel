using System;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselHemisphereDefinition.kNodeTypeName, group: ChiselToolGroups.kBasePrimitives)]
    public sealed class ChiselHemispherePlacementTool : ChiselBoundsPlacementTool<ChiselHemisphereDefinition>
    {
        public int horizontalSegments   = ChiselHemisphere.DefaultSettings.horizontalSegments;
        public int verticalSegments     = ChiselHemisphere.DefaultSettings.verticalSegments;


        [ToggleFlags(includeFlags: (int)(PlacementFlags.SameLengthXZ | PlacementFlags.HeightEqualsHalfXZ | PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.HeightEqualsHalfXZ | PlacementFlags.GenerateFromCenterXZ;
        public override PlacementFlags PlacementFlags => placement;

        [NonSerialized] Vector3[] previewVertices;

        public override void OnCreate(ref ChiselHemisphereDefinition definition)
        {
            definition.settings.verticalSegments     = verticalSegments;
            definition.settings.horizontalSegments   = horizontalSegments;
        }

        public override void OnUpdate(ref ChiselHemisphereDefinition definition, Bounds bounds)
        {
            definition.settings.diameterXYZ = bounds.size;
        }

        // the hemisphere that fills the box, outlined as when it's edited; its vertices are made where it stands and put
        // in the box by the matrix (they don't take a move)
        public override void OnPaint(IChiselHandleRenderer renderer, Bounds bounds)
        {
            var sides    = math.max(horizontalSegments, 3);
            var segments = math.max(verticalSegments, 1);
            BrushMeshFactory.GenerateHemisphereVertices(bounds.size, Matrix4x4.identity, sides, segments, ref previewVertices);
            var matrix = renderer.matrix;
            renderer.matrix = matrix * Matrix4x4.Translate(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            ChiselHemisphereDefinition.DrawOutline(renderer, sides, segments, previewVertices, LineMode.NoZTest);
            renderer.matrix = matrix;
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
