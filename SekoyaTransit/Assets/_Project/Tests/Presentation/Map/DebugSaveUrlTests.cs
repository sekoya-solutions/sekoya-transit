#nullable enable
using NUnit.Framework;
using SekoyaTransit.Presentation.Map;

namespace SekoyaTransit.Presentation.Tests.Map
{
    public sealed class DebugSaveUrlTests
    {
        [TestCase("http://127.0.0.1:8765/?save=bug.sav", "bug.sav")]
        [TestCase("http://127.0.0.1:8765/index.html?other=1&save=sekoya-transit-debug-1903-04-12.sav", "sekoya-transit-debug-1903-04-12.sav")]
        [TestCase("http://127.0.0.1:8765/?save=a%5Fb.sav", "a_b.sav")]
        [TestCase("http://127.0.0.1:8765/?save=first.sav&save=second.sav", "first.sav")]
        public void TryGetFileName_PlainSaveFileName_IsAccepted(string url, string expected)
        {
            Assert.That(DebugSaveUrl.TryGetFileName(url, out string fileName), Is.True);
            Assert.That(fileName, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not a url")]
        [TestCase("http://127.0.0.1:8765/")]
        [TestCase("http://127.0.0.1:8765/?saves=bug.sav")]
        [TestCase("http://127.0.0.1:8765/?save=")]
        [TestCase("http://127.0.0.1:8765/?save")]
        [TestCase("http://127.0.0.1:8765/?save=bug.txt")]
        [TestCase("http://127.0.0.1:8765/?save=../bug.sav")]
        [TestCase("http://127.0.0.1:8765/?save=..%2Fbug.sav")]
        [TestCase("http://127.0.0.1:8765/?save=dir/bug.sav")]
        [TestCase("http://127.0.0.1:8765/?save=https%3A%2F%2Fexample.com%2Fbug.sav")]
        [TestCase("http://127.0.0.1:8765/?save=a..sav")]
        [TestCase("http://127.0.0.1:8765/?save=.hidden.sav")]
        [TestCase("http://127.0.0.1:8765/?save=%E0%A4%A.sav")]
        public void TryGetFileName_AnythingButAPlainSaveFileName_IsRefused(string? url)
        {
            Assert.That(DebugSaveUrl.TryGetFileName(url, out string fileName), Is.False);
            Assert.That(fileName, Is.Empty);
        }

        [TestCase("http://127.0.0.1:8765/?save=bug.sav", "http://127.0.0.1:8765/bug.sav")]
        [TestCase("http://127.0.0.1:8765/index.html?save=bug.sav", "http://127.0.0.1:8765/bug.sav")]
        [TestCase("https://example.github.io/sekoya-transit/?save=bug.sav", "https://example.github.io/sekoya-transit/bug.sav")]
        public void ResolveFileUrl_IsTheFileInThePageFolder(string pageUrl, string expected)
        {
            Assert.That(DebugSaveUrl.ResolveFileUrl(pageUrl, "bug.sav"), Is.EqualTo(expected));
        }
    }
}
