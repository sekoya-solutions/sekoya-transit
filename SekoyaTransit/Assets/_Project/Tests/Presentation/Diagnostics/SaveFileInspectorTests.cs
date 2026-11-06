#nullable enable
using System;
using System.IO;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;
using SekoyaTransit.Presentation.Diagnostics;
using SekoyaTransit.Save;
using UnityEngine;

namespace SekoyaTransit.Presentation.Tests.Diagnostics
{
    public sealed class SaveFileInspectorTests
    {
        private static ContentCatalog LoadCatalog()
        {
            return SaveFileInspector.LoadCatalog(Path.Combine(Application.dataPath, "_Project", "Content", "Data"));
        }

        private static byte[] NewGameFile(ContentCatalog catalog, out string json)
        {
            GeneratedWorld world = NewGame.Create(new NewGameRequest(20261106UL, MapSize.Small), catalog);
            var serializer = new JsonGzipSaveSerializer(CurrentSaveMigrations.CreatePipeline());
            byte[] file = serializer.Serialize(SaveSnapshot.Capture(world.MapSeed, Tick.Zero, world));
            json = SaveFileInspector.Inspect(file, catalog, 0).LoadedStateJson;
            return file;
        }

        [Test]
        public void Inspect_NoDays_DescribesTheGameAsLoadedAndGivesItsJson()
        {
            ContentCatalog catalog = LoadCatalog();
            byte[] file = NewGameFile(catalog, out _);

            SaveFileReport report = SaveFileInspector.Inspect(file, catalog, 0);

            Assert.That(report.Summary, Does.Contain("## As loaded"));
            Assert.That(report.Summary, Does.Contain("- Date: 1900-01-01 (tick 0)"));
            Assert.That(report.Summary, Does.Contain("- Seed: 20261106"));
            Assert.That(report.Summary, Does.Not.Contain("## After"));
            Assert.That(report.LoadedStateJson, Does.StartWith("{").And.Contain("\"formatVersion\""));
            Assert.That(report.AdvancedStateJson, Is.Null);
        }

        [Test]
        public void Inspect_SomeDays_RunsTheWorldThatManyDays()
        {
            ContentCatalog catalog = LoadCatalog();
            byte[] file = NewGameFile(catalog, out string loadedJson);

            SaveFileReport report = SaveFileInspector.Inspect(file, catalog, 3);

            Assert.That(report.Summary, Does.Contain("## After 3 days"));
            Assert.That(report.Summary, Does.Contain("- Date: 1900-01-04"));
            Assert.That(report.LoadedStateJson, Is.EqualTo(loadedJson));
            Assert.That(report.AdvancedStateJson, Is.Not.Null.And.Not.EqualTo(loadedJson));
        }

        [Test]
        public void Inspect_DamagedFile_FailsLikeAGameLoad()
        {
            Assert.Throws<SaveFormatException>(() => SaveFileInspector.Inspect(new byte[] { 1, 2, 3 }, LoadCatalog(), 0));
        }

        [Test]
        public void Inspect_NegativeDays_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SaveFileInspector.Inspect(new byte[] { 1 }, LoadCatalog(), -1));
        }
    }
}
