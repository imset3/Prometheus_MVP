using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Narthex.Tools
{
    /// <summary>Creates a small replaceable victory-loop prototype without external tooling.</summary>
    public static class PrometheusVictoryMusicGenerator
    {
        public const string OutputPath =
            "Assets/_Project/Audio/Music/Tutorial/Prototypes/MUS_TUTO_HelteDefeat_Prototype_Loop.wav";
        private const int SampleRate = 44100;
        private const float DurationSeconds = 16f;

        [MenuItem(PrometheusToolMenuPaths.Ai + "Generate Helte Defeat Music Prototype")]
        public static void GenerateMenu() => Generate(true);

        public static void Generate(bool overwrite)
        {
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), OutputPath);
            if (!overwrite && File.Exists(fullPath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? string.Empty);

            var total = Mathf.RoundToInt(SampleRate * DurationSeconds);
            var samples = new float[total];
            for (var index = 0; index < total; index++)
            {
                var time = index / (float)SampleRate;
                var bar = Mathf.FloorToInt(time / 2f) % 8;
                var root = new[] { 293.665f, 369.994f, 440f, 329.628f, 293.665f, 369.994f, 493.883f, 440f }[bar];
                var pad = 0.12f * (Mathf.Sin(Mathf.PI * 2f * root * time) +
                                  0.45f * Mathf.Sin(Mathf.PI * 2f * root * 1.5f * time));
                var beat = Mathf.Repeat(time, 0.5f);
                var noteIndex = Mathf.FloorToInt(time / 0.5f) % 4;
                var melodyRatio = new[] { 2f, 2.5f, 3f, 2.5f }[noteIndex];
                var envelope = Mathf.Clamp01(beat / 0.025f) * Mathf.Clamp01((0.42f - beat) / 0.12f);
                var bell = 0.18f * envelope * (Mathf.Sin(Mathf.PI * 2f * root * melodyRatio * time) +
                                                0.18f * Mathf.Sin(Mathf.PI * 2f * root * melodyRatio * 2f * time));
                var kickEnvelope = Mathf.Clamp01((0.16f - beat) / 0.16f);
                var kick = 0.08f * kickEnvelope * Mathf.Sin(Mathf.PI * 2f * (74f - beat * 120f) * time);
                var fade = Mathf.Min(1f, time / 0.35f) * Mathf.Min(1f, (DurationSeconds - time) / 0.35f);
                samples[index] = Mathf.Clamp((pad + bell + kick) * fade, -0.82f, 0.82f);
            }

            using var stream = File.Create(fullPath);
            using var writer = new BinaryWriter(stream);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + total * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(total * 2);
            foreach (var sample in samples) writer.Write((short)Mathf.RoundToInt(sample * short.MaxValue));
            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
