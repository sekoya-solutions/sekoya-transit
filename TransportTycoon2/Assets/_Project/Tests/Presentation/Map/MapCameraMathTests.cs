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
        public void ClampCameraCenter_ZoomedInFarPan_KeepsViewportFullyInsideMap()
        {
            MapCameraMath.GetMapWorldBounds(128, out float mapMinX, out float mapMaxX, out float mapMinY, out float mapMaxY);
            const float halfW = 8f;
            const float halfH = 5f;

            Vector2 clamped = MapCameraMath.ClampCameraCenter(
                new Vector2(10000f, -10000f),
                mapMinX,
                mapMaxX,
                mapMinY,
                mapMaxY,
                halfW,
                halfH);

            float overlap = MapCameraMath.GetViewportMapOverlapFraction(
                clamped,
                mapMinX,
                mapMaxX,
                mapMinY,
                mapMaxY,
                halfW,
                halfH);

            Assert.That(clamped.x, Is.EqualTo(mapMaxX - halfW).Within(0.0001f));
            Assert.That(clamped.y, Is.EqualTo(mapMinY + halfH).Within(0.0001f));
            Assert.That(overlap, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ClampCameraCenter_ZoomedIn_ExtremeCorners_AlwaysFullOverlap()
        {
            MapCameraMath.GetMapWorldBounds(128, out float mapMinX, out float mapMaxX, out float mapMinY, out float mapMaxY);
            const float halfW = 4f;
            const float halfH = 3f;
            Vector2[] attempts =
            {
                new Vector2(9999f, 9999f),
                new Vector2(-9999f, 9999f),
                new Vector2(9999f, -9999f),
                new Vector2(-9999f, -9999f),
                new Vector2(0f, 9999f),
                new Vector2(0f, -9999f),
            };

            foreach (Vector2 attempt in attempts)
            {
                Vector2 clamped = MapCameraMath.ClampCameraCenter(
                    attempt,
                    mapMinX,
                    mapMaxX,
                    mapMinY,
                    mapMaxY,
                    halfW,
                    halfH);
                float overlap = MapCameraMath.GetViewportMapOverlapFraction(
                    clamped,
                    mapMinX,
                    mapMaxX,
                    mapMinY,
                    mapMaxY,
                    halfW,
                    halfH);
                Assert.That(overlap, Is.EqualTo(1f).Within(0.0001f), "Failed for attempt " + attempt);
            }
        }

        [Test]
        public void ClampCameraCenter_ZoomedOut_PinsToMapCenter()
        {
            MapCameraMath.GetMapWorldBounds(128, out float mapMinX, out float mapMaxX, out float mapMinY, out float mapMaxY);
            float midX = (mapMinX + mapMaxX) * 0.5f;
            float midY = (mapMinY + mapMaxY) * 0.5f;
            const float halfW = 200f;
            const float halfH = 200f;

            Vector2 clamped = MapCameraMath.ClampCameraCenter(
                new Vector2(500f, -500f),
                mapMinX,
                mapMaxX,
                mapMinY,
                mapMaxY,
                halfW,
                halfH);

            Assert.That(clamped.x, Is.EqualTo(midX).Within(0.0001f));
            Assert.That(clamped.y, Is.EqualTo(midY).Within(0.0001f));
        }

        [Test]
        public void ClampCameraCenter_InsideAllowedRange_Unchanged()
        {
            Vector2 center = new Vector2(0f, 20f);
            Vector2 clamped = MapCameraMath.ClampCameraCenter(
                center,
                -64f,
                64f,
                -0.25f,
                63.75f,
                8f,
                5f);

            Assert.That(clamped, Is.EqualTo(center));
        }

        [Test]
        public void GetViewportMapOverlapFraction_NoIntersection_ReturnsZero()
        {
            float overlap = MapCameraMath.GetViewportMapOverlapFraction(
                new Vector2(1000f, 1000f),
                -10f,
                10f,
                0f,
                10f,
                5f,
                5f);
            Assert.That(overlap, Is.EqualTo(0f));
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
