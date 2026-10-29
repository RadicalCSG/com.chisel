using Chisel.Components;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public class ChiselPaintToolSettingsTests
    {
        // the user's own settings, put back after each test
        string[] keptStrings;
        float[]  keptFloats;
        int[]    keptInts;
        bool[]   hadKey;

        string               modeName;
        ChiselPaintOperation operation;
        Color                color;
        float                value, radius, strength, hardness;

        [SetUp]
        public void SetUp()
        {
            var keys = ChiselPaintTool.kSettingsPrefs;
            keptStrings = new string[keys.Length];
            keptFloats  = new float[keys.Length];
            keptInts    = new int[keys.Length];
            hadKey      = new bool[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                hadKey[i]      = EditorPrefs.HasKey(keys[i]);
                keptStrings[i] = EditorPrefs.GetString(keys[i]);
                keptFloats[i]  = EditorPrefs.GetFloat(keys[i]);
                keptInts[i]    = EditorPrefs.GetInt(keys[i]);
            }
            modeName  = ChiselPaintTool.s_ModeName;
            operation = ChiselPaintTool.s_Operation;
            color     = ChiselPaintTool.s_Color;
            value     = ChiselPaintTool.s_Value;
            radius    = ChiselPaintTool.s_Radius;
            strength  = ChiselPaintTool.s_Strength;
            hardness  = ChiselPaintTool.s_Hardness;
        }

        [TearDown]
        public void TearDown()
        {
            var keys = ChiselPaintTool.kSettingsPrefs;
            for (int i = 0; i < keys.Length; i++)
            {
                if (!hadKey[i])
                    EditorPrefs.DeleteKey(keys[i]);
                else if (i == 0)
                    EditorPrefs.SetString(keys[i], keptStrings[i]);
                else if (i == 1)
                    EditorPrefs.SetInt(keys[i], keptInts[i]);
                else
                    EditorPrefs.SetFloat(keys[i], keptFloats[i]);
            }
            ChiselPaintTool.s_ModeName  = modeName;
            ChiselPaintTool.s_Operation = operation;
            ChiselPaintTool.s_Color     = color;
            ChiselPaintTool.s_Value     = value;
            ChiselPaintTool.s_Radius    = radius;
            ChiselPaintTool.s_Strength  = strength;
            ChiselPaintTool.s_Hardness  = hardness;
        }

        // A domain reload puts the statics back; the tool reads its settings again after one
        [Test]
        public void TheBrushSettings_AreTheSameAfterAReload()
        {
            var expectedColor = new Color(0.123f, 0.456f, 0.789f, 0.5f);
            ChiselPaintTool.s_ModeName  = "Wetness";
            ChiselPaintTool.s_Operation = ChiselPaintOperation.Smooth;
            ChiselPaintTool.s_Color     = expectedColor;
            ChiselPaintTool.s_Value     = 0.25f;
            ChiselPaintTool.s_Radius    = 1.75f;
            ChiselPaintTool.s_Strength  = 0.3f;
            ChiselPaintTool.s_Hardness  = 0.9f;
            ChiselPaintTool.SaveSettings();

            ChiselPaintTool.ResetSettings();
            Assert.That(ChiselPaintTool.s_Color, Is.Not.EqualTo(expectedColor), "the reset didn't put the color back");
            ChiselPaintTool.LoadSettings();

            Assert.That(ChiselPaintTool.s_Color,     Is.EqualTo(expectedColor));
            Assert.That(ChiselPaintTool.s_ModeName,  Is.EqualTo("Wetness"));
            Assert.That(ChiselPaintTool.s_Operation, Is.EqualTo(ChiselPaintOperation.Smooth));
            Assert.That(ChiselPaintTool.s_Value,     Is.EqualTo(0.25f));
            Assert.That(ChiselPaintTool.s_Radius,    Is.EqualTo(1.75f));
            Assert.That(ChiselPaintTool.s_Strength,  Is.EqualTo(0.3f));
            Assert.That(ChiselPaintTool.s_Hardness,  Is.EqualTo(0.9f));
        }

        // Nothing saved yet: the tool starts at its defaults
        [Test]
        public void WithNothingSaved_TheBrushStartsAtItsDefaults()
        {
            foreach (var key in ChiselPaintTool.kSettingsPrefs)
                EditorPrefs.DeleteKey(key);
            ChiselPaintTool.ResetSettings();
            ChiselPaintTool.LoadSettings();

            Assert.That(ChiselPaintTool.s_Color,     Is.EqualTo(Color.red));
            Assert.That(string.IsNullOrEmpty(ChiselPaintTool.s_ModeName), Is.True);
            Assert.That(ChiselPaintTool.s_Operation, Is.EqualTo(ChiselPaintOperation.Paint));
            Assert.That(ChiselPaintTool.s_Radius,    Is.EqualTo(0.5f));
        }
    }
}
