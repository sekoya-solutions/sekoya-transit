#nullable enable
using NUnit.Framework;
using TransportTycoon.Presentation.Map;

namespace TransportTycoon.Presentation.Tests.Map
{
    [TestFixture]
    public sealed class RailWagonTrailHistoryTests
    {
        [Test]
        public void SampleWagons_StraightTrail_PlacesAtSpacingBehindLoco()
        {
            var trail = new RailWagonTrailHistory(maxPoints: 64, minRecordDistance: 0.01f);
            // Oldest → newest along +X; loco tip at x=3.
            trail.SeedOldestFirst(
                new[] { 0f, 1f, 2f, 3f },
                new[] { 0f, 0f, 0f, 0f });

            var outX = new float[2];
            var outY = new float[2];
            trail.SampleWagons(locoX: 3f, locoY: 0f, wagonCount: 2, spacing: 1f, outX, outY);

            Assert.That(outX[0], Is.EqualTo(2f).Within(0.001f));
            Assert.That(outY[0], Is.EqualTo(0f).Within(0.001f));
            Assert.That(outX[1], Is.EqualTo(1f).Within(0.001f));
            Assert.That(outY[1], Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void SampleWagons_LShapedTrail_FollowsBendNotChordAcrossGap()
        {
            var trail = new RailWagonTrailHistory(maxPoints: 64, minRecordDistance: 0.01f);
            // L: (0,0)→(2,0)→(2,2). Loco tip at (2,2).
            trail.SeedOldestFirst(
                new[] { 0f, 1f, 2f, 2f, 2f },
                new[] { 0f, 0f, 0f, 1f, 2f });

            var outX = new float[2];
            var outY = new float[2];
            trail.SampleWagons(locoX: 2f, locoY: 2f, wagonCount: 2, spacing: 1f, outX, outY);

            // Wagon 0 at distance 1 behind tip → (2,1) on the vertical leg (not the (1,1) chord).
            Assert.That(outX[0], Is.EqualTo(2f).Within(0.001f));
            Assert.That(outY[0], Is.EqualTo(1f).Within(0.001f));
            // Wagon 1 at distance 2 → (2,0) at the corner (still on the path).
            Assert.That(outX[1], Is.EqualTo(2f).Within(0.001f));
            Assert.That(outY[1], Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void SampleWagons_ShortHistory_ClampsToOldestPoint()
        {
            var trail = new RailWagonTrailHistory(maxPoints: 16, minRecordDistance: 0.01f);
            trail.SeedOldestFirst(new[] { 5f, 6f }, new[] { 1f, 1f });

            var outX = new float[3];
            var outY = new float[3];
            trail.SampleWagons(locoX: 6f, locoY: 1f, wagonCount: 3, spacing: 1f, outX, outY);

            Assert.That(outX[0], Is.EqualTo(5f).Within(0.001f));
            Assert.That(outX[1], Is.EqualTo(5f).Within(0.001f));
            Assert.That(outX[2], Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void Record_IgnoresMicroMoves_ButUpdatesTip()
        {
            var trail = new RailWagonTrailHistory(maxPoints: 16, minRecordDistance: 0.5f);
            trail.Record(0f, 0f);
            trail.Record(0.1f, 0f);
            Assert.That(trail.Count, Is.EqualTo(1));

            var outX = new float[1];
            var outY = new float[1];
            trail.SampleWagons(0.1f, 0f, wagonCount: 1, spacing: 1f, outX, outY);
            // Only tip+one history point at ~origin; wagon clamps near oldest.
            Assert.That(outX[0], Is.EqualTo(0.1f).Within(0.001f));
        }

        [Test]
        public void Record_ThenSample_UsesAccumulatedPath()
        {
            var trail = new RailWagonTrailHistory(maxPoints: 64, minRecordDistance: 0.05f);
            for (int step = 0; step <= 10; step++)
            {
                trail.Record(step * 0.5f, 0f);
            }

            var outX = new float[2];
            var outY = new float[2];
            float locoX = 5f;
            trail.SampleWagons(locoX, 0f, wagonCount: 2, spacing: 1f, outX, outY);

            Assert.That(outX[0], Is.EqualTo(locoX - 1f).Within(0.05f));
            Assert.That(outX[1], Is.EqualTo(locoX - 2f).Within(0.05f));
            Assert.That(outY[0], Is.EqualTo(0f).Within(0.001f));
            Assert.That(outY[1], Is.EqualTo(0f).Within(0.001f));
        }
    }
}
