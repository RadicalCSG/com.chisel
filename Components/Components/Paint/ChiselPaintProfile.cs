using System;
using System.Collections.Generic;
using Chisel.Core;
using UnityEngine;

namespace Chisel.Components
{
    /// <summary>What a paint mode writes: a color the brush paints with, or a value per component</summary>
    public enum ChiselPaintKind : byte
    {
        /// <summary>A color, written to the mode's components (rgba, or rgb)</summary>
        Color = 0,
        /// <summary>A number (a height, wetness, a layer's weight): painted up or down with strength and falloff</summary>
        Value = 1
    }

    /// <summary>One thing a shader takes from paint: what it is called, and where it goes in the vertices</summary>
    [Serializable]
    public struct ChiselPaintMode
    {
        public string                   name;
        public ChiselPaintKind          kind;
        public ChiselPaintAttribute     attribute;
        public ChiselPaintComponents    components;
        /// <summary>What an unpainted surface has, in all four components</summary>
        public Vector4                  defaultValue;
        /// <summary>A value mode's lowest and highest value</summary>
        public Vector2                  range;
        /// <summary>Modes of one group (above 0), in components of one attribute, are weights that sum to one: painting
        /// one of them takes from the others (blending material layers)</summary>
        public int                      group;

        public static ChiselPaintMode Color(string name, ChiselPaintAttribute attribute = ChiselPaintAttribute.Color,
                                            ChiselPaintComponents components = ChiselPaintComponents.XYZW)
        {
            return new ChiselPaintMode { name = name, kind = ChiselPaintKind.Color, attribute = attribute, components = components,
                                         defaultValue = Vector4.one, range = new Vector2(0, 1) };
        }

        public static ChiselPaintMode Value(string name, ChiselPaintAttribute attribute, ChiselPaintComponents components,
                                            float defaultValue = 0, float min = 0, float max = 1)
        {
            return new ChiselPaintMode { name = name, kind = ChiselPaintKind.Value, attribute = attribute, components = components,
                                         defaultValue = new Vector4(defaultValue, defaultValue, defaultValue, defaultValue),
                                         range = new Vector2(min, max) };
        }
    }

    /// <summary>
    /// What paint means to a shader: the modes a surface drawn with it can be painted in. The shader shows the paint;
    /// Chisel only writes it to the vertex attributes the modes name. Two modes don't write one component of one attribute.
    /// </summary>
    [CreateAssetMenu(menuName = "Chisel/Paint Profile", fileName = "Paint Profile")]
    public sealed class ChiselPaintProfile : ScriptableObject
    {
        public Shader shader;
        public List<ChiselPaintMode> modes = new();

        /// <summary>The components of the other modes in a mode's group (in its attribute); None when it has no group</summary>
        public ChiselPaintComponents GroupOf(int mode)
        {
            var result = ChiselPaintComponents.None;
            if (mode < 0 || mode >= modes.Count || modes[mode].group <= 0)
                return result;
            for (int i = 0; i < modes.Count; i++)
            {
                if (i != mode && modes[i].group == modes[mode].group && modes[i].attribute == modes[mode].attribute)
                    result |= modes[i].components;
            }
            return result & ~modes[mode].components;
        }

        /// <summary>The first two modes that write one component of one attribute; false when there are none</summary>
        public bool FindOverlap(out int first, out int second)
        {
            for (first = 0; first < modes.Count; first++)
            {
                for (second = first + 1; second < modes.Count; second++)
                {
                    if (modes[first].attribute == modes[second].attribute &&
                        (modes[first].components & modes[second].components) != 0)
                        return true;
                }
            }
            first = second = -1;
            return false;
        }
    }
}
