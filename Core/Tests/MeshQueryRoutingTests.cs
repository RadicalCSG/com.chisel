using System.Collections.Generic;
using NUnit.Framework;

namespace Chisel.Core.Tests
{
	[TestFixture]
	public class MeshQueryRoutingTests
	{
		readonly static int[] kValidRenderableIndices = { 2, 3, 7, 9, 10, 11, 13, 15 };
		const int kShadowCastingRendererIndex = 2;  // ‹[generated-ShadowCasting]›, ie shadow-only
		const int kDefaultRendererIndex       = 7;  // ‹[generated-Renderable|ShadowCasting|ShadowReceiving]›
		const int kRenderableRendererIndex    = 9;  // ‹[generated-Renderable|ExcludedFromGlobalIllumination]›
		const int kReceivingRendererIndex     = 13; // ‹[generated-Renderable|ShadowReceiving|ExcludedFromGlobalIllumination]›
		const int kExcludedRendererIndex      = 15; // ‹[generated-Renderable|ShadowCasting|ShadowReceiving|ExcludedFromGlobalIllumination]›

		// Renderable, ShadowCasting, ShadowReceiving, Collidable and ExcludedFromGlobalIllumination;
		// Discarded is not a combination a real surface carries alongside the others, so it is left out.
		const int kFlagCombinations = 32;

		static bool Matches(SurfaceDestinationFlags destinationFlags, MeshQuery query)
		{
			return (destinationFlags & query.LayerQueryMask) == query.LayerQuery;
		}

		static int RenderIndexOf(MeshQuery query)
		{
			return query.LayerQuery.RendererIndex();
		}

		static bool HasDebugRenderer(MeshQuery query)
		{
			foreach (var flags in AssignMeshesJob.kGeneratedDebugRendererFlags)
			{
				if (flags.Item1 == query.LayerQuery &&
					flags.Item2 == query.LayerQueryMask)
					return true;
			}
			return false;
		}

		// Finds the queries that would claim a surface with these flags, restricted to the ones
		// that produce a renderer (so colliders and debug visualization do not count as "rendered").
		static List<MeshQuery> RenderQueriesFor(SurfaceDestinationFlags destinationFlags)
		{
			var found = new List<MeshQuery>();
			foreach (var query in MeshQuery.DefaultQueries)
			{
				if (query.LayerParameterIndex != SurfaceParameterIndex.RenderMaterial)
					continue;
				if (Matches(destinationFlags, query))
					found.Add(query);
			}
			return found;
		}

		static int RendererIndexFor(SurfaceDestinationFlags destinationFlags)
		{
			var queries = RenderQueriesFor(destinationFlags.Normalize());
			Assert.That(queries.Count, Is.EqualTo(1),
				$"Expected exactly one renderable query to claim a surface with {destinationFlags}, found {queries.Count}.");
			return RenderIndexOf(queries[0]);
		}

		[Test]
		public void DefaultQueries_EveryQueryHasADestination()
		{
			foreach (var query in MeshQuery.DefaultQueries)
			{
				switch (query.LayerParameterIndex)
				{
					case SurfaceParameterIndex.None:
					{
						Assert.That(HasDebugRenderer(query), Is.True,
							$"{query} is tagged SurfaceParameterIndex.None, so AssignMeshesJob treats it as a " +
							$"debug visualization query, but no entry in kGeneratedDebugRendererFlags has that " +
							$"query/mask pair - every surface matching it is silently discarded. If this is meant " +
							$"to produce a renderable mesh, give it SurfaceParameterIndex.RenderMaterial.");
						break;
					}
					case SurfaceParameterIndex.RenderMaterial:
					{
						Assert.That(kValidRenderableIndices, Contains.Item(RenderIndexOf(query)),
							$"{query} routes to renderables[{RenderIndexOf(query)}], which is an invalid slot.");
						break;
					}
					case SurfaceParameterIndex.PhysicsMaterial:
					{
						Assert.That(query.LayerQuery & SurfaceDestinationFlags.Collidable,
							Is.EqualTo(SurfaceDestinationFlags.Collidable),
							$"{query} is tagged PhysicsMaterial but does not ask for collidable surfaces.");
						break;
					}
					default:
					{
						Assert.Fail($"{query} uses a parameter index AssignMeshesJob does not handle.");
						break;
					}
				}
			}
		}

