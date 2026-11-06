#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace SekoyaTransit.Save
{
    /// <summary>
    /// Browser download and file picker of WebGL players (Issue #320), through <c>SekoyaTransitSaveFile.jslib</c>. The
    /// picker answers through <c>SendMessage</c> on this object; <see cref="SaveFilePickRequests"/> matches each answer
    /// to its request. Outside WebGL players it is unavailable.
    /// </summary>
    public sealed class WebGlSaveFileTransfer : MonoBehaviour, ISaveFileTransfer
    {
        /// <summary>
        /// Largest file Load from file accepts: far above any save, and small enough for the Base64 copy to fit the
        /// WebGL heap.
        /// </summary>
        public const int MaxFileBytes = 64 * 1024 * 1024;

        private const string GameObjectName = "SekoyaTransitSaveFileTransfer";

        private static WebGlSaveFileTransfer? _instance;
        private readonly SaveFilePickRequests _picks = new SaveFilePickRequests();

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int SekoyaTransitDownloadFile(string fileName, byte[] data, int length);

        [DllImport("__Internal")]
        private static extern void SekoyaTransitOpenFile(string gameObjectName, int requestId, int maxBytes);
#endif

        /// <inheritdoc />
        public bool IsAvailable
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Gets or creates the persistent receiver of the browser's answers.
        /// </summary>
        public static WebGlSaveFileTransfer Create()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var gameObject = new GameObject(GameObjectName);
            DontDestroyOnLoad(gameObject);
            _instance = gameObject.AddComponent<WebGlSaveFileTransfer>();
            return _instance;
        }

        /// <inheritdoc />
        public void Download(string fileName, byte[] data)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("A file name is required.", nameof(fileName));
            }

            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("A download needs data.", nameof(data));
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            if (SekoyaTransitDownloadFile(fileName, data, data.Length) == 0)
            {
                throw new IOException("The browser refused the download of " + fileName + ".");
            }
#else
            throw new NotSupportedException("Save file download exists only in WebGL players.");
#endif
        }

        /// <inheritdoc />
        public Task<byte[]?> PickFileAsync()
        {
            int requestId = _picks.Begin(out Task<byte[]?> result);
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                SekoyaTransitOpenFile(GameObjectName, requestId, MaxFileBytes);
            }
            catch (Exception exception)
            {
                _picks.Fail(requestId, new IOException("Could not open the browser file picker.", exception));
            }
#else
            _picks.Fail(requestId, new NotSupportedException("Load from file exists only in WebGL players."));
#endif
            return result;
        }

        /// <summary>
        /// Browser answer: the chosen file, as "request id|Base64".
        /// </summary>
        public void OnFilePicked(string message)
        {
            _picks.OnPicked(message);
        }

        /// <summary>
        /// Browser answer: the player closed the picker without a file, as "request id|".
        /// </summary>
        public void OnFileCancelled(string message)
        {
            _picks.OnCancelled(message);
        }

        /// <summary>
        /// Browser answer: the picker or the file read failed, as "request id|reason".
        /// </summary>
        public void OnFileFailed(string message)
        {
            _picks.OnFailed(message);
        }
    }
}
