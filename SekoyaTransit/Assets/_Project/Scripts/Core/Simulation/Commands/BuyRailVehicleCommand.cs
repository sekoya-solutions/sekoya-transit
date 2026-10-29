#nullable enable
using System;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Simulation.Commands
{
    /// <summary>
    /// Spawns a rail locomotive at an existing rail station and debits its purchase price.
    /// </summary>
    public sealed class BuyRailVehicleCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly int _stationId;
        private readonly ContentCatalog _catalog;
        private readonly string _typeId;
        private readonly int _calendarYear;

        /// <summary>
        /// Initializes a buy-locomotive command for the catalog starting rail type when
        /// <paramref name="typeId"/> is omitted. <paramref name="calendarYear"/> defaults to 1900.
        /// </summary>
        public BuyRailVehicleCommand(
            int stationId,
            ContentCatalog? catalog = null,
            string? typeId = null,
            int calendarYear = GameDate.FirstYear)
        {
            if (calendarYear < GameDate.FirstYear)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(calendarYear),
                    calendarYear,
                    "Calendar year cannot be before the simulation start year.");
            }

            _stationId = stationId;
            _catalog = catalog ?? CreateMinimalCatalog();
            _typeId = string.IsNullOrWhiteSpace(typeId) ? _catalog.StartingRailLocomotive.Id : typeId;
            _calendarYear = calendarYear;
        }

        /// <summary>
        /// Gets the spawn station id.
        /// </summary>
        public int StationId
        {
            get
            {
                return _stationId;
            }
        }

        /// <summary>
        /// Gets the locomotive type content id to purchase.
        /// </summary>
        public string TypeId
        {
            get
            {
                return _typeId;
            }
        }

        /// <summary>
        /// Gets the calendar year used for the availability gate.
        /// </summary>
        public int CalendarYear
        {
            get
            {
                return _calendarYear;
            }
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

            if (!TryResolveType(out VehicleDefinition? definition, out string? error) || definition == null)
            {
                return CommandValidationResult.Rejected(error ?? "Invalid vehicle type.");
            }

            if (!VehicleAvailability.CanPurchase(definition, state, _calendarYear))
            {
                if (!VehicleAvailability.IsYearReached(definition, _calendarYear))
                {
                    return CommandValidationResult.Rejected("Vehicle type is not available in this calendar year.");
                }

                return CommandValidationResult.Rejected("Vehicle type is not unlocked.");
            }

            if (!state.TryGetRailStation(_stationId, out RailStation? station) || station == null)
            {
                return CommandValidationResult.Rejected("Rail station does not exist.");
            }

            if (!state.RailGraph.TryGetNodeId(station.Coordinate, out _))
            {
                return CommandValidationResult.Rejected("Rail station is not present in the rail graph.");
            }

            if (state.CompanyCash < definition.Purchase)
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

            if (!TryResolveType(out VehicleDefinition? definition, out _) || definition == null)
            {
                throw new InvalidOperationException("Rail locomotive type is invalid.");
            }

            if (!state.TryDebitCompanyCash(definition.Purchase, FinanceCategory.VehiclePurchases))
            {
                throw new InvalidOperationException("Company cash is insufficient to buy a rail vehicle.");
            }

            state.AddRailVehicle(
                _stationId,
                definition.Id,
                definition.MaxWagons,
                definition.CapacityPerWagon);
        }

        private bool TryResolveType(out VehicleDefinition? definition, out string? error)
        {
            if (!_catalog.TryGetVehicle(_typeId, out definition) || definition == null)
            {
                error = "Vehicle type does not exist.";
                return false;
            }

            if (definition.Mode != VehicleMode.Rail)
            {
                error = "Vehicle type is not a rail locomotive.";
                definition = null;
                return false;
            }

            error = null;
            return true;
        }

        private static ContentCatalog CreateMinimalCatalog()
        {
            return new ContentCatalog(
                Array.Empty<CargoDefinition>(),
                Array.Empty<IndustryDefinition>(),
                vehicles: ContentCatalog.CreatePlaceholderVehicles());
        }
    }
}
