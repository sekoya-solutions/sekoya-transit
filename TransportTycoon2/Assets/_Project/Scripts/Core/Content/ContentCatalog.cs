#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Content
{
    /// <summary>
    /// Immutable catalog of cargo, industry, vehicle, and achievement definitions.
    /// </summary>
    public sealed class ContentCatalog
    {
        private readonly ReadOnlyCollection<CargoDefinition> _cargo;
        private readonly ReadOnlyCollection<IndustryDefinition> _industries;
        private readonly ReadOnlyCollection<VehicleDefinition> _vehicles;
        private readonly ReadOnlyCollection<AchievementDefinition> _achievements;
        private readonly ReadOnlyDictionary<string, CargoDefinition> _cargoById;
        private readonly ReadOnlyDictionary<string, IndustryDefinition> _industriesById;
        private readonly ReadOnlyDictionary<string, VehicleDefinition> _vehiclesById;
        private readonly VehicleDefinition _startingRoad;
        private readonly VehicleDefinition _startingRail;
        private readonly VehicleDefinition _railWagon;

        public ContentCatalog(
            IReadOnlyList<CargoDefinition> cargo,
            IReadOnlyList<IndustryDefinition> industries,
            TownNeedsSettings? townNeeds = null,
            EconomySettings? economy = null,
            IReadOnlyList<VehicleDefinition>? vehicles = null,
            IReadOnlyList<AchievementDefinition>? achievements = null,
            ContractSettings? contracts = null,
            WorldGenerationData? worldGeneration = null)
        {
            CargoDefinition[] cargoCopy = CopyById(
                cargo,
                nameof(cargo),
                definition => definition.Id,
                out Dictionary<string, CargoDefinition> cargoById);
            IndustryDefinition[] industryCopy = CopyById(
                industries,
                nameof(industries),
                definition => definition.Id,
                out Dictionary<string, IndustryDefinition> industriesById);
            VehicleDefinition[] vehicleCopy = CopyById(
                vehicles ?? CreatePlaceholderVehicles(),
                nameof(vehicles),
                definition => definition.Id,
                out Dictionary<string, VehicleDefinition> vehiclesById);

            AchievementDefinition[] achievementCopy = CopyById(
                achievements ?? Array.Empty<AchievementDefinition>(),
                nameof(achievements),
                definition => definition.Id,
                out Dictionary<string, AchievementDefinition> _);
            for (int index = 0; index < achievementCopy.Length; index++)
            {
                AchievementDefinition achievement = achievementCopy[index];
                if (achievement.CargoId != null && !cargoById.ContainsKey(achievement.CargoId))
                {
                    throw new ArgumentException(
                        $"Achievement '{achievement.Id}' references unknown cargo id '{achievement.CargoId}'.",
                        nameof(achievements));
                }
            }

            _cargo = Array.AsReadOnly(cargoCopy);
            _achievements = Array.AsReadOnly(achievementCopy);
            _industries = Array.AsReadOnly(industryCopy);
            _vehicles = Array.AsReadOnly(vehicleCopy);
            _cargoById = new ReadOnlyDictionary<string, CargoDefinition>(cargoById);
            _industriesById = new ReadOnlyDictionary<string, IndustryDefinition>(industriesById);
            _vehiclesById = new ReadOnlyDictionary<string, VehicleDefinition>(vehiclesById);
            TownNeeds = townNeeds ?? TownNeedsSettings.CreatePlaceholder();
            Economy = economy ?? EconomySettings.CreatePlaceholder();
            Contracts = contracts ?? ContractSettings.CreatePlaceholder();
            WorldGeneration = worldGeneration ?? WorldGenerationData.CreatePlaceholder();
            if (worldGeneration != null)
            {
                // Explicit content fails fast on unknown industries or invalid counts; the placeholder is not checked.
                foreach (MapSize mapSize in MapSizes.All())
                {
                    _ = worldGeneration.CreateSettings(mapSize, industryCopy);
                }
            }

            _startingRoad = RequireUniqueStarting(vehicleCopy, VehicleMode.Road, "road");
            _startingRail = RequireUniqueStarting(vehicleCopy, VehicleMode.Rail, "rail");
            _railWagon = RequireUniqueMode(vehicleCopy, VehicleMode.RailWagon, "rail wagon");
        }

        /// <summary>
        /// Gets town population range and need-tier settings.
        /// </summary>
        public TownNeedsSettings TownNeeds { get; }

        /// <summary>
        /// Gets company starting cash.
        /// </summary>
        public EconomySettings Economy { get; }

        /// <summary>
        /// Gets contract generation and reward settings.
        /// </summary>
        public ContractSettings Contracts { get; }

        /// <summary>
        /// Gets the town and industry generation data for every map size.
        /// </summary>
        public WorldGenerationData WorldGeneration { get; }

        /// <summary>
        /// Gets the cargo definitions in source order.
        /// </summary>
        public IReadOnlyList<CargoDefinition> Cargo
        {
            get
            {
                return _cargo;
            }
        }

        /// <summary>
        /// Gets the industry definitions in source order.
        /// </summary>
        public IReadOnlyList<IndustryDefinition> Industries
        {
            get
            {
                return _industries;
            }
        }

        /// <summary>
        /// Gets the vehicle definitions in source order.
        /// </summary>
        public IReadOnlyList<VehicleDefinition> Vehicles
        {
            get
            {
                return _vehicles;
            }
        }

        /// <summary>
        /// Gets the achievement definitions in source order.
        /// </summary>
        public IReadOnlyList<AchievementDefinition> Achievements
        {
            get
            {
                return _achievements;
            }
        }

        /// <summary>
        /// Gets the unique starting road vehicle type offered by MapPlay Buy.
        /// </summary>
        public VehicleDefinition StartingRoadVehicle
        {
            get
            {
                return _startingRoad;
            }
        }

        /// <summary>
        /// Gets the unique starting rail locomotive type offered by MapPlay Buy.
        /// </summary>
        public VehicleDefinition StartingRailLocomotive
        {
            get
            {
                return _startingRail;
            }
        }

        /// <summary>
        /// Gets the shared rail wagon catalog entry.
        /// </summary>
        public VehicleDefinition RailWagon
        {
            get
            {
                return _railWagon;
            }
        }

        /// <summary>
        /// Looks up a cargo definition using an ordinal ID comparison.
        /// </summary>
        public bool TryGetCargo(string? id, out CargoDefinition? definition)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                definition = null;
                return false;
            }

            return _cargoById.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Looks up an industry definition using an ordinal ID comparison.
        /// </summary>
        public bool TryGetIndustry(string? id, out IndustryDefinition? definition)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                definition = null;
                return false;
            }

            return _industriesById.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Looks up a vehicle definition using an ordinal ID comparison.
        /// </summary>
        public bool TryGetVehicle(string? id, out VehicleDefinition? definition)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                definition = null;
                return false;
            }

            return _vehiclesById.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Returns monthly upkeep for a locomotive of <paramref name="locomotiveTypeId"/> plus wagons.
        /// </summary>
        public long MonthlyUpkeepForRail(string locomotiveTypeId, int wagonCount)
        {
            if (wagonCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wagonCount),
                    wagonCount,
                    "Wagon count cannot be negative.");
            }

            if (!TryGetVehicle(locomotiveTypeId, out VehicleDefinition? loco) || loco == null || loco.Mode != VehicleMode.Rail)
            {
                throw new ArgumentException(
                    $"Unknown rail locomotive type '{locomotiveTypeId}'.",
                    nameof(locomotiveTypeId));
            }

            return checked(loco.UpkeepPerMonth + (_railWagon.UpkeepPerMonth * wagonCount));
        }

        /// <summary>
        /// Returns the sell refund for a locomotive plus wagons.
        /// </summary>
        public long SellRefundForRail(string locomotiveTypeId, int wagonCount)
        {
            if (wagonCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wagonCount),
                    wagonCount,
                    "Wagon count cannot be negative.");
            }

            if (!TryGetVehicle(locomotiveTypeId, out VehicleDefinition? loco) || loco == null || loco.Mode != VehicleMode.Rail)
            {
                throw new ArgumentException(
                    $"Unknown rail locomotive type '{locomotiveTypeId}'.",
                    nameof(locomotiveTypeId));
            }

            return checked(
                EconomySettings.HalfRefund(loco.Purchase) +
                (EconomySettings.HalfRefund(_railWagon.Purchase) * wagonCount));
        }

        /// <summary>
        /// Placeholder starter truck, loco, and wagon matching the Issue #61 economy numbers.
        /// </summary>
        public static IReadOnlyList<VehicleDefinition> CreatePlaceholderVehicles()
        {
            return new[]
            {
                new VehicleDefinition(
                    "truck_mk1",
                    "vehicle.truck_mk1",
                    VehicleMode.Road,
                    8000L,
                    200L,
                    1900,
                    0L,
                    true,
                    cargoCapacity: 10,
                    speedThousandths: 250),
                new VehicleDefinition(
                    "loco_mk1",
                    "vehicle.loco_mk1",
                    VehicleMode.Rail,
                    20000L,
                    400L,
                    1900,
                    0L,
                    true,
                    maxWagons: 3,
                    capacityPerWagon: 10,
                    speedThousandths: 250),
                new VehicleDefinition(
                    "rail_wagon",
                    "vehicle.rail_wagon",
                    VehicleMode.RailWagon,
                    4000L,
                    100L,
                    1900,
                    0L,
                    true)
            };
        }

        private static VehicleDefinition RequireUniqueStarting(
            VehicleDefinition[] vehicles,
            VehicleMode mode,
            string label)
        {
            VehicleDefinition? found = null;
            for (int index = 0; index < vehicles.Length; index++)
            {
                VehicleDefinition vehicle = vehicles[index];
                if (vehicle.Mode != mode || !vehicle.Starting)
                {
                    continue;
                }

                if (found != null)
                {
                    throw new ArgumentException($"Catalog must contain exactly one starting {label} vehicle.");
                }

                found = vehicle;
            }

            if (found == null)
            {
                throw new ArgumentException($"Catalog must contain exactly one starting {label} vehicle.");
            }

            return found;
        }

        private static VehicleDefinition RequireUniqueMode(
            VehicleDefinition[] vehicles,
            VehicleMode mode,
            string label)
        {
            VehicleDefinition? found = null;
            for (int index = 0; index < vehicles.Length; index++)
            {
                VehicleDefinition vehicle = vehicles[index];
                if (vehicle.Mode != mode)
                {
                    continue;
                }

                if (found != null)
                {
                    throw new ArgumentException($"Catalog must contain exactly one {label} entry.");
                }

                found = vehicle;
            }

            if (found == null)
            {
                throw new ArgumentException($"Catalog must contain exactly one {label} entry.");
            }

            return found;
        }

        private static TDefinition[] CopyById<TDefinition>(
            IReadOnlyList<TDefinition> definitions,
            string parameterName,
            Func<TDefinition, string> getId,
            out Dictionary<string, TDefinition> definitionsById)
            where TDefinition : class
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (definitions is null)
            {
                throw new ArgumentNullException(parameterName);
            }
#pragma warning restore CA1510

            var copied = new TDefinition[definitions.Count];
            definitionsById = new Dictionary<string, TDefinition>(definitions.Count, StringComparer.Ordinal);
            for (int index = 0; index < definitions.Count; index++)
            {
                TDefinition? definition = definitions[index];
                if (definition is null)
                {
                    throw new ArgumentException("Definition collections cannot contain null entries.", parameterName);
                }

                string id = getId(definition);
                if (!definitionsById.TryAdd(id, definition))
                {
                    throw new ArgumentException($"Duplicate definition ID '{id}'.", parameterName);
                }

                copied[index] = definition;
            }

            return copied;
        }
    }
}
