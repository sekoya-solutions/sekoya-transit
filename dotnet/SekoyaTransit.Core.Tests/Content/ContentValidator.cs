#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Json.Schema;

namespace SekoyaTransit.Core.Tests.Content
{
    /// <summary>
    /// Validates content JSON files against their schemas and checks cross-file referential integrity (ADR 0010).
    /// Test-only: the game runtime never loads or validates schemas.
    /// </summary>
    internal static class ContentValidator
    {
        private static readonly string _contentRoot = Path.Combine(AppContext.BaseDirectory, "Content");

        public static string ReadData(string fileName) => File.ReadAllText(Path.Combine(_contentRoot, "Data", fileName));

        public static JsonSchema LoadSchema(string fileName) =>
            JsonSchema.FromText(File.ReadAllText(Path.Combine(_contentRoot, "Schemas", fileName)));

        public static IReadOnlyList<string> ValidateSchema(JsonSchema schema, string json)
        {
            var options = new EvaluationOptions { OutputFormat = OutputFormat.List };
            EvaluationResults results = schema.Evaluate(JsonNode.Parse(json), options);
            if (results.IsValid)
            {
                return Array.Empty<string>();
            }

            var errors = new List<string>();
            foreach (EvaluationResults detail in results.Details.Where(d => !d.IsValid && d.Errors != null))
            {
                foreach (KeyValuePair<string, string> error in detail.Errors!)
                {
                    errors.Add(detail.InstanceLocation + ": " + error.Value);
                }
            }

            return errors;
        }

