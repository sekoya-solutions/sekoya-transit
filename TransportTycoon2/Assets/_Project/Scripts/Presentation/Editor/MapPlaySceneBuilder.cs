#nullable enable
using System.IO;
using TransportTycoon.Presentation.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TransportTycoon.Presentation.Editor
{
    /// <summary>
    /// Creates the production MapPlay scene under _Project (not spike or art-pilot scenes).
    /// </summary>
    public static class MapPlaySceneBuilder
    {
        private const string SceneDirectory = "Assets/_Project/Scenes";
        private const string ScenePath = SceneDirectory + "/MapPlay.unity";

        [MenuItem("Transport Tycoon/Build Map Play Scene")]
        public static void BuildFromMenu()
        {
            Build();
            Debug.Log("MapPlay scene written to " + ScenePath);
        }

        /// <summary>
        /// Batchmode entry point: Unity -batchmode -quit -executeMethod TransportTycoon.Presentation.Editor.MapPlaySceneBuilder.BuildFromBatch
        /// </summary>
        public static void BuildFromBatch()
        {
            Build();
        }

        private static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            {
                throw new InvalidDataException("Assets/_Project is missing.");
            }

            if (!AssetDatabase.IsValidFolder(SceneDirectory))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sessionObject = new GameObject("MapPlaySession");
            sessionObject.AddComponent<MapPlaySession>();

            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 48f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.16f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<AudioListener>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }
    }
}
