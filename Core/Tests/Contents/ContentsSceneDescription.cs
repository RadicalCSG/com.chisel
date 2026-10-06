using System.Collections.Generic;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	public sealed class ContentsSceneNode
	{
		public const int kSolidContents = 0;

		// How this node combines with what its siblings to the left already produced.
		public CSGOperationType operation = CSGOperationType.Additive;

		// Brush only: the convex volume's planes, in local space. Null on a composite.
		public float4[] localPlanes;

		// Brush only: index into the project's contents list. 0 is Solid.
		public int contents = kSolidContents;

		// Brush only: where the volume sits in the tree.
		public float4x4 localToTree = float4x4.identity;

		// Composite only: children, evaluated left to right. Null on a brush.
		public List<ContentsSceneNode> children;

		// A label that survives shrinking, so a failure names the same brush in the printed fixture
		// as in the scene that produced it.
		public string name;

		// Brush only: the entity id its node is created with (0 unless a test needs to tell brushes apart, as
		// decal targets do).
		public ulong entityID;

		public float3[] localVertices;
		public int[]    polygonLoops;

		public bool IsBrush => localPlanes != null;

		// This brush with its own mesh (see localVertices).
		public ContentsSceneNode WithMesh(float3[] vertices, int[] loops)
		{
			localVertices = vertices;
			polygonLoops  = loops;
			return this;
		}

		public static ContentsSceneNode Brush(float4[] localPlanes, int contents = kSolidContents,
											  CSGOperationType operation = CSGOperationType.Additive,
											  float4x4 localToTree = default, string name = null)
		{
			return new ContentsSceneNode
			{
				localPlanes = localPlanes,
				contents    = contents,
				operation   = operation,
				localToTree = localToTree.Equals(default(float4x4)) ? float4x4.identity : localToTree,
				name        = name
			};
		}

		public static ContentsSceneNode Composite(CSGOperationType operation, params ContentsSceneNode[] children)
		{
			return new ContentsSceneNode
			{
				operation = operation,
				children  = new List<ContentsSceneNode>(children)
			};
		}

		public float4[] TreePlanes()
		{
			var inverseTranspose = math.transpose(math.inverse(localToTree));
			var result = new float4[localPlanes.Length];
			for (int i = 0; i < localPlanes.Length; i++)
			{
				var plane  = math.mul(inverseTranspose, localPlanes[i]);
				var length = math.length(plane.xyz);
				result[i]  = plane / length;
			}
			return result;
		}
	}

	// The whole scene: the top level children of the tree, left to right.
	public sealed class ContentsScene
	{
		public readonly List<ContentsSceneNode> roots = new List<ContentsSceneNode>();

		public ContentsScene Add(ContentsSceneNode node)
		{
			roots.Add(node);
			return this;
		}

		// Six planes of an axis aligned box, normals pointing out, so the inside is the negative side
		// of every plane.
		public static float4[] BoxPlanes(float3 min, float3 max)
		{
			return new[]
			{
				new float4(-1,  0,  0,  min.x),
				new float4( 1,  0,  0, -max.x),
				new float4( 0, -1,  0,  min.y),
				new float4( 0,  1,  0, -max.y),
				new float4( 0,  0, -1,  min.z),
				new float4( 0,  0,  1, -max.z)
			};
		}

		public static float4[] BoxWithTangentPlanePlanes(float3 min, float3 max)
		{
			var box   = BoxPlanes(min, max);
			var cut   = math.normalize(new float3(1, 1, 0));
			var plane = new float4(cut, -math.dot(cut, new float3(max.x, max.y, min.z)));
			var result = new float4[box.Length + 1];
			box.CopyTo(result, 0);
			result[box.Length] = plane;
			return result;
		}

		// A box with its (max.x, max.y) edge cut off halfway along both faces that meet there, which
		// gives the generators a face that is not axis aligned while the brush keeps all six of the box's.
		public static float4[] WedgePlanes(float3 min, float3 max)
		{
			var box    = BoxPlanes(min, max);
			var size   = max - min;
			var onCut  = new float3(max.x - (size.x * 0.5f), max.y, min.z);
			var normal = math.normalize(new float3(size.y, size.x, 0));
			var result = new float4[box.Length + 1];
			box.CopyTo(result, 0);
			result[box.Length] = new float4(normal, -math.dot(normal, onCut));
			return result;
		}
	}
}
