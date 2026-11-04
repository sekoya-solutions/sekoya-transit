#nullable enable
using System;
using SekoyaTransit.Presentation.Map;
using UnityEditor;
using UnityEngine;

namespace SekoyaTransit.Presentation.Editor
{
    /// <summary>
    /// Import settings for the map art of ADR 0032. Source models import without materials (the bake tool brings its
    /// own); source color maps sample with point filtering, like Kenney's palette textures expect. Baked building and
    /// vehicle sprites are single sprites at 128 pixels per unit (one tile width) with their tile-center pivot; the road
    /// and rail atlas is a plain texture. Everything baked is uncompressed with mipmaps for far zoom.
    /// </summary>
    public sealed class MapArtImportSettings : AssetPostprocessor
    {
        public const string MapArtRoot = "Assets/_Project/Art/Map/";
        public const string SourceRoot = MapArtRoot + "Source/";
        public const string BakedRoot = MapArtRoot + "Baked/";
        public const string AtlasFileName = "MapTileAtlas.png";
        public const string GroundAtlasFileName = "MapGroundAtlas.png";
        public const string BuildingPrefix = "building_";
        public const string VehiclePrefix = "vehicle_";
        public const string IconPrefix = "icon_";

        // Cargo icon sprites for map labels (Issue #193): square canvas, centered pivot, one world unit tall before scaling.
        public const int IconPixels = 64;
        public const float PixelsPerUnit = MapTileAtlas.CellWidth;

        // Sprite canvases (pixels) and where the tile center sits on them, from the bottom-left corner.
        public const int BuildingCanvasWidth = 160;
        public const int BuildingCanvasHeight = 200;
        public const int BuildingPivotY = 40;

        // Town and industry sprites cover a 2x2 block (Issue #219): twice the building canvas, pivot at the block center.
        public const int SiteCanvasWidth = BuildingCanvasWidth * 2;
        public const int SiteCanvasHeight = BuildingCanvasHeight * 2;
        public const int SitePivotY = BuildingPivotY * 2;

        // Town sprites cover a 3x3 block (Issue #250, #292): three times the building canvas (480x600 px), pivot at the
        // block center (normalized 0.5, 0.2) at 128 pixels per unit.
        public const int TownCanvasHeight = BuildingCanvasHeight * 3;
        public const int TownPivotY = BuildingPivotY * 3;
        public const string TownPrefix = BuildingPrefix + "town_";
        public const string IndustryPrefix = BuildingPrefix + "industry_";
        public const int VehicleCanvasWidth = 160;
        public const int VehicleCanvasHeight = 128;
        public const int VehiclePivotY = 52;

        // Original (Grok Imagine) site sprites are not baked, so they are fitted at import (Issue #248): each is scaled
        // (z, 1 = the baked scale) about its pivot, which sits on its own ground point (x, y in pixels from the left and
        // bottom edges) so the whole base stays inside the diamond of the block (2x2 industries, 3x3 town), touching its front edges.
        private static readonly System.Collections.Generic.Dictionary<string, Vector3> OriginalSiteFits =
            new System.Collections.Generic.Dictionary<string, Vector3>(StringComparer.Ordinal)
            {
                { "building_industry_forest.png", new Vector3(159f, 58.2f, 0.95f) },
                { "building_industry_sawmill.png", new Vector3(160f, 63.2f, 0.95f) },
                { "building_industry_furniture_factory.png", new Vector3(160f, 75.2f, 0.95f) },
                { "building_industry_wheat_farm.png", new Vector3(160f, 73.2f, 0.95f) },
                { "building_industry_flour_mill.png", new Vector3(160f, 61.2f, 0.95f) },
                { "building_industry_bakery.png", new Vector3(160f, 69.2f, 0.95f) },
                { "building_industry_iron_mine.png", new Vector3(160f, 68.2f, 0.95f) },
                { "building_industry_iron_foundry.png", new Vector3(160f, 50.7f, 0.95f) },
                { "building_industry_machine_shop.png", new Vector3(160f, 69.7f, 0.95f) },
                { "building_industry_livestock_ranch.png", new Vector3(160f, 57.2f, 0.95f) },
                { "building_industry_butcher.png", new Vector3(160f, 55.2f, 0.95f) },
                { "building_industry_cannery.png", new Vector3(160f, 58.2f, 0.95f) },
                { "building_industry_sheep_farm.png", new Vector3(160f, 55.2f, 0.95f) },
                { "building_industry_textile_mill.png", new Vector3(160f, 53.2f, 0.95f) },
                { "building_industry_clothing_workshop.png", new Vector3(160f, 64.7f, 0.95f) },
            };

        /// <summary>
        /// Returns whether a baked file is a town house or an industry building (a 2x2 block), not a station.
        /// </summary>
        public static bool IsSite(string fileName)
        {
            return fileName.StartsWith(TownPrefix, StringComparison.Ordinal)
                || fileName.StartsWith(IndustryPrefix, StringComparison.Ordinal);
        }

        private void OnPreprocessModel()
        {
            if (assetPath.StartsWith(SourceRoot, StringComparison.Ordinal) && assetImporter is ModelImporter importer)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.isReadable = true;
            }
        }

        private void OnPreprocessTexture()
        {
            if (!(assetImporter is TextureImporter importer))
            {
                return;
            }

            if (assetPath.StartsWith(SourceRoot, StringComparison.Ordinal))
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }

            if (!assetPath.StartsWith(BakedRoot, StringComparison.Ordinal))
            {
                return;
            }

            string fileName = assetPath.Substring(BakedRoot.Length);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (fileName == AtlasFileName || fileName == GroundAtlasFileName)
            {
                importer.textureType = TextureImporterType.Default;
                return;
            }

            bool isIcon = fileName.StartsWith(IconPrefix, StringComparison.Ordinal);
            float pivotY = isIcon
                ? 0.5f
                : fileName.StartsWith(VehiclePrefix, StringComparison.Ordinal)
                    ? (float)VehiclePivotY / VehicleCanvasHeight
                    : fileName.StartsWith(TownPrefix, StringComparison.Ordinal)
                        ? (float)TownPivotY / TownCanvasHeight
                    : IsSite(fileName)
                        ? (float)SitePivotY / SiteCanvasHeight
                        : (float)BuildingPivotY / BuildingCanvasHeight;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, pivotY);
            settings.spritePixelsPerUnit = isIcon ? IconPixels : PixelsPerUnit;
            if (OriginalSiteFits.TryGetValue(fileName, out Vector3 originalFit))
            {
                settings.spritePivot = new Vector2(originalFit.x / SiteCanvasWidth, originalFit.y / SiteCanvasHeight);
                settings.spritePixelsPerUnit = PixelsPerUnit / originalFit.z;
            }

            // Site sprites use a tight mesh so Sprite.bounds ends at the opaque top, where the map labels sit (Issue #219).
            settings.spriteMeshType = !isIcon && IsSite(fileName) ? SpriteMeshType.Tight : SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
