using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    /// <summary>An attribute a material's shader takes paint in, and what it has where nothing is painted</summary>
    public struct ChiselPaintMaterialSlot
    {
        public ulong                 material;
        public ChiselPaintAttribute  attribute;
        public ChiselPaintComponents components;
        public float4                defaultValue;
    }

    /// <summary>
    /// The paint attributes of the materials surfaces are drawn with: a render mesh with such a material has those
    /// attributes, at their defaults where nothing is painted. Materials are registered as surfaces take them; what
    /// their shaders take comes from <see cref="Resolver"/>.
    /// </summary>
    public static class ChiselPaintMaterials
    {
        /// <summary>Adds the attributes a material's shader takes paint in, with their defaults</summary>
        public static Action<Material, List<ChiselPaintMaterialSlot>> Resolver;

        static readonly Dictionary<ulong, Material>       s_Materials = new();
        static readonly List<ChiselPaintMaterialSlot>     s_Slots     = new();
        static readonly List<ChiselPaintMaterialSlot>     s_Resolved  = new();
        static readonly List<ChiselPaintMaterialSlot>     s_Scratch   = new();
        static readonly List<ulong>                       s_Gone      = new();
        static bool s_Dirty;
        static int  s_Version;
        static bool s_Changed;

        public static void Register(Material material)
        {
            if (!material)
                return;
            if (s_Materials.TryAdd(EntityId.ToULong(material.GetEntityId()), material))
                s_Dirty = true;
        }

        /// <summary>What shaders take may have changed; looked into at the next <see cref="Update"/></summary>
        public static void Changed() => s_Changed = true;

        /// <summary>Before the trees are built: every brush is built again when a registered material's attributes changed</summary>
        public static void Update()
        {
            if (!s_Changed)
                return;
            s_Changed = false;
            s_Dirty = true;
            var before = s_Slots.Count;
            s_Scratch.Clear();
            s_Scratch.AddRange(s_Slots);
            Resolve();
            var same = before == s_Slots.Count;
            for (int i = 0; same && i < before; i++)
            {
                same = s_Scratch[i].material == s_Slots[i].material && s_Scratch[i].attribute == s_Slots[i].attribute &&
                       s_Scratch[i].components == s_Slots[i].components && s_Scratch[i].defaultValue.Equals(s_Slots[i].defaultValue);
            }
            if (same)
                return;
            using var trees = new NativeList<CSGTree>(Allocator.Temp);
            CompactHierarchyManager.GetAllTrees(trees);
            foreach (var tree in trees)
            {
                for (int i = 0; tree.Valid && i < tree.Count; i++)
                    SetBrushesDirty(tree[i]);
            }
        }

        static void SetBrushesDirty(CSGTreeNode node)
        {
            if (!node.Valid)
                return;
            if (node.Type == CSGNodeType.Brush)
            {
                node.SetSurfacesDirty();
                return;
            }
            for (int i = 0; i < node.Count; i++)
                SetBrushesDirty(node[i]);
        }

        // the slots of every registered material, its slots together
        static void Resolve()
        {
            if (!s_Dirty)
                return;
            s_Dirty = false;
            s_Version++;
            s_Slots.Clear();
            s_Gone.Clear();
            foreach (var entry in s_Materials)
            {
                if (!entry.Value)
                {
                    s_Gone.Add(entry.Key);
                    continue;
                }
                if (Resolver == null)
                    continue;
                s_Resolved.Clear();
                Resolver(entry.Value, s_Resolved);
                foreach (var slot in s_Resolved)
                    s_Slots.Add(new ChiselPaintMaterialSlot { material = entry.Key, attribute = slot.attribute, components = slot.components, defaultValue = slot.defaultValue });
            }
            foreach (var key in s_Gone)
                s_Materials.Remove(key);
        }

        // The slots as they are now, and a number that changes when they do
        internal static int Refresh()
        {
            Resolve();
            return s_Version;
        }

        internal static void Gather(NativeList<ChiselPaintMaterialSlot> slots)
        {
            Resolve();
            foreach (var slot in s_Slots)
                slots.Add(slot);
        }
    }
}
