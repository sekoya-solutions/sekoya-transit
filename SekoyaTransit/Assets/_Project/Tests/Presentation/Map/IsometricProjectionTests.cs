#nullable enable
using NUnit.Framework;
using SekoyaTransit.Presentation.Map;
using UnityEngine;

namespace SekoyaTransit.Presentation.Tests.Map
{
    public sealed class IsometricProjectionTests
    {
        [Test]
        public void WorldToTileContinuous_TileCenter_RoundTrips()
        {
            Vector3 world = IsometricProjection.TileToWorld(12f, 7f);
            IsometricProjection.WorldToTileContinuous(world.x, world.y, out float tileX, out float tileY);
            Assert.That(tileX, Is.EqualTo(12f).Within(0.0001f));
            Assert.That(tileY, Is.EqualTo(7f).Within(0.0001f));
        }

        [Test]
        public void TryWorldToTile_TileCenter_ReturnsSameTile()
        {
            Vector3 world = IsometricProjection.TileToWorld(3f, 5f);
            bool ok = IsometricProjection.TryWorldToTile(world.x, world.y, 128, out int tileX, out int tileY);
            Assert.That(ok, Is.True);
            Assert.That(tileX, Is.EqualTo(3));
            Assert.That(tileY, Is.EqualTo(5));
        }

        [Test]
        public void TryWorldToTile_OutsideMap_ReturnsFalse()
        {
            Vector3 world = IsometricProjection.TileToWorld(-2f, -2f);
            bool ok = IsometricProjection.TryWorldToTile(world.x, world.y, 128, out _, out _);
            Assert.That(ok, Is.False);
        }

        [Test]
        public void TryWorldToTile_NearCornerInsideMap_PicksCornerTile()
        {
            Vector3 world = IsometricProjection.TileToWorld(0f, 0f);
            bool ok = IsometricProjection.TryWorldToTile(world.x, world.y, 128, out int tileX, out int tileY);
            Assert.That(ok, Is.True);
            Assert.That(tileX, Is.EqualTo(0));
            Assert.That(tileY, Is.EqualTo(0));
        }

        [Test]
        public void TryWorldToTile_InvalidSideLength_Throws()
        {
            Assert.That(
                () => IsometricProjection.TryWorldToTile(0f, 0f, 0, out _, out _),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
