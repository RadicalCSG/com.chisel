using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Unity.Mathematics;

using UnityEngine;

namespace Chisel.Core
{
	// TODO: add comments
	[Serializable, Flags]
	public enum SurfaceOutputFlags : byte
	{
		None = 0,

		// TODO: implement this
		// {{
		DoubleSided		  = (int)((uint)1 << 1),
		EnvironmentSkybox = (int)((uint)1 << 2), // <-- TODO: replace material with environment skybox, so we can define it in one place
		NoLightmap		  = (int)((uint)1 << 3), // <-- TODO: should be part of SurfaceDestinationFlags?
		Transparent		  = (int)((uint)1 << 4), // <-- TODO: as in, brush that has a transparent side will not remove it's contents? is this the best way to handle this?
		// }}

		Default = None
	};

	[AddMetadataMenu("Chisel/Surface Parameters"), DisplayName("Chisel Surface Parameters")]
	[DisallowMultipleCustomAssetMetadata, RestrictMetadataAssetTypes(typeof(UnityEngine.Material))]
	public class ChiselSurfaceMetadata : CustomAssetMetadata
	{
		public const string kDestinationFlagsFieldName = nameof(destinationFlags);
		public const string kOutputFlagsName           = nameof(outputFlags);
		public const string kPhysicsMaterialFieldName  = nameof(physicsMaterial);

		public SurfaceDestinationFlags destinationFlags = SurfaceDestinationFlags.Default;
		public SurfaceOutputFlags	   outputFlags		= SurfaceOutputFlags.Default;
		public PhysicsMaterial          physicsMaterial;


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode()
		{
			unchecked
			{
				uint physicsMaterialHashcode = (physicsMaterial != null) ? (uint)physicsMaterial.GetHashCode() : 0u;
				var hash = math.hash(new uint3((uint)destinationFlags, (uint)outputFlags, physicsMaterialHashcode));
				return (int)hash;
			}
		}

		public override void OnReset()
		{
			physicsMaterial = ChiselDefaultMaterials.DefaultPhysicsMaterial;
		}
	}
}
