#nullable enable

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Where a contract stands in its life (Issue #227). The values are saved, so never renumber them.
    /// </summary>
    public enum ContractStatus
    {
        /// <summary>Offered to the player, who may accept or decline it. It never expires.</summary>
        Proposal = 0,

        /// <summary>Accepted: deliveries count until the deadline.</summary>
        Accepted = 1,

        /// <summary>Delivered in time: the player may claim the reward.</summary>
        Completed = 2,

        /// <summary>The deadline passed before the amount was delivered: the player may remove it.</summary>
        Failed = 3
    }
}
