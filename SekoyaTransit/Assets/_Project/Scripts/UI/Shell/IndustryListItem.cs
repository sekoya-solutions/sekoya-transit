#nullable enable
using System;
using System.Collections.Generic;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// UI-facing snapshot of one placed industry for the industries panel (Issue #52, Issue #282).
    /// Presentation builds these from Core; the shell never mutates simulation state.
    /// </summary>
    public readonly struct IndustryListItem : IEquatable<IndustryListItem>
    {
        private readonly CargoStackListLine[] _cargoStacks;
        private readonly string[] _outputCargoIds;

        /// <summary>
        /// Initializes an industry list row.
        /// </summary>
        public IndustryListItem(
            string industryId,
            string nameKey,
            int instanceIndex,
            int level,
            int cargoAmount,
            int cargoCapacity,
            long monthProduced,
            long monthTransported,
            int tileX = 0,
            int tileY = 0,
            IReadOnlyList<CargoStackListLine>? cargoStacks = null,
            long lastMonthProduced = 0L,
            long lastMonthTransported = 0L,
            bool canGrow = false,
            int growthTier = 0,
            int maxGrowthTier = 0,
            int servedMonths = 0,
            int monthsToGrow = 1,
            IReadOnlyList<string>? outputCargoIds = null)
        {
            if (string.IsNullOrWhiteSpace(industryId))
            {
                throw new ArgumentException("Industry id cannot be null or whitespace.", nameof(industryId));
            }

            if (string.IsNullOrWhiteSpace(nameKey))
            {
                throw new ArgumentException("Name key cannot be null or whitespace.", nameof(nameKey));
            }

            if (instanceIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(instanceIndex),
                    instanceIndex,
                    "Instance index cannot be negative.");
            }

            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Industry level must be positive.");
            }

            if (cargoAmount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cargoAmount), cargoAmount, "Cargo amount cannot be negative.");
            }

            if (cargoCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cargoCapacity),
                    cargoCapacity,
                    "Cargo capacity must be positive.");
            }

            if (cargoAmount > cargoCapacity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cargoAmount),
                    cargoAmount,
                    "Cargo amount cannot exceed capacity.");
            }

            if (monthProduced < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monthProduced),
                    monthProduced,
                    "Month produced cannot be negative.");
            }

            if (monthTransported < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monthTransported),
                    monthTransported,
                    "Month transported cannot be negative.");
            }

            if (lastMonthProduced < 0L || lastMonthTransported < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lastMonthProduced), "Last month figures cannot be negative.");
            }

            if (growthTier < 0 || maxGrowthTier < growthTier)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(growthTier),
                    growthTier,
                    "Growth tier must be between 0 and the maximum tier.");
            }

            if (servedMonths < 0 || monthsToGrow < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(servedMonths),
                    servedMonths,
                    "Served months cannot be negative and months to grow must be positive.");
            }

            if (outputCargoIds == null || outputCargoIds.Count == 0)
            {
                _outputCargoIds = Array.Empty<string>();
            }
            else
            {
                _outputCargoIds = new string[outputCargoIds.Count];
                for (int index = 0; index < outputCargoIds.Count; index++)
                {
                    _outputCargoIds[index] = outputCargoIds[index];
                }
            }

            if (cargoStacks == null || cargoStacks.Count == 0)
            {
                _cargoStacks = Array.Empty<CargoStackListLine>();
            }
            else
            {
                _cargoStacks = new CargoStackListLine[cargoStacks.Count];
                for (int index = 0; index < cargoStacks.Count; index++)
                {
                    _cargoStacks[index] = cargoStacks[index];
                }
            }

            IndustryId = industryId;
            NameKey = nameKey;
            InstanceIndex = instanceIndex;
            Level = level;
            CargoAmount = cargoAmount;
            CargoCapacity = cargoCapacity;
            MonthProduced = monthProduced;
            MonthTransported = monthTransported;
            TileX = tileX;
            TileY = tileY;
            LastMonthProduced = lastMonthProduced;
            LastMonthTransported = lastMonthTransported;
            CanGrow = canGrow;
            GrowthTier = growthTier;
            MaxGrowthTier = maxGrowthTier;
            ServedMonths = servedMonths;
            MonthsToGrow = monthsToGrow;
        }

        /// <summary>
        /// Gets the content industry definition id.
        /// </summary>
        public string IndustryId { get; }

        /// <summary>
        /// Gets the localization key for the industry display name.
        /// </summary>
        public string NameKey { get; }

        /// <summary>
        /// Gets the zero-based instance index within the definition.
        /// </summary>
        public int InstanceIndex { get; }

        /// <summary>
        /// Gets the industry chain level.
        /// </summary>
        public int Level { get; }

        /// <summary>
        /// Gets cargo units currently stocked at the industry.
        /// </summary>
        public int CargoAmount { get; }

        /// <summary>
        /// Gets maximum industry cargo capacity.
        /// </summary>
        public int CargoCapacity { get; }

        /// <summary>
        /// Gets cargo units produced in the current calendar month.
        /// </summary>
        public long MonthProduced { get; }

        /// <summary>
        /// Gets cargo units transported to stations in the current calendar month.
        /// </summary>
        public long MonthTransported { get; }

        /// <summary>
        /// Gets the industry anchor tile X (for map centering).
        /// </summary>
        public int TileX { get; }

        /// <summary>
        /// Gets the industry anchor tile Y (for map centering).
        /// </summary>
        public int TileY { get; }

        /// <summary>
        /// Gets cargo units produced in the previous calendar month.
        /// </summary>
        public long LastMonthProduced { get; }

        /// <summary>
        /// Gets cargo units delivered (loaded onto vehicles) in the previous calendar month.
        /// </summary>
        public long LastMonthTransported { get; }

        /// <summary>
        /// Gets whether the industry can grow (primary industries when growth is enabled).
        /// </summary>
        public bool CanGrow { get; }

        /// <summary>
        /// Gets the zero-based growth tier; the panel shows it from 1.
        /// </summary>
        public int GrowthTier { get; }

        /// <summary>
        /// Gets the highest zero-based growth tier.
        /// </summary>
        public int MaxGrowthTier { get; }

        /// <summary>
        /// Gets the consecutive well-served months counted toward the next tier.
        /// </summary>
        public int ServedMonths { get; }

        /// <summary>
        /// Gets the consecutive months needed for the next tier.
        /// </summary>
        public int MonthsToGrow { get; }

        /// <summary>
        /// Gets the cargo ids this industry outputs (their icons follow the amounts in the panel).
        /// </summary>
        public IReadOnlyList<string> OutputCargoIds => _outputCargoIds;

        /// <summary>
        /// Gets per-cargo stacks currently stocked (empty when none).
        /// </summary>
        public IReadOnlyList<CargoStackListLine> CargoStacks => _cargoStacks;

        /// <inheritdoc />
        public bool Equals(IndustryListItem other)
        {
            if (!string.Equals(IndustryId, other.IndustryId, StringComparison.Ordinal)
                || !string.Equals(NameKey, other.NameKey, StringComparison.Ordinal)
                || InstanceIndex != other.InstanceIndex
                || Level != other.Level
                || CargoAmount != other.CargoAmount
                || CargoCapacity != other.CargoCapacity
                || MonthProduced != other.MonthProduced
                || MonthTransported != other.MonthTransported
                || TileX != other.TileX
                || TileY != other.TileY
                || LastMonthProduced != other.LastMonthProduced
                || LastMonthTransported != other.LastMonthTransported
                || CanGrow != other.CanGrow
                || GrowthTier != other.GrowthTier
                || MaxGrowthTier != other.MaxGrowthTier
                || ServedMonths != other.ServedMonths
                || MonthsToGrow != other.MonthsToGrow
                || _cargoStacks.Length != other._cargoStacks.Length
                || _outputCargoIds.Length != other._outputCargoIds.Length)
            {
                return false;
            }

            for (int index = 0; index < _outputCargoIds.Length; index++)
            {
                if (!string.Equals(_outputCargoIds[index], other._outputCargoIds[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            for (int index = 0; index < _cargoStacks.Length; index++)
            {
                if (!_cargoStacks[index].Equals(other._cargoStacks[index]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is IndustryListItem other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = IndustryId.GetHashCode();
                hash = (hash * 397) ^ NameKey.GetHashCode();
                hash = (hash * 397) ^ InstanceIndex;
                hash = (hash * 397) ^ Level;
                hash = (hash * 397) ^ CargoAmount;
                hash = (hash * 397) ^ CargoCapacity;
                hash = (hash * 397) ^ MonthProduced.GetHashCode();
                hash = (hash * 397) ^ MonthTransported.GetHashCode();
                hash = (hash * 397) ^ TileX;
                hash = (hash * 397) ^ TileY;
                hash = (hash * 397) ^ LastMonthProduced.GetHashCode();
                hash = (hash * 397) ^ LastMonthTransported.GetHashCode();
                hash = (hash * 397) ^ GrowthTier;
                hash = (hash * 397) ^ ServedMonths;
                for (int index = 0; index < _cargoStacks.Length; index++)
                {
                    hash = (hash * 397) ^ _cargoStacks[index].GetHashCode();
                }

                return hash;
            }
        }
    }
}
