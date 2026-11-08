using System;
using System.Collections.Generic;
using Chisel.Core;

namespace Chisel.Components
{
    /// <summary>
    /// Which brush of a generator something means (a decal's surface, say), so it keeps meaning the same brush as the
    /// generator changes: a role the generator gives its brushes ("Step", "Side"), and the brush's place among those of
    /// that role, counted from whichever end it was nearer to, so the top step stays the top step when steps are added.
    /// Not valid: the generator's first brush with the surface, as before.
    /// </summary>
    [Serializable]
    public struct ChiselPartReference : IEquatable<ChiselPartReference>
    {
        public bool valid;
        /// <summary>The generator's role for the brush (IChiselGeneratorParts); 0 for a generator that has none</summary>
        public int  role;
        /// <summary>The brush's place among the role's, from the start or (fromEnd) from the end</summary>
        public int  index;
        public bool fromEnd;
        /// <summary>Every brush of the role, not just this one</summary>
        public bool all;

        public readonly bool Equals(ChiselPartReference other) => valid == other.valid && role == other.role && index == other.index && fromEnd == other.fromEnd && all == other.all;
        public override readonly bool Equals(object obj) => obj is ChiselPartReference other && Equals(other);
        public override readonly int GetHashCode() => valid ? (((role * 1024 + index) * 2 + (fromEnd ? 1 : 0)) * 2 + (all ? 1 : 0)) : -1;
    }

    /// <summary>The brushes of a generator by the part they are, and back</summary>
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

        /// <summary>The role of each of a generator's brushes in build order; all 0 for a generator that has none</summary>
        public static int RoleOf(ChiselGeneratorComponent generator, int brushIndex)
        {
            var parts = generator != null ? generator.Parts : null;
            return parts != null ? parts.GetPartRole(brushIndex) : 0;
        }

        /// <summary>The name of a role ("Step"), "Brush" for a generator that has none</summary>
        public static string RoleName(ChiselGeneratorComponent generator, int role)
        {
            var parts = generator != null ? generator.Parts : null;
            return (parts != null ? parts.GetPartRoleName(role) : null) ?? "Brush";
        }

        /// <summary>The part a brush of a generator is: its role, and its place among them counted from the nearer end;
        /// not valid when the brush isn't the generator's</summary>
        public static ChiselPartReference PartOf(ChiselGeneratorComponent generator, CSGTreeBrush brush)
        {
            var brushes = new List<CSGTreeBrush>();
            BrushesOf(generator, brushes);
            var at = brushes.IndexOf(brush);
            if (at < 0)
                return default;
            var role = RoleOf(generator, at);
            int place = 0, count = 0;
            for (int i = 0; i < brushes.Count; i++)
            {
                if (RoleOf(generator, i) != role)
                    continue;
                if (i < at)
                    place++;
                count++;
            }
            var part = PartAt(place, count);
            part.role = role;
            return part;
        }

        /// <summary>The part at this place of so many: from the start in the first half, from the end after</summary>
        public static ChiselPartReference PartAt(int place, int count)
        {
            if (place < 0 || place >= count)
                return default;
            var fromEnd = count - 1 - place;
            return fromEnd < place ? new ChiselPartReference { valid = true, index = fromEnd, fromEnd = true }
                                   : new ChiselPartReference { valid = true, index = place };
        }

        /// <summary>The place a part has among so many of its role, -1 when there aren't that many</summary>
        public static int PlaceOf(ChiselPartReference part, int count)
        {
            if (!part.valid || part.index < 0 || part.index >= count)
                return -1;
            return part.fromEnd ? count - 1 - part.index : part.index;
        }

        /// <summary>The brush a part of a generator is now; false when it has no such brush</summary>
        public static bool TryResolve(ChiselGeneratorComponent generator, ChiselPartReference part, out CSGTreeBrush brush)
        {
            brush = default;
            if (!part.valid || generator == null)
                return false;
            var brushes = new List<CSGTreeBrush>();
            BrushesOf(generator, brushes);
            var ofRole = new List<CSGTreeBrush>();
            for (int i = 0; i < brushes.Count; i++)
            {
                if (RoleOf(generator, i) == part.role)
                    ofRole.Add(brushes[i]);
            }
            var place = PlaceOf(part, ofRole.Count);
            if (place < 0)
                return false;
            brush = ofRole[place];
            return true;
        }

        /// <summary>How many brushes of a generator have this role</summary>
        public static int CountOf(ChiselGeneratorComponent generator, int role)
        {
            var brushes = new List<CSGTreeBrush>();
            BrushesOf(generator, brushes);
            var count = 0;
            for (int i = 0; i < brushes.Count; i++)
            {
                if (RoleOf(generator, i) == role)
                    count++;
            }
            return count;
        }

        /// <summary>The part at this place among its role, counted from the start, kept to one brush</summary>
        public static ChiselPartReference AtPlace(ChiselPartReference part, int place)
        {
            return new ChiselPartReference { valid = part.valid, role = part.role, index = place };
        }

        /// <summary>A part as people say it: "Step 2 from the end", or "Every Step"</summary>
        public static string NameOf(ChiselGeneratorComponent generator, ChiselPartReference part)
        {
            if (!part.valid)
                return null;
            if (part.all)
                return $"Every {RoleName(generator, part.role)}";
            return $"{RoleName(generator, part.role)} {part.index + 1} from the {(part.fromEnd ? "end" : "start")}";
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
