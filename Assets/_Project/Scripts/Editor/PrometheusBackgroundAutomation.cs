using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Narthex.Core;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public static class PrometheusBackgroundAutomation
    {
        private const string RootName = "AI_TutorialBackgroundRoot";

        public static PrometheusAiChange Apply(
            Scene scene,
            string locationKey,
            string spritePath,
            float opacity,
            int sortingOrder,
            float cameraSpaceDepth,
            bool dryRun)
        {
            var normalizedKey = NormalizeKey(locationKey);
            ValidateAssetPath(spritePath);
            var camera = FindTargetCamera(scene);
            if (camera == null)
                throw new InvalidOperationException("Tutorial scene has no Camera for the background backplate.");
            var serviceRoot = PrometheusSceneQuery.All(scene)
                .Select(item => item.GetComponent<ServiceRoot>())
                .FirstOrDefault(item => item != null);

            var existingRoot = scene.GetRootGameObjects()
                .FirstOrDefault(item => string.Equals(item.name, RootName, StringComparison.Ordinal));
            var existingVisual = existingRoot != null
                ? existingRoot.transform.Find($"Background_{normalizedKey}")?.gameObject
                : null;
            var before = existingVisual == null
                ? "missing"
                : Describe(existingVisual.GetComponent<SpriteRenderer>());
            var after =
                $"sprite={spritePath}; key={normalizedKey}; opacity={opacity.ToString("0.###", CultureInfo.InvariantCulture)}; " +
                $"sortingOrder={sortingOrder}; cameraDepth={cameraSpaceDepth.ToString("0.###", CultureInfo.InvariantCulture)}";
            var change = new PrometheusAiChange
            {
                action = existingVisual == null ? "create-background-backplate" : "update-background-backplate",
                hierarchyPath = $"{RootName}/Background_{normalizedKey}",
                before = before,
                after = after
            };
            if (dryRun) return change;

            var sprite = ImportSprite(spritePath);
            var root = existingRoot;
            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Create tutorial background root");
            }

            var presenter = root.GetComponent<TutorialBackgroundPresenter>();
            if (presenter == null)
                presenter = Undo.AddComponent<TutorialBackgroundPresenter>(root);
            Undo.RecordObject(presenter, "Configure tutorial background presenter");
            presenter.Configure(serviceRoot, camera, "A", cameraSpaceDepth);

            var visual = existingVisual;
            if (visual == null)
            {
                visual = new GameObject($"Background_{normalizedKey}");
                Undo.RegisterCreatedObjectUndo(visual, "Create tutorial background visual");
                visual.transform.SetParent(root.transform, false);
            }

            var renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = Undo.AddComponent<SpriteRenderer>(visual);
            Undo.RecordObject(renderer, "Configure tutorial background renderer");
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(opacity));
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            Undo.RecordObject(visual.transform, "Reset tutorial background transform");
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            presenter.UpsertEntry(normalizedKey, visual);
            EditorUtility.SetDirty(presenter);
            EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(scene);

            change.objectId = PrometheusSceneQuery.ObjectId(visual);
            change.hierarchyPath = PrometheusSceneQuery.Path(visual);
            return change;
        }

        private static Sprite ImportSprite(string spritePath)
        {
            AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(spritePath) is not TextureImporter importer)
                throw new InvalidOperationException($"Asset is not an importable texture: {spritePath}");

            var changed = importer.textureType != TextureImporterType.Sprite ||
                          importer.spriteImportMode != SpriteImportMode.Single ||
                          importer.mipmapEnabled ||
                          importer.alphaIsTransparency;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = false;
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
            return cameras.FirstOrDefault(item => item.CompareTag("MainCamera")) ??
                   cameras.FirstOrDefault();
        }

        private static string NormalizeKey(string key)
        {
            var normalized = (key ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized.Length != 1 || normalized[0] < 'A' || normalized[0] > 'H')
                throw new ArgumentException("Argument 'locationKey' must be one letter from A through H.");
            return normalized;
        }

        private static void ValidateAssetPath(string spritePath)
        {
            if (string.IsNullOrWhiteSpace(spritePath) ||
                !spritePath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !File.Exists(spritePath))
                throw new FileNotFoundException(
                    "Background sprite must be an existing project asset under Assets/.",
                    spritePath);
        }

        private static string Describe(SpriteRenderer renderer)
        {
            if (renderer == null) return "missing SpriteRenderer";
            return
                $"sprite={AssetDatabase.GetAssetPath(renderer.sprite)}; opacity={renderer.color.a.ToString("0.###", CultureInfo.InvariantCulture)}; " +
                $"sortingOrder={renderer.sortingOrder}";
        }
    }
}
