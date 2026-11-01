#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.World
{
    [TestFixture]
    public sealed class SiteFootprintTests
    {
        private static readonly string[] _emptyCargoIds = Array.Empty<string>();
        private static readonly string[] _woodCargoIds = { "wood" };

        [Test]
        public void Fits_BlockNearMapEdge_RequiresWholeBlockAndOneFreeTileInsideMap()
        {
            Assert.That(SiteFootprint.Fits(new TileCoordinate(1, 1), 128, SiteFootprint.IndustrySize), Is.True);
            Assert.That(SiteFootprint.Fits(new TileCoordinate(125, 125), 128, SiteFootprint.IndustrySize), Is.True);
            Assert.That(SiteFootprint.Fits(new TileCoordinate(0, 5), 128, SiteFootprint.IndustrySize), Is.False);
            Assert.That(SiteFootprint.Fits(new TileCoordinate(5, 0), 128, SiteFootprint.IndustrySize), Is.False);
            Assert.That(SiteFootprint.Fits(new TileCoordinate(126, 5), 128, SiteFootprint.IndustrySize), Is.False);
            Assert.That(SiteFootprint.Fits(new TileCoordinate(5, 126), 128, SiteFootprint.IndustrySize), Is.False);
        }

        [Test]
        public void Fits_TownBlockNearMapEdge_NeedsThreeTilesAndAFreeBorder()
        {
            Assert.That(SiteFootprint.Fits(new TileCoordinate(124, 124), 128, SiteFootprint.TownSize), Is.True);
            Assert.That(SiteFootprint.Fits(new TileCoordinate(125, 124), 128, SiteFootprint.TownSize), Is.False);
            Assert.That(SiteFootprint.Fits(new TileCoordinate(124, 125), 128, SiteFootprint.TownSize), Is.False);
            Assert.That(SiteFootprint.Fits(new TileCoordinate(0, 5), 128, SiteFootprint.TownSize), Is.False);
        }

        [Test]
        public void Contains_TileInsideAndOutsideTownBlock_MatchesBlockTilesOnly()
        {
            var anchor = new TileCoordinate(10, 10);

            for (int dy = 0; dy < SiteFootprint.TownSize; dy++)
            {
                for (int dx = 0; dx < SiteFootprint.TownSize; dx++)
                {
                    Assert.That(
                        SiteFootprint.Contains(anchor, new TileCoordinate(10 + dx, 10 + dy), SiteFootprint.TownSize),
                        Is.True);
                }
            }

            Assert.That(SiteFootprint.Contains(anchor, new TileCoordinate(9, 10), SiteFootprint.TownSize), Is.False);
            Assert.That(SiteFootprint.Contains(anchor, new TileCoordinate(10, 9), SiteFootprint.TownSize), Is.False);
            Assert.That(SiteFootprint.Contains(anchor, new TileCoordinate(13, 10), SiteFootprint.TownSize), Is.False);
            Assert.That(SiteFootprint.Contains(anchor, new TileCoordinate(10, 13), SiteFootprint.TownSize), Is.False);
        }

        [Test]
        public void Stamp_BlockWithTransport_KeepsExistingRoadTile()
        {
            var map = new TileMap(MapSize.Small);
            var road = new TileCoordinate(11, 10);
            map.SetRoadTypeId(road, TileMap.BasicRoadTypeId);

            SiteFootprint.Stamp(map, new TileCoordinate(10, 10), 5, SiteFootprint.TownSize);

            Assert.That(map.GetObjectId(new TileCoordinate(10, 10)), Is.EqualTo(5));
            Assert.That(map.GetObjectId(new TileCoordinate(10, 11)), Is.EqualTo(5));
            Assert.That(map.GetObjectId(new TileCoordinate(11, 11)), Is.EqualTo(5));
            Assert.That(map.GetObjectId(new TileCoordinate(12, 12)), Is.EqualTo(5));
            Assert.That(map.GetObjectId(road), Is.EqualTo(TileMap.NoObjectId));
        }

        [Test]
        public void Generate_EverySiteTile_RejectsRoadAndStation()
        {
            GeneratedWorld world = CreateWorldWithForest();
            PlacedIndustry forest = world.Industries[0];
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);

            for (int dy = 0; dy < SiteFootprint.IndustrySize; dy++)
            {
                for (int dx = 0; dx < SiteFootprint.IndustrySize; dx++)
                {
                    var tile = new TileCoordinate(forest.Coordinate.X + dx, forest.Coordinate.Y + dy);
                    Assert.That(processor.Process(new Tick(1), new BuildRoadCommand(tile)).IsAccepted, Is.False);
                    Assert.That(processor.Process(new Tick(1), new BuildRoadStationCommand(tile)).IsAccepted, Is.False);
                }
            }
        }

        [Test]
        public void TryGetAdjacentIndustry_StationBesideNonAnchorTile_ResolvesIndustry()
        {
            GeneratedWorld world = CreateWorldWithForest();
            PlacedIndustry forest = world.Industries[0];
            var beside = new TileCoordinate(
                forest.Coordinate.X + SiteFootprint.IndustrySize,
                forest.Coordinate.Y + SiteFootprint.IndustrySize - 1);

            bool found = StationAdjacency.TryGetAdjacentIndustry(world, beside, out PlacedIndustry? industry);

            Assert.That(found, Is.True);
            Assert.That(industry, Is.SameAs(forest));
        }

        [Test]
        public void TryGetAdjacentTown_StationBesideNonAnchorTile_ResolvesTown()
        {
            GeneratedWorld world = CreateWorldWithForest();
            TileCoordinate anchor = world.TownAnchors[0];
            var beside = new TileCoordinate(anchor.X - 1, anchor.Y + SiteFootprint.TownSize - 1);
            if (!world.Map.Contains(beside))
            {
                beside = new TileCoordinate(anchor.X + SiteFootprint.TownSize, anchor.Y + SiteFootprint.TownSize - 1);
            }

            bool found = StationAdjacency.TryGetAdjacentTown(world, beside, out Town? town);

            Assert.That(found, Is.True);
            Assert.That(town, Is.SameAs(world.Towns[0]));
        }

        private static GeneratedWorld CreateWorldWithForest()
        {
            var rules = new[]
            {
                new IndustryPlacementRule("forest", 1, _emptyCargoIds, _woodCargoIds, 1, 0)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 8),
                StationAdjacency.MinimumSiteSeparationChebyshev,
                rules);
            return WorldGenerator.Generate(settings, 48101UL);
        }
    }
}
