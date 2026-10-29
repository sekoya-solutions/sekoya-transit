#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Presentation.Audio;
using SekoyaTransit.Presentation.Map;
using UnityEditor;
using UnityEngine;

namespace SekoyaTransit.Presentation.Tests.Audio
{
    /// <summary>
    /// Checks the shipped audio of Issue #68: the map action mapping, the import settings, and the MapPlay scene wiring.
    /// </summary>
    public sealed class MapPlayAudioAssetsTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/MapPlay.unity";
        private const string MusicPath = "Assets/_Project/Audio/Music/LofiLoop.ogg";

        private static readonly string[] EffectPaths =
        {
            "Assets/_Project/Audio/Sfx/click_001_padded.wav",
            "Assets/_Project/Audio/Sfx/impactPlank_medium_000.ogg",
            "Assets/_Project/Audio/Sfx/impactPlate_heavy_000.ogg",
            "Assets/_Project/Audio/Sfx/snd_purchase.wav",
        };

        [TestCase(MapPlayerAction.Selected, SoundEffect.Click)]
        [TestCase(MapPlayerAction.Built, SoundEffect.Build)]
        [TestCase(MapPlayerAction.Demolished, SoundEffect.Demolish)]
        public void EffectFor_MapAction_PlaysItsSound(MapPlayerAction action, SoundEffect expected)
        {
            Assert.That(MapPlayAudio.EffectFor(action), Is.EqualTo(expected));
        }

        [Test]
        public void MusicImport_StreamsCompressedVorbisInStereo()
        {
            AudioImporter importer = LoadImporter(MusicPath);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;

            Assert.That(settings.loadType, Is.EqualTo(AudioClipLoadType.CompressedInMemory));
            Assert.That(settings.compressionFormat, Is.EqualTo(AudioCompressionFormat.Vorbis));
            Assert.That(importer.forceToMono, Is.False);
            Assert.That(importer.loadInBackground, Is.True);
            Assert.That(settings.preloadAudioData, Is.True);
        }

        [Test]
        public void EffectImports_AreDecompressedMonoAt44100Hz()
        {
            foreach (string path in EffectPaths)
            {
                AudioImporter importer = LoadImporter(path);
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;

                Assert.That(settings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad), path);
                Assert.That(settings.compressionFormat, Is.EqualTo(AudioCompressionFormat.Vorbis), path);
                Assert.That(settings.sampleRateSetting, Is.EqualTo(AudioSampleRateSetting.OverrideSampleRate), path);
                Assert.That(settings.sampleRateOverride, Is.EqualTo(44100), path);
                Assert.That(importer.forceToMono, Is.True, path);
                Assert.That(settings.preloadAudioData, Is.True, path);
            }
        }

        [Test]
        public void MapPlayScene_ReferencesEveryShippedClip()
        {
            string[] dependencies = AssetDatabase.GetDependencies(ScenePath, recursive: false);

            Assert.That(dependencies, Does.Contain(MusicPath));
            foreach (string path in EffectPaths)
            {
                Assert.That(dependencies, Does.Contain(path));
            }
        }

        private static AudioImporter LoadImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                return importer;
            }

            throw new InvalidOperationException("No audio importer for " + path);
        }
    }
}
