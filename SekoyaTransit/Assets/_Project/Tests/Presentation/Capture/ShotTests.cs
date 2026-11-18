#nullable enable
using NUnit.Framework;
using SekoyaTransit.Presentation.Editor.Capture;

namespace SekoyaTransit.Presentation.Tests.Capture
{
    [TestFixture]
    public sealed class ShotTests
    {
        private const string ValidScreenshot = "{\"name\":\"map-overview\",\"save\":\"showcase-1935.sav\",\"kind\":\"screenshot\","
            + "\"width\":1920,\"height\":1080,\"hud\":\"shown\",\"panels\":[\"routes\",\"towns\"],\"speed\":2,"
            + "\"frameRate\":30,\"seconds\":2,\"camera\":[{\"time\":0,\"tileX\":120.5,\"tileY\":80,\"zoom\":12}]}";

        private const string ValidClip = "{\"name\":\"delivery\",\"save\":\"showcase-1935.sav\",\"kind\":\"clip\","
            + "\"width\":1280,\"height\":720,\"hud\":\"hidden\",\"speed\":1,\"frameRate\":30,\"seconds\":8,"
            + "\"camera\":[{\"time\":0,\"tileX\":100,\"tileY\":100,\"zoom\":20},{\"time\":8,\"tileX\":110,\"tileY\":90,\"zoom\":8}]}";

        [Test]
        public void Parse_ValidScreenshot_ReadsEveryField()
        {
            Shot shot = Shot.Parse(ValidScreenshot, "map-overview.json");

            Assert.That(shot.Name, Is.EqualTo("map-overview"));
            Assert.That(shot.Save, Is.EqualTo("showcase-1935.sav"));
            Assert.That(shot.Kind, Is.EqualTo(ShotKind.Screenshot));
            Assert.That(shot.Width, Is.EqualTo(1920));
            Assert.That(shot.Height, Is.EqualTo(1080));
            Assert.That(shot.ShowHud, Is.True);
            Assert.That(shot.Panels, Is.EqualTo(new[] { ShotPanel.Routes, ShotPanel.Towns }));
            Assert.That(shot.Speed, Is.EqualTo(2));
            Assert.That(shot.FrameRate, Is.EqualTo(30));
            Assert.That(shot.FrameCount, Is.EqualTo(60));
            Assert.That(shot.Camera, Has.Count.EqualTo(1));
            Assert.That(shot.Camera[0].TileX, Is.EqualTo(120.5f));
            Assert.That(shot.Camera[0].Zoom, Is.EqualTo(12f));
        }

        [Test]
        public void Parse_ValidClip_ReadsKeyframesAndNoPanels()
        {
            Shot shot = Shot.Parse(ValidClip, "delivery.json");

            Assert.That(shot.Kind, Is.EqualTo(ShotKind.Clip));
            Assert.That(shot.ShowHud, Is.False);
            Assert.That(shot.Panels, Is.Empty);
            Assert.That(shot.FrameCount, Is.EqualTo(240));
            Assert.That(shot.Camera, Has.Count.EqualTo(2));
        }

        [TestCase("\"width\":1920,", "", "width", "is missing")]
        [TestCase("\"hud\":\"shown\"", "\"hud\":\"yes\"", "hud", "must be")]
        [TestCase("\"kind\":\"screenshot\"", "\"kind\":\"video\"", "kind", "must be")]
        [TestCase("\"speed\":2", "\"speed\":4", "speed", "must be from 1 to 3")]
        [TestCase("\"save\":\"showcase-1935.sav\"", "\"save\":\"../x.sav\"", "save", "without a path")]
        [TestCase("\"name\":\"map-overview\"", "\"name\":\"map overview\"", "name", "letters, digits and dashes")]
        [TestCase("\"routes\",\"towns\"", "\"routes\",\"map\"", "panels", "unknown panel")]
        [TestCase("\"routes\",\"towns\"", "\"routes\",\"routes\"", "panels", "twice")]
        [TestCase("\"seconds\":2,", "", "seconds", "is missing")]
        [TestCase("\"zoom\":12", "\"zoom\":2", "camera[0].zoom", "outside")]
        [TestCase("\"tileY\":80,", "", "camera[0].tileY", "is missing")]
        [TestCase("\"time\":0,", "\"time\":1,", "camera[0].time", "must be 0")]
        public void Parse_WrongField_NamesTheFileAndTheField(string find, string replace, string field, string problem)
        {
            string json = ValidScreenshot.Replace(find, replace);

            var exception = Assert.Throws<ShotFileException>(() => Shot.Parse(json, "broken.json"));

            Assert.That(exception!.FileName, Is.EqualTo("broken.json"));
            Assert.That(exception.Field, Is.EqualTo(field));
            Assert.That(exception.Message, Does.StartWith("broken.json: " + field + " ").And.Contain(problem));
        }

        [Test]
        public void Parse_ScreenshotWithTwoKeyframes_IsRejected()
        {
            string json = ValidScreenshot.Replace(
                "\"camera\":[{\"time\":0,\"tileX\":120.5,\"tileY\":80,\"zoom\":12}]",
                "\"camera\":[{\"time\":0,\"tileX\":1,\"tileY\":1,\"zoom\":12},{\"time\":1,\"tileX\":2,\"tileY\":2,\"zoom\":12}]");

            var exception = Assert.Throws<ShotFileException>(() => Shot.Parse(json, "two.json"));

            Assert.That(exception!.Field, Is.EqualTo("camera"));
        }

        [Test]
        public void Parse_ClipKeyframesOutOfOrderOrPastTheEnd_AreRejected()
        {
            string backwards = ValidClip.Replace("{\"time\":8,", "{\"time\":0,");
            string late = ValidClip.Replace("{\"time\":8,", "{\"time\":9,");

            Assert.That(
                Assert.Throws<ShotFileException>(() => Shot.Parse(backwards, "a.json"))!.Message,
                Does.Contain("later than the previous keyframe"));
            Assert.That(
                Assert.Throws<ShotFileException>(() => Shot.Parse(late, "b.json"))!.Message,
                Does.Contain("after the end of the shot"));
        }

        [Test]
        public void Parse_ClipWithOddSize_IsRejected()
        {
            string json = ValidClip.Replace("\"width\":1280", "\"width\":1281");

            var exception = Assert.Throws<ShotFileException>(() => Shot.Parse(json, "odd.json"));

            Assert.That(exception!.Field, Is.EqualTo("width/height"));
        }

        [Test]
        public void Parse_NotJson_IsRejectedWithTheFileName()
        {
            var exception = Assert.Throws<ShotFileException>(() => Shot.Parse("{ not json", "bad.json"));

            Assert.That(exception!.Message, Does.StartWith("bad.json: "));
        }
    }
}
