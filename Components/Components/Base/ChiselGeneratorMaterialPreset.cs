using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chisel.Components
{
    /// <summary>
    /// The materials of all of a generator's parts in one go ("wooden stairs"): a material per part, by the name the
    /// generator gives the part's surface. Made from a generator as it is; a generator of another kind takes the parts
    /// with the same name
    /// </summary>
    [CreateAssetMenu(fileName = "Material Preset", menuName = "Chisel/Generator Material Preset")]
    public sealed class ChiselGeneratorMaterialPreset : ScriptableObject
    {
        [Serializable]
        public struct Part
        {
            public string   name;
            public Material material;   // null: the part is left as it is
        }

        /// <summary>The kind of generator it was made from: its component's type</summary>
        public string     generator;
        public List<Part> parts = new();

        /// <summary>A generator's part as it is named: its surface's name, "Surface 3" when it has none</summary>
        public static string PartName(ChiselGeneratorComponent generator, int surface)
        {
            var name = generator != null ? generator.GetSurfaceName(surface) : null;
            return string.IsNullOrEmpty(name) ? "Surface " + (surface + 1) : name;
        }

        public static string KindOf(ChiselGeneratorComponent generator) { return generator != null ? generator.GetType().FullName : null; }

        /// <summary>Whether it was made from a generator of this one's kind</summary>
        public bool IsFor(ChiselGeneratorComponent generator) { return generator != null && KindOf(generator) == this.generator; }

        /// <summary>The material it sets on a part, null when it doesn't set it</summary>
        public Material MaterialFor(string partName)
        {
            foreach (var part in parts)
            {
                if (string.Equals(part.name, partName, StringComparison.Ordinal) && part.material != null)
                    return part.material;
            }
            return null;
        }

        /// <summary>Takes a generator's parts as they are now: each surface it uses, by name, with its material</summary>
        public void CopyFrom(ChiselGeneratorComponent source)
        {
            parts.Clear();
            generator = KindOf(source);
            var surfaces = source != null ? source.SurfaceDefinition?.surfaces : null;
            if (surfaces == null)
                return;
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (!source.IsSurfaceListed(i))
                    continue;
                var name = PartName(source, i);
                // two surfaces with one name are one part: the first one's material
                if (parts.Exists(part => part.name == name))
                    continue;
                parts.Add(new Part { name = name, material = surfaces[i]?.RenderMaterial });
            }
        }

        /// <summary>What it changes on a generator: each surface it uses whose part this sets to another material</summary>
        public List<KeyValuePair<int, Material>> ChangesFor(ChiselGeneratorComponent target)
        {
            var changes = new List<KeyValuePair<int, Material>>();
            var surfaces = target != null ? target.SurfaceDefinition?.surfaces : null;
            if (surfaces == null)
                return changes;
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i] == null || !target.IsSurfaceListed(i))
                    continue;
                var material = MaterialFor(PartName(target, i));
                if (material != null && material != surfaces[i].RenderMaterial)
                    changes.Add(new KeyValuePair<int, Material>(i, material));
            }
            return changes;
        }
    }
}
