using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chisel.Editors.Tests
{
    // A material whose shader has no main texture is drawn in the browser on every repaint: asking it for one must not
    // log an error each time
    public sealed class ChiselSurfaceBrowserMainTextureTests
    {
        const string kNoMainTexture = "Packages/com.chisel/Package Resources/Materials/URP/Paint/Paint Color Test.mat";

        [Test]
        public void AMaterialWithoutAMainTexture_IsDrawnWithoutErrors()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(kNoMainTexture);
            Assume.That(material, Is.Not.Null, "Chisel's paint test material");
            Assume.That(material.HasTexture("_MainTex"), Is.False, "its shader has no main texture");

            Assert.That(Chisel.Core.ChiselTextureScale.MainTextureOf(material), Is.Null);
            ChiselSurfaceBrowserWindow.PreviewOf(material, flat: true);
            ChiselSurfaceBrowserDecals.ImageTextureOf(material);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
