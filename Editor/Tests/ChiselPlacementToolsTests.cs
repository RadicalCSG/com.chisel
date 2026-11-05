using System.Linq;

using Chisel.Components;
using Chisel.Core;

using NUnit.Framework;

using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Every generator can be drawn with the create tool
    public sealed class ChiselPlacementToolsTests
    {
        static System.Type ComponentOf(ChiselPlacementToolInstance tool)
        {
            var arguments = tool.GetType().GetGenericArguments();
            return arguments.Length == 3 ? arguments[2] : null;
        }

        [Test]
        public void EveryGenerator_HasAPlacementTool()
        {
            var drawn   = ChiselGeneratorManager.s_GeneratorModes.Select(ComponentOf).ToHashSet();
            var missing = TypeCache.GetTypesDerivedFrom<ChiselGeneratorComponent>()
                                   .Where(type => !type.IsAbstract && type.GetGenericBaseClass(typeof(ChiselNodeGeneratorComponent<>)) != null)
                                   .Where(type => type != typeof(ChiselBrushComponent))     // a brush is made from another generator
                                   .Where(type => !drawn.Contains(type))
                                   .Select(type => type.Name)
                                   .ToArray();
            Assert.That(missing, Is.Empty, "can't be drawn");
        }

        // without an icon a button shows a short name and the full name as its tooltip
        [Test]
        public void WithoutAnIcon_ButtonHasShortNameAndFullTooltip()
        {
            var tool = ChiselGeneratorManager.s_GeneratorModes.FirstOrDefault(mode => mode.ToolName == ChiselRevolvedShapeDefinition.kNodeTypeName);
            Assert.That(tool, Is.Not.Null);
            var content = tool.Content;
            if (content.image)
                Assert.Ignore("it has an icon");
            Assert.That(content.text,    Is.EqualTo("RS"));
            Assert.That(content.tooltip, Does.StartWith(ChiselRevolvedShapeDefinition.kNodeTypeName));
        }

        // its tooltip shows the key it's bound to now, whatever it was rebound to
        [Test]
        public void Tooltip_ShowsCurrentShortcut()
        {
            foreach (var tool in ChiselGeneratorManager.s_GeneratorModes)
            {
                var key = ChiselGeneratorShortcuts.KeyOf(tool.ToolName);
                Assert.That(key, Is.Not.Empty, $"{tool.ToolName} has no key");
                Assert.That(tool.Content.tooltip, Is.EqualTo($"{tool.ToolName} ({key})"));
            }
        }

        [Test]
        public void ShortName_IsInitialsOrFirstLetters()
        {
            Assert.That(ChiselPlacementToolInstance.ShortName("Pathed Stairs"), Is.EqualTo("PS"));
            Assert.That(ChiselPlacementToolInstance.ShortName("Stadium"),       Is.EqualTo("St"));
            Assert.That(ChiselPlacementToolInstance.ShortName("Box"),           Is.EqualTo("Bo"));
        }

        static T Tool<T>() where T : ScriptableObject { return ScriptableObject.CreateInstance<T>(); }

        [Test]
        public void Torus_FitsWhatIsDrawn()
        {
            var tool = Tool<ChiselTorusPlacementTool>();
            try
            {
                var definition = new ChiselTorusDefinition();
                definition.Reset();
                tool.OnUpdate(ref definition, new Bounds(new Vector3(0, 0.25f, 0), new Vector3(4, 0.5f, 4)));
                Assert.That(definition.settings.outerDiameter, Is.EqualTo(4));
                Assert.That(definition.settings.tubeHeight,    Is.EqualTo(0.5f));
                Assert.That(definition.settings.tubeWidth,     Is.EqualTo(0.5f));
                Assert.That(tool.CenteredOnY, Is.True, "built around its middle, it would sink halfway into what it's drawn on");
            }
            finally { Object.DestroyImmediate(tool); }
        }

        [Test]
        public void Stadium_FitsWhatIsDrawn()
        {
            var tool = Tool<ChiselStadiumPlacementTool>();
            try
            {
                var definition = new ChiselStadiumDefinition();
                definition.Reset();
                tool.OnUpdate(ref definition, new Bounds(new Vector3(0, 1, 0), new Vector3(2, 2, 6)));
                Assert.That(definition.settings.width,        Is.EqualTo(2));
                Assert.That(definition.settings.height,       Is.EqualTo(2));
                Assert.That(definition.settings.length,       Is.EqualTo(6));
                Assert.That(definition.settings.topLength,    Is.EqualTo(1), "its ends are half circles");
                Assert.That(definition.settings.bottomLength, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(tool); }
        }

        [Test]
        public void RevolvedShape_FitsWhatIsDrawn()
        {
            var shape = ChiselRevolvedShapePlacementTool.FitShape(ChiselRevolvedShapeDefinition.DefaultShape,
                                                                  new Bounds(new Vector3(0, 1.5f, 0), new Vector3(4, 3, 4)));
            var points = shape.controlPoints.Select(point => point.position).ToArray();
            Assert.That(points.Min(p => p.x), Is.EqualTo(-2));
            Assert.That(points.Max(p => p.x), Is.EqualTo(2));
            Assert.That(points.Min(p => p.y), Is.EqualTo(0));
            Assert.That(points.Max(p => p.y), Is.EqualTo(3));
            Assert.That(ChiselRevolvedShapeDefinition.DefaultShape.controlPoints[0].position, Is.EqualTo(new Vector2(-1, -1)), "the default was changed");
        }

        [Test]
        public void PathedStairs_AreAsHighAsDrawn()
        {
            var tool = Tool<ChiselPathedStairsPlacementTool>();
            try
            {
                var definition = new ChiselPathedStairsDefinition();
                definition.Reset();
                var drawn = new Curve2D(new CurveControlPoint2D(0, 0), new CurveControlPoint2D(4, 0), new CurveControlPoint2D(4, 4));
                tool.OnCreate(ref definition, drawn);
                tool.OnUpdate(ref definition, 3);
                Assert.That(definition.shape.controlPoints.Length, Is.EqualTo(3));
                Assert.That(definition.settings.stairs.BoundsMin.y, Is.EqualTo(0));
                Assert.That(definition.settings.stairs.BoundsMax.y, Is.EqualTo(3));
            }
            finally { Object.DestroyImmediate(tool); }
        }
    }
}
