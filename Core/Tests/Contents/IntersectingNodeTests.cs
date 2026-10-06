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

		// A brush after the intersect overlaps one the intersect removed, so none of it is cut away any
		// more, even though it touches neither the intersect nor anything that survives.
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

		// Not about intersecting in particular: a composite's operation is part of the routing table of
		// every brush below it, and changing it used to flag only the composite itself.
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
