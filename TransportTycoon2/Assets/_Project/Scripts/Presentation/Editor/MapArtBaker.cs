#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TransportTycoon.Presentation.Map;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TransportTycoon.Presentation.Editor
{
    /// <summary>
    /// Bakes the map art of ADR 0032 from the Kenney sources under <see cref="MapArtImportSettings.SourceRoot"/>:
    /// the road and rail atlas (2D road tiles cropped to the tile diamond, plus every rail shape built from the Train
    /// Kit's one-tile track) and the building and vehicle sprites (3D models rendered at the map's 2:1 angle). It then
    /// writes the <see cref="MapArtSet"/> the map views read. The baked PNGs are committed; re-bake after changing a
    /// source or this tool. Results can differ slightly between graphics cards, so the committed files are the reference.
    /// </summary>
    public static class MapArtBaker
    {
        public const string ArtSetPath = MapArtImportSettings.MapArtRoot + "MapArtSet.asset";
        public const string AtlasMaterialPath = MapArtImportSettings.MapArtRoot + "MapTileAtlas.mat";
        public const string AtlasPath = MapArtImportSettings.BakedRoot + MapArtImportSettings.AtlasFileName;

        /// <summary>Industry ids with art of their own, and the source each one is baked from.</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> IndustryModels = new[]
        {
            new KeyValuePair<string, string>("forest", ForestComposite),
            new KeyValuePair<string, string>("sawmill", "CityKitIndustrial/building-s"),
            new KeyValuePair<string, string>("furniture_factory", "CityKitIndustrial/building-m"),
            new KeyValuePair<string, string>("iron_mine", "CityKitIndustrial/building-e"),
        };

        /// <summary>Suburban houses a town picks from by town id.</summary>
        public static readonly IReadOnlyList<string> HouseModels = new[]
        {
            "CityKitSuburban/building-type-a",
            "CityKitSuburban/building-type-c",
            "CityKitSuburban/building-type-f",
            "CityKitSuburban/building-type-m",
        };

        private const string ForestComposite = "forest";
        private const string GenericIndustryModel = "CityKitIndustrial/building-j";
        private const string RoadStationModel = "CityKitIndustrial/building-a";
        private const string RailStationModel = "CityKitIndustrial/building-o";
        private const string TruckModel = "CarKit/delivery";
        private const string LocomotiveModel = "TrainKit/train-locomotive-c";
        private const string WagonModel = "TrainKit/train-carriage-container-blue";
        private const string TrackModel = "TrainKit/track";
        private const string RailsOnlyModel = "TrainKit/railroad-rail-straight";
        private const string AtlasShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const string BakeShaderName = "Hidden/TransportTycoon/MapArtBake";

        // Footprints in tile widths: buildings fill most of their tile; a train car spans one tile, a truck less.
        private const float BuildingFootprint = 0.9f;
        private const float RailCarLength = 0.95f;
        private const float TruckLength = 0.75f;

        // Camera: orthographic, pitch 30 and yaw 45 degrees turn a square tile into the 2:1 diamond. One tile width is
        // sqrt(2) model units across the screen and 128 pixels in the bake.
        private const float CameraPitch = 30f;
        private const float CameraYaw = 45f;
        private static readonly float UnitsPerPixel = Mathf.Sqrt(2f) / MapTileAtlas.CellWidth;

        // One light from the front-left: tops brightest, left faces lit, right faces shaded (as Kenney's 2D tiles).
        private static readonly Vector3 TowardLight = -(Quaternion.Euler(45f, 60f, 0f) * Vector3.forward);

        [MenuItem("Transport Tycoon/Art/Bake Map Art")]
        public static void BakeFromMenu()
        {
            Bake();
            Debug.Log("Map art baked to " + MapArtImportSettings.BakedRoot);
        }

        /// <summary>
        /// Batchmode entry point: Unity -batchmode -quit -executeMethod TransportTycoon.Presentation.Editor.MapArtBaker.BakeFromBatch
        /// </summary>
        public static void BakeFromBatch()
        {
            Bake();
        }

        /// <summary>
        /// File names (under the baked folder) of every building and vehicle sprite the bake writes.
        /// </summary>
        public static IReadOnlyList<string> ExpectedSpriteFiles()
        {
            var files = new List<string>
            {
                BuildingFile("road_station"),
                BuildingFile("rail_station"),
                BuildingFile("industry_generic"),
            };
            for (int index = 0; index < HouseModels.Count; index++)
            {
                files.Add(BuildingFile("house_" + index));
            }

            for (int index = 0; index < IndustryModels.Count; index++)
            {
                files.Add(BuildingFile("industry_" + IndustryModels[index].Key));
            }

            foreach (string vehicle in new[] { "truck", "locomotive", "wagon" })
            {
                for (int heading = 0; heading < MapHeadings.Count; heading++)
                {
                    files.Add(VehicleFile(vehicle, (MapHeading)heading));
                }
            }

            return files;
        }

        private static void Bake()
        {
            Directory.CreateDirectory(MapArtImportSettings.BakedRoot);
            using (var baker = new Session())
            {
                baker.BakeAtlas();
                baker.BakeBuilding(BuildingFile("road_station"), RoadStationModel);
                baker.BakeBuilding(BuildingFile("rail_station"), RailStationModel);
                baker.BakeBuilding(BuildingFile("industry_generic"), GenericIndustryModel);
                for (int index = 0; index < HouseModels.Count; index++)
                {
                    baker.BakeBuilding(BuildingFile("house_" + index), HouseModels[index]);
                }

                for (int index = 0; index < IndustryModels.Count; index++)
                {
                    string file = BuildingFile("industry_" + IndustryModels[index].Key);
                    if (IndustryModels[index].Value == ForestComposite)
                    {
                        baker.BakeForest(file);
                    }
                    else
                    {
                        baker.BakeBuilding(file, IndustryModels[index].Value);
                    }
                }

                for (int heading = 0; heading < MapHeadings.Count; heading++)
                {
                    var mapHeading = (MapHeading)heading;

                    // Car Kit and Train Kit models both face +Z, so yaw 0 faces north.
                    float yaw = 90f * heading;
                    baker.BakeVehicle(VehicleFile("truck", mapHeading), TruckModel, yaw, TruckLength);
                    baker.BakeVehicle(VehicleFile("locomotive", mapHeading), LocomotiveModel, yaw, RailCarLength);
                    baker.BakeVehicle(VehicleFile("wagon", mapHeading), WagonModel, yaw, RailCarLength);
                }
            }

            AssetDatabase.Refresh();
            WriteArtSet();
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

            SerializedProperty houses = so.FindProperty("_houses");
            houses.arraySize = HouseModels.Count;
            for (int index = 0; index < HouseModels.Count; index++)
            {
                houses.GetArrayElementAtIndex(index).objectReferenceValue = LoadSprite(BuildingFile("house_" + index));
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

            AssignHeadings(so.FindProperty("_truck"), "truck");
            AssignHeadings(so.FindProperty("_locomotive"), "locomotive");
            AssignHeadings(so.FindProperty("_wagon"), "wagon");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
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
        /// One bake run: the per-kit bake materials, the rail gauge, and the render targets.
        /// </summary>
        private sealed class Session : IDisposable
        {
            private readonly Shader _shader;
            private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
            private readonly List<UnityEngine.Object> _temporary = new List<UnityEngine.Object>();
            private readonly float _gauge;

            public Session()
            {
                _shader = Shader.Find(BakeShaderName)
                    ?? throw new InvalidOperationException("Shader not found: " + BakeShaderName);

                // The track is narrowed to the locomotive's scale so trains sit on their rails.
                Bounds locomotive = ModelBounds(LoadModel(LocomotiveModel), Matrix4x4.identity);
                _gauge = RailCarLength / Mathf.Max(locomotive.size.x, locomotive.size.z);
            }

            public void Dispose()
            {
                foreach (Material material in _materials.Values)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }

                foreach (UnityEngine.Object item in _temporary)
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }

            public void BakeAtlas()
            {
                var pixels = new Color32[MapTileAtlas.TextureWidth * MapTileAtlas.TextureHeight];
                foreach (KeyValuePair<int, string> road in RoadTiles())
                {
                    Blit(pixels, road.Key, CropRoadTile(road.Value));
                }

                var draws = new List<Draw>();
                foreach (KeyValuePair<int, Action<List<Draw>>> rail in RailShapes())
                {
                    draws.Clear();
                    rail.Value(draws);
                    Blit(
                        pixels,
                        rail.Key,
                        Render(draws, MapTileAtlas.CellWidth, MapTileAtlas.CellHeight, MapTileAtlas.CellHeight / 2));
                }

                WritePng(MapArtImportSettings.AtlasFileName, pixels, MapTileAtlas.TextureWidth, MapTileAtlas.TextureHeight);
            }

            public void BakeBuilding(string file, string model)
            {
                var draws = new List<Draw>();
                AddFitted(draws, model, 0f, BuildingFootprint);
                WritePng(
                    file,
                    Render(
                        draws,
                        MapArtImportSettings.BuildingCanvasWidth,
                        MapArtImportSettings.BuildingCanvasHeight,
                        MapArtImportSettings.BuildingPivotY),
                    MapArtImportSettings.BuildingCanvasWidth,
                    MapArtImportSettings.BuildingCanvasHeight);
            }

            public void BakeVehicle(string file, string model, float yaw, float length)
            {
                var draws = new List<Draw>();
                AddFitted(draws, model, yaw, length);
                WritePng(
                    file,
                    Render(
                        draws,
                        MapArtImportSettings.VehicleCanvasWidth,
                        MapArtImportSettings.VehicleCanvasHeight,
                        MapArtImportSettings.VehiclePivotY),
                    MapArtImportSettings.VehicleCanvasWidth,
                    MapArtImportSettings.VehicleCanvasHeight);
            }

            // Three 2D Kenney conifers on the tile, farthest first; positions are pixel offsets from the tile center.
            public void BakeForest(string file)
            {
                const int width = MapArtImportSettings.BuildingCanvasWidth;
                const int height = MapArtImportSettings.BuildingCanvasHeight;
                const int pivotX = width / 2;
                const int pivotY = MapArtImportSettings.BuildingPivotY;
                var pixels = new Color32[width * height];
                Texture2D tall = LoadSourcePng("IsometricRoads/coniferTall.png");
                Texture2D alternate = LoadSourcePng("IsometricRoads/coniferAltTall.png");
                DrawTree(pixels, width, height, tall, pivotX - 22, pivotY);
                DrawTree(pixels, width, height, alternate, pivotX + 18, pivotY - 4);
                DrawTree(pixels, width, height, tall, pivotX - 2, pivotY - 16);
                WritePng(file, pixels, width, height);
            }

            private static IEnumerable<KeyValuePair<int, string>> RoadTiles()
            {
                yield return new KeyValuePair<int, string>(MapTileAtlas.RoadNS, "roadNS");
                yield return new KeyValuePair<int, string>(MapTileAtlas.RoadEW, "roadEW");
                yield return new KeyValuePair<int, string>(MapTileAtlas.RoadNES, "crossroadNES");
                yield return new KeyValuePair<int, string>(MapTileAtlas.RoadESW, "crossroadESW");
                yield return new KeyValuePair<int, string>(MapTileAtlas.RoadNSW, "crossroadNSW");
                yield return new KeyValuePair<int, string>(MapTileAtlas.RoadNEW, "crossroadNEW");
                yield return new KeyValuePair<int, string>(MapTileAtlas.RoadCross, "crossroad");
                yield return new KeyValuePair<int, string>(MapTileAtlas.RoadNE, "roadNE");
                yield return new KeyValuePair<int, string>(MapTileAtlas.RoadES, "roadES");
                yield return new KeyValuePair<int, string>(MapTileAtlas.RoadSW, "roadSW");
                yield return new KeyValuePair<int, string>(MapTileAtlas.RoadNW, "roadNW");
                yield return new KeyValuePair<int, string>(MapTileAtlas.RoadEndN, "endN");
                yield return new KeyValuePair<int, string>(MapTileAtlas.RoadEndE, "endE");
                yield return new KeyValuePair<int, string>(MapTileAtlas.RoadEndS, "endS");
                yield return new KeyValuePair<int, string>(MapTileAtlas.RoadEndW, "endW");
            }

            // Yaw turns a piece that runs along +Z (north): 90 = east, 180 = south, 270 = west.
            private IEnumerable<KeyValuePair<int, Action<List<Draw>>>> RailShapes()
            {
                yield return Shape(MapTileAtlas.RailNS, d => AddTrack(d, 0f, 1f, 0f));
                yield return Shape(MapTileAtlas.RailEW, d => AddTrack(d, 90f, 1f, 0f));
                yield return Shape(MapTileAtlas.RailCross, d =>
                {
                    AddTrack(d, 0f, 1f, 0f);
                    AddTrack(d, 90f, 1f, 0f);
                });

                // T junctions: a straight piece plus a half piece toward the third side.
                yield return Shape(MapTileAtlas.RailNES, d =>
                {
                    AddTrack(d, 0f, 1f, 0f);
                    AddTrack(d, 90f, 0.5f, 0.25f);
                });
                yield return Shape(MapTileAtlas.RailNSW, d =>
                {
                    AddTrack(d, 0f, 1f, 0f);
                    AddTrack(d, 270f, 0.5f, 0.25f);
                });
                yield return Shape(MapTileAtlas.RailNEW, d =>
                {
                    AddTrack(d, 90f, 1f, 0f);
                    AddTrack(d, 0f, 0.5f, 0.25f);
                });
                yield return Shape(MapTileAtlas.RailESW, d =>
                {
                    AddTrack(d, 90f, 1f, 0f);
                    AddTrack(d, 180f, 0.5f, 0.25f);
                });

                // Corners: the canonical bend joins S and E; the yaw turns it to the other three corners.
                yield return Shape(MapTileAtlas.RailES, d => AddCorner(d, 0f));
                yield return Shape(MapTileAtlas.RailSW, d => AddCorner(d, 90f));
                yield return Shape(MapTileAtlas.RailNW, d => AddCorner(d, 180f));
                yield return Shape(MapTileAtlas.RailNE, d => AddCorner(d, 270f));

                // Level crossings: rails without sleepers, laid over the road tile.
                yield return Shape(MapTileAtlas.RailsOnlyNS, d => AddRailsOnly(d, 0f));
                yield return Shape(MapTileAtlas.RailsOnlyEW, d => AddRailsOnly(d, 90f));
            }

            private static KeyValuePair<int, Action<List<Draw>>> Shape(int cell, Action<List<Draw>> build)
            {
                return new KeyValuePair<int, Action<List<Draw>>>(cell, build);
            }

            private void AddTrack(List<Draw> draws, float yaw, float lengthScale, float offset)
            {
                Matrix4x4 placement = Matrix4x4.Rotate(Quaternion.Euler(0f, yaw, 0f))
                    * Matrix4x4.Translate(new Vector3(0f, 0f, offset))
                    * Matrix4x4.Scale(new Vector3(_gauge, _gauge, lengthScale));
                AddModel(draws, TrackModel, placement);
            }

            // The rails-only piece is four tiles long, starting at z = 0.
            private void AddRailsOnly(List<Draw> draws, float yaw)
            {
                Matrix4x4 placement = Matrix4x4.Rotate(Quaternion.Euler(0f, yaw, 0f))
                    * Matrix4x4.Translate(new Vector3(0f, 0f, -0.5f))
                    * Matrix4x4.Scale(new Vector3(_gauge, _gauge, 0.25f));
                AddModel(draws, RailsOnlyModel, placement);
            }

            // Bends the one-tile track into a quarter circle around the tile's south-east corner, after subdividing it
            // along its length so the rails curve smoothly. Texture coordinates, and so Kenney's colors, are kept.
            private void AddCorner(List<Draw> draws, float yaw)
            {
                GameObject model = LoadModel(TrackModel);
                Material material = MaterialFor(TrackModel);
                Matrix4x4 rotation = Matrix4x4.Rotate(Quaternion.Euler(0f, yaw, 0f));
                foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    Mesh source = filter.sharedMesh;
                    Matrix4x4 toModel = filter.transform.localToWorldMatrix;
                    Vector3[] vertices = source.vertices;
                    Vector2[] uvs = source.uv;
                    var positions = new List<Vector3>();
                    var bentUvs = new List<Vector2>();
                    var bent = new Mesh { name = "BentTrack", subMeshCount = source.subMeshCount };
                    var subMeshes = new List<int>[source.subMeshCount];
                    for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
                    {
                        subMeshes[subMesh] = new List<int>();
                        int[] triangles = source.GetTriangles(subMesh);
                        var work = new Stack<Corner[]>();
                        for (int index = 0; index < triangles.Length; index += 3)
                        {
                            var triangle = new Corner[3];
                            for (int k = 0; k < 3; k++)
                            {
                                Vector3 p = toModel.MultiplyPoint3x4(vertices[triangles[index + k]]);
                                triangle[k] = new Corner(new Vector3(p.x * _gauge, p.y * _gauge, p.z), uvs[triangles[index + k]]);
                            }

                            work.Push(triangle);
                        }

                        while (work.Count > 0)
                        {
                            Corner[] triangle = work.Pop();
                            int longest = LongestAlongTrack(triangle);
                            if (longest < 0)
                            {
                                for (int k = 0; k < 3; k++)
                                {
                                    subMeshes[subMesh].Add(positions.Count);
                                    positions.Add(Bend(triangle[k].Position));
                                    bentUvs.Add(triangle[k].Uv);
                                }

                                continue;
                            }

                            Corner a = triangle[longest];
                            Corner b = triangle[(longest + 1) % 3];
                            Corner c = triangle[(longest + 2) % 3];
                            var middle = new Corner((a.Position + b.Position) * 0.5f, (a.Uv + b.Uv) * 0.5f);
                            work.Push(new[] { a, middle, c });
                            work.Push(new[] { middle, b, c });
                        }
                    }

                    bent.SetVertices(positions);
                    bent.SetUVs(0, bentUvs);
                    for (int subMesh = 0; subMesh < subMeshes.Length; subMesh++)
                    {
                        bent.SetTriangles(subMeshes[subMesh], subMesh);
                    }

                    bent.RecalculateNormals();
                    bent.RecalculateBounds();
                    _temporary.Add(bent);
                    for (int subMesh = 0; subMesh < bent.subMeshCount; subMesh++)
                    {
                        draws.Add(new Draw(bent, rotation, subMesh, material));
                    }
                }
            }

            // Index of the triangle edge longer than the subdivision step along the track (z), or -1.
            private static int LongestAlongTrack(Corner[] triangle)
            {
                const float step = 0.04f;
                int longest = -1;
                float best = step;
                for (int edge = 0; edge < 3; edge++)
                {
                    float length = Mathf.Abs(triangle[edge].Position.z - triangle[(edge + 1) % 3].Position.z);
                    if (length > best)
                    {
                        best = length;
                        longest = edge;
                    }
                }

                return longest;
            }

            // Maps the straight piece (x across, z from -0.5 at the S edge to +0.5 at the N edge) onto an arc of
            // radius 0.5 centered on the tile's south-east corner, from the S edge midpoint to the E edge midpoint.
            private static Vector3 Bend(Vector3 p)
            {
                float t = Mathf.Clamp01(p.z + 0.5f);
                float theta = Mathf.PI - ((Mathf.PI / 2f) * t);
                float radius = 0.5f - p.x;
                return new Vector3(0.5f + (radius * Mathf.Cos(theta)), p.y, -0.5f + (radius * Mathf.Sin(theta)));
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

            // Resamples the top diamond of a 100 x 65 Kenney road tile (rows 1-51 from the top) to a 128 x 64 cell and
            // drops the block sides below it. The tile stays opaque 1.5 pixels past its diamond, filled with the color
            // of the diamond's edge, so neighboring road tiles overlap instead of leaving a seam of ground between them.
            private Color32[] CropRoadTile(string name)
            {
                const int width = MapTileAtlas.CellWidth;
                const int height = MapTileAtlas.CellHeight;
                const float halfWidth = width * 0.5f;
                const float halfHeight = height * 0.5f;
                const float insideEdge = 0.95f;
                const float bleedPixels = 1.5f;
                Texture2D source = LoadSourcePng("IsometricRoads/" + name + ".png");
                var pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Diamond-normalized offset from the center: |dx| + |dy| = 1 on the tile's edge.
                        float dx = (x + 0.5f - halfWidth) / halfWidth;
                        float dy = (y + 0.5f - halfHeight) / halfHeight;
                        float distance = Mathf.Abs(dx) + Mathf.Abs(dy);
                        float pull = distance > insideEdge ? insideEdge / distance : 1f;
                        float sampleX = halfWidth + (dx * pull * halfWidth);
                        float sampleY = halfHeight + (dy * pull * halfHeight);

                        float sourceRowFromTop = 1f + (((height - sampleY) / height) * 50f);
                        Color color = source.GetPixelBilinear(sampleX / width, 1f - (sourceRowFromTop / source.height));
                        color.a = Mathf.Clamp01(((1f - distance) * halfHeight) + bleedPixels + 0.5f);
                        pixels[(y * width) + x] = color;
                    }
                }

                return pixels;
            }

            private static void DrawTree(Color32[] canvas, int width, int height, Texture2D tree, int baseX, int baseY)
            {
                const float scale = 1.2f;
                int treeWidth = Mathf.RoundToInt(tree.width * scale);
                int treeHeight = Mathf.RoundToInt(tree.height * scale);
                int left = baseX - (treeWidth / 2);
                for (int y = 0; y < treeHeight; y++)
                {
                    for (int x = 0; x < treeWidth; x++)
                    {
                        int canvasX = left + x;
                        int canvasY = baseY + y;
                        if (canvasX < 0 || canvasY < 0 || canvasX >= width || canvasY >= height)
                        {
                            continue;
                        }

                        Color over = tree.GetPixelBilinear((x + 0.5f) / treeWidth, (y + 0.5f) / treeHeight);
                        int index = (canvasY * width) + canvasX;
                        canvas[index] = AlphaOver(over, canvas[index]);
                    }
                }
            }

            private static Color32 AlphaOver(Color over, Color under)
            {
                float alpha = over.a + (under.a * (1f - over.a));
                if (alpha <= 0f)
                {
                    return new Color32(0, 0, 0, 0);
                }

                Color color = ((over * over.a) + (under * under.a * (1f - over.a))) / alpha;
                color.a = alpha;
                return color;
            }

            private Texture2D LoadSourcePng(string relativePath)
            {
                string path = MapArtImportSettings.SourceRoot + relativePath;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    throw new InvalidDataException("Could not read " + path);
                }

                _temporary.Add(texture);
                return texture;
            }

            private static void Blit(Color32[] atlas, int cell, Color32[] cellPixels)
            {
                RectInt rect = MapTileAtlas.GetPixelRect(cell);
                for (int y = 0; y < rect.height; y++)
                {
                    Array.Copy(
                        cellPixels,
                        y * rect.width,
                        atlas,
                        ((rect.y + y) * MapTileAtlas.TextureWidth) + rect.x,
                        rect.width);
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

        private readonly struct Corner
        {
            public Corner(Vector3 position, Vector2 uv)
            {
                Position = position;
                Uv = uv;
            }

            public Vector3 Position { get; }

            public Vector2 Uv { get; }
        }
    }
}
