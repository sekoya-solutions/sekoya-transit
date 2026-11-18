#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SekoyaTransit.Presentation.Editor.Capture
{
    /// <summary>
    /// What a shot records (ADR 0036): one PNG screenshot, or a clip as an MP4 with sound.
    /// </summary>
    public enum ShotKind
    {
        Screenshot,
        Clip,
    }

    /// <summary>
    /// A panel of the map shell a shot opens before recording.
    /// </summary>
    public enum ShotPanel
    {
        Routes,
        Contracts,
        Vehicles,
        Unlocks,
        Finance,
        Achievements,
        Industries,
        Towns,
    }

    /// <summary>
    /// One camera keyframe: at <see cref="Time"/> seconds after recording starts, the camera is centered on the map
    /// point (<see cref="TileX"/>, <see cref="TileY"/>) in tile coordinates, at orthographic size <see cref="Zoom"/>
    /// (smaller is closer).
    /// </summary>
    public readonly struct CameraKey
    {
        public CameraKey(float time, float tileX, float tileY, float zoom)
        {
            Time = time;
            TileX = tileX;
            TileY = tileY;
            Zoom = zoom;
        }

        public float Time { get; }

        public float TileX { get; }

        public float TileY { get; }

        public float Zoom { get; }
    }

    /// <summary>
    /// A validated shot file (ADR 0036, Issue #352). <see cref="Parse"/> reads the JSON and stops at the first missing
    /// or wrong field with a <see cref="ShotFileException"/> that names the file and the field.
    /// </summary>
    public sealed class Shot
    {
        /// <summary>Closest camera zoom the game allows (orthographic size).</summary>
        public const float MinZoom = 4f;

        /// <summary>Farthest camera zoom the game allows (orthographic size).</summary>
        public const float MaxZoom = 140f;

        private const int MaxSide = 7680;

        private Shot(
            string name,
            string save,
            ShotKind kind,
            int width,
            int height,
            bool showHud,
            IReadOnlyList<ShotPanel> panels,
            int speed,
            int frameRate,
            float seconds,
            IReadOnlyList<CameraKey> camera)
        {
            Name = name;
            Save = save;
            Kind = kind;
            Width = width;
            Height = height;
            ShowHud = showHud;
            Panels = panels;
            Speed = speed;
            FrameRate = frameRate;
            Seconds = seconds;
            Camera = camera;
        }

        /// <summary>Gets the shot name, also the output file name (letters, digits and dashes).</summary>
        public string Name { get; }

        /// <summary>Gets the save file name, in the capture saves folder.</summary>
        public string Save { get; }

        public ShotKind Kind { get; }

        public int Width { get; }

        public int Height { get; }

        /// <summary>Gets whether the heads-up display is shown.</summary>
        public bool ShowHud { get; }

        /// <summary>Gets the panels opened before recording, in order.</summary>
        public IReadOnlyList<ShotPanel> Panels { get; }

        /// <summary>Gets the game speed while recording: 1, 2 or 3.</summary>
        public int Speed { get; }

        /// <summary>Gets the recorded frames per second; the game advances exactly one frame step per recorded frame.</summary>
        public int FrameRate { get; }

        /// <summary>
        /// Gets the seconds of game time recorded: the clip length, or for a screenshot how long the game runs before
        /// the single frame is taken (so vehicles are moving and the UI has settled).
        /// </summary>
        public float Seconds { get; }

        /// <summary>Gets the camera keyframes, in time order; the first is at time 0.</summary>
        public IReadOnlyList<CameraKey> Camera { get; }

        /// <summary>
        /// Gets the number of recorded frames: for a clip every frame of <see cref="Seconds"/>, for a screenshot the
        /// index of the single frame taken.
        /// </summary>
        public int FrameCount => Mathf.RoundToInt(Seconds * FrameRate);

        /// <summary>
        /// Reads and validates a shot file; <paramref name="fileName"/> names it in errors.
        /// </summary>
        public static Shot Parse(string json, string fileName)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            ShotFileData? data;
            try
            {
                data = JsonUtility.FromJson<ShotFileData>(json);
            }
            catch (ArgumentException exception)
            {
                throw new ShotFileException(fileName, "(file)", "is not valid JSON: " + exception.Message);
            }

            if (data == null)
            {
                throw new ShotFileException(fileName, "(file)", "is empty");
            }

            string name = RequireText(data.name, fileName, "name");
            if (!IsFileNameSafe(name))
            {
                throw new ShotFileException(fileName, "name", "may contain only letters, digits and dashes");
            }

            string save = RequireText(data.save, fileName, "save");
            if (save.IndexOfAny(new[] { '/', '\\' }) >= 0 || !save.EndsWith(".sav", StringComparison.Ordinal))
            {
                throw new ShotFileException(fileName, "save", "must be a .sav file name in the saves folder, without a path");
            }

            ShotKind kind = RequireText(data.kind, fileName, "kind") switch
            {
                "screenshot" => ShotKind.Screenshot,
                "clip" => ShotKind.Clip,
                _ => throw new ShotFileException(fileName, "kind", "must be \"screenshot\" or \"clip\""),
            };

            int width = RequireRange(data.width, 16, MaxSide, fileName, "width");
            int height = RequireRange(data.height, 16, MaxSide, fileName, "height");
            if (kind == ShotKind.Clip && (width % 2 != 0 || height % 2 != 0))
            {
                throw new ShotFileException(fileName, "width/height", "must be even for an MP4 clip");
            }

            bool showHud = RequireText(data.hud, fileName, "hud") switch
            {
                "shown" => true,
                "hidden" => false,
                _ => throw new ShotFileException(fileName, "hud", "must be \"shown\" or \"hidden\""),
            };

            var panels = new List<ShotPanel>();
            foreach (string? panel in data.panels ?? Array.Empty<string>())
            {
                if (!TryParsePanel(panel, out ShotPanel parsed))
                {
                    throw new ShotFileException(
                        fileName,
                        "panels",
                        "has an unknown panel \"" + panel + "\" (known: routes, contracts, vehicles, unlocks, finance, achievements, industries, towns)");
                }

                if (panels.Contains(parsed))
                {
                    throw new ShotFileException(fileName, "panels", "lists \"" + panel + "\" twice");
                }

                panels.Add(parsed);
            }

            int speed = RequireRange(data.speed, 1, 3, fileName, "speed");
            int frameRate = RequireRange(data.frameRate, 1, 120, fileName, "frameRate");
            float seconds = data.seconds;
            if (float.IsNaN(seconds) || seconds <= 0f || seconds > 600f)
            {
                throw new ShotFileException(fileName, "seconds", "is missing or not more than 0 and at most 600");
            }

            if (Mathf.RoundToInt(seconds * frameRate) < 1)
            {
                throw new ShotFileException(fileName, "seconds", "is shorter than one frame at this frame rate");
            }

            IReadOnlyList<CameraKey> camera = ReadCamera(data.camera, kind, seconds, fileName);
            return new Shot(name, save, kind, width, height, showHud, panels, speed, frameRate, seconds, camera);
        }

        private static IReadOnlyList<CameraKey> ReadCamera(CameraKeyData?[]? keys, ShotKind kind, float seconds, string fileName)
        {
            if (keys == null || keys.Length == 0)
            {
                throw new ShotFileException(fileName, "camera", "needs at least one keyframe");
            }

            if (kind == ShotKind.Screenshot && keys.Length != 1)
            {
                throw new ShotFileException(fileName, "camera", "of a screenshot has exactly one keyframe");
            }

            var result = new List<CameraKey>(keys.Length);
            for (int index = 0; index < keys.Length; index++)
            {
                string field = "camera[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                CameraKeyData? key = keys[index];
                if (key == null)
                {
                    throw new ShotFileException(fileName, field, "is empty");
                }

                if (float.IsNaN(key.time) || key.time < 0f)
                {
                    throw new ShotFileException(fileName, field + ".time", "is missing or negative");
                }

                if (index == 0 && key.time != 0f)
                {
                    throw new ShotFileException(fileName, field + ".time", "of the first keyframe must be 0");
                }

                if (index > 0 && key.time <= result[index - 1].Time)
                {
                    throw new ShotFileException(fileName, field + ".time", "must be later than the previous keyframe");
                }

                if (key.time > seconds)
                {
                    throw new ShotFileException(fileName, field + ".time", "is after the end of the shot (seconds)");
                }

                if (float.IsNaN(key.tileX) || key.tileX < 0f)
                {
                    throw new ShotFileException(fileName, field + ".tileX", "is missing or negative");
                }

                if (float.IsNaN(key.tileY) || key.tileY < 0f)
                {
                    throw new ShotFileException(fileName, field + ".tileY", "is missing or negative");
                }

                if (float.IsNaN(key.zoom) || key.zoom < MinZoom || key.zoom > MaxZoom)
                {
                    throw new ShotFileException(
                        fileName,
                        field + ".zoom",
                        "is missing or outside " + MinZoom.ToString(CultureInfo.InvariantCulture) + " to "
                        + MaxZoom.ToString(CultureInfo.InvariantCulture));
                }

                result.Add(new CameraKey(key.time, key.tileX, key.tileY, key.zoom));
            }

            return result;
        }

        private static string RequireText(string? value, string fileName, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ShotFileException(fileName, field, "is missing");
            }

            return value!;
        }

        private static int RequireRange(int value, int min, int max, string fileName, string field)
        {
            if (value == ShotFileData.Missing)
            {
                throw new ShotFileException(fileName, field, "is missing");
            }

            if (value < min || value > max)
            {
                throw new ShotFileException(
                    fileName,
                    field,
                    "must be from " + min.ToString(CultureInfo.InvariantCulture) + " to " + max.ToString(CultureInfo.InvariantCulture));
            }

            return value;
        }

        private static bool IsFileNameSafe(string name)
        {
            foreach (char character in name)
            {
                bool allowed = (character >= 'a' && character <= 'z')
                    || (character >= 'A' && character <= 'Z')
                    || (character >= '0' && character <= '9')
                    || character == '-';
                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryParsePanel(string? text, out ShotPanel panel)
        {
            switch (text)
            {
                case "routes":
                    panel = ShotPanel.Routes;
                    return true;
                case "contracts":
                    panel = ShotPanel.Contracts;
                    return true;
                case "vehicles":
                    panel = ShotPanel.Vehicles;
                    return true;
                case "unlocks":
                    panel = ShotPanel.Unlocks;
                    return true;
                case "finance":
                    panel = ShotPanel.Finance;
                    return true;
                case "achievements":
                    panel = ShotPanel.Achievements;
                    return true;
                case "industries":
                    panel = ShotPanel.Industries;
                    return true;
                case "towns":
                    panel = ShotPanel.Towns;
                    return true;
                default:
                    panel = default;
                    return false;
            }
        }

        // JsonUtility keeps a field's initial value when the JSON has no such field, so these sentinels mark a field as
        // missing: no valid value equals them (NaN never comes from JSON).
#pragma warning disable CA1051, IDE1006, SA1307, SA1401 // JsonUtility fills public lower-case fields.
        [Serializable]
        private sealed class ShotFileData
        {
            public const int Missing = int.MinValue;

            public string? name;
            public string? save;
            public string? kind;
            public int width = Missing;
            public int height = Missing;
            public string? hud;
            public string?[]? panels;
            public int speed = Missing;
            public int frameRate = Missing;
            public float seconds = float.NaN;
            public CameraKeyData?[]? camera;
        }

        [Serializable]
        private sealed class CameraKeyData
        {
            public float time = float.NaN;
            public float tileX = float.NaN;
            public float tileY = float.NaN;
            public float zoom = float.NaN;
        }
#pragma warning restore CA1051, IDE1006, SA1307, SA1401
    }

    /// <summary>
    /// A shot file that cannot be used: names the file and the field.
    /// </summary>
    public sealed class ShotFileException : Exception
    {
        public ShotFileException(string fileName, string field, string problem)
            : base(fileName + ": " + field + " " + problem + ".")
        {
            FileName = fileName;
            Field = field;
        }

        public string FileName { get; }

        public string Field { get; }
    }
}
