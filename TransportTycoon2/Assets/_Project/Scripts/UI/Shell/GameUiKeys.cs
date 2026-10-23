#nullable enable

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Localization table and entry keys for the MapPlay shell (English-only in V1).
    /// </summary>
    public static class GameUiKeys
    {
        public const string Table = "GameUi";

        public const string Cash = "shell.cash";

        public const string Hint = "shell.hint";

        public const string HintToolRoad = "shell.hint.tool.road";

        public const string HintToolDemolishRoad = "shell.hint.tool.demolish_road";

        public const string HintToolStation = "shell.hint.tool.station";

        public const string HintToolDemolishStation = "shell.hint.tool.demolish_station";

        public const string ToolSelect = "toolbar.tool.select";

        public const string ToolRoad = "toolbar.tool.road";

        public const string ToolDemolishRoad = "toolbar.tool.demolish_road";

        public const string ToolStation = "toolbar.tool.station";

        public const string ToolDemolishStation = "toolbar.tool.demolish_station";

        /// <summary>
        /// Returns the toolbar label key for a tool.
        /// </summary>
        public static string ToolLabelKey(BuildToolId tool)
        {
            switch (tool)
            {
                case BuildToolId.Select:
                    return ToolSelect;
                case BuildToolId.Road:
                    return ToolRoad;
                case BuildToolId.DemolishRoad:
                    return ToolDemolishRoad;
                case BuildToolId.Station:
                    return ToolStation;
                case BuildToolId.DemolishStation:
                    return ToolDemolishStation;
                default:
                    return ToolSelect;
            }
        }

        /// <summary>
        /// Returns the hint key for a tool. Select uses the general map hint.
        /// </summary>
        public static string HintKey(BuildToolId tool)
        {
            switch (tool)
            {
                case BuildToolId.Road:
                    return HintToolRoad;
                case BuildToolId.DemolishRoad:
                    return HintToolDemolishRoad;
                case BuildToolId.Station:
                    return HintToolStation;
                case BuildToolId.DemolishStation:
                    return HintToolDemolishStation;
                case BuildToolId.Select:
                    return Hint;
                default:
                    return Hint;
            }
        }
    }
}
