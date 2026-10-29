#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The three map sizes offered on the new game screen. The UI assembly does not reference Core, so
    /// Presentation maps these to the Core map sizes (Issue #67).
    /// </summary>
    public enum NewGameMapSize
    {
        Small = 0,
        Medium = 1,
        Large = 2
    }

    /// <summary>
    /// Helpers for <see cref="NewGameMapSize"/>.
    /// </summary>
    public static class NewGameMapSizes
    {
        private static readonly NewGameMapSize[] _all =
        {
            NewGameMapSize.Small,
            NewGameMapSize.Medium,
            NewGameMapSize.Large
        };

        /// <summary>
        /// Gets a copy of the sizes in display order.
        /// </summary>
        public static NewGameMapSize[] All()
        {
            return (NewGameMapSize[])_all.Clone();
        }

        /// <summary>
        /// Gets the number of tiles along one side of the square map.
        /// </summary>
        public static int Tiles(NewGameMapSize size)
        {
            switch (size)
            {
                case NewGameMapSize.Small:
                    return 128;
                case NewGameMapSize.Medium:
                    return 256;
                case NewGameMapSize.Large:
                    return 512;
                default:
                    throw new ArgumentOutOfRangeException(nameof(size), size, "Map size is not supported.");
            }
        }
    }
}
