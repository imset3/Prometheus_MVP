using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Gameplay;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Narthex.Tools
{
    public static class PrometheusTutorialWorldPolishAutomation
    {
        private const string ScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string LadderSpritePath =
            "Assets/_Project/Art/AIConcepts/TutorialHQTransition/ReviewBatch_v1/Generated/TUTO_HQ_LadderAssembly_v1.png";
        private const string WindMachineSpritePath =
            "Assets/_Project/Art/AIConcepts/TutorialHiddenRoomProps/Generated/TUTO_B_UpdraftDevice_v1.png";
        private const string WindSpritePath =
            "Assets/_Project/Art/AIConcepts/TutorialWindVFX/Generated/TUTO_VFX_UpdraftWind_Subtle_Alpha_v2.png";
        private const string PromePortraitSourcePath =
            "Assets/_Project/Art/Motions/Prome/Idle/프로메_IDLE_000.png";
        private const string ProjectileSpritePath =
            "Assets/_Project/Art/AIConcepts/TutorialPlayerVFX/ReviewBatch_v2/Generated/TUTO_VFX_TheusSupportProjectile_v1.png";
        private const string MeleeMarchSpritePath =
            "Assets/_Project/Art/AIConcepts/TutorialEnemies/ReviewBatch_v2/TutorialGuardPolished/Animations/Work/Frames/TutorialGuard_Work_00.png";
        private const string RangedMarchSpritePath =
            "Assets/_Project/Art/AIConcepts/TutorialEnemies/ReviewBatch_v2/TutorialRangedGuard/Animations/Work/Frames/TutorialRangedGuard_Work_00.png";
        private const string TeamGateSpritePath =
            "Assets/TileMap/SpriteSheet/ChatGPT Image 2026년 8월 3일 오후 09_54_21 (1).png";

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            if (!scene.IsValid() || scene.path != ScenePath)
                throw new InvalidOperationException("World polish is restricted to " + ScenePath);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying tutorial world polish.");

            var changes = Describe(scene);
            if (dryRun) return changes;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Polish tutorial F/G, ladder, wind, march and Theus support");
            try
            {
                PrometheusTilemapSceneIntegrator.RebuildPlatformTilemaps(
                    scene,
                    false,
                    new[] { "F스테이지", "G스테이지" });
                StyleEncounterGates(scene);
                ApplyCorridorLadder(scene);
                ApplyExteriorMarch(scene);
                ApplyWindPresentations(scene);
                ApplyHiddenRoomLighting(scene);
                CleanupTrainingRangedTargets(scene);
                ApplyTheusRangedSupport(scene);

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

        public static List<PrometheusAiChange> ApplyTheusProjectile(Scene scene, bool dryRun)
        {
            if (!scene.IsValid() || scene.path != ScenePath)
                throw new InvalidOperationException("Theus projectile art is restricted to " + ScenePath);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying Theus projectile art.");

            var guide = Require(scene, "TutorialGuideCompanion");
            var changes = new List<PrometheusAiChange>
            {
                Change("apply-theus-projectile-art", guide, "existing projectile sprite", ProjectileSpritePath)
            };
            if (dryRun) return changes;

            var host = guide.GetComponent<TutorialTheusRangedSupportHost>();
            if (host == null) throw new InvalidOperationException("TutorialTheusRangedSupportHost missing.");
            var sprite = LoadSprite(ProjectileSpritePath);
            var serialized = new SerializedObject(host);
            var renderers = serialized.FindProperty("projectileRenderers");
            if (renderers == null || !renderers.isArray || renderers.arraySize < 3)
                throw new InvalidOperationException("Theus projectile renderer pool is incomplete.");

            for (var index = 0; index < renderers.arraySize; index++)
            {
                var renderer = renderers.GetArrayElementAtIndex(index).objectReferenceValue as SpriteRenderer;
                if (renderer == null) throw new InvalidOperationException("Theus projectile renderer is missing.");
                Undo.RecordObjects(new UnityEngine.Object[] { renderer, renderer.transform }, "Apply Theus projectile art");
                renderer.sprite = sprite;
                renderer.color = Color.white;
                renderer.sortingOrder = 95;
                var scale = 0.75f / Mathf.Max(0.01f, sprite.bounds.size.x);
                renderer.transform.localScale = new Vector3(scale, scale, 1f);
                EditorUtility.SetDirty(renderer);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return changes;
        }

        public static List<PrometheusAiChange> ApplyExteriorMarchOnly(Scene scene, bool dryRun)
        {
            if (!scene.IsValid() || scene.path != ScenePath)
                throw new InvalidOperationException("Exterior march art is restricted to " + ScenePath);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying exterior march art.");

            var exterior = Require(scene, "외부");
            var changes = new List<PrometheusAiChange>
            {
                Change("reframe-exterior-enemy-march", exterior,
                    "full-body formation marching right", "lower-edge upper-body formation marching left")
            };
            if (dryRun) return changes;

            ApplyExteriorMarch(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            return changes;
        }

        public static List<PrometheusAiChange> ApplyWindAndDialogueArt(Scene scene, bool dryRun)
        {
            if (!scene.IsValid() || scene.path != ScenePath)
                throw new InvalidOperationException("Wind and dialogue art is restricted to " + ScenePath);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying wind and dialogue art.");

            var windHosts = SceneObjects<TutorialWindHazardHost>(scene);
            var dialogue = SceneObjects<DialogueViewModule>(scene).FirstOrDefault();
            if (dialogue == null) throw new InvalidOperationException("DialogueViewModule missing.");
            var changes = new List<PrometheusAiChange>
            {
                Change("replace-updraft-bars-with-subtle-sprite", windHosts.First().gameObject,
                    $"{windHosts.Length} marker presentations", WindSpritePath),
                Change("hide-hidden-room-legacy-wind-bars", Require(scene, "HiddenRoom_Updraft_MARKER"),
                    "legacy WindStrip renderers", "disabled; marker gameplay preserved"),
                Change("connect-prome-dialogue-portrait", dialogue.gameObject,
                    "missing speaker portrait entry", PromePortraitSourcePath)
            };
            if (dryRun) return changes;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply subtle tutorial wind and Prome dialogue portrait");
            try
            {
                DisableHiddenRoomLegacyWindBars(scene);
                ApplyWindPresentations(scene);
                ApplyPromeDialoguePortrait(scene, dialogue);
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

        private static List<PrometheusAiChange> Describe(Scene scene)
        {
            var changes = new List<PrometheusAiChange>
            {
                Change("rebuild-static-platform-art", Require(scene, "F스테이지"), "mixed blockout", "team tiles, stateful gates excluded"),
                Change("rebuild-static-platform-art", Require(scene, "G스테이지"), "mixed blockout", "team tiles, stateful gates excluded"),
                Change("apply-ladder-sprite", Require(scene, "C03_LadderPresentation"), "shape ladder", LadderSpritePath),
                Change("apply-exterior-enemy-march", Require(scene, "외부"), "missing", "10-unit presentation formation"),
                Change("apply-wind-presentations", Require(scene, "HiddenRoom_Updraft_MARKER"), "static/incomplete", "marker-relative machine and rising streaks"),
                Change("apply-hidden-room-lighting", Require(scene, "숨겨진방"), "uniform brightness", "dark until passkey"),
                Change("apply-theus-ranged-support", Require(scene, "TutorialGuideCompanion"), "missing", "pooled auto ranged support")
            };
            var phaseRoot = Find(scene, "05_원거리공격");
            if (phaseRoot != null)
            {
                var count = phaseRoot.GetComponentsInChildren<CombatActorHost>(true)
                    .Count(item => item.name.StartsWith("RangedTarget_", StringComparison.Ordinal));
                if (count != 3)
                    changes.Add(Change("deduplicate-training-ranged-targets", phaseRoot, count.ToString(), "3"));
            }
            return changes;
        }

        private static void StyleEncounterGates(Scene scene)
        {
            var sprite = LoadSprite(TeamGateSpritePath);
            var bindings = SceneObjects<TutorialGateVisualBindingHost>(scene);
            foreach (var binding in bindings)
            {
                var serialized = new SerializedObject(binding);
                var renderer = serialized.FindProperty("boundRenderer")?.objectReferenceValue as SpriteRenderer;
                if (renderer == null) continue;
                var worldBounds = renderer.bounds;
                Undo.RecordObjects(new UnityEngine.Object[] { renderer, renderer.transform }, "Style stateful team-tile gate");
                renderer.sprite = sprite;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.size = worldBounds.size;
                renderer.transform.localScale = Vector3.one;
                renderer.sortingOrder = 2;
                renderer.enabled = true;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static void ApplyCorridorLadder(Scene scene)
        {
            var presentation = Require(scene, "C03_LadderPresentation").transform;
            foreach (var renderer in presentation.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.transform == presentation.Find("LadderAssembly_ART")) continue;
                Undo.RecordObject(renderer, "Hide shape ladder visual");
                renderer.enabled = false;
            }

            var visual = GetOrCreateChild(presentation, "LadderAssembly_ART");
            var spriteRenderer = GetOrAdd<SpriteRenderer>(visual.gameObject);
            var sprite = LoadSprite(LadderSpritePath);
            Undo.RecordObjects(new UnityEngine.Object[] { visual, spriteRenderer }, "Apply corridor ladder sprite");
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 8;
            spriteRenderer.enabled = true;
            visual.localPosition = new Vector3(0f, 0.15f, -0.04f);
            visual.localRotation = Quaternion.identity;
            var scale = 7.2f / Mathf.Max(0.01f, sprite.bounds.size.y);
            visual.localScale = new Vector3(scale, scale, 1f);
            EditorUtility.SetDirty(spriteRenderer);
        }

        private static void ApplyExteriorMarch(Scene scene)
        {
            var exterior = Require(scene, "외부").transform;
            var old = exterior.Find("외부_적진격연출");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = CreateChild(exterior, "외부_적진격연출");
            // Prome watches from the upper route. Sink the formation behind the lower
            // screen edge so only torsos and heads rise into view.
            root.localPosition = new Vector3(0f, -7.35f, -0.12f);
            var melee = LoadSprite(MeleeMarchSpritePath);
            var ranged = LoadSprite(RangedMarchSpritePath);
            var soldiers = new Transform[10];
            for (var index = 0; index < soldiers.Length; index++)
            {
                var soldier = CreateChild(root, $"행군병_{index + 1:00}_{(index % 3 == 2 ? "원거리" : "근접")}");
                soldiers[index] = soldier;
                soldier.localPosition = new Vector3(-15f + index * 3.1f, index % 2 == 0 ? 0f : -0.45f, index % 2 * 0.03f);
                var renderer = Undo.AddComponent<SpriteRenderer>(soldier.gameObject);
                renderer.sprite = index % 3 == 2 ? ranged : melee;
                // Keep the distant formation behind gameplay actors, but in front of
                // the rebuilt floor tiles. A negative order made the floor cut each
                // soldier through the torso even though the formation was positioned
                // correctly below the playable lane.
                renderer.sortingOrder = 3 + index % 2;
                renderer.flipX = true;
                renderer.color = index % 2 == 0 ? Color.white : new Color(0.72f, 0.78f, 0.82f, 0.92f);
                // Keep this as a distant invasion read, not a foreground combat
                // formation. The source work frames have generous transparent padding,
                // so size by renderer bounds and stay close to Prome's visual scale.
                var targetHeight = index % 2 == 0 ? 2.25f : 2.05f;
                var scale = targetHeight / Mathf.Max(0.01f, renderer.sprite.bounds.size.y);
                soldier.localScale = new Vector3(scale, scale, 1f);
            }
            var host = Undo.AddComponent<TutorialExteriorEnemyMarchHost>(root.gameObject);
            var serialized = new SerializedObject(host);
            AssignArray(serialized.FindProperty("soldiers"), soldiers.Cast<UnityEngine.Object>().ToArray());
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyWindPresentations(Scene scene)
        {
            var machine = LoadSprite(WindMachineSpritePath);
            var streakSprite = LoadSprite(WindSpritePath);
            foreach (var wind in SceneObjects<TutorialWindHazardHost>(scene))
            {
                var collider = wind.GetComponent<BoxCollider2D>();
                if (collider == null) continue;
                var old = wind.transform.Find("WindPresentation_ART");
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                var root = CreateChild(wind.transform, "WindPresentation_ART");
                root.localPosition = collider.offset;

                var machineVisual = CreateChild(root, "WindMachine_ART");
                var machineRenderer = Undo.AddComponent<SpriteRenderer>(machineVisual.gameObject);
                machineRenderer.sprite = machine;
                machineRenderer.sortingOrder = 6;
                machineVisual.localPosition = new Vector3(0f, -collider.size.y * 0.5f + 0.15f, -0.04f);
                var machineHeight = Mathf.Clamp(collider.size.x * 0.48f, 1.15f, 2.2f);
                var machineScale = machineHeight / Mathf.Max(0.01f, machine.bounds.size.y);
                machineVisual.localScale = new Vector3(machineScale, machineScale, 1f);

                const int streakCount = 5;
                var streaks = new Transform[streakCount];
                var renderers = new SpriteRenderer[streakCount];
                for (var index = 0; index < streakCount; index++)
                {
                    var streak = CreateChild(root, $"WindStreak_{index + 1:00}");
                    streaks[index] = streak;
                    var x = Mathf.Lerp(-collider.size.x * 0.34f, collider.size.x * 0.34f, index / (streakCount - 1f));
                    streak.localPosition = new Vector3(x, -collider.size.y * 0.5f, -0.06f);
                    var targetHeight = Mathf.Clamp(collider.size.x * (0.34f + index % 3 * 0.05f), 1.35f, 2.5f);
                    var scale = targetHeight / Mathf.Max(0.01f, streakSprite.bounds.size.y);
                    streak.localScale = new Vector3(scale * (index % 2 == 0 ? 0.92f : 1f), scale, 1f);
                    streak.localRotation = Quaternion.Euler(0f, 0f, -4f + index * 2f);
                    var renderer = Undo.AddComponent<SpriteRenderer>(streak.gameObject);
                    renderers[index] = renderer;
                    renderer.sprite = streakSprite;
                    renderer.color = new Color(0.82f, 0.91f, 0.94f, 0.18f);
                    renderer.sortingOrder = 5;
                }

                var host = Undo.AddComponent<TutorialUpdraftVisualHost>(root.gameObject);
                var serialized = new SerializedObject(host);
                AssignArray(serialized.FindProperty("streaks"), streaks.Cast<UnityEngine.Object>().ToArray());
                AssignArray(serialized.FindProperty("streakRenderers"), renderers.Cast<UnityEngine.Object>().ToArray());
                serialized.FindProperty("bottomY").floatValue = -collider.size.y * 0.5f + 0.7f;
                serialized.FindProperty("topY").floatValue = collider.size.y * 0.5f - 0.4f;
                serialized.FindProperty("riseSpeed").floatValue = Mathf.Clamp(collider.size.y * 0.3f, 3.5f, 8f);
                serialized.FindProperty("swayAmount").floatValue = Mathf.Clamp(collider.size.x * 0.025f, 0.08f, 0.2f);
                serialized.FindProperty("peakAlpha").floatValue = 0.2f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void DisableHiddenRoomLegacyWindBars(Scene scene)
        {
            var roots = new List<Transform>();
            var hiddenMarker = Require(scene, "HiddenRoom_Updraft_MARKER").transform;
            var markerLegacy = hiddenMarker.Find("Updraft_ART_SLOT");
            if (markerLegacy != null) roots.Add(markerLegacy);
            var integrationLegacy = Find(scene, "B_Updraft_ART_SLOT");
            if (integrationLegacy != null) roots.Add(integrationLegacy.transform);

            foreach (var root in roots)
            foreach (var child in root.GetComponentsInChildren<Transform>(true)
                         .Where(item => item != root && item.name.StartsWith("WindStrip_", StringComparison.Ordinal)))
            {
                Undo.RecordObject(child.gameObject, "Disable legacy hidden-room wind bar");
                child.gameObject.SetActive(false);
                EditorUtility.SetDirty(child.gameObject);
            }
        }

        private static void ApplyPromeDialoguePortrait(Scene scene, DialogueViewModule dialogue)
        {
            var portraitSprite = LoadSprite(PromePortraitSourcePath);
            var dialogueSerialized = new SerializedObject(dialogue);
            var currentImage = dialogueSerialized.FindProperty("leftPortraitImage").objectReferenceValue as UnityEngine.UI.Image;
            if (currentImage == null) throw new InvalidOperationException("Left dialogue portrait slot is missing.");

            var viewport = currentImage.transform;
            GetOrAdd<UnityEngine.UI.RectMask2D>(viewport.gameObject);
            var portraitTransform = GetOrCreateRectChild(viewport, "PromePortraitFace_ART");
            var portraitImage = GetOrAdd<UnityEngine.UI.Image>(portraitTransform.gameObject);
            Undo.RecordObjects(new UnityEngine.Object[] { portraitTransform, portraitImage }, "Apply Prome dialogue portrait");
            portraitTransform.anchorMin = new Vector2(0.5f, 0.5f);
            portraitTransform.anchorMax = new Vector2(0.5f, 0.5f);
            portraitTransform.pivot = new Vector2(0.5f, 0.5f);
            portraitTransform.sizeDelta = new Vector2(260f, 260f);
            portraitTransform.anchoredPosition = new Vector2(0f, -43f);
            portraitTransform.localScale = Vector3.one;
            portraitImage.sprite = portraitSprite;
            portraitImage.color = Color.white;
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            dialogueSerialized.Update();
            dialogueSerialized.FindProperty("leftPortraitImage").objectReferenceValue = portraitImage;
            var portraits = dialogueSerialized.FindProperty("speakerPortraits");
            var promeIndex = -1;
            for (var index = 0; index < portraits.arraySize; index++)
            {
                if (portraits.GetArrayElementAtIndex(index).FindPropertyRelative("speakerName").stringValue == "프로메")
                {
                    promeIndex = index;
                    break;
                }
            }
            if (promeIndex < 0)
            {
                promeIndex = portraits.arraySize;
                portraits.arraySize++;
            }
            var entry = portraits.GetArrayElementAtIndex(promeIndex);
            entry.FindPropertyRelative("speakerName").stringValue = "프로메";
            entry.FindPropertyRelative("portrait").objectReferenceValue = portraitSprite;
            dialogueSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(dialogue);
        }

        private static void ApplyHiddenRoomLighting(Scene scene)
        {
            var hidden = Require(scene, "숨겨진방").transform;
            var host = hidden.GetComponent<TutorialHiddenRoomLightingHost>();
            if (host == null) host = Undo.AddComponent<TutorialHiddenRoomLightingHost>(hidden.gameObject);
            var sprites = hidden.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => !IsPasskeyRenderer(renderer.transform))
                .ToArray();
            var tilemaps = hidden.GetComponentsInChildren<Tilemap>(true);
            var serialized = new SerializedObject(host);
            AssignArray(serialized.FindProperty("spriteRenderers"), sprites.Cast<UnityEngine.Object>().ToArray());
            AssignArray(serialized.FindProperty("tilemaps"), tilemaps.Cast<UnityEngine.Object>().ToArray());
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var flow = SceneObjects<TutorialChapter0IntroFlowHost>(scene).FirstOrDefault();
            if (flow == null) throw new InvalidOperationException("TutorialChapter0IntroFlowHost missing.");
            var flowSerialized = new SerializedObject(flow);
            flowSerialized.FindProperty("hiddenRoomLighting").objectReferenceValue = host;
            flowSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool IsPasskeyRenderer(Transform transform)
        {
            while (transform != null)
            {
                if (transform.name.Contains("Passkey", StringComparison.OrdinalIgnoreCase) || transform.name.Contains("패스키"))
                    return true;
                transform = transform.parent;
            }
            return false;
        }

        private static void CleanupTrainingRangedTargets(Scene scene)
        {
            var root = Require(scene, "05_원거리공격").transform;
            var desiredX = new Dictionary<string, float>
            {
                ["RangedTarget_01"] = 197f,
                ["RangedTarget_02"] = 200f,
                ["RangedTarget_03"] = 203f
            };
            var targets = root.GetComponentsInChildren<CombatActorHost>(true)
                .Where(item => desiredX.ContainsKey(item.name))
                .ToArray();
            foreach (var pair in desiredX)
            {
                var matches = targets.Where(item => item != null && item.name == pair.Key)
                    .OrderBy(item => Mathf.Abs(item.transform.position.x - pair.Value))
                    .ToArray();
                for (var index = 1; index < matches.Length; index++)
                    Undo.DestroyObjectImmediate(matches[index].gameObject);
            }
        }

        private static void ApplyTheusRangedSupport(Scene scene)
        {
            var guide = Require(scene, "TutorialGuideCompanion");
            var oldRoot = guide.transform.Find("TheusRangedSupport_ART");
            if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot.gameObject);
            var root = CreateChild(guide.transform, "TheusRangedSupport_ART");
            var sprite = LoadSprite(ProjectileSpritePath);
            var pool = new GameObject[3];
            var renderers = new SpriteRenderer[3];
            for (var index = 0; index < pool.Length; index++)
            {
                var projectile = CreateChild(root, $"TheusProjectile_{index + 1:00}");
                pool[index] = projectile.gameObject;
                var renderer = Undo.AddComponent<SpriteRenderer>(projectile.gameObject);
                renderers[index] = renderer;
                renderer.sprite = sprite;
                // The generated asset already carries Theus' cyan body, gold trim and
                // red jewel accents. Preserve those authored colors without tinting.
                renderer.color = Color.white;
                renderer.sortingOrder = 95;
                var scale = 0.75f / Mathf.Max(0.01f, sprite.bounds.size.x);
                projectile.localScale = new Vector3(scale, scale, 1f);
                projectile.gameObject.SetActive(false);
            }

            var host = guide.GetComponent<TutorialTheusRangedSupportHost>();
            if (host == null) host = Undo.AddComponent<TutorialTheusRangedSupportHost>(guide);
            var player = SceneObjects<CombatActorHost>(scene).FirstOrDefault(item => item.Kind == CombatActorKind.Player);
            if (player == null) throw new InvalidOperationException("Player CombatActorHost missing.");
            var serialized = new SerializedObject(host);
            serialized.FindProperty("playerSourceActor").objectReferenceValue = player;
            serialized.FindProperty("lightFormHost").objectReferenceValue = guide.GetComponent<TutorialTheusLightFormHost>();
            AssignArray(serialized.FindProperty("projectilePool"), pool.Cast<UnityEngine.Object>().ToArray());
            AssignArray(serialized.FindProperty("projectileRenderers"), renderers.Cast<UnityEngine.Object>().ToArray());
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Sprite asset missing: " + path);
            return sprite;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var existing = target.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(target);
        }

        private static Transform GetOrCreateChild(Transform parent, string name) =>
            parent.Find(name) ?? CreateChild(parent, name);

        private static RectTransform GetOrCreateRectChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                var existingRect = existing as RectTransform;
                if (existingRect == null)
                    throw new InvalidOperationException(name + " exists without a RectTransform.");
                return existingRect;
            }

            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void AssignArray(SerializedProperty property, UnityEngine.Object[] values)
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static T[] SceneObjects<T>(Scene scene) where T : UnityEngine.Object =>
            Resources.FindObjectsOfTypeAll<T>().Where(item =>
            {
                if (item is Component component) return component.gameObject.scene == scene;
                if (item is GameObject gameObject) return gameObject.scene == scene;
                return false;
            }).ToArray();

        private static GameObject Require(Scene scene, string name) =>
            Find(scene, name) ?? throw new InvalidOperationException("Scene object missing: " + name);

        private static GameObject Find(Scene scene, string name) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(item => item.name == name)?.gameObject;

        private static PrometheusAiChange Change(string action, GameObject target, string before, string after) => new()
        {
            action = action,
            objectId = PrometheusSceneQuery.ObjectId(target),
            hierarchyPath = PrometheusSceneQuery.Path(target),
            before = before,
            after = after
        };
    }
}
