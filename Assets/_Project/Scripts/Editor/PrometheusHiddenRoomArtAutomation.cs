using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public static class PrometheusHiddenRoomArtAutomation
    {
        private const string ReviewScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string ArtRoot = "Assets/_Project/Art/AIConcepts/TutorialHiddenRoomProps/Generated";
        private const string UpdraftPath = ArtRoot + "/TUTO_B_UpdraftDevice_v1.png";
        private const string PasskeyPath = ArtRoot + "/TUTO_B_AirshipPasskey_v1.png";
        private const string WindMaterialPath = ArtRoot + "/TUTO_B_UpdraftWind_v1.mat";
        private const string UpdraftSlotName = "Updraft_ART_SLOT";
        private const string PasskeySlotName = "AirshipPasskey_ART_SLOT";
        private const string UpdraftVisualName = "UpdraftDeviceVisual_ART";
        private const string PasskeyVisualName = "AirshipPasskeyVisual_ART";
        private static readonly Vector3 UpdraftWorldPosition = new(77.5f, -4.75f, 0f);
        private const float UpdraftWorldHeight = 2.4f;
        private const float PasskeyWorldHeight = 1.5f;

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            if (scene.path != ReviewScenePath)
                throw new InvalidOperationException("Hidden-room art is restricted to " + ReviewScenePath + "; active=" + scene.path);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying hidden-room art.");

            ValidateAssets();
            var updraftSlot = RequireOne(scene, UpdraftSlotName).transform;
            var passkeySlot = RequireOne(scene, PasskeySlotName).transform;
            var toothSlot = passkeySlot.Find("PasskeyTooth_ART_SLOT") ??
                            throw new InvalidOperationException("Missing PasskeyTooth_ART_SLOT under " + PasskeySlotName);
            var changes = DescribeChanges(updraftSlot, passkeySlot, toothSlot);
            if (dryRun || changes.Count == 0) return changes;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply B hidden-room functional art");
            try
            {
                var updraftSprite = ImportSprite(UpdraftPath, new Vector2(0.5f, 0f));
                var passkeySprite = ImportSprite(PasskeyPath, new Vector2(0.5f, 0.5f));
                var windMaterial = UpsertWindMaterial();

                UpsertVisual(updraftSlot, UpdraftVisualName, updraftSprite,
                    UpdraftWorldPosition, UpdraftWorldHeight, 6);
                UpsertVisual(passkeySlot, PasskeyVisualName, passkeySprite,
                    passkeySlot.position, PasskeyWorldHeight, 8);
                ApplyWindMaterial(updraftSlot, windMaterial);
                SetLegacyRendererEnabled(passkeySlot, false);
                SetLegacyRendererEnabled(toothSlot, false);

                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                return changes;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }

        private static List<PrometheusAiChange> DescribeChanges(
            Transform updraftSlot, Transform passkeySlot, Transform toothSlot)
        {
            var changes = new List<PrometheusAiChange>();
            DescribeVisual(changes, updraftSlot, UpdraftVisualName, UpdraftPath,
                UpdraftWorldPosition, UpdraftWorldHeight, "upsert-updraft-device");
            DescribeVisual(changes, passkeySlot, PasskeyVisualName, PasskeyPath,
                passkeySlot.position, PasskeyWorldHeight, "upsert-airship-passkey");
            foreach (var strip in WindStrips(updraftSlot))
            {
                var renderer = strip.GetComponent<MeshRenderer>();
                var current = renderer == null || renderer.sharedMaterial == null
                    ? "missing"
                    : AssetDatabase.GetAssetPath(renderer.sharedMaterial);
                if (current != WindMaterialPath)
                    changes.Add(Change("style-updraft-wind", strip.gameObject, current, WindMaterialPath));
            }
            DescribeLegacyRenderer(changes, passkeySlot);
            DescribeLegacyRenderer(changes, toothSlot);
            return changes;
        }

        private static void DescribeVisual(List<PrometheusAiChange> changes, Transform parent,
            string name, string spritePath, Vector3 worldPosition, float worldHeight, string action)
        {
            var visual = parent.Find(name);
            var renderer = visual == null ? null : visual.GetComponent<SpriteRenderer>();
            var current = renderer == null || renderer.sprite == null
                ? "missing"
                : AssetDatabase.GetAssetPath(renderer.sprite);
            var matches = current == spritePath && Approximately(visual.position, worldPosition) &&
                          Mathf.Abs(renderer.bounds.size.y - worldHeight) <= 0.05f;
            if (matches) return;
            changes.Add(Change(action, visual == null ? parent.gameObject : visual.gameObject,
                current, spritePath));
        }

        private static void DescribeLegacyRenderer(List<PrometheusAiChange> changes, Transform target)
        {
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled) return;
            changes.Add(Change("disable-legacy-passkey-mesh", target.gameObject, "enabled", "disabled"));
        }

        private static void UpsertVisual(Transform parent, string name, Sprite sprite,
            Vector3 worldPosition, float worldHeight, int sortingOrder)
        {
            var visual = parent.Find(name);
            if (visual == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create hidden-room art visual");
                visual = go.transform;
                visual.SetParent(parent, false);
            }

            var renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = Undo.AddComponent<SpriteRenderer>(visual.gameObject);
            Undo.RecordObject(renderer, "Configure hidden-room sprite");
            Undo.RecordObject(visual, "Place hidden-room sprite");
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.sortingOrder = sortingOrder;
            visual.position = worldPosition;
            visual.rotation = Quaternion.identity;
            var worldScale = worldHeight / Mathf.Max(0.01f, sprite.bounds.size.y);
            var parentScale = parent.lossyScale;
            visual.localScale = new Vector3(
                worldScale / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)),
                worldScale / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)),
                1f / Mathf.Max(0.01f, Mathf.Abs(parentScale.z)));
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(visual);
        }

        private static void SetLegacyRendererEnabled(Transform target, bool enabled)
        {
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.enabled == enabled) return;
            Undo.RecordObject(renderer, "Replace legacy hidden-room mesh art");
            renderer.enabled = enabled;
            EditorUtility.SetDirty(renderer);
        }

        private static Material UpsertWindMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(WindMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Sprites/Default") ??
                             throw new InvalidOperationException("Sprites/Default shader was not found.");
                material = new Material(shader) { name = "TUTO_B_UpdraftWind_v1" };
                AssetDatabase.CreateAsset(material, WindMaterialPath);
            }
            material.color = new Color(0.52f, 0.94f, 1f, 0.24f);
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyWindMaterial(Transform updraftSlot, Material material)
        {
            foreach (var strip in WindStrips(updraftSlot))
            {
                var renderer = strip.GetComponent<MeshRenderer>() ??
                               throw new InvalidOperationException("Missing MeshRenderer on " + strip.name);
                if (renderer.sharedMaterial == material) continue;
                Undo.RecordObject(renderer, "Style hidden-room updraft wind");
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static IEnumerable<Transform> WindStrips(Transform updraftSlot)
        {
            for (var index = 1; index <= 5; index++)
            {
                var name = $"WindStrip_{index:00}";
                var strip = updraftSlot.Find(name) ??
                            throw new InvalidOperationException("Missing " + name + " under " + UpdraftSlotName);
                yield return strip;
            }
        }

        private static Sprite ImportSprite(string path, Vector2 pivot)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("No TextureImporter: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 256f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Hidden-room sprite import failed: " + path);
            return sprite;
        }

        private static PrometheusAiChange Change(string action, GameObject target, string before, string after) =>
            new()
            {
                action = action,
                objectId = PrometheusSceneQuery.ObjectId(target),
                hierarchyPath = PrometheusSceneQuery.Path(target),
                before = before,
                after = after
            };

        private static GameObject RequireOne(Scene scene, string name)
        {
            var matches = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item.scene == scene && item.name == name).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Expected one " + name + ", found " + matches.Length);
            return matches[0];
        }

        private static bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= 0.0025f;

        private static void ValidateAssets()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            foreach (var path in new[] { UpdraftPath, PasskeyPath })
                if (!File.Exists(Path.Combine(projectRoot, path)))
                    throw new FileNotFoundException("Missing B hidden-room art asset", path);
        }
    }
}
