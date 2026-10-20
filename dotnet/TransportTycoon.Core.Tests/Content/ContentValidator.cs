#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Json.Schema;

namespace TransportTycoon.Core.Tests.Content
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

        public static IReadOnlyList<string> ValidateIntegrity(string cargoJson, string industriesJson)
        {
            var errors = new List<string>();
            JsonArray cargo = JsonNode.Parse(cargoJson)!["cargo"]!.AsArray();
            JsonArray industries = JsonNode.Parse(industriesJson)!["industries"]!.AsArray();

            var cargoClasses = new Dictionary<string, string>();
            foreach (JsonNode? entry in cargo)
            {
                string id = entry!["id"]!.GetValue<string>();
                if (!cargoClasses.TryAdd(id, entry["class"]!.GetValue<string>()))
                {
                    errors.Add($"Duplicate cargo id '{id}'.");
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

            string? cycle = FindCycle(edges);
            if (cycle != null)
            {
                errors.Add($"Production chain cycle: {cycle}.");
            }

            return errors;
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
