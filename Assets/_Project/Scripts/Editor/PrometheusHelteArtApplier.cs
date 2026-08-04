using System;
using System.Linq;
using Narthex.Gameplay;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public static class PrometheusHelteArtApplier
    {
        private const string ControllerPath =
            "Assets/_Project/Art/AIConcepts/TutorialHelte/AnimationBatch_v1/UnityGenerated/HelteBoss.controller";
        private const string VisualName = "CharacterSprite_ART";

        [MenuItem(PrometheusToolMenuPaths.Root + "Art/Apply Helte Boss Art to Active Scene")]
        public static void ApplyToActiveSceneMenu()
        {
            ApplyToActiveScene(false);
        }

        [MenuItem(PrometheusToolMenuPaths.Root + "Art/Apply Helte Boss Art to All Scenes")]
        public static void ApplyToAllScenesMenu()
        {
            var targetScenePaths = new[]
            {
                "Assets/Scenes/BossDevelopmentScene.unity",
                "Assets/Scenes/TutorialScene.unity",
                "Assets/Scenes/AIReview/TutorialScene_ArtCandidate.unity",
                "Assets/Scenes/AIReview/TutorialScene_FPilot_Review.unity"
            };

            foreach (var path in targetScenePaths)
            {
                if (!System.IO.File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                ApplyToScene(scene, false);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[PrometheusHelteArtApplier] Saved Helte boss art updates to: {path}");
            }
        }

        [MenuItem(PrometheusToolMenuPaths.Root + "Art/Dry Run Helte Boss Art Application")]
        public static void DryRunMenu()
        {
            ApplyToActiveScene(true);
        }

        public static void ApplyToActiveScene(bool dryRun)
        {
            var activeScene = SceneManager.GetActiveScene();
            ApplyToScene(activeScene, dryRun);
        }

        public static void ApplyToScene(Scene scene, bool dryRun)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException($"Invalid or unloaded scene: {scene.name}");

            var helte = FindHelteInScene(scene);
            if (helte == null)
                throw new InvalidOperationException($"'TutorialHelte' object not found in scene: {scene.name}");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                throw new InvalidOperationException($"Helte controller missing at: {ControllerPath}");

            Debug.Log($"[PrometheusHelteArtApplier] {(dryRun ? "DRY RUN" : "APPLYING")} Helte art to target '{helte.name}' in scene '{scene.name}'.");
            if (dryRun) return;

            Undo.RegisterFullObjectHierarchyUndo(helte, "Apply Helte Boss Art");
            var visualBind = FindDescendant(helte.transform, "Visual_ART_BIND") ?? helte.transform;

            var visualTransform = FindDirectChild(visualBind, VisualName);
            if (visualTransform == null)
            {
                var visualGo = new GameObject(VisualName);
                Undo.RegisterCreatedObjectUndo(visualGo, "Create Helte Sprite Visual");
                visualGo.transform.SetParent(visualBind, false);
                visualTransform = visualGo.transform;
            }

            var spriteRenderer = visualTransform.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = Undo.AddComponent<SpriteRenderer>(visualTransform.gameObject);

            var animator = visualTransform.GetComponent<Animator>();
            if (animator == null)
                animator = Undo.AddComponent<Animator>(visualTransform.gameObject);

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            // Set default idle sprite from controller
            var firstState = controller.layers[0].stateMachine.states.FirstOrDefault(s => s.state.name.Equals("Idle", StringComparison.OrdinalIgnoreCase));
            if (firstState.state != null && firstState.state.motion is AnimationClip idleClip)
            {
                var sprite = GetFirstSprite(idleClip);
                if (sprite != null) spriteRenderer.sprite = sprite;
            }

            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 100;

            // Disable placeholder renderers on visualBind
            var placeholderRenderers = visualBind.GetComponentsInChildren<Renderer>(true)
                .Where(r => r != spriteRenderer && !r.transform.IsChildOf(visualTransform))
                .ToArray();

            foreach (var r in placeholderRenderers)
            {
                Undo.RecordObject(r, "Disable Helte Placeholder Renderer");
                r.enabled = false;
                EditorUtility.SetDirty(r);
            }

            // Configure CharacterPngAnimationBridge
            var bridge = helte.GetComponent<CharacterPngAnimationBridge>();
            if (bridge == null)
                bridge = Undo.AddComponent<CharacterPngAnimationBridge>(helte);

            var body = helte.GetComponent<Rigidbody2D>();
            var patternHost = helte.GetComponent<HelteBossPatternHost>();
            var combatActor = helte.GetComponent<CombatActorHost>();
            var enemyAttack = helte.GetComponent<EnemyAttackHost>();
            var motionHost = helte.GetComponent<CombatVisualMotionHost>();

            var player = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.CompareTag("Player"));

            bridge.Configure(
                CharacterPngAnimationPreset.Helte,
                animator,
                spriteRenderer,
                body,
                null,
                null,
                null,
                enemyAttack,
                combatActor,
                patternHost,
                motionHost,
                true,
                player,
                0.76f,
                0.76f,
                0.76f);

            if (motionHost != null)
            {
                Undo.RecordObject(motionHost, "Disable Procedural Visual Motion");
                motionHost.enabled = false;
                EditorUtility.SetDirty(motionHost);
            }

            // Fit visual bounds nicely
            var box = helte.GetComponent<BoxCollider2D>();
            if (box != null && spriteRenderer.sprite != null)
            {
                var spriteBounds = spriteRenderer.sprite.bounds;
                var heightScale = box.size.y / Mathf.Max(0.01f, spriteBounds.size.y);
                visualTransform.localScale = new Vector3(heightScale, heightScale, 1f);
                visualTransform.localPosition = new Vector3(
                    box.offset.x,
                    box.offset.y - box.size.y * 0.5f - spriteBounds.min.y * heightScale,
                    0f);
            }

            EditorUtility.SetDirty(helte);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[PrometheusHelteArtApplier] Successfully applied Helte 2D Animator and Visual hierarchy in '{scene.name}'.");
        }

        private static GameObject FindHelteInScene(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "TutorialHelte")?.gameObject;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDescendant(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindDirectChild(Transform root, string name)
        {
            for (var i = 0; i < root.childCount; i++)
                if (root.GetChild(i).name == name) return root.GetChild(i);
            return null;
        }

        private static Sprite GetFirstSprite(AnimationClip clip)
        {
            if (clip == null) return null;
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            if (bindings.Length == 0) return null;
            var curve = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            return curve != null && curve.Length > 0 ? curve[0].value as Sprite : null;
        }
    }
}
