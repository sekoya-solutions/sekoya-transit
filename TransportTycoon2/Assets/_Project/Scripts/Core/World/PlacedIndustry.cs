#nullable enable

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// A deterministically placed instance of an industry definition.
    /// </summary>
    public readonly struct PlacedIndustry
    {
        /// <summary>
        /// Initializes a placed industry value.
        /// </summary>
        /// <param name="industryId">The stable industry definition ID.</param>
        /// <param name="instanceIndex">The zero-based instance index within that definition.</param>
        /// <param name="coordinate">The industry anchor tile.</param>
        public PlacedIndustry(string industryId, int instanceIndex, TileCoordinate coordinate)
        {
            IndustryId = industryId;
            InstanceIndex = instanceIndex;
            Coordinate = coordinate;
        }

        /// <summary>
        /// Gets the stable industry definition ID.
        /// </summary>
        public string IndustryId { get; }

        /// <summary>
        /// Gets the zero-based instance index within the definition.
        /// </summary>
        public int InstanceIndex { get; }

        /// <summary>
        /// Gets the industry anchor tile.
        /// </summary>
        public TileCoordinate Coordinate { get; }
    }
}
