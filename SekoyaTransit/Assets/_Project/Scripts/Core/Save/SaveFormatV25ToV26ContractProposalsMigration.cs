#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Bumps format 25 saves to 26 (Issue #227). Format 26 stores each contract with a status (proposal, accepted,
    /// completed, failed), a town target only, and the settled date. Contracts of older saves are dropped when the save
    /// is read (the id counter and the generator state are kept), so a loaded game proposes new contracts one per month.
    /// </summary>
    public sealed class SaveFormatV25ToV26ContractProposalsMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion => 25;

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
