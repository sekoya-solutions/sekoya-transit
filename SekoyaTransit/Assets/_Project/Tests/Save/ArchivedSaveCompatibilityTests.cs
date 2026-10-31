#nullable enable
using System.IO;
using NUnit.Framework;
using SekoyaTransit.Core.Save;
using UnityEngine;

namespace SekoyaTransit.Save.Tests
{
    [TestFixture]
    public sealed class ArchivedSaveCompatibilityTests
    {
        private JsonGzipSaveSerializer _serializer = null!;

        [SetUp]
        public void SetUp()
        {
            _serializer = new JsonGzipSaveSerializer(CurrentSaveMigrations.CreatePipeline());
        }

        [Test]
        public void Deserialize_ArchivedVersionOneFixture_IsRefusedAsTooOld()
        {
            string fixturePath = Path.Combine(
                Application.dataPath,
                "_Project",
                "Tests",
                "Save",
                "Fixtures",
                "Issue34V1.bytes");

            Assert.That(File.Exists(fixturePath), Is.True, "The archived version-1 fixture is missing.");

            // Saves older than format 27 are no longer loaded (Issue #229); the fixture still proves they are read far
            // enough to report their version instead of failing as corrupt.
            SaveFormatTooOldException exception = Assert.Throws<SaveFormatTooOldException>(
                () => _serializer.Deserialize(File.ReadAllBytes(fixturePath)))!;
            Assert.That(exception.FormatVersion, Is.EqualTo(1));
        }
    }
}
