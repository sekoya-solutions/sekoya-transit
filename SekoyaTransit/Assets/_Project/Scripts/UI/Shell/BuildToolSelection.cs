#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Pure helpers for mutually exclusive MapPlay build-tool selection (Issue #50 / #54 / #181).
    /// </summary>
    public static class BuildToolSelection
    {
        /// <summary>
        /// Resolves the active tool from Presentation mode flags (at most one should be true).
        /// </summary>
        public static BuildToolId FromModes(
            bool roadBuild,
            bool railBuild,
            bool stationBuild,
            bool railStationBuild,
            bool demolish)
        {
            if (roadBuild)
            {
                return BuildToolId.Road;
            }

            if (railBuild)
            {
                return BuildToolId.Rail;
            }

            if (stationBuild)
            {
                return BuildToolId.Station;
            }

            if (railStationBuild)
            {
                return BuildToolId.RailStation;
            }

            if (demolish)
            {
                return BuildToolId.Demolish;
            }

            return BuildToolId.Select;
        }

        /// <summary>
        /// Maps a tool id to Presentation mode flags.
        /// </summary>
        public static void ToModes(
            BuildToolId tool,
            out bool roadBuild,
            out bool railBuild,
            out bool stationBuild,
            out bool railStationBuild,
            out bool demolish)
        {
            roadBuild = tool == BuildToolId.Road;
            railBuild = tool == BuildToolId.Rail;
            stationBuild = tool == BuildToolId.Station;
            railStationBuild = tool == BuildToolId.RailStation;
            demolish = tool == BuildToolId.Demolish;
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
