#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// An immutable node-to-node route over a <see cref="RoadGraph"/>.
    /// </summary>
    public sealed class RoadPath
    {
        private readonly ReadOnlyCollection<int> _nodeIds;
        private readonly ReadOnlyCollection<int> _edgeIds;

        /// <summary>
        /// Initializes a route from ordered node and edge id sequences.
        /// </summary>
        /// <param name="nodeIds">Visited nodes from start to goal, inclusive.</param>
        /// <param name="edgeIds">Edges traversed between consecutive nodes.</param>
        /// <param name="length">Total travel length in tile steps.</param>
        public RoadPath(IReadOnlyList<int> nodeIds, IReadOnlyList<int> edgeIds, int length)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (nodeIds == null)
            {
                throw new ArgumentNullException(nameof(nodeIds));
            }

            if (edgeIds == null)
            {
                throw new ArgumentNullException(nameof(edgeIds));
            }
#pragma warning restore CA1510

            if (nodeIds.Count == 0)
            {
                throw new ArgumentException("A path must include at least one node.", nameof(nodeIds));
            }

            if (edgeIds.Count != nodeIds.Count - 1)
            {
                throw new ArgumentException("Edge count must equal node count minus one.", nameof(edgeIds));
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "Path length cannot be negative.");
            }

            var nodeCopy = new int[nodeIds.Count];
            for (int i = 0; i < nodeIds.Count; i++)
            {
                nodeCopy[i] = nodeIds[i];
            }

            var edgeCopy = new int[edgeIds.Count];
            for (int i = 0; i < edgeIds.Count; i++)
            {
                edgeCopy[i] = edgeIds[i];
            }

            _nodeIds = Array.AsReadOnly(nodeCopy);
            _edgeIds = Array.AsReadOnly(edgeCopy);
            Length = length;
        }

        /// <summary>
        /// Gets the ordered node ids from start to goal, inclusive.
        /// </summary>
        public IReadOnlyList<int> NodeIds
        {
            get
            {
                return _nodeIds;
            }
        }

        /// <summary>
        /// Gets the ordered edge ids between consecutive <see cref="NodeIds"/>.
        /// </summary>
        public IReadOnlyList<int> EdgeIds
        {
            get
            {
                return _edgeIds;
            }
        }

        /// <summary>
        /// Gets the total travel length in tile steps.
        /// </summary>
        public int Length { get; }
    }
}
