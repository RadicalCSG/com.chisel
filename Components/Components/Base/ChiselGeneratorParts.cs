using System;
using System.Collections.Generic;
using Chisel.Core;

namespace Chisel.Components
{
    /// <summary>
    /// Which brush of a generator something means (a decal's surface, say), so it keeps meaning the same brush as the
    /// generator changes: counted in build order from whichever end it was nearer to, so the top step stays the top step
    /// when steps are added. Not valid: the generator's first brush with the surface, as before.
    /// </summary>
    [Serializable]
    public struct ChiselPartReference : IEquatable<ChiselPartReference>
    {
        public bool valid;
        /// <summary>The brush's place in build order, from the start or (fromEnd) from the end</summary>
        public int  index;
        public bool fromEnd;

        public readonly bool Equals(ChiselPartReference other) => valid == other.valid && index == other.index && fromEnd == other.fromEnd;
        public override readonly bool Equals(object obj) => obj is ChiselPartReference other && Equals(other);
        public override readonly int GetHashCode() => valid ? (index * 2 + (fromEnd ? 1 : 0)) : -1;
    }

    /// <summary>The brushes of a generator by their place in it, and back</summary>
    public static class ChiselGeneratorParts
    {
        /// <summary>A generator's brushes in build order (depth first)</summary>
        public static void BrushesOf(ChiselGeneratorComponent generator, List<CSGTreeBrush> brushes)
        {
            brushes.Clear();
            if (generator != null)
                Add(generator.TopTreeNode, brushes);
        }

        static void Add(CSGTreeNode node, List<CSGTreeBrush> brushes)
        {
            if (!node.Valid)
                return;
            if (node.Type == CSGNodeType.Brush)
            {
                brushes.Add((CSGTreeBrush)node);
                return;
            }
            for (int i = 0; i < node.Count; i++)
                Add(node[i], brushes);
        }

        /// <summary>The part a brush of a generator is, counted from the end it is nearer to; not valid when the brush isn't the generator's</summary>
        public static ChiselPartReference PartOf(ChiselGeneratorComponent generator, CSGTreeBrush brush)
        {
            var brushes = new List<CSGTreeBrush>();
            BrushesOf(generator, brushes);
            return PartOf(brushes, brushes.IndexOf(brush));
        }

        /// <summary>The part the brush at this place of so many is: from the start in the first half, from the end after</summary>
        public static ChiselPartReference PartOf(IReadOnlyList<CSGTreeBrush> brushes, int place)
        {
            if (brushes == null || place < 0 || place >= brushes.Count)
                return default;
            var fromEnd = brushes.Count - 1 - place;
            return fromEnd < place ? new ChiselPartReference { valid = true, index = fromEnd, fromEnd = true }
                                   : new ChiselPartReference { valid = true, index = place };
        }

        /// <summary>The place a part has among so many brushes, -1 when there aren't that many</summary>
        public static int PlaceOf(ChiselPartReference part, int brushCount)
        {
            if (!part.valid || part.index < 0 || part.index >= brushCount)
                return -1;
            return part.fromEnd ? brushCount - 1 - part.index : part.index;
        }

        /// <summary>The brush a part of a generator is now; false when it has no such brush</summary>
        public static bool TryResolve(ChiselGeneratorComponent generator, ChiselPartReference part, out CSGTreeBrush brush)
        {
            brush = default;
            if (!part.valid || generator == null)
                return false;
            var brushes = new List<CSGTreeBrush>();
            BrushesOf(generator, brushes);
            var place = PlaceOf(part, brushes.Count);
            if (place < 0)
                return false;
            brush = brushes[place];
            return true;
        }

        /// <summary>A surface of a generator's brush as a decal target: the generator, its surface, and which of its brushes</summary>
        public static ChiselDecalSurfaceTarget TargetOf(ChiselNodeComponent node, CSGTreeBrush brush, int surfaceIndex)
        {
            var target = new ChiselDecalSurfaceTarget { brush = node, surfaceIndex = surfaceIndex };
            if (node is ChiselGeneratorComponent generator)
                target.part = PartOf(generator, brush);
            return target;
        }
    }
}
