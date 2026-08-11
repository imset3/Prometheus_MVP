using System;
using System.Collections.Generic;
using System.IO;
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
    /// <summary>
    /// Builds the dedicated Helte v2 PNG sequences and assigns them without moving the authored boss layout.
    /// </summary>
    public static class PrometheusHelteAnimationV2Automation
    {
        public const string SequenceRoot =
            "Assets/_Project/Art/AIConcepts/TutorialHelte/AnimationBatch_v2/Sequences";
        public const string OutputRoot =
            "Assets/_Project/Art/AIConcepts/TutorialHelte/AnimationBatch_v2/UnityGenerated";
        public const string ControllerPath = OutputRoot + "/HelteBoss_v2.controller";

        private static readonly MotionSpec[] Motions =
        {
            new("Idle", 10f, true),
            new("BasicWindup", 8f),
            new("BasicLeftSlash", 16f),
            new("BasicAdvance", 12f),
            new("BasicRightSlash", 16f),
            new("BlinkVanish", 18f),
            new("BlinkReappear", 18f),
            new("DashTelegraph", 8f),
            new("DashApproach", 18f),
            new("CrossSlashTelegraph", 10f),
            new("CrossSlash", 16f),
            new("SwordFocus", 6f),
            new("SwordVolley", 12f),
            new("CounterTelegraph", 8f),
            new("CounterStance", 10f),
            new("PhaseTransition", 6f),
            new("Recover", 8f),
            new("Hit", 16f),
            new("Death", 6f)
        };

        private static readonly (string Property, float Value)[] ReadablePacing =
        {
            ("openingDelaySeconds", 0.55f),
            ("basicWindupSeconds", 0.4f),
            ("basicSecondHitDelaySeconds", 0.4f),
            ("basicAdvanceSeconds", 0.25f),
            ("basicFinalFollowThroughSeconds", 0.4f),
            ("blinkVanishSeconds", 0.25f),
            ("blinkTelegraphSeconds", 0.3f),
            ("dashTelegraphSeconds", 0.4f),
            ("dashDurationSeconds", 0.45f),
            ("crossSlashWarningSeconds", 0.35f),
            ("crossSlashFollowThroughSeconds", 0.4f),
            ("phaseTransitionSeconds", 1.35f),
            ("finalRushTransitionSeconds", 1f),
            ("swordFocusSeconds", 0.7f),
            ("swordIntervalSeconds", 0.38f),
            ("counterTelegraphSeconds", 0.5f)
        };

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            if (!scene.IsValid() ||
                (scene.path != "Assets/Scenes/BossDevelopmentScene.unity" &&
                 scene.path != "Assets/Scenes/TutorialScene.unity"))
                throw new InvalidOperationException(
                    "Helte animation v2 is restricted to BossDevelopmentScene or TutorialScene.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying Helte animation v2.");

            ValidateSequences();
            var changes = new List<PrometheusAiChange>
            {
                new()
                {
                    action = "build-helte-animation-v2",
                    hierarchyPath = ControllerPath,
                    before = "shared or fallback Helte clips",
                    after = $"{Motions.Length} dedicated PNG animation states"
                },
                new()
                {
                    action = "assign-helte-animation-v2",
                    hierarchyPath = "Boss/Visual_ART_BIND/BossVisual/AI_HelteAnimatedSprite",
                    before = "HelteBoss.controller (AnimationBatch_v1)",
                    after = "HelteBoss_v2.controller (AnimationBatch_v2)"
                }
            };
            if (dryRun) return changes;

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureFolder(OutputRoot);
            EnsureFolder(OutputRoot + "/Clips");

            var clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
            foreach (var motion in Motions)
            {
                var sprites = ImportMotionSprites(motion.Name);
                clips[motion.Name] = CreateOrUpdateClip(motion, sprites);
            }

            var controller = CreateOrUpdateController(clips);
            ApplyToScene(scene, controller, clips["Idle"]);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return changes;
        }

        public static List<PrometheusAiChange> ApplyReadableMotionPacing(Scene scene, bool dryRun)
        {
            var changes = Apply(scene, dryRun);
            var patternHost = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HelteBossPatternHost>(true))
                .FirstOrDefault();
            if (patternHost == null)
                throw new InvalidOperationException("HelteBossPatternHost was not found in the target scene.");

            foreach (var (property, value) in ReadablePacing)
            {
                changes.Add(new PrometheusAiChange
                {
                    action = "set-helte-readable-pacing",
                    hierarchyPath = BuildHierarchyPath(patternHost.transform),
                    before = property,
                    after = value.ToString("0.###")
                });
            }
            if (dryRun) return changes;

            var serialized = new SerializedObject(patternHost);
            foreach (var (property, value) in ReadablePacing)
            {
                var field = serialized.FindProperty(property);
                if (field == null)
                    throw new InvalidOperationException($"Helte pacing property is missing: {property}");
                field.floatValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(patternHost);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return changes;
        }

        private static string BuildHierarchyPath(Transform target)
        {
            var parts = new Stack<string>();
            while (target != null)
            {
                parts.Push(target.name);
                target = target.parent;
            }
            return string.Join("/", parts);
        }

        private static void ValidateSequences()
        {
            foreach (var motion in Motions)
            {
                var folder = $"{SequenceRoot}/{motion.Name}";
                if (!Directory.Exists(folder))
                    throw new DirectoryNotFoundException($"Helte motion folder is missing: {folder}");
                if (Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly).Length == 0)
                    throw new InvalidOperationException($"Helte motion has no PNG frames: {motion.Name}");
            }
        }

        private static List<Sprite> ImportMotionSprites(string motionName)
        {
            var folder = $"{SequenceRoot}/{motionName}";
            var paths = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly)
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var sprites = new List<Sprite>(paths.Length);
            foreach (var path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("Texture importer missing: " + path);

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 256f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) throw new InvalidOperationException("Sprite import failed: " + path);
                sprites.Add(sprite);
            }
            return sprites;
        }

        private static AnimationClip CreateOrUpdateClip(MotionSpec motion, IReadOnlyList<Sprite> sprites)
        {
            var path = $"{OutputRoot}/Clips/{motion.Name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = motion.Name };
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.frameRate = motion.Fps;
            var keyframes = sprites.Select((sprite, index) => new ObjectReferenceKeyframe
            {
                time = index / motion.Fps,
                value = sprite
            }).ToArray();
            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = motion.Loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController CreateOrUpdateController(
            IReadOnlyDictionary<string, AnimationClip> clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            var stateMachine = controller.layers[0].stateMachine;
            AnimatorState idle = null;
            foreach (var motion in Motions)
            {
                var state = stateMachine.states.Select(item => item.state)
                    .FirstOrDefault(item => item.name == motion.Name);
                if (state == null) state = stateMachine.AddState(motion.Name);
                state.motion = clips[motion.Name];
                state.writeDefaultValues = true;
                if (motion.Name == "Idle") idle = state;
            }
            if (idle != null) stateMachine.defaultState = idle;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ApplyToScene(
            Scene scene,
            RuntimeAnimatorController controller,
            AnimationClip idleClip)
        {
            var actors = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CombatActorHost>(true))
                .ToArray();
            var boss = actors.FirstOrDefault(actor => actor.Kind == CombatActorKind.Boss) ??
                       throw new InvalidOperationException("Boss CombatActorHost was not found.");
            var player = actors.FirstOrDefault(actor => actor.Kind == CombatActorKind.Player) ??
                         throw new InvalidOperationException("Player CombatActorHost was not found.");
            var helte = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HelteBossPatternHost>(true))
                .FirstOrDefault() ?? throw new InvalidOperationException("HelteBossPatternHost was not found.");
            var visual = boss.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "AI_HelteAnimatedSprite") ??
                         throw new InvalidOperationException("AI_HelteAnimatedSprite was not found.");
            var renderer = visual.GetComponent<SpriteRenderer>() ??
                           throw new InvalidOperationException("Helte SpriteRenderer was not found.");
            var animator = visual.GetComponent<Animator>() ?? Undo.AddComponent<Animator>(visual.gameObject);
            var bridge = visual.GetComponent<CharacterPngAnimationBridge>() ??
                         Undo.AddComponent<CharacterPngAnimationBridge>(visual.gameObject);
            var blendOverlay = visual.Find("FrameBlendOverlay_ART");
            if (blendOverlay == null)
            {
                var overlayObject = new GameObject("FrameBlendOverlay_ART");
                Undo.RegisterCreatedObjectUndo(overlayObject, "Create Helte frame blend overlay");
                blendOverlay = overlayObject.transform;
                blendOverlay.SetParent(visual, false);
            }
            var blendRenderer = blendOverlay.GetComponent<SpriteRenderer>();
            if (blendRenderer == null) blendRenderer = Undo.AddComponent<SpriteRenderer>(blendOverlay.gameObject);
            var frameBlend = visual.GetComponent<SpriteFrameBlendHost>();
            if (frameBlend == null) frameBlend = Undo.AddComponent<SpriteFrameBlendHost>(visual.gameObject);
            if (blendRenderer == null || frameBlend == null)
                throw new InvalidOperationException("Could not create the Helte frame blend components.");

            Undo.RecordObjects(new UnityEngine.Object[] { renderer, animator, bridge, blendRenderer, frameBlend },
                "Apply dedicated Helte v2 animations");
            animator.runtimeAnimatorController = controller;
            renderer.sprite = GetFirstSprite(idleClip);
            renderer.color = Color.white;
            bridge.Configure(
                CharacterPngAnimationPreset.Helte,
                animator,
                renderer,
                boss.GetComponent<Rigidbody2D>(),
                null,
                null,
                null,
                boss.GetComponent<EnemyAttackHost>(),
                boss,
                helte,
                boss.GetComponent<CombatVisualMotionHost>(),
                false,
                player.transform,
                0.27f,
                0.27f,
                0.27f);
            blendOverlay.localPosition = Vector3.zero;
            blendOverlay.localRotation = Quaternion.identity;
            blendOverlay.localScale = Vector3.one;
            blendRenderer.sprite = null;
            blendRenderer.color = Color.clear;
            frameBlend.Configure(renderer, blendRenderer, 0.04f);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(bridge);
            EditorUtility.SetDirty(blendRenderer);
            EditorUtility.SetDirty(frameBlend);
        }

        private static Sprite GetFirstSprite(AnimationClip clip)
        {
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            if (bindings.Length == 0) return null;
            var frames = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            return frames.Length == 0 ? null : frames[0].value as Sprite;
        }

        private static void EnsureFolder(string path)
        {
            var normalized = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized)) return;
            var split = normalized.LastIndexOf('/');
            var parent = normalized.Substring(0, split);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, normalized.Substring(split + 1));
        }

        private readonly struct MotionSpec
        {
            public MotionSpec(string name, float fps, bool loop = false)
            {
                Name = name;
                Fps = fps;
                Loop = loop;
            }

            public string Name { get; }
            public float Fps { get; }
            public bool Loop { get; }
        }
    }
}
