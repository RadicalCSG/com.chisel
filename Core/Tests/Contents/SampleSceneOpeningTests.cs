using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class SampleSceneOpeningTests
	{
		// Nothing may be drawn with its centre strictly inside the box: the opening's inside, kept clear of its walls
		static void AssertNothingDrawnIn(ContentsScene scene, float3 min, float3 max, string what)
		{
			using (var harness = ContentsTreeHarness.Build(scene))
			{
				Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
				Assert.That(harness.PlaneMismatches, Is.Empty, what + ": the brushes were not given the scene's planes");
				Assert.That(harness.Triangles.Count, Is.GreaterThan(0), what + ": nothing was drawn at all");
				var inside = harness.Triangles.Where(t => math.all(t.Center > min) && math.all(t.Center < max)).ToList();
				double area = inside.Sum(t => (double)math.length(math.cross(t.b - t.a, t.c - t.a)) * 0.5);
				Assert.That(inside, Is.Empty, $"{what}: {inside.Count} triangle(s) drawn inside the opening, {area:0.####} in all, e.g. " +
							string.Join("; ", inside.Take(4).Select(t => $"({t.a.x:R}, {t.a.y:R}, {t.a.z:R}) ({t.b.x:R}, {t.b.y:R}, {t.b.z:R}) ({t.c.x:R}, {t.c.y:R}, {t.c.z:R}) facing {t.Normal}")));
			}
		}

		// "Box (Door)" (additive, the wall) and the subtractive "Box" that cuts the doorway through it: x -2..2,
		// y -8.5..-4.5, through the wall's whole thickness z -38..-34.
		[Test]
		public void TheDoorway_ThroughBoxDoor_IsOpen()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(0f, 1f, 0f, 0.5f),
					new float4(0f, -1f, 0f, -8.5f),
					new float4(-0.707106769f, 1.95982852e-06f, 0.707106769f, -4.24263954f),
					new float4(0.707106769f, 1.95982852e-06f, 0.707106769f, -4.24263954f),
					new float4(0f, -2.771616e-06f, -1f, -2.00000143f),
					new float4(0f, 2.771616e-06f, 1f, -1.99999857f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(0f, -0.5f, -36f, 1f)), name: "Box (Door) #175")
					.WithMesh(new float3[] { new float3(-8f, -0.5f, -2f), new float3(-4f, -0.5f, 2f), new float3(4f, -0.5f, 2f), new float3(8f, -0.5f, -2f), new float3(-8f, -8.5f, -1.99997783f), new float3(8f, -8.5f, -1.99997783f), new float3(4f, -8.5f, 2.00002217f), new float3(-4f, -8.5f, 2.00002217f) },
					          new int[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, -1, 0, 4, 7, 1, -1, 3, 2, 6, 5, -1, 0, 3, 5, 4, -1, 1, 7, 6, 2, -1 }))
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -2f),
					new float4(0f, -1f, 0f, -4f),
					new float4(0f, 0f, -1f, -1.99999988f),
					new float4(1f, 0f, 0f, -2f),
					new float4(0f, 1f, 0f, -1f),
					new float4(0f, 0f, 1f, -1.99999988f),
				}, contents: 0, operation: CSGOperationType.Subtractive,
					localToTree: new float4x4(new float4(1f, 0f, 0f, 0f), new float4(0f, -1.192093e-07f, -1.00000024f, 0f), new float4(0f, 1.00000024f, -1.192093e-07f, 0f), new float4(0f, -6.5f, -38f, 1f)), name: "Box #236")
					.WithMesh(new float3[] { new float3(-2f, -4f, 1.99999988f), new float3(-2f, -4f, -1.99999988f), new float3(2f, -4f, -1.99999988f), new float3(2f, -4f, 1.99999988f), new float3(-2f, 1f, 1.99999988f), new float3(-2f, 1f, -1.99999988f), new float3(2f, 1f, -1.99999988f), new float3(2f, 1f, 1.99999988f) },
					          new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				;
			AssertNothingDrawnIn(scene, new float3(-1.9f, -8.4f, -38.1f), new float3(1.9f, -4.6f, -33.9f), "the doorway through Box (Door)");
		}

		[Test]
		public void ThePitUnderExtrudedShapeRemove_IsOpen()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -8f),
					new float4(0f, -1f, 0f, -1.5f),
					new float4(0f, 0f, -1f, -2f),
					new float4(1f, 0f, 0f, -8f),
					new float4(0f, 1f, 0f, 0.5f),
					new float4(0f, 0f, 1f, -2f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(0f, -0.5f, -32f, 1f)), name: "Box #174")
					.WithMesh(new float3[] { new float3(-8f, -1.5f, 2f), new float3(-8f, -1.5f, -2f), new float3(8f, -1.5f, -2f), new float3(8f, -1.5f, 2f), new float3(-8f, -0.5f, 2f), new float3(-8f, -0.5f, -2f), new float3(8f, -0.5f, -2f), new float3(8f, -0.5f, 2f) },
					          new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				.Add(ContentsSceneNode.Composite(CSGOperationType.Additive,
					ContentsSceneNode.Brush(new float4[]
					{
						new float4(0f, -1f, 7.40718e-08f, -7f),
						new float4(0f, 1f, -7.40718e-08f, 6f),
						new float4(1f, 0f, 0f, -5.5f),
						new float4(0.707106769f, 0f, 0.7071068f, -5.656854f),
						new float4(0f, 0f, 1f, -5.49999952f),
						new float4(-0.707106769f, 0f, 0.7071068f, -5.656854f),
						new float4(-1f, 0f, 0f, -5.5f),
						new float4(-0.707106769f, 0f, -0.7071068f, -5.656854f),
						new float4(0f, 0f, -1f, -5.49999952f),
						new float4(0.707106769f, 0f, -0.7071068f, -5.656854f),
					}, contents: 0, operation: CSGOperationType.Additive,
						localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(0f, 5f, -30f, 1f)), name: "Extruded Shape (Additive) #240")
						.WithMesh(new float3[] { new float3(2.5f, -6.99999952f, 5.49999952f), new float3(-2.5f, -6.99999952f, 5.49999952f), new float3(-5.5f, -7f, 2.49999976f), new float3(-5.5f, -7f, -2.49999976f), new float3(-2.5f, -7.00000048f, -5.49999952f), new float3(2.5f, -7.00000048f, -5.49999952f), new float3(5.5f, -7f, -2.49999976f), new float3(5.5f, -7f, 2.49999976f), new float3(2.5f, -5.99999952f, 5.49999952f), new float3(-2.5f, -5.99999952f, 5.49999952f), new float3(-5.5f, -6f, 2.49999976f), new float3(-5.5f, -6f, -2.49999976f), new float3(-2.5f, -6.00000048f, -5.49999952f), new float3(2.5f, -6.00000048f, -5.49999952f), new float3(5.5f, -6f, -2.49999976f), new float3(5.5f, -6f, 2.49999976f) },
						          new int[] { 0, 1, 2, 3, 4, 5, 6, 7, -1, 14, 13, 12, 11, 10, 9, 8, 15, -1, 6, 14, 15, 7, -1, 7, 15, 8, 0, -1, 0, 8, 9, 1, -1, 1, 9, 10, 2, -1, 2, 10, 11, 3, -1, 3, 11, 12, 4, -1, 4, 12, 13, 5, -1, 5, 13, 14, 6, -1 })))
				.Add(ContentsSceneNode.Composite(CSGOperationType.Subtractive,
					ContentsSceneNode.Brush(new float4[]
					{
						new float4(0f, -1f, 8.141123e-08f, -7.25f),
						new float4(0f, 1f, -5.960465e-08f, 0.5f),
						new float4(1f, 0f, 0f, -5f),
						new float4(0.7071067f, 0f, 0.7071068f, -4.949747f),
						new float4(0f, 0f, 1f, -4.99999952f),
						new float4(-0.7071067f, 0f, 0.7071068f, -4.949747f),
						new float4(-1f, 0f, 0f, -5f),
						new float4(-0.7071067f, 0f, -0.7071068f, -4.949747f),
						new float4(0f, 0f, -1f, -4.99999952f),
						new float4(0.7071067f, 0f, -0.7071068f, -4.949747f),
					}, contents: 0, operation: CSGOperationType.Additive,
						localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(0f, -1.5f, -30f, 1f)), name: "Extruded Shape One #242")
						.WithMesh(new float3[] { new float3(2f, -7.24999952f, 4.99999952f), new float3(-2f, -7.24999952f, 4.99999952f), new float3(-5f, -7.25f, 1.99999988f), new float3(-5f, -7.25f, -1.99999988f), new float3(-2f, -7.25000048f, -4.99999952f), new float3(2f, -7.25000048f, -4.99999952f), new float3(5f, -7.25f, -1.99999988f), new float3(5f, -7.25f, 1.99999988f), new float3(2f, -0.4999997f, 4.99999952f), new float3(-2f, -0.4999997f, 4.99999952f), new float3(-5f, -0.499999881f, 1.99999988f), new float3(-5f, -0.5000001f, -1.99999988f), new float3(-2f, -0.5000003f, -4.99999952f), new float3(2f, -0.5000003f, -4.99999952f), new float3(5f, -0.5000001f, -1.99999988f), new float3(5f, -0.499999881f, 1.99999988f) },
						          new int[] { 0, 1, 2, 3, 4, 5, 6, 7, -1, 14, 13, 12, 11, 10, 9, 8, 15, -1, 6, 14, 15, 7, -1, 7, 15, 8, 0, -1, 0, 8, 9, 1, -1, 1, 9, 10, 2, -1, 2, 10, 11, 3, -1, 3, 11, 12, 4, -1, 4, 12, 13, 5, -1, 5, 13, 14, 6, -1 })))
				.Add(ContentsSceneNode.Composite(CSGOperationType.Subtractive,
					ContentsSceneNode.Brush(new float4[]
					{
						new float4(0f, -1f, 8.141123e-08f, -7.5f),
						new float4(0f, 1f, -8.141123e-08f, 5.5f),
						new float4(1f, 0f, 0f, -5f),
						new float4(0.7071067f, 0f, 0.7071068f, -4.949747f),
						new float4(0f, 0f, 1f, -4.99999952f),
						new float4(-0.7071067f, 0f, 0.7071068f, -4.949747f),
						new float4(-1f, 0f, 0f, -5f),
						new float4(-0.7071067f, 0f, -0.7071068f, -4.949747f),
						new float4(0f, 0f, -1f, -4.99999952f),
						new float4(0.7071067f, 0f, -0.7071068f, -4.949747f),
					}, contents: 0, operation: CSGOperationType.Additive,
						localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(0f, 5f, -30f, 1f)), name: "Extruded Shape (Remove) #274")
						.WithMesh(new float3[] { new float3(2f, -7.49999952f, 4.99999952f), new float3(-2f, -7.49999952f, 4.99999952f), new float3(-5f, -7.5f, 1.99999988f), new float3(-5f, -7.5f, -1.99999988f), new float3(-2f, -7.50000048f, -4.99999952f), new float3(2f, -7.50000048f, -4.99999952f), new float3(5f, -7.5f, -1.99999988f), new float3(5f, -7.5f, 1.99999988f), new float3(2f, -5.49999952f, 4.99999952f), new float3(-2f, -5.49999952f, 4.99999952f), new float3(-5f, -5.5f, 1.99999988f), new float3(-5f, -5.5f, -1.99999988f), new float3(-2f, -5.50000048f, -4.99999952f), new float3(2f, -5.50000048f, -4.99999952f), new float3(5f, -5.5f, -1.99999988f), new float3(5f, -5.5f, 1.99999988f) },
						          new int[] { 0, 1, 2, 3, 4, 5, 6, 7, -1, 14, 13, 12, 11, 10, 9, 8, 15, -1, 6, 14, 15, 7, -1, 7, 15, 8, 0, -1, 0, 8, 9, 1, -1, 1, 9, 10, 2, -1, 2, 10, 11, 3, -1, 3, 11, 12, 4, -1, 4, 12, 13, 5, -1, 5, 13, 14, 6, -1 })))
				;
			AssertNothingDrawnIn(scene, new float3(-3.5f, -2.5f, -33.5f), new float3(3.5f, -1.5f, -26.5f), "the pit under Extruded Shape (Remove)");
		}
	

		[Test]
		public void ThePitUnderExtrudedShapeRemove_AsFirstBuilt_IsOpenOnceThePlanesAreSnapped()
		{
			var scene = new ContentsScene()
				.Add(ContentsSceneNode.Brush(new float4[]
				{
					new float4(-1f, 0f, 0f, -8f),
					new float4(0f, -1f, 0f, -1.5f),
					new float4(0f, 0f, -1f, -2f),
					new float4(1f, 0f, 0f, -8f),
					new float4(0f, 1f, 0f, 0.5f),
					new float4(0f, 0f, 1f, -2f),
				}, contents: 0, operation: CSGOperationType.Additive,
					localToTree: new float4x4(new float4(-1.00000048f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1.00000048f, 0f), new float4(0f, -0.5f, -32f, 1f)), name: "Box #174")
					.WithMesh(new float3[] { new float3(-8f, -1.5f, 2f), new float3(-8f, -1.5f, -2f), new float3(8f, -1.5f, -2f), new float3(8f, -1.5f, 2f), new float3(-8f, -0.5f, 2f), new float3(-8f, -0.5f, -2f), new float3(8f, -0.5f, -2f), new float3(8f, -0.5f, 2f) },
					          new int[] { 1, 0, 4, 5, -1, 3, 0, 1, 2, -1, 6, 2, 1, 5, -1, 3, 2, 6, 7, -1, 4, 7, 6, 5, -1, 4, 0, 3, 7, -1 }))
				.Add(ContentsSceneNode.Composite(CSGOperationType.Additive,
					ContentsSceneNode.Brush(new float4[]
					{
						new float4(0f, -1f, 7.40718e-08f, -7f),
						new float4(0f, 1f, -7.40718e-08f, 6f),
						new float4(1f, 0f, 0f, -5.5f),
						new float4(0.707106769f, 0f, 0.7071068f, -5.656854f),
						new float4(0f, 0f, 1f, -5.49999952f),
						new float4(-0.707106769f, 0f, 0.7071068f, -5.656854f),
						new float4(-1f, 0f, 0f, -5.5f),
						new float4(-0.707106769f, 0f, -0.7071068f, -5.656854f),
						new float4(0f, 0f, -1f, -5.49999952f),
						new float4(0.707106769f, 0f, -0.7071068f, -5.656854f),
					}, contents: 0, operation: CSGOperationType.Additive,
						localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(0f, 5f, -30f, 1f)), name: "Extruded Shape (Additive) #240")
						.WithMesh(new float3[] { new float3(2.5f, -6.99999952f, 5.49999952f), new float3(-2.5f, -6.99999952f, 5.49999952f), new float3(-5.5f, -7f, 2.49999976f), new float3(-5.5f, -7f, -2.49999976f), new float3(-2.5f, -7.00000048f, -5.49999952f), new float3(2.5f, -7.00000048f, -5.49999952f), new float3(5.5f, -7f, -2.49999976f), new float3(5.5f, -7f, 2.49999976f), new float3(2.5f, -5.99999952f, 5.49999952f), new float3(-2.5f, -5.99999952f, 5.49999952f), new float3(-5.5f, -6f, 2.49999976f), new float3(-5.5f, -6f, -2.49999976f), new float3(-2.5f, -6.00000048f, -5.49999952f), new float3(2.5f, -6.00000048f, -5.49999952f), new float3(5.5f, -6f, -2.49999976f), new float3(5.5f, -6f, 2.49999976f) },
						          new int[] { 0, 1, 2, 3, 4, 5, 6, 7, -1, 14, 13, 12, 11, 10, 9, 8, 15, -1, 6, 14, 15, 7, -1, 7, 15, 8, 0, -1, 0, 8, 9, 1, -1, 1, 9, 10, 2, -1, 2, 10, 11, 3, -1, 3, 11, 12, 4, -1, 4, 12, 13, 5, -1, 5, 13, 14, 6, -1 })))
				.Add(ContentsSceneNode.Composite(CSGOperationType.Subtractive,
					ContentsSceneNode.Brush(new float4[]
					{
						new float4(0f, -1f, 8.141123e-08f, -7.25f),
						new float4(0f, 1f, -5.960465e-08f, 0.5f),
						new float4(1f, 0f, 0f, -5f),
						new float4(0.7071067f, 0f, 0.7071068f, -4.949747f),
						new float4(0f, 0f, 1f, -4.99999952f),
						new float4(-0.7071067f, 0f, 0.7071068f, -4.949747f),
						new float4(-1f, 0f, 0f, -5f),
						new float4(-0.7071067f, 0f, -0.7071068f, -4.949747f),
						new float4(0f, 0f, -1f, -4.99999952f),
						new float4(0.7071067f, 0f, -0.7071068f, -4.949747f),
					}, contents: 0, operation: CSGOperationType.Additive,
						localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(0f, -1.5f, -30f, 1f)), name: "Extruded Shape One #242")
						.WithMesh(new float3[] { new float3(2f, -7.24999952f, 4.99999952f), new float3(-2f, -7.24999952f, 4.99999952f), new float3(-5f, -7.25f, 1.99999988f), new float3(-5f, -7.25f, -1.99999988f), new float3(-2f, -7.25000048f, -4.99999952f), new float3(2f, -7.25000048f, -4.99999952f), new float3(5f, -7.25f, -1.99999988f), new float3(5f, -7.25f, 1.99999988f), new float3(2f, -0.4999997f, 4.99999952f), new float3(-2f, -0.4999997f, 4.99999952f), new float3(-5f, -0.499999881f, 1.99999988f), new float3(-5f, -0.5000001f, -1.99999988f), new float3(-2f, -0.5000003f, -4.99999952f), new float3(2f, -0.5000003f, -4.99999952f), new float3(5f, -0.5000001f, -1.99999988f), new float3(5f, -0.499999881f, 1.99999988f) },
						          new int[] { 0, 1, 2, 3, 4, 5, 6, 7, -1, 14, 13, 12, 11, 10, 9, 8, 15, -1, 6, 14, 15, 7, -1, 7, 15, 8, 0, -1, 0, 8, 9, 1, -1, 1, 9, 10, 2, -1, 2, 10, 11, 3, -1, 3, 11, 12, 4, -1, 4, 12, 13, 5, -1, 5, 13, 14, 6, -1 })))
				.Add(ContentsSceneNode.Composite(CSGOperationType.Subtractive,
					ContentsSceneNode.Brush(new float4[]
					{
						new float4(0f, -1f, 8.141123e-08f, -7f),
						new float4(0f, 1f, -8.141123e-08f, 6f),
						new float4(1f, 0f, 0f, -5f),
						new float4(0.7071067f, 0f, 0.7071068f, -4.949747f),
						new float4(0f, 0f, 1f, -4.99999952f),
						new float4(-0.7071067f, 0f, 0.7071068f, -4.949747f),
						new float4(-1f, 0f, 0f, -5f),
						new float4(-0.7071067f, 0f, -0.7071068f, -4.949747f),
						new float4(0f, 0f, -1f, -4.99999952f),
						new float4(0.7071067f, 0f, -0.7071068f, -4.949747f),
					}, contents: 0, operation: CSGOperationType.Additive,
						localToTree: new float4x4(new float4(-1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, -1f, 0f), new float4(0f, 5f, -30f, 1f)), name: "Extruded Shape (Remove) #274")
						.WithMesh(new float3[] { new float3(2f, -6.99999952f, 4.99999952f), new float3(-2f, -6.99999952f, 4.99999952f), new float3(-5f, -7f, 1.99999988f), new float3(-5f, -7f, -1.99999988f), new float3(-2f, -7.00000048f, -4.99999952f), new float3(2f, -7.00000048f, -4.99999952f), new float3(5f, -7f, -1.99999988f), new float3(5f, -7f, 1.99999988f), new float3(2f, -5.99999952f, 4.99999952f), new float3(-2f, -5.99999952f, 4.99999952f), new float3(-5f, -6f, 1.99999988f), new float3(-5f, -6f, -1.99999988f), new float3(-2f, -6.00000048f, -4.99999952f), new float3(2f, -6.00000048f, -4.99999952f), new float3(5f, -6f, -1.99999988f), new float3(5f, -6f, 1.99999988f) },
						          new int[] { 0, 1, 2, 3, 4, 5, 6, 7, -1, 14, 13, 12, 11, 10, 9, 8, 15, -1, 6, 14, 15, 7, -1, 7, 15, 8, 0, -1, 0, 8, 9, 1, -1, 1, 9, 10, 2, -1, 2, 10, 11, 3, -1, 3, 11, 12, 4, -1, 4, 12, 13, 5, -1, 5, 13, 14, 6, -1 })))
				;
			
			AssertNothingDrawnIn(scene, new float3(-3.5f, -2.5f, -33.5f), new float3(3.5f, -1.5f, -26.5f), "the pit as first built");
		}
	}
}
