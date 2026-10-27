#nullable enable
using System;
using System.Globalization;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// State and rules of the new game screen (Issue #67): the seed text, its validation, the Random button, the
    /// selected map size, and the Start request. Plain C#, independent of UI Toolkit and Core.
    /// </summary>
    public sealed class NewGameViewModel
    {
        private readonly Func<ulong> _randomSeed;

        /// <summary>
        /// Initializes the model with a random seed already in the field and Small selected.
        /// </summary>
        /// <param name="randomSeed">Supplies a fresh seed; must not use any Core random generator.</param>
        public NewGameViewModel(Func<ulong> randomSeed)
        {
            _randomSeed = randomSeed ?? throw new ArgumentNullException(nameof(randomSeed));
            SelectedSize = NewGameMapSize.Small;
            Randomize();
        }

        /// <summary>
        /// Gets the text currently in the seed field.
        /// </summary>
        public string SeedText { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the selected map size.
        /// </summary>
        public NewGameMapSize SelectedSize { get; private set; }

        /// <summary>
        /// Gets whether the seed text is a whole number from 0 to 18446744073709551615.
        /// </summary>
        public bool IsSeedValid => TryParseSeed(SeedText, out _);

        /// <summary>
        /// Gets whether Start is enabled.
        /// </summary>
        public bool CanStart => IsSeedValid;

        /// <summary>
        /// Replaces the seed text with what the player typed.
        /// </summary>
        public void SetSeedText(string? text)
        {
            SeedText = text ?? string.Empty;
        }

        /// <summary>
        /// Rolls a new random seed into the field.
        /// </summary>
        public void Randomize()
        {
            SeedText = _randomSeed().ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Selects a map size; exactly one is selected at a time.
        /// </summary>
        public void SelectSize(NewGameMapSize size)
        {
            if (Array.IndexOf(NewGameMapSizes.All(), size) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "Map size is not supported.");
            }

            SelectedSize = size;
        }

        /// <summary>
        /// Builds the Start request, or returns false while the seed is invalid.
        /// </summary>
        public bool TryCreateSelection(out NewGameSelection selection)
        {
            if (TryParseSeed(SeedText, out ulong seed))
            {
                selection = new NewGameSelection(seed, SelectedSize);
                return true;
            }

            selection = default;
            return false;
        }

        /// <summary>
        /// Parses a seed: digits only (surrounding spaces ignored), invariant culture, 0 to 18446744073709551615.
        /// </summary>
        public static bool TryParseSeed(string? text, out ulong seed)
        {
            seed = 0UL;
            if (text is null)
            {
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            return ulong.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out seed);
        }

        /// <summary>
        /// Formats the seed and map size for display (for example "Seed 123456 · 256×256") from a localized template
        /// with the seed as {0} and the tiles per side as {1}.
        /// </summary>
        public static string FormatSeedInfo(string template, ulong seed, NewGameMapSize size)
        {
            if (template is null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                template,
                seed.ToString(CultureInfo.InvariantCulture),
                NewGameMapSizes.Tiles(size).ToString(CultureInfo.InvariantCulture));
        }
    }
}
