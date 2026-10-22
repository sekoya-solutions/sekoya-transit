#nullable enable
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TransportTycoon.Art.Editor
{
    public static class ArtPilotSceneBuilder
    {
        private const string ArtRoot = "Assets/_Project/Art/Pilot";
        private const string ScenePath = ArtRoot + "/ArtPilotPreview.unity";
        private const int GridSize = 8;
        private const int PlacementSeed = 16002618;

        public static void CreatePreviewScene()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            Sprite ground = LoadSprite("ground_grass_01.png");
            Sprite town = LoadSprite("building_town_shop_01.png");
            Sprite industry = LoadSprite("building_industry_flour_mill_01.png");
            Sprite truck = LoadSprite("vehicle_road_cargo_lorry_01.png");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Art Pilot Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.1f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.13f, 0.16f, 0.19f);
            cameraObject.transform.position = new Vector3(0f, 2.6f, -10f);

            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    Vector3 position = TileToWorld(x, y);
                    AddSprite("Ground " + x + "-" + y, ground, position, SortOrder(x, y));
                }
            }

            var random = new System.Random(PlacementSeed);
            AddRandomlyPlacedSprite(
                "Town shop",
                town,
                random,
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(0, 1));
            AddRandomlyPlacedSprite(
                "Flour mill",
                industry,
                random,
                new Vector2Int(7, 3),
                new Vector2Int(6, 3),
                new Vector2Int(7, 4));
            AddRandomlyPlacedSprite(
                "Cargo lorry",
                truck,
                random,
                new Vector2Int(0, 7),
                new Vector2Int(1, 7),
                new Vector2Int(0, 6));

            Directory.CreateDirectory(ArtRoot);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        public static void BuildWebGLPreview()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                throw new InvalidOperationException("Start Unity with -buildTarget WebGL before building the art preview.");
            }

            WebGLCompressionFormat previousCompression = PlayerSettings.WebGL.compressionFormat;
            bool previousFallback = PlayerSettings.WebGL.decompressionFallback;
            BuildReport report;
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.decompressionFallback = false;
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = "Builds/ArtPilotWebGL",
                    target = BuildTarget.WebGL,
                    options = BuildOptions.Development,
                });
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = previousCompression;
                PlayerSettings.WebGL.decompressionFallback = previousFallback;
            }

            Debug.Log($"Art pilot WebGL build: {report.summary.result}, size={report.summary.totalSize / (1024 * 1024)} MB, time={report.summary.totalTime}");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static Sprite LoadSprite(string fileName)
        {
            string path = ArtRoot + "/" + fileName;
            Sprite? sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException("Pilot sprite not found: " + path);
            }

            return sprite;
        }

        private static void AddSprite(string name, Sprite sprite, Vector3 position, int sortingOrder)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.position = position;
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
        }

        private static void AddRandomlyPlacedSprite(
            string name,
            Sprite sprite,
            System.Random random,
            params Vector2Int[] candidates)
        {
            Vector2Int cell = candidates[random.Next(candidates.Length)];
            AddSprite(name, sprite, TileToWorld(cell.x, cell.y), SortOrder(cell.x, cell.y) + 1);
        }

        private static Vector3 TileToWorld(float x, float y)
        {
            return new Vector3((x - y) * 0.5f, (x + y) * 0.25f, 0f);
        }

        private static int SortOrder(float x, float y)
        {
            return Mathf.RoundToInt(-(x + y) * 10f);
        }
    }
}
