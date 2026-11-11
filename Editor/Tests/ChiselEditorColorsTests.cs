using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // The editor windows' colors drawn on the window stand out from it in both of Unity's skins
    [TestFixture]
    public sealed class ChiselEditorColorsTests
    {
        static float Luminance(Color color)
        {
            var linear = color.linear;
            return 0.2126f * linear.r + 0.7152f * linear.g + 0.0722f * linear.b;
        }

        // The contrast ratio between two colors, from 1 (the same) to 21 (black on white)
        static float Contrast(Color a, Color b)
        {
            var la = Luminance(a);
            var lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        // 3:1, the least for marks and large text to be told from what is around them
        const float kLeastContrast = 3f;

        [TestCase(true), TestCase(false)]
        public void MarksAndTextOnTheWindow_StandOutFromIt(bool proSkin)
        {
            var window = ChiselEditorColors.PictureBackgroundFor(proSkin);
            Assert.That(Contrast(ChiselEditorColors.AccentFor(proSkin),      window), Is.GreaterThanOrEqualTo(kLeastContrast), "accent");
            Assert.That(Contrast(ChiselEditorColors.HighlightFor(proSkin),   window), Is.GreaterThanOrEqualTo(kLeastContrast), "highlight");
            Assert.That(Contrast(ChiselEditorColors.WarmTextFor(proSkin),    window), Is.GreaterThanOrEqualTo(kLeastContrast), "warm text");
            Assert.That(Contrast(ChiselEditorColors.MeasurementFor(proSkin), window), Is.GreaterThanOrEqualTo(kLeastContrast), "measurement");
        }

        // The skin's labels are light in the dark skin and dark in the light one: a chosen row keeps them readable
        [TestCase(true), TestCase(false)]
        public void TextOnAChosenRow_IsReadable(bool proSkin)
        {
            var text = proSkin ? Color.white : Color.black;
            Assert.That(Contrast(ChiselEditorColors.ChosenRowFor(proSkin), text), Is.GreaterThanOrEqualTo(4.5f));
        }

        // Over the window, a hovered row and a grip darken the light skin's window and lighten the dark one's
        [TestCase(true), TestCase(false)]
        public void HoverAndGrip_ShowOnTheWindow(bool proSkin)
        {
            var window = ChiselEditorColors.PictureBackgroundFor(proSkin);
            foreach (var over in new[] { ChiselEditorColors.RowHoverFor(proSkin), ChiselEditorColors.GripFor(proSkin) })
            {
                var blended = Color.Lerp(window, new Color(over.r, over.g, over.b, 1), over.a);
                Assert.That(Luminance(blended), proSkin ? Is.GreaterThan(Luminance(window)) : Is.LessThan(Luminance(window)));
            }
        }
    }
}
