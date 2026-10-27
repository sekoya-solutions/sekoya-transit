#nullable enable

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Localization keys (table <see cref="GameUiKeys.Table"/>) for the new game screen, the in-game seed display,
    /// and the game-over New game button (Issue #67).
    /// </summary>
    public static class NewGameUiKeys
    {
        public const string Title = "new_game.title";

        public const string SeedLabel = "new_game.seed_label";

        public const string Random = "new_game.random";

        public const string SeedError = "new_game.seed_error";

        public const string SizeLabel = "new_game.size_label";

        public const string SizeSmall = "new_game.size.small";

        public const string SizeMedium = "new_game.size.medium";

        public const string SizeLarge = "new_game.size.large";

        public const string Start = "new_game.start";

        /// <summary>
        /// Template with the seed as {0} and the tiles per side as {1}, for the side panel and the game-over screen.
        /// </summary>
        public const string SeedInfo = "shell.seed_info";

        public const string GameOverNewGame = "game_over.new_game";

        /// <summary>
        /// Keys loaded when the shell starts.
        /// </summary>
        public static readonly string[] StaticKeys =
        {
            Title,
            SeedLabel,
            Random,
            SeedError,
            SizeLabel,
            SizeSmall,
            SizeMedium,
            SizeLarge,
            Start,
            SeedInfo,
            GameOverNewGame
        };

        /// <summary>
        /// Gets the label key of a size toggle.
        /// </summary>
        public static string SizeKey(NewGameMapSize size)
        {
            switch (size)
            {
                case NewGameMapSize.Small:
                    return SizeSmall;
                case NewGameMapSize.Medium:
                    return SizeMedium;
                default:
                    return SizeLarge;
            }
        }
    }
}
