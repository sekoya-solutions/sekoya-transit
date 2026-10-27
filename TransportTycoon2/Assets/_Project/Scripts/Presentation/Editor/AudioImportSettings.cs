#nullable enable
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TransportTycoon.Presentation.Editor
{
    /// <summary>
    /// Import settings for the game's audio (Issue #68, ADR 0018), so a source file keeps its original format and a new
    /// clip needs no manual importer setup. Music streams from compressed memory; short effects are decompressed on
    /// load, mono and resampled to 44.1 kHz (the cash-in WAV is a 96 kHz stereo recording). Every clip preloads.
    /// </summary>
    public sealed class AudioImportSettings : AssetPostprocessor
    {
        /// <summary>
        /// Folder holding the game's audio.
        /// </summary>
        public const string AudioRoot = "Assets/_Project/Audio/";

        /// <summary>
        /// Folder holding the ambient music.
        /// </summary>
        public const string MusicRoot = AudioRoot + "Music/";

        private const int EffectsSampleRate = 44100;
        private const float MusicQuality = 0.6f;
        private const float EffectsQuality = 0.7f;

        /// <summary>
        /// Applies the settings to every clip under <see cref="AudioRoot"/> and reimports the ones that changed (files
        /// imported before this postprocessor existed).
        /// </summary>
        public static void EnsureAll()
        {
            string root = AudioRoot.TrimEnd('/');
            if (!Directory.Exists(root))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
            {
                string path = file.Replace(Path.DirectorySeparatorChar, '/');
                if (IsAudioFile(path) && AssetImporter.GetAtPath(path) is AudioImporter importer && Apply(importer, path))
                {
                    importer.SaveAndReimport();
                }
            }
        }

        /// <summary>
        /// Sets the importer of <paramref name="assetPath"/>; returns whether anything changed.
        /// </summary>
        public static bool Apply(AudioImporter importer, string assetPath)
        {
            if (importer is null)
            {
                throw new ArgumentNullException(nameof(importer));
            }

            bool isMusic = assetPath.StartsWith(MusicRoot, StringComparison.Ordinal);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            AudioImporterSampleSettings wanted = settings;
            wanted.compressionFormat = AudioCompressionFormat.Vorbis;
            // Loaded with the scene, so the first click or the music does not wait for a lazy load.
            wanted.preloadAudioData = true;
            if (isMusic)
            {
                wanted.loadType = AudioClipLoadType.CompressedInMemory;
                wanted.quality = MusicQuality;
                wanted.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            }
            else
            {
                wanted.loadType = AudioClipLoadType.DecompressOnLoad;
                wanted.quality = EffectsQuality;
                wanted.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                wanted.sampleRateOverride = EffectsSampleRate;
            }

            bool forceToMono = !isMusic;
            bool loadInBackground = isMusic;
            bool changed = !SameSettings(settings, wanted)
                || importer.forceToMono != forceToMono
                || importer.loadInBackground != loadInBackground;
            if (!changed)
            {
                return false;
            }

            importer.defaultSampleSettings = wanted;
            importer.forceToMono = forceToMono;
            importer.loadInBackground = loadInBackground;
            return true;
        }

        private void OnPreprocessAudio()
        {
            if (assetPath.StartsWith(AudioRoot, StringComparison.Ordinal) && assetImporter is AudioImporter importer)
            {
                Apply(importer, assetPath);
            }
        }

        private static bool IsAudioFile(string path)
        {
            return path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameSettings(AudioImporterSampleSettings left, AudioImporterSampleSettings right)
        {
            return left.loadType == right.loadType
                && left.compressionFormat == right.compressionFormat
                && Mathf.Approximately(left.quality, right.quality)
                && left.sampleRateSetting == right.sampleRateSetting
                && left.preloadAudioData == right.preloadAudioData
                && (left.sampleRateSetting != AudioSampleRateSetting.OverrideSampleRate
                    || left.sampleRateOverride == right.sampleRateOverride);
        }
    }
}
