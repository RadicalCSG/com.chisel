using System.Runtime.CompilerServices;

using Unity.Entities;

namespace Chisel.Core
{
    // This describes a chisel surface for use within job systems 
    // (which cannot handle Materials since they're managed classes)
	public struct InternalChiselSurface
    {
        public readonly static InternalChiselSurface Default = new()
        {
			details          = SurfaceDetails.Default,
			parameters       = SurfaceDestinationParameters.Empty,
			destinationFlags = SurfaceDestinationFlags.None,
			outputFlags      = SurfaceOutputFlags.Default
		};

		public SurfaceDetails               details;
		public SurfaceDestinationParameters parameters;
		public SurfaceDestinationFlags      destinationFlags;
		public SurfaceOutputFlags           outputFlags;
		// The hotspot layout the surface's material has (ChiselHotspotStore), 0 for none
		public int                          hotspotLayout;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static InternalChiselSurface Convert(ChiselSurface surface)
		{
			ChiselPaintMaterials.Register(surface.RenderMaterial);
			return new InternalChiselSurface
            {
                details		     = surface.surfaceDetails,
                parameters       = new SurfaceDestinationParameters
				                   {
										parameter1 = (surface.RenderMaterial == null) ? 0UL : UnityEngine.EntityId.ToULong(surface.RenderMaterial.GetEntityId()),
										parameter2 = (surface.PhysicsMaterial == null) ? 0UL : UnityEngine.EntityId.ToULong(surface.PhysicsMaterial.GetEntityId())
				},
				destinationFlags = surface.DestinationFlags,
				outputFlags	     = surface.OutputFlags,
				hotspotLayout    = ChiselHotspotStore.Register(surface.HotspotLayout, surface.RenderMaterial, surface.HotspotVariants, surface.HotspotSheets),
            };
		}
	}

	public struct InternalChiselSurfaceArray
	{
		public BlobArray<InternalChiselSurface> surfaces;
	}
}