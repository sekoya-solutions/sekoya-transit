#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Reports invalid, corrupt, incomplete, or unsupported save data.
    /// </summary>
    public sealed class SaveFormatException : Exception
    {
        public SaveFormatException(string message)
            : base(message)
        {
        }

        public SaveFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
