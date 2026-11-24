using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class IntersectingNodeTests
	{
		// near overlaps inter; far and away touch nothing
		static ContentsSceneNode Near()  => Box("near",  new float3(0),         new float3(2));
		static ContentsSceneNode Far()   => Box("far",   new float3(10, 0, 0),  new float3(12, 2, 2));
		static ContentsSceneNode Away()  => Box("away",  new float3(20, 0, 0),  new float3(22, 2, 2), CSGOperationType.Intersecting);
		static ContentsSceneNode Inter(CSGOperationType operation = CSGOperationType.Intersecting)
			=> Box("inter", new float3(1, -1, -1), new float3(3, 3, 3), operation);

		const int kWholeBox = 12;

		static ContentsSceneNode Box(string name, float3 min, float3 max, CSGOperationType operation = CSGOperationType.Additive)
		{
			return ContentsSceneNode.Brush(ContentsScene.BoxPlanes(min, max), operation: operation, name: name);
		}

		static ContentsTreeHarness BuildAndUpdate(ContentsScene scene, string what)
		{
			var harness = ContentsTreeHarness.Build(scene);
			Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
			return harness;
		}

		static void UpdateAndAssert(ContentsTreeHarness harness, string what)
		{
			Assert.That(harness.Update(), Is.True, what + ": the CSG update did not run");
			ContentsComparison.AssertMatchesOracle(harness, what);
		}

		#region Full builds
		[Test]
		public void Intersect_RemovesBrushesItDoesNotTouch()
		{
			var near = Near(); var far = Far(); var inter = Inter();
			var scene = new ContentsScene().Add(near).Add(far).Add(inter);
			using (var harness = BuildAndUpdate(scene, "near, far, inter"))
			{
				ContentsComparison.AssertMatchesOracle(harness, "near, far, inter");
				Assert.That(harness.TriangleCountOf(far),   Is.EqualTo(0), "far is outside inter, so nothing of it survives");
				Assert.That(harness.TriangleCountOf(near),  Is.GreaterThan(0));
				Assert.That(harness.TriangleCountOf(inter), Is.GreaterThan(0));
			}
		}

		[Test]
		public void Intersect_ThatTouchesNothing_RemovesEverythingBeforeIt()
		{
			var scene = new ContentsScene().Add(Near()).Add(Far()).Add(Away());
			using (var harness = BuildAndUpdate(scene, "near, far, away"))
			{
				ContentsComparison.AssertMatchesOracle(harness, "near, far, away");
				Assert.That(harness.Triangles.Count, Is.EqualTo(0), "nothing is inside away");
			}
		}

		// Its reach ends at its own composite, on either side of it.
		[Test]
		public void Intersect_OnlyReachesItsOwnComposite([Values] bool farFirst)
		{
			var far       = Far();
			var composite = ContentsSceneNode.Composite(CSGOperationType.Additive, Near(), Inter());
			var scene     = farFirst ? new ContentsScene().Add(far).Add(composite)
									 : new ContentsScene().Add(composite).Add(far);
			var what      = farFirst ? "far, [near, inter]" : "[near, inter], far";
			using (var harness = BuildAndUpdate(scene, what))
			{
				ContentsComparison.AssertMatchesOracle(harness, what);
				Assert.That(harness.TriangleCountOf(far), Is.EqualTo(kWholeBox), "far is outside the composite inter works in");
			}
		}

		// The same holds for an intersecting composite, whose brushes are additive inside it.
		[Test]
		public void IntersectingComposite_RemovesBrushesItDoesNotTouch()
		{
			var far       = Far();
			var composite = ContentsSceneNode.Composite(CSGOperationType.Intersecting, Inter(CSGOperationType.Additive));
			var scene     = new ContentsScene().Add(Near()).Add(far).Add(composite);
			using (var harness = BuildAndUpdate(scene, "near, far, [Int: inter]"))
			{
				ContentsComparison.AssertMatchesOracle(harness, "near, far, [Int: inter]");
				Assert.That(harness.TriangleCountOf(far), Is.EqualTo(0));
			}
		}

		// A brush after the intersect overlaps one the intersect removed, so none of it is cut away
		[Test]
		public void BrushAfterAnIntersect_IsNotCutByTheBrushItRemoved()
		{
			var p = Box("p", new float3(0), new float3(2));
			var r = Box("r", new float3(1), new float3(3));
			var scene = new ContentsScene().Add(p).Add(Far().WithOperation(CSGOperationType.Intersecting)).Add(r);
			using (var harness = BuildAndUpdate(scene, "p, far(Int), r"))
			{
				ContentsComparison.AssertMatchesOracle(harness, "p, far(Int), r");
				Assert.That(harness.TriangleCountOf(p), Is.EqualTo(0));
			}
		}

		// The same, with the intersect removing p inside a composite and r outside it.
		[Test]
		public void BrushOutsideTheComposite_IsNotCutByTheBrushItRemoved()
		{
			var p = Box("p", new float3(0), new float3(2));
			var r = Box("r", new float3(1), new float3(3));
			var composite = ContentsSceneNode.Composite(CSGOperationType.Additive, p, Far().WithOperation(CSGOperationType.Intersecting));
			var scene = new ContentsScene().Add(composite).Add(r);
			using (var harness = BuildAndUpdate(scene, "[p, far(Int)], r"))
			{
				ContentsComparison.AssertMatchesOracle(harness, "[p, far(Int)], r");
				Assert.That(harness.TriangleCountOf(p), Is.EqualTo(0));
			}
		}
		#endregion

		#region Edits
		[Test]
		public void AddingAnIntersect_RemovesBrushesItDoesNotTouch()
		{
			var far = Far();
			var scene = new ContentsScene().Add(Near()).Add(far);
			using (var harness = BuildAndUpdate(scene, "near, far"))
			{
				harness.Insert(null, 2, Inter());
				UpdateAndAssert(harness, "near, far, then inter added");
				Assert.That(harness.TriangleCountOf(far), Is.EqualTo(0));
			}
		}

		[Test]
		public void RemovingAnIntersect_BringsBackBrushesItDidNotTouch([Values] bool destroy)
		{
			var far = Far(); var inter = Inter();
			var scene = new ContentsScene().Add(Near()).Add(far).Add(inter);
			using (var harness = BuildAndUpdate(scene, "near, far, inter"))
			{
				harness.Remove(inter, destroy);
				UpdateAndAssert(harness, destroy ? "inter destroyed" : "inter detached");
				Assert.That(harness.TriangleCountOf(far), Is.EqualTo(kWholeBox));
			}
		}

		[Test]
		public void ChangingAnOperation_ToAndFromIntersecting()
		{
			var far = Far(); var box = Inter(CSGOperationType.Additive);
			var scene = new ContentsScene().Add(Near()).Add(far).Add(box);
			using (var harness = BuildAndUpdate(scene, "near, far, inter(Add)"))
			{
				harness.SetOperation(box, CSGOperationType.Intersecting);
				UpdateAndAssert(harness, "inter made intersecting");
				Assert.That(harness.TriangleCountOf(far), Is.EqualTo(0));

				harness.SetOperation(box, CSGOperationType.Additive);
				UpdateAndAssert(harness, "inter made additive again");
				Assert.That(harness.TriangleCountOf(far), Is.EqualTo(kWholeBox));
			}
		}

		// At the front of its branch an intersect has nothing before it, and is skipped.
		[Test]
		public void MovingAnIntersectToTheFront_BringsBackBrushesItDidNotTouch()
		{
			var near = Near(); var far = Far(); var inter = Inter();
			var scene = new ContentsScene().Add(near).Add(far).Add(inter);
			using (var harness = BuildAndUpdate(scene, "near, far, inter"))
			{
				harness.Move(inter, null, 0);
				UpdateAndAssert(harness, "inter moved to the front");
				Assert.That(harness.TriangleCountOf(far), Is.EqualTo(kWholeBox));
			}
		}

		// The intersect lands in a composite and removes p there; r, outside the composite, touches p
		// but not the intersect, and has to be rebuilt through p.
		[Test]
		public void AddingAnIntersectToAComposite_RebuildsATouchingBrushOutsideIt()
		{
			var p = Box("p", new float3(0), new float3(2));
			var r = Box("r", new float3(1), new float3(3));
			var composite = ContentsSceneNode.Composite(CSGOperationType.Additive, p);
			var scene = new ContentsScene().Add(composite).Add(r);
			using (var harness = BuildAndUpdate(scene, "[p], r"))
			{
				harness.Insert(composite, 1, Far().WithOperation(CSGOperationType.Intersecting));
				UpdateAndAssert(harness, "[p, far(Int)], r");
				Assert.That(harness.TriangleCountOf(p), Is.EqualTo(0));
			}
		}

		// A composite's operation is part of the routing table of every brush below it, so changing it dirties them all
		[Test]
		public void ChangingACompositeOperation_RebuildsItsBrushes()
		{
			var a = Box("a", new float3(0), new float3(2));
			var b = Box("b", new float3(1), new float3(3));
			var composite = ContentsSceneNode.Composite(CSGOperationType.Additive, b);
			var scene = new ContentsScene().Add(a).Add(composite);
			using (var harness = BuildAndUpdate(scene, "a, [Add: b]"))
			{
				harness.SetOperation(composite, CSGOperationType.Subtractive);
				UpdateAndAssert(harness, "a, [Sub: b]");
				harness.SetOperation(composite, CSGOperationType.Intersecting);
				UpdateAndAssert(harness, "a, [Int: b]");
				harness.SetOperation(composite, CSGOperationType.Additive);
				UpdateAndAssert(harness, "a, [Add: b] again");
			}
		}

		[Test]
		public void MovingAComposite_RebuildsItsBrushes()
		{
			var a = Box("a", new float3(0), new float3(2));
			var c = Box("c", new float3(1), new float3(3));
			var composite = ContentsSceneNode.Composite(CSGOperationType.Subtractive, c);
			var scene = new ContentsScene().Add(a).Add(composite);
			using (var harness = BuildAndUpdate(scene, "a, [Sub: c]"))
			{
				harness.Move(composite, null, 0);
				UpdateAndAssert(harness, "[Sub: c], a");
			}
		}

		static void Shift(ContentsTreeHarness harness, ContentsSceneNode brush, float3 offset)
			=> harness.SetLocalToTree(brush, float4x4.Translate(offset));

		// An intersect dragged from the brush before it that it touches onto another brush before it, into empty space,
		// and back: each brush before it is kept only where the intersect is
		[Test]
		public void MovingAnIntersect_OntoAndAwayFromTheBrushesBeforeIt()
		{
			var near = Near(); var far = Far(); var inter = Inter();
			var scene = new ContentsScene().Add(near).Add(far).Add(inter);
			using (var harness = BuildAndUpdate(scene, "near, far, inter"))
			{
				Shift(harness, inter, new float3(9.5f, 0, 0));
				UpdateAndAssert(harness, "inter moved onto far");
				Assert.That(harness.TriangleCountOf(near), Is.EqualTo(0), "near is outside inter now");
				Assert.That(harness.TriangleCountOf(far), Is.GreaterThan(0));

				Shift(harness, inter, new float3(30, 0, 0));
				UpdateAndAssert(harness, "inter moved into empty space");
				Assert.That(harness.TriangleCountOf(near) + harness.TriangleCountOf(far), Is.EqualTo(0), "nothing is inside inter");

				Shift(harness, inter, new float3(0));
				UpdateAndAssert(harness, "inter moved back");
				Assert.That(harness.TriangleCountOf(near), Is.GreaterThan(0));
				Assert.That(harness.TriangleCountOf(far), Is.EqualTo(0));
			}
		}

		// A brush before an intersect moved out of it and back
		[Test]
		public void MovingABrushBeforeAnIntersect_OutOfItAndBack()
		{
			var near = Near(); var inter = Inter();
			var scene = new ContentsScene().Add(near).Add(Far()).Add(inter);
			using (var harness = BuildAndUpdate(scene, "near, far, inter"))
			{
				Shift(harness, near, new float3(-5, 0, 0));
				UpdateAndAssert(harness, "near moved out of inter");
				Assert.That(harness.TriangleCountOf(near), Is.EqualTo(0));

				Shift(harness, near, new float3(0));
				UpdateAndAssert(harness, "near moved back");
				Assert.That(harness.TriangleCountOf(near), Is.GreaterThan(0));
			}
		}

		// A brush the intersect removes, reordered to after the intersect and back: after it, nothing cuts it
		[Test]
		public void ReorderingABrushPastAnIntersect_AndBack()
		{
			var far = Far();
			var scene = new ContentsScene().Add(Near()).Add(far).Add(Inter());
			using (var harness = BuildAndUpdate(scene, "near, far, inter"))
			{
				harness.Move(far, null, 2);
				UpdateAndAssert(harness, "near, inter, far");
				Assert.That(harness.TriangleCountOf(far), Is.EqualTo(kWholeBox));

				harness.Move(far, null, 1);
				UpdateAndAssert(harness, "near, far, inter again");
				Assert.That(harness.TriangleCountOf(far), Is.EqualTo(0));
			}
		}

		// A brush reparented into the composite of an intersect that does not touch it, and out again
		[Test]
		public void ReparentingABrushIntoAndOutOfAnIntersectsComposite([Values] bool atTheFront)
		{
			var far = Far();
			var composite = ContentsSceneNode.Composite(CSGOperationType.Additive, Near(), Inter());
			var scene = new ContentsScene().Add(composite).Add(far);
			using (var harness = BuildAndUpdate(scene, "[near, inter], far"))
			{
				harness.Move(far, composite, atTheFront ? 0 : 1);
				UpdateAndAssert(harness, "far moved into the composite before inter");
				Assert.That(harness.TriangleCountOf(far), Is.EqualTo(0));

				harness.Move(far, null, 1);
				UpdateAndAssert(harness, "far moved out again");
				Assert.That(harness.TriangleCountOf(far), Is.EqualTo(kWholeBox));
			}
		}

		// An intersect moved inside its composite onto p; r, outside the composite, touches p but never the intersect, and
		// has to be rebuilt through p each time
		[Test]
		public void MovingAnIntersectInAComposite_RebuildsATouchingBrushOutsideIt()
		{
			var p = Box("p", new float3(0), new float3(2));
			var r = Box("r", new float3(1), new float3(3));
			var inter = Far().WithOperation(CSGOperationType.Intersecting);
			var composite = ContentsSceneNode.Composite(CSGOperationType.Additive, p, inter);
			var scene = new ContentsScene().Add(composite).Add(r);
			using (var harness = BuildAndUpdate(scene, "[p, far(Int)], r"))
			{
				Shift(harness, inter, new float3(-10.5f, 0, 0));
				UpdateAndAssert(harness, "the intersect moved onto part of p");
				Assert.That(harness.TriangleCountOf(p), Is.GreaterThan(0));

				Shift(harness, inter, new float3(0));
				UpdateAndAssert(harness, "the intersect moved away again");
				Assert.That(harness.TriangleCountOf(p), Is.EqualTo(0));
			}
		}

		// Random moves, operation changes, reorders and reparenting among brushes, intersects and a composite holding one:
		// after every edit the update must give what the scene is
		[Test]
		public void RandomEditsAroundIntersects_GiveWhatTheSceneIs([Values(1, 2, 3)] int seed)
		{
			var random    = new Unity.Mathematics.Random((uint)seed * 104729u);
			var brushes   = new List<ContentsSceneNode>
			{
				Box("a", new float3(0), new float3(2)),
				Box("b", new float3(1.5f, 0, 0), new float3(3.5f, 2, 2)),
				Box("c", new float3(4, 0, 0), new float3(6, 2, 2)),
				Box("i", new float3(1, -0.5f, -0.5f), new float3(5, 1.5f, 2.5f), CSGOperationType.Intersecting),
				Box("p", new float3(0, 0, 3), new float3(3, 2, 5)),
				Box("q", new float3(2, 0.5f, 2.5f), new float3(4, 1.5f, 5.5f), CSGOperationType.Intersecting),
				Box("r", new float3(2.5f, 0, 4), new float3(5, 2, 6)),
			};
			var composite = ContentsSceneNode.Composite(CSGOperationType.Additive, brushes[4], brushes[5]);
			composite.name = "composite";
			var scene = new ContentsScene().Add(brushes[0]).Add(brushes[1]).Add(brushes[2]).Add(brushes[3]).Add(composite).Add(brushes[6]);
			var offsets = new Dictionary<ContentsSceneNode, float3>();
			using var harness = BuildAndUpdate(scene, "first update");
			ContentsComparison.AssertMatchesOracle(harness, "first update");
			for (int step = 0; step < 30; step++)
			{
				var brush = brushes[random.NextInt(brushes.Count)];
				var inComposite = composite.children.Contains(brush);
				var siblings = inComposite ? composite.children : harness.Scene.roots;
				string what;
				switch (random.NextInt(4))
				{
					case 0:
					{
						var offset = random.NextInt(3) switch { 0 => new float3(0.5f, 0, 0), 1 => new float3(0, 0.5f, 0), _ => new float3(0, 0, 0.5f) };
						if (random.NextBool()) offset = -offset;
						offsets.TryGetValue(brush, out var at);
						offsets[brush] = at + offset;
						Shift(harness, brush, at + offset);
						what = $"{brush.name} moved by {offset}";
						break;
					}
					case 1:
					{
						var operation = (CSGOperationType)random.NextInt(3);
						harness.SetOperation(brush, operation);
						what = $"{brush.name} made {operation}";
						break;
					}
					case 2:
					{
						int index = random.NextInt(siblings.Count);
						harness.Move(brush, inComposite ? composite : null, index);
						what = $"{brush.name} reordered to {index}";
						break;
					}
					default:
					{
						if (inComposite && composite.children.Count == 1)
						{
							what = $"{brush.name} left alone, the last in the composite";
							break;
						}
						var target = inComposite ? harness.Scene.roots : composite.children;
						int index = random.NextInt(target.Count + 1);
						harness.Move(brush, inComposite ? null : composite, index);
						what = $"{brush.name} moved {(inComposite ? "out of" : "into")} the composite at {index}";
						break;
					}
				}
				// an edit that changes nothing (the same operation, the same place) leaves nothing to update
				harness.Update();
				ContentsComparison.AssertMatchesOracle(harness, $"seed {seed} step {step}: {what}");
			}
		}
		#endregion
	}

	static class ContentsSceneNodeTestExtensions
	{
		public static ContentsSceneNode WithOperation(this ContentsSceneNode node, CSGOperationType operation)
		{
			node.operation = operation;
			return node;
		}
	}
}
