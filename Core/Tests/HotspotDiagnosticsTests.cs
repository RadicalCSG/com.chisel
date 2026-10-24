using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class HotspotDiagnosticsTests
    {
        // a 2048 texture at 256 pixels a unit: a unit is an eighth of the texture
        static readonly float2 kTexture = new float2(2048, 2048);
        const float kPixelsPerUnit = 256;

        // The uv of a point of a plane through the origin with these axes, at the tile's density
        static float2 UV(float3 p, float3 right, float3 up, float2 scale) => new float2(math.dot(p, right), math.dot(p, up)) * scale / 8;

        static void Measure(float3 a, float3 b, float3 c, float3 right, float3 up, float2 scale, out double s1, out double s2, out bool mirrored)
        {
            Assert.That(HotspotDiagnostics.Measure(a, b, c, UV(a, right, up, scale), UV(b, right, up, scale), UV(c, right, up, scale),
                                                   kTexture, kPixelsPerUnit, out s1, out s2, out mirrored), Is.True);
        }

        [Test]
        public void ATriangleAtItsTilesDensity_IsUndistorted_OnAnyPlane()
        {
            var right = math.normalize(new float3(1, -1, 0));
            var up    = math.normalize(new float3(1, 1, -2));
            var a = right * 0.3f + up * 0.1f; var b = right * 1.7f + up * 0.4f; var c = right * 0.9f + up * 1.3f;
            Measure(a, b, c, right, up, new float2(1), out var s1, out var s2, out _);
            Assert.That(s1, Is.EqualTo(1).Within(1e-5));
            Assert.That(s2, Is.EqualTo(1).Within(1e-5));
            Assert.That(HotspotDiagnostics.Energy(s1, s2), Is.EqualTo(1).Within(1e-5));
        }

        [Test]
        public void ATriangleStretchedAcross_HasHalfTheDensityThatWay()
        {
            var right = new float3(1, 0, 0); var up = new float3(0, 1, 0);
            Measure(new float3(0, 0, 0), new float3(1.3f, 0, 0), new float3(0.2f, 0.7f, 0), right, up, new float2(0.5f, 1), out var s1, out var s2, out _);
            Assert.That(s1, Is.EqualTo(1).Within(1e-5));
            Assert.That(s2, Is.EqualTo(0.5).Within(1e-5));
            // (1 + 1 + 1/4 + 4) / 4
            Assert.That(HotspotDiagnostics.Energy(s1, s2), Is.EqualTo(1.5625).Within(1e-5));
        }

        [Test]
        public void AMirroredTexture_IsMirrored_AndAsDistortedAsUnmirrored()
        {
            var right = new float3(1, 0, 0); var up = new float3(0, 1, 0);
            var a = new float3(0, 0, 0); var b = new float3(1.3f, 0, 0); var c = new float3(0.2f, 0.7f, 0);
            Measure(a, b, c, right, up, new float2(1, 2), out var s1, out var s2, out var mirrored);
            Measure(a, b, c, right, up, new float2(-1, 2), out var m1, out var m2, out var mirroredToo);
            Assert.That(mirroredToo, Is.Not.EqualTo(mirrored));
            Assert.That(m1, Is.EqualTo(s1).Within(1e-6));
            Assert.That(m2, Is.EqualTo(s2).Within(1e-6));
        }

        [Test]
        public void ATriangleWithoutArea_IsNotMeasured()
        {
            Assert.That(HotspotDiagnostics.Measure(new float3(0, 0, 0), new float3(1, 0, 0), new float3(2, 0, 0), float2.zero, new float2(1, 0), new float2(0, 1),
                                                   kTexture, kPixelsPerUnit, out _, out _, out _), Is.False);
        }

        [Test]
        public void TheRecord_SaysWhatMatchingChose()
        {
            var layout = ScriptableObject.CreateInstance<ChiselHotspotLayout>();
            layout.size = new Vector2Int(2048, 2048);
            layout.pixelsPerUnit = 256;
            layout.tiles = new List<HotspotTile> { new() { id = 7, name = "B1", border = 32, pixelsPerUnit = 128,
                points = new[] { new Vector2Int(1024, 0), new Vector2Int(2048, 0), new Vector2Int(2048, 512), new Vector2Int(1024, 512) } } };
            using var tiles = new NativeList<HotspotTileData>(Allocator.Temp);
            HotspotMatcher.AddTiles(layout, tiles, out var data);
            try
            {
                foreach (var (width, height, pinned, lastResort, allBorder) in new[] { (8f, 4f, false, false, false), (8f, 0.3f, true, false, true), (400f, 12f, false, true, false) })
                {
                    using var positions = new NativeArray<float3>(new[] { new float3(0, 0, 0), new float3(width, 0, 0), new float3(width, height, 0), new float3(0, height, 0) }, Allocator.Temp);
                    var shape  = HotspotMatcher.ShapeOf(positions, new float3(0, 0, -1));
                    var choice = HotspotMatcher.Choose(tiles.AsArray(), data, shape, 0, pinned ? 7 : 0);
                    var input  = HotspotSections.Input(tiles[choice.tile], data, shape, choice, new float3(0, 0, -1));
                    var info   = HotspotDiagnostics.Info(tiles[choice.tile], data, choice, input, pinned);
                    var what   = $"{width} x {height}";
                    Assert.That(info.IsMapped, Is.True, what);
                    Assert.That(info.tileId, Is.EqualTo(7), what);
                    Assert.That(info.pixelsPerUnit, Is.EqualTo(128), what);
                    Assert.That(info.textureSize, Is.EqualTo(kTexture), what);
                    Assert.That((info.flags & HotspotSurfaceFlags.Pinned) != 0, Is.EqualTo(pinned), what + ": pinned");
                    Assert.That((info.flags & HotspotSurfaceFlags.LastResort) != 0, Is.EqualTo(lastResort), what + ": last resort");
                    Assert.That((info.flags & HotspotSurfaceFlags.Repeated) != 0, Is.EqualTo(lastResort), what + ": repeated");
                    Assert.That((info.flags & HotspotSurfaceFlags.AllBorder) != 0, Is.EqualTo(allBorder), what + ": all border");
                }
                Assert.That(default(HotspotSurfaceInfo).IsMapped, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }
    }
}
