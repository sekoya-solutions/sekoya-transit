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
        public const string HousePrefix = BuildingPrefix + "house_";
        public const string IndustryPrefix = BuildingPrefix + "industry_";
        public const int VehicleCanvasWidth = 160;
        public const int VehicleCanvasHeight = 128;
        public const int VehiclePivotY = 52;

        /// <summary>
        /// Returns whether a baked file is a town house or an industry building (a 2x2 block), not a station.
        /// </summary>
        public static bool IsSite(string fileName)
        {
            return fileName.StartsWith(HousePrefix, StringComparison.Ordinal)
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
            if (fileName == AtlasFileName)
            {
                importer.textureType = TextureImporterType.Default;
                return;
            }

            bool isIcon = fileName.StartsWith(IconPrefix, StringComparison.Ordinal);
            float pivotY = isIcon
                ? 0.5f
                : fileName.StartsWith(VehiclePrefix, StringComparison.Ordinal)
                    ? (float)VehiclePivotY / VehicleCanvasHeight
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
            // Site sprites use a tight mesh so Sprite.bounds ends at the opaque top, where the map labels sit (Issue #219).
            settings.spriteMeshType = !isIcon && IsSite(fileName) ? SpriteMeshType.Tight : SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
