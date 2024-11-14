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
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static InternalChiselSurface Convert(ChiselSurface surface)
		{
			return new InternalChiselSurface
            {
                details		     = surface.surfaceDetails,
                parameters       = new SurfaceDestinationParameters
				                   {
										parameter1 = (surface.RenderMaterial == null) ? 0 : surface.RenderMaterial.GetInstanceID(),
										parameter2 = (surface.PhysicsMaterial == null) ? 0 : surface.PhysicsMaterial.GetInstanceID()
				},
				destinationFlags = surface.DestinationFlags,
				outputFlags	     = surface.OutputFlags,
            };
		}
	}

	public struct InternalChiselSurfaceArray
	{
		public BlobArray<InternalChiselSurface> surfaces;
	}
}