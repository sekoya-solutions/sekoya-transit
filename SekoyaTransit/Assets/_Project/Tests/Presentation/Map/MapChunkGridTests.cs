#nullable enable
using NUnit.Framework;
using SekoyaTransit.Core.World;
using SekoyaTransit.Presentation.Map;

namespace SekoyaTransit.Presentation.Tests.Map
{
    public sealed class MapChunkGridTests
    {
        [Test]
        public void GetChunkCountPerSide_SupportedSizes_ReturnsExpectedCounts()
        {
            Assert.That(MapChunkGrid.GetChunkCountPerSide(MapSize.Small), Is.EqualTo(4));
            Assert.That(MapChunkGrid.GetChunkCountPerSide(MapSize.Medium), Is.EqualTo(8));
            Assert.That(MapChunkGrid.GetChunkCountPerSide(MapSize.Large), Is.EqualTo(16));
        }

        [Test]
        public void GetChunkCount_SmallMap_ReturnsSixteenChunks()
        {
            Assert.That(MapChunkGrid.GetChunkCount(MapSize.Small), Is.EqualTo(16));
        }

        [Test]
        public void GetChunkIndexForTile_Boundaries_MapToExpectedChunks()
        {
            Assert.That(MapChunkGrid.GetChunkIndexForTile(0, 0, MapSize.Small), Is.EqualTo(0));
            Assert.That(MapChunkGrid.GetChunkIndexForTile(31, 31, MapSize.Small), Is.EqualTo(0));
            Assert.That(MapChunkGrid.GetChunkIndexForTile(32, 0, MapSize.Small), Is.EqualTo(1));
            Assert.That(MapChunkGrid.GetChunkIndexForTile(0, 32, MapSize.Small), Is.EqualTo(4));
            Assert.That(MapChunkGrid.GetChunkIndexForTile(127, 127, MapSize.Small), Is.EqualTo(15));
        }

        [Test]
        public void GetChunkIndexForTile_OutOfRange_Throws()
        {
            Assert.That(
                () => MapChunkGrid.GetChunkIndexForTile(128, 0, MapSize.Small),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
