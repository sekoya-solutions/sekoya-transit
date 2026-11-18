#nullable enable
using NUnit.Framework;
using SekoyaTransit.Presentation.Editor.Capture;

namespace SekoyaTransit.Presentation.Tests.Capture
{
    [TestFixture]
    public sealed class CameraPathTests
    {
        private static readonly CameraKey[] _keys =
        {
            new CameraKey(0f, 10f, 20f, 40f),
            new CameraKey(4f, 30f, 0f, 10f),
            new CameraKey(6f, 30f, 0f, 10f),
        };

        [Test]
        public void Evaluate_AtAndAroundTheEnds_HoldsTheFirstAndLastView()
        {
            CameraKey before = CameraPath.Evaluate(_keys, -1f);
            CameraKey after = CameraPath.Evaluate(_keys, 99f);

            Assert.That(before.TileX, Is.EqualTo(10f));
            Assert.That(before.Zoom, Is.EqualTo(40f));
            Assert.That(after.TileX, Is.EqualTo(30f));
            Assert.That(after.Zoom, Is.EqualTo(10f));
        }

        [Test]
        public void Evaluate_AtAKeyframe_ReturnsThatKeyframe()
        {
            CameraKey view = CameraPath.Evaluate(_keys, 4f);

            Assert.That(view.TileX, Is.EqualTo(30f).Within(1e-5f));
            Assert.That(view.TileY, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(view.Zoom, Is.EqualTo(10f).Within(1e-4f));
        }

        [Test]
        public void Evaluate_Halfway_IsHalfwayInPositionAndInZoomScale()
        {
            CameraKey view = CameraPath.Evaluate(_keys, 2f);

            Assert.That(view.TileX, Is.EqualTo(20f).Within(1e-4f));
            Assert.That(view.TileY, Is.EqualTo(10f).Within(1e-4f));
            // Halfway in scale between 40 and 10 is 20 (geometric mean), not 25.
            Assert.That(view.Zoom, Is.EqualTo(20f).Within(1e-3f));
        }

        [Test]
        public void Evaluate_NearTheStartOfAMove_MovesSlowerThanLinear()
        {
            CameraKey early = CameraPath.Evaluate(_keys, 0.4f);

            float linear = 10f + (20f * 0.1f);
            Assert.That(early.TileX, Is.GreaterThan(10f).And.LessThan(linear));
        }

        [Test]
        public void Evaluate_OneKeyframe_AlwaysReturnsIt()
        {
            var single = new[] { new CameraKey(0f, 5f, 6f, 7f) };

            Assert.That(CameraPath.Evaluate(single, 3f).TileX, Is.EqualTo(5f));
        }
    }
}
