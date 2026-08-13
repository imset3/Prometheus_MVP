using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Narthex.Gameplay;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public static class PrometheusPromeMotionNormalizationAutomation
    {
        private const string MotionRoot = "Assets/_Project/Art/Motions/Prome";
        private static readonly string[] MotionNames = { "Idle", "Run", "Jump", "Dash", "Attack01" };

        public static List<PrometheusAiChange> Normalize(Scene scene, bool dryRun)
        {
            var framePaths = CollectFramePaths();
            var changes = framePaths.Select(path => new PrometheusAiChange
            {
                action = "normalize-prome-foot-pivot",
                hierarchyPath = path,
                after = "opaque-foot pivot; <= 0.05 world-unit baseline error"
            }).ToList();
            changes.Add(new PrometheusAiChange
            {
                action = "author-attack-impact-event",
                hierarchyPath = "PlayerVisual/CharacterSprite_ART",
                after = "Attack01 0.35s; impact event 0.16s"
            });
            if (dryRun) return changes;

            // SpriteDataProvider pivot writes are discarded when queued inside
            // StartAssetEditing on Unity 6. Import each frame immediately so the
            // authored foot pivot is guaranteed to survive the reimport.
            foreach (var path in framePaths)
                NormalizeFrame(path);

            CharacterPngSequenceSetupWindow.BuildPromeAttack01Clip();
            CharacterPngSequenceSetupWindow.BuildPromeDashAndJumpClips();
            // Clip rebuilds can refresh texture importers. Re-assert the authored
            // pivots after all clip asset work, then do not refresh again.
            foreach (var path in framePaths)
                NormalizeFrame(path);
            AuthorAttackRelays(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return changes;
        }

        public static List<PrometheusAiChange> Validate(Scene scene)
        {
            var issues = new List<PrometheusAiChange>();
            foreach (var path in CollectFramePaths())
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || !TryCalculateFootPivot(path, out var expected, out _, out _))
                {
                    issues.Add(Issue("prome-frame-unreadable", path, "readable PNG with opaque pixels"));
                    continue;
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var ppu = Mathf.Max(1f, importer.spritePixelsPerUnit);
                var height = sprite != null ? sprite.rect.height : 0f;
                var verticalError = height > 0f
                    ? Mathf.Abs((importer.spritePivot.y - expected.y) * height / ppu)
                    : float.PositiveInfinity;
                var providerFactories = new SpriteDataProviderFactories();
                providerFactories.Init();
                var spriteProvider = providerFactories.GetSpriteEditorDataProviderFromObject(importer);
                spriteProvider.InitSpriteEditorDataProvider();
                var authoredRects = spriteProvider.GetSpriteRects();
                var authoredPivot = authoredRects.Length == 1 ? authoredRects[0].pivot : importer.spritePivot;
                var authoredAlignment = authoredRects.Length == 1
                    ? authoredRects[0].alignment
                    : SpriteAlignment.Center;
                verticalError = height > 0f
                    ? Mathf.Abs((authoredPivot.y - expected.y) * height / ppu)
                    : float.PositiveInfinity;
                if (authoredAlignment != SpriteAlignment.Custom || verticalError > 0.05f)
                    issues.Add(Issue("prome-foot-pivot-error", path,
                        $"custom pivot; vertical error <= 0.05 (actual {verticalError:0.###})"));
            }

            foreach (var bridge in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<CharacterPngAnimationBridge>(true))
                         .Where(item => item.Preset == CharacterPngAnimationPreset.Prome))
            {
                var animator = bridge.GetComponentInChildren<Animator>(true);
                var relay = animator != null ? animator.GetComponent<AttackImpactAnimationRelay>() : null;
                var melee = bridge.GetComponentInParent<MeleeAttackHost>();
                if (relay == null || !relay.HasValidSetup || melee == null || !melee.UsesAnimationEventImpact)
                    issues.Add(Issue("prome-attack-relay-missing", PrometheusSceneQuery.Path(bridge.gameObject),
                        "authored AttackImpactAnimationRelay and animation-event impact"));
            }
            return issues;
        }

        public static List<PrometheusAiChange> AlignVisualFootToCollider(Scene scene, bool dryRun)
        {
            var changes = new List<PrometheusAiChange>();
            foreach (var bridge in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<CharacterPngAnimationBridge>(true))
                         .Where(item => item.Preset == CharacterPngAnimationPreset.Prome))
            {
                var actor = bridge.GetComponentInParent<CombatActorHost>();
                var bodyCollider = actor != null ? actor.GetComponent<Collider2D>() : null;
                var renderer = bridge.GetComponentInChildren<SpriteRenderer>(true);
                if (bodyCollider == null || renderer == null) continue;

                var visual = renderer.transform;
                var targetWorldY = bodyCollider.bounds.min.y;
                changes.Add(new PrometheusAiChange
                {
                    action = "align-prome-visible-foot",
                    hierarchyPath = PrometheusSceneQuery.Path(visual.gameObject),
                    before = $"worldY={visual.position.y:0.###}; colliderBottom={targetWorldY:0.###}",
                    after = $"worldY={targetWorldY:0.###}; foot/collider error=0"
                });
                if (dryRun) continue;

                Undo.RecordObject(visual, "Align Prome visible foot to collider");
                var world = visual.position;
                world.y = targetWorldY;
                visual.position = world;
                EditorUtility.SetDirty(visual);
            }

            if (!dryRun && changes.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            return changes;
        }

        private static void AuthorAttackRelays(Scene scene)
        {
            foreach (var bridge in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<CharacterPngAnimationBridge>(true))
                         .Where(item => item.Preset == CharacterPngAnimationPreset.Prome))
            {
                var animator = bridge.GetComponentInChildren<Animator>(true);
                var melee = bridge.GetComponentInParent<MeleeAttackHost>();
                if (animator == null || melee == null) continue;
                var relay = animator.GetComponent<AttackImpactAnimationRelay>() ??
                            Undo.AddComponent<AttackImpactAnimationRelay>(animator.gameObject);
                relay.Configure(melee);
                // Resolve no later than one rendered frame after the authored
                // 0.16s event if Unity suppresses a repeated same-state event.
                melee.ConfigureAnimationEventImpact(true, 0.17f);
                EditorUtility.SetDirty(animator.gameObject);
            }
        }

        private static IReadOnlyList<string> CollectFramePaths()
        {
            var paths = new List<string>();
            foreach (var motion in MotionNames)
            {
                var folder = $"{MotionRoot}/{motion}";
                paths.AddRange(AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)));
            }
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        private static void NormalizeFrame(string assetPath)
        {
            if (!TryCalculateFootPivot(assetPath, out var pivot, out _, out _))
                throw new InvalidOperationException($"Opaque Prome pixels were not found: {assetPath}");
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter ??
                           throw new InvalidOperationException($"TextureImporter missing: {assetPath}");
            // Establish Single mode first. Reading TextureImporterSettings before
            // this point can preserve the old Multiple mode and overwrite Single.
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            // Commit the format/mode settings before using SpriteDataProvider.
            // Otherwise the importer's pending serialized cache overwrites the
            // provider pivot during the following import.
            importer.SaveAndReimport();
            importer = AssetImporter.GetAtPath(assetPath) as TextureImporter ??
                       throw new InvalidOperationException($"TextureImporter disappeared: {assetPath}");
            var providerFactories = new SpriteDataProviderFactories();
            providerFactories.Init();
            var spriteProvider = providerFactories.GetSpriteEditorDataProviderFromObject(importer);
            spriteProvider.InitSpriteEditorDataProvider();
            var spriteRects = spriteProvider.GetSpriteRects();
            for (var i = 0; i < spriteRects.Length; i++)
            {
                spriteRects[i].alignment = SpriteAlignment.Custom;
                spriteRects[i].pivot = pivot;
            }
            spriteProvider.SetSpriteRects(spriteRects);
            spriteProvider.Apply();
            // Do not call importer.SaveAndReimport after the data provider: the
            // importer's stale serialized cache can overwrite the provider pivot.
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static bool TryCalculateFootPivot(
            string assetPath,
            out Vector2 pivot,
            out int width,
            out int height)
        {
            pivot = new Vector2(0.5f, 0f);
            width = height = 0;
            var fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath)) return false;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(fullPath), false)) return false;
                width = texture.width;
                height = texture.height;
                var pixels = texture.GetPixels32();
                var minX = width;
                var maxX = -1;
                var minY = height;
                var maxY = -1;
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a < 16) continue;
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
                if (maxY < minY) return false;

                var footBandTop = Mathf.Min(maxY, minY + Mathf.Max(4, Mathf.CeilToInt((maxY - minY + 1) * 0.06f)));
                var footMinX = width;
                var footMaxX = -1;
                for (var y = minY; y <= footBandTop; y++)
                for (var x = minX; x <= maxX; x++)
                {
                    if (pixels[y * width + x].a < 16) continue;
                    footMinX = Mathf.Min(footMinX, x);
                    footMaxX = Mathf.Max(footMaxX, x);
                }
                var footCenter = footMaxX >= footMinX ? (footMinX + footMaxX + 1f) * 0.5f : (minX + maxX + 1f) * 0.5f;
                pivot = new Vector2(Mathf.Clamp01(footCenter / width), Mathf.Clamp01(minY / (float)height));
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static PrometheusAiChange Issue(string action, string path, string expected) =>
            new PrometheusAiChange { action = action, hierarchyPath = path, after = expected };
    }
}
