#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Save;

namespace SekoyaTransit.Save.Tests
{
    [TestFixture]
    public sealed class PersistentFileSaveStoreTests
    {
        private string _rootDirectory = null!;

        [SetUp]
        public void SetUp()
        {
            _rootDirectory = Path.Combine(Path.GetTempPath(), "SekoyaTransitSaveTests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, true);
            }
        }

        [Test]
        public async Task SaveLoadListAndDeleteAsync_ManualAndAutosave_OperateOnDistinctSlots()
        {
            var store = new PersistentFileSaveStore(_rootDirectory);
            SaveSlot manual = SaveSlot.Manual("campaign");
            byte[] autosaveData = { 1, 2, 3 };
            byte[] manualData = { 4, 5, 6 };

            await store.SaveAsync(SaveSlot.Autosave, autosaveData);
            await store.SaveAsync(manual, manualData);

            CollectionAssert.AreEqual(autosaveData, await store.LoadAsync(SaveSlot.Autosave));
            CollectionAssert.AreEqual(manualData, await store.LoadAsync(manual));
            CollectionAssert.AreEquivalent(new[] { SaveSlot.Autosave, manual }, await store.ListAsync());

            await store.DeleteAsync(manual);

            CollectionAssert.AreEqual(new[] { SaveSlot.Autosave }, await store.ListAsync());
            Assert.That(File.Exists(Path.Combine(_rootDirectory, "manual-campaign.sav")), Is.False);
        }

        [Test]
        public async Task SaveAsync_Overwrite_ReplacesCompletePayloadAndRemovesTemporaryFile()
        {
            var store = new PersistentFileSaveStore(_rootDirectory);
            SaveSlot slot = SaveSlot.Manual("campaign");

            await store.SaveAsync(slot, new byte[] { 1, 2, 3 });
            await store.SaveAsync(slot, new byte[] { 7, 8, 9, 10 });

            CollectionAssert.AreEqual(new byte[] { 7, 8, 9, 10 }, await store.LoadAsync(slot));
            Assert.That(Directory.GetFiles(_rootDirectory, "*.tmp"), Is.Empty);
        }

        [Test]
        public void LoadAndDeleteAsync_MissingSlot_ThrowExplicitNotFoundException()
        {
            var store = new PersistentFileSaveStore(_rootDirectory);
            SaveSlot slot = SaveSlot.Manual("missing");

            Assert.ThrowsAsync<SaveSlotNotFoundException>(() => store.LoadAsync(slot));
            Assert.ThrowsAsync<SaveSlotNotFoundException>(() => store.DeleteAsync(slot));
        }

        [Test]
        public async Task SaveAndDeleteAsync_WithSynchronizer_SynchronizeAfterMutations()
        {
            var synchronizer = new RecordingSynchronizer();
            var store = new PersistentFileSaveStore(_rootDirectory, synchronizer);
            SaveSlot slot = SaveSlot.Manual("campaign");

            await store.SaveAsync(slot, new byte[] { 1 });
            await store.DeleteAsync(slot);

            Assert.That(synchronizer.SynchronizationCount, Is.EqualTo(2));
        }

        [Test]
        public async Task SaveAsync_WaitsForFilesystemSynchronization()
        {
            var synchronizer = new DeferredSynchronizer();
            var store = new PersistentFileSaveStore(_rootDirectory, synchronizer);
            Task saving = store.SaveAsync(SaveSlot.Autosave, new byte[] { 1 });

            await synchronizer.SynchronizationStarted;

            Assert.That(saving.IsCompleted, Is.False);
            synchronizer.Complete();
            await saving;
        }

        [Test]
        public void SaveAsync_SynchronizationFails_PropagatesFailure()
        {
            var store = new PersistentFileSaveStore(_rootDirectory, new FailingSynchronizer());

            Assert.ThrowsAsync<IOException>(() => store.SaveAsync(SaveSlot.Autosave, new byte[] { 1 }));
        }

        [Test]
        public void ListAsync_InvalidSaveFilename_FailsExplicitly()
        {
            Directory.CreateDirectory(_rootDirectory);
            File.WriteAllBytes(Path.Combine(_rootDirectory, "invalid.sav"), new byte[] { 1 });
            var store = new PersistentFileSaveStore(_rootDirectory);

            Assert.ThrowsAsync<InvalidDataException>(() => store.ListAsync());
        }

        private sealed class RecordingSynchronizer : IFileSystemSynchronizer
        {
            public int SynchronizationCount { get; private set; }

            public Task SynchronizeAsync()
            {
                SynchronizationCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class FailingSynchronizer : IFileSystemSynchronizer
        {
            public Task SynchronizeAsync()
            {
                throw new IOException("Expected synchronization failure.");
            }
        }

        private sealed class DeferredSynchronizer : IFileSystemSynchronizer
        {
            private readonly TaskCompletionSource<bool> _started = new TaskCompletionSource<bool>();
            private readonly TaskCompletionSource<bool> _completion = new TaskCompletionSource<bool>();

            public Task SynchronizationStarted
            {
                get
                {
                    return _started.Task;
                }
            }

            public Task SynchronizeAsync()
            {
                _started.TrySetResult(true);
                return _completion.Task;
            }

            public void Complete()
            {
                _completion.TrySetResult(true);
            }
        }
    }
}
