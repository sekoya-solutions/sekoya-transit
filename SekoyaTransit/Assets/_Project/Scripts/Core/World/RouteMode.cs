#nullable enable

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// The transport mode of a route, fixed by its first stop (Issue #229).
    /// </summary>
    public enum RouteMode
    {
        /// <summary>
        /// The route has no stop yet, so it accepts a road or a rail station as its first stop.
        /// </summary>
        None = 0,

        /// <summary>
        /// The route stops at road stations and runs trucks.
        /// </summary>
        Road = 1,

        /// <summary>
        /// The route stops at rail stations and runs trains.
        /// </summary>
        Rail = 2
    }
}
