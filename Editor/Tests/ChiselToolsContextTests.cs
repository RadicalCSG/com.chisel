using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    [TestFixture]
    public class ChiselToolsContextTests
    {
        // The scene view's tools overlay shows the context's tools: that is where the surface tools are picked
        [Test]
        public void SurfaceTools_AreInTheSceneViewsTools()
        {
            var context = ScriptableObject.CreateInstance<ChiselToolsContext>();
            try
            {
                var tools = context.GetAdditionalToolTypes().ToList();
                Assert.That(tools, Does.Contain(typeof(ChiselPaintTool)));
                Assert.That(tools, Does.Contain(typeof(ChiselSurfaceShapeTool)));
            }
            finally
            {
                Object.DestroyImmediate(context);
            }
        }

        [Test]
        public void SurfaceShapeTool_HasAnIcon()
        {
            var tool = ScriptableObject.CreateInstance<ChiselSurfaceShapeTool>();
            try
            {
                Assert.That(tool.toolbarIcon.image, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(tool);
            }
        }
    }
}
