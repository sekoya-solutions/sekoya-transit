#nullable enable
using System;
using Unity.VectorGraphics.Editor;
using UnityEditor;

namespace SekoyaTransit.UI.Editor
{
    /// <summary>
    /// Imports every SVG under <see cref="IconsRoot"/> as a UI Toolkit vector image (Issue #182), so a new icon file
    /// needs no manual importer setup.
    /// </summary>
    public sealed class IconSvgImportSettings : AssetPostprocessor
    {
        /// <summary>
        /// Folder holding the game's SVG icons.
        /// </summary>
        public const string IconsRoot = "Assets/_Project/Art/Icons/";

        /// <summary>
        /// Reimports <paramref name="assetPath"/> as a vector image if an earlier import used another type (files
        /// imported before this postprocessor existed).
        /// </summary>
        public static void EnsureVectorImage(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is SVGImporter importer && importer.SvgType != SVGType.VectorImage)
            {
                importer.SvgType = SVGType.VectorImage;
                importer.SaveAndReimport();
            }
        }

        private void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith(IconsRoot, StringComparison.Ordinal)
                || !assetPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (assetImporter is SVGImporter importer && importer.SvgType != SVGType.VectorImage)
            {
                importer.SvgType = SVGType.VectorImage;
            }
        }
    }
}
