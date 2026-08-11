using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Narthex.SceneFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public static class PrometheusTitleSceneAutomation
    {
        public const string ScenePath = "Assets/Scenes/TitleScene.unity";
        private const string BackgroundPath =
            "Assets/_Project/Art/AIConcepts/TitleScreen/Generated/PROMETHEUS_TitleBackgroundClean_v1.png";
        private const string ZenithPath =
            "Assets/_Project/Art/AIConcepts/TutorialBackgrounds/TUTO_Zenith_Continuous_Cutout_v6.png";
        private const string PromeFolder = "Assets/_Project/Art/Motions/Prome/Idle";
        private const string LogoFramePath =
            "Assets/_Project/Resources/UI/Title/TITLE_UI_LogoFrame_v1.png";
        private const string ButtonFramePath =
            "Assets/_Project/Resources/UI/Title/TITLE_UI_ButtonPlate_v1.png";
        private const string LoadingCompassPath =
            "Assets/_Project/Resources/UI/Title/TITLE_UI_LoadingCompass_v1.png";
        private const string ModalPanelPath =
            "Assets/_Project/Resources/UI/Title/TITLE_UI_ModalPanel_v1.png";
        private const string TitleFontPath = "Assets/_Project/Art/Fonts/GoogleFonts/DoHyeon-Regular.ttf";
        private const string BodyFontPath = "Assets/_Project/Art/Fonts/GoogleFonts/GowunDodum-Regular.ttf";
        private const string MusicPath =
            "Assets/_Project/Audio/Music/Tutorial/Prototypes/MUS_TITLE_Prometheus_Prototype_Loop.wav";

        [MenuItem(PrometheusToolMenuPaths.Ai + "Create or Update Title Scene")]
        public static void CreateOrUpdateMenu()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (string.IsNullOrWhiteSpace(scene.path)) EditorSceneManager.SaveScene(scene, ScenePath);

            var beforePath = "Temp/PrometheusSceneToolkit/title-before.json";
            var afterPath = "Temp/PrometheusSceneToolkit/title-after.json";
            PrometheusSceneSnapshotService.Save(PrometheusSceneSnapshotService.Capture(scene), beforePath);
            var preview = Apply(scene, true);
            Debug.Log($"[sragon000][Title] Dry-run: {string.Join(" | ", preview.Select(item => item.after))}");
            var changes = Apply(scene, false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EnsureBuildSettings();
            var issues = PrometheusSceneDoctor.Scan(scene);
            PrometheusSceneSnapshotService.Save(PrometheusSceneSnapshotService.Capture(scene), afterPath);
            var comparison = PrometheusSceneSnapshotService.Compare(
                PrometheusSceneSnapshotService.Load(beforePath),
                PrometheusSceneSnapshotService.Load(afterPath));
            var deltaCount = comparison.added.Count + comparison.modified.Count + comparison.removed.Count;
            Selection.activeGameObject = FindRoot(scene, "TitleScreenRoot");
            Debug.Log($"[sragon000][Title] Applied {changes.Count} change(s), Doctor {issues.Count} issue(s), " +
                      $"snapshot delta {deltaCount}. {ScenePath}");
        }

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            var changes = new List<PrometheusAiChange>();
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Title scene must be loaded.");
            changes.Add(Change("title-root", "Create or update MainCamera and TitleScreenRoot"));
            changes.Add(Change("title-art", "Bind clean background, resized Prome, enlarged Zenith, UI sprites and title music"));
            changes.Add(Change("build-settings", "Register Title/Tutorial/Boss Development scenes in Build Settings"));
            if (dryRun) return changes;

            PrometheusTitleButtonLabelGenerator.GenerateAll();
            ConfigureSpriteImporter(BackgroundPath, 100f);
            ConfigureSpriteImporter(ZenithPath, 100f);
            ConfigureSpriteImporter(LogoFramePath, 100f);
            ConfigureSpriteImporter(ButtonFramePath, 100f);
            ConfigureSpriteImporter(LoadingCompassPath, 100f);
            ConfigureSpriteImporter(ModalPanelPath, 100f);
            var cameraObject = FindRoot(scene, "Main Camera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
            }
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.015f, 0.028f, 1f);
            camera.orthographic = true;

            var root = FindRoot(scene, "TitleScreenRoot");
            if (root == null)
            {
                root = new GameObject("TitleScreenRoot");
                SceneManager.MoveGameObjectToScene(root, scene);
            }
            var host = root.GetComponent<TitleScreenHost>();
            if (host == null) host = root.AddComponent<TitleScreenHost>();
            var serialized = new SerializedObject(host);
            serialized.FindProperty("backgroundSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            serialized.FindProperty("zenithSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(ZenithPath);
            serialized.FindProperty("titleLogoFrameSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(LogoFramePath);
            serialized.FindProperty("buttonFrameSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(ButtonFramePath);
            serialized.FindProperty("loadingCompassSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(LoadingCompassPath);
            serialized.FindProperty("modalPanelSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(ModalPanelPath);
            AssignLabelSprite(serialized, "newGameLabelSprite", "TITLE_LABEL_NewGame_v1");
            AssignLabelSprite(serialized, "continueLabelSprite", "TITLE_LABEL_Continue_v1");
            AssignLabelSprite(serialized, "bossLabelSprite", "TITLE_LABEL_Boss_v1");
            AssignLabelSprite(serialized, "settingsLabelSprite", "TITLE_LABEL_Settings_v1");
            AssignLabelSprite(serialized, "quitLabelSprite", "TITLE_LABEL_Quit_v1");
            AssignLabelSprite(serialized, "applyLabelSprite", "TITLE_LABEL_Apply_v1");
            AssignLabelSprite(serialized, "backLabelSprite", "TITLE_LABEL_Back_v1");
            serialized.FindProperty("titleFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(TitleFontPath);
            serialized.FindProperty("bodyFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(BodyFontPath);
            serialized.FindProperty("titleMusic").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
            serialized.FindProperty("tutorialSceneName").stringValue = "TutorialScene";
            serialized.FindProperty("bossSceneName").stringValue = "BossDevelopmentScene";

            var frames = AssetDatabase.FindAssets("t:Sprite", new[] { PromeFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(sprite => sprite != null)
                .ToArray();
            var frameProperty = serialized.FindProperty("promeIdleFrames");
            frameProperty.arraySize = frames.Length;
            for (var index = 0; index < frames.Length; index++)
                frameProperty.GetArrayElementAtIndex(index).objectReferenceValue = frames[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            host.RebuildAuthoredPresentation();
            EditorUtility.SetDirty(host);
            EnsureBuildSettings();
            return changes;
        }

        private static void AssignLabelSprite(SerializedObject serialized, string propertyName, string assetName)
        {
            serialized.FindProperty(propertyName).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(
                PrometheusTitleButtonLabelGenerator.GetAssetPath(assetName));
        }

        private static PrometheusAiChange Change(string action, string after) => new()
        {
            action = action,
            hierarchyPath = "TitleScreenRoot",
            before = "existing or missing",
            after = after
        };

        private static void ConfigureSpriteImporter(string path, float pixelsPerUnit)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            var changed = importer.textureType != TextureImporterType.Sprite ||
                          importer.spriteImportMode != SpriteImportMode.Single ||
                          !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            if (changed) importer.SaveAndReimport();
        }

        private static GameObject FindRoot(Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);

        private static void EnsureBuildSettings()
        {
            var preferred = new[]
            {
                ScenePath,
                "Assets/_Project/Scenes/Boot.unity",
                "Assets/Scenes/TutorialScene.unity",
                "Assets/Scenes/BossDevelopmentScene.unity",
                "Assets/Scenes/Chapter01.unity"
            };
            var existing = EditorBuildSettings.scenes.ToDictionary(scene => scene.path, scene => scene.enabled);
            var output = new List<EditorBuildSettingsScene>();
            foreach (var path in preferred)
            {
                if (string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(path))) continue;
                output.Add(new EditorBuildSettingsScene(path, true));
            }
            foreach (var scene in EditorBuildSettings.scenes)
                if (!preferred.Contains(scene.path)) output.Add(scene);
            EditorBuildSettings.scenes = output.ToArray();
        }
    }
}
