#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using TransportTycoon.Core.Save;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Save;
using TransportTycoon.UI.Shell;
using UnityEngine;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Connects the save and load panel to the save storage (Issue #183): maps the panel's slot ids to save slots, saves
    /// and loads through <see cref="SaveService"/>, builds the slot list, and remembers the last slot written so the
    /// Continue button knows the most recent save.
    /// </summary>
    public sealed class SaveGameController
    {
        private const string LastSlotPreference = "TransportTycoon.LastSaveSlot";
        private const string ManualPrefix = "manual-";

        private readonly SaveService _service;
        private readonly ISaveStore _store;
        private readonly SimulationCalendar _calendar;
        private readonly Func<string?> _readLastSlot;
        private readonly Action<string> _writeLastSlot;

        /// <summary>
        /// Initializes the controller over a store and serializer; the calendar turns a save tick into a date. The last
        /// slot written is kept in the player preferences unless the two functions are supplied (tests).
        /// </summary>
        public SaveGameController(
            ISaveStore store,
            ISaveSerializer serializer,
            SimulationCalendar calendar,
            Func<string?>? readLastSlot = null,
            Action<string>? writeLastSlot = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _service = new SaveService(serializer ?? throw new ArgumentNullException(nameof(serializer)), store);
            _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
            _readLastSlot = readLastSlot ?? ReadLastSlotPreference;
            _writeLastSlot = writeLastSlot ?? WriteLastSlotPreference;
        }

        /// <summary>
        /// Creates the controller over the platform store (persistent data path; IndexedDB-backed on WebGL).
        /// </summary>
        public static SaveGameController CreateDefault(SimulationCalendar calendar)
        {
            return new SaveGameController(
                PersistentFileSaveStore.CreateDefault(),
                new JsonGzipSaveSerializer(CurrentSaveMigrations.CreatePipeline()),
                calendar);
        }

        /// <summary>
        /// Maps a panel slot id (the autosave id or a manual slot number) to a save slot.
        /// </summary>
        public static SaveSlot ToSlot(string slotId)
        {
            if (string.Equals(slotId, SaveSlotEntry.AutosaveId, StringComparison.Ordinal))
            {
                return SaveSlot.Autosave;
            }

            return SaveSlot.Manual(slotId);
        }

        /// <summary>
        /// Maps a save slot back to its panel slot id, or null when the panel has no row for it.
        /// </summary>
        public static string? ToSlotId(SaveSlot slot)
        {
            if (slot.IsAutosave)
            {
                return SaveSlotEntry.AutosaveId;
            }

            if (!slot.StorageKey.StartsWith(ManualPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            string number = slot.StorageKey.Substring(ManualPrefix.Length);
            for (int index = 1; index <= SaveSlotEntry.ManualSlotCount; index++)
            {
                if (string.Equals(number, index.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                {
                    return number;
                }
            }

            return null;
        }

        /// <summary>
        /// Runs work off the main thread where threads exist (not on WebGL), so serializing or reading a large map does
        /// not stall the frame.
        /// </summary>
        public static Task<T> RunInBackground<T>(Func<T> work)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Task.FromResult(work());
#else
            return Task.Run(work);
#endif
        }

        /// <summary>
        /// Serializes and stores a snapshot in the slot, then remembers the slot as the most recent save.
        /// </summary>
        public async Task SaveAsync(string slotId, SaveSnapshot snapshot)
        {
            SaveSlot slot = ToSlot(slotId);
#if UNITY_WEBGL && !UNITY_EDITOR
            await _service.SaveAsync(slot, snapshot);
#else
            await Task.Run(() => _service.SaveAsync(slot, snapshot));
#endif
            RememberLastSlot(slotId);
        }

        /// <summary>
        /// Reads and migrates the snapshot in the slot.
        /// </summary>
        public Task<SaveSnapshot> LoadAsync(string slotId)
        {
            SaveSlot slot = ToSlot(slotId);
#if UNITY_WEBGL && !UNITY_EDITOR
            return _service.LoadAsync(slot);
#else
            return Task.Run(() => _service.LoadAsync(slot));
#endif
        }

        /// <summary>
        /// Lists the autosave and the manual slots that hold a file; each is read to show its date, seed and map size,
        /// and a file that cannot be read becomes an unreadable entry. Slots without a file are left out.
        /// </summary>
        public async Task<IReadOnlyList<SaveSlotEntry>> ListEntriesAsync()
        {
            IReadOnlyList<SaveSlot> slots = await _store.ListAsync();
            var entries = new List<SaveSlotEntry>(slots.Count);
            for (int index = 0; index < slots.Count; index++)
            {
                string? slotId = ToSlotId(slots[index]);
                if (slotId == null)
                {
                    continue;
                }

                try
                {
                    SaveSnapshot snapshot = await LoadAsync(slotId);
                    entries.Add(SaveSlotEntry.Filled(
                        slotId,
                        _calendar.GetDate(snapshot.CurrentTick).ToString(),
                        snapshot.MapSeed,
                        (int)snapshot.MapSize));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Save slot " + slotId + " could not be read: " + exception.Message);
                    entries.Add(SaveSlotEntry.Unreadable(slotId));
                }
            }

            return entries;
        }

        /// <summary>
        /// Picks the slot Continue loads: the slot written last when it is still loadable, else the autosave, else the
        /// first loadable slot; null when nothing can be loaded.
        /// </summary>
        public static string? FindContinueSlotId(IReadOnlyList<SaveSlotEntry> entries, string? lastSlotId)
        {
            if (entries is null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            string? firstLoadable = null;
            bool autosaveLoadable = false;
            bool lastLoadable = false;
            for (int index = 0; index < entries.Count; index++)
            {
                SaveSlotEntry entry = entries[index];
                if (!entry.CanLoad)
                {
                    continue;
                }

                firstLoadable ??= entry.Id;
                autosaveLoadable |= entry.IsAutosave;
                lastLoadable |= string.Equals(entry.Id, lastSlotId, StringComparison.Ordinal);
            }

            if (lastLoadable)
            {
                return lastSlotId;
            }

            return autosaveLoadable ? SaveSlotEntry.AutosaveId : firstLoadable;
        }

        /// <summary>
        /// Gets the slot id written last, or null before any save.
        /// </summary>
        public string? GetLastSlotId()
        {
            return _readLastSlot();
        }

        /// <summary>
        /// Remembers the slot written or loaded last, for Continue.
        /// </summary>
        public void RememberLastSlot(string slotId)
        {
            _writeLastSlot(slotId);
        }

        private static string? ReadLastSlotPreference()
        {
            string value = PlayerPrefs.GetString(LastSlotPreference, string.Empty);
            return value.Length == 0 ? null : value;
        }

        private static void WriteLastSlotPreference(string slotId)
        {
            PlayerPrefs.SetString(LastSlotPreference, slotId);
            PlayerPrefs.Save();
        }
    }
}
