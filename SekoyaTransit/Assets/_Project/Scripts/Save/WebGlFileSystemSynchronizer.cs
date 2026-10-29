#nullable enable
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace SekoyaTransit.Save
{
    /// <summary>
    /// Awaits synchronization of Unity WebGL's IndexedDB-backed virtual filesystem.
    /// </summary>
    public sealed class WebGlFileSystemSynchronizer : MonoBehaviour, IFileSystemSynchronizer
    {
        private const string GameObjectName = "SekoyaTransitSaveFileSystemSync";

        private static WebGlFileSystemSynchronizer? _instance;
        private TaskCompletionSource<bool>? _pendingSynchronization;

        [DllImport("__Internal")]
        private static extern void SekoyaTransitSyncFileSystem(string gameObjectName);

        /// <summary>
        /// Gets or creates the persistent callback receiver used by the WebGL bridge.
        /// </summary>
        public static WebGlFileSystemSynchronizer Create()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var gameObject = new GameObject(GameObjectName);
            DontDestroyOnLoad(gameObject);
            _instance = gameObject.AddComponent<WebGlFileSystemSynchronizer>();
            return _instance;
        }

        public Task SynchronizeAsync()
        {
            if (_pendingSynchronization != null)
            {
                throw new InvalidOperationException("A filesystem synchronization is already in progress.");
            }

            var completion = new TaskCompletionSource<bool>();
            _pendingSynchronization = completion;
            try
            {
                SekoyaTransitSyncFileSystem(GameObjectName);
            }
            catch (Exception exception)
            {
                _pendingSynchronization = null;
                completion.SetException(new IOException("Could not start WebGL filesystem synchronization.", exception));
            }

            return completion.Task;
        }

        public void OnSynchronizationSucceeded(string unused)
        {
            TaskCompletionSource<bool>? completion = _pendingSynchronization;
            _pendingSynchronization = null;
            completion?.TrySetResult(true);
        }

        public void OnSynchronizationFailed(string error)
        {
            TaskCompletionSource<bool>? completion = _pendingSynchronization;
            _pendingSynchronization = null;
            completion?.TrySetException(
                new IOException("WebGL filesystem synchronization failed: " + error));
        }
    }
}
