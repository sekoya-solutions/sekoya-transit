#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using SekoyaTransit.Presentation.Map;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SekoyaTransit.Presentation.Editor
{
    /// <summary>
    /// Bakes the one remaining Kenney-based sprite of ADR 0032, the generic industry fallback (a 3D model from the
    /// sources under <see cref="MapArtImportSettings.SourceRoot"/>, rendered at the map's 2:1 angle), then writes the
    /// <see cref="MapArtSet"/> the map views read. Every other sprite is an original one (Issues #248, #254, #260, #262:
    /// towns, chain industries, vehicles, stations and the road and rail atlas) that is committed as it is and never
    /// re-baked. Results can differ slightly between graphics cards, so the committed files are the reference.
    /// </summary>
    public static class MapArtBaker
    {
        public const string ArtSetPath = MapArtImportSettings.MapArtRoot + "MapArtSet.asset";
        public const string AtlasMaterialPath = MapArtImportSettings.MapArtRoot + "MapTileAtlas.mat";
        public const string AtlasPath = MapArtImportSettings.BakedRoot + MapArtImportSettings.AtlasFileName;

        /// <summary>
        /// Number of town sprites, one per growth tier (Issue #292). They are original sprites: the bake leaves
        /// <c>building_town_mkN.png</c> (N = 1 to 5) as committed and only wires it into the art set.
        /// </summary>
        public const int TownTierCount = 5;

        /// <summary>Vehicle tiers MK1 to MK5 (Issue #288); each has its own truck, locomotive and wagon sprites.</summary>
        public const int VehicleTierCount = 5;

        /// <summary>
        /// Marks an industry whose committed sprite is an original, ready-made one that the bake must not overwrite.
        /// </summary>
        public const string OriginalSprite = "original";

        /// <summary>Industry ids with art of their own, and the source each one is baked from.</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> IndustryModels = new[]
        {
            new KeyValuePair<string, string>("forest", OriginalSprite),
            new KeyValuePair<string, string>("sawmill", OriginalSprite),
            new KeyValuePair<string, string>("furniture_factory", OriginalSprite),
            new KeyValuePair<string, string>("wheat_farm", OriginalSprite),
            new KeyValuePair<string, string>("flour_mill", OriginalSprite),
            new KeyValuePair<string, string>("bakery", OriginalSprite),
            new KeyValuePair<string, string>("iron_mine", OriginalSprite),
            new KeyValuePair<string, string>("iron_foundry", OriginalSprite),
            new KeyValuePair<string, string>("machine_shop", OriginalSprite),
            new KeyValuePair<string, string>("livestock_ranch", OriginalSprite),
            new KeyValuePair<string, string>("butcher", OriginalSprite),
            new KeyValuePair<string, string>("cannery", OriginalSprite),
            new KeyValuePair<string, string>("sheep_farm", OriginalSprite),
            new KeyValuePair<string, string>("textile_mill", OriginalSprite),
            new KeyValuePair<string, string>("clothing_workshop", OriginalSprite),
        };

        private const string GenericIndustryModel = "CityKitIndustrial/building-j";
        private const string AtlasShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const string BakeShaderName = "Hidden/SekoyaTransit/MapArtBake";

        // Footprint in tile widths: buildings fill most of their tile.
        private const float BuildingFootprint = 0.9f;

        // Camera: orthographic, pitch 30 and yaw 45 degrees turn a square tile into the 2:1 diamond. One tile width is
        // sqrt(2) model units across the screen and 128 pixels in the bake.
        private const float CameraPitch = 30f;
        private const float CameraYaw = 45f;
        private static readonly float UnitsPerPixel = Mathf.Sqrt(2f) / MapTileAtlas.CellWidth;

        // One light from the front-left: tops brightest, left faces lit, right faces shaded (as Kenney's 2D tiles).
        private static readonly Vector3 TowardLight = -(Quaternion.Euler(45f, 60f, 0f) * Vector3.forward);

        [MenuItem("Sekoya Transit/Art/Bake Map Art")]
        public static void BakeFromMenu()
        {
            Bake();
            Debug.Log("Map art baked to " + MapArtImportSettings.BakedRoot);
        }

        /// <summary>
        /// Batchmode entry point: Unity -batchmode -quit -executeMethod SekoyaTransit.Presentation.Editor.MapArtBaker.BakeFromBatch
        /// </summary>
        public static void BakeFromBatch()
        {
            Bake();
        }

        /// <summary>
        /// File names (under the baked folder) of every building and vehicle sprite the art set uses (the vehicle ones are original, not baked).
        /// </summary>
        public static IReadOnlyList<string> ExpectedSpriteFiles()
        {
            var files = new List<string>
            {
                BuildingFile("road_station"),
                BuildingFile("rail_station"),
                BuildingFile("industry_generic"),
            };
            for (int tier = 1; tier <= TownTierCount; tier++)
            {
                files.Add(BuildingFile("town_mk" + tier));
            }

            for (int index = 0; index < IndustryModels.Count; index++)
            {
                files.Add(BuildingFile("industry_" + IndustryModels[index].Key));
            }

            foreach (string vehicle in new[] { "truck", "locomotive", "wagon" })
            {
                for (int tier = 1; tier <= VehicleTierCount; tier++)
                {
                    for (int heading = 0; heading < MapHeadings.Count; heading++)
                    {
                        files.Add(VehicleFile(VehicleTierName(vehicle, tier), (MapHeading)heading));
                    }
                }
            }

            return files;
        }

        private static void Bake()
        {
            Directory.CreateDirectory(MapArtImportSettings.BakedRoot);
            using (var baker = new Session())
            {
                baker.BakeSite(BuildingFile("industry_generic"), GenericIndustryModel);
                for (int index = 0; index < IndustryModels.Count; index++)
                {
                    if (IndustryModels[index].Value != OriginalSprite)
                    {
                        baker.BakeSite(BuildingFile("industry_" + IndustryModels[index].Key), IndustryModels[index].Value);
                    }
                }
            }

            AssetDatabase.Refresh();

            // Import settings can change while the baked pixels stay identical, so re-apply them to every sprite.
            foreach (string file in ExpectedSpriteFiles())
            {
                AssetDatabase.ImportAsset(MapArtImportSettings.BakedRoot + file, ImportAssetOptions.ForceUpdate);
            }

            WriteArtSet();
            CargoIconBaker.Bake();
        }

        private static string BuildingFile(string name)
        {
            return MapArtImportSettings.BuildingPrefix + name + ".png";
        }

        private static string VehicleFile(string vehicle, MapHeading heading)
        {
            return MapArtImportSettings.VehiclePrefix + vehicle + "_" + heading.ToString().ToLowerInvariant() + ".png";
        }

        private static void WriteArtSet()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath)
                ?? throw new FileNotFoundException("Baked atlas is missing.", AtlasPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AtlasMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find(AtlasShaderName)
                    ?? throw new InvalidOperationException("Shader not found: " + AtlasShaderName);
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, AtlasMaterialPath);
            }

            material.mainTexture = atlas;
            EditorUtility.SetDirty(material);

            MapArtSet set = AssetDatabase.LoadAssetAtPath<MapArtSet>(ArtSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<MapArtSet>();
                AssetDatabase.CreateAsset(set, ArtSetPath);
            }

            var so = new SerializedObject(set);
            so.FindProperty("_tileAtlasMaterial").objectReferenceValue = material;
            so.FindProperty("_roadStation").objectReferenceValue = LoadSprite(BuildingFile("road_station"));
            so.FindProperty("_railStation").objectReferenceValue = LoadSprite(BuildingFile("rail_station"));
            so.FindProperty("_genericIndustry").objectReferenceValue = LoadSprite(BuildingFile("industry_generic"));

            SerializedProperty townTiers = so.FindProperty("_townTiers");
            townTiers.arraySize = TownTierCount;
            for (int index = 0; index < TownTierCount; index++)
            {
                townTiers.GetArrayElementAtIndex(index).objectReferenceValue = LoadSprite(BuildingFile("town_mk" + (index + 1)));
            }

            SerializedProperty industries = so.FindProperty("_industries");
            industries.arraySize = IndustryModels.Count;
            for (int index = 0; index < IndustryModels.Count; index++)
            {
                SerializedProperty entry = industries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("_industryId").stringValue = IndustryModels[index].Key;
                entry.FindPropertyRelative("_sprite").objectReferenceValue =
                    LoadSprite(BuildingFile("industry_" + IndustryModels[index].Key));
            }

            AssignVehicleTiers(so.FindProperty("_truckTypes"), "truck", "truck");
            AssignVehicleTiers(so.FindProperty("_locomotiveTypes"), "locomotive", "loco");
            AssignVehicleTiers(so.FindProperty("_wagonTypes"), "wagon", "loco");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
        }

        // One table entry per tier. A wagon is keyed by its locomotive type id, as it takes the tier of its locomotive.
        private static void AssignVehicleTiers(SerializedProperty table, string spriteName, string typeIdPrefix)
        {
            table.arraySize = VehicleTierCount;
            for (int tier = 1; tier <= VehicleTierCount; tier++)
            {
                SerializedProperty entry = table.GetArrayElementAtIndex(tier - 1);
                entry.FindPropertyRelative("_typeId").stringValue = VehicleTierName(typeIdPrefix, tier);
                AssignHeadings(entry.FindPropertyRelative("_sprites"), VehicleTierName(spriteName, tier));
            }
        }

        private static string VehicleTierName(string name, int tier)
        {
            return name + "_mk" + tier;
        }

        private static void AssignHeadings(SerializedProperty property, string vehicle)
        {
            property.arraySize = MapHeadings.Count;
            for (int heading = 0; heading < MapHeadings.Count; heading++)
            {
                property.GetArrayElementAtIndex(heading).objectReferenceValue =
                    LoadSprite(VehicleFile(vehicle, (MapHeading)heading));
            }
        }

        private static Sprite LoadSprite(string file)
        {
            string path = MapArtImportSettings.BakedRoot + file;
            return AssetDatabase.LoadAssetAtPath<Sprite>(path)
                ?? throw new FileNotFoundException("Baked sprite is missing.", path);
        }

        /// <summary>
        /// One bake run: the per-kit bake materials and the render targets.
        /// </summary>
        private sealed class Session : IDisposable
        {
            private readonly Shader _shader;
            private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();

            public Session()
            {
                _shader = Shader.Find(BakeShaderName)
                    ?? throw new InvalidOperationException("Shader not found: " + BakeShaderName);
            }

            public void Dispose()
            {
                foreach (Material material in _materials.Values)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }


            // A town or industry fills most of its 2x2 block: the same relative footprint as a one-tile building.
            public void BakeSite(string file, string model)
            {
                var draws = new List<Draw>();
                AddFitted(draws, model, 0f, BuildingFootprint * IsometricProjection.IndustrySizeTiles);
                WritePng(
                    file,
                    Render(
                        draws,
                        MapArtImportSettings.SiteCanvasWidth,
                        MapArtImportSettings.SiteCanvasHeight,
                        MapArtImportSettings.SitePivotY),
                    MapArtImportSettings.SiteCanvasWidth,
                    MapArtImportSettings.SiteCanvasHeight);
            }


            // Scales a model so its longest horizontal side is size tiles, centered on the tile and resting on y = 0.
            private void AddFitted(List<Draw> draws, string model, float yaw, float size)
            {
                Bounds bounds = ModelBounds(LoadModel(model), Matrix4x4.identity);
                float scale = size / Mathf.Max(bounds.size.x, bounds.size.z);
                Matrix4x4 placement = Matrix4x4.Rotate(Quaternion.Euler(0f, yaw, 0f))
                    * Matrix4x4.Scale(Vector3.one * scale)
                    * Matrix4x4.Translate(new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z));
                AddModel(draws, model, placement);
            }

            private void AddModel(List<Draw> draws, string model, Matrix4x4 placement)
            {
                Material material = MaterialFor(model);
                foreach (MeshFilter filter in LoadModel(model).GetComponentsInChildren<MeshFilter>(true))
                {
                    Matrix4x4 matrix = placement * filter.transform.localToWorldMatrix;
                    for (int subMesh = 0; subMesh < filter.sharedMesh.subMeshCount; subMesh++)
                    {
                        draws.Add(new Draw(filter.sharedMesh, matrix, subMesh, material));
                    }
                }
            }

            private static Bounds ModelBounds(GameObject model, Matrix4x4 placement)
            {
                bool any = false;
                var bounds = new Bounds();
                foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    Matrix4x4 matrix = placement * filter.transform.localToWorldMatrix;
                    Bounds local = filter.sharedMesh.bounds;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 point = local.center + Vector3.Scale(
                            local.extents,
                            new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                        Vector3 world = matrix.MultiplyPoint3x4(point);
                        if (!any)
                        {
                            bounds = new Bounds(world, Vector3.zero);
                            any = true;
                        }
                        else
                        {
                            bounds.Encapsulate(world);
                        }
                    }
                }

                return bounds;
            }

            private static GameObject LoadModel(string model)
            {
                string path = MapArtImportSettings.SourceRoot + model + ".fbx";
                return AssetDatabase.LoadAssetAtPath<GameObject>(path)
                    ?? throw new FileNotFoundException("Source model is missing (see CREDITS.md).", path);
            }

            // Each Kenney kit shares one color map texture next to its models.
            private Material MaterialFor(string model)
            {
                string kit = model.Substring(0, model.IndexOf('/'));
                if (_materials.TryGetValue(kit, out Material? existing))
                {
                    return existing;
                }

                string path = MapArtImportSettings.SourceRoot + kit + "/Textures/colormap.png";
                Texture2D colorMap = AssetDatabase.LoadAssetAtPath<Texture2D>(path)
                    ?? throw new FileNotFoundException("Kit color map is missing.", path);
                var material = new Material(_shader) { mainTexture = colorMap };
                material.SetVector("_LightDirection", TowardLight);
                _materials[kit] = material;
                return material;
            }

            // Renders the draws into a width x height image with the tile center at (width / 2, pivotY) pixels from
            // the bottom-left; returns straight-alpha pixels.
            private static Color32[] Render(List<Draw> draws, int width, int height, int pivotY)
            {
                Quaternion rotation = Quaternion.Euler(CameraPitch, CameraYaw, 0f);
                float viewWidth = width * UnitsPerPixel;
                float viewHeight = height * UnitsPerPixel;
                Vector3 center = rotation * Vector3.up * (((height * 0.5f) - pivotY) * UnitsPerPixel);
                Vector3 position = center - (rotation * Vector3.forward * 50f);
                Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f))
                    * Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
                // SetViewProjectionMatrices takes the plain (OpenGL-style) projection and adapts it to the graphics API.
                Matrix4x4 projection =
                    Matrix4x4.Ortho(-viewWidth / 2f, viewWidth / 2f, -viewHeight / 2f, viewHeight / 2f, 0.1f, 200f);

                var multisampled = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                {
                    antiAliasing = 8,
                };
                var resolved = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                var readback = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                RenderTexture? previous = RenderTexture.active;
                try
                {
                    using (var commands = new CommandBuffer { name = "MapArtBake" })
                    {
                        commands.SetRenderTarget(multisampled);
                        commands.ClearRenderTarget(true, true, new Color(0f, 0f, 0f, 0f));
                        commands.SetViewProjectionMatrices(view, projection);
                        foreach (Draw draw in draws)
                        {
                            commands.DrawMesh(draw.Mesh, draw.Matrix, draw.Material, draw.SubMesh, 0);
                        }

                        Graphics.ExecuteCommandBuffer(commands);
                    }

                    Graphics.Blit(multisampled, resolved);
                    RenderTexture.active = resolved;
                    readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    readback.Apply(false, false);
                    Color32[] pixels = readback.GetPixels32();
                    Unpremultiply(pixels);
                    return pixels;
                }
                finally
                {
                    RenderTexture.active = previous;
                    UnityEngine.Object.DestroyImmediate(multisampled);
                    UnityEngine.Object.DestroyImmediate(resolved);
                    UnityEngine.Object.DestroyImmediate(readback);
                }
            }

            // Antialiased edges were blended against a transparent black clear; restore straight alpha so they do
            // not show dark fringes.
            private static void Unpremultiply(Color32[] pixels)
            {
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color32 pixel = pixels[index];
                    if (pixel.a == 0 || pixel.a == 255)
                    {
                        continue;
                    }

                    pixels[index] = new Color32(
                        (byte)Mathf.Min(255, pixel.r * 255 / pixel.a),
                        (byte)Mathf.Min(255, pixel.g * 255 / pixel.a),
                        (byte)Mathf.Min(255, pixel.b * 255 / pixel.a),
                        pixel.a);
                }
            }


            private static void WritePng(string file, Color32[] pixels, int width, int height)
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                try
                {
                    texture.SetPixels32(pixels);
                    texture.Apply(false, false);
                    string path = MapArtImportSettings.BakedRoot + file;
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        private readonly struct Draw
        {
            public Draw(Mesh mesh, Matrix4x4 matrix, int subMesh, Material material)
            {
                Mesh = mesh;
                Matrix = matrix;
                SubMesh = subMesh;
                Material = material;
            }

            public Mesh Mesh { get; }

            public Matrix4x4 Matrix { get; }

            public int SubMesh { get; }

            public Material Material { get; }
        }
    }
}
