#nullable enable
using System;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Buys a vehicle of the route's mode and puts it, empty, at the route's first station (Issue #229). A train is
    /// bought as a locomotive with a chosen number of wagons, from 1 to the locomotive's maximum (one by default,
    /// Issue #326), at the combined price. Refused while the route has fewer than two stops.
    /// </summary>
    public sealed class BuyVehicleForRouteCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _routeId;
        private readonly ContentCatalog _catalog;
        private readonly string _typeId;
        private readonly int _calendarYear;
        private readonly int? _wagonCount;

        /// <summary>
        /// Initializes a buy command for <paramref name="typeId"/> in <paramref name="calendarYear"/>; a train comes
        /// with one wagon.
        /// </summary>
        public BuyVehicleForRouteCommand(int routeId, ContentCatalog catalog, string typeId, int calendarYear)
            : this(routeId, catalog, typeId, calendarYear, null)
        {
        }

        /// <summary>
        /// Initializes a buy command for <paramref name="typeId"/> in <paramref name="calendarYear"/> with
        /// <paramref name="wagonCount"/> wagons: 1 to the locomotive's maximum for a train, 0 for a road vehicle.
        /// Validation rejects any other count.
        /// </summary>
        public BuyVehicleForRouteCommand(
            int routeId,
            ContentCatalog catalog,
            string typeId,
            int calendarYear,
            int wagonCount)
            : this(routeId, catalog, typeId, calendarYear, (int?)wagonCount)
        {
        }

        private BuyVehicleForRouteCommand(
            int routeId,
            ContentCatalog catalog,
            string typeId,
            int calendarYear,
            int? wagonCount)
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
            _wagonCount = wagonCount;
        }

        /// <summary>
        /// Returns the price of buying <paramref name="definition"/> for a route with its default wagon count: the
        /// purchase price, plus one wagon for a locomotive.
        /// </summary>
        public static long GetPrice(ContentCatalog catalog, VehicleDefinition definition)
        {
#pragma warning disable CA1510
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
#pragma warning restore CA1510

            return GetPrice(catalog, definition, DefaultWagonCount(definition));
        }

        /// <summary>
        /// Returns the price of buying <paramref name="definition"/> for a route with <paramref name="wagonCount"/>
        /// wagons: the purchase price, plus <paramref name="wagonCount"/> wagons for a locomotive.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="wagonCount"/> is not valid for the type (see <see cref="IsValidWagonCount"/>).
        /// </exception>
        public static long GetPrice(ContentCatalog catalog, VehicleDefinition definition, int wagonCount)
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

            if (!IsValidWagonCount(definition, wagonCount))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wagonCount),
                    wagonCount,
                    "Wagon count must be 1 to the locomotive's maximum for a train, and 0 for a road vehicle.");
            }

            return definition.Mode == VehicleMode.Rail
                ? checked(definition.Purchase + (catalog.RailWagon.Purchase * wagonCount))
                : definition.Purchase;
        }

        /// <summary>
        /// Returns whether <paramref name="wagonCount"/> can be bought with <paramref name="definition"/>: 1 to
        /// <see cref="VehicleDefinition.MaxWagons"/> for a locomotive, 0 for a road vehicle.
        /// </summary>
        public static bool IsValidWagonCount(VehicleDefinition definition, int wagonCount)
        {
#pragma warning disable CA1510
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
#pragma warning restore CA1510

            return definition.Mode == VehicleMode.Rail
                ? wagonCount >= 1 && wagonCount <= definition.MaxWagons
                : wagonCount == 0;
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
                return VehicleAvailability.IsAvailable(definition, state, _calendarYear)
                    ? CommandValidationResult.Rejected("Vehicle type is not unlocked.")
                    : CommandValidationResult.Rejected("Vehicle type is not available in this calendar year.");
            }

            int wagonCount = _wagonCount ?? DefaultWagonCount(definition);
            if (!IsValidWagonCount(definition, wagonCount))
            {
                return CommandValidationResult.Rejected(
                    definition.Mode == VehicleMode.Rail
                        ? "Wagon count must be between 1 and the locomotive's maximum."
                        : "A road vehicle has no wagons.");
            }

            if (state.CompanyCash < GetPrice(_catalog, definition, wagonCount))
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

            int wagonCount = _wagonCount ?? DefaultWagonCount(definition);
            if (state.CompanyCash < GetPrice(_catalog, definition, wagonCount))
            {
                throw new InvalidOperationException("Company cash is insufficient to buy a vehicle for the route.");
            }

            state.TryDebitCompanyCash(
                definition.Purchase,
                FinanceCategory.VehiclePurchases,
                definition.Mode == VehicleMode.Road ? FinanceMode.Road : FinanceMode.Rail);
            if (definition.Mode == VehicleMode.Road)
            {
                RoadVehicle vehicle = state.AddUnassignedRoadVehicle(definition.Id, definition.CargoCapacity);
                state.DeployRoadVehicle(vehicle.Id, route);
                return;
            }

            state.TryDebitCompanyCash(
                checked(_catalog.RailWagon.Purchase * wagonCount),
                FinanceCategory.VehiclePurchases,
                FinanceMode.Rail);
            RailVehicle train = state.AddUnassignedRailVehicle(
                definition.Id,
                definition.MaxWagons,
                definition.CapacityPerWagon);
            for (int wagon = 0; wagon < wagonCount; wagon++)
            {
                if (!train.TryAddWagon())
                {
                    throw new InvalidOperationException(
                        $"Locomotive type '{definition.Id}' cannot pull {wagonCount} wagons.");
                }
            }

            state.DeployRailVehicle(train.Id, route);
        }

        private static int DefaultWagonCount(VehicleDefinition definition)
        {
            return definition.Mode == VehicleMode.Rail ? 1 : 0;
        }

        private static bool MatchesRoute(VehicleDefinition definition, Route route)
        {
            return (definition.Mode == VehicleMode.Rail && route.Mode == RouteMode.Rail) ||
                (definition.Mode == VehicleMode.Road && route.Mode == RouteMode.Road);
        }
    }
}
