#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;
using UnityEngine;

namespace SekoyaTransit.UI.Tests.Shell
{
    public sealed class ShellTextRequestsTests
    {
        private readonly Dictionary<string, Action<string?>> _completions = new Dictionary<string, Action<string?>>();
        private ShellTextRequests _requests = null!;

        [SetUp]
        public void SetUp()
        {
            _completions.Clear();
            _requests = new ShellTextRequests((key, completed) => _completions[key] = completed);
        }

        [Test]
        public void Request_NotCompleted_StaysPendingAndDoesNotApply()
        {
            string? applied = null;

            _requests.Request("a", value => applied = value);

            Assert.That(_requests.Pending, Is.EqualTo(1));
            Assert.That(applied, Is.Null);
        }

        [Test]
        public void Request_Completed_AppliesTextAndFinishes()
        {
            string? applied = null;
            _requests.Request("a", value => applied = value);

            _completions["a"]("text");

            Assert.That(applied, Is.EqualTo("text"));
            Assert.That(_requests.Pending, Is.EqualTo(0));
        }

        [Test]
        public void Request_SeveralKeys_PendingUntilTheLastOneCompletes()
        {
            _requests.Request("a", _ => { });
            _requests.Request("b", _ => { });

            _completions["b"]("text");
            Assert.That(_requests.Pending, Is.EqualTo(1));

            _completions["a"]("text");
            Assert.That(_requests.Pending, Is.EqualTo(0));
        }

        [TestCase(null)]
        [TestCase("")]
        public void Request_FailedOrEmptyResult_FinishesWithoutApplying(string? result)
        {
            bool applied = false;
            _requests.Request("a", _ => applied = true);

            _completions["a"](result);

            Assert.That(applied, Is.False);
            Assert.That(_requests.Pending, Is.EqualTo(0));
        }

        [Test]
        public void Request_ApplyThrows_StillFinishes()
        {
            _requests.Request("a", _ => throw new InvalidOperationException("apply failed"));

            Assert.Throws<InvalidOperationException>(() => _completions["a"]("text"));

            Assert.That(_requests.Pending, Is.EqualTo(0));
        }

        [Test]
        public void Request_SourceThrowsWhenStarting_DoesNotStayPending()
        {
            var requests = new ShellTextRequests((_, _) => throw new InvalidOperationException("source failed"));

            Assert.Throws<InvalidOperationException>(() => requests.Request("a", _ => { }));

            Assert.That(requests.Pending, Is.EqualTo(0));
        }

        [Test]
        public void Request_SourceCompletesAtOnce_Finishes()
        {
            var requests = new ShellTextRequests((_, completed) => completed("text"));
            string? applied = null;

            requests.Request("a", value => applied = value);

            Assert.That(applied, Is.EqualTo("text"));
            Assert.That(requests.Pending, Is.EqualTo(0));
        }

        [Test]
        public void ProjectScripts_OnlyShellTextRequestsCallsTheStringDatabase()
        {
            string scripts = Path.Combine(Application.dataPath, "_Project", "Scripts");

            List<string> callers = Directory
                .EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains("GetLocalizedStringAsync"))
                .Select(path => Path.GetFileName(path))
                .ToList();

            Assert.That(
                callers,
                Is.EqualTo(new[] { nameof(ShellTextRequests) + ".cs" }),
                "A text request outside ShellTextRequests is not tracked, so the shell could be revealed before it finishes.");
        }
    }
}
