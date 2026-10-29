#nullable enable
using System;
using System.Threading.Tasks;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Connects snapshot serialization to an asynchronous save-slot store.
    /// </summary>
    public sealed class SaveService
    {
        private readonly ISaveSerializer _serializer;
        private readonly ISaveStore _store;

        public SaveService(ISaveSerializer serializer, ISaveStore store)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }
#pragma warning restore CA1510

            _serializer = serializer;
            _store = store;
        }

        public async Task SaveAsync(SaveSlot slot, SaveSnapshot snapshot)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
#pragma warning restore CA1510

            byte[] compressedData = _serializer.Serialize(snapshot);
            await _store.SaveAsync(slot, compressedData);
        }

        public async Task<SaveSnapshot> LoadAsync(SaveSlot slot)
        {
            byte[] compressedData = await _store.LoadAsync(slot);
            return _serializer.Deserialize(compressedData);
        }
    }
}
