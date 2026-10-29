#nullable enable

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Localization keys (table <see cref="GameUiKeys.Table"/>) for the sound panel and its top bar button (Issue #68).
    /// </summary>
    public static class SoundUiKeys
    {
        public const string Toggle = "sound.toggle";

        public const string Title = "sound.title";

        public const string Music = "sound.music";

        public const string Effects = "sound.effects";

        public const string Mute = "sound.mute";

        public const string Close = "sound.close";

        /// <summary>
        /// Keys loaded when the shell starts.
        /// </summary>
        public static readonly string[] StaticKeys =
        {
            Toggle,
            Title,
            Music,
            Effects,
            Mute,
            Close
        };
    }
}
