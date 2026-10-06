using NUnit.Framework;
using UnityEngine;

namespace Chisel.Core.Tests
{
	[TestFixture]
	public class ChiselSurfaceMetadataFlagsTests
	{
		static SurfaceDestinationFlags FlagsOf(string json)
		{
			var metadata = ScriptableObject.CreateInstance<ChiselSurfaceMetadata>();
			try
			{
				JsonUtility.FromJsonOverwrite(json, metadata);
				return metadata.destinationFlags;
			}
			finally
			{
				Object.DestroyImmediate(metadata);
			}
		}

		[Test]
		public void FlagsSavedBeforeTheFlagExisted_StillTakePartInTheBake()
		{
			var saved = FlagsOf("{\"destinationFlags\":15}");
			Assert.AreEqual(SurfaceDestinationFlags.Default, saved, "what Chisel's own materials store");
			Assert.AreEqual(SurfaceDestinationFlags.None, saved.Normalize() & SurfaceDestinationFlags.ExcludedFromGlobalIllumination);

			var drawnAndCasting = FlagsOf("{\"destinationFlags\":3}");
			Assert.AreEqual(SurfaceDestinationFlags.RenderShadowsCasting, drawnAndCasting);
			Assert.AreEqual(SurfaceDestinationFlags.None,
							drawnAndCasting.Normalize() & SurfaceDestinationFlags.ExcludedFromGlobalIllumination);
		}

		// A shadow-only surface is a solid wall the player never sees, and it blocks light in a bake as it does in the game
		[Test]
		public void FlagsSavedBeforeTheFlagExisted_ThatAreNotDrawn_AreInTheBakeToo()
		{
			var shadowOnly = FlagsOf("{\"destinationFlags\":10}");
			Assert.AreEqual(SurfaceDestinationFlags.ShadowCasting | SurfaceDestinationFlags.Collidable, shadowOnly);
			Assert.AreEqual(SurfaceDestinationFlags.None,
							shadowOnly.Normalize() & SurfaceDestinationFlags.ExcludedFromGlobalIllumination);
		}

		// Drawn without shadows, it lets light through in a bake as it does in the game, whatever it was saved as
		[Test]
		public void FlagsSavedBeforeTheFlagExisted_ThatCastNoShadows_AreKeptOutOfIt()
		{
			var sky = FlagsOf("{\"destinationFlags\":9}");
			Assert.AreEqual(SurfaceDestinationFlags.Renderable | SurfaceDestinationFlags.Collidable, sky);
			Assert.AreEqual(SurfaceDestinationFlags.ExcludedFromGlobalIllumination,
							sky.Normalize() & SurfaceDestinationFlags.ExcludedFromGlobalIllumination);
		}

		[Test]
		public void NewSurfaceParameters_TakePartInTheBake()
		{
			var metadata = ScriptableObject.CreateInstance<ChiselSurfaceMetadata>();
			try
			{
				Assert.AreEqual(SurfaceDestinationFlags.Default, metadata.destinationFlags);
				Assert.AreEqual(SurfaceDestinationFlags.None,
								metadata.destinationFlags & SurfaceDestinationFlags.ExcludedFromGlobalIllumination);
			}
			finally
			{
				Object.DestroyImmediate(metadata);
			}
		}
	}
}
