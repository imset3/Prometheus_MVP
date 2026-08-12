using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Narthex.Tools
{
    public static class PrometheusReleaseBuildAutomation
    {
        private static readonly string[] RequiredScenePaths =
        {
            "Assets/Scenes/TitleScene.unity",
            "Assets/_Project/Scenes/Boot.unity",
            "Assets/Scenes/TutorialScene.unity",
            "Assets/Scenes/BossDevelopmentScene.unity",
            "Assets/Scenes/Chapter01.unity"
        };

        [MenuItem("sragon000/Build/Release/Build Windows x64")]
        public static void BuildWindows() => Build(
            BuildTarget.StandaloneWindows64,
            "Builds/Release/DEMO_V2/Windows/Prometheus_MVP.exe");

        [MenuItem("sragon000/Build/Release/Build macOS")]
        public static void BuildMacOS() => Build(
            BuildTarget.StandaloneOSX,
            "Builds/Release/DEMO_V2/macOS/Prometheus_MVP.app");

        [MenuItem("sragon000/Build/Release/Build Windows and macOS")]
        public static void BuildAllDesktop()
        {
            BuildWindows();
            BuildMacOS();
        }

        public static string[] GetRequiredScenePaths() => RequiredScenePaths.ToArray();

        private static void Build(BuildTarget target, string locationPathName)
        {
            EnsureRequiredScenes();
            var outputDirectory = Path.GetDirectoryName(locationPathName);
            if (!string.IsNullOrWhiteSpace(outputDirectory)) Directory.CreateDirectory(outputDirectory);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = RequiredScenePaths,
                target = target,
                locationPathName = locationPathName,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"{target} build failed: {report.summary.result}, " +
                    $"errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}");

            Debug.Log(
                $"[sragon000][Release Build] {target} complete: {locationPathName}, " +
                $"size={report.summary.totalSize}, duration={report.summary.totalTime}");
        }

        private static void EnsureRequiredScenes()
        {
            foreach (var scenePath in RequiredScenePaths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                    throw new FileNotFoundException("Required release scene is missing.", scenePath);
            }

            var configured = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (configured.SequenceEqual(RequiredScenePaths)) return;

            EditorBuildSettings.scenes = RequiredScenePaths
                .Select(scenePath => new EditorBuildSettingsScene(scenePath, true))
                .ToArray();
            Debug.Log("[sragon000][Release Build] Required game-flow scenes were synchronized in Build Settings.");
        }
    }
}
