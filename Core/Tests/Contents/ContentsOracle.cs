using System.Collections.Generic;
using Unity.Mathematics;

namespace Chisel.Core.Tests
{
	public enum ContentsFaceVerdict
	{
		// Nothing is drawn here: either both sides are filled, or both are empty.
		None,
		// The face is drawn facing out of its brush: filled behind it, empty in front.
		FacingOut,
		// The face is drawn facing into its brush, the wall a carve leaves behind: empty behind,
		// filled in front.
		FacingIn
	}

	public sealed class ContentsOracle
	{
		public const int kSolidContents = ContentsSceneNode.kSolidContents;

		sealed class Entry
		{
			public ContentsSceneNode node;
			public float4[]          treePlanes;
			// True when this brush carves: its own operation is subtractive or intersecting, or it
			// sits inside a composite that is.
			public bool carving;
			// Left to right order in the scene, so failures name a stable brush.
			public int order;
		}

		readonly ContentsScene scene;
		readonly List<Entry> entries = new List<Entry>();
		readonly Dictionary<ContentsSceneNode, Entry> byNode = new Dictionary<ContentsSceneNode, Entry>();

		readonly Dictionary<ContentsSceneNode, (ContentsSceneNode parent, int index)> places = new Dictionary<ContentsSceneNode, (ContentsSceneNode, int)>();
		readonly Dictionary<ContentsSceneNode, List<int>> intersecting = new Dictionary<ContentsSceneNode, List<int>>();
		readonly List<int> intersectingRoots = new List<int>();

		public ContentsOracle(ContentsScene scene)
		{
			this.scene = scene;
			Flatten(scene.roots, null, ancestorCarving: false);
		}

		void Flatten(List<ContentsSceneNode> nodes, ContentsSceneNode parent, bool ancestorCarving)
		{
			if (nodes == null)
				return;
			for (int i = 0; i < nodes.Count; i++)
			{
				var node = nodes[i];
				places[node] = (parent, i);
				if (node.operation == CSGOperationType.Intersecting)
				{
					if (parent == null)
						intersectingRoots.Add(i);
					else
					{
						if (!intersecting.TryGetValue(parent, out var list))
							intersecting[parent] = list = new List<int>();
						list.Add(i);
					}
				}
				var carving = ancestorCarving ||
							  node.operation == CSGOperationType.Subtractive ||
							  node.operation == CSGOperationType.Intersecting;
				if (node.IsBrush)
				{
					var entry = new Entry
					{
						node       = node,
						treePlanes = node.TreePlanes(),
						carving    = carving,
						order      = entries.Count
					};
					entries.Add(entry);
					byNode[node] = entry;
				}
				else
					Flatten(node.children, node, carving);
			}
		}

		public IReadOnlyList<ContentsSceneNode> Brushes
		{
			get
			{
				var result = new List<ContentsSceneNode>(entries.Count);
				foreach (var entry in entries)
					result.Add(entry.node);
				return result;
			}
		}

		public float4[] TreePlanesOf(ContentsSceneNode brush) => byNode[brush].treePlanes;
		public bool     IsCarving(ContentsSceneNode brush)    => byNode[brush].carving;
		public int      OrderOf(ContentsSceneNode brush)      => byNode[brush].order;

		public bool IsFilled(float3 point, int judgingContents)
		{
			return Evaluate(scene.roots, brush => Inside(byNode[brush].treePlanes, point), judgingContents);
		}

		// The same, with each brush's membership decided by the caller: ExactJudge asks about points an infinitely small
		// step off a plane, which only exact predicates can place.
		public bool IsFilled(System.Func<ContentsSceneNode, bool> insideBrush, int judgingContents)
		{
			return Evaluate(scene.roots, insideBrush, judgingContents);
		}

		public bool IsFilled(IReadOnlyList<ContentsSceneNode> candidates, System.Func<ContentsSceneNode, bool> insideBrush, int judgingContents)
		{
			try
			{
				foreach (var candidate in candidates)
				{
					var node = candidate;
					while (involved.Add(node))
					{
						var place = places[node];
						InvolvedIn(place.parent).Add(place.index);
						if (place.parent == null)
							break;
						node = place.parent;
					}
				}
				return EvaluateInvolved(scene.roots, involvedRoots, intersectingRoots, insideBrush, judgingContents);
			}
			finally
			{
				involved.Clear();
				involvedRoots.Clear();
				foreach (var list in involvedChildren.Values)
				{
					list.Clear();
					spareLists.Push(list);
				}
				involvedChildren.Clear();
			}
		}

