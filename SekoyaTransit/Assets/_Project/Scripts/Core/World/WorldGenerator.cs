#nullable enable
using SekoyaTransit.Core.Content;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Composes deterministic V1 terrain, town placement, and industry placement.
    /// </summary>
    public static class WorldGenerator
    {
        /// <summary>
        /// Generates a flat map and places its towns and industries from a seed.
        /// Towns receive placeholder population and empty needs when the catalog is omitted. With a catalog the town
        /// count is raised to <see cref="ResolveTownCount"/> and towns get names and dealt need plans (Issue #193).
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

            TownNeedsSettings needsSettings = townNeedsSettings ?? TownNeedsSettings.CreatePlaceholder();
            TownPlacementSettings townPlacement = settings.TownPlacementSettings;
            int townCount = ResolveTownCount(townPlacement.TownCount, needsSettings, catalog);
            if (townCount != townPlacement.TownCount)
            {
                townPlacement = new TownPlacementSettings(
                    townPlacement.MapSize,
                    townCount,
                    townPlacement.MinimumChebyshevDistance);
            }

            TileCoordinate[] townAnchors = TownPlacement.Generate(townPlacement, seed);
            var industrySettings = new IndustryPlacementSettings(
                townPlacement.MapSize,
                settings.MinimumIndustryChebyshevDistance,
                settings.IndustryRules,
                townAnchors);
            PlacedIndustry[] industries = IndustryPlacement.Generate(industrySettings, seed);

            var map = new TileMap(townPlacement.MapSize);
            for (int townIndex = 0; townIndex < townAnchors.Length; townIndex++)
            {
                SiteFootprint.Stamp(map, townAnchors[townIndex], townIndex);
            }

            for (int industryIndex = 0; industryIndex < industries.Length; industryIndex++)
            {
                SiteFootprint.Stamp(
                    map,
                    industries[industryIndex].Coordinate,
                    checked(townAnchors.Length + industryIndex));
            }

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

        /// <summary>
        /// Returns the town count generation places: the configured count, raised with a catalog to the minimum that
        /// lets every freight cargo be needed by enough towns (Issue #193).
        /// </summary>
        public static int ResolveTownCount(
            int configuredTownCount,
            TownNeedsSettings townNeedsSettings,
            ContentCatalog? catalog)
        {
            if (catalog == null)
            {
                return configuredTownCount;
            }

            int minimum = TownNeedsAssignment.MinimumTownCount(townNeedsSettings, catalog);
            return minimum > configuredTownCount ? minimum : configuredTownCount;
        }
    }
}
