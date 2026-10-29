#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// State and rules of the save and load panel (Issue #183): the slot list, which actions each slot offers, and the
    /// confirmation asked before a load replaces a running game. Plain C#, independent of UI Toolkit and Core.
    /// </summary>
    public sealed class SaveLoadViewModel
    {
        private IReadOnlyList<SaveSlotEntry> _slots = Array.Empty<SaveSlotEntry>();

        /// <summary>
        /// Gets the slots in display order: the autosave, then the manual slots.
        /// </summary>
        public IReadOnlyList<SaveSlotEntry> Slots => _slots;

        /// <summary>
        /// Gets whether a game is running. Saving needs one; loading asks for confirmation.
        /// </summary>
        public bool GameRunning { get; private set; }

        /// <summary>
        /// Gets the slot whose load waits for confirmation, or null.
        /// </summary>
        public string? PendingLoadId { get; private set; }

        /// <summary>
        /// Gets the last save or load outcome.
        /// </summary>
        public SaveLoadStatus Status { get; private set; }

        /// <summary>
        /// Gets a counter that changes whenever anything shown by the panel changes.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// Gets whether any slot holds a loadable save (the Continue and Load buttons need one).
        /// </summary>
        public bool HasLoadableSave
        {
            get
            {
                for (int index = 0; index < _slots.Count; index++)
                {
                    if (_slots[index].CanLoad)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Builds the full slot list from the entries found: the autosave and slots 1 to 5, in that order, with an
        /// empty entry for every slot that has no save.
        /// </summary>
        public static IReadOnlyList<SaveSlotEntry> CompleteSlots(IEnumerable<SaveSlotEntry> found)
        {
            if (found is null)
            {
                throw new ArgumentNullException(nameof(found));
            }

            var byId = new Dictionary<string, SaveSlotEntry>(StringComparer.Ordinal);
            foreach (SaveSlotEntry entry in found)
            {
                byId[entry.Id] = entry;
            }

            var result = new List<SaveSlotEntry>(SaveSlotEntry.ManualSlotCount + 1);
            result.Add(byId.TryGetValue(SaveSlotEntry.AutosaveId, out SaveSlotEntry? autosave)
                ? autosave
                : SaveSlotEntry.Empty(SaveSlotEntry.AutosaveId));
            for (int number = 1; number <= SaveSlotEntry.ManualSlotCount; number++)
            {
                string id = number.ToString(CultureInfo.InvariantCulture);
                result.Add(byId.TryGetValue(id, out SaveSlotEntry? manual) ? manual : SaveSlotEntry.Empty(id));
            }

            return result;
        }

        /// <summary>
        /// Replaces the slot list.
        /// </summary>
        public void SetSlots(IReadOnlyList<SaveSlotEntry> slots)
        {
            _slots = slots ?? throw new ArgumentNullException(nameof(slots));
            if (PendingLoadId != null && !CanLoad(PendingLoadId))
            {
                PendingLoadId = null;
            }

            Version++;
        }

        /// <summary>
        /// Records whether a game is running.
        /// </summary>
        public void SetGameRunning(bool running)
        {
            if (GameRunning == running)
            {
                return;
            }

            GameRunning = running;
            PendingLoadId = null;
            Version++;
        }

        /// <summary>
        /// Records the outcome of a save or load.
        /// </summary>
        public void SetStatus(SaveLoadStatus status)
        {
            Status = status;
            Version++;
        }

        /// <summary>
        /// Gets whether the slot offers Save: needs a running game and is never the autosave slot.
        /// </summary>
        public bool CanSave(SaveSlotEntry slot)
        {
            if (slot is null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            return GameRunning && !slot.IsAutosave;
        }

        /// <summary>
        /// Gets whether the slot with this id can be loaded.
        /// </summary>
        public bool CanLoad(string slotId)
        {
            for (int index = 0; index < _slots.Count; index++)
            {
                if (string.Equals(_slots[index].Id, slotId, StringComparison.Ordinal))
                {
                    return _slots[index].CanLoad;
                }
            }

            return false;
        }

        /// <summary>
        /// The player pressed Load on a slot. Returns true when the load should start now; while a game runs it
        /// instead asks for confirmation and returns false.
        /// </summary>
        public bool TryRequestLoad(string slotId)
        {
            if (!CanLoad(slotId))
            {
                return false;
            }

            if (GameRunning)
            {
                PendingLoadId = slotId;
                Version++;
                return false;
            }

            return true;
        }

        /// <summary>
        /// The player confirmed the pending load; returns its slot id, or null when none was pending.
        /// </summary>
        public string? ConfirmLoad()
        {
            string? slotId = PendingLoadId;
            if (slotId != null)
            {
                PendingLoadId = null;
                Version++;
            }

            return slotId;
        }

        /// <summary>
        /// The player declined the pending load.
        /// </summary>
        public void CancelLoad()
        {
            if (PendingLoadId != null)
            {
                PendingLoadId = null;
                Version++;
            }
        }

        /// <summary>
        /// Formats a slot's date, seed and map size from the localized template (date {0}, seed {1}, tiles per side {2}).
        /// </summary>
        public static string FormatSlotInfo(string template, SaveSlotEntry slot)
        {
            if (template is null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (slot is null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                template,
                slot.DateText,
                slot.Seed.ToString(CultureInfo.InvariantCulture),
                slot.MapTiles.ToString(CultureInfo.InvariantCulture));
        }
    }
}
