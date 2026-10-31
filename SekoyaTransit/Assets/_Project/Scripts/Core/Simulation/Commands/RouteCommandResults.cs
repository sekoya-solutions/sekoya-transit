#nullable enable
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Turns a <see cref="RouteEditRefusal"/> into a command validation result (Issue #229).
    /// </summary>
    internal static class RouteCommandResults
    {
        /// <summary>
        /// Returns the route an already validated command applies to.
        /// </summary>
        internal static Route Require(GeneratedWorld world, int routeId)
        {
            if (!world.TryGetRoute(routeId, out Route? route) || route == null)
            {
                throw new System.InvalidOperationException($"Route {routeId} does not exist.");
            }

            return route;
        }

        internal static CommandValidationResult From(RouteEditRefusal refusal)
        {
            switch (refusal)
            {
                case RouteEditRefusal.None:
                    return CommandValidationResult.Valid;
                case RouteEditRefusal.MissingRoute:
                    return CommandValidationResult.Rejected("Route does not exist.");
                case RouteEditRefusal.EmptyName:
                    return CommandValidationResult.Rejected("Route name cannot be empty.");
                case RouteEditRefusal.NameTooLong:
                    return CommandValidationResult.Rejected($"Route name cannot exceed {Route.MaxNameLength} characters.");
                case RouteEditRefusal.NameTaken:
                    return CommandValidationResult.Rejected("Another route already uses this name.");
                case RouteEditRefusal.MissingStation:
                    return CommandValidationResult.Rejected("Station does not exist.");
                case RouteEditRefusal.ModeMismatch:
                    return CommandValidationResult.Rejected("Road and rail stations cannot be mixed in one route.");
                case RouteEditRefusal.RepeatedStop:
                    return CommandValidationResult.Rejected("A route cannot stop at the same station twice in a row.");
                default:
                    return CommandValidationResult.Rejected("Route stop is out of range.");
            }
        }
    }
}
