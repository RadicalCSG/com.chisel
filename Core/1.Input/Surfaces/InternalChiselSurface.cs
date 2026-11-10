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
			outputFlags      = SurfaceOutputFlags.Default,
			textureScale     = 1
		};

		public SurfaceDetails               details;
		public SurfaceDestinationParameters parameters;
		public SurfaceDestinationFlags      destinationFlags;
		public SurfaceOutputFlags           outputFlags;
		// The hotspot layout the surface's material has (ChiselHotspotStore), 0 for none
		public int                          hotspotLayout;
		// The scale of its material's texture along the texture's axes (ChiselTextureScale), 0 taken as 1
		public Unity.Mathematics.float2     textureScale;

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
				textureScale     = ChiselTextureScale.Of(surface.RenderMaterial),
            };
		}
	}

	public struct InternalChiselSurfaceArray
	{
		public BlobArray<InternalChiselSurface> surfaces;
	}
}