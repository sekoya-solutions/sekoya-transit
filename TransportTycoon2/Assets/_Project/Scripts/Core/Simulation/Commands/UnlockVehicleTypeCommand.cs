#nullable enable
using System;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Simulation.Commands
{
    /// <summary>
    /// Permanently unlocks a road or rail vehicle type for the company after paying unlock cost.
    /// </summary>
    public sealed class UnlockVehicleTypeCommand : ISimulationCommand<GeneratedWorld>
    {
        private readonly string _typeId;
        private readonly int _calendarYear;
        private readonly ContentCatalog _catalog;

        /// <summary>
        /// Initializes an unlock command for <paramref name="typeId"/> as of <paramref name="calendarYear"/>.
        /// </summary>
        public UnlockVehicleTypeCommand(string typeId, int calendarYear, ContentCatalog? catalog = null)
        {
            if (string.IsNullOrWhiteSpace(typeId))
            {
                throw new ArgumentException("Vehicle type id cannot be null or whitespace.", nameof(typeId));
            }

            if (calendarYear < GameDate.FirstYear)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(calendarYear),
                    calendarYear,
                    "Calendar year cannot be before the simulation start year.");
            }

            _typeId = typeId;
            _calendarYear = calendarYear;
            _catalog = catalog ?? CreateMinimalCatalog();
        }

        /// <summary>
        /// Gets the vehicle type content id to unlock.
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

            if (definition.Starting)
            {
                return CommandValidationResult.Rejected("Starting vehicle types do not require an unlock.");
            }

            if (!VehicleAvailability.IsYearReached(definition, _calendarYear))
            {
                return CommandValidationResult.Rejected("Vehicle type is not available in this calendar year.");
            }

            if (state.IsVehicleTypeUnlocked(definition.Id))
            {
                return CommandValidationResult.Rejected("Vehicle type is already unlocked.");
            }

            if (state.CompanyCash < definition.UnlockCost)
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
                throw new InvalidOperationException("Vehicle type is invalid.");
            }

            if (!state.TryDebitCompanyCash(definition.UnlockCost, FinanceCategory.VehicleUnlocks))
            {
                throw new InvalidOperationException("Company cash is insufficient to unlock the vehicle type.");
            }

            state.UnlockVehicleType(definition.Id);
        }

        private bool TryResolveType(out VehicleDefinition? definition, out string? error)
        {
            if (!_catalog.TryGetVehicle(_typeId, out definition) || definition == null)
            {
                error = "Vehicle type does not exist.";
                return false;
            }

            if (definition.Mode != VehicleMode.Road && definition.Mode != VehicleMode.Rail)
            {
                error = "Only road trucks and rail locomotives can be unlocked.";
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
