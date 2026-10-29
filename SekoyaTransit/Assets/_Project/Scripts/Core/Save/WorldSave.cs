#nullable enable
using System;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Applies persisted map channels from a <see cref="SaveSnapshot"/> onto a live world.
    /// </summary>
    public static class WorldSave
    {
        /// <summary>
        /// Copies road and rail tile channels from the snapshot when present.
        /// </summary>
        public static void ApplyTransportTileChannels(TileMap map, SaveSnapshot snapshot)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
#pragma warning restore CA1510

            if (map.Size != snapshot.MapSize)
            {
                throw new ArgumentException("Snapshot map size does not match the target map.", nameof(snapshot));
            }

            if (snapshot.HasRoadTypeIds)
            {
                map.CopyRoadTypeIdsFrom(snapshot.RoadTypeIds);
            }

            if (snapshot.HasRailTypeIds)
            {
                map.CopyRailTypeIdsFrom(snapshot.RailTypeIds);
            }
        }

        /// <summary>
        /// Rebuilds road and rail graphs from the map after transport channels were applied.
        /// </summary>
        public static void RebuildTransportGraphs(GeneratedWorld world)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            world.RoadGraph.RebuildFromMap(world.Map);
            world.RailGraph.RebuildFromMap(world.Map);
        }

        /// <summary>
        /// Applies transport tile channels and rebuilds derived graphs.
        /// </summary>
        public static void ApplyTransportChannels(GeneratedWorld world, SaveSnapshot snapshot)
        {
            ApplyTransportTileChannels(world.Map, snapshot);
            RebuildTransportGraphs(world);
        }

        /// <summary>
        /// Rebuilds a running world from a current-format snapshot (Issue #183). Everything the snapshot persists is
        /// restored; derived data (road and rail graphs, route caches, vehicle paths) is rebuilt from the map. The
        /// snapshot is not modified or shared with the world. The contract and vehicle-upkeep month markers follow the
        /// snapshot tick, because a save is taken after that tick's monthly processing already ran.
        /// </summary>
        /// <param name="snapshot">A snapshot at <see cref="SaveSnapshot.CurrentFormatVersion"/>.</param>
        /// <param name="catalog">The content catalog; contracts that no longer fit it are dropped.</param>
        /// <param name="calendar">Converts the snapshot tick to the date that the month markers follow.</param>
        public static GeneratedWorld Restore(SaveSnapshot snapshot, ContentCatalog catalog, SimulationCalendar calendar)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (calendar == null)
            {
                throw new ArgumentNullException(nameof(calendar));
            }
#pragma warning restore CA1510

            if (snapshot.FormatVersion != SaveSnapshot.CurrentFormatVersion)
            {
                throw new ArgumentException(
                    $"Only format version {SaveSnapshot.CurrentFormatVersion} snapshots can be restored.",
                    nameof(snapshot));
            }

            var map = new TileMap(snapshot.MapSize);
            ReadOnlySpan<int> terrain = snapshot.TerrainTypeIds;
            ReadOnlySpan<int> objects = snapshot.ObjectIds;
            ReadOnlySpan<int> owners = snapshot.OwnerIds;
            for (int tileId = 0; tileId < map.TileCount; tileId++)
            {
                TileCoordinate coordinate = map.GetCoordinate(tileId);
                map.SetTerrainTypeId(coordinate, terrain[tileId]);
                map.SetObjectId(coordinate, objects[tileId]);
                map.SetOwnerId(coordinate, owners[tileId]);
            }

            ApplyTransportTileChannels(map, snapshot);

            var towns = new Town[snapshot.Towns.Count];
            for (int index = 0; index < towns.Length; index++)
            {
                towns[index] = snapshot.Towns[index].Copy();
            }

            // Saves before format 23 have unnamed towns without need plans (Issue #193).
            TownNeedsAssignment.AssignMissingNamesAndPlans(towns, snapshot.MapSeed, catalog.TownNeeds, catalog);

            var industries = new PlacedIndustry[snapshot.Industries.Count];
            for (int index = 0; index < industries.Length; index++)
            {
                industries[index] = snapshot.Industries[index].Copy();
                MoveLegacyStockedInputs(industries[index], catalog);
            }

            var world = new GeneratedWorld(map, towns, industries, snapshot.MapSeed, snapshot.CompanyCash);
            for (int index = 0; index < snapshot.RoadStations.Count; index++)
            {
                RoadStation station = snapshot.RoadStations[index];
                world.RestoreRoadStation(new RoadStation(station.Id, station.Coordinate));
            }

            for (int index = 0; index < snapshot.RailStations.Count; index++)
            {
                RailStation station = snapshot.RailStations[index];
                world.RestoreRailStation(new RailStation(station.Id, station.Coordinate));
            }

            RebuildTransportGraphs(world);
            for (int index = 0; index < snapshot.RoadVehicles.Count; index++)
            {
                world.RestoreRoadVehicle(snapshot.RoadVehicles[index].Copy());
            }

            for (int index = 0; index < snapshot.RailVehicles.Count; index++)
            {
                world.RestoreRailVehicle(snapshot.RailVehicles[index].Copy());
            }

            world.SetLastUpkeepMonth(snapshot.LastUpkeepYear, snapshot.LastUpkeepMonth);
            world.RestoreUnlockedVehicleTypes(snapshot.UnlockedVehicleTypeIds);
            world.RestoreAchievementProgress(snapshot.AchievementCompletions, snapshot.CargoDeliveryCounters);
            FinanceLedger ledger = snapshot.FinanceLedger;
            world.Finance.Restore(ledger.Year, ledger.Current, ledger.Previous);
            world.RestoreContracts(
                snapshot.OpenContracts,
                snapshot.NextContractId,
                snapshot.ContractRandomState,
                catalog);
            if (!snapshot.CurrentTick.Equals(Tick.Zero))
            {
                GameDate date = calendar.GetDate(snapshot.CurrentTick);
                world.SetLastContractMonth(date.Year, date.Month);
            }

            return world;
        }

        /// <summary>
        /// Saves before format 23 kept delivered inputs (and unpaid foreign cargo) in industry stock. Industry stock
        /// now holds outputs only (Issue #193): inputs are delivered as if they had just arrived, and other non-output
        /// cargo is dropped. A format 23 industry holds outputs only, so this changes nothing for it.
        /// </summary>
        private static void MoveLegacyStockedInputs(PlacedIndustry industry, ContentCatalog catalog)
        {
            if (!catalog.TryGetIndustry(industry.IndustryId, out IndustryDefinition? definition) || definition == null)
            {
                return;
            }

            for (int index = industry.Cargo.Stacks.Count - 1; index >= 0; index--)
            {
                CargoAmount stack = industry.Cargo.Stacks[index];
                if (IsOutput(definition, stack.CargoId))
                {
                    continue;
                }

                int removed = industry.Cargo.TryRemove(stack.CargoId, stack.Amount);
                if (removed > 0 && IndustryProduction.IsInput(definition, stack.CargoId))
                {
                    IndustryProduction.TryDeliverInput(industry, definition, stack.CargoId, removed, null);
                }
            }
        }

        private static bool IsOutput(IndustryDefinition definition, string cargoId)
        {
            for (int index = 0; index < definition.Outputs.Count; index++)
            {
                if (string.Equals(definition.Outputs[index].CargoId, cargoId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
