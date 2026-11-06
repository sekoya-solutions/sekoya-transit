#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SekoyaTransit.Save.Tests
{
    public sealed class SaveFilePickRequestsTests
    {
        [Test]
        public void OnPicked_MatchingRequest_CompletesWithTheDecodedBytes()
        {
            var picks = new SaveFilePickRequests();
            int id = picks.Begin(out Task<byte[]?> result);

            picks.OnPicked(id + "|" + Convert.ToBase64String(new byte[] { 1, 2, 3 }));

            Assert.That(result.IsCompleted, Is.True);
            Assert.That(result.Result, Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(picks.HasPending, Is.False);
        }

        [Test]
        public void OnCancelled_MatchingRequest_CompletesWithNull()
        {
            var picks = new SaveFilePickRequests();
            int id = picks.Begin(out Task<byte[]?> result);

            picks.OnCancelled(id + "|");

            Assert.That(result.IsCompleted, Is.True);
            Assert.That(result.Result, Is.Null);
        }

        [Test]
        public void OnFailed_MatchingRequest_FailsWithAnIOExceptionCarryingTheReason()
        {
            var picks = new SaveFilePickRequests();
            int id = picks.Begin(out Task<byte[]?> result);

            picks.OnFailed(id + "|blocked");

            Assert.That(result.IsFaulted, Is.True);
            Assert.That(result.Exception!.InnerException, Is.InstanceOf<IOException>());
            Assert.That(result.Exception.InnerException!.Message, Is.EqualTo("blocked"));
        }

        [Test]
        public void Begin_WhileAPickIsOpen_EndsTheOldPickAsCancelledAndDropsItsLateAnswer()
        {
            var picks = new SaveFilePickRequests();
            int oldId = picks.Begin(out Task<byte[]?> oldResult);
            picks.Begin(out Task<byte[]?> newResult);

            picks.OnPicked(oldId + "|" + Convert.ToBase64String(new byte[] { 9 }));

            Assert.That(oldResult.IsCompleted, Is.True);
            Assert.That(oldResult.Result, Is.Null);
            Assert.That(newResult.IsCompleted, Is.False);
            Assert.That(picks.HasPending, Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("no separator")]
        [TestCase("|AQID")]
        [TestCase("x|AQID")]
        [TestCase("-1|AQID")]
        public void OnPicked_MalformedAnswer_IsIgnored(string? message)
        {
            var picks = new SaveFilePickRequests();
            picks.Begin(out Task<byte[]?> result);

            picks.OnPicked(message);

            Assert.That(result.IsCompleted, Is.False);
            Assert.That(picks.HasPending, Is.True);
        }

        [Test]
        public void OnPicked_InvalidBase64_FailsWithAnIOException()
        {
            var picks = new SaveFilePickRequests();
            int id = picks.Begin(out Task<byte[]?> result);

            picks.OnPicked(id + "|not base64!");

            Assert.That(result.IsFaulted, Is.True);
            Assert.That(result.Exception!.InnerException, Is.InstanceOf<IOException>());
        }

        [Test]
        public void Fail_OnlyEndsTheMatchingRequest()
        {
            var picks = new SaveFilePickRequests();
            int id = picks.Begin(out Task<byte[]?> result);

            picks.Fail(id + 1, new IOException("other"));
            Assert.That(result.IsCompleted, Is.False);

            picks.Fail(id, new IOException("this one"));

            Assert.That(result.IsFaulted, Is.True);
        }

        [Test]
        public void UnavailableTransfer_IsUnavailableAndRefusesEveryCall()
        {
            var transfer = new UnavailableSaveFileTransfer();

            Assert.That(transfer.IsAvailable, Is.False);
            Assert.Throws<NotSupportedException>(() => transfer.Download("a.sav", new byte[] { 1 }));
            Assert.Throws<NotSupportedException>(() => transfer.PickFileAsync());
        }
    }
}
