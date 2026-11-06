#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using SekoyaTransit.Content;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;
using SekoyaTransit.Presentation.Map;
using SekoyaTransit.Save;
using UnityEngine;

namespace SekoyaTransit.Presentation.Diagnostics
{
    /// <summary>
    /// Opens a save file the way the game does and describes it, so an agent can find what is wrong in a save a player
    /// downloaded with the Debug button (Issue #320). The file goes through the game's serializer, migrations and world
    /// restore; the world can then run a number of in-game days with the game's own simulation step.
    /// </summary>
    public static class SaveFileInspector
    {
        /// <summary>
        /// Loads the shipped content data from a folder holding the game's JSON files (for example
        /// <c>Assets/_Project/Content/Data</c>).
        /// </summary>
        public static ContentCatalog LoadCatalog(string contentDirectory)
        {
            if (string.IsNullOrEmpty(contentDirectory))
            {
                throw new ArgumentException("A content folder is required.", nameof(contentDirectory));
            }

            TextAsset Read(string name)
            {
                return new TextAsset(File.ReadAllText(Path.Combine(contentDirectory, name + ".json")));
            }

            return ContentLoader.Load(
                Read("cargo"),
                Read("industries"),
                Read("towns"),
                Read("economy"),
                Read("vehicles"),
                Read("achievements"),
                Read("contracts"),
                Read("worldgen"));
        }

        /// <summary>
        /// Same as <see cref="Inspect"/> with the content data loaded from <paramref name="contentDirectory"/> (the
        /// batchmode command, whose assembly does not reference Core).
        /// </summary>
        public static SaveFileReport InspectWithContent(byte[] data, string contentDirectory, int advanceDays)
        {
            return Inspect(data, LoadCatalog(contentDirectory), advanceDays);
        }

        /// <summary>
        /// Reads <paramref name="data"/> (save file bytes), restores its world, runs it <paramref name="advanceDays"/>
        /// in-game days (stopping early on bankruptcy) and returns the report. Throws the same exceptions a load in the
        /// game reports: <see cref="SaveFormatTooOldException"/>, <see cref="SaveFormatException"/>, or another one for
        /// a world that cannot be restored.
        /// </summary>
        public static SaveFileReport Inspect(byte[] data, ContentCatalog catalog, int advanceDays)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (catalog is null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (advanceDays < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(advanceDays), "The days to advance cannot be negative.");
            }

            var serializer = new JsonGzipSaveSerializer(CurrentSaveMigrations.CreatePipeline());
            SaveSnapshot loaded = serializer.Deserialize(data);
            GeneratedWorld world = WorldSave.Restore(loaded, catalog, new SimulationCalendar(MapPlaySession.TickDuration));
            var loop = new SimulationLoop(MapPlaySession.TickDuration, loaded.CurrentTick);
            var calendar = new SimulationCalendar(loop);
            var report = new StringBuilder();
            report.AppendLine("# Save file report");
            report.AppendLine();
            report.AppendLine("File: " + data.Length.ToString(CultureInfo.InvariantCulture) + " bytes, read as save format "
                + loaded.FormatVersion.ToString(CultureInfo.InvariantCulture) + " (current).");
            AppendState(report, "As loaded", world, loop.CurrentTick, calendar);
            string loadedJson = ToJson(serializer, loaded);
            string? advancedJson = null;
            if (advanceDays > 0)
            {
                GameDate start = calendar.GetDate(loop.CurrentTick);
                var events = new EventBus();
                var failures = new List<string>();
                void Step(Tick tick)
                {
                    WorldSimulationStep.Advance(world, catalog, calendar, tick, events);
                    EventDispatchResult result = events.DispatchPending();
                    for (int index = 0; index < result.Failures.Count; index++)
                    {
                        failures.Add(result.Failures[index].EventType.Name + ": " + result.Failures[index].Exception.Message);
                    }
                }

                while (start.DaysUntil(calendar.GetDate(loop.CurrentTick)) < advanceDays && !world.IsBankrupt)
                {
                    loop.Advance(loop.TickDuration, Step);
                }

                AppendState(report, "After " + advanceDays.ToString(CultureInfo.InvariantCulture) + " days", world, loop.CurrentTick, calendar);
                if (failures.Count > 0)
                {
                    report.AppendLine();
                    report.AppendLine("## Event observer failures");
                    report.AppendLine();
                    foreach (string failure in failures)
                    {
                        report.AppendLine("- " + failure);
                    }
                }

                advancedJson = ToJson(serializer, SaveSnapshot.Capture(world.MapSeed, loop.CurrentTick, world));
            }

            return new SaveFileReport(report.ToString(), loadedJson, advancedJson);
        }

