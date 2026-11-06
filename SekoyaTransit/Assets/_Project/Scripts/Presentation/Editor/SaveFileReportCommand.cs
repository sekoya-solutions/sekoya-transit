#nullable enable
using System;
using System.IO;
using SekoyaTransit.Presentation.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace SekoyaTransit.Presentation.Editor
{
    /// <summary>
    /// Batchmode command that opens a save file the way the game does and writes a report (Issue #320). Arguments:
    /// <c>-saveFile &lt;absolute path&gt;</c>, <c>-reportDir &lt;absolute folder&gt;</c>, and optionally
    /// <c>-advanceDays &lt;N&gt;</c> to run the world N in-game days first. Writes <c>report.md</c>,
    /// <c>state-loaded.json</c> and, with days run, <c>state-advanced.json</c>; exits 0 on success, 1 on failure (the
    /// reason is in the log and in <c>report.md</c>).
    /// </summary>
    public static class SaveFileReportCommand
    {
        /// <summary>
        /// Entry point for <c>-executeMethod SekoyaTransit.Presentation.Editor.SaveFileReportCommand.RunFromBatch</c>.
        /// </summary>
        public static void RunFromBatch()
        {
            int exitCode = 1;
            string? reportDir = CommandLineValue("-reportDir");
            try
            {
                string saveFile = RequiredPath("-saveFile");
                if (reportDir == null || !Path.IsPathRooted(reportDir))
                {
                    throw new ArgumentException("-reportDir needs an absolute folder.");
                }

                int advanceDays = 0;
                string? days = CommandLineValue("-advanceDays");
                if (days != null && (!int.TryParse(days, out advanceDays) || advanceDays < 0))
                {
                    throw new ArgumentException("-advanceDays needs a whole number of days, 0 or more.");
                }

                string contentDir = Path.Combine(Application.dataPath, "_Project", "Content", "Data");
                SaveFileReport report = SaveFileInspector.InspectWithContent(
                    File.ReadAllBytes(saveFile),
                    contentDir,
                    advanceDays);
                Directory.CreateDirectory(reportDir);
                File.WriteAllText(Path.Combine(reportDir, "report.md"), "Save file: " + saveFile + "\n\n" + report.Summary);
                File.WriteAllText(Path.Combine(reportDir, "state-loaded.json"), report.LoadedStateJson);
                if (report.AdvancedStateJson != null)
                {
                    File.WriteAllText(Path.Combine(reportDir, "state-advanced.json"), report.AdvancedStateJson);
                }

                Debug.Log("Save file report written to " + reportDir);
                exitCode = 0;
            }
            catch (Exception exception)
            {
                Debug.LogError("Save file report failed: " + exception);
                if (reportDir != null && Path.IsPathRooted(reportDir))
                {
                    Directory.CreateDirectory(reportDir);
                    File.WriteAllText(Path.Combine(reportDir, "report.md"), "# Save file report failed\n\n" + exception + "\n");
                }
            }

            EditorApplication.Exit(exitCode);
        }

        private static string RequiredPath(string name)
        {
            string? value = CommandLineValue(name);
            if (value == null || !Path.IsPathRooted(value) || !File.Exists(value))
            {
                throw new ArgumentException(name + " needs the absolute path of an existing file.");
            }

            return value;
        }

        private static string? CommandLineValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.Ordinal))
                {
                    return args[index + 1];
                }
            }

            return null;
        }
    }
}
