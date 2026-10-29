#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// A town's full ordered freight-need list per manufacturing level, fixed when the town is created (Issue #193).
    /// The current needs are a prefix of each level's list whose length comes from the population tier, so needs
    /// unlocked by growth are known (but hidden) from the start.
    /// </summary>
    public sealed class TownNeedPlan
    {
        /// <summary>
        /// Highest manufacturing level a plan holds.
        /// </summary>
        public const int MaxLevel = 3;

        private readonly ReadOnlyCollection<string>[] _levels;

        /// <summary>
        /// Initializes a plan from ordered cargo ids per level.
        /// </summary>
        public TownNeedPlan(IReadOnlyList<string> level1, IReadOnlyList<string> level2, IReadOnlyList<string> level3)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            _levels = new[]
            {
                Copy(level1, nameof(level1), seen),
                Copy(level2, nameof(level2), seen),
                Copy(level3, nameof(level3), seen)
            };
        }

        /// <summary>
        /// Gets the plan of a town created before plans existed (restore fills it from content).
        /// </summary>
        public static TownNeedPlan Empty { get; } =
            new TownNeedPlan(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        /// <summary>
        /// Gets whether the plan holds no need at any level.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return _levels[0].Count == 0 && _levels[1].Count == 0 && _levels[2].Count == 0;
            }
        }

        /// <summary>
        /// Gets the ordered cargo ids planned for a manufacturing level (1–3).
        /// </summary>
        public IReadOnlyList<string> GetLevel(int manufacturingLevel)
        {
            if (manufacturingLevel < 1 || manufacturingLevel > MaxLevel)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(manufacturingLevel),
                    manufacturingLevel,
                    "Manufacturing level must be between 1 and 3.");
            }

            return _levels[manufacturingLevel - 1];
        }

        /// <summary>
        /// Returns whether any level of the plan holds <paramref name="cargoId"/>.
        /// </summary>
        public bool Contains(string cargoId)
        {
            for (int level = 0; level < _levels.Length; level++)
            {
                if (_levels[level].Contains(cargoId))
                {
                    return true;
                }
            }

            return false;
        }

        private static ReadOnlyCollection<string> Copy(
            IReadOnlyList<string> cargoIds,
            string parameterName,
            HashSet<string> seen)
        {
#pragma warning disable CA1510
            if (cargoIds is null)
            {
                throw new ArgumentNullException(parameterName);
            }
#pragma warning restore CA1510

            var copy = new string[cargoIds.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                string cargoId = cargoIds[index];
                if (string.IsNullOrWhiteSpace(cargoId))
                {
                    throw new ArgumentException($"Planned need {index} has no cargo id.", parameterName);
                }

                if (!seen.Add(cargoId))
                {
                    throw new ArgumentException($"Duplicate planned need cargo '{cargoId}'.", parameterName);
                }

                copy[index] = cargoId;
            }

            return Array.AsReadOnly(copy);
        }
    }
}
