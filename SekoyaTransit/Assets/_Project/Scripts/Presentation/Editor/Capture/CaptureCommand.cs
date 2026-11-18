#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SekoyaTransit.Presentation.Editor.Capture
{
    /// <summary>
    /// Makes the promotional screenshots and clips from the shot files in <c>SekoyaTransit/Capture/Shots/</c>
    /// (ADR 0036, Issue #352); outputs go to the ignored <c>Builds/Capture/</c>. From the menu, or from the command
    /// line with the Editor drawing (the Recorder records a rendered Game view, so not with <c>-batchmode</c>):
    /// <c>-executeMethod SekoyaTransit.Presentation.Editor.Capture.CaptureCommand.RunFromCommandLine</c>, optionally
    /// <c>-shot &lt;name&gt;</c> for one shot; the Editor then quits with exit code 0 when every shot was written, 1
    /// otherwise.
    /// </summary>
    public static class CaptureCommand
    {
        /// <summary>
        /// Gets the folder of the shot files.
        /// </summary>
        public static string ShotsDirectory => Path.Combine(ProjectDirectory, "Capture", "Shots");

        /// <summary>
        /// Gets the folder of the saves the shots load.
        /// </summary>
        public static string SavesDirectory => Path.Combine(ProjectDirectory, "Capture", "Saves");

        /// <summary>
        /// Gets the output folder (ignored by Git).
        /// </summary>
        public static string OutputDirectory => Path.Combine(Path.GetDirectoryName(ProjectDirectory)!, "Builds", "Capture");

        private static string ProjectDirectory => Path.GetDirectoryName(Application.dataPath)!;

        [MenuItem("Sekoya Transit/Capture/Make All Shots")]
        private static void MakeAllShots()
        {
            Start(null, success => Debug.Log(success ? "All shots written to " + OutputDirectory : "Some shots failed; see the log."));
        }

        [MenuItem("Sekoya Transit/Capture/Make All Shots", true)]
        private static bool CanMakeAllShots()
        {
            return !CaptureRunner.IsRunning && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>
        /// Entry point for <c>-executeMethod</c>; see the class summary.
        /// </summary>
        public static void RunFromCommandLine()
        {
            string? only = CommandLineValue("-shot");
            try
            {
                Start(only, success => EditorApplication.Exit(success ? 0 : 1));
            }
            catch (Exception exception)
            {
                Debug.LogError("Capture failed: " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Reads every shot file, or only <paramref name="only"/>, and stops at the first invalid one.
        /// </summary>
        public static IReadOnlyList<Shot> LoadShots(string directory, string? only)
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException("No shot folder at " + directory + ".");
            }

            var shots = new List<Shot>();
            foreach (string file in Directory.GetFiles(directory, "*.json").OrderBy(path => path, StringComparer.Ordinal))
            {
                Shot shot = Shot.Parse(File.ReadAllText(file), Path.GetFileName(file));
                if (shots.Any(other => other.Name == shot.Name))
                {
                    throw new ShotFileException(Path.GetFileName(file), "name", "\"" + shot.Name + "\" is used by another shot file");
                }

                shots.Add(shot);
            }

            if (only != null)
            {
                shots = shots.Where(shot => shot.Name == only).ToList();
                if (shots.Count == 0)
                {
                    throw new ArgumentException("No shot is named \"" + only + "\".");
                }
            }

            if (shots.Count == 0)
            {
                throw new ArgumentException("The shot folder " + directory + " has no shot file.");
            }

            return shots;
        }

        private static void Start(string? only, Action<bool> onFinished)
        {
            IReadOnlyList<Shot> shots = LoadShots(ShotsDirectory, only);
            CaptureRunner.Run(shots, SavesDirectory, OutputDirectory, onFinished);
        }

        private static string? CommandLineValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (args[index] == name)
                {
                    return args[index + 1];
                }
            }

            return null;
        }
    }
}