        public static IReadOnlyList<string> ValidateIntegrity(
            string cargoJson,
            string industriesJson,
            string? vehiclesJson = null,
            string? achievementsJson = null)
        {
            var errors = new List<string>();
            JsonArray cargo = JsonNode.Parse(cargoJson)!["cargo"]!.AsArray();
            JsonArray industries = JsonNode.Parse(industriesJson)!["industries"]!.AsArray();

            var cargoClasses = new Dictionary<string, string>();
            var cargoManufacturingLevels = new Dictionary<string, int>();
            foreach (JsonNode? entry in cargo)
            {
                string id = entry!["id"]!.GetValue<string>();
                string cargoClass = entry["class"]!.GetValue<string>();
                if (!cargoClasses.TryAdd(id, cargoClass))
                {
                    errors.Add($"Duplicate cargo id '{id}'.");
                }

                int manufacturingLevel = entry["manufacturingLevel"]!.GetValue<int>();
                cargoManufacturingLevels[id] = manufacturingLevel;
                if (cargoClass == "passengers" && manufacturingLevel != 0)
                {
                    errors.Add($"Passenger cargo '{id}' must have manufacturingLevel 0.");
                }

                if (cargoClass == "cargo" && (manufacturingLevel < 1 || manufacturingLevel > 3))
                {
                    errors.Add($"Freight cargo '{id}' must have manufacturingLevel 1–3.");
                }
            }

            var industryIds = new HashSet<string>();
            var producedCargo = new HashSet<string>();
            var producerLevels = new Dictionary<string, HashSet<int>>();
            var edges = new Dictionary<string, HashSet<string>>();

            foreach (JsonNode? industry in industries)
            {
                int level = industry!["level"]!.GetValue<int>();
                foreach (JsonNode? output in industry["outputs"]!.AsArray())
                {
                    string cargoId = output!["cargo"]!.GetValue<string>();
                    if (!producerLevels.TryGetValue(cargoId, out HashSet<int>? levels))
                    {
                        levels = new HashSet<int>();
                        producerLevels.Add(cargoId, levels);
                    }

                    levels.Add(level);
                }
            }

            foreach (JsonNode? industry in industries)
            {
                string id = industry!["id"]!.GetValue<string>();
                if (!industryIds.Add(id))
                {
                    errors.Add($"Duplicate industry id '{id}'.");
                }

                JsonArray inputs = industry["inputs"]!.AsArray();
                JsonArray outputs = industry["outputs"]!.AsArray();
                int level = industry["level"]!.GetValue<int>();

                if (level == 1 && inputs.Count > 0)
                {
                    errors.Add($"Industry '{id}' is level 1 (primary) but declares inputs.");
                }

                if (level > 1 && inputs.Count == 0)
                {
                    errors.Add($"Industry '{id}' is level {level} but declares no inputs.");
                }

                var inputIds = CargoIds(id, "input", inputs, cargoClasses, errors);
                var outputIds = CargoIds(id, "output", outputs, cargoClasses, errors);
                producedCargo.UnionWith(outputIds);

                foreach (string input in inputIds)
                {
                    if (cargoClasses.ContainsKey(input) &&
                        (!producerLevels.TryGetValue(input, out HashSet<int>? levels) ||
                         !levels.Any(producerLevel => producerLevel < level)))
                    {
                        errors.Add($"Industry '{id}' consumes cargo '{input}' at level {level}, but it is not produced by any industry at a lower level.");
                    }
                }

                foreach (string input in inputIds)
                {
                    if (!edges.TryGetValue(input, out HashSet<string>? targets))
                    {
                        targets = new HashSet<string>();
                        edges[input] = targets;
                    }

                    targets.UnionWith(outputIds);
                }
            }

            foreach (KeyValuePair<string, string> entry in cargoClasses)
            {
                if (entry.Value == "cargo" && !producedCargo.Contains(entry.Key))
                {
                    errors.Add($"Cargo '{entry.Key}' is not produced by any industry.");
                }
            }

            foreach (KeyValuePair<string, HashSet<int>> produced in producerLevels)
            {
                if (!cargoManufacturingLevels.TryGetValue(produced.Key, out int manufacturingLevel))
                {
                    continue;
                }

                if (produced.Value.Count == 1 && !produced.Value.Contains(manufacturingLevel))
                {
                    errors.Add(
                        $"Cargo '{produced.Key}' manufacturingLevel {manufacturingLevel} does not match its sole producer industry level.");
                }
            }

            string? cycle = FindCycle(edges);
            if (cycle != null)
            {
                errors.Add($"Production chain cycle: {cycle}.");
            }

            if (vehiclesJson != null)
            {
                ValidateVehicles(vehiclesJson, errors);
            }

            if (achievementsJson != null)
            {
                ValidateAchievements(achievementsJson, cargoClasses.Keys, errors);
            }

            return errors;
        }

