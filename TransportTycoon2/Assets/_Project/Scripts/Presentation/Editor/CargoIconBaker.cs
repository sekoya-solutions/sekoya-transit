#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TransportTycoon.Presentation.Map;
using TransportTycoon.UI.Editor;
using Unity.VectorGraphics;
using UnityEditor;
using UnityEngine;

namespace TransportTycoon.Presentation.Editor
{
    /// <summary>
    /// Bakes the UI cargo SVG icons (ADR 0031) into PNG sprites for the world-space map labels (Issue #193) and stores
    /// them in the <see cref="MapArtSet"/>. The SVGs are tessellated with Unity's vector graphics module and the
    /// triangles are filled on the CPU with supersampling, so the bake needs no render pipeline or shader.
    /// </summary>
    public static class CargoIconBaker
    {
        public const string CargoIconsRoot = IconSvgImportSettings.IconsRoot + "Cargo/";
        public const string GenericIconPath = IconSvgImportSettings.IconsRoot + "Common/cargo_generic.svg";
        public const string GenericIconId = "cargo_generic";

        // Samples per pixel side; 4 x 4 samples give smooth edges at the label size.
        private const int Supersample = 4;

        [MenuItem("Transport Tycoon/Art/Bake Cargo Icons")]
        public static void BakeFromMenu()
        {
            Bake();
            Debug.Log("Cargo icons baked to " + MapArtImportSettings.BakedRoot);
        }

        /// <summary>
        /// Batchmode entry point: Unity -batchmode -quit -executeMethod TransportTycoon.Presentation.Editor.CargoIconBaker.BakeFromBatch
        /// </summary>
        public static void BakeFromBatch()
        {
            Bake();
        }

        /// <summary>
        /// Writes one PNG per cargo SVG plus the generic icon, then points the map art set at them.
        /// </summary>
        public static void Bake()
        {
            Directory.CreateDirectory(MapArtImportSettings.BakedRoot);
            string[] svgFiles = Directory.GetFiles(CargoIconsRoot, "*.svg", SearchOption.TopDirectoryOnly);
            Array.Sort(svgFiles, StringComparer.Ordinal);
            var cargoIds = new List<string>(svgFiles.Length);
            foreach (string svgFile in svgFiles)
            {
                string cargoId = Path.GetFileNameWithoutExtension(svgFile);
                BakeIcon(svgFile, IconFile(cargoId));
                cargoIds.Add(cargoId);
            }

            BakeIcon(GenericIconPath, IconFile(GenericIconId));
            AssetDatabase.Refresh();
            WriteArtSet(cargoIds);
        }

        private static string IconFile(string id)
        {
            return MapArtImportSettings.BakedRoot + MapArtImportSettings.IconPrefix + id + ".png";
        }

