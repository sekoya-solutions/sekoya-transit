#nullable enable
using System;
using System.Collections.Generic;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Version-stamped cache of successful <see cref="RailPathfinder"/> routes.
    /// </summary>
    public sealed class RailRouteCache
    {
        private readonly Dictionary<RouteKey, CacheEntry> _entries = new Dictionary<RouteKey, CacheEntry>();

        /// <summary>
        /// Returns a cached route when present and still valid for <paramref name="graph"/>.<see cref="RailGraph.Version"/>;
        /// otherwise computes via <see cref="RailPathfinder.TryFindPath"/> and stores successes.
        /// </summary>
        public bool TryGetPath(RailGraph graph, int fromNodeId, int toNodeId, out RailPath? path)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }
#pragma warning restore CA1510

            var key = new RouteKey(fromNodeId, toNodeId);
            if (_entries.TryGetValue(key, out CacheEntry entry) && entry.GraphVersion == graph.Version)
            {
                path = entry.Path;
                return true;
            }

            if (!RailPathfinder.TryFindPath(graph, fromNodeId, toNodeId, out path) || path == null)
            {
                _entries.Remove(key);
                return false;
            }

            _entries[key] = new CacheEntry(graph.Version, path);
            return true;
        }

        /// <summary>
        /// Removes all cached routes.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
        }

        private readonly struct RouteKey : IEquatable<RouteKey>
        {
            public RouteKey(int fromNodeId, int toNodeId)
            {
                FromNodeId = fromNodeId;
                ToNodeId = toNodeId;
            }

            public int FromNodeId { get; }

            public int ToNodeId { get; }

            public bool Equals(RouteKey other)
            {
                return FromNodeId == other.FromNodeId && ToNodeId == other.ToNodeId;
            }

            public override bool Equals(object? obj)
            {
                return obj is RouteKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (FromNodeId * 397) ^ ToNodeId;
                }
            }
        }

        private readonly struct CacheEntry
        {
            public CacheEntry(int graphVersion, RailPath path)
            {
                GraphVersion = graphVersion;
                Path = path;
            }

            public int GraphVersion { get; }

            public RailPath Path { get; }
        }
    }
}
