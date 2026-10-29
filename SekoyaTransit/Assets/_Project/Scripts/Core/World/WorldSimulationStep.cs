#nullable enable
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// The per-tick simulation step shared by the game loop and the save round-trip tests (Issue #183).
    /// </summary>
    public static class WorldSimulationStep
    {
        /// <summary>
        /// Advances every world system by one simulation tick; events are queued on <paramref name="events"/> and
        /// dispatched by the caller.
        /// </summary>
        public static void Advance(
            GeneratedWorld world,
            ContentCatalog catalog,
            SimulationCalendar calendar,
            Tick tick,
            EventBus events)
        {
            GameDate date = calendar.GetDate(tick);
            // Roll the finance year first so the first tick of a year books its money into the new year.
            world.Finance.AdvanceTo(date.Year);
            // Primary industries read their finished month before loading or production resets the stats (Issue #73).
            IndustryGrowth.AdvanceAll(world, catalog, date);
            long deliveryIncomeBefore = world.Finance.Current.Get(FinanceCategory.DeliveryIncome);
            // Shared load pass: road+rail unload/travel, industry+town Distribute, then finish.
            RoadVehicleMovement.AdvanceUnloadOrTravelAll(world, catalog, date);
            RailVehicleMovement.AdvanceUnloadOrTravelAll(world, catalog, date);
            IndustryCargoLoading.Distribute(world, catalog, date);
            TownCargoLoading.Distribute(world, catalog, date);
            RoadVehicleMovement.FinishServicesAll(world, catalog);
            RailVehicleMovement.FinishServicesAll(world, catalog);
            long deliveryIncome = world.Finance.Current.Get(FinanceCategory.DeliveryIncome) - deliveryIncomeBefore;
            if (deliveryIncome > 0)
            {
                events.Publish(new DeliveryIncomeEvent(deliveryIncome, date));
            }

            IndustryProduction.AdvanceAll(world, catalog, tick, date);
            TownPassengerGeneration.AdvanceAll(world, catalog, tick);
            TownGrowth.AdvanceAll(world, catalog, date);
            VehicleUpkeep.AdvanceAll(world, catalog, date);
            Contracts.AdvanceAll(world, catalog, date, events);
            Achievements.AdvanceAll(world, catalog, date, events);
        }
    }
}
