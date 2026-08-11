#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Narthex.Gameplay;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.EditorTools
{
    public static class PrometheusEnemyArchetypeArtBuilder
    {
        private const string RequiredScene = "Assets/Scenes/TutorialScene.unity";
        private const string ProjectileSpritePath =
            "Assets/_Project/Art/AIConcepts/TutorialPlayerVFX/ReviewBatch_v1/Generated/TUTO_VFX_RangedProjectile_v1.png";

        private static readonly string[] EnemyPaths =
        {
            "/GameplayIntegrationRoot/F_Encounter01_Integration/F01_EnemySlots/ExteriorA_Enemy_01_ART_SLOT",
            "/GameplayIntegrationRoot/F_Encounter01_Integration/F01_EnemySlots/ExteriorA_Enemy_02_ART_SLOT",
            "/GameplayIntegrationRoot/F_Encounter01_Integration/F01_EnemySlots/ExteriorA_Enemy_03_ART_SLOT",
            "/GameplayIntegrationRoot/G_Encounter02_Integration/G01_EnemySlots/ExteriorB_Enemy_01_ART_SLOT",
            "/GameplayIntegrationRoot/G_Encounter02_Integration/G01_EnemySlots/ExteriorB_Enemy_02_ART_SLOT",
            "/GameplayIntegrationRoot/G_Encounter02_Integration/G01_EnemySlots/ExteriorB_Enemy_03_ART_SLOT",
            "/GameplayIntegrationRoot/G_Encounter02_Integration/G01_EnemySlots/ExteriorB_Enemy_04_ART_SLOT"
        };

        private static readonly HashSet<string> RangedPaths = new HashSet<string>
        {
            EnemyPaths[2],
            EnemyPaths[4],
            EnemyPaths[6]
        };

        private const string MeleeFramesRoot =
            "Assets/_Project/Art/AIConcepts/TutorialEnemies/ReviewBatch_v2/TutorialGuardPolished/Animations";
        private const string MeleeGeneratedRoot =
            "Assets/_Project/Art/AIConcepts/TutorialEnemies/ReviewBatch_v2/TutorialGuardPolished/UnityGenerated";
        private const string RangedFramesRoot =
            "Assets/_Project/Art/AIConcepts/TutorialEnemies/ReviewBatch_v2/TutorialRangedGuard/Animations";
        private const string RangedGeneratedRoot =
            "Assets/_Project/Art/AIConcepts/TutorialEnemies/ReviewBatch_v2/TutorialRangedGuard/UnityGenerated";

        [MenuItem("sragon000/AI Toolkit/Enemy Archetypes/Dry Run")]
        public static void DryRun()
        {
            var issues = ValidateSceneAndAssets();
            if (issues.Count > 0)
            {
                Debug.LogError("[Enemy Archetypes] Dry run failed:\n- " + string.Join("\n- ", issues));
                return;
            }

            Debug.Log(
                "[Enemy Archetypes] Dry run passed. 4 melee slots will receive polished animation; " +
                "F03, G02, and G04 will become ranged guards. Existing spawn-marker transforms remain editable and unchanged.");
        }

        [MenuItem("sragon000/AI Toolkit/Enemy Archetypes/Apply")]
        public static void Apply()
        {
            var issues = ValidateSceneAndAssets();
            if (issues.Count > 0)
            {
                Debug.LogError("[Enemy Archetypes] Apply aborted:\n- " + string.Join("\n- ", issues));
                return;
            }

            ConfigureFrameImports(MeleeFramesRoot, "TutorialGuard");
            ConfigureFrameImports(RangedFramesRoot, "TutorialRangedGuard");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var meleeController = BuildController(MeleeFramesRoot, MeleeGeneratedRoot, "TutorialGuardPolished", false);
            var rangedController = BuildController(RangedFramesRoot, RangedGeneratedRoot, "TutorialRangedGuard", true);
            var target = UnityEngine.Object.FindObjectsByType<CombatActorHost>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .First(actor => actor.Kind == CombatActorKind.Player)
                .transform;

            foreach (var path in EnemyPaths)
            {
                var enemy = FindByPath(path);
                if (RangedPaths.Contains(path))
                    ConfigureRanged(enemy, target, rangedController);
                else
                    ConfigureMelee(enemy, meleeController);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = FindByPath(EnemyPaths[2]);
            Debug.Log(
                "[Enemy Archetypes] Applied. Editable spawn positions remain under " +
                "F01_EnemySpawns and G01_EnemySpawns; ranged muzzle anchors are named RangedMuzzle_EDITABLE.");
        }

        private static List<string> ValidateSceneAndAssets()
        {
            var issues = new List<string>();
            var scene = SceneManager.GetActiveScene();
            if (scene.path != RequiredScene)
                issues.Add($"Active scene must be {RequiredScene}, but is {scene.path}.");

            foreach (var path in EnemyPaths)
                if (FindByPath(path) == null)
                    issues.Add("Missing enemy hierarchy path: " + path);

            foreach (var tuple in RequiredFrameSets())
                for (var index = 0; index < 8; index++)
                {
                    var path = FramePath(tuple.root, tuple.actor, tuple.motion, index);
                    if (!File.Exists(path)) issues.Add("Missing frame: " + path);
                }

            if (UnityEngine.Object.FindObjectsByType<CombatActorHost>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .All(actor => actor.Kind != CombatActorKind.Player))
                issues.Add("No Player CombatActorHost found.");
            if (!File.Exists(ProjectileSpritePath)) issues.Add("Missing projectile sprite: " + ProjectileSpritePath);
            return issues;
        }

        private static IEnumerable<(string root, string actor, string motion)> RequiredFrameSets()
        {
            foreach (var motion in new[] { "Work", "Attack", "Die" })
            {
                yield return (MeleeFramesRoot, "TutorialGuard", motion);
                yield return (RangedFramesRoot, "TutorialRangedGuard", motion);
            }
        }

        private static void ConfigureFrameImports(string root, string actor)
        {
            foreach (var motion in new[] { "Work", "Attack", "Die" })
                for (var index = 0; index < 8; index++)
                    ConfigureSpriteImport(FramePath(root, actor, motion, index), true);
        }

        private static void ConfigureSpriteImport(string path, bool bottomPivot)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 256f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (bottomPivot)
            {
                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
                textureSettings.spritePivot = new Vector2(0.5f, 0f);
                importer.SetTextureSettings(textureSettings);
            }
            importer.SaveAndReimport();
        }

        private static AnimatorController BuildController(
            string frameRoot,
            string generatedRoot,
            string assetStem,
            bool ranged)
        {
            EnsureAssetFolder(generatedRoot);
            var work = BuildClip(frameRoot, generatedRoot, assetStem, "Work", true,
                new[] { 0f, .14f, .30f, .47f, .64f, .80f, .97f, 1.13f, 1.28f });
            var attack = BuildClip(frameRoot, generatedRoot, assetStem, "Attack", false,
                ranged
                    ? new[] { 0f, .10f, .22f, .36f, .50f, .62f, .75f, .90f }
                    : new[] { 0f, .08f, .17f, .28f, .38f, .48f, .60f, .72f });
            var death = BuildClip(frameRoot, generatedRoot, assetStem, "Death", false,
                new[] { 0f, .10f, .22f, .36f, .54f, .72f, .92f, 1.08f, 1.38f });

            var controllerPath = $"{generatedRoot}/{assetStem}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);
            var workState = machine.AddState("Work", new Vector3(220f, 20f));
            workState.motion = work;
            machine.defaultState = workState;
            var attackState = machine.AddState("Attack", new Vector3(440f, -50f));
            attackState.motion = attack;
            var deathState = machine.AddState("Death", new Vector3(440f, 90f));
            deathState.motion = death;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip BuildClip(
            string frameRoot,
            string generatedRoot,
            string assetStem,
            string stateName,
            bool loop,
            float[] times)
        {
            var motion = stateName == "Death" ? "Die" : stateName;
            var path = $"{generatedRoot}/{assetStem}_{stateName}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { frameRate = 60f, name = assetStem + "_" + stateName };
                AssetDatabase.CreateAsset(clip, path);
            }

            var sprites = Enumerable.Range(0, 8)
                .Select(index => AssetDatabase.LoadAssetAtPath<Sprite>(FramePath(frameRoot,
                    assetStem.StartsWith("TutorialGuardPolished", StringComparison.Ordinal)
                        ? "TutorialGuard"
                        : "TutorialRangedGuard",
                    motion,
                    index)))
                .ToArray();
            var keys = new List<ObjectReferenceKeyframe>();
            for (var index = 0; index < sprites.Length; index++)
                keys.Add(new ObjectReferenceKeyframe { time = times[index], value = sprites[index] });
            if (times.Length > sprites.Length)
                keys.Add(new ObjectReferenceKeyframe
                {
                    time = times[^1],
                    value = loop ? sprites[0] : sprites[^1]
                });

            AnimationUtility.SetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"),
                keys.ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void ConfigureMelee(GameObject enemy, RuntimeAnimatorController controller)
        {
            Undo.RecordObject(enemy, "Polish melee enemy animation");
            var ranged = enemy.GetComponent<TutorialRangedEnemyHost>();
            if (ranged != null) ranged.enabled = false;
            var rangedBridge = enemy.GetComponent<TutorialRangedEnemyAnimationBridge>();
            if (rangedBridge != null) rangedBridge.enabled = false;
            var meleeAttack = enemy.GetComponent<EnemyAttackHost>();
            if (meleeAttack != null)
            {
                Undo.RecordObject(meleeAttack, "Calm melee attack cadence");
                meleeAttack.enabled = true;
                SetSerializedFloat(meleeAttack, "telegraphSeconds", .38f);
                SetSerializedFloat(meleeAttack, "intervalSeconds", 1.35f);
            }
            var pursuit = enemy.GetComponent<TutorialEnemyPursuitHost>();
            if (pursuit != null) pursuit.enabled = true;

            var bridge = enemy.GetComponent<CharacterPngAnimationBridge>();
            if (bridge != null)
            {
                Undo.RecordObject(bridge, "Polish melee animation timing");
                bridge.enabled = true;
                var serialized = new SerializedObject(bridge);
                serialized.FindProperty("attackOneDuration").floatValue = .72f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var animator = enemy.GetComponentInChildren<Animator>(true);
            var renderer = animator != null ? animator.GetComponent<SpriteRenderer>() : null;
            if (animator == null || renderer == null) return;
            Undo.RecordObjects(new UnityEngine.Object[] { animator, renderer }, "Assign polished melee art");
            animator.runtimeAnimatorController = controller;
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                FramePath(MeleeFramesRoot, "TutorialGuard", "Work", 0));
            animator.Rebind();
            ConfigureTempo(enemy, animator);
        }

        private static void ConfigureRanged(GameObject enemy, Transform target, RuntimeAnimatorController controller)
        {
            var actor = enemy.GetComponent<CombatActorHost>();
            var body = enemy.GetComponent<Collider2D>();
            var meleeAttack = enemy.GetComponent<EnemyAttackHost>();
            var pursuit = enemy.GetComponent<TutorialEnemyPursuitHost>();
            var meleeBridge = enemy.GetComponent<CharacterPngAnimationBridge>();
            if (meleeAttack != null) { Undo.RecordObject(meleeAttack, "Disable melee attack"); meleeAttack.enabled = false; }
            if (pursuit != null) { Undo.RecordObject(pursuit, "Disable melee pursuit"); pursuit.enabled = false; }
            if (meleeBridge != null) { Undo.RecordObject(meleeBridge, "Disable melee presentation"); meleeBridge.enabled = false; }

            var visualRoot = FindOrCreateChild(enemy.transform, "Visual_ART_BIND");
            foreach (var renderer in visualRoot.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.gameObject.name == "RangedCharacterSprite_ART_EDITABLE") continue;
                Undo.RecordObject(renderer, "Hide previous enemy sprite");
                renderer.enabled = false;
            }

            var visual = FindOrCreateChild(visualRoot, "RangedCharacterSprite_ART_EDITABLE");
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            var spriteRenderer = GetOrAdd<SpriteRenderer>(visual.gameObject);
            spriteRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                FramePath(RangedFramesRoot, "TutorialRangedGuard", "Work", 0));
            spriteRenderer.sortingLayerName = "Default";
            spriteRenderer.sortingOrder = 120;
            var animator = GetOrAdd<Animator>(visual.gameObject);
            animator.runtimeAnimatorController = controller;

            var muzzle = FindOrCreateChild(enemy.transform, "RangedMuzzle_EDITABLE");
            muzzle.localPosition = new Vector3(0.9f, 0.8f, 0f);
            var warning = FindDescendant(enemy.transform, "AttackWarning_ART_SLOT");
            if (warning == null) warning = FindOrCreateChild(enemy.transform, "RangedWarning_ART_SLOT");
            var warningRenderer = warning.GetComponentInChildren<Renderer>(true);
            var pool = BuildProjectilePool(enemy.transform);

            var ranged = GetOrAdd<TutorialRangedEnemyHost>(enemy);
            Undo.RecordObject(ranged, "Configure ranged enemy");
            ranged.Configure(actor, target, body, muzzle, warning.gameObject, warningRenderer, pool);
            SetSerializedFloat(ranged, "telegraphSeconds", .62f);
            SetSerializedFloat(ranged, "recoverySeconds", 1.55f);
            ranged.enabled = true;

            var bridge = GetOrAdd<TutorialRangedEnemyAnimationBridge>(enemy);
            Undo.RecordObject(bridge, "Configure ranged animation");
            bridge.Configure(animator, spriteRenderer, ranged, actor, target);
            bridge.enabled = true;
            animator.Rebind();
            ConfigureTempo(enemy, animator);

            var contract = enemy.GetComponent<ArtReplacementContractHost>();
            if (contract != null)
            {
                Undo.RecordObject(contract, "Update ranged art contract");
                var serialized = new SerializedObject(contract);
                serialized.FindProperty("visualRoot").objectReferenceValue = visualRoot;
                var renderers = serialized.FindProperty("renderers");
                renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = spriteRenderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static TutorialEnemyProjectileHost[] BuildProjectilePool(Transform enemy)
        {
            var root = FindOrCreateChild(enemy, "RangedProjectilePool");
            root.localPosition = Vector3.zero;
            var sprite = AssetDatabase.LoadAllAssetsAtPath(ProjectileSpritePath)
                .OfType<Sprite>()
                .FirstOrDefault();
            if (sprite == null)
                throw new InvalidOperationException("Ranged projectile sprite sub-asset is missing: " + ProjectileSpritePath);
            var result = new TutorialEnemyProjectileHost[3];
            for (var index = 0; index < result.Length; index++)
            {
                var projectile = FindOrCreateChild(root, $"RangedProjectile_{index + 1:00}");
                projectile.localPosition = Vector3.zero;
                projectile.localScale = Vector3.one * .45f;
                var renderer = GetOrAdd<SpriteRenderer>(projectile.gameObject);
                renderer.sprite = sprite;
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
                renderer.color = Color.white;
                renderer.enabled = true;
                renderer.sortingOrder = 180;
                var body = GetOrAdd<Rigidbody2D>(projectile.gameObject);
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.freezeRotation = true;
                var collider = GetOrAdd<CircleCollider2D>(projectile.gameObject);
                collider.isTrigger = true;
                collider.radius = .22f;
                var host = GetOrAdd<TutorialEnemyProjectileHost>(projectile.gameObject);
                host.Configure(collider, body, renderer);
                projectile.gameObject.SetActive(false);
                result[index] = host;
            }
            return result;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(gameObject);
        }

        private static void ConfigureTempo(GameObject enemy, Animator animator)
        {
            var tempo = GetOrAdd<TutorialEnemyAnimationTempoHost>(enemy);
            Undo.RecordObject(tempo, "Configure calm enemy animation tempo");
            tempo.Configure(animator, enemy.transform);
            tempo.enabled = true;
        }

        private static void SetSerializedFloat(UnityEngine.Object target, string propertyName, float value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null) return;
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child;
            var gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);
            gameObject.transform.SetParent(parent, false);
            return gameObject.transform;
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            foreach (var child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static GameObject FindByPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var segments = path.Trim('/').Split('/');
            var root = SceneManager.GetActiveScene().GetRootGameObjects()
                .FirstOrDefault(item => item.name == segments[0]);
            if (root == null) return null;
            var current = root.transform;
            for (var index = 1; index < segments.Length; index++)
            {
                current = current.Find(segments[index]);
                if (current == null) return null;
            }
            return current.gameObject;
        }

        private static string FramePath(string root, string actor, string motion, int index) =>
            $"{root}/{motion}/Frames/{actor}_{motion}_{index:00}.png";

        private static void EnsureAssetFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
#endif
