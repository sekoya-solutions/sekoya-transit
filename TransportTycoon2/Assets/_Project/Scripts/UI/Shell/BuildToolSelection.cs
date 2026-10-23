#nullable enable
using System;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Pure helpers for mutually exclusive MapPlay build-tool selection (Issue #50 / #54).
    /// </summary>
    public static class BuildToolSelection
    {
        /// <summary>
        /// Resolves the active tool from Presentation mode flags (at most one should be true).
        /// </summary>
        public static BuildToolId FromModes(
            bool roadBuild,
            bool roadDemolish,
            bool railBuild,
            bool railDemolish,
            bool stationBuild,
            bool stationDemolish,
            bool railStationBuild,
            bool railStationDemolish)
        {
            if (roadBuild)
            {
                return BuildToolId.Road;
            }

            if (roadDemolish)
            {
                return BuildToolId.DemolishRoad;
            }

            if (railBuild)
            {
                return BuildToolId.Rail;
            }

            if (railDemolish)
            {
                return BuildToolId.DemolishRail;
            }

            if (stationBuild)
            {
                return BuildToolId.Station;
            }

            if (stationDemolish)
            {
                return BuildToolId.DemolishStation;
            }

            if (railStationBuild)
            {
                return BuildToolId.RailStation;
            }

            if (railStationDemolish)
            {
                return BuildToolId.DemolishRailStation;
            }

            return BuildToolId.Select;
        }

        /// <summary>
        /// Maps a tool id to Presentation mode flags.
        /// </summary>
        public static void ToModes(
            BuildToolId tool,
            out bool roadBuild,
            out bool roadDemolish,
            out bool railBuild,
            out bool railDemolish,
            out bool stationBuild,
            out bool stationDemolish,
            out bool railStationBuild,
            out bool railStationDemolish)
        {
            roadBuild = tool == BuildToolId.Road;
            roadDemolish = tool == BuildToolId.DemolishRoad;
            railBuild = tool == BuildToolId.Rail;
            railDemolish = tool == BuildToolId.DemolishRail;
            stationBuild = tool == BuildToolId.Station;
            stationDemolish = tool == BuildToolId.DemolishStation;
            railStationBuild = tool == BuildToolId.RailStation;
            railStationDemolish = tool == BuildToolId.DemolishRailStation;
        }

        /// <summary>
        /// Keyboard-style toggle: pressing the active tool again returns <see cref="BuildToolId.Select"/>.
        /// </summary>
        public static BuildToolId Toggle(BuildToolId current, BuildToolId pressed)
        {
            if (!Enum.IsDefined(typeof(BuildToolId), pressed))
            {
                throw new ArgumentOutOfRangeException(nameof(pressed), pressed, "Unknown build tool.");
            }

            if (pressed == BuildToolId.Select)
            {
                return BuildToolId.Select;
            }

            return current == pressed ? BuildToolId.Select : pressed;
        }
    }
}
