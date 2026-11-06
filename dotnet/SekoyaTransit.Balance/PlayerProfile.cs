#nullable enable
using System;
using System.Linq;

namespace SekoyaTransit.Balance
{
    /// <summary>
    /// Which contract proposals a simulated player accepts.
    /// </summary>
    internal enum ContractPolicy
    {
        /// <summary>Only proposals that one of its routes already serves.</summary>
        ServedOnly,

        /// <summary>Every second proposal, declining the others, and it builds what each one needs.</summary>
        EverySecond,

        /// <summary>Every proposal, and it builds what each one needs.</summary>
        All,
    }

    /// <summary>
    /// How a simulated player behaves (see docs/specs/economy-progression.md); neither profile is an optimal or a real
    /// human player.
    /// </summary>
    internal sealed class PlayerProfile
    {
        /// <summary>A steady player, the reference for "most players": does about half of the contracts, a new route every 2 months.</summary>
        public static readonly PlayerProfile Steady = new PlayerProfile("steady")
        {
            MonthsPerRoute = 2,
            TransportTargetPercent = 80,
            Contracts = ContractPolicy.EverySecond,
        };

        /// <summary>An experienced player who puts contracts first: every contract, a new route every month, contract routes before any other.</summary>
        public static readonly PlayerProfile Veteran = new PlayerProfile("veteran")
        {
            MonthsPerRoute = 1,
            TransportTargetPercent = 100,
            Contracts = ContractPolicy.All,
        };

        /// <summary>A relaxed player: a new route every 3 months, only the contracts its routes already serve.</summary>
        public static readonly PlayerProfile Casual = new PlayerProfile("casual")
        {
            MonthsPerRoute = 3,
            TransportTargetPercent = 50,
        };

        private static readonly PlayerProfile[] _all = { Casual, Steady, Veteran };

        private PlayerProfile(string name)
        {
            Name = name;
        }

        /// <summary>Gets every profile, in the order reports list them.</summary>
        public static PlayerProfile[] All => _all.ToArray();

        /// <summary>Gets the profile's name on the command line and in reports.</summary>
        public string Name { get; }

        /// <summary>Gets the game months between two new routes.</summary>
        public int MonthsPerRoute { get; private init; }

        /// <summary>Gets the share of a source's monthly output, in percent, the player buys vehicles to transport.</summary>
        public int TransportTargetPercent { get; private init; }

        /// <summary>Gets which contract proposals the player accepts; completed rewards are always claimed.</summary>
        public ContractPolicy Contracts { get; private init; } = ContractPolicy.ServedOnly;

        /// <summary>Gets whether the player builds routes and adds vehicles for the contracts it accepts.</summary>
        public bool ChasesContracts => Contracts != ContractPolicy.ServedOnly;

        /// <summary>Returns the profile with this name, or throws on an unknown one.</summary>
        public static PlayerProfile Parse(string name)
        {
            return _all.FirstOrDefault(profile => profile.Name == name)
                ?? throw new ArgumentException($"Unknown profile '{name}'.");
        }
    }
}