        /// <summary>
        /// Gets the plain JSON of a snapshot: the save file content before compression.
        /// </summary>
        private static string ToJson(JsonGzipSaveSerializer serializer, SaveSnapshot snapshot)
        {
            byte[] compressed = serializer.Serialize(snapshot);
            using (var input = new MemoryStream(compressed))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip, new UTF8Encoding(false, true)))
            {
                return reader.ReadToEnd();
            }
        }

        private static void AppendState(StringBuilder report, string title, GeneratedWorld world, Tick tick, SimulationCalendar calendar)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            report.AppendLine();
            report.AppendLine("## " + title);
            report.AppendLine();
            report.AppendLine("- Date: " + calendar.GetDate(tick) + " (tick " + tick.Value.ToString(inv) + ")");
            report.AppendLine("- Seed: " + world.MapSeed.ToString(inv) + ", map " + world.Map.Size);
            report.AppendLine("- Cash: " + world.CompanyCash.ToString(inv) + (world.IsBankrupt ? " (bankrupt)" : string.Empty));
            report.AppendLine("- Towns: " + world.Towns.Count.ToString(inv) + ", industries: " + world.Industries.Count.ToString(inv));
            report.AppendLine("- Stations: " + world.RoadStations.Count.ToString(inv) + " road, "
                + world.RailStations.Count.ToString(inv) + " rail");
            report.AppendLine("- Unlocked vehicle types: " + string.Join(", ", world.UnlockedVehicleTypeIds)
                + (world.VehicleYearGateLifted ? " (year gate lifted)" : string.Empty));

            report.AppendLine();
            report.AppendLine("### Routes (" + world.Routes.Count.ToString(inv) + ")");
            report.AppendLine();
            foreach (Route route in world.Routes)
            {
                report.AppendLine("- #" + route.Id.ToString(inv) + " " + route.Name + " (" + route.Mode + "): "
                    + CountRoadVehicles(world, route.Id).ToString(inv) + " road and "
                    + CountRailVehicles(world, route.Id).ToString(inv) + " rail vehicles, last month revenue "
                    + route.LastMonthRevenue.ToString(inv) + ", running cost " + route.LastMonthRunningCost.ToString(inv));
            }

            report.AppendLine();
            report.AppendLine("### Road vehicles (" + world.RoadVehicles.Count.ToString(inv) + ")");
            report.AppendLine();
            foreach (RoadVehicle vehicle in world.RoadVehicles)
            {
                report.AppendLine("- #" + vehicle.Id.ToString(inv) + " " + vehicle.TypeId + ", route " + vehicle.RouteId.ToString(inv)
                    + ", order " + vehicle.OrderIndex.ToString(inv) + "/" + vehicle.OrderStationIds.Count.ToString(inv)
                    + ", cargo " + vehicle.Cargo.TotalAmount.ToString(inv) + "/" + vehicle.CargoCapacity.ToString(inv)
                    + VehicleFlags(vehicle.IsStuck, vehicle.IsOnEdge, vehicle.IsServicingStop)
                    + ", lifetime income " + vehicle.LifetimeDeliveryIncome.ToString(inv));
            }

            report.AppendLine();
            report.AppendLine("### Rail vehicles (" + world.RailVehicles.Count.ToString(inv) + ")");
            report.AppendLine();
            foreach (RailVehicle vehicle in world.RailVehicles)
            {
                report.AppendLine("- #" + vehicle.Id.ToString(inv) + " " + vehicle.TypeId + " with "
                    + vehicle.WagonCount.ToString(inv) + " wagons, route " + vehicle.RouteId.ToString(inv)
                    + ", order " + vehicle.OrderIndex.ToString(inv)
                    + ", cargo " + vehicle.Cargo.TotalAmount.ToString(inv) + "/" + vehicle.CargoCapacity.ToString(inv)
                    + VehicleFlags(vehicle.IsStuck, vehicle.IsOnEdge, vehicle.IsServicingStop)
                    + ", lifetime income " + vehicle.LifetimeDeliveryIncome.ToString(inv));
            }

            report.AppendLine();
            report.AppendLine("### Contracts (" + world.ContractList.Count.ToString(inv) + ")");
            report.AppendLine();
            foreach (Contract contract in world.ContractList)
            {
                report.AppendLine("- #" + contract.Id.ToString(inv) + " " + contract.Status + ": "
                    + contract.Delivered.ToString(inv) + "/" + contract.Amount.ToString(inv) + " " + contract.CargoId
                    + " to town " + contract.TownIndex.ToString(inv) + ", deadline " + contract.Deadline);
            }
        }

        private static string VehicleFlags(bool stuck, bool onEdge, bool servicing)
        {
            return (stuck ? ", STUCK" : string.Empty)
                + (onEdge ? ", moving" : string.Empty)
                + (servicing ? ", at a stop" : string.Empty);
        }

        private static int CountRoadVehicles(GeneratedWorld world, int routeId)
        {
            int count = 0;
            foreach (RoadVehicle vehicle in world.RoadVehicles)
            {
                if (vehicle.RouteId == routeId)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountRailVehicles(GeneratedWorld world, int routeId)
        {
            int count = 0;
            foreach (RailVehicle vehicle in world.RailVehicles)
            {
                if (vehicle.RouteId == routeId)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// What <see cref="SaveFileInspector.Inspect"/> found: a readable Markdown summary and the full state as plain JSON,
    /// as loaded and (when days were run) at the end.
    /// </summary>
    public sealed class SaveFileReport
    {
        public SaveFileReport(string summary, string loadedStateJson, string? advancedStateJson)
        {
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            LoadedStateJson = loadedStateJson ?? throw new ArgumentNullException(nameof(loadedStateJson));
            AdvancedStateJson = advancedStateJson;
        }

        /// <summary>
        /// Gets the Markdown summary: date, cash, routes, vehicles and contracts.
        /// </summary>
        public string Summary { get; }

        /// <summary>
        /// Gets the full state as loaded, as the save's plain JSON.
        /// </summary>
        public string LoadedStateJson { get; }

        /// <summary>
        /// Gets the full state after the days run, or null when none were run.
        /// </summary>
        public string? AdvancedStateJson { get; }
    }
}
