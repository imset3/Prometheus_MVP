using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public static class PrometheusZenithApproachAutomation
    {
        private const string RootName = "AI_TutorialBackgroundRoot";
        private const string VisualName = "Zenith_Continuous";

        public static PrometheusAiChange Apply(
            Scene scene,
            string spritePath,
            string playerPath,
            float startWorldX,
            float endWorldX,
            Vector2 farViewportAnchor,
            Vector2 nearViewportAnchor,
            float farScreenWidth,
            float nearScreenWidth,
            float farOpacity,
            float nearOpacity,
            int sortingOrder,
            bool dryRun)
        {
            ValidateAssetPath(spritePath);
            if (endWorldX <= startWorldX)
                throw new ArgumentException("endWorldX must be greater than startWorldX.");

            var camera = FindTargetCamera(scene);
            if (camera == null)
                throw new InvalidOperationException("Tutorial scene has no Camera for Zenith approach presentation.");
            var player = ResolvePlayer(scene, playerPath);
            if (player == null)
                throw new InvalidOperationException($"Player target was not found at '{playerPath}'.");

            var root = scene.GetRootGameObjects()
                .FirstOrDefault(item => string.Equals(item.name, RootName, StringComparison.Ordinal));
            var visual = root != null ? root.transform.Find(VisualName)?.gameObject : null;
            var before = visual == null
                ? "missing"
                : Describe(visual.GetComponent<SpriteRenderer>(), root.GetComponent<ZenithApproachPresenter>());
            var after =
                $"sprite={spritePath}; player={PrometheusSceneQuery.Path(player)}; " +
                $"worldX={Format(startWorldX)}..{Format(endWorldX)}; " +
                $"screenWidth={Format(farScreenWidth)}..{Format(nearScreenWidth)}; " +
                $"sortingOrder={sortingOrder}";
            var change = new PrometheusAiChange
            {
                action = visual == null ? "create-zenith-approach" : "update-zenith-approach",
                hierarchyPath = $"{RootName}/{VisualName}",
                before = before,
                after = after
            };
            if (dryRun) return change;

            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Create tutorial background root");
            }

            var presenter = root.GetComponent<ZenithApproachPresenter>();
            if (presenter == null)
                presenter = Undo.AddComponent<ZenithApproachPresenter>(root);

            if (visual == null)
            {
                visual = new GameObject(VisualName);
                Undo.RegisterCreatedObjectUndo(visual, "Create continuous Zenith visual");
                visual.transform.SetParent(root.transform, false);
            }

            var renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = Undo.AddComponent<SpriteRenderer>(visual);
            Undo.RecordObject(renderer, "Configure continuous Zenith renderer");
            renderer.sprite = ImportTransparentSprite(spritePath);
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            Undo.RecordObject(presenter, "Configure continuous Zenith approach");
            presenter.Configure(
                player.transform,
                camera,
                renderer,
                startWorldX,
                endWorldX,
                farViewportAnchor,
                nearViewportAnchor,
                farScreenWidth,
                nearScreenWidth,
                farOpacity,
                nearOpacity);

            EditorUtility.SetDirty(presenter);
            EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(scene);

            change.objectId = PrometheusSceneQuery.ObjectId(visual);
            change.hierarchyPath = PrometheusSceneQuery.Path(visual);
            return change;
        }

        private static Sprite ImportTransparentSprite(string spritePath)
        {
            AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(spritePath) is not TextureImporter importer)
                throw new InvalidOperationException($"Asset is not an importable texture: {spritePath}");

            var changed = importer.textureType != TextureImporterType.Sprite ||
                          importer.spriteImportMode != SpriteImportMode.Single ||
                          importer.mipmapEnabled ||
                          !importer.alphaIsTransparency;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
                throw new InvalidOperationException($"Sprite import failed: {spritePath}");
            return sprite;
        }

        private static Camera FindTargetCamera(Scene scene)
        {
            var cameras = PrometheusSceneQuery.All(scene)
                .Select(item => item.GetComponent<Camera>())
                .Where(item => item != null)
                .ToArray();
            return cameras.FirstOrDefault(item => item.CompareTag("MainCamera")) ?? cameras.FirstOrDefault();
        }

        private static GameObject ResolvePlayer(Scene scene, string playerPath)
        {
            var normalizedPath = string.IsNullOrWhiteSpace(playerPath)
                ? "TutorialRuntimeRoot/StageRoot/PlayerRoot"
                : playerPath.Trim().TrimStart('/');
            return PrometheusSceneQuery.All(scene).FirstOrDefault(item =>
                string.Equals(PrometheusSceneQuery.Path(item), normalizedPath, StringComparison.Ordinal));
        }

        private static void ValidateAssetPath(string spritePath)
        {
            if (string.IsNullOrWhiteSpace(spritePath) ||
                !spritePath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !File.Exists(spritePath))
                throw new FileNotFoundException(
                    "Zenith sprite must be an existing project asset under Assets/.", spritePath);
        }

        private static string Describe(SpriteRenderer renderer, ZenithApproachPresenter presenter)
        {
            if (renderer == null || presenter == null) return "incomplete";
            return
                $"sprite={AssetDatabase.GetAssetPath(renderer.sprite)}; " +
                $"worldX={Format(presenter.StartWorldX)}..{Format(presenter.EndWorldX)}; " +
                $"sortingOrder={renderer.sortingOrder}";
        }

        private static string Format(float value) =>
            value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
