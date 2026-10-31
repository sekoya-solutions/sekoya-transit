#nullable enable
using System;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Buys a vehicle of the route's mode and puts it, empty, at the route's first station (Issue #229). A train is
    /// bought as a locomotive with one wagon, at the combined price. Refused while the route has fewer than two stops.
    /// </summary>
    public sealed class BuyVehicleForRouteCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _routeId;
        private readonly ContentCatalog _catalog;
        private readonly string _typeId;
        private readonly int _calendarYear;

        /// <summary>
        /// Initializes a buy command for <paramref name="typeId"/> in <paramref name="calendarYear"/>.
        /// </summary>
        public BuyVehicleForRouteCommand(int routeId, ContentCatalog catalog, string typeId, int calendarYear)
        {
            if (string.IsNullOrWhiteSpace(typeId))
            {
                throw new ArgumentException("Type id cannot be null or whitespace.", nameof(typeId));
            }

            if (calendarYear < GameDate.FirstYear)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(calendarYear),
                    calendarYear,
                    "Calendar year cannot be before the simulation start year.");
            }

            _routeId = routeId;
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _typeId = typeId;
            _calendarYear = calendarYear;
        }

        /// <summary>
        /// Returns the price of buying <paramref name="definition"/> for a route: the purchase price, plus one wagon
        /// for a locomotive.
        /// </summary>
        public static long GetPrice(ContentCatalog catalog, VehicleDefinition definition)
        {
#pragma warning disable CA1510
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
#pragma warning restore CA1510

            return definition.Mode == VehicleMode.Rail
                ? checked(definition.Purchase + catalog.RailWagon.Purchase)
                : definition.Purchase;
        }

        /// <inheritdoc />
        public CommandValidationResult Validate(GeneratedWorld state)
        {
#pragma warning disable CA1510
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            if (!state.TryGetRoute(_routeId, out Route? route) || route == null)
            {
                return RouteCommandResults.From(RouteEditRefusal.MissingRoute);
            }

            if (!route.IsValid)
            {
                return CommandValidationResult.Rejected("A route needs at least two stops before it gets vehicles.");
            }

            if (!_catalog.TryGetVehicle(_typeId, out VehicleDefinition? definition) || definition == null)
            {
                return CommandValidationResult.Rejected("Vehicle type does not exist.");
            }

            if (!MatchesRoute(definition, route))
            {
                return CommandValidationResult.Rejected("Vehicle type does not match the route's mode.");
            }

            if (!VehicleAvailability.CanPurchase(definition, state, _calendarYear))
            {
                return VehicleAvailability.IsYearReached(definition, _calendarYear)
                    ? CommandValidationResult.Rejected("Vehicle type is not unlocked.")
                    : CommandValidationResult.Rejected("Vehicle type is not available in this calendar year.");
            }

            if (state.CompanyCash < GetPrice(_catalog, definition))
            {
                return CommandValidationResult.Rejected("Insufficient company cash.");
            }

            return CommandValidationResult.Valid;
        }

        /// <inheritdoc />
        public void Apply(GeneratedWorld state)
        {
#pragma warning disable CA1510
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
#pragma warning restore CA1510

            Route route = RouteCommandResults.Require(state, _routeId);
            if (!_catalog.TryGetVehicle(_typeId, out VehicleDefinition? definition) || definition == null ||
                !MatchesRoute(definition, route))
            {
                throw new InvalidOperationException($"Vehicle type '{_typeId}' cannot run on route {_routeId}.");
            }

            if (state.CompanyCash < GetPrice(_catalog, definition))
            {
                throw new InvalidOperationException("Company cash is insufficient to buy a vehicle for the route.");
            }

            state.TryDebitCompanyCash(definition.Purchase, FinanceCategory.VehiclePurchases);
            if (definition.Mode == VehicleMode.Road)
            {
                RoadVehicle vehicle = state.AddUnassignedRoadVehicle(definition.Id, definition.CargoCapacity);
                state.DeployRoadVehicle(vehicle.Id, route);
                return;
            }

            state.TryDebitCompanyCash(_catalog.RailWagon.Purchase, FinanceCategory.VehiclePurchases);
            RailVehicle train = state.AddUnassignedRailVehicle(
                definition.Id,
                definition.MaxWagons,
                definition.CapacityPerWagon);
            if (!train.TryAddWagon())
            {
                throw new InvalidOperationException($"Locomotive type '{definition.Id}' cannot pull a wagon.");
            }

            state.DeployRailVehicle(train.Id, route);
        }

        private static bool MatchesRoute(VehicleDefinition definition, Route route)
        {
            return (definition.Mode == VehicleMode.Road && route.Mode == RouteMode.Road) ||
                (definition.Mode == VehicleMode.Rail && route.Mode == RouteMode.Rail);
        }
    }
}
