#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TransportTycoon.Core.Content
{
    /// <summary>
    /// Immutable catalog of cargo and industry definitions.
    /// </summary>
    public sealed class ContentCatalog
    {
        private readonly ReadOnlyCollection<CargoDefinition> _cargo;
        private readonly ReadOnlyCollection<IndustryDefinition> _industries;
        private readonly ReadOnlyDictionary<string, CargoDefinition> _cargoById;
        private readonly ReadOnlyDictionary<string, IndustryDefinition> _industriesById;

        public ContentCatalog(
            IReadOnlyList<CargoDefinition> cargo,
            IReadOnlyList<IndustryDefinition> industries)
        {
            CargoDefinition[] cargoCopy = CopyById(
                cargo,
                nameof(cargo),
                definition => definition.Id,
                out Dictionary<string, CargoDefinition> cargoById);
            IndustryDefinition[] industryCopy = CopyById(
                industries,
                nameof(industries),
                definition => definition.Id,
                out Dictionary<string, IndustryDefinition> industriesById);

            _cargo = Array.AsReadOnly(cargoCopy);
            _industries = Array.AsReadOnly(industryCopy);
            _cargoById = new ReadOnlyDictionary<string, CargoDefinition>(cargoById);
            _industriesById = new ReadOnlyDictionary<string, IndustryDefinition>(industriesById);
        }

        /// <summary>
        /// Gets the cargo definitions in source order.
        /// </summary>
        public IReadOnlyList<CargoDefinition> Cargo
        {
            get
            {
                return _cargo;
            }
        }

        /// <summary>
        /// Gets the industry definitions in source order.
        /// </summary>
        public IReadOnlyList<IndustryDefinition> Industries
        {
            get
            {
                return _industries;
            }
        }

        /// <summary>
        /// Looks up a cargo definition using an ordinal ID comparison.
        /// </summary>
        public bool TryGetCargo(string? id, out CargoDefinition? definition)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                definition = null;
                return false;
            }

            return _cargoById.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Looks up an industry definition using an ordinal ID comparison.
        /// </summary>
        public bool TryGetIndustry(string? id, out IndustryDefinition? definition)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                definition = null;
                return false;
            }

            return _industriesById.TryGetValue(id, out definition);
        }

        private static TDefinition[] CopyById<TDefinition>(
            IReadOnlyList<TDefinition> definitions,
            string parameterName,
            Func<TDefinition, string> getId,
            out Dictionary<string, TDefinition> definitionsById)
            where TDefinition : class
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (definitions is null)
            {
                throw new ArgumentNullException(parameterName);
            }
#pragma warning restore CA1510

            var copied = new TDefinition[definitions.Count];
            definitionsById = new Dictionary<string, TDefinition>(definitions.Count, StringComparer.Ordinal);
            for (int index = 0; index < definitions.Count; index++)
            {
                TDefinition? definition = definitions[index];
                if (definition is null)
                {
                    throw new ArgumentException("Definition collections cannot contain null entries.", parameterName);
                }

                string id = getId(definition);
                if (!definitionsById.TryAdd(id, definition))
                {
                    throw new ArgumentException($"Duplicate definition ID '{id}'.", parameterName);
                }

                copied[index] = definition;
            }

            return copied;
        }
    }
}
