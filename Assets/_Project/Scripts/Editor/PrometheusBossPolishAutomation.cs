using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Core;
using Narthex.Gameplay;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Narthex.Tools
{
    public static class PrometheusBossPolishAutomation
    {
        private const string ScenePath = "Assets/Scenes/BossDevelopmentScene.unity";
        private const string MainScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string BarSpritePath =
            "Assets/_Project/Art/UI/Tutorial/Generated_v4/TUTO_UI_BarTrack_v4.png";
        private const string ImpactSpritePath =
            "Assets/_Project/Art/AIConcepts/TutorialPlayerVFX/ReviewBatch_v1/Generated/TUTO_VFX_HitImpact_v1.png";
        private const string TheusProjectilePath =
            "Assets/_Project/Art/AIConcepts/TutorialPlayerVFX/ReviewBatch_v2/Generated/TUTO_VFX_TheusSupportProjectile_v1.png";
        private const string SkillIconPath =
            "Assets/_Project/Art/UI/BossSkills/PROME_UI_FourSlashSkill_v1.png";
        private const string TheusFocusedVolleyIconPath =
            "Assets/_Project/Art/UI/TheusSkills/Generated/THEUS_UI_FocusedVolley_v1.png";
        private const string KeyBadgeSpritePath =
            "Assets/_Project/Art/UI/Tutorial/Generated_v4/TUTO_UI_CompactStrip_v4.png";
        private const string SlashVfxPath =
            "Assets/_Project/Art/VFX/BossSkills/PROME_VFX_FourSlashArc_v1.png";
        private const string TheusModelPath =
            "Assets/_Project/Art/AIConcepts/TutorialCharacters/ReviewBatch_v1/Generated/TUTO_CHAR_Theus_v1.png";
        private const string HelteIdlePath =
            "Assets/_Project/Art/AIConcepts/TutorialHelte/AnimationBatch_v2/Sequences/Idle/HELTE_Idle_000.png";
        private const string HelteControllerPath =
            "Assets/_Project/Art/AIConcepts/TutorialHelte/AnimationBatch_v2/UnityGenerated/HelteBoss_v2.controller";
        private const string HeltePortraitPath =
            "Assets/_Project/Art/AIConcepts/TutorialHelte/ReviewBatch_v1/Generated/HELTE_Body_Base.png";
        private const string HeavyImpactClipPath =
            "Assets/_Project/Audio/Sfx/Tutorial/Prototypes/SFX_Impact_Heavy.wav";
        private const string RangedIconPath =
            "Assets/_Project/Art/UI/Tutorial/Generated_v2/TUTO_UI_RangedSkillIcon_v2.png";
        private const string BossBackgroundPath =
            "Assets/_Project/Art/AIConcepts/TutorialBackgrounds/TUTO_EH_BrightSky_Continuous_v4.png";
        private const string ZenithSpritePath =
            "Assets/_Project/Art/AIConcepts/TutorialBackgrounds/TUTO_Zenith_Continuous_Cutout_v6.png";
        private const string BossArenaPlatformSpritePath =
            "Assets/_Project/Art/AIConcepts/TutorialTileSets/ReviewBatch_v1/H_NadirDock/Generated/TUTO_H_Dock_Platform_Middle_v1.png";
        private const string BossArenaSupportSpritePath =
            "Assets/_Project/Art/AIConcepts/TutorialTileSets/ReviewBatch_v1/H_NadirDock/Generated/TUTO_H_Dock_Support_Pillar_v1.png";
        private const string SpriteUnlitMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string HelteCrossSlashPath =
            "Assets/_Project/Art/AIConcepts/TutorialHelte/ReviewBatch_v1/Generated/HELTE_VFX_CrossSlash.png";
        private const string HelteDashPath =
            "Assets/_Project/Art/AIConcepts/TutorialHelte/ReviewBatch_v1/Generated/HELTE_VFX_DashPath.png";
        private const string HelteWarningPath =
            "Assets/_Project/Art/AIConcepts/TutorialHelte/ReviewBatch_v1/Generated/HELTE_VFX_BossWarning.png";
        private const string HeltePhasePath =
            "Assets/_Project/Art/AIConcepts/TutorialHelte/ReviewBatch_v1/Generated/HELTE_VFX_PhaseTransition.png";
        private const string HelteSwordPath =
            "Assets/_Project/Art/AIConcepts/TutorialHelte/ReviewBatch_v1/Generated/HELTE_Weapon_Saber.png";

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            if (!scene.IsValid() || (scene.path != ScenePath && scene.path != MainScenePath))
                throw new InvalidOperationException("Boss polish is restricted to the boss development or tutorial main scene.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying boss polish.");

            var changes = Describe(scene);
            ValidateAssets();
            if (dryRun) return changes;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Helte boss polish and Prome resonance skill");
            try
            {
                var player = RequireComponent<CombatActorHost>(scene, item => item.Kind == CombatActorKind.Player);
                var boss = RequireComponent<CombatActorHost>(scene, item => item.Kind == CombatActorKind.Boss);
                var arena = RequireComponent<TutorialBossArenaHost>(scene);
                var helte = RequireComponent<HelteBossPatternHost>(scene);
                var restart = RequireComponent<TutorialRestartHost>(scene);
                var input = player.GetComponent<PlayerInputHost>();
                var melee = player.GetComponent<MeleeAttackHost>();
                var ranged = player.GetComponent<PlayerRangedAttackHost>() ??
                             scene.GetRootGameObjects()
                                 .SelectMany(item => item.GetComponentsInChildren<PlayerRangedAttackHost>(true))
                                 .FirstOrDefault();
                var body = player.GetComponent<Rigidbody2D>();
                var animation = player.GetComponentInChildren<CharacterPngAnimationBridge>(true);
                var cameraFollow = RequireComponent<CameraFollowHost>(scene);
                if (input == null || melee == null || animation == null)
                    throw new InvalidOperationException("Prome boss skill requires input, melee, and PNG animation bridge.");

                SetInt(player, "maxHealth", 500);
                SetInt(boss, "maxHealth", 2500);

                var hud = Require(scene, "TutorialHUD").transform;
                var bossPanel = Require(scene, "BossHealthBarPanel");
                var track = Require(scene, "BossHealthBarTrack").GetComponent<Image>();
                var fill = Require(scene, "BossHealthBarFill_ART_SLOT").GetComponent<Image>();
                var healthText = Require(scene, "BossHealthValueText").GetComponent<Text>();
                if (track == null || fill == null || healthText == null)
                    throw new InvalidOperationException("Boss health HUD images or value text are missing.");

                var barSprite = LoadSprite(BarSpritePath);
                var panelRect = bossPanel.GetComponent<RectTransform>();
                if (panelRect == null) throw new InvalidOperationException("BossHealthBarPanel RectTransform is missing.");
                Undo.RecordObject(panelRect, "Polish Helte boss health panel layout");
                panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
                panelRect.pivot = new Vector2(0.5f, 1f);
                panelRect.anchoredPosition = new Vector2(0f, -48f);
                panelRect.sizeDelta = new Vector2(1080f, 124f);

                Undo.RecordObject(track, "Apply Helte boss health bar sprite");
                track.sprite = barSprite;
                track.type = Image.Type.Simple;
                track.color = Color.white;
                track.raycastTarget = false;
                track.preserveAspect = false;
                Undo.RecordObject(track.rectTransform, "Resize Helte boss health track");
                track.rectTransform.anchorMin = track.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                track.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                track.rectTransform.anchoredPosition = new Vector2(0f, -20f);
                track.rectTransform.sizeDelta = new Vector2(930f, 42f);

                Undo.RecordObjects(new UnityEngine.Object[] { fill, fill.rectTransform },
                    "Polish Helte boss health fill");
                fill.sprite = barSprite;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.preserveAspect = false;
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = Vector2.one;
                fill.rectTransform.offsetMin = new Vector2(11f, 9f);
                fill.rectTransform.offsetMax = new Vector2(-11f, -9f);

                Undo.RecordObjects(new UnityEngine.Object[] { healthText, healthText.rectTransform },
                    "Polish Helte boss health value label");
                healthText.fontSize = 28;
                healthText.fontStyle = FontStyle.Bold;
                healthText.alignment = TextAnchor.MiddleCenter;
                healthText.horizontalOverflow = HorizontalWrapMode.Overflow;
                healthText.verticalOverflow = VerticalWrapMode.Overflow;
                healthText.raycastTarget = false;
                healthText.rectTransform.anchorMin = healthText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                healthText.rectTransform.pivot = new Vector2(0.5f, 1f);
                healthText.rectTransform.anchoredPosition = new Vector2(0f, -4f);
                healthText.rectTransform.sizeDelta = new Vector2(930f, 40f);
                EnsureOutline(healthText.gameObject);

                var phaseTwoMarker = ConfigureMarker(track.rectTransform, "Phase2Marker", 0.55f, new Color(0.3f, 0.9f, 1f, 0.6f));
                var finalMarker = ConfigureMarker(track.rectTransform, "FinalRushMarker", 0.2f, new Color(1f, 0.35f, 0.2f, 0.6f));
                var stateText = ConfigureText(bossPanel.transform, "BossStateText", healthText.font, 22,
                    TextAnchor.MiddleCenter, new Vector2(0f, -54f), new Vector2(760f, 32f));

                var skill = player.GetComponent<PromeBossSkillHost>();
                if (skill == null) skill = Undo.AddComponent<PromeBossSkillHost>(player.gameObject);
                var strikeVfx = ConfigureStrikeVfx(player.transform, LoadSprite(SlashVfxPath));
                var skillAudio = ConfigureSkillAudio(player.transform);
                var skillSerialized = new SerializedObject(skill);
                Assign(skillSerialized, "inputHost", input);
                Assign(skillSerialized, "playerActor", player);
                Assign(skillSerialized, "bossActor", boss);
                Assign(skillSerialized, "arenaHost", arena);
                Assign(skillSerialized, "meleeAttack", melee);
                Assign(skillSerialized, "rangedAttack", ranged);
                Assign(skillSerialized, "animationBridge", animation);
                Assign(skillSerialized, "playerBody", body);
                AssignArray(skillSerialized, "strikeVfx", strikeVfx.Cast<UnityEngine.Object>().ToArray());
                Assign(skillSerialized, "cameraFollowHost", cameraFollow);
                Assign(skillSerialized, "finalImpactSource", skillAudio);
                Assign(skillSerialized, "finalImpactClip", LoadAudioClip(HeavyImpactClipPath));
                skillSerialized.FindProperty("suppressRangedDuringCombat").boolValue = scene.path != ScenePath;
                skillSerialized.FindProperty("cooldownSeconds").floatValue = 10f;
                skillSerialized.FindProperty("impactDelay").floatValue = 0.12f;
                skillSerialized.FindProperty("strikeInterval").floatValue = 0.34f;
                skillSerialized.FindProperty("finalRecovery").floatValue = 0.55f;
                skillSerialized.FindProperty("finalHitstopTimeScale").floatValue = 0.08f;
                skillSerialized.FindProperty("finalHitstopSeconds").floatValue = 0.075f;
                skillSerialized.FindProperty("finalImpactVolume").floatValue = 0.95f;
                skillSerialized.FindProperty("finalShakeAmplitude").floatValue = 0.18f;
                skillSerialized.FindProperty("finalShakeDuration").floatValue = 0.16f;
                AssignFloatArray(skillSerialized, "playbackSpeed", new[] { 0.9f, 0.96f, 1.02f, 0.84f });
                skillSerialized.ApplyModifiedProperties();

                ConfigureLoreDismissPrompt(hud, healthText.font);
                ConfigureHelteDialoguePortrait(scene);
                var guide = Require(scene, "TutorialGuideCompanion");
                ConfigureTutorialModels(guide, player, boss, helte);
                var theusSupport = ConfigureTheusSupport(guide, player, input, arena,
                    LoadSprite(ImpactSpritePath), scene.path == ScenePath);
                var theusSkillUi = ConfigureTheusSkillHud(hud, LoadSprite(TheusFocusedVolleyIconPath),
                    LoadSprite(KeyBadgeSpritePath), healthText.font, theusSupport);
                var skillUi = ConfigureSkillHud(hud, barSprite, LoadSprite(SkillIconPath),
                    LoadSprite(KeyBadgeSpritePath), healthText.font, skill);
                if (scene.path == ScenePath)
                {
                    ConfigureBossDevelopmentBackground(scene);
                    ConfigureBossArenaPlatforms(scene, LoadSprite(BossArenaPlatformSpritePath),
                        LoadSprite(BossArenaSupportSpritePath));
                    ConfigureHelteSpriteEffects(scene, helte);
                    ConfigureRangedSkillHud(hud, ranged, LoadSprite(RangedIconPath),
                        LoadSprite(KeyBadgeSpritePath), healthText.font);
                    var rangedSerialized = new SerializedObject(ranged);
                    rangedSerialized.FindProperty("startsUnlocked").boolValue = true;
                    rangedSerialized.ApplyModifiedProperties();
                }
                ConfigureTheusAssist(guide, player, arena, helte, restart, LoadSprite(ImpactSpritePath));

                var presenter = bossPanel.GetComponent<BossHealthBarPresenter>();
                if (presenter == null) throw new InvalidOperationException("BossHealthBarPresenter is missing.");
                var presenterSerialized = new SerializedObject(presenter);
                Assign(presenterSerialized, "patternHost", helte);
                Assign(presenterSerialized, "phaseTwoMarker", phaseTwoMarker);
                Assign(presenterSerialized, "finalRushMarker", finalMarker);
                Assign(presenterSerialized, "stateText", stateText);
                presenterSerialized.ApplyModifiedProperties();

                EditorUtility.SetDirty(skill);
                EditorUtility.SetDirty(skillUi);
                EditorUtility.SetDirty(theusSupport);
                EditorUtility.SetDirty(theusSkillUi);
                EditorSceneManager.MarkSceneDirty(scene);
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
            Change("apply-boss-health-sprite", Require(scene, "BossHealthBarTrack"), "no sprite", BarSpritePath),
            Change("add-phase-markers-and-state", Require(scene, "BossHealthBarPanel"), "basic health only", "phase markers + readable opportunity state"),
            Change("configure-prome-boss-skill", Require(scene, "PlayerRoot"), "resonance-gated four-hit rush", "key 3 cooldown-only four-hit rush"),
            Change("polish-four-slash-readability", Require(scene, "PlayerRoot"), "accumulating direction-neutral arcs", "one-at-a-time overlapping arcs aligned to Prome facing"),
            Change("add-fourth-hit-impact-feedback", Require(scene, "PlayerRoot"), "visual slash only", "0.075s hitstop + bounded shake + heavy impact SFX"),
            Change("sync-tutorial-models", Require(scene, "TutorialHelte"), "boss development placeholders", "tutorial-integrated Theus and Helte models"),
            Change("connect-theus-ranged-support", Require(scene, "TutorialGuideCompanion"), "2.4s support cadence", "1.0s low-damage visual support cadence"),
            Change("add-theus-focused-volley", Require(scene, "TutorialGuideCompanion"), "automatic support only", "key 2 five-shot focused volley with 8s cooldown"),
            Change("add-theus-impact-sparks", Require(scene, "TutorialGuideCompanion"), "projectile disappears on hit", "pooled cyan-white expanding impact sparks"),
            Change("connect-theus-boss-assist", Require(scene, "TutorialGuideCompanion"), "not connected", "one revive + phase-two full heal"),
            Change("repair-boss-dev-lore-prompt", Require(scene, "TutorialLoreSubtitlePanel"), "dismiss prompt missing", "SPACE dismiss prompt connected"),
            Change("connect-helte-dialogue-portrait", RequireComponent<DialogueViewModule>(scene).gameObject, "Helte falls back to a color block", HeltePortraitPath),
            Change("sync-boss-development-background", Require(scene, "Main Camera"), "blank boss-development backdrop", "Tutorial H sky + continuous Zenith"),
            Change("replace-helte-placeholder-effects", Require(scene, "HelteCombatPresentation_ART_SLOTS"), "primitive presentation slots", "approved Helte sprite VFX and saber art"),
            Change("normalize-boss-skill-icons", Require(scene, "TutorialHUD"), "missing/mixed skill icon sizes", "skills 1/2/3 visible at 112x112"),
                Change("balance-boss-prototype-health", Require(scene, "TutorialHelte"), "5000 boss / 100 player", "2500 boss / 500 player")
            };
            if (scene.path == ScenePath)
                changes.Add(Change("replace-boss-arena-blockout-platform",
                    Require(scene, "BossArena_Floor_ART_SLOT"),
                    "white blockout meshes", "Nadir dock platform sprites"));
            return changes;
        }

        private static void ConfigureHelteDialoguePortrait(Scene scene)
        {
            var dialogue = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<DialogueViewModule>(true))
                .FirstOrDefault();
            if (dialogue == null) throw new InvalidOperationException("DialogueViewModule is missing.");

            var serialized = new SerializedObject(dialogue);
            var portraits = serialized.FindProperty("speakerPortraits");
            var helteIndex = -1;
            for (var index = 0; index < portraits.arraySize; index++)
            {
                var entry = portraits.GetArrayElementAtIndex(index);
                if (entry.FindPropertyRelative("speakerName").stringValue != "헬테") continue;
                helteIndex = index;
                break;
            }

            if (helteIndex < 0)
            {
                helteIndex = portraits.arraySize;
                portraits.arraySize++;
            }

            var helteEntry = portraits.GetArrayElementAtIndex(helteIndex);
            helteEntry.FindPropertyRelative("speakerName").stringValue = "헬테";
            helteEntry.FindPropertyRelative("portrait").objectReferenceValue = LoadSprite(HeltePortraitPath);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(dialogue);
        }

        private static void ConfigureLoreDismissPrompt(Transform hud, Font font)
        {
            var panel = hud.Find("TutorialLoreSubtitlePanel");
            if (panel == null) return;
            var presenter = panel.GetComponent<TutorialLoreSubtitlePresenter>();
            if (presenter == null) return;
            var prompt = ConfigureText(panel, "LoreDismissPromptText", font, 21, TextAnchor.MiddleCenter,
                new Vector2(0f, -84f), new Vector2(720f, 34f));
            prompt.text = "SPACE  ·  눌러서 닫기";
            prompt.color = new Color(0.72f, 0.96f, 1f, 0.9f);
            var serialized = new SerializedObject(presenter);
            Assign(serialized, "dismissPromptText", prompt);
            serialized.ApplyModifiedProperties();
        }

        private static void ConfigureTutorialModels(GameObject guide, CombatActorHost player,
            CombatActorHost boss, HelteBossPatternHost helte)
        {
            var guideVisual = GetOrCreateChild(guide.transform, "Visual");
            var guideSlot = GetOrCreateChild(guideVisual, "ModelSlot");
            var theusModel = GetOrCreateChild(guideSlot, "AI_ReviewBatch16_Theus");
            var theusRenderer = theusModel.GetComponent<SpriteRenderer>();
            if (theusRenderer == null) theusRenderer = Undo.AddComponent<SpriteRenderer>(theusModel.gameObject);
            Undo.RecordObjects(new UnityEngine.Object[] { theusModel, theusRenderer }, "Sync tutorial Theus model");
            theusModel.localPosition = new Vector3(0f, 0f, -0.05f);
            theusModel.localRotation = Quaternion.identity;
            theusModel.localScale = new Vector3(0.45f, 0.45f, 1f);
            theusRenderer.sprite = LoadSprite(TheusModelPath);
            theusRenderer.color = Color.white;
            theusRenderer.sortingOrder = 700;
            theusModel.gameObject.SetActive(true);
            foreach (var placeholder in guideSlot.GetComponentsInChildren<MeshRenderer>(true))
            {
                Undo.RecordObject(placeholder, "Disable Theus placeholder renderer");
                placeholder.enabled = false;
            }

            var artBind = GetOrCreateChild(boss.transform, "Visual_ART_BIND");
            var bossVisual = GetOrCreateChild(artBind, "BossVisual");
            var helteModel = GetOrCreateChild(bossVisual, "AI_HelteAnimatedSprite");
            var helteRenderer = helteModel.GetComponent<SpriteRenderer>();
            if (helteRenderer == null) helteRenderer = Undo.AddComponent<SpriteRenderer>(helteModel.gameObject);
            var animator = helteModel.GetComponent<Animator>();
            if (animator == null) animator = Undo.AddComponent<Animator>(helteModel.gameObject);
            var bridge = helteModel.GetComponent<CharacterPngAnimationBridge>();
            if (bridge == null) bridge = Undo.AddComponent<CharacterPngAnimationBridge>(helteModel.gameObject);
            Undo.RecordObjects(new UnityEngine.Object[] { helteModel, helteRenderer, animator, bridge },
                "Sync tutorial Helte model");
            helteModel.localPosition = new Vector3(0.000162760422f, -0.360447973f, 0f);
            helteModel.localRotation = Quaternion.identity;
            helteModel.localScale = new Vector3(1.6f, 0.960000038f, 1f);
            helteRenderer.sprite = LoadSprite(HelteIdlePath);
            helteRenderer.color = Color.white;
            helteRenderer.sortingOrder = 700;
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HelteControllerPath);
            bridge.Configure(CharacterPngAnimationPreset.Helte, animator, helteRenderer,
                boss.GetComponent<Rigidbody2D>(), null, null, null, boss.GetComponent<EnemyAttackHost>(), boss,
                helte, boss.GetComponent<CombatVisualMotionHost>(), false, player.transform, 0.76f, 0.76f, 0.76f);
            var bridgeSerialized = new SerializedObject(bridge);
            bridgeSerialized.FindProperty("crossFadeSeconds").floatValue = 0.04f;
            bridgeSerialized.FindProperty("hitDuration").floatValue = 0.16f;
            bridgeSerialized.FindProperty("attackSortingOrder").intValue = 1000;
            bridgeSerialized.ApplyModifiedProperties();
            helteModel.gameObject.SetActive(true);

            foreach (var placeholder in boss.GetComponentsInChildren<MeshRenderer>(true))
            {
                Undo.RecordObject(placeholder, "Disable Helte placeholder renderer");
                placeholder.enabled = false;
            }
        }

        private static TutorialTheusRangedSupportHost ConfigureTheusSupport(GameObject guide,
            CombatActorHost player, PlayerInputHost input, TutorialBossArenaHost arena,
            Sprite impactSprite, bool startsFocusedVolleyUnlocked)
        {
            var root = GetOrCreateChild(guide.transform, "TheusRangedSupport_ART");
            var sprite = LoadSprite(TheusProjectilePath);
            var pool = new GameObject[5];
            var renderers = new SpriteRenderer[5];
            var impactPool = new GameObject[5];
            var impactRenderers = new SpriteRenderer[5];
            for (var index = 0; index < pool.Length; index++)
            {
                var projectile = GetOrCreateChild(root, $"TheusProjectile_{index + 1:00}");
                pool[index] = projectile.gameObject;
                var renderer = projectile.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = Undo.AddComponent<SpriteRenderer>(projectile.gameObject);
                renderers[index] = renderer;
                Undo.RecordObjects(new UnityEngine.Object[] { renderer, projectile }, "Configure Theus boss projectile");
                renderer.sprite = sprite;
                renderer.color = Color.white;
                renderer.sortingOrder = 1100;
                var scale = 0.75f / Mathf.Max(0.01f, sprite.bounds.size.x);
                projectile.localScale = new Vector3(scale, scale, 1f);
                projectile.gameObject.SetActive(false);

                var impact = GetOrCreateChild(root, $"TheusImpact_{index + 1:00}");
                impactPool[index] = impact.gameObject;
                var impactRenderer = impact.GetComponent<SpriteRenderer>();
                if (impactRenderer == null) impactRenderer = Undo.AddComponent<SpriteRenderer>(impact.gameObject);
                impactRenderers[index] = impactRenderer;
                Undo.RecordObjects(new UnityEngine.Object[] { impactRenderer, impact }, "Configure Theus impact spark");
                impactRenderer.sprite = impactSprite;
                impactRenderer.color = Color.white;
                impactRenderer.sortingOrder = 1210 + index;
                var impactScale = 1.05f / Mathf.Max(0.01f, impactSprite.bounds.size.x);
                impact.localScale = new Vector3(impactScale, impactScale, 1f);
                impact.gameObject.SetActive(false);
            }

            var host = guide.GetComponent<TutorialTheusRangedSupportHost>();
            if (host == null) host = Undo.AddComponent<TutorialTheusRangedSupportHost>(guide);
            var serialized = new SerializedObject(host);
            Assign(serialized, "playerSourceActor", player);
            Assign(serialized, "inputHost", input);
            Assign(serialized, "lightFormHost", guide.GetComponent<TutorialTheusLightFormHost>());
            Assign(serialized, "bossArenaHost", arena);
            serialized.FindProperty("bossCombatOnly").boolValue = true;
            serialized.FindProperty("damage").intValue = 4;
            serialized.FindProperty("cooldown").floatValue = 1f;
            serialized.FindProperty("projectileSpeed").floatValue = 14f;
            AssignArray(serialized, "projectilePool", pool.Cast<UnityEngine.Object>().ToArray());
            AssignArray(serialized, "projectileRenderers", renderers.Cast<UnityEngine.Object>().ToArray());
            AssignArray(serialized, "impactPool", impactPool.Cast<UnityEngine.Object>().ToArray());
            AssignArray(serialized, "impactRenderers", impactRenderers.Cast<UnityEngine.Object>().ToArray());
            serialized.FindProperty("impactDuration").floatValue = 0.22f;
            serialized.FindProperty("impactExpansion").floatValue = 0.45f;
            serialized.FindProperty("startsFocusedVolleyUnlocked").boolValue = startsFocusedVolleyUnlocked;
            serialized.FindProperty("focusedVolleyShots").intValue = 5;
            serialized.FindProperty("focusedVolleyDamage").intValue = 12;
            serialized.FindProperty("focusedVolleyInterval").floatValue = 0.18f;
            serialized.FindProperty("focusedVolleyCooldown").floatValue = 8f;
            serialized.FindProperty("focusedVolleyFinalScale").floatValue = 1.35f;
            serialized.ApplyModifiedProperties();
            return host;
        }

        private static void ConfigureTheusAssist(GameObject guide, CombatActorHost player, TutorialBossArenaHost arena,
            HelteBossPatternHost helte, TutorialRestartHost restart, Sprite effectSprite)
        {
            var root = GetOrCreateChild(guide.transform, "TheusBossAssist_ART");
            var revive = ConfigureWorldEffect(root, "ReviveVFX", effectSprite, new Color(0.25f, 1f, 1f, 0.9f), 2.3f);
            var heal = ConfigureWorldEffect(root, "PhaseHealVFX", effectSprite, new Color(0.45f, 1f, 0.65f, 0.9f), 1.8f);
            var host = guide.GetComponent<TutorialTheusBossAssistHost>();
            if (host == null) host = Undo.AddComponent<TutorialTheusBossAssistHost>(guide);
            var serialized = new SerializedObject(host);
            Assign(serialized, "playerActor", player);
            Assign(serialized, "arenaHost", arena);
            Assign(serialized, "heltePatternHost", helte);
            Assign(serialized, "restartHost", restart);
            Assign(serialized, "reviveVfx", revive);
            Assign(serialized, "phaseHealVfx", heal);
            serialized.ApplyModifiedProperties();
        }

        private static TheusFocusedVolleyPresenter ConfigureTheusSkillHud(Transform hud, Sprite skillIcon,
            Sprite keyBadgeSprite, Font font, TutorialTheusRangedSupportHost supportHost)
        {
            var panel = GetOrCreateRectChild(hud, "TheusFocusedVolleyPanel");
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.zero;
            panel.pivot = Vector2.zero;
            panel.anchoredPosition = new Vector2(166f, 42f);
            panel.sizeDelta = new Vector2(112f, 112f);

            var canvas = panel.GetComponent<CanvasGroup>();
            if (canvas == null) canvas = Undo.AddComponent<CanvasGroup>(panel.gameObject);
            canvas.alpha = 0f;
            canvas.blocksRaycasts = false;
            canvas.interactable = false;

            var background = panel.GetComponent<Image>();
            if (background == null) background = Undo.AddComponent<Image>(panel.gameObject);
            background.sprite = null;
            background.color = Color.clear;
            background.raycastTarget = false;
            background.enabled = false;

            var iconRect = GetOrCreateRectChild(panel, "SkillIcon");
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var icon = iconRect.GetComponent<Image>();
            if (icon == null) icon = Undo.AddComponent<Image>(iconRect.gameObject);
            icon.sprite = skillIcon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var cooldownRect = GetOrCreateRectChild(iconRect, "CooldownOverlay");
            cooldownRect.anchorMin = Vector2.zero;
            cooldownRect.anchorMax = Vector2.one;
            cooldownRect.offsetMin = Vector2.zero;
            cooldownRect.offsetMax = Vector2.zero;
            var cooldown = cooldownRect.GetComponent<Image>();
            if (cooldown == null) cooldown = Undo.AddComponent<Image>(cooldownRect.gameObject);
            cooldown.sprite = skillIcon;
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Radial360;
            cooldown.fillOrigin = (int)Image.Origin360.Top;
            cooldown.fillClockwise = false;
            cooldown.fillAmount = 0f;
            cooldown.color = new Color(0.02f, 0.04f, 0.07f, 0.78f);
            cooldown.raycastTarget = false;

            var cooldownText = ConfigureText(iconRect, "CooldownSecondsText", font, 30,
                TextAnchor.MiddleCenter, Vector2.zero, new Vector2(112f, 112f));
            cooldownText.text = string.Empty;

            var keyBadgeRect = GetOrCreateRectChild(panel, "KeyBadge");
            keyBadgeRect.anchorMin = keyBadgeRect.anchorMax = new Vector2(1f, 0f);
            keyBadgeRect.pivot = new Vector2(1f, 0f);
            keyBadgeRect.anchoredPosition = new Vector2(-1f, 1f);
            keyBadgeRect.sizeDelta = new Vector2(38f, 30f);
            var keyBadge = keyBadgeRect.GetComponent<Image>();
            if (keyBadge == null) keyBadge = Undo.AddComponent<Image>(keyBadgeRect.gameObject);
            keyBadge.sprite = keyBadgeSprite;
            keyBadge.type = Image.Type.Sliced;
            keyBadge.color = Color.white;
            keyBadge.raycastTarget = false;

            var keyLabel = ConfigureText(keyBadgeRect, "KeyText", font, 20,
                TextAnchor.MiddleCenter, Vector2.zero, new Vector2(38f, 30f));
            keyLabel.text = "2";

            var presenter = panel.GetComponent<TheusFocusedVolleyPresenter>();
            if (presenter == null) presenter = Undo.AddComponent<TheusFocusedVolleyPresenter>(panel.gameObject);
            var serialized = new SerializedObject(presenter);
            Assign(serialized, "supportHost", supportHost);
            Assign(serialized, "canvasGroup", canvas);
            Assign(serialized, "iconImage", icon);
            Assign(serialized, "cooldownOverlay", cooldown);
            Assign(serialized, "cooldownText", cooldownText);
            serialized.ApplyModifiedProperties();
            return presenter;
        }

        private static PromeBossSkillPresenter ConfigureSkillHud(Transform hud, Sprite barSprite, Sprite skillIcon,
            Sprite keyBadgeSprite, Font font, PromeBossSkillHost skill)
        {
            var panel = GetOrCreateRectChild(hud, "PromeBossSkillPanel");
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = new Vector2(290f, 42f);
            panel.sizeDelta = new Vector2(112f, 112f);
            var canvas = panel.GetComponent<CanvasGroup>();
            if (canvas == null) canvas = Undo.AddComponent<CanvasGroup>(panel.gameObject);
            var background = panel.GetComponent<Image>();
            if (background == null) background = Undo.AddComponent<Image>(panel.gameObject);
            background.sprite = null;
            background.color = Color.clear;
            background.raycastTarget = false;
            background.enabled = false;

            var iconRect = GetOrCreateRectChild(panel, "SkillIcon");
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var icon = iconRect.GetComponent<Image>();
            if (icon == null) icon = Undo.AddComponent<Image>(iconRect.gameObject);
            icon.sprite = skillIcon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var cooldownRect = GetOrCreateRectChild(iconRect, "CooldownOverlay");
            cooldownRect.anchorMin = Vector2.zero;
            cooldownRect.anchorMax = Vector2.one;
            cooldownRect.offsetMin = Vector2.zero;
            cooldownRect.offsetMax = Vector2.zero;
            var cooldown = cooldownRect.GetComponent<Image>();
            if (cooldown == null) cooldown = Undo.AddComponent<Image>(cooldownRect.gameObject);
            cooldown.sprite = skillIcon;
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Radial360;
            cooldown.fillOrigin = 2;
            cooldown.fillClockwise = false;
            cooldown.color = new Color(0.02f, 0.04f, 0.07f, 0.78f);
            cooldown.raycastTarget = false;

            var keyText = ConfigureText(iconRect, "SkillKeyText", font, 25, TextAnchor.MiddleCenter,
                new Vector2(18f, 30f), new Vector2(36f, 36f));
            keyText.text = "3";
            keyText.color = new Color(0.75f, 1f, 1f, 1f);
            keyText.gameObject.SetActive(false);

            var keyBadgeRect = GetOrCreateRectChild(panel, "KeyBadge");
            keyBadgeRect.anchorMin = keyBadgeRect.anchorMax = new Vector2(1f, 0f);
            keyBadgeRect.pivot = new Vector2(1f, 0f);
            keyBadgeRect.anchoredPosition = new Vector2(-1f, 1f);
            keyBadgeRect.sizeDelta = new Vector2(38f, 30f);
            var keyBadge = keyBadgeRect.GetComponent<Image>();
            if (keyBadge == null) keyBadge = Undo.AddComponent<Image>(keyBadgeRect.gameObject);
            keyBadge.sprite = keyBadgeSprite;
            keyBadge.type = Image.Type.Sliced;
            keyBadge.color = Color.white;
            keyBadge.raycastTarget = false;

            var keyLabelRect = GetOrCreateRectChild(keyBadgeRect, "KeyText");
            keyLabelRect.anchorMin = Vector2.zero;
            keyLabelRect.anchorMax = Vector2.one;
            keyLabelRect.offsetMin = Vector2.zero;
            keyLabelRect.offsetMax = Vector2.zero;
            var keyLabel = keyLabelRect.GetComponent<Text>();
            if (keyLabel == null) keyLabel = Undo.AddComponent<Text>(keyLabelRect.gameObject);
            keyLabel.font = font;
            keyLabel.fontSize = 20;
            keyLabel.alignment = TextAnchor.MiddleCenter;
            keyLabel.color = Color.white;
            keyLabel.raycastTarget = false;
            keyLabel.text = "3";
            keyBadgeRect.gameObject.SetActive(true);
            keyLabelRect.gameObject.SetActive(true);

            var fillRect = GetOrCreateRectChild(panel, "ResonanceFill");
            fillRect.anchorMin = new Vector2(0.22f, 0.25f);
            fillRect.anchorMax = new Vector2(0.96f, 0.68f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fill = fillRect.GetComponent<Image>();
            if (fill == null) fill = Undo.AddComponent<Image>(fillRect.gameObject);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.color = new Color(0.15f, 0.95f, 1f, 0.9f);
            fill.raycastTarget = false;
            Undo.RecordObject(fillRect.gameObject, "Hide retired resonance gauge");
            fillRect.gameObject.SetActive(false);

            var value = ConfigureText(panel, "ResonanceValueText", font, 24, TextAnchor.MiddleCenter,
                new Vector2(60f, 0f), new Vector2(450f, 64f));
            value.text = string.Empty;
            value.gameObject.SetActive(false);
            var presenter = panel.GetComponent<PromeBossSkillPresenter>();
            if (presenter == null) presenter = Undo.AddComponent<PromeBossSkillPresenter>(panel.gameObject);
            var serialized = new SerializedObject(presenter);
            Assign(serialized, "skillHost", skill);
            Assign(serialized, "canvasGroup", canvas);
            Assign(serialized, "iconImage", icon);
            Assign(serialized, "cooldownOverlay", cooldown);
            Assign(serialized, "valueText", value);
            serialized.ApplyModifiedProperties();
            return presenter;
        }

        private static void ConfigureBossDevelopmentBackground(Scene scene)
        {
            PrometheusBackgroundAutomation.Apply(scene, "H", BossBackgroundPath, 1f, -1000, 20f, false);
            PrometheusZenithApproachAutomation.Apply(
                scene,
                ZenithSpritePath,
                "TutorialRuntimeRoot/StageRoot/PlayerRoot",
                239f,
                867.87f,
                new Vector2(0.8f, 0.7f),
                new Vector2(0.7f, 0.58f),
                0.14f,
                0.56f,
                0.72f,
                1f,
                -990,
                false);

            var root = scene.GetRootGameObjects().First(item => item.name == "AI_TutorialBackgroundRoot");
            var presenter = root.GetComponent<TutorialBackgroundPresenter>();
            var service = scene.GetRootGameObjects()
                .SelectMany(item => item.GetComponentsInChildren<ServiceRoot>(true))
                .FirstOrDefault();
            var camera = scene.GetRootGameObjects()
                .SelectMany(item => item.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(item => item.CompareTag("MainCamera"));
            if (presenter == null || camera == null)
                throw new InvalidOperationException("Boss development background presenter or main camera is missing.");
            presenter.Configure(service, camera, "H", 20f);
            EditorUtility.SetDirty(presenter);
        }

        private static void ConfigureHelteSpriteEffects(Scene scene, HelteBossPatternHost helte)
        {
            var idleSprite = LoadSprite(HelteIdlePath);
            ConfigureEffectSlot(Require(scene, "BlinkAfterimage_ART_SLOT"), idleSprite,
                new Color(0.35f, 0.95f, 1f, 0.42f), 2.1f, 930);
            ConfigureEffectSlot(Require(scene, "DashPath_ART_SLOT"), LoadSprite(HelteDashPath),
                Color.white, 6.4f, 920);
            ConfigureEffectSlot(Require(scene, "CrossSlashWarning_ART_SLOT"), LoadSprite(HelteCrossSlashPath),
                Color.white, 4.5f, 940);
            ConfigureEffectSlot(Require(scene, "PhaseTransition_ART_SLOT"), LoadSprite(HeltePhasePath),
                Color.white, 5.4f, 935);
            ConfigureEffectSlot(Require(scene, "BossWarning_ART_SLOT"), LoadSprite(HelteWarningPath),
                Color.white, 10.5f, 900);

            var saber = LoadSprite(HelteSwordPath);
            ConfigureEffectSlot(Require(scene, "SwordVisual_Left_ART_SLOT"), saber,
                Color.white, 1.8f, 945, true);
            ConfigureEffectSlot(Require(scene, "SwordVisual_Right_ART_SLOT"), saber,
                Color.white, 1.8f, 945, true);
            ConfigureEffectSlot(Require(scene, "SwordVisual_Center_ART_SLOT"), saber,
                Color.white, 1.8f, 945, true);

            var vfxHost = helte.GetComponent<HeltePatternVfxHost>();
            if (vfxHost == null) vfxHost = Undo.AddComponent<HeltePatternVfxHost>(helte.gameObject);
            var serialized = new SerializedObject(vfxHost);
            Assign(serialized, "patternHost", helte);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(vfxHost);
        }

        private static void ConfigureBossArenaPlatforms(Scene scene, Sprite platformSprite, Sprite supportSprite)
        {
            var unlitMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteUnlitMaterialPath);
            if (unlitMaterial == null)
                throw new InvalidOperationException($"Boss platform material is missing: {SpriteUnlitMaterialPath}");

            // The boss-development scene still keeps the imported dock's long white
            // SpriteRenderer as its playable base. Replace that renderer in-place so
            // its hand-authored transform and collider relationship remain untouched.
            var dockRoot = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "선착장");
            var dockBase = dockRoot != null ? dockRoot.Find("Square") : null;
            var dockBaseRenderer = dockBase != null ? dockBase.GetComponent<SpriteRenderer>() : null;
            if (dockBaseRenderer != null)
            {
                Undo.RecordObject(dockBase, "Restore imported boss platform footprint");
                dockBase.localScale = new Vector3(60f, 1f, 1f);
                var sourceScale = dockBase.lossyScale;
                // The imported Square uses a 362x transform to compensate for its
                // tiny source sprite; its authored visible platform is 60x1 units.
                // Keep that visible footprint instead of inheriting the oversized
                // collider/transform dimensions.
                var dockWorldSize = new Vector2(60f, 1f);
                Undo.RecordObject(dockBaseRenderer, "Hide imported boss platform blockout");
                dockBaseRenderer.enabled = false;
                EditorUtility.SetDirty(dockBaseRenderer);

                var dockArt = dockBase.Find("BossDockPlatformSprite_ART") ??
                              GetOrCreateChild(dockBase, "BossDockPlatformSprite_ART");
                var dockArtRenderer = dockArt.GetComponent<SpriteRenderer>();
                if (dockArtRenderer == null)
                    dockArtRenderer = Undo.AddComponent<SpriteRenderer>(dockArt.gameObject);
                Undo.RecordObjects(new UnityEngine.Object[] { dockArt, dockArtRenderer },
                    "Apply Nadir dock sprite to imported boss platform");
                dockArtRenderer.sprite = platformSprite;
                dockArtRenderer.sharedMaterial = unlitMaterial;
                dockArtRenderer.color = Color.white;
                dockArtRenderer.drawMode = SpriteDrawMode.Tiled;
                dockArtRenderer.tileMode = SpriteTileMode.Continuous;
                dockArtRenderer.size = dockWorldSize;
                dockArtRenderer.sortingOrder = 5;
                var dockScale = dockBase.lossyScale;
                dockArt.localPosition = Vector3.zero;
                dockArt.localRotation = Quaternion.identity;
                dockArt.localScale = new Vector3(
                    1f / Mathf.Max(0.01f, Mathf.Abs(dockScale.x)),
                    1f / Mathf.Max(0.01f, Mathf.Abs(dockScale.y)),
                    1f);
                EditorUtility.SetDirty(dockArtRenderer);
                EditorUtility.SetDirty(dockArt);
            }

            var floor = Require(scene, "BossArena_Floor_ART_SLOT");
            var collider = floor.GetComponent<BoxCollider2D>() ??
                           throw new InvalidOperationException("Boss arena floor collider is missing.");
            var mesh = floor.GetComponent<MeshRenderer>();
            if (mesh != null)
            {
                Undo.RecordObject(mesh, "Disable boss arena blockout mesh");
                mesh.enabled = false;
                EditorUtility.SetDirty(mesh);
            }

            var visual = floor.transform.Find("BossArenaFloorSprite_ART") ??
                         GetOrCreateChild(floor.transform, "BossArenaFloorSprite_ART");
            var renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = Undo.AddComponent<SpriteRenderer>(visual.gameObject);
            if (renderer == null)
                throw new InvalidOperationException("Could not create the boss arena floor SpriteRenderer.");
            Undo.RecordObjects(new UnityEngine.Object[] { visual, renderer },
                "Apply Nadir dock sprite to boss arena floor");
            var floorScale = floor.transform.lossyScale;
            var worldSize = new Vector2(
                collider.size.x * Mathf.Abs(floorScale.x),
                collider.size.y * Mathf.Abs(floorScale.y));
            renderer.sprite = platformSprite;
            renderer.sharedMaterial = unlitMaterial;
            renderer.color = Color.white;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = new Vector2(worldSize.x, worldSize.y);
            renderer.sortingOrder = 5;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = new Vector3(
                1f / Mathf.Max(0.01f, Mathf.Abs(floorScale.x)),
                1f / Mathf.Max(0.01f, Mathf.Abs(floorScale.y)),
                1f);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(visual);

            var terrainBlocks = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TerrainBlock>(true));
            foreach (var block in terrainBlocks)
            {
                var blockVisual = block.transform.Find("Visual");
                if (blockVisual == null || !block.HasValidSetup) continue;
                var blockMesh = blockVisual.GetComponent<MeshRenderer>();
                if (blockMesh != null)
                {
                    Undo.RecordObject(blockMesh, "Disable runtime terrain blockout mesh");
                    blockMesh.enabled = false;
                    EditorUtility.SetDirty(blockMesh);
                }
                var blockSprite = block.transform.Find("Sprite_ART") ??
                                  GetOrCreateChild(block.transform, "Sprite_ART");
                var blockRenderer = blockSprite.GetComponent<SpriteRenderer>();
                if (blockRenderer == null)
                    blockRenderer = Undo.AddComponent<SpriteRenderer>(blockSprite.gameObject);
                if (blockRenderer == null)
                    throw new InvalidOperationException("Could not create a runtime terrain SpriteRenderer.");
                Undo.RecordObjects(new UnityEngine.Object[] { blockSprite, blockRenderer },
                    "Apply Nadir dock sprite to runtime terrain");
                blockRenderer.sprite = block.Size.y > block.Size.x ? supportSprite : platformSprite;
                blockRenderer.sharedMaterial = unlitMaterial;
                blockRenderer.color = Color.white;
                blockRenderer.drawMode = SpriteDrawMode.Tiled;
                blockRenderer.tileMode = SpriteTileMode.Continuous;
                blockRenderer.size = block.Size;
                blockRenderer.sortingOrder = 5;
                blockSprite.localPosition = Vector3.zero;
                blockSprite.localRotation = Quaternion.identity;
                blockSprite.localScale = Vector3.one;
                EditorUtility.SetDirty(blockRenderer);
                EditorUtility.SetDirty(blockSprite);
            }
        }

        private static void ConfigureEffectSlot(GameObject slot, Sprite sprite, Color color,
            float targetWidth, int sortingOrder, bool sizeByHeight = false)
        {
            var mesh = slot.GetComponent<MeshRenderer>();
            if (mesh != null)
            {
                Undo.RecordObject(mesh, "Disable Helte placeholder mesh");
                mesh.enabled = false;
            }

            var visual = slot.transform.Find("Sprite_ART") ?? GetOrCreateChild(slot.transform, "Sprite_ART");
            var renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = Undo.AddComponent<SpriteRenderer>(visual.gameObject);
            Undo.RecordObjects(new UnityEngine.Object[] { renderer, slot.transform, visual }, "Apply Helte sprite effect");
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = true;
            var sourceSize = sizeByHeight ? sprite.bounds.size.y : sprite.bounds.size.x;
            var scale = targetWidth / Mathf.Max(0.01f, sourceSize);
            slot.transform.localScale = Vector3.one;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = new Vector3(scale, scale, 1f);
            EditorUtility.SetDirty(renderer);
        }

        private static void ConfigureRangedSkillHud(Transform hud, PlayerRangedAttackHost rangedAttack,
            Sprite iconSprite, Sprite keyBadgeSprite, Font font)
        {
            if (rangedAttack == null)
                throw new InvalidOperationException("Boss development ranged attack host is missing.");

            var panel = GetOrCreateRectChild(hud, "RangedAttackCooldownHUD");
            panel.anchorMin = panel.anchorMax = Vector2.zero;
            panel.pivot = Vector2.zero;
            panel.anchoredPosition = new Vector2(42f, 42f);
            panel.sizeDelta = new Vector2(112f, 112f);

            var icon = panel.GetComponent<Image>();
            if (icon == null) icon = Undo.AddComponent<Image>(panel.gameObject);
            icon.sprite = iconSprite;
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.color = Color.white;
            icon.raycastTarget = false;

            var canvas = panel.GetComponent<CanvasGroup>();
            if (canvas == null) canvas = Undo.AddComponent<CanvasGroup>(panel.gameObject);
            canvas.alpha = 1f;
            canvas.interactable = false;
            canvas.blocksRaycasts = false;

            var overlayRect = GetOrCreateRectChild(panel, "CooldownRadialOverlay");
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
            var overlay = overlayRect.GetComponent<Image>();
            if (overlay == null) overlay = Undo.AddComponent<Image>(overlayRect.gameObject);
            overlay.sprite = iconSprite;
            overlay.type = Image.Type.Filled;
            overlay.fillMethod = Image.FillMethod.Radial360;
            overlay.fillOrigin = (int)Image.Origin360.Top;
            overlay.fillClockwise = false;
            overlay.color = new Color(0.02f, 0.03f, 0.04f, 0.76f);
            overlay.raycastTarget = false;

            var cooldownText = ConfigureText(panel, "CooldownSecondsText", font, 30,
                TextAnchor.MiddleCenter, Vector2.zero, new Vector2(112f, 112f));
            cooldownText.text = string.Empty;

            var badge = GetOrCreateRectChild(panel, "KeyBadge");
            badge.anchorMin = badge.anchorMax = new Vector2(1f, 0f);
            badge.pivot = new Vector2(1f, 0f);
            badge.anchoredPosition = new Vector2(-1f, 1f);
            badge.sizeDelta = new Vector2(38f, 30f);
            var badgeImage = badge.GetComponent<Image>();
            if (badgeImage == null) badgeImage = Undo.AddComponent<Image>(badge.gameObject);
            badgeImage.sprite = keyBadgeSprite;
            badgeImage.type = Image.Type.Sliced;
            badgeImage.color = Color.white;
            badgeImage.raycastTarget = false;
            var keyText = ConfigureText(badge, "KeyText", font, 20,
                TextAnchor.MiddleCenter, Vector2.zero, new Vector2(38f, 30f));
            keyText.text = "1";

            var presenter = panel.GetComponent<RangedAttackCooldownPresenter>();
            if (presenter == null) presenter = Undo.AddComponent<RangedAttackCooldownPresenter>(panel.gameObject);
            var serialized = new SerializedObject(presenter);
            Assign(serialized, "rangedAttack", rangedAttack);
            Assign(serialized, "canvasGroup", canvas);
            Assign(serialized, "cooldownOverlay", overlay);
            Assign(serialized, "cooldownText", cooldownText);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(presenter);
        }

        private static GameObject[] ConfigureStrikeVfx(Transform player, Sprite sprite)
        {
            var root = GetOrCreateChild(player, "BossSkillVFX_ART");
            var result = new GameObject[4];
            for (var index = 0; index < result.Length; index++)
            {
                var effect = GetOrCreateChild(root, $"StrikeVFX_{index + 1:00}");
                result[index] = effect.gameObject;
                var renderer = effect.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = Undo.AddComponent<SpriteRenderer>(effect.gameObject);
                renderer.sprite = sprite;
                renderer.color = index == 3
                    ? new Color(0.95f, 1f, 1f, 1f)
                    : new Color(0.25f, 0.95f, 1f, 0.82f);
                renderer.sortingOrder = 1200 + index;
                var widths = new[] { 2.9f, 3.25f, 3.6f, 4.25f };
                var scale = widths[index] / Mathf.Max(0.01f, sprite.bounds.size.x);
                effect.localScale = new Vector3(scale, scale, 1f);
                effect.gameObject.SetActive(false);
            }
            return result;
        }

        private static AudioSource ConfigureSkillAudio(Transform player)
        {
            var audioRoot = GetOrCreateChild(player, "BossSkillAudio");
            var source = audioRoot.GetComponent<AudioSource>();
            if (source == null) source = Undo.AddComponent<AudioSource>(audioRoot.gameObject);
            Undo.RecordObject(source, "Configure Prome boss skill impact audio");
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            source.priority = 32;
            return source;
        }

        private static Image ConfigureMarker(RectTransform parent, string name, float normalizedX, Color color)
        {
            var rect = GetOrCreateRectChild(parent, name);
            rect.anchorMin = new Vector2(normalizedX, 0.18f);
            rect.anchorMax = new Vector2(normalizedX, 0.82f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(4f, 0f);
            var image = rect.GetComponent<Image>();
            if (image == null) image = Undo.AddComponent<Image>(rect.gameObject);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text ConfigureText(Transform parent, string name, Font font, int size,
            TextAnchor alignment, Vector2 position, Vector2 dimensions)
        {
            var rect = GetOrCreateRectChild(parent, name);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            var text = rect.GetComponent<Text>();
            if (text == null) text = Undo.AddComponent<Text>(rect.gameObject);
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void EnsureOutline(GameObject target)
        {
            var outline = target.GetComponent<Outline>();
            if (outline == null) outline = Undo.AddComponent<Outline>(target);
            Undo.RecordObject(outline, "Polish boss HUD text outline");
            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
        }

        private static GameObject ConfigureWorldEffect(Transform parent, string name, Sprite sprite, Color color, float width)
        {
            var effect = GetOrCreateChild(parent, name);
            var renderer = effect.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = Undo.AddComponent<SpriteRenderer>(effect.gameObject);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 1250;
            var scale = width / Mathf.Max(0.01f, sprite.bounds.size.x);
            effect.localScale = Vector3.one * scale;
            effect.gameObject.SetActive(false);
            return effect.gameObject;
        }

        private static void SetInt(Component component, string property, int value)
        {
            var serialized = new SerializedObject(component);
            var field = serialized.FindProperty(property) ?? throw new MissingFieldException(component.GetType().Name, property);
            field.intValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static void Assign(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName) ??
                           throw new MissingFieldException(serialized.targetObject.GetType().Name, propertyName);
            property.objectReferenceValue = value;
        }

        private static void AssignArray(SerializedObject serialized, string propertyName, UnityEngine.Object[] values)
        {
            var property = serialized.FindProperty(propertyName) ??
                           throw new MissingFieldException(serialized.targetObject.GetType().Name, propertyName);
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static void AssignFloatArray(SerializedObject serialized, string propertyName, float[] values)
        {
            var property = serialized.FindProperty(propertyName) ??
                           throw new MissingFieldException(serialized.targetObject.GetType().Name, propertyName);
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).floatValue = values[index];
        }

        private static void ValidateAssets()
        {
            LoadSprite(BarSpritePath);
            LoadSprite(ImpactSpritePath);
            LoadSprite(TheusProjectilePath);
            LoadSprite(SkillIconPath);
            LoadSprite(KeyBadgeSpritePath);
            LoadSprite(SlashVfxPath);
            LoadSprite(TheusModelPath);
            LoadSprite(HelteIdlePath);
            LoadSprite(HeltePortraitPath);
            LoadSprite(BossArenaPlatformSpritePath);
            LoadSprite(BossArenaSupportSpritePath);
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HelteControllerPath) == null)
                throw new InvalidOperationException("Animator controller missing: " + HelteControllerPath);
            LoadAudioClip(HeavyImpactClipPath);
        }

        private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path) ??
            throw new InvalidOperationException("Sprite asset missing: " + path);

        private static AudioClip LoadAudioClip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path) ??
            throw new InvalidOperationException("Audio clip missing: " + path);

        private static GameObject Require(Scene scene, string name) =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == name)?.gameObject ??
            throw new InvalidOperationException("Scene object missing: " + name);

        private static T RequireComponent<T>(Scene scene, Func<T, bool> predicate = null) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault(item => predicate == null || predicate(item)) ??
            throw new InvalidOperationException("Scene component missing: " + typeof(T).Name);

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);
            gameObject.transform.SetParent(parent, false);
            return gameObject.transform;
        }

        private static RectTransform GetOrCreateRectChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing as RectTransform ?? throw new InvalidOperationException(name + " requires RectTransform.");
            var gameObject = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }

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
