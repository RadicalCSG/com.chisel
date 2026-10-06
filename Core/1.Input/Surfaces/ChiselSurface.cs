using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

using UnityEngine;

namespace Chisel.Core
{
	// Encapsulated data so we can more easily make a propertydrawer for it
	//TODO: make a property drawer for it
	[Serializable]
	public struct ChiselMaterial
	{
        public const string kMaterialFieldName		  = nameof(material);
		public const string kSurfaceMetadataFieldName = nameof(surfaceMetadata);

		public Material				 material;

		public ChiselSurfaceMetadata surfaceMetadata;

		/// <summary>Looks the metadata up from the material. <see cref="MetadataLookup"/> is itself
		/// the cache, so this cannot go stale and does not need invalidating.</summary>
		public readonly ChiselSurfaceMetadata ResolveSurfaceMetadata()
		{
			if (material == null)
				return null;
			return material.GetMetadataOfType<ChiselSurfaceMetadata>();
		}

		public static ChiselMaterial Create(Material material)
		{
			ChiselMaterial chiselMaterial = new() { material = material };
			chiselMaterial.Update();
			return chiselMaterial;
		}

		public bool Update()
		{
			if (material == null)
			{
				if (surfaceMetadata != null)
				{
					surfaceMetadata = null;
					return true;
				}
				return false;
			}
			var newSurfaceMetadata = material.GetMetadataOfType<ChiselSurfaceMetadata>();
			if (surfaceMetadata != newSurfaceMetadata)
			{
				surfaceMetadata = newSurfaceMetadata;
				return true;
			}
			return false;
		}
	}

	/// <summary>Defines a surface on a <see cref="Chisel.Core.BrushMesh"/>. 
	/// Some of a surface is specific to a particular surface (for example: uvs),
	/// other parts are shared between surfaces (for example: the material).</summary>
	/// <seealso cref="Chisel.Core.ChiselBrushMaterial"/>
	/// <seealso cref="Chisel.Core.ChiselSurfaceArray"/>
	/// <seealso cref="Chisel.Core.SurfaceDetails"/>
	[Serializable]
    public sealed class ChiselSurface
	{
		public const string kChiselMaterialName = nameof(chiselMaterial);
		public const string kSurfaceDetailsName	= nameof(surfaceDetails);

		[SerializeField]
		internal ChiselMaterial chiselMaterial = new();
		public SurfaceDetails surfaceDetails = SurfaceDetails.Default;

		public bool HasMaterial { get { return chiselMaterial.material != null; } }

		public Material				   RenderMaterial   { get { return (chiselMaterial.material != null) ? chiselMaterial.material : ChiselProjectSettings.DefaultMaterial; } set { SetMaterial(value); } }
		// These resolve the metadata rather than reading ChiselMaterial.surfaceMetadata: that field
		// is only refreshed when a surface is selected, and these feed the CSG pipeline.
		public PhysicsMaterial		   PhysicsMaterial  { get { var metadata = chiselMaterial.ResolveSurfaceMetadata(); return (metadata != null) ? metadata.physicsMaterial : ChiselProjectSettings.DefaultPhysicsMaterial; } }
		// Normalized on the way out rather than on the way in, so that what the metadata stores is
		// left exactly as authored and only what the pipeline acts on is corrected.
		public SurfaceDestinationFlags DestinationFlags { get { var metadata = chiselMaterial.ResolveSurfaceMetadata(); return ((metadata != null) ? metadata.destinationFlags : SurfaceDestinationFlags.Default).Normalize(); } }
		public SurfaceOutputFlags      OutputFlags      { get { var metadata = chiselMaterial.ResolveSurfaceMetadata(); return (metadata != null) ? metadata.outputFlags : SurfaceOutputFlags.Default; } }

		public void SetMaterial(Material material)
		{
			chiselMaterial.material = material;
			chiselMaterial.Update();
		}

		public static ChiselSurface Create(Material material, SurfaceDetails details) 
		{ 
			return new ChiselSurface
			{
				chiselMaterial = ChiselMaterial.Create(material),
				surfaceDetails = details
			};
		}

		public static ChiselSurface Create(Material material)
		{
			return Create(material, SurfaceDetails.Default);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            unchecked
            {
				uint materialHashcode = (chiselMaterial.material != null) ? (uint)chiselMaterial.material.GetHashCode() : 0u;
				// Resolved, not the serialized cache: this hash is what decides whether a surface
				// needs rebuilding, so hashing a stale value would keep the stale result too.
				var surfaceMetadata = chiselMaterial.ResolveSurfaceMetadata();
				uint surfaceMetadataHashcode = (surfaceMetadata != null) ? (uint)surfaceMetadata.GetHashCode() : 0;

				uint hash = surfaceDetails.Hash();				
                hash = math.hash(new uint3(hash, materialHashcode, surfaceMetadataHashcode));
                return (int)hash;
            }
        }
    }
}