		readonly HashSet<ContentsSceneNode> involved = new HashSet<ContentsSceneNode>();
		readonly Dictionary<ContentsSceneNode, List<int>> involvedChildren = new Dictionary<ContentsSceneNode, List<int>>();
		readonly List<int> involvedRoots = new List<int>();
		readonly Stack<List<int>> spareLists = new Stack<List<int>>();

		List<int> InvolvedIn(ContentsSceneNode parent)
		{
			if (parent == null)
				return involvedRoots;
			if (!involvedChildren.TryGetValue(parent, out var list))
				involvedChildren[parent] = list = spareLists.Count > 0 ? spareLists.Pop() : new List<int>();
			return list;
		}

		// Evaluate over the involved and the intersecting nodes of one list of siblings, in their order.
		bool EvaluateInvolved(List<ContentsSceneNode> nodes, List<int> involvedIndices, List<int> intersectingIndices,
							  System.Func<ContentsSceneNode, bool> insideBrush, int judgingContents)
		{
			involvedIndices?.Sort();
			int involvedCount = involvedIndices?.Count ?? 0, intersectingCount = intersectingIndices?.Count ?? 0;
			var filled = false;
			for (int i = 0, j = 0; i < involvedCount || j < intersectingCount;)
			{
				int  index;
				bool isInvolved;
				if (j >= intersectingCount || (i < involvedCount && involvedIndices[i] <= intersectingIndices[j]))
				{
					index = involvedIndices[i++];
					isInvolved = true;
					if (j < intersectingCount && intersectingIndices[j] == index)
						j++;
				} else
				{
					index = intersectingIndices[j++];
					isInvolved = false;
				}

				var  node   = nodes[index];
				bool inside = false;
				if (node.IsBrush)
				{
					if (!Participates(byNode[node], judgingContents))
						continue;
					inside = isInvolved && insideBrush(node);
				} else if (isInvolved)
				{
					involvedChildren.TryGetValue(node, out var involvedBelow);
					intersecting.TryGetValue(node, out var intersectingBelow);
					inside = EvaluateInvolved(node.children, involvedBelow, intersectingBelow, insideBrush, judgingContents);
				}

				switch (node.operation)
				{
					case CSGOperationType.Additive:     filled = filled || inside;  break;
					case CSGOperationType.Subtractive:  filled = filled && !inside; break;
					case CSGOperationType.Intersecting: filled = filled && inside;  break;
				}
			}
			return filled;
		}

		bool Evaluate(List<ContentsSceneNode> nodes, System.Func<ContentsSceneNode, bool> insideBrush, int judgingContents)
		{
			var filled = false;
			if (nodes == null)
				return false;
			foreach (var node in nodes)
			{
				bool inside;
				if (node.IsBrush)
				{
					var entry = byNode[node];
					if (!Participates(entry, judgingContents))
						continue;
					inside = insideBrush(node);
				}
				else
					inside = Evaluate(node.children, insideBrush, judgingContents);

				switch (node.operation)
				{
					case CSGOperationType.Additive:     filled = filled || inside;  break;
					case CSGOperationType.Subtractive:  filled = filled && !inside; break;
					case CSGOperationType.Intersecting: filled = filled && inside;  break;
				}
			}
			return filled;
		}

		static bool Participates(Entry entry, int judgingContents)
		{
			return entry.carving ||
				   entry.node.contents == judgingContents ||
				   entry.node.contents == kSolidContents;
		}

		static bool Inside(float4[] planes, float3 point)
		{
			for (int i = 0; i < planes.Length; i++)
			{
				if (math.dot(planes[i].xyz, point) + planes[i].w > 0)
					return false;
			}
			return true;
		}

		public ContentsFaceVerdict JudgeFace(ContentsSceneNode brush, int planeIndex, float3 pointOnFace, float offset)
		{
			var entry  = byNode[brush];
			var normal = entry.treePlanes[planeIndex].xyz;
			var judgingContents = brush.contents;

			var filledBehind = IsFilled(pointOnFace - normal * offset, judgingContents);
			var filledFront  = IsFilled(pointOnFace + normal * offset, judgingContents);

			if (filledBehind && !filledFront) return ContentsFaceVerdict.FacingOut;
			if (!filledBehind && filledFront) return ContentsFaceVerdict.FacingIn;
			return ContentsFaceVerdict.None;
		}

		public static bool WinsTie(int contents, int against)
		{
			if (contents == against)                return false;
			if (contents == kSolidContents)         return true;
			if (against  == kSolidContents)         return false;
			return contents < against;
		}
	}
}
