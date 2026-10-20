#nullable enable
using System;
using UnityEditor;
using UnityEngine;

namespace TransportTycoon.Art.Editor
{
    public sealed class ArtPilotImportSettings : AssetPostprocessor
    {
        private const string PilotRoot = "Assets/_Project/Art/Pilot/";
        private const string GroundTileName = "ground_grass_01.png";
        private const string VehicleName = "vehicle_road_cargo_lorry_01.png";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(PilotRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            bool isHighResolutionAsset =
                assetPath.EndsWith(GroundTileName, StringComparison.OrdinalIgnoreCase) ||
                assetPath.EndsWith(VehicleName, StringComparison.OrdinalIgnoreCase);
            importer.spritePixelsPerUnit = isHighResolutionAsset ? 128f : 64f;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = assetPath.EndsWith(GroundTileName, StringComparison.OrdinalIgnoreCase)
                ? new Vector2(0.5f, 0.5f)
                : new Vector2(0.5f, 0f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