        private static void BakeIcon(string svgPath, string pngPath)
        {
            SVGParser.SceneInfo info;
            using (var reader = new StreamReader(svgPath))
            {
                info = SVGParser.ImportSVG(reader);
            }

            var options = new VectorUtils.TessellationOptions
            {
                StepDistance = 0.1f,
                MaxCordDeviation = 0.01f,
                MaxTanAngleDeviation = 0.01f,
                SamplingStepSize = 0.01f,
            };
            List<VectorUtils.Geometry> geometries = VectorUtils.TessellateScene(info.Scene, options);
            Rect viewport = info.SceneViewport;
            int size = MapArtImportSettings.IconPixels * Supersample;
            var samples = new Color[size * size];
            foreach (VectorUtils.Geometry geometry in geometries)
            {
                FillGeometry(samples, size, viewport, geometry);
            }

            Color32[] pixels = Downsample(samples, size);
            var texture = new Texture2D(MapArtImportSettings.IconPixels, MapArtImportSettings.IconPixels, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pixels);
                File.WriteAllBytes(pngPath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void FillGeometry(Color[] samples, int size, Rect viewport, VectorUtils.Geometry geometry)
        {
            if (geometry.Vertices == null || geometry.Indices == null)
            {
                return;
            }

            // Sample coordinates: x right, y down as in SVG; rows are flipped when written to the texture.
            var points = new Vector2[geometry.Vertices.Length];
            for (int index = 0; index < points.Length; index++)
            {
                Vector2 point = geometry.WorldTransform.MultiplyPoint(geometry.Vertices[index]);
                points[index] = new Vector2(
                    (point.x - viewport.x) / viewport.width * size,
                    (point.y - viewport.y) / viewport.height * size);
            }

            for (int index = 0; index + 2 < geometry.Indices.Length; index += 3)
            {
                FillTriangle(
                    samples,
                    size,
                    points[geometry.Indices[index]],
                    points[geometry.Indices[index + 1]],
                    points[geometry.Indices[index + 2]],
                    geometry.Color);
            }
        }

        private static void FillTriangle(Color[] samples, int size, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            float area = Edge(a, b, c);
            if (Mathf.Approximately(area, 0f))
            {
                return;
            }

            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var center = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Edge(b, c, center) / area;
                    float w1 = Edge(c, a, center) / area;
                    float w2 = Edge(a, b, center) / area;
                    if (w0 < 0f || w1 < 0f || w2 < 0f)
                    {
                        continue;
                    }

                    int sample = ((size - 1 - y) * size) + x;
                    samples[sample] = Over(color, samples[sample]);
                }
            }
        }

        private static float Edge(Vector2 from, Vector2 to, Vector2 point)
        {
            return ((to.x - from.x) * (point.y - from.y)) - ((to.y - from.y) * (point.x - from.x));
        }

        // Straight-alpha "source over destination" compositing.
        private static Color Over(Color source, Color destination)
        {
            float alpha = source.a + (destination.a * (1f - source.a));
            if (alpha <= 0f)
            {
                return Color.clear;
            }

            Color rgb = ((source * source.a) + (destination * destination.a * (1f - source.a))) / alpha;
            return new Color(rgb.r, rgb.g, rgb.b, alpha);
        }

        // Averages each block of samples with alpha weighting so transparent samples do not darken edges.
        private static Color32[] Downsample(Color[] samples, int size)
        {
            int pixelsPerSide = size / Supersample;
            var pixels = new Color32[pixelsPerSide * pixelsPerSide];
            for (int py = 0; py < pixelsPerSide; py++)
            {
                for (int px = 0; px < pixelsPerSide; px++)
                {
                    float r = 0f;
                    float g = 0f;
                    float b = 0f;
                    float a = 0f;
                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            Color sample = samples[(((py * Supersample) + sy) * size) + (px * Supersample) + sx];
                            r += sample.r * sample.a;
                            g += sample.g * sample.a;
                            b += sample.b * sample.a;
                            a += sample.a;
                        }
                    }

                    Color pixel = a > 0f
                        ? new Color(r / a, g / a, b / a, a / (Supersample * Supersample))
                        : Color.clear;
                    pixels[(py * pixelsPerSide) + px] = pixel;
                }
            }

            return pixels;
        }

        private static void WriteArtSet(IReadOnlyList<string> cargoIds)
        {
            MapArtSet set = AssetDatabase.LoadAssetAtPath<MapArtSet>(MapArtBaker.ArtSetPath)
                ?? throw new FileNotFoundException("Map art set is missing; bake the map art first.", MapArtBaker.ArtSetPath);
            var so = new SerializedObject(set);
            SerializedProperty icons = so.FindProperty("_cargoIcons");
            icons.arraySize = cargoIds.Count;
            for (int index = 0; index < cargoIds.Count; index++)
            {
                SerializedProperty entry = icons.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("_cargoId").stringValue = cargoIds[index];
                entry.FindPropertyRelative("_sprite").objectReferenceValue = LoadSprite(IconFile(cargoIds[index]));
            }

            so.FindProperty("_genericCargoIcon").objectReferenceValue = LoadSprite(IconFile(GenericIconId));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
        }

        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path)
                ?? throw new FileNotFoundException("Baked cargo icon is missing.", path);
        }
    }
}