		// Every generated renderer has the one query that fills it.
		[Test]
		public void DefaultQueries_EveryRendererHasOneQuery()
		{
			foreach (var index in kValidRenderableIndices)
			{
				var count = 0;
				foreach (var query in MeshQuery.DefaultQueries)
				{
					if (query.LayerParameterIndex == SurfaceParameterIndex.RenderMaterial && RenderIndexOf(query) == index)
						count++;
				}
				Assert.That(count, Is.EqualTo(1), $"renderables[{index}] is filled by {count} queries");
			}
		}

		// The regression this file exists for: a surface that casts shadows but is NOT rendered has
		// to reach ‹[generated-ShadowCasting]›, which casts it with the ForceShadowOnly material.
		[Test]
		public void DefaultQueries_ShadowOnlySurfaceReachesTheShadowCastingRenderer()
		{
			var shadowOnly = SurfaceDestinationFlags.ShadowCasting | SurfaceDestinationFlags.Collidable;
			Assert.That(RendererIndexFor(shadowOnly), Is.EqualTo(kShadowCastingRendererIndex));
		}

		[Test]
		public void DefaultQueries_DefaultSurfaceReachesTheRendererInTheBake()
		{
			Assert.That((int)SurfaceDestinationFlags.Default, Is.EqualTo(15), "surfaces saved before the flag existed say 15");
			Assert.That(RendererIndexFor(SurfaceDestinationFlags.Default), Is.EqualTo(kDefaultRendererIndex));
		}

		[Test]
		public void DefaultQueries_SurfaceThatCastsNoShadowsStaysOutOfTheBake()
		{
			var sky = SurfaceDestinationFlags.Renderable | SurfaceDestinationFlags.Collidable;
			Assert.That(RendererIndexFor(sky), Is.EqualTo(kRenderableRendererIndex));

			var receiving = SurfaceDestinationFlags.RenderShadowsReceiving;
			Assert.That(RendererIndexFor(receiving), Is.EqualTo(kReceivingRendererIndex));
		}

		// A surface that casts shadows can be left out of the bake, and then it is drawn by a renderer of its own.
		[Test]
		public void DefaultQueries_ShadowCasterCanStayOutOfTheBake()
		{
			var excluded = SurfaceDestinationFlags.Default | SurfaceDestinationFlags.ExcludedFromGlobalIllumination;
			Assert.That(RendererIndexFor(excluded), Is.EqualTo(kExcludedRendererIndex));
		}

		// Shadow-only surfaces are solid walls the player never sees, and they block light in a bake as they
		// block it in the game, so they take part in one unless they say otherwise.
		[Test]
		public void DefaultQueries_ShadowOnlySurfaceIsInTheBake()
		{
			var shadowOnly = SurfaceDestinationFlags.ShadowCasting | SurfaceDestinationFlags.Collidable;
			Assert.That(shadowOnly.Normalize() & SurfaceDestinationFlags.ExcludedFromGlobalIllumination,
						Is.EqualTo(SurfaceDestinationFlags.None));
			Assert.That(RendererIndexFor(shadowOnly), Is.EqualTo(kShadowCastingRendererIndex));
		}

		[Test]
		public void DefaultQueries_EveryDebugHelperHasAQuery()
		{
			foreach (var flags in AssignMeshesJob.kGeneratedDebugRendererFlags)
			{
				var found = false;
				foreach (var query in MeshQuery.DefaultQueries)
				{
					if (query.LayerParameterIndex == SurfaceParameterIndex.None &&
						query.LayerQuery == flags.Item1 && query.LayerQueryMask == flags.Item2)
						found = true;
				}
				Assert.That(found, Is.True, $"no query fills the debug helper for {flags.Item1} (mask {flags.Item2})");
			}
		}

