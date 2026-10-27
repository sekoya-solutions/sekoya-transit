#nullable enable
using System;
using TransportTycoon.Core.Content;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Builds a fresh world from a <see cref="NewGameRequest"/> and the catalog's per-size generation data (Issue #67).
    /// </summary>
    public static class NewGame
    {
        /// <summary>
        /// Generates the world for a request; the same request and catalog always give an identical world.
        /// </summary>
        public static GeneratedWorld Create(NewGameRequest request, ContentCatalog catalog)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (catalog is null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            WorldGenerationSettings settings = catalog.WorldGeneration.CreateSettings(request.MapSize, catalog.Industries);
            return WorldGenerator.Generate(settings, request.Seed, catalog.TownNeeds, catalog);
        }
    }
}
