#nullable enable
using System;
using SekoyaTransit.Core.Content;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Year and permanent-unlock rules for vehicle types (Issue #63).
    /// </summary>
    public static class VehicleAvailability
    {
        /// <summary>
        /// Returns whether the calendar year has reached the type's <see cref="VehicleDefinition.AvailableYear"/>.
        /// </summary>
        public static bool IsYearReached(VehicleDefinition definition, int calendarYear)
        {
#pragma warning disable CA1510
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
#pragma warning restore CA1510

            return calendarYear >= definition.AvailableYear;
        }

        /// <summary>
        /// Returns whether the type is available for <paramref name="world"/> in <paramref name="calendarYear"/>: its year
        /// is reached, or the unlock-all cheat lifted the year gate (Issue #316).
        /// </summary>
        public static bool IsAvailable(VehicleDefinition definition, GeneratedWorld world, int calendarYear)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            return IsYearReached(definition, calendarYear) || world.VehicleYearGateLifted;
        }

        /// <summary>
        /// Returns whether the company may treat the type as unlocked (starters need no unlock entry).
        /// </summary>
        public static bool IsUnlocked(VehicleDefinition definition, GeneratedWorld world)
        {
#pragma warning disable CA1510
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            if (definition.Starting)
            {
                return true;
            }

            return world.IsVehicleTypeUnlocked(definition.Id);
        }

        /// <summary>
        /// Returns whether a road truck or rail locomotive may be purchased now.
        /// </summary>
        public static bool CanPurchase(VehicleDefinition definition, GeneratedWorld world, int calendarYear)
        {
            if (definition.Mode != VehicleMode.Road && definition.Mode != VehicleMode.Rail)
            {
                return false;
            }

            return IsAvailable(definition, world, calendarYear) && IsUnlocked(definition, world);
        }

        /// <summary>
        /// Returns whether the type can be permanently unlocked with money now.
        /// </summary>
        public static bool CanUnlock(VehicleDefinition definition, GeneratedWorld world, int calendarYear)
        {
            if (definition.Mode != VehicleMode.Road && definition.Mode != VehicleMode.Rail)
            {
                return false;
            }

            if (definition.Starting)
            {
                return false;
            }

            if (!IsAvailable(definition, world, calendarYear))
            {
                return false;
            }

            return !world.IsVehicleTypeUnlocked(definition.Id);
        }
    }
}
