#nullable enable
using TransportTycoon.Core.Content;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Composes deterministic V1 terrain, town placement, and industry placement.
    /// </summary>
    public static class WorldGenerator
    {
        /// <summary>
        /// Generates a flat map and places its towns and industries from a seed.
        /// Towns receive placeholder population and empty needs when catalog or town settings are omitted.
        /// </summary>
        /// <param name="settings">The explicit data-driven generation settings.</param>
        /// <param name="seed">The shared world seed; each placement routine uses its own fixed random stream.</param>
        /// <param name="townNeedsSettings">Optional town population/tier settings; defaults to placeholder.</param>
        /// <param name="catalog">Optional content catalog used to assign freight needs.</param>
        /// <returns>The map and ordered placement results.</returns>
        public static GeneratedWorld Generate(
            WorldGenerationSettings settings,
            ulong seed,
            TownNeedsSettings? townNeedsSettings = null,
            ContentCatalog? catalog = null)
        {
            settings.Validate();

            TileCoordinate[] townAnchors = TownPlacement.Generate(
                settings.TownPlacementSettings,
                seed);
            var industrySettings = new IndustryPlacementSettings(
                settings.TownPlacementSettings.MapSize,
                settings.MinimumIndustryChebyshevDistance,
                settings.IndustryRules,
                townAnchors);
            PlacedIndustry[] industries = IndustryPlacement.Generate(industrySettings, seed);

            var map = new TileMap(settings.TownPlacementSettings.MapSize);
            for (int townIndex = 0; townIndex < townAnchors.Length; townIndex++)
            {
                map.SetObjectId(townAnchors[townIndex], townIndex);
            }

            for (int industryIndex = 0; industryIndex < industries.Length; industryIndex++)
            {
                map.SetObjectId(
                    industries[industryIndex].Coordinate,
                    checked(townAnchors.Length + industryIndex));
            }

            TownNeedsSettings needsSettings = townNeedsSettings ?? TownNeedsSettings.CreatePlaceholder();
            Town[] towns;
            if (catalog != null)
            {
                towns = TownNeedsAssignment.CreateTowns(townAnchors, seed, needsSettings, catalog);
            }
            else
            {
                towns = TownNeedsAssignment.CreateTownsWithoutNeeds(
                    townAnchors,
                    needsSettings.MinStartingPopulation);
            }

            long startingCash = catalog != null
                ? catalog.Economy.StartingCash
                : GeneratedWorld.PlaceholderStartingCash;
            return new GeneratedWorld(map, towns, industries, seed, startingCash);
        }
    }
}
