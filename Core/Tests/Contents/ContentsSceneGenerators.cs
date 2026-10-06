using System.Collections.Generic;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Chisel.Core.Tests
{
	public static class ContentsSceneGenerators
	{
		public const int kDefaultContentsCount = 4;

		enum BrushKind { GridBox, Free, Cut, NearMissBox }

		public static ContentsScene GridBoxes(int seed, int contentsCount = kDefaultContentsCount)
		{
			var random = MakeRandom(seed);
			var scene  = new ContentsScene();
			BuildTree(ref random, scene, contentsCount, BrushKind.GridBox);
			return scene;
		}

		public static ContentsScene RotatedGridBoxes(int seed, int contentsCount = kDefaultContentsCount)
		{
			var random = MakeRandom(seed);
			var scene  = new ContentsScene();
			BuildTree(ref random, scene, contentsCount, BrushKind.GridBox);
			var placement = float4x4.TRS(random.NextFloat3(-4f, 4f),
										 quaternion.Euler(random.NextFloat3(-math.PI, math.PI)),
										 new float3(1));
			ApplyPlacement(scene.roots, placement);
			return scene;
		}

		// Boxes, some with a redundant seventh plane (see BoxWithTangentPlanePlanes), each with its own
		// rotation and scale, so brushes meet at arbitrary angles and almost never share a plane.
		public static ContentsScene FreeBrushes(int seed, int contentsCount = kDefaultContentsCount)
		{
			var random = MakeRandom(seed);
			var scene  = new ContentsScene();
			BuildTree(ref random, scene, contentsCount, BrushKind.Free);
			return scene;
		}

		// The layouts of FreeBrushes with real wedges where FreeBrushes has its redundant plane: the
		// random sequence is the same, only that seventh plane differs.
		public static ContentsScene CutBrushes(int seed, int contentsCount = kDefaultContentsCount)
		{
			var random = MakeRandom(seed);
			var scene  = new ContentsScene();
			BuildTree(ref random, scene, contentsCount, BrushKind.Cut);
			return scene;
		}

		public static ContentsScene NearMissBoxes(int seed, bool rotated, bool allowIntersecting = false)
		{
			var random = MakeRandom(seed);
			var scene  = new ContentsScene();
			BuildTree(ref random, scene, 1, BrushKind.NearMissBox, allowIntersecting);
			if (rotated)
				Turn(ref random, scene);
			return scene;
		}

		public static ContentsScene NearMissSlivers(int seed, bool rotated)
		{
			var random = MakeRandom(seed);
			var scene  = new ContentsScene();
			var pairs  = random.NextInt(1, 4);
			for (int pair = 0; pair < pairs; pair++)
			{
				var axisU = random.NextInt(0, 3);
				var axisV = (axisU + random.NextInt(1, 3)) % 3;
				var axisW = 3 - axisU - axisV;

				var slope = random.NextInt(0, 3);
				var run   = slope == 1 ? 3 : 1;
				var rise  = slope == 2 ? 3 : 1;
				var min   = (float3)random.NextInt3(new int3(0), new int3(4));
				var size  = (float3)random.NextInt3(new int3(2), new int3(4));
				size[axisU] = run  + random.NextInt(1, 3);
				size[axisV] = rise + random.NextInt(1, 3);
				var max   = min + size;
				scene.Add(ContentsSceneNode.Brush(SlopedBoxPlanes(min, max, axisU, axisV, run, rise, 0), name: "w" + pair));

				// The slope's height at u0, and the box's corner delta below it, inside the wedge
				var along = random.NextFloat(0.25f, 0.75f);
				var delta = NudgeMagnitude(ref random);
				var u0    = max[axisU] - run + (along * run);
				var v0    = max[axisV] - (along * rise) - delta;

				// Strictly inside the wedge along w, so the box's edges cross the inside of the slope
				var neighbourMin = float3.zero;
				var neighbourMax = float3.zero;
				neighbourMin[axisU] = u0;
				neighbourMax[axisU] = u0 + random.NextInt(1, 3);
				neighbourMin[axisV] = v0;
				neighbourMax[axisV] = v0 + random.NextInt(1, 3);
				neighbourMin[axisW] = min[axisW] + (random.NextFloat(0.1f, 0.4f) * size[axisW]);
				neighbourMax[axisW] = min[axisW] + (random.NextFloat(0.6f, 0.9f) * size[axisW]);
				scene.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(neighbourMin, neighbourMax), name: "n" + pair));
			}
			if (rotated)
				Turn(ref random, scene);
			return scene;
		}

		public static ContentsScene NearMissLedges(int seed, bool rotated)
		{
			var random = MakeRandom(seed);
			var scene  = new ContentsScene();
			var pairs  = random.NextInt(1, 4);
			for (int pair = 0; pair < pairs; pair++)
			{
				var axisU = random.NextInt(0, 3);
				var axisV = (axisU + random.NextInt(1, 3)) % 3;
				var axisW = 3 - axisU - axisV;

				var min    = (float3)random.NextInt3(new int3(0), new int3(4));
				var max    = min + (float3)random.NextInt3(new int3(2), new int3(4));
				var alongU = random.NextInt(1, (int)(max[axisU] - min[axisU]));
				var alongV = random.NextInt(1, (int)(max[axisV] - min[axisV]));
				var offset = NudgeMagnitude(ref random) * (random.NextBool() ? 1 : -1);
				scene.Add(ContentsSceneNode.Brush(SlopedBoxPlanes(min, max, axisU, axisV, alongU, alongV, offset),
												  name: "w" + pair));

				// On the cut face, with its edge on the grid line the slope just misses
				var neighbourMin = float3.zero;
				var neighbourMax = float3.zero;
				neighbourMin[axisU] = max[axisU] - alongU;
				neighbourMax[axisU] = neighbourMin[axisU] + random.NextInt(1, 3);
				neighbourMin[axisV] = max[axisV];
				neighbourMax[axisV] = neighbourMin[axisV] + random.NextInt(1, 3);
				neighbourMin[axisW] = min[axisW] + random.NextInt(0, (int)(max[axisW] - min[axisW]));
				neighbourMax[axisW] = neighbourMin[axisW] + random.NextInt(1, 3);
				scene.Add(ContentsSceneNode.Brush(ContentsScene.BoxPlanes(neighbourMin, neighbourMax), name: "n" + pair));
			}
			if (rotated)
				Turn(ref random, scene);
			return scene;
		}

		// A box with its (maxU, maxV) edge cut off by a slope from (maxU - alongU - offset, maxV) to
		// (maxU, maxV - alongV).
		static float4[] SlopedBoxPlanes(float3 min, float3 max, int axisU, int axisV, int alongU, int alongV, float offset)
		{
			var onCut = max;
			onCut[axisU] = max[axisU] - alongU - offset;
			var normal = float3.zero;
			normal[axisU] = alongV;
			normal[axisV] = alongU + offset;
			normal = math.normalize(normal);

			var box    = ContentsScene.BoxPlanes(min, max);
			var result = new float4[box.Length + 1];
			box.CopyTo(result, 0);
			result[box.Length] = new float4(normal, -math.dot(normal, onCut));
			return result;
		}

		static void Turn(ref Random random, ContentsScene scene)
		{
			var placement = float4x4.TRS(random.NextFloat3(-4f, 4f),
										 quaternion.Euler(random.NextFloat3(-math.PI, math.PI)),
										 new float3(1));
			ApplyPlacement(scene.roots, placement);
		}

		// Unity.Mathematics.Random rejects a zero state, and a caller passing seed 0 is likely.
		static Random MakeRandom(int seed) => new Random((uint)(seed * 747796405 + 2891336453) | 1u);

		static void BuildTree(ref Random random, ContentsScene scene, int contentsCount, BrushKind kind, bool allowIntersecting = true)
		{
			var count = random.NextInt(2, 7);
			var made  = new List<ContentsSceneNode>(count);
			for (int i = 0; i < count; i++)
			{
				var planes = kind == BrushKind.GridBox     ? GridBoxPlanes(ref random) :
							 kind == BrushKind.NearMissBox ? NearMissBoxPlanes(ref random) :
															 FreeBrushPlanes(ref random, kind);
				var node = ContentsSceneNode.Brush(
					planes,
					contents:  random.NextInt(0, contentsCount),
					operation: i == 0 ? CSGOperationType.Additive : RandomOperation(ref random, allowIntersecting),
					name:      "b" + i);

				if (kind == BrushKind.Free || kind == BrushKind.Cut)
				{
					node.localToTree = float4x4.TRS(random.NextFloat3(-3f, 3f),
													quaternion.Euler(random.NextFloat3(-math.PI, math.PI)),
													random.NextFloat3(0.5f, 2f));
				}
				made.Add(node);
			}

			var index = 0;
			while (index < made.Count)
			{
				// Occasionally wrap two or three siblings in a composite, which is where a carving
				// composite turns additive brushes into carving ones.
				var groupSize = (index > 0 && made.Count - index >= 2 && random.NextFloat() < 0.3f)
					? random.NextInt(2, math.min(4, made.Count - index + 1))
					: 1;
				if (groupSize == 1)
				{
					scene.Add(made[index]);
					index++;
					continue;
				}

				var children = new ContentsSceneNode[groupSize];
				for (int i = 0; i < groupSize; i++)
					children[i] = made[index + i];
				// A composite's children start from empty, so the first one has to add something.
				children[0].operation = CSGOperationType.Additive;
				scene.Add(ContentsSceneNode.Composite(RandomOperation(ref random, allowIntersecting), children));
				index += groupSize;
			}
		}

		static float4[] GridBoxPlanes(ref Random random)
		{
			var min = (float3)random.NextInt3(new int3(0), new int3(5));
			var max = math.min(min + (float3)random.NextInt3(new int3(1), new int3(4)), new float3(6));
			return ContentsScene.BoxPlanes(min, max);
		}

		static float4[] FreeBrushPlanes(ref Random random, BrushKind kind)
		{
			var size  = random.NextFloat3(0.75f, 3f);
			var wedge = random.NextFloat() < 0.35f;
			if (!wedge)
				return ContentsScene.BoxPlanes(-size * 0.5f, size * 0.5f);
			return kind == BrushKind.Cut ? ContentsScene.WedgePlanes(-size * 0.5f, size * 0.5f)
										 : ContentsScene.BoxWithTangentPlanePlanes(-size * 0.5f, size * 0.5f);
		}

		static float4[] NearMissBoxPlanes(ref Random random)
		{
			var min = (float3)random.NextInt3(new int3(0), new int3(5));
			var max = math.min(min + (float3)random.NextInt3(new int3(1), new int3(4)), new float3(6));
			min += new float3(Nudge(ref random), Nudge(ref random), Nudge(ref random));
			max += new float3(Nudge(ref random), Nudge(ref random), Nudge(ref random));
			return ContentsScene.BoxPlanes(min, max);
		}

		static float Nudge(ref Random random)
		{
			if (random.NextFloat() >= 0.4f)
				return 0;
			var magnitude = NudgeMagnitude(ref random);
			return random.NextBool() ? magnitude : -magnitude;
		}

		// 0.05 to 12 mm, log-uniformly: the band where the weld has decisions to make
		static float NudgeMagnitude(ref Random random) => math.pow(10f, random.NextFloat(math.log10(0.00005f), math.log10(0.012f)));

		static CSGOperationType RandomOperation(ref Random random, bool allowIntersecting = true)
		{
			var roll = random.NextFloat();
			if (roll < 0.70f) return CSGOperationType.Additive;
			if (roll < 0.90f || !allowIntersecting) return CSGOperationType.Subtractive;
			return CSGOperationType.Intersecting;
		}

		static void ApplyPlacement(List<ContentsSceneNode> nodes, float4x4 placement)
		{
			foreach (var node in nodes)
			{
				if (node.IsBrush)
					node.localToTree = math.mul(placement, node.localToTree);
				else
					ApplyPlacement(node.children, placement);
			}
		}

		// The control every generated scene also runs: the same geometry with one type everywhere must
		// reproduce today's output exactly.
		public static ContentsScene AllSolid(ContentsScene scene)
		{
			var copy = new ContentsScene();
			foreach (var node in scene.roots)
				copy.Add(CloneAsSolid(node));
			return copy;
		}

		static ContentsSceneNode CloneAsSolid(ContentsSceneNode node)
		{
			if (node.IsBrush)
			{
				return ContentsSceneNode.Brush(node.localPlanes, ContentsSceneNode.kSolidContents,
											   node.operation, node.localToTree, node.name);
			}
			var children = new ContentsSceneNode[node.children.Count];
			for (int i = 0; i < node.children.Count; i++)
				children[i] = CloneAsSolid(node.children[i]);
			return ContentsSceneNode.Composite(node.operation, children);
		}
	}
}
