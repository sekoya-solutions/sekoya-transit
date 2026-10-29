#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Data-driven placement requirements for one industry definition.
    /// </summary>
    public sealed class IndustryPlacementRule
    {
        private readonly ReadOnlyCollection<string> _inputCargoIds;
        private readonly ReadOnlyCollection<string> _outputCargoIds;

        /// <summary>
        /// Initializes a placement rule for an industry and its configured instance count.
        /// </summary>
        /// <param name="industryId">The stable content ID.</param>
        /// <param name="level">The industry level in its production chain.</param>
        /// <param name="inputCargoIds">Cargo IDs required by the industry.</param>
        /// <param name="outputCargoIds">Cargo IDs produced by the industry.</param>
        /// <param name="instanceCount">The exact number of instances to place.</param>
        /// <param name="maximumProducerChebyshevDistance">
        /// The inclusive maximum distance from a consumer to at least one lower-level producer of each input.
        /// Must be zero for a primary industry and positive for a higher-level industry.
        /// </param>
        public IndustryPlacementRule(
            string industryId,
            int level,
            IReadOnlyList<string> inputCargoIds,
            IReadOnlyList<string> outputCargoIds,
            int instanceCount,
            int maximumProducerChebyshevDistance)
        {
            if (string.IsNullOrWhiteSpace(industryId))
            {
                throw new ArgumentException("Industry ID cannot be null or whitespace.", nameof(industryId));
            }

            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Industry level must be positive.");
            }

#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (inputCargoIds is null)
            {
                throw new ArgumentNullException(nameof(inputCargoIds));
            }

            if (outputCargoIds is null)
            {
                throw new ArgumentNullException(nameof(outputCargoIds));
            }
#pragma warning restore CA1510

            if (instanceCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(instanceCount), instanceCount, "Industry count must be positive.");
            }

            if (maximumProducerChebyshevDistance < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumProducerChebyshevDistance),
                    maximumProducerChebyshevDistance,
                    "Maximum producer distance cannot be negative.");
            }

            if ((level == 1 && inputCargoIds.Count != 0) ||
                (level > 1 && inputCargoIds.Count == 0))
            {
                throw new ArgumentException(
                    "Primary industries must have no inputs; higher-level industries must have at least one input.",
                    nameof(inputCargoIds));
            }

            if ((level == 1 && maximumProducerChebyshevDistance != 0) ||
                (level > 1 && maximumProducerChebyshevDistance == 0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumProducerChebyshevDistance),
                    maximumProducerChebyshevDistance,
                    "Primary industries use distance zero; higher-level industries require a positive producer distance.");
            }

            _inputCargoIds = CopyIds(inputCargoIds, nameof(inputCargoIds));
            _outputCargoIds = CopyIds(outputCargoIds, nameof(outputCargoIds));
            if (_outputCargoIds.Count == 0)
            {
                throw new ArgumentException("An industry must produce at least one cargo.", nameof(outputCargoIds));
            }

            IndustryId = industryId;
            Level = level;
            InstanceCount = instanceCount;
            MaximumProducerChebyshevDistance = maximumProducerChebyshevDistance;
        }

        private static ReadOnlyCollection<string> CopyIds(IReadOnlyList<string> ids, string parameterName)
        {
            var copy = new string[ids.Count];
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < ids.Count; index++)
            {
                string? id = ids[index];
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new ArgumentException("Cargo IDs cannot be null or whitespace.", parameterName);
                }

                if (!uniqueIds.Add(id))
                {
                    throw new ArgumentException($"Cargo ID '{id}' is duplicated.", parameterName);
                }

                copy[index] = id;
            }

            return Array.AsReadOnly(copy);
        }

        /// <summary>
        /// Gets the stable content ID.
        /// </summary>
        public string IndustryId { get; }

        /// <summary>
        /// Gets the level in the production chain.
        /// </summary>
        public int Level { get; }

        /// <summary>
        /// Gets the input cargo IDs.
        /// </summary>
        public IReadOnlyList<string> InputCargoIds
        {
            get
            {
                return _inputCargoIds;
            }
        }

        /// <summary>
        /// Gets the output cargo IDs.
        /// </summary>
        public IReadOnlyList<string> OutputCargoIds
        {
            get
            {
                return _outputCargoIds;
            }
        }

        /// <summary>
        /// Gets the exact number of instances to place.
        /// </summary>
        public int InstanceCount { get; }

        /// <summary>
        /// Gets the inclusive maximum Chebyshev distance to an input producer, in tiles.
        /// </summary>
        public int MaximumProducerChebyshevDistance { get; }
    }
}
