#nullable enable
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace TransportTycoon.Spikes.WebGL.Editor
{
    // Adds the Localization package through Unity's Package Manager so Unity itself updates the manifest.
    public static class SpikePackageInstaller
    {
        private static AddRequest? _request;

        public static void AddLocalization()
        {
            _request = Client.Add("com.unity.localization");
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (_request == null || !_request.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= Poll;
            if (_request.Status == StatusCode.Success)
            {
                Debug.Log("Installed " + _request.Result.packageId);
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("Package install failed: " + _request.Error.message);
                EditorApplication.Exit(1);
            }
        }
    }
}