        private static void ValidateAchievements(
            string achievementsJson,
            IEnumerable<string> cargoIds,
            List<string> errors)
        {
            JsonArray achievements = JsonNode.Parse(achievementsJson)!["achievements"]!.AsArray();
            var knownCargo = new HashSet<string>(cargoIds, StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int globalTracks = 0;
            foreach (JsonNode? entry in achievements)
            {
                string id = entry!["id"]!.GetValue<string>();
                if (!ids.Add(id))
                {
                    errors.Add($"Duplicate achievement id '{id}'.");
                }

                if (entry["kind"]!.GetValue<string>() == "achievementsUnlocked")
                {
                    globalTracks++;
                }

                long previousGoal = 0L;
                foreach (JsonNode? milestone in entry["milestones"]!.AsArray())
                {
                    long goal = milestone!["goal"]!.GetValue<long>();
                    if (goal <= previousGoal)
                    {
                        errors.Add($"Achievement '{id}' milestone goals must strictly increase.");
                    }

                    previousGoal = goal;
                }

                string? cargo = entry["cargo"]?.GetValue<string>();
                if (cargo != null && !knownCargo.Contains(cargo))
                {
                    errors.Add($"Achievement '{id}' references unknown cargo '{cargo}'.");
                }
            }

            if (globalTracks > 1)
            {
                errors.Add("Only one achievementsUnlocked track is allowed.");
            }
        }

        private static void ValidateVehicles(string vehiclesJson, List<string> errors)
        {
            JsonArray vehicles = JsonNode.Parse(vehiclesJson)!["vehicles"]!.AsArray();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int startingRoad = 0;
            int startingRail = 0;
            int railWagon = 0;

            foreach (JsonNode? entry in vehicles)
            {
                string id = entry!["id"]!.GetValue<string>();
                if (!ids.Add(id))
                {
                    errors.Add($"Duplicate vehicle id '{id}'.");
                }

                string mode = entry["mode"]!.GetValue<string>();
                bool starting = entry["starting"]!.GetValue<bool>();
                switch (mode)
                {
                    case "road":
                        if (entry["cargoCapacity"] == null || entry["speedThousandths"] == null)
                        {
                            errors.Add($"Road vehicle '{id}' requires cargoCapacity and speedThousandths.");
                        }

                        if (starting)
                        {
                            startingRoad++;
                        }

                        break;

                    case "rail":
                        if (entry["maxWagons"] == null ||
                            entry["capacityPerWagon"] == null ||
                            entry["speedThousandths"] == null)
                        {
                            errors.Add($"Rail locomotive '{id}' requires maxWagons, capacityPerWagon, and speedThousandths.");
                        }

                        if (starting)
                        {
                            startingRail++;
                        }

                        break;

                    case "rail_wagon":
                        railWagon++;
                        if (entry["cargoCapacity"] != null ||
                            entry["maxWagons"] != null ||
                            entry["capacityPerWagon"] != null ||
                            entry["speedThousandths"] != null)
                        {
                            errors.Add($"Rail wagon '{id}' must not set capacity, wagon, or speed fields.");
                        }

                        break;

                    default:
                        errors.Add($"Vehicle '{id}' has unsupported mode '{mode}'.");
                        break;
                }
            }

            if (startingRoad != 1)
            {
                errors.Add("Catalog must contain exactly one starting road vehicle.");
            }

            if (startingRail != 1)
            {
                errors.Add("Catalog must contain exactly one starting rail locomotive.");
            }

            if (railWagon != 1)
            {
                errors.Add("Catalog must contain exactly one rail_wagon entry.");
            }
        }

        private static List<string> CargoIds(
            string industryId,
            string role,
            JsonArray amounts,
            Dictionary<string, string> cargoClasses,
            List<string> errors)
        {
            var ids = new List<string>();
            foreach (JsonNode? amount in amounts)
            {
                string cargoId = amount!["cargo"]!.GetValue<string>();
                if (!cargoClasses.ContainsKey(cargoId))
                {
                    errors.Add($"Industry '{industryId}' {role} references unknown cargo '{cargoId}'.");
                }

                if (ids.Contains(cargoId))
                {
                    errors.Add($"Industry '{industryId}' lists {role} cargo '{cargoId}' more than once.");
                }

                ids.Add(cargoId);
            }

            return ids;
        }

        private static string? FindCycle(Dictionary<string, HashSet<string>> edges)
        {
            var state = new Dictionary<string, int>();
            var path = new List<string>();

            string? Visit(string node)
            {
                state[node] = 1;
                path.Add(node);
                if (edges.TryGetValue(node, out HashSet<string>? targets))
                {
                    foreach (string target in targets.OrderBy(t => t, StringComparer.Ordinal))
                    {
                        state.TryGetValue(target, out int targetState);
                        if (targetState == 1)
                        {
                            return string.Join(" -> ", path.SkipWhile(p => p != target).Append(target));
                        }

                        if (targetState == 0)
                        {
                            string? found = Visit(target);
                            if (found != null)
                            {
                                return found;
                            }
                        }
                    }
                }

                path.RemoveAt(path.Count - 1);
                state[node] = 2;
                return null;
            }

            foreach (string node in edges.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (!state.ContainsKey(node))
                {
                    string? found = Visit(node);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }
    }
}
