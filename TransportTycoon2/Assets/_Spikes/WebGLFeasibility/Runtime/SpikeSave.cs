#nullable enable
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace TransportTycoon.Spikes.WebGL
{
    // Bridge to the browser. Falls back to harmless stubs outside WebGL builds.
    public static class SpikeWeb
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void SpikeIdb_Init();
        [DllImport("__Internal")] private static extern int SpikeIdb_Put(string key, byte[] data, int length);
        [DllImport("__Internal")] private static extern int SpikeIdb_Get(string key);
        [DllImport("__Internal")] private static extern int SpikeIdb_State(int id);
        [DllImport("__Internal")] private static extern int SpikeIdb_ResultSize(int id);
        [DllImport("__Internal")] private static extern void SpikeIdb_ReadResult(int id, byte[] dest, int length);
        [DllImport("__Internal")] private static extern void SpikeWeb_Publish(string text);
        [DllImport("__Internal")] private static extern IntPtr SpikeWeb_GetQuery();

        public static bool IsAvailable => true;
        public static void Init() => SpikeIdb_Init();
        public static int Put(string key, byte[] data) => SpikeIdb_Put(key, data, data.Length);
        public static int Get(string key) => SpikeIdb_Get(key);
        public static int State(int id) => SpikeIdb_State(id);
        public static int ResultSize(int id) => SpikeIdb_ResultSize(id);
        public static void ReadResult(int id, byte[] dest) => SpikeIdb_ReadResult(id, dest, dest.Length);
        public static void Publish(string text) => SpikeWeb_Publish(text);
        public static string Query() => Marshal.PtrToStringUTF8(SpikeWeb_GetQuery()) ?? string.Empty;
#else
        public static bool IsAvailable => false;
        public static void Init() { }
        public static int Put(string key, byte[] data) => throw new NotSupportedException();
        public static int Get(string key) => throw new NotSupportedException();
        public static int State(int id) => throw new NotSupportedException();
        public static int ResultSize(int id) => throw new NotSupportedException();
        public static void ReadResult(int id, byte[] dest) => throw new NotSupportedException();
        public static void Publish(string text) { }
        public static string Query() => string.Empty;
#endif
    }

    // A deterministic, realistic-looking save payload for a 512x512 map (tile arrays plus an entity list as JSON).
    public static class SpikePayload
    {
        public static byte[] Build()
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                for (int y = 0; y < SpikeIso.MapSize; y++)
                {
                    for (int x = 0; x < SpikeIso.MapSize; x++)
                    {
                        writer.Write((byte)SpikeIso.TerrainAt(x, y));
                        writer.Write((byte)((x * 31 + y * 17) & 0x0F));
                        writer.Write((ushort)((x ^ y) & 0xFF));
                        writer.Write((ushort)(x * y & 0x3FF));
                    }
                }

                var json = new StringBuilder();
                json.Append("{\"entities\":[");
                for (int i = 0; i < 20000; i++)
                {
                    if (i > 0)
                    {
                        json.Append(',');
                    }

                    json.Append("{\"id\":").Append(i)
                        .Append(",\"x\":").Append(i * 7 % SpikeIso.MapSize)
                        .Append(",\"y\":").Append(i * 13 % SpikeIso.MapSize)
                        .Append(",\"cargo\":").Append(i % 5)
                        .Append(",\"amount\":").Append(i * 3 % 40).Append('}');
                }

                json.Append("]}");
                writer.Write(Encoding.UTF8.GetBytes(json.ToString()));
            }

            return stream.ToArray();
        }

        public static ulong Hash(byte[] data)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < data.Length; i++)
            {
                hash = (hash ^ data[i]) * 1099511628211UL;
            }

            return hash;
        }

        public static byte[] Gzip(byte[] data, System.IO.Compression.CompressionLevel level)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, level, true))
            {
                gzip.Write(data, 0, data.Length);
            }

            return output.ToArray();
        }

        public static byte[] Gunzip(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }

    // Runs the persistence checks: gzip cost, then write and read back through two candidate stores.
    public static class SpikeSaveTest
    {
        private const string Key = "spike-save.bin.gz";

        public static IEnumerator Write(Action<string> log)
        {
            var sw = Stopwatch.StartNew();
            byte[] raw = SpikePayload.Build();
            log($"save.payload rawBytes={raw.Length} buildMs={sw.ElapsedMilliseconds}");

            sw.Restart();
            byte[] fast = SpikePayload.Gzip(raw, System.IO.Compression.CompressionLevel.Fastest);
            log($"save.gzip.fastest bytes={fast.Length} ms={sw.ElapsedMilliseconds}");

            sw.Restart();
            byte[] optimal = SpikePayload.Gzip(raw, System.IO.Compression.CompressionLevel.Optimal);
            log($"save.gzip.optimal bytes={optimal.Length} ms={sw.ElapsedMilliseconds}");

            sw.Restart();
            byte[] back = SpikePayload.Gunzip(optimal);
            log($"save.gunzip ms={sw.ElapsedMilliseconds} roundTripOk={SpikePayload.Hash(back) == SpikePayload.Hash(raw)}");

            sw.Restart();
            string filePath = Path.Combine(Application.persistentDataPath, Key);
            File.WriteAllBytes(filePath, optimal);
            log($"save.file.write path={Application.persistentDataPath} ms={sw.ElapsedMilliseconds}");

            if (SpikeWeb.IsAvailable)
            {
                SpikeWeb.Init();
                sw.Restart();
                int op = SpikeWeb.Put(Key, optimal);
                while (SpikeWeb.State(op) == 0)
                {
                    yield return null;
                }

                log($"save.idb.write state={SpikeWeb.State(op)} ms={sw.ElapsedMilliseconds}");
            }
        }

        public static IEnumerator Verify(Action<string> log)
        {
            byte[] expected = SpikePayload.Build();
            ulong expectedHash = SpikePayload.Hash(expected);

            var sw = Stopwatch.StartNew();
            string filePath = Path.Combine(Application.persistentDataPath, Key);
            if (File.Exists(filePath))
            {
                bool ok = SpikePayload.Hash(SpikePayload.Gunzip(File.ReadAllBytes(filePath))) == expectedHash;
                log($"verify.file found=true ok={ok} ms={sw.ElapsedMilliseconds}");
            }
            else
            {
                log("verify.file found=false");
            }

            if (SpikeWeb.IsAvailable)
            {
                SpikeWeb.Init();
                sw.Restart();
                int op = SpikeWeb.Get(Key);
                while (SpikeWeb.State(op) == 0)
                {
                    yield return null;
                }

                int size = SpikeWeb.ResultSize(op);
                if (size < 0)
                {
                    log("verify.idb found=false");
                }
                else
                {
                    var buffer = new byte[size];
                    SpikeWeb.ReadResult(op, buffer);
                    bool ok = SpikePayload.Hash(SpikePayload.Gunzip(buffer)) == expectedHash;
                    log($"verify.idb found=true bytes={size} ok={ok} ms={sw.ElapsedMilliseconds}");
                }
            }
        }
    }
}
