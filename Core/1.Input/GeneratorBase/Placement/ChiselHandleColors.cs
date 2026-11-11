using UnityEngine;

namespace Chisel.Core
{
    // Colors a generator's handles are drawn with, made from the color the handles are given
    public static class ChiselHandleColors
    {
        /// <summary>A line behind something else, drawn dashed: darker, as see through as the color</summary>
        public static Color Hidden(Color color)
        {
            var hidden = Color.Lerp(color, Color.black, 0.45f);
            hidden.a = color.a;
            return hidden;
        }
    }
}
