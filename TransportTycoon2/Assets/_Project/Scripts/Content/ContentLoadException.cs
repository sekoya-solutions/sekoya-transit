#nullable enable
using System;

namespace TransportTycoon.Content
{
    /// <summary>
    /// Reports content that could not be loaded into a complete catalog.
    /// </summary>
    public sealed class ContentLoadException : Exception
    {
        public ContentLoadException(string message)
            : base(message)
        {
        }

        public ContentLoadException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
