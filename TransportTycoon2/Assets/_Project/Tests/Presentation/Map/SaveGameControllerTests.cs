#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;
using TransportTycoon.Presentation.Map;
using TransportTycoon.Save;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.Presentation.Tests.Map
{
    public sealed class SaveGameControllerTests
    {
        private string _directory = string.Empty;
        private string? _lastSlot;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "tt2-save-controller-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void ToSlot_AutosaveId_GivesTheAutosaveSlot()
        {
            Assert.That(SaveGameController.ToSlot(SaveSlotEntry.AutosaveId).IsAutosave, Is.True);
        }

        [TestCase("1")]
        [TestCase("5")]
        public void ToSlot_ManualNumber_RoundTripsThroughToSlotId(string slotId)
        {
            SaveSlot slot = SaveGameController.ToSlot(slotId);

            Assert.That(slot.IsAutosave, Is.False);
            Assert.That(SaveGameController.ToSlotId(slot), Is.EqualTo(slotId));
        }

        [Test]
        public void ToSlotId_ManualSlotOutsideTheFivePanelSlots_HasNoRow()
        {
            Assert.That(SaveGameController.ToSlotId(SaveSlot.Manual("6")), Is.Null);
            Assert.That(SaveGameController.ToSlotId(SaveSlot.Manual("custom")), Is.Null);
        }

        [Test]
        public void FindContinueSlotId_LastWrittenSlotIsLoadable_PicksIt()
        {
            IReadOnlyList<SaveSlotEntry> slots = SaveLoadViewModel.CompleteSlots(new[]
            {
                Filled(SaveSlotEntry.AutosaveId),
                Filled("2"),
                Filled("4")
            });

            Assert.That(SaveGameController.FindContinueSlotId(slots, "4"), Is.EqualTo("4"));
        }

        [Test]
        public void FindContinueSlotId_LastWrittenSlotIsGone_FallsBackToTheAutosave()
        {
            IReadOnlyList<SaveSlotEntry> slots = SaveLoadViewModel.CompleteSlots(new[]
            {
                Filled(SaveSlotEntry.AutosaveId),
                Filled("2")
            });

            Assert.That(SaveGameController.FindContinueSlotId(slots, "4"), Is.EqualTo(SaveSlotEntry.AutosaveId));
        }

        [Test]
        public void FindContinueSlotId_NoAutosave_PicksTheFirstLoadableSlot()
        {
            IReadOnlyList<SaveSlotEntry> slots = SaveLoadViewModel.CompleteSlots(new[]
            {
                SaveSlotEntry.Unreadable("1"),
                Filled("3"),
                Filled("5")
            });

            Assert.That(SaveGameController.FindContinueSlotId(slots, null), Is.EqualTo("3"));
        }

        [Test]
        public void FindContinueSlotId_NothingLoadable_IsNull()
        {
            IReadOnlyList<SaveSlotEntry> slots = SaveLoadViewModel.CompleteSlots(new[] { SaveSlotEntry.Unreadable("1") });

            Assert.That(SaveGameController.FindContinueSlotId(slots, "1"), Is.Null);
        }

        [TestCase(MapSize.Small, NewGameMapSize.Small)]
        [TestCase(MapSize.Medium, NewGameMapSize.Medium)]
        [TestCase(MapSize.Large, NewGameMapSize.Large)]
        public void ToUiMapSize_IsTheReverseOfToCoreMapSize(MapSize size, NewGameMapSize expected)
        {
            Assert.That(MapPlaySession.ToUiMapSize(size), Is.EqualTo(expected));
            Assert.That(MapPlaySession.ToCoreMapSize(expected), Is.EqualTo(size));
        }

        [Test]
        public async Task SaveAsync_ThenListEntries_ShowsTheSlotWithItsDateSeedAndMapSize()
        {
            SaveGameController controller = CreateController();

            await controller.SaveAsync("2", CreateSnapshot(777UL, new Tick(40)));
            IReadOnlyList<SaveSlotEntry> entries = await controller.ListEntriesAsync();

            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].Id, Is.EqualTo("2"));
            Assert.That(entries[0].Seed, Is.EqualTo(777UL));
            Assert.That(entries[0].MapTiles, Is.EqualTo(128));
            Assert.That(entries[0].DateText, Does.StartWith("1900-"));
            Assert.That(controller.GetLastSlotId(), Is.EqualTo("2"));
        }

        [Test]
        public async Task ListEntries_UnreadableFile_ShowsAnUnreadableEntryInsteadOfFailing()
        {
            SaveGameController controller = CreateController();
            await controller.SaveAsync("1", CreateSnapshot(5UL, Tick.Zero));
            string[] files = Directory.GetFiles(_directory);
            Assert.That(files.Length, Is.EqualTo(1));
            File.WriteAllBytes(files[0], new byte[] { 1, 2, 3 });

            IReadOnlyList<SaveSlotEntry> entries = await controller.ListEntriesAsync();

            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].IsUnreadable, Is.True);
        }

        [Test]
        public async Task LoadAsync_SavedSlot_ReturnsTheSnapshot()
        {
            SaveGameController controller = CreateController();
            await controller.SaveAsync(SaveSlotEntry.AutosaveId, CreateSnapshot(99UL, new Tick(7)));

            SaveSnapshot loaded = await controller.LoadAsync(SaveSlotEntry.AutosaveId);

            Assert.That(loaded.MapSeed, Is.EqualTo(99UL));
            Assert.That(loaded.CurrentTick, Is.EqualTo(new Tick(7)));
        }

        private SaveGameController CreateController()
        {
            return new SaveGameController(
                new PersistentFileSaveStore(_directory),
                new JsonGzipSaveSerializer(CurrentSaveMigrations.CreatePipeline()),
                new SimulationCalendar(TimeSpan.FromMilliseconds(50)),
                () => _lastSlot,
                slotId => _lastSlot = slotId);
        }

        private static SaveSlotEntry Filled(string id)
        {
            return SaveSlotEntry.Filled(id, "1900-01-01", 1UL, 128);
        }

        private static SaveSnapshot CreateSnapshot(ulong seed, Tick tick)
        {
            int tiles = 128 * 128;
            var terrain = new int[tiles];
            var objects = new int[tiles];
            var owners = new int[tiles];
            for (int index = 0; index < tiles; index++)
            {
                objects[index] = TileMap.NoObjectId;
                owners[index] = TileMap.UnownedId;
            }

            return new SaveSnapshot(
                SaveSnapshot.CurrentFormatVersion,
                seed,
                tick,
                MapSize.Small,
                terrain,
                objects,
                owners,
                new int[tiles],
                new int[tiles],
                Array.Empty<TileCoordinate>(),
                Array.Empty<PlacedIndustry>(),
                Array.Empty<RoadStation>(),
                Array.Empty<RoadVehicle>(),
                1000L,
                Array.Empty<RailStation>(),
                Array.Empty<RailVehicle>(),
                Array.Empty<Town>());
        }
    }
}
