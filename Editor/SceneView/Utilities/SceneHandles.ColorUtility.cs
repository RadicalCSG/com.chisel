using System.Globalization;
using UnityEditor;
using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    public static class ColorUtility
    {
        public static Color GetPreferenceColor(string name, Color defaultColor)
        {
            string prevname = string.Empty;
            Color resultColor = defaultColor;
            if (GetPreferenceColor(name, ref prevname, defaultColor, ref resultColor))
                return resultColor;
            return defaultColor;
        }

        public static bool GetPreferenceColor(string name, ref string prevname, Color defaultColor, ref Color resultColor)
        {
            var prefString	= EditorPrefs.GetString(name);
            if (prevname == prefString)
                return false;

            prevname = prefString;
            if (TryParsePreferenceColor(prefString, out var newColor))
            {
                if (newColor == resultColor)
                    return false;
                resultColor = newColor;
                return true;
            }

            resultColor = defaultColor;
            return false;
        }

        /// <summary>A color as Unity keeps it in EditorPrefs for its Colors preferences: "name;r;g;b;a"</summary>
        public static string FormatPreferenceColor(string name, Color color)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0};{1:R};{2:R};{3:R};{4:R}", name, color.r, color.g, color.b, color.a);
        }

        /// <summary>Reads a color written as Unity keeps it in EditorPrefs ("name;r;g;b;a"), also with decimal commas</summary>
        public static bool TryParsePreferenceColor(string prefString, out Color color)
        {
            color = default;
            var split = prefString?.Split(';');
            if (split == null || split.Length != 5)
                return false;
            for (int i = 0; i < 4; i++)
            {
                if (!float.TryParse(split[i + 1].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out var channel))
                    return false;
                color[i] = channel;
            }
            return true;
        }

        public static Color CopyHueSaturation(Color src1, Color src2)
        {
            float h1, s1, v1;
            Color.RGBToHSV(src1, out h1, out s1, out v1);
            
            float h2, s2, v2;
            Color.RGBToHSV(src2, out h2, out s2, out v2);

            var dstColor = Color.HSVToRGB(h2, (s1 + s2) * 0.5f, v2);
            dstColor.a = src1.a;// (src1.a + src2.a) * 0.5f;

            return dstColor;
        }
        
        public static Color CopyValue(Color src1, Color src2)
        {
            float h1, s1, v1;
            Color.RGBToHSV(src1, out h1, out s1, out v1);
            
            float h2, s2, v2;
            Color.RGBToHSV(src2, out h2, out s2, out v2);

            var dstColor = Color.HSVToRGB(h2, s2, v1);
            dstColor.a = src2.a;// (src1.a + src2.a) * 0.5f;

            return dstColor;
        }
        
        public static Color InvertHue(Color src)
        {
            float h1, s1, v1;
            Color.RGBToHSV(src, out h1, out s1, out v1);
            
            var dstColor = Color.HSVToRGB(1-h1, s1, v1);
            dstColor.a = src.a;

            return dstColor;
        }

        public static Color Brighten(Color color, float brightScale)
        {
            float h, s, v;
            Color.RGBToHSV(color, out h, out s, out v);
            
            v *= brightScale;

            var outColor = Color.HSVToRGB(h, s, v);
            outColor.a = color.a;
            return outColor;
        }

    }
}
