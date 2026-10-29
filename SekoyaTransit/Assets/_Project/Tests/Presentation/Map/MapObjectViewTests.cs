#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.World;
using SekoyaTransit.Presentation.Map;
using UnityEditor;
using UnityEngine;

namespace SekoyaTransit.Presentation.Tests.Map
{
    public sealed class MapObjectViewTests
    {
        private const string ArtSetPath = "Assets/_Project/Art/Map/MapArtSet.asset";

        [Test]
        public void Constructor_GeneratedWorld_DrawsOneSpritePerTownAndIndustry()
        {
            GeneratedWorld world = CreateWorld();
            using var view = new MapObjectView(world, null, LoadArt());

            Assert.That(view.SpriteCount, Is.EqualTo(world.TownAnchors.Count + world.Industries.Count));
        }

        [Test]
        public void RefreshTile_StationBuiltThenRemoved_AddsThenRemovesItsBuilding()
        {
            GeneratedWorld world = CreateWorld();
            MapArtSet art = LoadArt();
            using var view = new MapObjectView(world, null, art);
            int before = view.SpriteCount;
            TileCoordinate free = FindEmptyTile(world.Map);

            world.Map.SetRailStationId(free, 1);
            view.RefreshTile(free);
            Assert.That(view.SpriteCount, Is.EqualTo(before + 1));

            world.Map.SetRailStationId(free, TileMap.NoRailStationId);
            view.RefreshTile(free);
            Assert.That(view.SpriteCount, Is.EqualTo(before));
        }

        [Test]
        public void Constructor_RejectsMissingArt()
        {
            Assert.That(
                () => new MapObjectView(CreateWorld(), null, null!),
                Throws.TypeOf<ArgumentNullException>());
        }

        private static TileCoordinate FindEmptyTile(TileMap map)
        {
            for (int tileId = 0; tileId < map.TileCount; tileId++)
            {
                TileCoordinate coordinate = map.GetCoordinate(tileId);
                if (map.GetObjectId(coordinate) == TileMap.NoObjectId)
                {
                    return coordinate;
                }
            }

            throw new InvalidOperationException("No empty tile.");
        }

        private static MapArtSet LoadArt()
        {
            MapArtSet? art = AssetDatabase.LoadAssetAtPath<MapArtSet>(ArtSetPath);
            Assert.That(art, Is.Not.Null, ArtSetPath);
            return art!;
        }

        private static GeneratedWorld CreateWorld()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 4, 24),
                4,
                new[]
                {
                    new IndustryPlacementRule("forest", 1, Array.Empty<string>(), new[] { "wood" }, 2, 0),
                    new IndustryPlacementRule("sawmill", 2, new[] { "wood" }, new[] { "planks" }, 1, 60),
                });
            return WorldGenerator.Generate(settings, 69001UL);
        }
    }
}
