#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Save
{
    [TestFixture]
    public sealed class SaveServiceTests
    {
        private static readonly byte[] _serializedPayload = { 1, 2, 3 };
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        [Test]
        public async Task SaveAsync_ManualSlot_SerializesAndStoresPayload()
        {
            SaveSnapshot snapshot = CreateSnapshot();
            var serializer = new FakeSerializer(snapshot);
            var store = new FakeStore();
            var service = new SaveService(serializer, store);
            SaveSlot slot = SaveSlot.Manual("campaign");

            await service.SaveAsync(slot, snapshot);

            Assert.That(serializer.SerializedSnapshot, Is.SameAs(snapshot));
            Assert.That(store.SavedSlot, Is.EqualTo(slot));
            CollectionAssert.AreEqual(_serializedPayload, store.SavedData);
        }

        [Test]
        public async Task LoadAsync_AutosaveSlot_DeserializesStoredPayload()
        {
            SaveSnapshot snapshot = CreateSnapshot();
            var serializer = new FakeSerializer(snapshot);
            var store = new FakeStore { SavedData = (byte[])_serializedPayload.Clone() };
            var service = new SaveService(serializer, store);

            SaveSnapshot loaded = await service.LoadAsync(SaveSlot.Autosave);

            Assert.That(store.LoadedSlot, Is.EqualTo(SaveSlot.Autosave));
            CollectionAssert.AreEqual(_serializedPayload, serializer.DeserializedPayload);
            Assert.That(loaded, Is.SameAs(snapshot));
        }

        [Test]
        public void Constructor_NullSerializer_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SaveService(null!, new FakeStore()));
        }

        private static SaveSnapshot CreateSnapshot()
        {
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 1),
                StationAdjacency.MinimumSiteSeparationChebyshev,
                new[]
                {
                    new IndustryPlacementRule(
                        "forest",
                        1,
                        Array.Empty<string>(),
                        _forestOutputCargoIds,
                        1,
                        0)
                });
            GeneratedWorld world = WorldGenerator.Generate(settings, 123UL);
            return SaveSnapshot.Capture(123UL, new Tick(12), world);
        }

        private sealed class FakeSerializer : ISaveSerializer
        {
            private readonly SaveSnapshot _snapshot;

            public FakeSerializer(SaveSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public SaveSnapshot? SerializedSnapshot { get; private set; }

            public byte[]? DeserializedPayload { get; private set; }

            public byte[] Serialize(SaveSnapshot snapshot)
            {
                SerializedSnapshot = snapshot;
                return (byte[])_serializedPayload.Clone();
            }

            public SaveSnapshot Deserialize(byte[] compressedData)
            {
                DeserializedPayload = (byte[])compressedData.Clone();
                return _snapshot;
            }
        }

        private sealed class FakeStore : ISaveStore
        {
            public SaveSlot? SavedSlot { get; private set; }

            public SaveSlot? LoadedSlot { get; private set; }

            public byte[] SavedData { get; set; } = Array.Empty<byte>();

            public Task SaveAsync(SaveSlot slot, byte[] compressedData)
            {
                SavedSlot = slot;
                SavedData = (byte[])compressedData.Clone();
                return Task.CompletedTask;
            }

            public Task<byte[]> LoadAsync(SaveSlot slot)
            {
                LoadedSlot = slot;
                return Task.FromResult((byte[])SavedData.Clone());
            }

            public Task<IReadOnlyList<SaveSlot>> ListAsync()
            {
                return Task.FromResult<IReadOnlyList<SaveSlot>>(Array.Empty<SaveSlot>());
            }

            public Task DeleteAsync(SaveSlot slot)
            {
                return Task.CompletedTask;
            }
        }
    }
}
