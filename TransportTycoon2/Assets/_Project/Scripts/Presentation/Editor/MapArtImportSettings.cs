#nullable enable
using System;
using TransportTycoon.Presentation.Map;
using UnityEditor;
using UnityEngine;

namespace TransportTycoon.Presentation.Editor
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
        public const float PixelsPerUnit = MapTileAtlas.CellWidth;

        // Sprite canvases (pixels) and where the tile center sits on them, from the bottom-left corner.
        public const int BuildingCanvasWidth = 160;
        public const int BuildingCanvasHeight = 200;
        public const int BuildingPivotY = 40;
        public const int VehicleCanvasWidth = 160;
        public const int VehicleCanvasHeight = 128;
        public const int VehiclePivotY = 52;

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

            float pivotY = fileName.StartsWith(VehiclePrefix, StringComparison.Ordinal)
                ? (float)VehiclePivotY / VehicleCanvasHeight
                : (float)BuildingPivotY / BuildingCanvasHeight;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, pivotY);
            settings.spritePixelsPerUnit = PixelsPerUnit;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
