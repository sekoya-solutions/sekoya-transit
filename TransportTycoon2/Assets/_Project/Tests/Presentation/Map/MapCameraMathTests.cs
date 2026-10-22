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
        public void GetMapDiamond_SmallMap_CenterMatchesIsometricProjection()
        {
            MapCameraMath.GetMapDiamond(128, out float centerX, out float centerY, out _, out _);
            Vector3 expected = IsometricProjection.MapCenter(128);
            Assert.That(centerX, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(centerY, Is.EqualTo(expected.y).Within(0.0001f));
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
        public void ClampCameraCenter_ZoomedIn_SidePanOutsideDiamond_PullsBackOntoDiamond()
        {
            // Reproduces the AABB-only bug: AABB corner region is empty void beside a diamond side.
            MapCameraMath.GetMapDiamond(
                128,
                out float cx,
                out float cy,
                out float dhw,
                out float dhh);
            const float halfW = 4f;
            const float halfH = 3f;

            Vector2 outsideBesideSide = new Vector2(cx + (dhw * 0.85f), cy + (dhh * 0.85f));
            Assert.That(
                MapCameraMath.GetDiamondManhattan(outsideBesideSide, cx, cy, dhw, dhh),
                Is.GreaterThan(1f));

            Vector2 clamped = MapCameraMath.ClampCameraCenter(
                outsideBesideSide,
                cx,
                cy,
                dhw,
                dhh,
                halfW,
                halfH);

            float cornerInset = (halfW / dhw) + (halfH / dhh);
            float maxManhattan = 1f - cornerInset;
            Assert.That(
                MapCameraMath.GetDiamondManhattan(clamped, cx, cy, dhw, dhh),
                Is.EqualTo(maxManhattan).Within(0.0001f));
            Assert.That(
                MapCameraMath.AreViewportCornersInsideDiamond(clamped, cx, cy, dhw, dhh, halfW, halfH),
                Is.True);
        }

        [Test]
        public void ClampCameraCenter_ZoomedIn_ExtremePans_KeepViewportCornersInsideDiamond()
        {
            MapCameraMath.GetMapDiamond(128, out float cx, out float cy, out float dhw, out float dhh);
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
                new Vector2(9999f, cy),
                new Vector2(-9999f, cy),
                new Vector2(cx + dhw * 0.9f, cy + dhh * 0.9f),
                new Vector2(cx - dhw * 0.9f, cy + dhh * 0.9f),
            };

            foreach (Vector2 attempt in attempts)
            {
                Vector2 clamped = MapCameraMath.ClampCameraCenter(attempt, cx, cy, dhw, dhh, halfW, halfH);
                Assert.That(
                    MapCameraMath.AreViewportCornersInsideDiamond(clamped, cx, cy, dhw, dhh, halfW, halfH),
                    Is.True,
                    "Failed for attempt " + attempt);
            }
        }

        [Test]
        public void ClampCameraCenter_ZoomedOut_PinsToDiamondCenter()
        {
            MapCameraMath.GetMapDiamond(128, out float cx, out float cy, out float dhw, out float dhh);
            Vector2 clamped = MapCameraMath.ClampCameraCenter(
                new Vector2(500f, -500f),
                cx,
                cy,
                dhw,
                dhh,
                200f,
                200f);

            Assert.That(clamped.x, Is.EqualTo(cx).Within(0.0001f));
            Assert.That(clamped.y, Is.EqualTo(cy).Within(0.0001f));
        }

        [Test]
        public void ClampCameraCenter_InsideInsetDiamond_Unchanged()
        {
            MapCameraMath.GetMapDiamond(128, out float cx, out float cy, out float dhw, out float dhh);
            Vector2 center = new Vector2(cx + 2f, cy + 1f);
            Vector2 clamped = MapCameraMath.ClampCameraCenter(center, cx, cy, dhw, dhh, 4f, 3f);
            Assert.That(clamped, Is.EqualTo(center));
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
