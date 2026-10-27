#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.UI.Tests.Shell
{
    public sealed class SaveLoadViewModelTests
    {
        private static IReadOnlyList<SaveSlotEntry> SlotsWith(params SaveSlotEntry[] found)
        {
            return SaveLoadViewModel.CompleteSlots(found);
        }

        private static SaveSlotEntry Filled(string id)
        {
            return SaveSlotEntry.Filled(id, "1905-03-01", 42UL, 256);
        }

        [Test]
        public void CompleteSlots_NothingFound_GivesTheAutosaveAndFiveEmptyManualSlots()
        {
            IReadOnlyList<SaveSlotEntry> slots = SaveLoadViewModel.CompleteSlots(new SaveSlotEntry[0]);

            Assert.That(slots.Count, Is.EqualTo(6));
            Assert.That(slots[0].Id, Is.EqualTo(SaveSlotEntry.AutosaveId));
            Assert.That(slots[1].Id, Is.EqualTo("1"));
            Assert.That(slots[5].Id, Is.EqualTo("5"));
            for (int index = 0; index < slots.Count; index++)
            {
                Assert.That(slots[index].IsEmpty, Is.True);
            }
        }

        [Test]
        public void CompleteSlots_FoundOutOfOrder_KeepsTheFixedOrderAndTheFoundEntries()
        {
            IReadOnlyList<SaveSlotEntry> slots = SlotsWith(Filled("3"), Filled(SaveSlotEntry.AutosaveId));

            Assert.That(slots[0].IsEmpty, Is.False);
            Assert.That(slots[1].IsEmpty, Is.True);
            Assert.That(slots[3].IsEmpty, Is.False);
            Assert.That(slots[3].Seed, Is.EqualTo(42UL));
        }

        [Test]
        public void HasLoadableSave_OnlyEmptyAndUnreadableSlots_IsFalse()
        {
            var model = new SaveLoadViewModel();
            model.SetSlots(SlotsWith(SaveSlotEntry.Unreadable("2")));

            Assert.That(model.HasLoadableSave, Is.False);
        }

        [Test]
        public void HasLoadableSave_AFilledSlot_IsTrue()
        {
            var model = new SaveLoadViewModel();
            model.SetSlots(SlotsWith(Filled("4")));

            Assert.That(model.HasLoadableSave, Is.True);
        }

        [Test]
        public void CanSave_NoRunningGame_IsFalseForEveryManualSlot()
        {
            var model = new SaveLoadViewModel();
            model.SetSlots(SlotsWith());

            Assert.That(model.CanSave(model.Slots[1]), Is.False);
        }

        [Test]
        public void CanSave_RunningGame_IsTrueForManualSlotsButNeverTheAutosave()
        {
            var model = new SaveLoadViewModel();
            model.SetSlots(SlotsWith());
            model.SetGameRunning(true);

            Assert.That(model.CanSave(model.Slots[0]), Is.False);
            Assert.That(model.CanSave(model.Slots[1]), Is.True);
        }

        [Test]
        public void TryRequestLoad_EmptySlot_DoesNothing()
        {
            var model = new SaveLoadViewModel();
            model.SetSlots(SlotsWith());

            Assert.That(model.TryRequestLoad("1"), Is.False);
            Assert.That(model.PendingLoadId, Is.Null);
        }

        [Test]
        public void TryRequestLoad_NoRunningGame_LoadsAtOnce()
        {
            var model = new SaveLoadViewModel();
            model.SetSlots(SlotsWith(Filled("1")));

            Assert.That(model.TryRequestLoad("1"), Is.True);
            Assert.That(model.PendingLoadId, Is.Null);
        }

        [Test]
        public void TryRequestLoad_RunningGame_AsksForConfirmationInsteadOfLoading()
        {
            var model = new SaveLoadViewModel();
            model.SetSlots(SlotsWith(Filled("1")));
            model.SetGameRunning(true);

            Assert.That(model.TryRequestLoad("1"), Is.False);
            Assert.That(model.PendingLoadId, Is.EqualTo("1"));
        }

        [Test]
        public void ConfirmLoad_PendingLoad_ReturnsTheSlotOnceAndClearsIt()
        {
            var model = new SaveLoadViewModel();
            model.SetSlots(SlotsWith(Filled("1")));
            model.SetGameRunning(true);
            model.TryRequestLoad("1");

            Assert.That(model.ConfirmLoad(), Is.EqualTo("1"));
            Assert.That(model.ConfirmLoad(), Is.Null);
        }

        [Test]
        public void CancelLoad_PendingLoad_ClearsIt()
        {
            var model = new SaveLoadViewModel();
            model.SetSlots(SlotsWith(Filled("1")));
            model.SetGameRunning(true);
            model.TryRequestLoad("1");

            model.CancelLoad();

            Assert.That(model.PendingLoadId, Is.Null);
        }

        [Test]
        public void SetSlots_PendingSlotNoLongerLoadable_DropsTheConfirmation()
        {
            var model = new SaveLoadViewModel();
            model.SetSlots(SlotsWith(Filled("1")));
            model.SetGameRunning(true);
            model.TryRequestLoad("1");

            model.SetSlots(SlotsWith());

            Assert.That(model.PendingLoadId, Is.Null);
        }

        [Test]
        public void Version_ChangesWhenAnythingShownChanges()
        {
            var model = new SaveLoadViewModel();
            int start = model.Version;

            model.SetSlots(SlotsWith());
            int afterSlots = model.Version;
            model.SetStatus(SaveLoadStatus.Saved);

            Assert.That(afterSlots, Is.GreaterThan(start));
            Assert.That(model.Version, Is.GreaterThan(afterSlots));
        }

        [Test]
        public void FormatSlotInfo_FillsDateSeedAndMapTiles()
        {
            string text = SaveLoadViewModel.FormatSlotInfo("{0} · Seed {1} · {2}×{2}", Filled("1"));

            Assert.That(text, Is.EqualTo("1905-03-01 · Seed 42 · 256×256"));
        }
    }
}
