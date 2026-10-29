#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SekoyaTransit.Core.Save;
using UnityEngine;

namespace SekoyaTransit.Save
{
    /// <summary>
    /// Stores save slots in a supplied directory.
    /// </summary>
    public sealed class PersistentFileSaveStore : ISaveStore
    {
        private readonly string _rootDirectory;
        private readonly IFileSystemSynchronizer? _fileSystemSynchronizer;
        private readonly SemaphoreSlim _operationGate = new SemaphoreSlim(1, 1);

        public PersistentFileSaveStore(string rootDirectory, IFileSystemSynchronizer? fileSystemSynchronizer = null)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("A save directory is required.", nameof(rootDirectory));
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            if (fileSystemSynchronizer == null)
            {
                throw new ArgumentNullException(
                    nameof(fileSystemSynchronizer),
                    "WebGL saves require explicit filesystem synchronization.");
            }
#endif

            _rootDirectory = Path.GetFullPath(rootDirectory);
            _fileSystemSynchronizer = fileSystemSynchronizer;
        }

        /// <summary>
        /// Creates the platform store rooted at Unity's persistent data path.
        /// </summary>
        public static PersistentFileSaveStore CreateDefault()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new PersistentFileSaveStore(
                Application.persistentDataPath,
                WebGlFileSystemSynchronizer.Create());
#else
            return new PersistentFileSaveStore(Application.persistentDataPath);
#endif
        }

        public Task SaveAsync(SaveSlot slot, byte[] compressedData)
        {
            ValidateSlot(slot);
            if (compressedData == null)
            {
                throw new ArgumentNullException(nameof(compressedData));
            }

            if (compressedData.Length == 0)
            {
                throw new ArgumentException("A save payload cannot be empty.", nameof(compressedData));
            }

            byte[] dataCopy = (byte[])compressedData.Clone();
            return RunOperationAsync(() => SaveCoreAsync(slot, dataCopy));
        }

        public Task<byte[]> LoadAsync(SaveSlot slot)
        {
            ValidateSlot(slot);
            return RunOperationAsync(() => LoadCoreAsync(slot));
        }

        public Task<IReadOnlyList<SaveSlot>> ListAsync()
        {
            return RunOperationAsync(ListCoreAsync);
        }

        public Task DeleteAsync(SaveSlot slot)
        {
            ValidateSlot(slot);
            return RunOperationAsync(() => DeleteCoreAsync(slot));
        }

        private Task RunOperationAsync(Func<Task> operation)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return operation();
#else
            return Task.Run(operation);
#endif
        }

        private Task<T> RunOperationAsync<T>(Func<Task<T>> operation)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return operation();
#else
            return Task.Run(operation);
#endif
        }

        private async Task SaveCoreAsync(SaveSlot slot, byte[] compressedData)
        {
            await _operationGate.WaitAsync();
            try
            {
                Directory.CreateDirectory(_rootDirectory);
                string destination = GetPath(slot);
                string temporary = Path.Combine(
                    _rootDirectory,
                    "." + slot.StorageKey + "." + Guid.NewGuid().ToString("N") + ".tmp");

                try
                {
                    using (var stream = new FileStream(
                        temporary,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None))
                    {
                        stream.Write(compressedData, 0, compressedData.Length);
                        stream.Flush(true);
                    }

                    ReplaceFile(temporary, destination);
                    await SynchronizeFileSystemAsync();
                }
                finally
                {
                    if (File.Exists(temporary))
                    {
                        File.Delete(temporary);
                    }
                }
            }
            finally
            {
                _operationGate.Release();
            }
        }

        private async Task<byte[]> LoadCoreAsync(SaveSlot slot)
        {
            await _operationGate.WaitAsync();
            try
            {
                string path = GetPath(slot);
                if (!File.Exists(path))
                {
                    throw new SaveSlotNotFoundException(slot);
                }

                return File.ReadAllBytes(path);
            }
            finally
            {
                _operationGate.Release();
            }
        }

        private async Task<IReadOnlyList<SaveSlot>> ListCoreAsync()
        {
            await _operationGate.WaitAsync();
            try
            {
                if (!Directory.Exists(_rootDirectory))
                {
                    return Array.Empty<SaveSlot>();
                }

                var slots = new List<SaveSlot>();
                foreach (string path in Directory.EnumerateFiles(_rootDirectory, "*.sav"))
                {
                    string storageKey = Path.GetFileNameWithoutExtension(path);
                    if (!SaveSlot.TryParseStorageKey(storageKey, out SaveSlot slot))
                    {
                        throw new InvalidDataException($"Save file '{Path.GetFileName(path)}' has an invalid slot name.");
                    }

                    slots.Add(slot);
                }

                slots.Sort((left, right) =>
                    StringComparer.Ordinal.Compare(left.StorageKey, right.StorageKey));
                return slots.AsReadOnly();
            }
            finally
            {
                _operationGate.Release();
            }
        }

        private async Task DeleteCoreAsync(SaveSlot slot)
        {
            await _operationGate.WaitAsync();
            try
            {
                string path = GetPath(slot);
                if (!File.Exists(path))
                {
                    throw new SaveSlotNotFoundException(slot);
                }

                File.Delete(path);
                await SynchronizeFileSystemAsync();
            }
            finally
            {
                _operationGate.Release();
            }
        }

        private async Task SynchronizeFileSystemAsync()
        {
            if (_fileSystemSynchronizer != null)
            {
                await _fileSystemSynchronizer.SynchronizeAsync();
            }
        }

        private string GetPath(SaveSlot slot)
        {
            return Path.Combine(_rootDirectory, slot.StorageKey + ".sav");
        }

        private static void ReplaceFile(string temporary, string destination)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            File.Move(temporary, destination);
#else
            if (File.Exists(destination))
            {
                File.Replace(temporary, destination, null);
            }
            else
            {
                File.Move(temporary, destination);
            }
#endif
        }

        private static void ValidateSlot(SaveSlot slot)
        {
            if (!SaveSlot.TryParseStorageKey(slot.StorageKey, out SaveSlot parsed) || parsed != slot)
            {
                throw new ArgumentException("The save slot is not valid.", nameof(slot));
            }
        }
    }
}
