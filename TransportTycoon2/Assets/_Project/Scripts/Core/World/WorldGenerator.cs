#nullable enable

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Composes deterministic V1 terrain, town placement, and industry placement.
    /// </summary>
    public static class WorldGenerator
    {
        /// <summary>
        /// Generates a flat map and places its towns and industries from a seed.
        /// </summary>
        /// <param name="settings">The explicit data-driven generation settings.</param>
        /// <param name="seed">The shared world seed; each placement routine uses its own fixed random stream.</param>
        /// <returns>The map and ordered placement results.</returns>
        public static GeneratedWorld Generate(WorldGenerationSettings settings, ulong seed)
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

            return new GeneratedWorld(map, townAnchors, industries);
        }
    }
}
