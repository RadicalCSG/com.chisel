using System;
using UnityEngine;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselCapsuleDefinition.kNodeTypeName, group: ChiselToolGroups.kBasePrimitives)]
    public sealed class ChiselCapsulePlacementTool : ChiselBoundsPlacementTool<ChiselCapsuleDefinition>
    {
        public int topSegments		= ChiselCapsule.DefaultSettings.topSegments;
        public int bottomSegments	= ChiselCapsule.DefaultSettings.bottomSegments;
        public int sides			= ChiselCapsule.DefaultSettings.sides;


        [ToggleFlags(includeFlags: (int)(PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterY | PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterXZ;
        public override PlacementFlags PlacementFlags => placement;


        public override void OnCreate(ref ChiselCapsuleDefinition definition) 
        {
            ref var settings = ref definition.settings;
            settings.sides           = sides;
            settings.topSegments     = topSegments;
            settings.bottomSegments  = bottomSegments;
        }

        public override void OnUpdate(ref ChiselCapsuleDefinition definition, Bounds bounds)
        {
            ref var settings = ref definition.settings;
            var height              = bounds.size[(int)Axis.Y];
            var hemisphereHeight    = Mathf.Min(bounds.size[(int)Axis.X], bounds.size[(int)Axis.Z]) * ChiselCapsule.kDefaultHemisphereRatio;

            settings.topHeight    = Mathf.Min(hemisphereHeight, Mathf.Abs(height) * 0.5f);
            settings.bottomHeight = Mathf.Min(hemisphereHeight, Mathf.Abs(height) * 0.5f);

            // placed with the same width both ways it's round, otherwise elliptical
            settings.isElliptical = (placement & PlacementFlags.SameLengthXZ) != PlacementFlags.SameLengthXZ;
            settings.diameterX    = bounds.size[(int)Axis.X];
            settings.height       = height;
            settings.diameterZ    = bounds.size[(int)Axis.Z];
        }

        // the capsule that is made in the box, outlined as when it's edited, with its end loops; its vertices are where it
        // stands, put in the box by the matrix
        public override void OnPaint(IChiselHandleRenderer renderer, Bounds bounds)
        {
            previewDefinition ??= new ChiselCapsuleDefinition();
            previewDefinition.settings = ChiselCapsule.DefaultSettings;
            OnCreate(ref previewDefinition);
            OnUpdate(ref previewDefinition, bounds);
            ref var settings = ref previewDefinition.settings;
            settings.Validate();
            if (BrushMeshFactory.GenerateCapsuleVertices(ref settings, ref previewVertices))
            {
                var matrix = renderer.matrix;
                renderer.matrix = matrix * Matrix4x4.Translate(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
                ChiselCapsuleDefinition.DrawOutline(renderer, settings, previewVertices, LineMode.NoZTest, drawEnds: true);
                renderer.matrix = matrix;
            }
            renderer.RenderBoxMeasurements(bounds);
        }

        [NonSerialized] ChiselCapsuleDefinition previewDefinition;
        [NonSerialized] Vector3[] previewVertices;
    }
}
