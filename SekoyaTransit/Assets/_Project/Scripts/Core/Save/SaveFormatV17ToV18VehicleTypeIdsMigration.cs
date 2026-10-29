#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Bumps format 17 saves to 18. String vehicle type ids are resolved at deserialize time
    /// from legacy integer <c>typeId</c> 0 to starter catalog ids.
    /// </summary>
    public sealed class SaveFormatV17ToV18VehicleTypeIdsMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion => 17;

        /// <inheritdoc />
        public SaveSnapshot Migrate(SaveSnapshot snapshot)
        {
#pragma warning disable CA1510
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
#pragma warning restore CA1510

            if (snapshot.FormatVersion != FromVersion)
            {
                throw new SaveFormatException(
                    $"Migration from version {FromVersion} received format version {snapshot.FormatVersion}.");
            }

            return snapshot.WithFormatVersion(FromVersion + 1);
        }
    }
}
