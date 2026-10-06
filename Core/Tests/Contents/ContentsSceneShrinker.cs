using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	public static class ContentsSceneShrinker
	{
		public static ContentsScene Shrink(ContentsScene scene, Func<ContentsScene, bool> stillFails,
										   int maxPasses = 8)
		{
			var current = Clone(scene);
			if (!stillFails(current))
				return current;

			for (int pass = 0; pass < maxPasses; pass++)
			{
				var removedAnything = false;
				var count = CountNodes(current.roots);
				for (int index = count - 1; index >= 0; index--)
				{
					var candidate = Clone(current);
					if (!RemoveNodeAt(candidate.roots, index))
						continue;
					if (candidate.roots.Count == 0 || !stillFails(candidate))
						continue;
					current = candidate;
					removedAnything = true;
					break;
				}
				if (!removedAnything)
					break;
			}

			// A composite with a single child says nothing the child doesn't, unless the composite is
			// what makes the child carve.
			Flatten(current.roots);
			return current;
		}

		public static string ToCSharp(ContentsScene scene, string sceneName = "scene")
		{
			var text = new StringBuilder();
			text.AppendLine($"var {sceneName} = new ContentsScene()");
			foreach (var node in scene.roots)
				AppendNode(text, node, "\t");
			text.AppendLine("\t;");
			return text.ToString();
		}

		static void AppendNode(StringBuilder text, ContentsSceneNode node, string indent)
		{
			text.Append(indent).Append(".Add(");
			AppendExpression(text, node, indent);
			text.AppendLine(")");
		}

		// The node as one expression. Children are arguments, not chained Adds, so they read as a list.
		static void AppendExpression(StringBuilder text, ContentsSceneNode node, string indent)
		{
			if (node.IsBrush)
			{
				text.AppendLine("ContentsSceneNode.Brush(new float4[]");
				text.AppendLine($"{indent}{{");
				foreach (var plane in node.localPlanes)
					text.AppendLine($"{indent}\tnew float4({F(plane.x)}, {F(plane.y)}, {F(plane.z)}, {F(plane.w)}),");
				text.AppendLine($"{indent}}}, contents: {node.contents}, operation: CSGOperationType.{node.operation},");
				text.Append($"{indent}\tlocalToTree: {Matrix(node.localToTree)}, name: \"{node.name}\")");
				if (node.localVertices != null)
				{
					// its own mesh, which registration derives the planes the CSG gets from
					text.AppendLine();
					text.Append($"{indent}\t.WithMesh(new float3[] {{ ");
					for (int i = 0; i < node.localVertices.Length; i++)
					{
						var v = node.localVertices[i];
						text.Append($"new float3({F(v.x)}, {F(v.y)}, {F(v.z)})").Append(i + 1 < node.localVertices.Length ? ", " : " ");
					}
					text.Append($"}}, new int[] {{ {string.Join(", ", node.polygonLoops)} }})");
				}
				return;
			}

			if (node.children.Count == 0)
			{
				text.Append($"ContentsSceneNode.Composite(CSGOperationType.{node.operation})");
				return;
			}
			text.AppendLine($"ContentsSceneNode.Composite(CSGOperationType.{node.operation},");
			for (int i = 0; i < node.children.Count; i++)
			{
				text.Append(indent).Append('\t');
				AppendExpression(text, node.children[i], indent + "\t");
				if (i + 1 < node.children.Count)
					text.AppendLine(",");
			}
			text.Append(")");
		}

		static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture) + "f";

		static string Matrix(float4x4 matrix)
		{
			if (matrix.Equals(float4x4.identity))
				return "float4x4.identity";
			var text = new StringBuilder("new float4x4(");
			for (int c = 0; c < 4; c++)
			{
				var column = matrix[c];
				text.Append($"new float4({F(column.x)}, {F(column.y)}, {F(column.z)}, {F(column.w)})");
				if (c < 3)
					text.Append(", ");
			}
			text.Append(")");
			return text.ToString();
		}

		public static ContentsScene Clone(ContentsScene scene)
		{
			var copy = new ContentsScene();
			foreach (var node in scene.roots)
				copy.Add(CloneNode(node));
			return copy;
		}

		static ContentsSceneNode CloneNode(ContentsSceneNode node)
		{
			if (node.IsBrush)
			{
				return ContentsSceneNode.Brush(node.localPlanes, node.contents, node.operation,
											   node.localToTree, node.name).WithMesh(node.localVertices, node.polygonLoops);
			}
			var children = new ContentsSceneNode[node.children.Count];
			for (int i = 0; i < node.children.Count; i++)
				children[i] = CloneNode(node.children[i]);
			return ContentsSceneNode.Composite(node.operation, children);
		}

		static int CountNodes(List<ContentsSceneNode> nodes)
		{
			var count = 0;
			foreach (var node in nodes)
			{
				count++;
				if (!node.IsBrush)
					count += CountNodes(node.children);
			}
			return count;
		}

		// Removes the node at a depth-first index, wherever it sits in the tree.
		static bool RemoveNodeAt(List<ContentsSceneNode> nodes, int index)
		{
			var running = 0;
			return RemoveNodeAt(nodes, index, ref running);
		}

		static bool RemoveNodeAt(List<ContentsSceneNode> nodes, int index, ref int running)
		{
			for (int i = 0; i < nodes.Count; i++)
			{
				if (running == index)
				{
					nodes.RemoveAt(i);
					return true;
				}
				running++;

				var node = nodes[i];
				if (node.IsBrush)
					continue;
				if (RemoveNodeAt(node.children, index, ref running))
				{
					if (node.children.Count == 0)
						nodes.RemoveAt(i);
					return true;
				}
			}
			return false;
		}

		static void Flatten(List<ContentsSceneNode> nodes)
		{
			for (int i = 0; i < nodes.Count; i++)
			{
				var node = nodes[i];
				if (node.IsBrush)
					continue;
				Flatten(node.children);
				if (node.children.Count == 1 && node.operation == CSGOperationType.Additive &&
					node.children[0].operation == CSGOperationType.Additive)
				{
					nodes[i] = node.children[0];
					i--;
				}
			}
		}
	}
}
