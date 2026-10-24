using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chisel.Core.Tests
{
	[TestFixture]
	[Category("Contents")]
	public class BrushWithoutMeshTests
	{
		const int kWholeBox = 12;

		[Test]
		public void BrushWithoutMesh_IsSkippedWithoutAnException()
		{
			// What Chisel reports, rightly, about a brush it has no mesh for. Anything else logged fails the test.
			LogAssert.Expect(LogType.Error, new Regex(@"has its brushMeshID set to \(0\), which is invalid"));
			LogAssert.Expect(LogType.Error, new Regex(@"The brushMeshID is invalid"));
			LogAssert.Expect(LogType.Error, new Regex(@"^Invalid ID 0"));

			// x >= 1 and x <= -1 (and the same for y and z): no volume at all
			var nothing = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(1), new float3(-1)), name: "nothing");
			var box     = ContentsSceneNode.Brush(ContentsScene.BoxPlanes(new float3(0), new float3(2)), name: "box");
			var scene   = new ContentsScene().Add(nothing).Add(box);

			using (var harness = ContentsTreeHarness.Build(scene))
			{
				Assert.That(harness.Update(), Is.True, "the CSG update did not run");
				Assert.That(harness.TriangleCountOf(box), Is.EqualTo(kWholeBox), harness.LastUpdateReport);
				Assert.That(harness.TriangleCountOf(nothing), Is.EqualTo(0));
			}
		}
	}
}
