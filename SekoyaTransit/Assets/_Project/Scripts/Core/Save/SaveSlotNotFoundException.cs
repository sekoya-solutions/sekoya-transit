#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Reports an attempt to load or delete a save slot that is not present.
    /// </summary>
    public sealed class SaveSlotNotFoundException : Exception
    {
        public SaveSlotNotFoundException(SaveSlot slot)
            : base($"Save slot '{slot.StorageKey}' does not exist.")
        {
            Slot = slot;
        }

        public SaveSlot Slot { get; }
    }
}
