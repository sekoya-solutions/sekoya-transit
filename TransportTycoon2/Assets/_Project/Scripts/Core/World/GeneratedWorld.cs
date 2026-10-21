#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// A generated tile map and its ordered town and industry anchors.
    /// </summary>
    public sealed class GeneratedWorld
    {
        private readonly ReadOnlyCollection<TileCoordinate> _townAnchors;
        private readonly ReadOnlyCollection<PlacedIndustry> _industries;

        internal GeneratedWorld(
            TileMap map,
            TileCoordinate[] townAnchors,
            PlacedIndustry[] industries)
        {
            Map = map;
            _townAnchors = Array.AsReadOnly((TileCoordinate[])townAnchors.Clone());
            _industries = Array.AsReadOnly((PlacedIndustry[])industries.Clone());
        }

        /// <summary>
        /// Gets the generated map. Terrain is flat, and unowned/unoccupied tile sentinels are preserved.
        /// </summary>
        public TileMap Map { get; }

        /// <summary>
        /// Gets town anchors in deterministic placement order. Their object IDs are their zero-based indices.
        /// </summary>
        public IReadOnlyList<TileCoordinate> TownAnchors
        {
            get
            {
                return _townAnchors;
            }
        }

        /// <summary>
        /// Gets industries in deterministic level, industry-ID, and instance order.
        /// Their object IDs follow the town IDs in this list's order.
        /// </summary>
        public IReadOnlyList<PlacedIndustry> Industries
        {
            get
            {
                return _industries;
            }
        }

        /// <summary>
        /// Gets the map object ID assigned to a town by its placement-order index.
        /// </summary>
        public int GetTownObjectId(int townIndex)
        {
            if (townIndex < 0 || townIndex >= _townAnchors.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(townIndex));
            }

            return townIndex;
        }

        /// <summary>
        /// Gets the map object ID assigned to an industry by its placement-order index.
        /// </summary>
        public int GetIndustryObjectId(int industryIndex)
        {
            if (industryIndex < 0 || industryIndex >= _industries.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(industryIndex));
            }

            return checked(_townAnchors.Count + industryIndex);
        }
    }
}
