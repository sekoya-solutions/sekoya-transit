#nullable enable
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    /// <summary>
    /// In-memory <see cref="ILanguagePreference"/> for tests.
    /// </summary>
    internal sealed class MemoryLanguagePreference : ILanguagePreference
    {
        private string? _code;

        public MemoryLanguagePreference(string? code = null)
        {
            _code = code;
        }

        public int StoreCount { get; private set; }

        public string? Load()
        {
            return _code;
        }

        public void Store(string code)
        {
            _code = code;
            StoreCount++;
        }
    }
}