		// Two renderable queries claiming the same surface would duplicate its geometry across two
		// renderers, which is how you get shadows or z-fighting from a surface drawn twice.
		[Test]
		public void DefaultQueries_NoSurfaceIsClaimedByTwoRenderableQueries()
		{
			for (int i = 0; i < kFlagCombinations; i++)
			{
				var destinationFlags = (SurfaceDestinationFlags)i;
				var queries = RenderQueriesFor(destinationFlags);
				Assert.That(queries.Count, Is.LessThanOrEqualTo(1),
					$"A surface with {destinationFlags} is claimed by {queries.Count} renderable queries.");
			}
		}

		// Receiving shadows only means anything while a surface is being drawn.
		[Test]
		public void Normalize_DropsShadowReceivingWhenNotRenderable()
		{
			var shadowOnly = SurfaceDestinationFlags.ShadowCasting |
							 SurfaceDestinationFlags.ShadowReceiving |
							 SurfaceDestinationFlags.Collidable;
			Assert.That(shadowOnly.Normalize(),
						Is.EqualTo(SurfaceDestinationFlags.ShadowCasting | SurfaceDestinationFlags.Collidable));

			// ... and is left alone when the surface IS drawn.
			var normal = SurfaceDestinationFlags.Default;
			Assert.That(normal.Normalize(), Is.EqualTo(normal));
		}

		// Blocking light in a bake only means anything for a surface that blocks it as it is drawn.
		[Test]
		public void Normalize_ExcludesFromGlobalIlluminationWhenNotCastingShadows()
		{
			var sky = SurfaceDestinationFlags.Renderable | SurfaceDestinationFlags.Collidable;
			Assert.That(sky.Normalize(), Is.EqualTo(sky | SurfaceDestinationFlags.ExcludedFromGlobalIllumination));

			// ... and a surface that casts shadows is left as it asks to be, drawn or not.
			var shadowOnly = SurfaceDestinationFlags.ShadowCasting | SurfaceDestinationFlags.Collidable;
			Assert.That(shadowOnly.Normalize(), Is.EqualTo(shadowOnly));
			Assert.That(SurfaceDestinationFlags.Default.Normalize(), Is.EqualTo(SurfaceDestinationFlags.Default));
		}

		[Test]
		public void EveryNormalizedFlagCombination_RoutesSomewhere()
		{
			for (int i = 0; i < kFlagCombinations; i++)
			{
				var destinationFlags = ((SurfaceDestinationFlags)i).Normalize();

				var claimed = false;
				foreach (var query in MeshQuery.DefaultQueries)
				{
					if (!Matches(destinationFlags, query))
						continue;
					claimed = true;
					if (query.LayerParameterIndex == SurfaceParameterIndex.RenderMaterial)
						Assert.That(kValidRenderableIndices, Contains.Item(RenderIndexOf(query)));
				}

				Assert.That(claimed, Is.True,
					$"a surface with {destinationFlags} (from {(SurfaceDestinationFlags)i}) matches no query at all");
			}
		}

		[Test]
		public void DefaultQueries_EveryRenderableSurfaceCombinationIsClaimed()
		{
			for (int i = 0; i < kFlagCombinations; i++)
			{
				var destinationFlags = ((SurfaceDestinationFlags)i).Normalize();
				if ((destinationFlags & SurfaceDestinationFlags.Renderable) != SurfaceDestinationFlags.Renderable)
					continue;

				var queries = RenderQueriesFor(destinationFlags);
				Assert.That(queries.Count, Is.EqualTo(1),
					$"A renderable surface with {destinationFlags} is claimed by no renderable query.");
				Assert.That(kValidRenderableIndices, Contains.Item(RenderIndexOf(queries[0])));
			}
		}
	}
}
