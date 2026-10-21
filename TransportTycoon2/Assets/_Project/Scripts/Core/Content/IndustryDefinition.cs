#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TransportTycoon.Core.Content
{
    /// <summary>
    /// Immutable definition of an industry and its fixed-ratio production recipe.
    /// </summary>
    public sealed class IndustryDefinition
    {
        private readonly ReadOnlyCollection<CargoAmountDefinition> _inputs;
        private readonly ReadOnlyCollection<CargoAmountDefinition> _outputs;

        public IndustryDefinition(
            string id,
            string nameKey,
            int level,
            IReadOnlyList<CargoAmountDefinition> inputs,
            IReadOnlyList<CargoAmountDefinition> outputs)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Industry ID cannot be null or whitespace.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(nameKey))
            {
                throw new ArgumentException("Industry name key cannot be null or whitespace.", nameof(nameKey));
            }

            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Industry level must be positive.");
            }

            _inputs = CopyAmounts(inputs, nameof(inputs));
            _outputs = CopyAmounts(outputs, nameof(outputs));
            if (_outputs.Count == 0)
            {
                throw new ArgumentException("An industry must produce at least one cargo.", nameof(outputs));
            }

            Id = id;
            NameKey = nameKey;
            Level = level;
        }

        /// <summary>
        /// Gets the stable content ID.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the localization key for the industry name.
        /// </summary>
        public string NameKey { get; }

        /// <summary>
        /// Gets the industry level in its production chain.
        /// </summary>
        public int Level { get; }

        /// <summary>
        /// Gets the cargo consumed per production cycle.
        /// </summary>
        public IReadOnlyList<CargoAmountDefinition> Inputs
        {
            get
            {
                return _inputs;
            }
        }

        /// <summary>
        /// Gets the cargo produced per production cycle.
        /// </summary>
        public IReadOnlyList<CargoAmountDefinition> Outputs
        {
            get
            {
                return _outputs;
            }
        }

        private static ReadOnlyCollection<CargoAmountDefinition> CopyAmounts(
            IReadOnlyList<CargoAmountDefinition> amounts,
            string parameterName)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (amounts is null)
            {
                throw new ArgumentNullException(parameterName);
            }
#pragma warning restore CA1510

            var copy = new CargoAmountDefinition[amounts.Count];
            for (int index = 0; index < amounts.Count; index++)
            {
                CargoAmountDefinition? amount = amounts[index];
                if (amount is null)
                {
                    throw new ArgumentException("Recipe amounts cannot contain null entries.", parameterName);
                }

                copy[index] = amount;
            }

            return Array.AsReadOnly(copy);
        }
    }
}
