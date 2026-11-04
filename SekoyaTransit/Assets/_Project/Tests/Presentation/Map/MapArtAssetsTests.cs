#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SekoyaTransit.Presentation.Map;
using UnityEditor;
using UnityEngine;

namespace SekoyaTransit.Presentation.Tests.Map
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

        private static readonly string[] Headings = { "north", "east", "south", "west" };

        private static readonly string[] VehicleKinds = { "truck", "locomotive", "wagon" };

        private const string VehiclesJsonPath = "Assets/_Project/Content/Data/vehicles.json";

        private static readonly string[] SpriteFiles =
        {
            "building_road_station.png",
            "building_rail_station.png",
            "building_industry_generic.png",
            "building_town_mk1.png",
            "building_town_mk2.png",
            "building_town_mk3.png",
            "building_town_mk4.png",
            "building_town_mk5.png",
            "building_industry_forest.png",
            "building_industry_sawmill.png",
            "building_industry_furniture_factory.png",
            "building_industry_iron_mine.png",
            "building_industry_wheat_farm.png",
            "building_industry_flour_mill.png",
            "building_industry_bakery.png",
            "building_industry_iron_foundry.png",
            "building_industry_machine_shop.png",
            "building_industry_livestock_ranch.png",
            "building_industry_butcher.png",
            "building_industry_cannery.png",
            "building_industry_sheep_farm.png",
            "building_industry_textile_mill.png",
            "building_industry_clothing_workshop.png",
        };

        // Original Grok Imagine sprites are fitted at import (scale and pivot), not baked at 128 pixels per unit.
        private static readonly string[] OriginalSiteFiles =
        {
            "building_industry_forest.png",
            "building_industry_sawmill.png",
            "building_industry_furniture_factory.png",
            "building_industry_iron_mine.png",
            "building_industry_wheat_farm.png",
            "building_industry_flour_mill.png",
            "building_industry_bakery.png",
            "building_industry_iron_foundry.png",
            "building_industry_machine_shop.png",
            "building_industry_livestock_ranch.png",
            "building_industry_butcher.png",
            "building_industry_cannery.png",
            "building_industry_sheep_farm.png",
            "building_industry_textile_mill.png",
            "building_industry_clothing_workshop.png",
        };

        [Test]
        public void BakedSprites_EveryExpectedFile_IsATileWideSpriteWithItsPivot()
        {
            foreach (string file in AllSpriteFiles())
            {
                string path = BakedRoot + file;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer!.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
                bool isOriginal = System.Array.IndexOf(OriginalSiteFiles, file) >= 0;
                if (!isOriginal)
                {
                    Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(MapTileAtlas.CellWidth), path);
                }

                Assert.That(importer.mipmapEnabled, Is.True, path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteAlignment, Is.EqualTo((int)SpriteAlignment.Custom), path);
                if (!isOriginal)
                {
                    Assert.That(settings.spritePivot.x, Is.EqualTo(0.5f), path);
                }

                Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(path), Is.Not.Null, path);
            }
        }

        // A site sprite's base must lie on its block (Issues #248 and #250): below the block center, no pixel of the sprite
        // may fall under the diamond's front edges (the diamond is sizeTiles world units wide and half as tall).
        [TestCase("building_industry_forest.png", 2)]
        [TestCase("building_industry_sawmill.png", 2)]
        [TestCase("building_industry_furniture_factory.png", 2)]
        [TestCase("building_industry_iron_mine.png", 2)]
        [TestCase("building_industry_wheat_farm.png", 2)]
        [TestCase("building_industry_flour_mill.png", 2)]
        [TestCase("building_industry_bakery.png", 2)]
        [TestCase("building_industry_iron_foundry.png", 2)]
        [TestCase("building_industry_machine_shop.png", 2)]
        [TestCase("building_industry_livestock_ranch.png", 2)]
        [TestCase("building_industry_butcher.png", 2)]
        [TestCase("building_industry_cannery.png", 2)]
        [TestCase("building_industry_sheep_farm.png", 2)]
        [TestCase("building_industry_textile_mill.png", 2)]
        [TestCase("building_industry_clothing_workshop.png", 2)]
        public void OriginalSiteSprites_Base_StaysInsideTheirBlockDiamond(string file, int sizeTiles)
        {
            string path = BakedRoot + file;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
                Color32[] pixels = texture.GetPixels32();
                float unit = importer.spritePixelsPerUnit;
                float pivotX = settings.spritePivot.x * texture.width;
                float pivotY = settings.spritePivot.y * texture.height;
                float tolerance = 1f / unit;
                float lowest = float.MaxValue;
                for (int x = 0; x < texture.width; x++)
                {
                    for (int y = 0; y < texture.height; y++)
                    {
                        if (pixels[(y * texture.width) + x].a < 40)
                        {
                            continue;
                        }

                        float worldX = (x - pivotX) / unit;
                        float worldY = (y - pivotY) / unit;
                        float frontEdge = (-sizeTiles / 4f) + (0.5f * Mathf.Abs(worldX));
                        Assert.That(worldY, Is.GreaterThanOrEqualTo(frontEdge - tolerance), path + " column " + x);
                        lowest = Mathf.Min(lowest, worldY - frontEdge);
                        break;
                    }
                }

                // The base touches the front edges instead of floating far above them.
                Assert.That(lowest, Is.LessThan(0.1f), path);
            }
            finally
            {
                Object.DestroyImmediate(texture);
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
        public void GroundAtlas_ArtSetUsesTheBakedAtlasAtItsLayoutSize()
        {
            Texture2D? texture = LoadArt().GroundAtlas;

            Assert.That(texture, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(texture), Is.EqualTo(BakedRoot + "MapGroundAtlas.png"));
            Assert.That(texture!.width, Is.EqualTo(MapGroundAtlas.TextureWidth));
            Assert.That(texture.height, Is.EqualTo(MapGroundAtlas.TextureHeight));
            Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(texture.mipmapCount, Is.GreaterThan(1));
        }

        [Test]
        public void ArtSet_StationsAndTownTiers_AreAssigned()
        {
            MapArtSet art = LoadArt();

            Assert.That(art.RoadStation, Is.Not.Null);
            Assert.That(art.RailStation, Is.Not.Null);
            Assert.That(art.RoadStation, Is.Not.EqualTo(art.RailStation));
            Assert.That(art.TownTierCount, Is.EqualTo(5));
            var seen = new System.Collections.Generic.HashSet<Sprite>();
            for (int tier = 1; tier <= 5; tier++)
            {
                Sprite? sprite = art.GetTownTier(tier);
                Assert.That(sprite, Is.Not.Null);
                Assert.That(seen.Add(sprite!), Is.True, "tier " + tier + " must have its own sprite");
            }

            Assert.That(art.GetTownTier(0), Is.EqualTo(art.GetTownTier(1)));
            Assert.That(art.GetTownTier(9), Is.EqualTo(art.GetTownTier(5)));
        }

        [TestCase(0, 1)]
        [TestCase(999, 1)]
        [TestCase(1000, 2)]
        [TestCase(1999, 2)]
        [TestCase(2000, 3)]
        [TestCase(5000, 4)]
        [TestCase(9999, 4)]
        [TestCase(10000, 5)]
        public void TownTierForPopulation_Population_PicksTierFromMinimums(int population, int expected)
        {
            Assert.That(MapArtSet.TownTierForPopulation(population, new[] { 0, 1000, 2000, 5000, 10000 }), Is.EqualTo(expected));
        }

        [Test]
        public void TownTierForPopulation_NoMinimums_IsFirstTier()
        {
            Assert.That(MapArtSet.TownTierForPopulation(50000, null), Is.EqualTo(1));
        }

        [TestCase("forest")]
        [TestCase("sawmill")]
        [TestCase("furniture_factory")]
        [TestCase("iron_mine")]
        [TestCase("wheat_farm")]
        [TestCase("flour_mill")]
        [TestCase("bakery")]
        [TestCase("iron_foundry")]
        [TestCase("machine_shop")]
        [TestCase("livestock_ranch")]
        [TestCase("butcher")]
        [TestCase("cannery")]
        [TestCase("sheep_farm")]
        [TestCase("textile_mill")]
        [TestCase("clothing_workshop")]
        public void GetIndustry_GeneratedIndustry_HasItsOwnBuilding(string industryId)
        {
            MapArtSet art = LoadArt();
            Sprite? sprite = art.GetIndustry(industryId);

            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite, Is.Not.EqualTo(art.GetIndustry("no_such_industry")));
            Assert.That(AssetDatabase.GetAssetPath(sprite), Does.EndWith("building_industry_" + industryId + ".png"));
        }

        [Test]
        public void CashIcon_IsTheBakedDollarIconOfTheUi()
        {
            MapArtSet art = LoadArt();

            Assert.That(
                AssetDatabase.GetAssetPath(art.CashIcon),
                Is.EqualTo(BakedRoot + "icon_cash.png"));
        }

        [Test]
        public void GetIndustry_IdWithoutArt_FallsBackToTheGenericBuilding()
        {
            MapArtSet art = LoadArt();

            Assert.That(
                AssetDatabase.GetAssetPath(art.GetIndustry("no_such_industry")),
                Is.EqualTo(BakedRoot + "building_industry_generic.png"));
        }

        [Test]
        public void GetVehicle_EveryKindTierAndHeading_HasItsTierSprite()
        {
            MapArtSet art = LoadArt();
            foreach (MapVehicleArt vehicle in new[] { MapVehicleArt.Truck, MapVehicleArt.Locomotive, MapVehicleArt.Wagon })
            {
                string kind = vehicle == MapVehicleArt.Truck ? "truck" : vehicle == MapVehicleArt.Wagon ? "wagon" : "locomotive";
                for (int tier = 1; tier <= 5; tier++)
                {
                    string typeId = (vehicle == MapVehicleArt.Truck ? "truck_mk" : "loco_mk") + tier;
                    for (int heading = 0; heading < MapHeadings.Count; heading++)
                    {
                        Sprite? sprite = art.GetVehicle(vehicle, typeId, (MapHeading)heading);
                        Assert.That(sprite, Is.Not.Null, typeId + " " + (MapHeading)heading);
                        Assert.That(
                            AssetDatabase.GetAssetPath(sprite),
                            Is.EqualTo(
                                BakedRoot + "vehicle_" + kind + "_mk" + tier + "_"
                                + ((MapHeading)heading).ToString().ToLowerInvariant() + ".png"));
                    }
                }
            }
        }

        [Test]
        public void GetVehicle_UnknownOrMissingTypeId_FallsBackToTheFirstTier()
        {
            MapArtSet art = LoadArt();

            Assert.That(
                art.GetVehicle(MapVehicleArt.Truck, "no_such_type", MapHeading.East),
                Is.SameAs(art.GetVehicle(MapVehicleArt.Truck, "truck_mk1", MapHeading.East)));
            Assert.That(
                art.GetVehicle(MapVehicleArt.Locomotive, null, MapHeading.North),
                Is.SameAs(art.GetVehicle(MapVehicleArt.Locomotive, "loco_mk1", MapHeading.North)));
        }

        [Test]
        public void GetVehicle_EveryTruckAndLocomotiveOfTheContentData_HasItsOwnArt()
        {
            MapArtSet art = LoadArt();
            MatchCollection matches = Regex.Matches(
                File.ReadAllText(VehiclesJsonPath),
                @"""id"":\s*""((?:truck|loco)_mk\d+)""");

            Assert.That(matches.Count, Is.GreaterThanOrEqualTo(10));
            foreach (Match match in matches)
            {
                string typeId = match.Groups[1].Value;
                MapVehicleArt vehicle = typeId.StartsWith("truck", System.StringComparison.Ordinal)
                    ? MapVehicleArt.Truck
                    : MapVehicleArt.Locomotive;
                for (int heading = 0; heading < MapHeadings.Count; heading++)
                {
                    Sprite? sprite = art.GetVehicle(vehicle, typeId, (MapHeading)heading);
                    Assert.That(sprite, Is.Not.Null, typeId + " " + (MapHeading)heading);
                    string kind = vehicle == MapVehicleArt.Truck ? "truck" : "locomotive";
                    Assert.That(
                        AssetDatabase.GetAssetPath(sprite),
                        Does.Contain("vehicle_" + kind + "_mk" + typeId.Substring(typeId.Length - 1) + "_"),
                        typeId);
                }
            }
        }

        // On the 2:1 map an east-west vehicle lies along "/" and a north-south one along "\" (Issue #69: trains were
        // baked a quarter turn off). The sign of the opaque pixels' x-y covariance gives the long axis.
        [TestCase("truck_mk1")]
        [TestCase("truck_mk2")]
        [TestCase("truck_mk3")]
        [TestCase("truck_mk4")]
        [TestCase("truck_mk5")]
        [TestCase("locomotive_mk1")]
        [TestCase("locomotive_mk2")]
        [TestCase("locomotive_mk3")]
        [TestCase("locomotive_mk4")]
        [TestCase("locomotive_mk5")]
        [TestCase("wagon_mk1")]
        [TestCase("wagon_mk2")]
        [TestCase("wagon_mk3")]
        [TestCase("wagon_mk4")]
        [TestCase("wagon_mk5")]
        public void VehicleSprites_EveryHeading_LieAlongTheirTrack(string vehicle)
        {
            foreach (string heading in Headings)
            {
                string path = BakedRoot + "vehicle_" + vehicle + "_" + heading + ".png";
                float covariance = OpaquePixelCovariance(path);
                bool alongXAxis = heading == "east" || heading == "west";

                Assert.That(covariance > 0f, Is.EqualTo(alongXAxis), path + " covariance " + covariance);
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

        // Covariance of the x and y coordinates (y up) of the pixels that are mostly opaque.
        private static float OpaquePixelCovariance(string path)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
                Color32[] pixels = texture.GetPixels32();
                double sumX = 0;
                double sumY = 0;
                double sumXY = 0;
                int count = 0;
                for (int index = 0; index < pixels.Length; index++)
                {
                    if (pixels[index].a < 128)
                    {
                        continue;
                    }

                    int x = index % texture.width;
                    int y = index / texture.width;
                    sumX += x;
                    sumY += y;
                    sumXY += (double)x * y;
                    count++;
                }

                Assert.That(count, Is.GreaterThan(0), path);
                return (float)((sumXY / count) - ((sumX / count) * (sumY / count)));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static MapArtSet LoadArt()
        {
            MapArtSet? art = AssetDatabase.LoadAssetAtPath<MapArtSet>(ArtSetPath);
            Assert.That(art, Is.Not.Null, ArtSetPath);
            return art!;
        }

        // The shared sprites plus the 60 vehicle sprites: 3 kinds x 5 tiers x 4 headings (Issue #288).
        private static IEnumerable<string> AllSpriteFiles()
        {
            foreach (string file in SpriteFiles)
            {
                yield return file;
            }

            foreach (string kind in VehicleKinds)
            {
                for (int tier = 1; tier <= 5; tier++)
                {
                    foreach (string heading in Headings)
                    {
                        yield return "vehicle_" + kind + "_mk" + tier + "_" + heading + ".png";
                    }
                }
            }
        }
    }
}
