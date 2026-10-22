#nullable enable
using NUnit.Framework;
using TransportTycoon.Presentation.Map;
using UnityEngine;

namespace TransportTycoon.Presentation.Tests.Map
{
    public sealed class MapCameraMathTests
    {
        [Test]
        public void GetMapWorldBounds_SmallMap_MatchesTileDiamondExtents()
        {
            MapCameraMath.GetMapWorldBounds(128, out float minX, out float maxX, out float minY, out float maxY);

            Assert.That(minX, Is.EqualTo(-63.5f - MapCameraMath.TileHalfWidth).Within(0.0001f));
            Assert.That(maxX, Is.EqualTo(63.5f + MapCameraMath.TileHalfWidth).Within(0.0001f));
            Assert.That(minY, Is.EqualTo(-MapCameraMath.TileHalfHeight).Within(0.0001f));
            Assert.That(maxY, Is.EqualTo(63.5f + MapCameraMath.TileHalfHeight).Within(0.0001f));
        }

        [Test]
        public void ClampZoom_OutsideRange_ClampsToMinAndMax()
        {
            Assert.That(MapCameraMath.ClampZoom(1f, 2f, 140f), Is.EqualTo(2f));
            Assert.That(MapCameraMath.ClampZoom(200f, 2f, 140f), Is.EqualTo(140f));
            Assert.That(MapCameraMath.ClampZoom(30f, 2f, 140f), Is.EqualTo(30f));
        }

        [Test]
        public void ApplyScrollZoom_PositiveScroll_ZoomsIn()
        {
            float next = MapCameraMath.ApplyScrollZoom(20f, 120f, 0.85f, 2f, 140f);
            Assert.That(next, Is.EqualTo(17f).Within(0.0001f));
        }

        [Test]
        public void ApplyScrollZoom_NegativeScroll_ZoomsOut()
        {
            float next = MapCameraMath.ApplyScrollZoom(20f, -120f, 0.85f, 2f, 140f);
            Assert.That(next, Is.EqualTo(20f / 0.85f).Within(0.0001f));
        }

        [Test]
        public void ClampCameraCenter_FarOutside_HalfViewportKeepsCenterInsideMap()
        {
            const float mapMinX = -10f;
            const float mapMaxX = 10f;
            const float mapMinY = 0f;
            const float mapMaxY = 10f;
            const float halfW = 5f;
            const float halfH = 5f;

            Vector2 clamped = MapCameraMath.ClampCameraCenter(
                new Vector2(1000f, 1000f),
                mapMinX,
                mapMaxX,
                mapMinY,
                mapMaxY,
                halfW,
                halfH,
                MapCameraMath.DefaultMinVisibleViewportFraction);

            // With fraction 0.5 and viewport smaller than map, center clamps to map AABB.
            Assert.That(clamped.x, Is.EqualTo(mapMaxX).Within(0.0001f));
            Assert.That(clamped.y, Is.EqualTo(mapMaxY).Within(0.0001f));
        }

        [Test]
        public void ClampCameraCenter_InsideMap_Unchanged()
        {
            Vector2 center = new Vector2(0f, 5f);
            Vector2 clamped = MapCameraMath.ClampCameraCenter(
                center,
                -10f,
                10f,
                0f,
                10f,
                5f,
                5f,
                MapCameraMath.DefaultMinVisibleViewportFraction);

            Assert.That(clamped, Is.EqualTo(center));
        }

        [Test]
        public void ClampCameraCenter_ZoomedOutPastMap_KeepsCenterInsideMap()
        {
            // Viewport 40×40, map 10×10: half-viewport overlap exceeds map size → center stays in map.
            Vector2 clamped = MapCameraMath.ClampCameraCenter(
                new Vector2(100f, 100f),
                -5f,
                5f,
                0f,
                10f,
                20f,
                20f,
                MapCameraMath.DefaultMinVisibleViewportFraction);

            Assert.That(clamped.x, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(clamped.y, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(clamped.x, Is.InRange(-5f, 5f));
            Assert.That(clamped.y, Is.InRange(0f, 10f));
        }

        [Test]
        public void KeyboardPanDelta_ScalesWithZoom()
        {
            Vector2 slow = MapCameraMath.KeyboardPanDelta(Vector2.right, 10f, 1.5f, 1f);
            Vector2 fast = MapCameraMath.KeyboardPanDelta(Vector2.right, 20f, 1.5f, 1f);
            Assert.That(fast.x, Is.EqualTo(slow.x * 2f).Within(0.0001f));
        }

        [Test]
        public void MouseDragPanDelta_RightDragMovesOpposite()
        {
            Vector2 delta = MapCameraMath.MouseDragPanDelta(new Vector2(100f, 0f), 10f, 200f);
            Assert.That(delta.x, Is.LessThan(0f));
            Assert.That(delta.y, Is.EqualTo(0f));
        }
    }
}
