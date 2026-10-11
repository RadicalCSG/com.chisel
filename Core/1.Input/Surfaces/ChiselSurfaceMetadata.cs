using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Unity.Mathematics;

using UnityEngine;

namespace Chisel.Core
{
	[Serializable, Flags]
	public enum SurfaceOutputFlags : byte
	{
		None = 0,

		// TODO: implement this
		// {{
		DoubleSided		  = (int)((uint)1 << 1),
		EnvironmentSkybox = (int)((uint)1 << 2), // <-- TODO: replace material with environment skybox, so we can define it in one place
		Transparent		  = (int)((uint)1 << 4), // <-- TODO: as in, brush that has a transparent side will not remove it's contents? is this the best way to handle this?
		// }}

		/// <summary>The surface gets no lightmap texels: it is never lit by a lightmap, as an unlit surface isn't</summary>
		NoLightmap		  = (int)((uint)1 << 3),
		/// <summary>
		/// The surface gets one lightmap texel: its own lighting doesn't show, as on a surface that gives off light, but a bake
		/// takes the light it gives off from its texels
		/// </summary>
		SingleLightmapTexel = (int)((uint)1 << 5),

		Default = None
	};

	[AddMetadataMenu("Chisel/Surface Parameters"), DisplayName("Chisel Surface Parameters")]
	[DisallowMultipleCustomAssetMetadata, RestrictMetadataAssetTypes(typeof(UnityEngine.Material))]
	public class ChiselSurfaceMetadata : CustomAssetMetadata
	{
		public const string kDestinationFlagsFieldName = nameof(destinationFlags);
		public const string kOutputFlagsName           = nameof(outputFlags);
		public const string kPhysicsMaterialFieldName  = nameof(physicsMaterial);
		public const string kHotspotLayoutFieldName    = nameof(hotspotLayout);
		public const string kHotspotVariantsFieldName  = nameof(hotspotVariants);
		public const string kHotspotSheetsFieldName    = nameof(hotspotSheets);

		public SurfaceDestinationFlags destinationFlags = SurfaceDestinationFlags.Default;
		public SurfaceOutputFlags	   outputFlags		= SurfaceOutputFlags.Default;
		public PhysicsMaterial         physicsMaterial;
		// Surfaces with this material take their uvs from a tile of this layout
		public ChiselHotspotLayout     hotspotLayout;
		// Other materials drawn on the same layout (another texture of the same tiles): a surface is given this material
		// or one of them, as a variation (ChiselHotspotStore)
		public System.Collections.Generic.List<Material> hotspotVariants = new();
		// Other sheets drawn with it (materials whose metadata has a layout of its own, a trim sheet say): matching picks from
		// their tiles too, and a surface given one of theirs is given that material (ChiselHotspotStore)
		public System.Collections.Generic.List<Material> hotspotSheets = new();


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode()
		{
			unchecked
			{
				uint physicsMaterialHashcode = (physicsMaterial != null) ? (uint)physicsMaterial.GetHashCode() : 0u;
				uint hotspotLayoutHashcode   = (hotspotLayout != null) ? (uint)hotspotLayout.GetHashCode() : 0u;
				var hash = math.hash(new uint4((uint)destinationFlags, (uint)outputFlags, physicsMaterialHashcode, hotspotLayoutHashcode));
				if (hotspotVariants != null)
				{
					foreach (var variant in hotspotVariants)
						hash = math.hash(new uint2(hash, variant != null ? (uint)variant.GetHashCode() : 0u));
				}
				if (hotspotSheets != null)
				{
					foreach (var sheet in hotspotSheets)
						hash = math.hash(new uint3(hash, sheet != null ? (uint)sheet.GetHashCode() : 0u, 1u));
				}
				return (int)hash;
			}
		}

		public override void OnReset()
		{
			physicsMaterial = ChiselProjectSettings.DefaultPhysicsMaterial;
		}
	}
}
