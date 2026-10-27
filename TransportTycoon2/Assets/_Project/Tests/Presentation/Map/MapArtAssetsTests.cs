#nullable enable
using System.IO;
using NUnit.Framework;
using TransportTycoon.Presentation.Map;
using UnityEditor;
using UnityEngine;

namespace TransportTycoon.Presentation.Tests.Map
{
    /// <summary>
    /// Checks the baked map art of Issue #69 (ADR 0032): every sprite exists with its import settings, the art set
    /// resolves towns, industries, and vehicles, and the MapPlay scene uses it without pulling in the source models.
    /// </summary>
    public sealed class MapArtAssetsTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/MapPlay.unity";
        private const string ArtSetPath = "Assets/_Project/Art/Map/MapArtSet.asset";
        private const string BakedRoot = "Assets/_Project/Art/Map/Baked/";
        private const string SourceRoot = "Assets/_Project/Art/Map/Source/";

        private static readonly string[] SpriteFiles =
        {
            "building_road_station.png",
            "building_rail_station.png",
            "building_industry_generic.png",
            "building_house_0.png",
            "building_house_1.png",
            "building_house_2.png",
            "building_house_3.png",
            "building_industry_forest.png",
            "building_industry_sawmill.png",
            "building_industry_furniture_factory.png",
            "building_industry_iron_mine.png",
            "vehicle_truck_north.png",
            "vehicle_truck_east.png",
            "vehicle_truck_south.png",
            "vehicle_truck_west.png",
            "vehicle_locomotive_north.png",
            "vehicle_locomotive_east.png",
            "vehicle_locomotive_south.png",
            "vehicle_locomotive_west.png",
            "vehicle_wagon_north.png",
            "vehicle_wagon_east.png",
            "vehicle_wagon_south.png",
            "vehicle_wagon_west.png",
        };

        [Test]
        public void BakedSprites_EveryExpectedFile_IsATileWideSpriteWithItsPivot()
        {
            foreach (string file in SpriteFiles)
            {
                string path = BakedRoot + file;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer!.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(MapTileAtlas.CellWidth), path);
                Assert.That(importer.mipmapEnabled, Is.True, path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteAlignment, Is.EqualTo((int)SpriteAlignment.Custom), path);
                Assert.That(settings.spritePivot.x, Is.EqualTo(0.5f), path);
                Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(path), Is.Not.Null, path);
            }
        }

        [Test]
        public void TileAtlas_MaterialUsesTheBakedAtlasAtItsLayoutSize()
        {
            MapArtSet art = LoadArt();
            Material? material = art.TileAtlasMaterial;

            Assert.That(material, Is.Not.Null);
            Texture texture = material!.mainTexture;
            Assert.That(AssetDatabase.GetAssetPath(texture), Is.EqualTo(BakedRoot + "MapTileAtlas.png"));
            Assert.That(texture.width, Is.EqualTo(MapTileAtlas.TextureWidth));
            Assert.That(texture.height, Is.EqualTo(MapTileAtlas.TextureHeight));
        }

        [Test]
        public void ArtSet_StationsAndHouses_AreAssigned()
        {
            MapArtSet art = LoadArt();

            Assert.That(art.RoadStation, Is.Not.Null);
            Assert.That(art.RailStation, Is.Not.Null);
            Assert.That(art.RoadStation, Is.Not.EqualTo(art.RailStation));
            Assert.That(art.HouseCount, Is.EqualTo(4));
            for (int townId = 0; townId < 8; townId++)
            {
                Assert.That(art.GetHouse(townId), Is.Not.Null);
            }

            Assert.That(art.GetHouse(5), Is.EqualTo(art.GetHouse(1)));
        }

        [TestCase("forest")]
        [TestCase("sawmill")]
        [TestCase("furniture_factory")]
        [TestCase("iron_mine")]
        public void GetIndustry_GeneratedIndustry_HasItsOwnBuilding(string industryId)
        {
            MapArtSet art = LoadArt();
            Sprite? sprite = art.GetIndustry(industryId);

            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite, Is.Not.EqualTo(art.GetIndustry("no_such_industry")));
            Assert.That(AssetDatabase.GetAssetPath(sprite), Does.EndWith("building_industry_" + industryId + ".png"));
        }

        [Test]
        public void GetIndustry_IdWithoutArt_FallsBackToTheGenericBuilding()
        {
            MapArtSet art = LoadArt();

            Assert.That(
                AssetDatabase.GetAssetPath(art.GetIndustry("bakery")),
                Is.EqualTo(BakedRoot + "building_industry_generic.png"));
        }

        [Test]
        public void GetVehicle_EveryKindAndHeading_HasItsSprite()
        {
            MapArtSet art = LoadArt();
            foreach (MapVehicleArt vehicle in new[] { MapVehicleArt.Truck, MapVehicleArt.Locomotive, MapVehicleArt.Wagon })
            {
                for (int heading = 0; heading < MapHeadings.Count; heading++)
                {
                    Sprite? sprite = art.GetVehicle(vehicle, (MapHeading)heading);
                    Assert.That(sprite, Is.Not.Null, vehicle + " " + (MapHeading)heading);
                    Assert.That(
                        AssetDatabase.GetAssetPath(sprite),
                        Does.EndWith(((MapHeading)heading).ToString().ToLowerInvariant() + ".png"));
                }
            }
        }

        [Test]
        public void MapPlayScene_UsesTheArtSetAndTheClosestZoomLimit()
        {
            string[] dependencies = AssetDatabase.GetDependencies(ScenePath, recursive: false);

            Assert.That(dependencies, Does.Contain(ArtSetPath));
            Assert.That(File.ReadAllText(ScenePath), Does.Contain("_minOrthographicSize: 4"));
        }

        [Test]
        public void MapPlayScene_DoesNotReferenceTheSourceModels()
        {
            foreach (string dependency in AssetDatabase.GetDependencies(ScenePath, recursive: true))
            {
                Assert.That(dependency, Does.Not.StartWith(SourceRoot));
            }
        }

        private static MapArtSet LoadArt()
        {
            MapArtSet? art = AssetDatabase.LoadAssetAtPath<MapArtSet>(ArtSetPath);
            Assert.That(art, Is.Not.Null, ArtSetPath);
            return art!;
        }
    }
}
