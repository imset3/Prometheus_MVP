using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Narthex.Content;
using Narthex.Core;
using Narthex.Gameplay;
using Narthex.Presentation;
using Narthex.Save;
using Narthex.SceneFlow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Narthex.PlayModeTests
{
    public sealed class TutorialSceneRuntimeSmokeTests
    {
        private const string ImportedTutorialScenePath =
            "Assets/Scenes/TutorialScene.unity";
        private const string BossDevelopmentScenePath =
            "Assets/Scenes/BossDevelopmentScene.unity";

        [UnityTest]
        public IEnumerator BossDevelopmentScene_BootstrapsDirectlyIntoHelte()
        {
#if UNITY_EDITOR
            var loadOperation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                BossDevelopmentScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(loadOperation, Is.Not.Null);
            while (!loadOperation.isDone) yield return null;
#else
            Assert.Ignore("The Helte FSM development scene test runs in the Unity Editor.");
            yield break;
#endif

            var scene = SceneManager.GetActiveScene();
            Assert.That(scene.name, Is.EqualTo("BossDevelopmentScene"));
            Assert.That(
                FindSceneComponent<HelteBossFsmDevBootstrapHost>(scene),
                Is.Not.Null,
                "The development scene must retain its Helte-only bootstrap marker.");

            var sectionSkip = FindSceneComponent<TutorialDebugSectionSkipHost>(scene);
            yield return WaitForConditionRealtime(
                () => sectionSkip.ActiveSectionIndex == sectionSkip.SectionCount - 1,
                3f,
                "The development scene did not jump directly to the Helte section.");

            Assert.That(
                FindSceneComponent<TutorialQuestSequenceHost>(scene).CurrentQuestId,
                Is.EqualTo("QST-TUTO-008"));
            Assert.That(
                FindSceneComponent<TutorialStatusPresenter>(scene).CurrentProgressId,
                Is.EqualTo("TUTO_H_01"),
                "The development HUD must follow the debug-jumped Helte quest.");
            Assert.That(
                FindSceneComponent<TutorialStatusPresenter>(scene).CurrentLocationName,
                Is.EqualTo("나디르 선착장"));
            Assert.That(FindSceneTransform(scene, "선착장").gameObject.activeInHierarchy, Is.True);
            Assert.That(FindSceneTransform(scene, "H_Helte_Integration").gameObject.activeInHierarchy, Is.True);
            Assert.That(
                FindSceneTransformOrDefault(scene, "BossArena_EntryGate_ART_SLOT"),
                Is.Null,
                "The development arena must use the permanently open Helte approach.");
            var bossArena = FindSceneTransform(scene, "BossArena_Controller")
                .GetComponent<TutorialBossArenaHost>();
            Assert.That(bossArena.HasValidSetup, Is.True);
            var helte = FindSceneTransform(scene, "TutorialHelte");
            var heltePattern = helte.GetComponent<HelteBossPatternHost>();
            var helteActor = helte.GetComponent<CombatActorHost>();
            Assert.That(
                heltePattern,
                Is.Not.Null,
                "The dedicated scene must retain the Helte FSM host for isolated development.");
            var sawBasicPattern = false;
            var sawBlinkPattern = false;
            var sawFakeBlinkPattern = false;
            var sawFakeBlinkPause = false;
            var sawCounterPattern = false;
            var sawCounterWindow = false;
            heltePattern.PatternStarted += pattern =>
            {
                sawBasicPattern |= pattern == HeltePattern.BasicCombo;
                sawBlinkPattern |= pattern == HeltePattern.BlinkDash;
                sawFakeBlinkPattern |= pattern == HeltePattern.FakeBlink;
                sawCounterPattern |= pattern == HeltePattern.CounterStance;
            };
            heltePattern.StateChanged += state =>
            {
                sawFakeBlinkPause |= state == HelteCombatState.FakeBlinkPause;
                sawCounterWindow |= state == HelteCombatState.CounterStance ||
                                    state == HelteCombatState.CounterOpen;
            };
            Assert.That(
                bossArena.BossHealthOverride,
                Is.EqualTo(2500),
                "The tutorial boss balance must halve Helte's runtime health at encounter start.");
            Assert.That(
                heltePattern.PhaseTwoHealthRatio,
                Is.EqualTo(0.55f).Within(0.001f),
                "Helte phase two must begin at 55% health.");
            Assert.That(
                heltePattern.FinalRushHealthRatio,
                Is.EqualTo(0.2f).Within(0.001f),
                "Helte's final rush must begin at 20% health.");
            Assert.That(
                heltePattern.FriendlyPatternPrototypeEnabled,
                Is.True,
                "The dedicated boss scene must enable the friendly-pattern prototype before tutorial promotion.");
            Assert.That(
                helte.GetComponent<HeltePatternVfxHost>().HasValidSetup,
                Is.True,
                "The development scene must expose state-driven VFX bindings.");
            var telemetry = FindSceneTransform(scene, "BossArena_Controller")
                .GetComponent<HelteCombatTelemetryHost>();
            Assert.That(telemetry, Is.Not.Null);
            Assert.That(telemetry.HasValidSetup, Is.True);
            Assert.That(telemetry.TargetDurationSeconds, Is.EqualTo(300f));

            FindSceneComponent<TutorialHelteEncounterDialogueHost>(scene).enabled = false;
            var arenaTrigger = FindSceneTransform(scene, "BossArena_StartTrigger")
                .GetComponent<Collider2D>();
            var playerBody = FindSceneTransform(scene, "PlayerRoot")
                .GetComponent<Rigidbody2D>();
            var playerActor = playerBody.GetComponent<CombatActorHost>();
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = new Vector2(
                arenaTrigger.bounds.min.x - 1f,
                arenaTrigger.bounds.center.y);
            Physics2D.SyncTransforms();
            yield return null;

            playerBody.position = new Vector2(
                arenaTrigger.bounds.max.x + 1f,
                arenaTrigger.bounds.center.y);
            Physics2D.SyncTransforms();
            yield return WaitForConditionRealtime(
                () => bossArena.FightStarted,
                1f,
                "A fast player crossing must not skip the boss-arena start trigger.");
            yield return WaitForConditionRealtime(
                () => bossArena.CombatActive,
                bossArena.IntroWarningSeconds + 1f,
                "The boss FSM did not activate after the arena-entry warning.");
            Assert.That(helteActor.Runtime.MaxHealth, Is.EqualTo(2500));
            Assert.That(playerActor.Runtime.MaxHealth, Is.EqualTo(500));

            playerActor.SetScriptedInvulnerability(true);
            yield return WaitForConditionRealtime(
                () => sawBasicPattern && sawBlinkPattern && sawFakeBlinkPattern && sawFakeBlinkPause,
                12f,
                "The friendly opening did not execute BasicCombo, BlinkDash and the readable FakeBlink pause.");

            playerActor.SetScriptedInvulnerability(false);
            Assert.That(
                playerActor.CombatSystem.TryApplyDamage(
                    playerActor.ActorId,
                    new DamagePacket(helteActor.ActorId, "TEST-THEUS-RESCUE", playerActor.Runtime.CurrentHealth)),
                Is.True,
                "The boss test could not trigger the one-time rescue path.");
            yield return null;
            Assert.That(bossArena.RescueUsed, Is.True);
            Assert.That(playerActor.Runtime.IsAlive, Is.True);
            Assert.That(playerActor.Runtime.CurrentHealth, Is.EqualTo(playerActor.Runtime.MaxHealth));
            playerActor.SetScriptedInvulnerability(true);
            var mercyTargetHealth = Mathf.Max(
                1,
                Mathf.FloorToInt(playerActor.Runtime.MaxHealth * 0.2f));
            Assert.That(
                playerActor.CombatSystem.TryApplyDamage(
                    playerActor.ActorId,
                    new DamagePacket(
                        helteActor.ActorId,
                        "TEST-MERCY-THRESHOLD",
                        playerActor.Runtime.CurrentHealth - mercyTargetHealth)),
                Is.True,
                "The boss test could not lower the player to Helte's mercy threshold.");
            playerActor.SetScriptedInvulnerability(true);
            yield return WaitForConditionRealtime(
                () => heltePattern.CurrentState == HelteCombatState.MercyRetreat,
                4f,
                "Low player health did not trigger Helte's mercy retreat.");
            yield return WaitForConditionRealtime(
                () => heltePattern.CurrentState != HelteCombatState.MercyRetreat &&
                      playerActor.Runtime.CurrentHealth >=
                      Mathf.CeilToInt(playerActor.Runtime.MaxHealth * 0.6f),
                4f,
                "Helte's mercy retreat did not restore the player to the configured recovery floor.");

            var phaseTwoDamage = Mathf.Max(
                1,
                helteActor.Runtime.CurrentHealth - Mathf.CeilToInt(
                    helteActor.Runtime.MaxHealth * heltePattern.PhaseTwoHealthRatio) + 1);
            Assert.That(
                helteActor.CombatSystem.TryApplyDamage(
                    helteActor.ActorId,
                    new DamagePacket(playerActor.ActorId, "TEST-PHASE-TWO", phaseTwoDamage)),
                Is.True);
            yield return WaitForConditionRealtime(
                () => heltePattern.CurrentState == HelteCombatState.PhaseTransition,
                5f,
                "Helte did not enter the protected phase-two transition.");
            yield return WaitForConditionRealtime(
                () => heltePattern.CurrentState != HelteCombatState.PhaseTransition &&
                      !helteActor.Runtime.IsInvincible,
                5f,
                "Helte remained invincible after the phase-two transition.");
            yield return WaitForConditionRealtime(
                () => sawCounterPattern && sawCounterWindow &&
                      !helteActor.Runtime.IsInvincible,
                15f,
                "The friendly phase-two loop did not reach its counter stance and readable response window.");

            helteActor.SetScriptedInvulnerability(false);
            var finalRushDamage = Mathf.Max(
                1,
                helteActor.Runtime.CurrentHealth - Mathf.CeilToInt(
                    helteActor.Runtime.MaxHealth * heltePattern.FinalRushHealthRatio) + 1);
            Assert.That(
                helteActor.CombatSystem.TryApplyDamage(
                    helteActor.ActorId,
                    new DamagePacket(playerActor.ActorId, "TEST-FINAL-RUSH", finalRushDamage)),
                Is.True,
                "The boss test could not lower Helte to the final-rush threshold after the counter window.");
            yield return WaitForConditionRealtime(
                () => heltePattern.CurrentState == HelteCombatState.FinalRushTransition,
                5f,
                "Helte did not enter the protected final-rush transition.");
            yield return WaitForConditionRealtime(
                () => heltePattern.CurrentState != HelteCombatState.FinalRushTransition &&
                      !helteActor.Runtime.IsInvincible,
                5f,
                "Helte remained invincible after the final-rush transition.");

            Assert.That(
                helteActor.CombatSystem.TryApplyDamage(
                    helteActor.ActorId,
                    new DamagePacket(playerActor.ActorId, "TEST-BOSS-COMPLETE", helteActor.Runtime.CurrentHealth)),
                Is.True);
            yield return WaitForConditionRealtime(
                () => bossArena.FightCompleted && !bossArena.CombatActive,
                2f,
                "Defeating Helte did not complete and close the boss encounter.");
            Assert.That(telemetry.IsTiming, Is.False);
            Assert.That(telemetry.LastCompletedDurationSeconds, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator TutorialScene_LoadsAndStartsTheOpeningFlow()
        {
            var loadOperation = SceneManager.LoadSceneAsync("TutorialScene", LoadSceneMode.Single);
            Assert.That(loadOperation, Is.Not.Null, "TutorialScene must be present in Build Settings.");
            while (!loadOperation.isDone) yield return null;

            var tutorialScene = SceneManager.GetActiveScene();
            Assert.That(tutorialScene.name, Is.EqualTo("TutorialScene"));

            var introFlow = FindSceneComponent<TutorialChapter0IntroFlowHost>(tutorialScene);
            var dialogue = FindSceneComponent<TutorialDialoguePresenter>(tutorialScene);
            var playerInput = FindSceneComponent<PlayerInputHost>(tutorialScene);
            var resetManager = FindSceneComponent<DevelopmentProgressResetManager>(tutorialScene);

            Assert.That(introFlow, Is.Not.Null);
            Assert.That(introFlow.enabled, Is.True);
            Assert.That(introFlow.HasValidSetup, Is.True);
            Assert.That(introFlow.HasValidUpdraftSetup, Is.True);
            Assert.That(introFlow.HasCoherentHiddenRoomLayout, Is.True);
            Assert.That(dialogue, Is.Not.Null);
            Assert.That(dialogue.enabled, Is.True);
            Assert.That(playerInput, Is.Not.Null);
            Assert.That(playerInput.enabled, Is.True);
            Assert.That(playerInput.UsesCSharpEvents, Is.True);
            Assert.That(resetManager, Is.Not.Null);
            Assert.That(resetManager.HasValidSetup, Is.True);

            var transparentHudBackgrounds = new[]
            {
                "TutorialObjectivePanel",
                "TutorialResultOverlay",
                "ModuleTreePanel",
                "TutorialDialoguePanel",
                "DialogueSpeakerLeft",
                "DialogueSpeakerRight",
                "InventoryPanel",
                "TutorialIntroductionCard",
                "TutorialInteractionPromptPanel",
                "TutorialLoreSubtitlePanel",
                "BossHealthBarPanel",
                "HiddenRoomGlideInstruction",
                "TutorialObjectiveDivider",
                "AccentBar"
            };
            foreach (var backgroundName in transparentHudBackgrounds)
            {
                var image = FindSceneTransform(tutorialScene, backgroundName).GetComponent<Image>();
                Assert.That(image, Is.Not.Null, $"{backgroundName} must retain its UI Image contract.");
                Assert.That(image.color.a, Is.Zero.Within(0.001f),
                    $"{backgroundName} must not restore a hologram background at runtime.");
            }

            var transitions = Resources.FindObjectsOfTypeAll<TutorialZoneTransitionHost>()
                .Where(candidate => candidate != null && candidate.gameObject.scene == tutorialScene)
                .ToArray();
            Assert.That(transitions, Is.Not.Empty);
            Assert.That(transitions.All(candidate => candidate.HasValidSetup), Is.True);
            Assert.That(transitions.All(candidate => candidate.UsesSweptPlayerDetection), Is.True);

            for (var frame = 0; frame < 120 && !dialogue.IsShowing; frame++) yield return null;
            Assert.That(dialogue.IsShowing, Is.True, "The opening dialogue must become visible after scene startup.");
            Assert.That(dialogue.PendingNarrativeCount, Is.Zero,
                "The opening quest must publish one narrative only; duplicate startup events would replay the dialogue after its card.");
        }

        [UnityTest]
        public IEnumerator DeveloperSectionSkip_JumpsFromFToGAndHelteWithoutCompletingSkippedQuests()
        {
            var loadOperation = SceneManager.LoadSceneAsync("TutorialScene", LoadSceneMode.Single);
            Assert.That(loadOperation, Is.Not.Null);
            while (!loadOperation.isDone) yield return null;

            var tutorialScene = SceneManager.GetActiveScene();
            var skip = FindSceneComponent<TutorialDebugSectionSkipHost>(tutorialScene);
            var questSequence = FindSceneComponent<TutorialQuestSequenceHost>(tutorialScene);
            var saveSystem = FindSceneComponent<SaveSystemHost>(tutorialScene);
            var dialogue = FindSceneComponent<TutorialDialoguePresenter>(tutorialScene);
            var intro = FindSceneComponent<TutorialChapter0IntroFlowHost>(tutorialScene);
            var playerMotor = FindSceneComponent<PlayerMotorHost>(tutorialScene);
            var player = FindSceneTransform(tutorialScene, "PlayerRoot");
            var initialCompletedQuestCount = saveSystem.System.Current.Run.QuestIds?.Count ?? 0;

            Assert.That(skip, Is.Not.Null);
            Assert.That(skip.HasValidSetup, Is.True);
            Assert.That(skip.SectionCount, Is.EqualTo(3));

            Assert.That(skip.JumpToFSection(), Is.True);
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-007-A"));
            Assert.That(FindSceneTransform(tutorialScene, "F스테이지").gameObject.activeInHierarchy, Is.True);
            Assert.That(FindSceneTransform(tutorialScene, "G스테이지").gameObject.activeInHierarchy, Is.False);
            Assert.That(FindSceneTransform(tutorialScene, "선착장").gameObject.activeInHierarchy, Is.False);
            Assert.That(
                GetPrivateField<bool>(FindSceneComponent<TutorialSimultaneousEncounterHost>(tutorialScene), "encounterStarted"),
                Is.True,
                "F enemies and gate logic must start immediately after the skip.");
            Assert.That(dialogue.IsShowing, Is.False);
            Assert.That(intro.enabled, Is.False);
            Assert.That(playerMotor.IsDoubleJumpUnlocked, Is.True,
                "Late-section debug testing must receive the already-taught double-jump ability without saving it.");

            Assert.That(skip.JumpToNextSection(), Is.True);
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-007-B"));
            Assert.That(FindSceneTransform(tutorialScene, "F스테이지").gameObject.activeInHierarchy, Is.False);
            Assert.That(FindSceneTransform(tutorialScene, "G스테이지").gameObject.activeInHierarchy, Is.True);
            Assert.That(FindSceneComponent<TutorialWaveEncounterHost>(tutorialScene).EncounterStarted, Is.True);

            Assert.That(skip.JumpToNextSection(), Is.True);
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-008"));
            Assert.That(FindSceneTransform(tutorialScene, "G스테이지").gameObject.activeInHierarchy, Is.False);
            var gIntegrationAfterTransition =
                FindSceneTransform(tutorialScene, "G_Encounter02_Integration").gameObject;
            Assert.That(gIntegrationAfterTransition.activeInHierarchy, Is.False,
                "G-to-H transition must disable the separate G integration root so invisible gate proxies cannot remain.");
            Assert.That(
                gIntegrationAfterTransition.GetComponentsInChildren<Collider2D>(true)
                    .Where(collider => collider.name.StartsWith("G01_보조출구잠금문"))
                    .All(collider => !collider.enabled && !collider.gameObject.activeInHierarchy),
                Is.True,
                "G auxiliary exit gate proxies must not remain active after arriving in H.");
            Assert.That(FindSceneTransform(tutorialScene, "선착장").gameObject.activeInHierarchy, Is.True);
            Assert.That(FindSceneTransform(tutorialScene, "TutorialHelte").gameObject.activeInHierarchy, Is.True);
            Assert.That(skip.ActiveSectionIndex, Is.EqualTo(2));
            Assert.That(saveSystem.System.Current.Run.QuestIds?.Count ?? 0, Is.EqualTo(initialCompletedQuestCount),
                "Debug skipping must not write skipped quest completion into the save.");
            Assert.That(player.gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator GWindRoute_LiftsPromeThroughAllColumnsAndReachesHNormally()
        {
            var loadOperation = SceneManager.LoadSceneAsync("TutorialScene", LoadSceneMode.Single);
            Assert.That(loadOperation, Is.Not.Null);
            while (!loadOperation.isDone) yield return null;

            var tutorialScene = SceneManager.GetActiveScene();
            var skip = FindSceneComponent<TutorialDebugSectionSkipHost>(tutorialScene);
            var playerMotor = FindSceneComponent<PlayerMotorHost>(tutorialScene);
            var playerInput = FindSceneComponent<PlayerInputHost>(tutorialScene);
            var playerBody = playerInput.GetComponent<Rigidbody2D>();
            var combatSystem = FindSceneComponent<CombatSystemHost>(tutorialScene);
            var questSequence = FindSceneComponent<TutorialQuestSequenceHost>(tutorialScene);
            var dialogue = FindSceneComponent<TutorialDialoguePresenter>(tutorialScene);
            var introductionCard = FindSceneComponent<DialogueIntroductionCardModule>(tutorialScene);

            Assert.That(skip.JumpToFSection(), Is.True);
            yield return null;
            Assert.That(skip.JumpToNextSection(), Is.True);
            yield return null;

            var gExit = FindSceneTransform(tutorialScene, "G01_Exit_ToH");
            var windNames = new[]
            {
                "G02_바람_시작_MARKER",
                "G02_바람_01_MARKER",
                "G02_바람_중간통로_MARKER",
                "G02_바람_02_MARKER"
            };
            foreach (var windName in windNames)
            {
                var wind = FindSceneTransform(tutorialScene, windName);
                var windCollider = wind.GetComponent<Collider2D>();
                Assert.That(windCollider, Is.Not.Null, $"{windName} must have a wind trigger.");
                if (windName == "G02_바람_시작_MARKER")
                {
                    Assert.That(
                        windCollider.bounds.size.y,
                        Is.GreaterThanOrEqualTo(6f),
                        "The entry wind must provide a short, readable lift into G.");
                }
                else if (windName == "G02_바람_중간통로_MARKER")
                {
                    Assert.That(
                        windCollider.bounds.size.y,
                        Is.GreaterThanOrEqualTo(30f),
                        "The middle wind must cover its full vertical passage.");
                }
                else
                {
                    Assert.That(
                        windCollider.bounds.max.y,
                        Is.GreaterThanOrEqualTo(gExit.position.y + 1.5f),
                        $"{windName} must not end before the top of G's authored ascent route.");
                }

                var initialPosition = new Vector2(
                    windCollider.bounds.center.x,
                    windCollider.bounds.min.y + 1.25f);
                MovePlayer(playerBody, initialPosition);
                playerMotor.SetGlideHeld(true);
                var lifted = false;
                for (var frame = 0; frame < 90 && !lifted; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    lifted = playerBody.position.y >= initialPosition.y + 2f;
                }
                playerMotor.SetGlideHeld(false);
                Assert.That(lifted, Is.True, $"{windName} did not lift Prome while Space/glide was held.");
            }

            var encounterB = FindSceneComponent<TutorialWaveEncounterHost>(tutorialScene);
            var enemies = GetPrivateField<CombatActorHost[]>(encounterB, "enemies");
            var enemySpawns = GetPrivateField<Transform[]>(encounterB, "spawnPoints");
            var waveEnemyCounts = GetPrivateField<int[]>(encounterB, "waveEnemyCounts");
            var firstGateCollider = GetPrivateField<Collider2D>(encounterB, "internalGateCollider");
            var firstGateRenderer = GetPrivateField<Renderer>(encounterB, "internalGateRenderer");
            var secondGateCollider = GetPrivateField<Collider2D>(encounterB, "exitGateCollider");
            var secondGateRenderer = GetPrivateField<Renderer>(encounterB, "exitGateRenderer");
            var auxiliaryGateColliders =
                GetPrivateField<Collider2D[]>(encounterB, "additionalExitGateColliders");
            var auxiliaryGateRenderers =
                GetPrivateField<Renderer[]>(encounterB, "additionalExitGateRenderers");
            var gRoot = FindSceneTransform(tutorialScene, "G스테이지");
            var authoredGRenderers = gRoot.GetComponentsInChildren<Renderer>(true);
            var authoredSquare21 = authoredGRenderers.Single(renderer => renderer.name == "Square (21)");
            var authoredSquare40 = authoredGRenderers.Single(renderer => renderer.name == "Square (40)");
            var authoredSquare46 = authoredGRenderers.Single(renderer => renderer.name == "Square (46)");
            var gCollisionRoot = FindSceneTransform(tutorialScene, "G 스테이지 충돌체");
            var square21Collision = gCollisionRoot.GetComponentsInChildren<BoxCollider2D>(true)
                .Single(collider => collider.name.EndsWith("_Square (21)", StringComparison.Ordinal));
            Assert.That(
                FindSceneTransformOrDefault(tutorialScene, "G01_보조출구잠금문_PROXY"),
                Is.Null,
                "The unnumbered legacy auxiliary gate proxy must not remain in G.");
            Assert.That(
                FindSceneTransformOrDefault(tutorialScene, "G01_보조출구잠금문_01_PROXY"),
                Is.Null,
                "Numbered auxiliary gate proxies must not be rebuilt from authored map geometry.");
            Assert.That(firstGateRenderer.name, Is.EqualTo("Square (39)"));
            Assert.That(secondGateRenderer.name, Is.EqualTo("Square (43)"));
            Assert.That(auxiliaryGateColliders, Is.Empty);
            Assert.That(auxiliaryGateRenderers, Is.Empty);
            Assert.That(authoredSquare40.gameObject.activeSelf, Is.False,
                "Square (40) is an intentionally removed route blocker and must stay inactive.");
            Assert.That(authoredSquare46.gameObject.activeSelf, Is.False,
                "Square (46) is an intentionally removed route blocker and must stay inactive.");
            Assert.That(authoredSquare21.gameObject.activeSelf, Is.True);
            Assert.That(authoredSquare21.enabled, Is.True,
                "Square (21) is the outer map boundary and must remain visible.");
            Assert.That(square21Collision.enabled, Is.True,
                "Square (21) needs a permanent collision proxy so Prome cannot leave the map.");
            Assert.That(
                gCollisionRoot.GetComponentsInChildren<BoxCollider2D>(true)
                    .Any(collider =>
                        collider.name.EndsWith("_Square (40)", StringComparison.Ordinal) ||
                        collider.name.EndsWith("_Square (46)", StringComparison.Ordinal)),
                Is.False,
                "Disabled authored geometry must not be recreated as invisible collision proxies.");
            Assert.That(firstGateRenderer.GetComponents<Collider2D>().All(collider => !collider.enabled), Is.True,
                "Square (39) must not retain an authored collider beside its runtime gate proxy.");
            Assert.That(secondGateRenderer.GetComponents<Collider2D>().All(collider => !collider.enabled), Is.True,
                "Square (43) must not retain an authored collider beside its runtime gate proxy.");
            Assert.That(enemySpawns.Take(2).All(spawn =>
                    spawn.position.x < firstGateRenderer.bounds.min.x),
                Is.True,
                "G wave 1 enemies must spawn before Square (39).");
            Assert.That(enemySpawns.Skip(2).All(spawn =>
                    spawn.position.x > firstGateRenderer.bounds.max.x &&
                    spawn.position.x < secondGateRenderer.bounds.min.x),
                Is.True,
                "G wave 2 enemies must spawn between Square (39) and Square (43).");
            var enemyOffset = 0;
            for (var waveIndex = 0; waveIndex < waveEnemyCounts.Length; waveIndex++)
            {
                var expectedWave = waveIndex;
                yield return WaitForConditionRealtime(
                    () => encounterB.CurrentWaveIndex == expectedWave &&
                          encounterB.ActiveEnemyCount == waveEnemyCounts[expectedWave],
                    3f,
                    $"G wave {expectedWave + 1} did not activate.");
                if (waveIndex == 0)
                    yield return VerifyPursuingEnemyStopsAtAuthoredGate(
                        enemies[enemyOffset],
                        playerBody,
                        firstGateCollider);
                for (var offset = 0; offset < waveEnemyCounts[waveIndex]; offset++)
                    KillActor(combatSystem, enemies[enemyOffset + offset]);
                enemyOffset += waveEnemyCounts[waveIndex];
                if (waveIndex != 0) continue;
                yield return WaitForConditionRealtime(
                    () => encounterB.CurrentWaveIndex == 1 &&
                          encounterB.ActiveEnemyCount == waveEnemyCounts[1],
                    2f,
                    "G did not automatically activate wave 2 after wave 1.");
                Assert.That(firstGateCollider.enabled, Is.False,
                    "Square (39) must open after wave 1 is defeated.");
                Assert.That(firstGateRenderer.enabled, Is.False,
                    "Square (39) visual must disappear after wave 1 is defeated.");
                Assert.That(secondGateCollider.enabled, Is.True,
                    "Square (43) must remain closed until wave 2 is defeated.");
            }

            yield return WaitForConditionRealtime(
                () => encounterB.IsCleared,
                2f,
                "G did not clear after both enemy groups were defeated.");
            Assert.That(secondGateCollider.enabled, Is.False,
                "Square (43) must open after wave 2 is defeated.");
            Assert.That(secondGateRenderer.enabled, Is.False,
                "Square (43) visual must disappear after wave 2 is defeated.");
            Assert.That(authoredSquare21.enabled, Is.True,
                "Clearing G must not hide the Square (21) outer boundary.");
            Assert.That(square21Collision.enabled, Is.True,
                "Clearing G must not disable the Square (21) outer boundary collision.");
            var gExitTransition = FindSceneTransform(tutorialScene, "G01_Exit_ToH")
                .GetComponent<TutorialZoneTransitionHost>();
            gExitTransition.enabled = false;
            var gatePassStart = new Vector2(
                secondGateRenderer.bounds.min.x - 1.5f,
                secondGateRenderer.bounds.min.y + 0.8f);
            MovePlayer(playerBody, gatePassStart);
            playerMotor.SetMovementInput(Vector2.right);
            var openRouteTargetX = authoredSquare46.bounds.max.x + 0.5f;
            var passedRemovedRouteBlockers = false;
            for (var frame = 0; frame < 600 && !passedRemovedRouteBlockers; frame++)
            {
                yield return new WaitForFixedUpdate();
                passedRemovedRouteBlockers = playerBody.position.x > openRouteTargetX;
            }
            playerMotor.SetMovementInput(Vector2.zero);
            var nearbyBlockers = Physics2D.OverlapBoxAll(
                    playerBody.position,
                    new Vector2(3f, 4f),
                    0f)
                .Where(collider => collider != null &&
                                   collider.transform != playerBody.transform &&
                                   !collider.transform.IsChildOf(playerBody.transform))
                .Select(collider =>
                    $"{GetTransformPath(collider.transform)}" +
                    $"[enabled={collider.enabled},active={collider.gameObject.activeInHierarchy}," +
                    $"trigger={collider.isTrigger},center={collider.bounds.center}]")
                .ToArray();
            Assert.That(passedRemovedRouteBlockers, Is.True,
                $"Prome was blocked by a collider rebuilt from disabled Square (40)/(46). " +
                $"Reached X={playerBody.position.x:F2}, required X>{openRouteTargetX:F2}. " +
                $"Nearby colliders: {string.Join("; ", nearbyBlockers)}");

            var playerCollider = playerBody.GetComponent<Collider2D>();
            var originalGravityScale = playerBody.gravityScale;
            playerBody.gravityScale = 0f;
            MovePlayer(
                playerBody,
                new Vector2(
                    authoredSquare21.bounds.min.x - playerCollider.bounds.extents.x - 0.4f,
                    authoredSquare21.bounds.center.y));
            playerMotor.SetMovementInput(Vector2.right);
            for (var frame = 0; frame < 120; frame++)
                yield return new WaitForFixedUpdate();
            playerMotor.SetMovementInput(Vector2.zero);
            Assert.That(
                playerCollider.bounds.max.x,
                Is.LessThanOrEqualTo(authoredSquare21.bounds.min.x + 0.05f),
                "Prome crossed Square (21); the permanent outer map boundary is not blocking movement.");
            playerBody.gravityScale = originalGravityScale;
            gExitTransition.enabled = true;
            yield return WaitForQuest(questSequence, "QST-TUTO-008");
            yield return UseZoneTransition(
                tutorialScene,
                playerBody,
                playerInput,
                "TUTORIAL-ENCOUNTER-B-EXIT");

            Assert.That(FindSceneTransform(tutorialScene, "G스테이지").gameObject.activeInHierarchy, Is.False);
            var gIntegrationAfterNormalTransition =
                FindSceneTransform(tutorialScene, "G_Encounter02_Integration").gameObject;
            Assert.That(gIntegrationAfterNormalTransition.activeInHierarchy, Is.False,
                "Normal G-to-H transition must disable the separate G integration root.");
            Assert.That(
                gIntegrationAfterNormalTransition.GetComponentsInChildren<Collider2D>(true)
                    .Where(collider => collider.name.StartsWith("G01_보조출구잠금문"))
                    .All(collider => !collider.enabled && !collider.gameObject.activeInHierarchy),
                Is.True,
                "Normal G-to-H arrival must not retain an active auxiliary gate proxy.");
            Assert.That(FindSceneTransform(tutorialScene, "선착장").gameObject.activeInHierarchy, Is.True);
            Assert.That(
                Vector2.Distance(
                    playerBody.position,
                    FindSceneTransform(tutorialScene, "H01_Spawn_FromG").position),
                Is.LessThan(0.5f),
                "Normal G-to-H transition must place Prome at H's authored spawn.");
            var dockRenderers = FindSceneTransform(tutorialScene, "선착장")
                .GetComponentsInChildren<Renderer>(true);
            var dockMinX = dockRenderers.Min(renderer => renderer.bounds.min.x);
            var dockMaxX = dockRenderers.Max(renderer => renderer.bounds.max.x);
            Assert.That(playerBody.position.x, Is.InRange(dockMinX + 1f, dockMaxX - 1f),
                "The H spawn reference may exist, but Prome must land inside the authored dock geometry.");
            yield return DismissCurrentPresentation(dialogue, introductionCard, 6f);
            var helteDialogue = FindSceneTransform(tutorialScene, "H01_헬테조우대화_TRIGGER")
                .GetComponent<TutorialHelteEncounterDialogueHost>();
            playerMotor.SetMovementInput(Vector2.right);
            for (var frame = 0; frame < 420 && !helteDialogue.EncounterPresented; frame++)
                yield return new WaitForFixedUpdate();
            playerMotor.SetMovementInput(Vector2.zero);
            Assert.That(helteDialogue.EncounterPresented, Is.True,
                "Prome could not walk from the H spawn to Helte; an invisible collider may block the dock route.");
        }

        private static IEnumerator VerifyPursuingEnemyStopsAtAuthoredGate(
            CombatActorHost enemy,
            Rigidbody2D playerBody,
            Collider2D wallCollider)
        {
            var enemyCollider = enemy.GetComponent<Collider2D>();
            Assert.That(enemyCollider, Is.Not.Null, "G pursuit enemy needs a body collider.");
            var testY = wallCollider.bounds.min.y + Mathf.Min(1.2f, wallCollider.bounds.extents.y);
            enemy.transform.position = new Vector2(
                wallCollider.bounds.min.x - enemyCollider.bounds.extents.x - 0.5f,
                testY);
            MovePlayer(playerBody, new Vector2(wallCollider.bounds.max.x + 3f, testY));
            Physics2D.SyncTransforms();

            for (var frame = 0; frame < 90; frame++)
                yield return new WaitForFixedUpdate();

            Assert.That(enemyCollider.bounds.max.x, Is.LessThanOrEqualTo(wallCollider.bounds.min.x + 0.04f),
                "G pursuit enemy crossed the authored Square (39) gate instead of stopping at it.");
        }

        [UnityTest]
        public IEnumerator Chapter0Intro_ReachesTheTrainingRoomThroughThePasskeyRoute()
        {
            var loadOperation = SceneManager.LoadSceneAsync("TutorialScene", LoadSceneMode.Single);
            Assert.That(loadOperation, Is.Not.Null);
            while (!loadOperation.isDone) yield return null;

            var tutorialScene = SceneManager.GetActiveScene();
            var introFlow = FindSceneComponent<TutorialChapter0IntroFlowHost>(tutorialScene);
            var dialogue = FindSceneComponent<TutorialDialoguePresenter>(tutorialScene);
            var introductionCard = FindSceneComponent<DialogueIntroductionCardModule>(tutorialScene);
            var questSequence = FindSceneComponent<TutorialQuestSequenceHost>(tutorialScene);
            var playerInput = FindSceneComponent<PlayerInputHost>(tutorialScene);
            var playerBody = playerInput.GetComponent<Rigidbody2D>();

            yield return WaitForCondition(() => dialogue.IsShowing, 120, "Opening dialogue did not start.");
            AdvanceDialogue(dialogue, 10);
            Assert.That(introductionCard.IsShowing, Is.True, "Theus introduction card must follow the opening dialogue.");

            SetPrivateField(introductionCard, "promptReady", true);
            introductionCard.TryDismiss();
            yield return new WaitForSecondsRealtime(0.35f);
            yield return WaitForCondition(
                () => !introductionCard.IsShowing,
                120,
                "The introduction card did not finish its close animation.");
            yield return WaitForCondition(
                () => dialogue.IsShowing,
                120,
                $"The departure line did not follow the introduction card. State={introFlow.State}, pending={dialogue.PendingNarrativeCount}.");
            Assert.That(dialogue.PendingNarrativeCount, Is.Zero);

            AdvanceDialogue(dialogue, 1);
            Assert.That(introFlow.State, Is.EqualTo(TutorialChapter0IntroState.SeekHiddenRoom));

            var hiddenRoomEntry = FindSceneTransformOrDefault(
                tutorialScene,
                "A01_HiddenRoomEntryTarget");
            MovePlayer(
                playerBody,
                hiddenRoomEntry != null
                    ? hiddenRoomEntry.position
                    : new Vector2(-37f, 1.5f));
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.HiddenRoomEntryDialogue && dialogue.IsShowing,
                2f,
                "The hidden glide room transition did not complete.");
            var hiddenRoomRoot = GetPrivateField<GameObject>(introFlow, "hiddenRoomRoot");
            var hiddenRoomSpawn = GetPrivateField<Transform>(introFlow, "hiddenRoomSpawn");
            var passkeyTarget = GetPrivateField<Transform>(introFlow, "passkeyTarget");
            var passkeyVisual = GetPrivateField<GameObject>(introFlow, "passkeyVisual");
            var cameraFollow = FindSceneComponent<CameraFollowHost>(tutorialScene);
            Assert.That(hiddenRoomRoot.activeInHierarchy, Is.True);
            Assert.That(hiddenRoomSpawn.IsChildOf(hiddenRoomRoot.transform), Is.True);
            Assert.That(passkeyTarget.IsChildOf(hiddenRoomRoot.transform), Is.True);
            Assert.That(passkeyTarget.position.y, Is.GreaterThan(hiddenRoomSpawn.position.y + 3f));
            Assert.That(passkeyTarget.position.x, Is.LessThan(hiddenRoomSpawn.position.x - 15f),
                "The passkey must sit diagonally above-left of the low room entrance.");
            Assert.That(passkeyVisual.transform.IsChildOf(passkeyTarget), Is.True,
                "The single imported passkey visual must follow the upper passkey marker.");
            Assert.That(Vector2.Distance(passkeyVisual.transform.position, passkeyTarget.position),
                Is.LessThan(0.05f),
                "The passkey visual must not remain at the player entrance.");
            Assert.That(Vector2.Distance(playerBody.position, hiddenRoomSpawn.position), Is.LessThan(0.25f));
            Assert.That(Mathf.Abs(cameraFollow.transform.position.x - hiddenRoomSpawn.position.x), Is.LessThan(0.25f));
            Assert.That(Mathf.Abs(cameraFollow.transform.position.y - hiddenRoomSpawn.position.y), Is.LessThan(0.25f));
            var objectiveBeacon = FindSceneComponent<TutorialObjectiveBeaconHost>(tutorialScene);
            Assert.That(objectiveBeacon.CurrentTarget, Is.EqualTo(passkeyTarget),
                "The overhead arrow must point diagonally toward the passkey as soon as the room is entered.");
            yield return null;
            var beaconVisual = GetPrivateField<GameObject>(objectiveBeacon, "beaconVisual");
            var expectedArrowDirection =
                ((Vector2)(passkeyTarget.position - hiddenRoomSpawn.position)).normalized;
            Assert.That(Vector2.Dot(beaconVisual.transform.right, expectedArrowDirection),
                Is.GreaterThan(0.98f),
                "The overhead arrow must rotate toward both the horizontal and vertical passkey offset.");

            AdvanceDialogue(dialogue, 3);
            Assert.That(introFlow.State, Is.EqualTo(TutorialChapter0IntroState.SeekLedge));
            MovePlayer(
                playerBody,
                GetPrivateField<Transform>(introFlow, "ledgeTarget").position);
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.HiddenRoomBriefing && dialogue.IsShowing,
                1f,
                "The glide briefing did not start at the ledge.");

            AdvanceDialogue(dialogue, 5);
            Assert.That(dialogue.IsShowing, Is.True, "The glide launch line must follow the briefing.");
            AdvanceDialogue(dialogue, 1);
            Assert.That(introFlow.State, Is.EqualTo(TutorialChapter0IntroState.SeekPasskey));

            MovePlayer(
                playerBody,
                GetPrivateField<Transform>(introFlow, "passkeyTarget").position);
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.ReturnToMeeting && introFlow.HasPasskey,
                1f,
                "The airship passkey was not collected.");
            Assert.That(passkeyVisual.activeSelf, Is.False,
                "The passkey visual must disappear immediately after collection.");
            Assert.That(dialogue.IsShowing, Is.True);
            AdvanceDialogue(dialogue, 1);
            yield return WaitForConditionRealtime(
                () => !dialogue.IsShowing,
                1f,
                "The passkey pickup line did not close.");

            MovePlayer(
                playerBody,
                GetPrivateField<Transform>(introFlow, "hiddenRoomReturnTarget").position);
            InvokePrivateMethod(introFlow, "HandleInteractRequested");
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.SeekTrainingExit && dialogue.IsShowing,
                2f,
                "The meeting-room return transition did not complete.");
            AdvanceDialogue(dialogue, 2);

            yield return UseZoneTransition(tutorialScene, playerBody, playerInput, "TUTORIAL-HQ-EXIT");
            yield return WaitForConditionRealtime(
                () => questSequence.CurrentQuestId == "QST-TUTO-004",
                3f,
                "The HQ ladder exit did not advance the tutorial into dash training.");
        }

        [UnityTest]
        public IEnumerator ImportedTrainingRoom_RunsFiveSequentialLessonsWithRetryAndScopeProtection()
        {
#if UNITY_EDITOR
            var loadOperation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                ImportedTutorialScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(loadOperation, Is.Not.Null);
            while (!loadOperation.isDone) yield return null;
#else
            Assert.Ignore("The imported tutorial scene smoke test runs in the Unity Editor.");
            yield break;
#endif

            var tutorialScene = SceneManager.GetActiveScene();
            var serviceRoot = FindSceneComponent<ServiceRoot>(tutorialScene);
            var dialogue = FindSceneComponent<TutorialDialoguePresenter>(tutorialScene);
            var introductionCard = FindSceneComponent<DialogueIntroductionCardModule>(tutorialScene);
            var questSequence = FindSceneComponent<TutorialQuestSequenceHost>(tutorialScene);
            var questManager = FindSceneComponent<QuestManagerHost>(tutorialScene);
            var inputHost = FindSceneComponent<PlayerInputHost>(tutorialScene);
            var playerMotor = inputHost.GetComponent<PlayerMotorHost>();
            var playerBody = inputHost.GetComponent<Rigidbody2D>();
            var playerCollider = inputHost.GetComponent<Collider2D>();
            var trainingSpawn = FindSceneComponent<TutorialTrainingSpawnHost>(tutorialScene);
            var trainingFlow = FindSceneComponent<TutorialImportedTrainingFlowHost>(tutorialScene);
            var jumpTraining = FindSceneComponent<TutorialJumpTrainingHost>(tutorialScene);
            var phaseController = FindSceneComponent<TutorialTrainingPhaseControllerHost>(tutorialScene);
            var actionScopes = FindSceneComponent<TutorialTrainingActionScopeHost>(tutorialScene);
            var meleeAttack = FindSceneComponent<MeleeAttackHost>(tutorialScene);
            var rangedAttack = FindSceneComponent<PlayerRangedAttackHost>(tutorialScene);
            var playerAnimation = inputHost.GetComponentInChildren<CharacterPngAnimationBridge>(true);

            Assert.That(trainingSpawn.HasValidSetup, Is.True);
            Assert.That(trainingFlow.HasValidSetup, Is.True);
            Assert.That(jumpTraining.HasValidSetup, Is.True);
            Assert.That(phaseController.HasValidSetup, Is.True);
            Assert.That(actionScopes.HasValidSetup, Is.True);
            Assert.That(meleeAttack.HasValidSetup, Is.True);
            Assert.That(rangedAttack.HasValidSetup, Is.True);
            Assert.That(playerAnimation, Is.Not.Null,
                "Prome must retain the PNG animation bridge used by the single-attack presentation.");
            Assert.That(playerAnimation.HasAttack01Clip, Is.True,
                "Prome's PNG animator must contain the generated Attack01 sequence.");
            Assert.That(meleeAttack.EffectiveCooldownSeconds, Is.EqualTo(0.5f).Within(0.02f),
                "Prome's melee cooldown must let the 15-frame Attack01 motion finish before another attack.");
            Assert.That(introductionCard.PromptDelay, Is.EqualTo(1f).Within(0.01f));
            Assert.That(playerCollider, Is.Not.Null);

            yield return ReachDashTraining(
                tutorialScene,
                dialogue,
                introductionCard,
                questSequence,
                playerBody);
            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);

            MovePlayer(playerBody, new Vector2(170f, -3.4f));
            var dashFires = GetPrivateField<GameObject[]>(phaseController, "phaseContentRoots")[0]
                .transform
                .GetComponentsInChildren<TutorialTrainingDashFireHost>(true);
            Assert.That(dashFires.Length, Is.EqualTo(3));
            Assert.That(dashFires.All(fire => fire.gameObject.activeInHierarchy), Is.True);
            Assert.That(
                dashFires.All(fire =>
                {
                    var bounds = fire.GetComponent<Renderer>().bounds;
                    return bounds.size.x <= 0.65f && bounds.size.y >= 8f;
                }),
                Is.True,
                "Dash fire pillars must be narrow enough to cross within one dash and tall enough to read as pillars.");
            Assert.That(FindSceneTransform(tutorialScene, "점프훈련").gameObject.activeInHierarchy, Is.False,
                "The imported jump-training circle must stay hidden during dash training.");
            Assert.That(FindSceneTransform(tutorialScene, "더블점프훈련").gameObject.activeInHierarchy, Is.False,
                "The imported double-jump platform must stay hidden during dash training.");
            var doubleJumpPlatformCollider = FindSceneTransform(tutorialScene, "더블점프훈련")
                .GetComponentInChildren<Collider2D>(true);
            Assert.That(doubleJumpPlatformCollider, Is.Not.Null,
                "The imported double-jump platform must own its phase-controlled collider.");
            Assert.That(doubleJumpPlatformCollider.gameObject.activeInHierarchy, Is.False,
                "The double-jump platform collider must stay disabled with its phase root during dash training.");
            var dashLaneBlockers = Resources.FindObjectsOfTypeAll<Collider2D>()
                .Where(collider => collider != null && collider.gameObject.scene == tutorialScene &&
                                   collider.enabled && collider.gameObject.activeInHierarchy &&
                                   !collider.isTrigger &&
                                   collider.transform != playerBody.transform &&
                                   !collider.transform.IsChildOf(playerBody.transform) &&
                                   collider.bounds.max.x > 185.5f && collider.bounds.min.x < 214.5f &&
                                   collider.bounds.max.y > -4.35f && collider.bounds.min.y < -2.5f)
                .ToArray();
            Assert.That(
                dashLaneBlockers,
                Is.Empty,
                "Unexpected physical blockers remain between dash pillars: " +
                string.Join(", ", dashLaneBlockers.Select(blocker => GetTransformPath(blocker.transform))));
            Assert.That(phaseController.CurrentPhaseIndex, Is.EqualTo(0));
            Assert.That(phaseController.ActivePhaseAreaCount, Is.EqualTo(1));
            Assert.That(phaseController.IsExitLocked, Is.True);
            var dashObjective = FindSceneComponent<TutorialTrainingDashObjectiveHost>(tutorialScene);
            Assert.That(dashObjective.HasValidSetup, Is.True);
            Assert.That(dashObjective.RequiredFireCount, Is.EqualTo(3));
            SetPrivateField(playerMotor, "dashEndsAt", Time.time + 1f);
            InvokePrivateMethod(dashFires[0], "HandleContact", playerCollider);
            Assert.That(dashFires[0].gameObject.activeSelf, Is.False,
                "A fire pillar must turn itself off immediately after a valid dash pass.");
            Assert.That(dashObjective.PassedFireCount, Is.EqualTo(1));
            dashObjective.ResetProgress();
            Assert.That(dashFires.All(fire => fire.gameObject.activeSelf), Is.True,
                "Restarting dash progress must reactivate all three fire pillars.");
            playerMotor.ResetTransientInput();

            Assert.That(phaseController.TryRestartCurrentPhase(), Is.True);
            yield return WaitForConditionRealtime(
                () => !phaseController.IsTransitioning,
                2f,
                "Dash phase marker restart did not finish.");
            Assert.That(
                Vector2.Distance(
                    playerBody.position,
                    FindSceneTransform(tutorialScene, "훈련_진입").position),
                Is.LessThan(0.15f),
                "Dash failure must return the player to the dash checkpoint.");

            MovePlayer(playerBody, new Vector2(170f, -3.4f));
            serviceRoot.Events.Publish(new GameplaySignal(
                QuestSignalType.PortalUsed,
                "TRAINING-DASH-FINISH"));
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-004"),
                "Dash finish signals outside the active training room must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Dash").position);
            Assert.That(dashObjective.TryNotifyFirePassed(0), Is.True);
            Assert.That(dashObjective.TryNotifyFirePassed(1), Is.True);
            Assert.That(dashObjective.TryNotifyFirePassed(2), Is.True);
            InvokePrivateMethod(dashObjective, "OnTriggerEnter2D", playerCollider);
            yield return WaitForQuest(questSequence, "QST-TUTO-006");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            yield return WaitForTrainingPhase(phaseController, 1);
            Assert.That(FindSceneTransform(tutorialScene, "더블점프훈련").gameObject.activeInHierarchy, Is.True);
            Assert.That(FindSceneTransform(tutorialScene, "점프훈련").gameObject.activeInHierarchy, Is.False);
            MovePlayer(playerBody, new Vector2(170f, -3.4f));
            serviceRoot.Events.Publish(new GameplaySignal(
                QuestSignalType.PortalUsed,
                "TRAINING-DOUBLE-JUMP-SUMMIT"));
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-006"),
                "Double jump outside its active full-room scope must not count.");
            var doubleJumpFinish = FindSceneTransform(tutorialScene, "훈련_더블점프_끝");
            MovePlayer(playerBody, doubleJumpFinish.position);
            var doubleJumpArrival = doubleJumpFinish.GetComponent<TutorialTrainingArrivalMarkerHost>();
            Assert.That(doubleJumpArrival, Is.Not.Null);
            InvokePrivateMethod(doubleJumpArrival, "OnTriggerEnter2D", playerCollider);
            yield return WaitForQuest(questSequence, "QST-TUTO-002");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            yield return WaitForTrainingPhase(phaseController, 2);
            Assert.That(FindSceneTransform(tutorialScene, "점프훈련").gameObject.activeInHierarchy, Is.True);
            Assert.That(FindSceneTransform(tutorialScene, "더블점프훈련").gameObject.activeInHierarchy, Is.False);
            var jumpProjectile = FindSceneTransform(
                tutorialScene,
                "ART_SLOT_JumpProjectile").gameObject;
            yield return WaitForConditionRealtime(
                () => jumpProjectile.activeInHierarchy,
                2f,
                "The jump lesson projectile did not become visible.");
            Assert.That(jumpTraining.TryRestartJumpSection(playerCollider), Is.True);
            yield return new WaitForFixedUpdate();
            Assert.That(
                Vector2.Distance(
                    playerBody.position,
                    FindSceneTransform(tutorialScene, "훈련_점프_재시작").position),
                Is.LessThan(0.15f),
                "Jump failure must return the player to the jump checkpoint.");
            MovePlayer(playerBody, new Vector2(170f, -3.4f));
            PublishSignals(serviceRoot, QuestSignalType.JumpPerformed, "PLAYER-001", 3);
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-002"));
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Jump").position);
            PublishSignals(serviceRoot, QuestSignalType.JumpPerformed, "PLAYER-001", 3);
            yield return WaitForQuest(questSequence, "QST-TUTO-003");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            yield return WaitForTrainingPhase(phaseController, 3);
            yield return WaitForConditionRealtime(
                () => trainingSpawn.EnemySequenceStarted,
                2f,
                "The melee-training enemy arrival did not start.");
            var tutorialEnemy = GetPrivateField<GameObject>(trainingSpawn, "tutorialEnemy");
            var tutorialEnemyCollider = GetPrivateField<Collider2D>(trainingSpawn, "enemyCollider");
            var tutorialEnemyAttack = GetPrivateField<Behaviour>(trainingSpawn, "enemyAttackBehaviour");
            yield return WaitForConditionRealtime(
                () => tutorialEnemy.activeInHierarchy && tutorialEnemyCollider.enabled,
                2f,
                "The melee-training enemy did not finish its arrival.");
            MovePlayer(playerBody, new Vector2(170f, -3.4f));
            PublishSignals(serviceRoot, QuestSignalType.AttackPerformed, "PLAYER-001", 3);
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-003"),
                "Melee hits before entering their lesson scope must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Attack").position);
            tutorialEnemyAttack.enabled = false;
            tutorialEnemy.GetComponent<CombatActorHost>().ResetRuntime();
            GetPrivateField<CombatActorHost>(meleeAttack, "sourceActor").ResetRuntime();
            var attackHitbox = GetPrivateField<Collider2D>(meleeAttack, "attackHitbox");
            var attackAnchor = GetPrivateField<Transform>(meleeAttack, "attackAnchor");
            var playerSprite = playerAnimation.GetComponentInChildren<SpriteRenderer>(true);
            Assert.That(playerSprite, Is.Not.Null);
            var playerBaseSortingOrder = playerSprite.sortingOrder;
            var attackPoint = (Vector2)attackHitbox.transform.TransformPoint(attackHitbox.offset);
            tutorialEnemy.transform.position = attackPoint;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(tutorialEnemy.activeInHierarchy, Is.True);
            attackHitbox.enabled = true;
            Physics2D.SyncTransforms();
            var overlapResults = new Collider2D[8];
            var overlapFilter = ContactFilter2D.noFilter;
            overlapFilter.useTriggers = true;
            var overlapCount = attackHitbox.Overlap(overlapFilter, overlapResults);
            var acceptedAttackCount = 0;
            meleeAttack.AttackStarted += () => acceptedAttackCount++;
            Assert.That(
                overlapResults.Take(overlapCount).Contains(tutorialEnemyCollider),
                Is.True,
                "The pre-placed melee hitbox does not overlap the training enemy collider.");
            attackHitbox.enabled = false;
            for (var hitIndex = 1; hitIndex <= 3; hitIndex++)
            {
                if (hitIndex == 3)
                    InvokePrivateMethod(inputHost, "UpdateAimDirection", 1f);
                attackPoint = attackHitbox.transform.TransformPoint(attackHitbox.offset);
                tutorialEnemy.transform.position = attackPoint;
                Physics2D.SyncTransforms();
                Assert.That(
                    actionScopes.IsPlayerInsideScope("QST-TUTO-003"),
                    Is.True,
                    $"The player left the melee lesson scope before hit {hitIndex}.");
                Assert.That(
                    tutorialEnemy.GetComponent<CombatActorHost>().Runtime.IsInvincible,
                    Is.False,
                    $"The training enemy was still invincible before hit {hitIndex}.");
                Assert.That(tutorialEnemy.activeInHierarchy, Is.True,
                    $"The training enemy became inactive before hit {hitIndex}.");
                Assert.That(tutorialEnemyCollider.enabled, Is.True,
                    $"The training enemy collider became disabled before hit {hitIndex}.");
                attackHitbox.enabled = true;
                Physics2D.SyncTransforms();
                overlapCount = attackHitbox.Overlap(overlapFilter, overlapResults);
                Assert.That(
                    overlapResults.Take(overlapCount).Contains(tutorialEnemyCollider),
                    Is.True,
                    $"The training enemy left the melee hitbox before hit {hitIndex}. " +
                    $"Hitbox={attackHitbox.bounds}, enemy={tutorialEnemyCollider.bounds}, " +
                    $"anchor={attackHitbox.transform.position}, enemyPosition={tutorialEnemy.transform.position}.");
                attackHitbox.enabled = false;
                InvokePrivateMethod(meleeAttack, "TryAttack");
                yield return null;
                Assert.That(meleeAttack.IsAttackDirectionLocked, Is.True,
                    "The player's facing must stay locked while the single attack animation is readable.");
                Assert.That(playerAnimation.IsSingleAttackMotionPlaying, Is.True,
                    "Each accepted melee input must start one visible Prome attack motion.");
                Assert.That(playerAnimation.IsAttackSortingPriorityActive, Is.True,
                    "Prome's attack motion must temporarily render above level geometry.");
                Assert.That(playerSprite.sortingOrder, Is.GreaterThan(playerBaseSortingOrder),
                    "Prome's attack SpriteRenderer did not move to the foreground sorting order.");
                Assert.That(playerAnimation.PresentedAttackCount, Is.EqualTo(hitIndex),
                    "Prome must present exactly one attack motion per accepted melee input.");
                Assert.That(
                    acceptedAttackCount,
                    Is.EqualTo(hitIndex),
                    $"Melee input {hitIndex} did not produce exactly one accepted attack.");
                Assert.That(
                    tutorialEnemy.GetComponent<CombatActorHost>().Runtime.CurrentHealth,
                    Is.EqualTo(100 - 25 * hitIndex),
                    $"Melee input {hitIndex} did not damage the training enemy.");
                Assert.That(
                    questManager.System.GetConditionProgress(
                        "QST-TUTO-003",
                        "COND-TUTO-003-ATTACK"),
                    Is.EqualTo(hitIndex),
                    $"Successful melee hit {hitIndex} did not update the visible quest count.");
                if (hitIndex < 3)
                {
                    var healthAfterInput = tutorialEnemy.GetComponent<CombatActorHost>().Runtime.CurrentHealth;
                    InvokePrivateMethod(meleeAttack, "TryAttack");
                    yield return null;
                    Assert.That(acceptedAttackCount, Is.EqualTo(hitIndex),
                        "Repeated input during the attack cooldown must not start another attack.");
                    Assert.That(
                        tutorialEnemy.GetComponent<CombatActorHost>().Runtime.CurrentHealth,
                        Is.EqualTo(healthAfterInput),
                        "Repeated input during the attack cooldown must not deal extra damage.");
                    yield return new WaitForSeconds(meleeAttack.EffectiveCooldownSeconds + 0.02f);
                    Assert.That(acceptedAttackCount, Is.EqualTo(hitIndex),
                        "One melee input must not schedule follow-up combo attacks.");
                    Assert.That(
                        tutorialEnemy.GetComponent<CombatActorHost>().Runtime.CurrentHealth,
                        Is.EqualTo(healthAfterInput),
                        "One melee input dealt additional delayed damage without another press.");
                    Assert.That(meleeAttack.IsAttackDirectionLocked, Is.False,
                        "The player facing lock must release before the next independent attack.");
                    Assert.That(playerAnimation.IsAttackSortingPriorityActive, Is.False,
                        "Prome's foreground sorting priority must clear after the attack motion.");
                    Assert.That(playerSprite.sortingOrder, Is.EqualTo(playerBaseSortingOrder),
                        "Prome's SpriteRenderer sorting order was not restored after the attack.");
                }
            }
            InvokePrivateMethod(inputHost, "UpdateAimDirection", -1f);
            Assert.That(attackAnchor.localScale.x, Is.GreaterThan(0f),
                "Changing aim during an attack must not flip the active hit direction.");
            yield return new WaitForSeconds(meleeAttack.EffectiveCooldownSeconds + 0.02f);
            tutorialEnemyCollider.enabled = false;
            InvokePrivateMethod(meleeAttack, "TryAttack");
            Assert.That(acceptedAttackCount, Is.EqualTo(4),
                "The first input after cooldown must start one new independent attack.");
            Assert.That(attackAnchor.localScale.x, Is.LessThan(0f),
                "The next accepted attack must resync its hitbox to the latest facing direction.");
            yield return WaitForQuest(questSequence, "QST-TUTO-005");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            yield return WaitForTrainingPhase(phaseController, 4);
            yield return WaitForConditionRealtime(
                () => trainingFlow.VisibleRangedTargetCount == 3,
                2f,
                "The ranged lesson did not display all three training targets.");
            var rangedDirection = Vector2.zero;
            rangedAttack.RangedAttackStarted += direction => rangedDirection = direction;
            InvokePrivateMethod(inputHost, "UpdateAimDirection", -1f);
            Assert.That(rangedAttack.TryFire(), Is.True);
            Assert.That(rangedDirection.x, Is.LessThan(-0.99f),
                "The ranged projectile must launch toward the player's current facing direction.");
            MovePlayer(playerBody, new Vector2(170f, -3.4f));
            serviceRoot.Events.Publish(new GameplaySignal(
                QuestSignalType.RangedTripleHitPerformed,
                "PLAYER-001"));
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-005"),
                "Ranged completion before entering its lesson scope must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Ranged").position);
            serviceRoot.Events.Publish(new GameplaySignal(
                QuestSignalType.RangedTripleHitPerformed,
                "PLAYER-001"));
            yield return WaitForQuest(questSequence, "QST-TUTO-007");

            Assert.That(phaseController.CurrentPhaseIndex, Is.EqualTo(-1));
            Assert.That(phaseController.ActivePhaseAreaCount, Is.Zero);
            Assert.That(phaseController.IsExitLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator TrainingThroughHelte_CompletesTheTutorialThroughLiveSceneSystems()
        {
#if UNITY_EDITOR
            var loadOperation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                ImportedTutorialScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(loadOperation, Is.Not.Null);
            while (!loadOperation.isDone) yield return null;
#else
            Assert.Ignore("The imported tutorial scene integration test runs in the Unity Editor.");
            yield break;
#endif

            var tutorialScene = SceneManager.GetActiveScene();
            var serviceRoot = FindSceneComponent<ServiceRoot>(tutorialScene);
            var dialogue = FindSceneComponent<TutorialDialoguePresenter>(tutorialScene);
            var introductionCard = FindSceneComponent<DialogueIntroductionCardModule>(tutorialScene);
            var questSequence = FindSceneComponent<TutorialQuestSequenceHost>(tutorialScene);
            var inputHost = FindSceneComponent<PlayerInputHost>(tutorialScene);
            var playerBody = inputHost.GetComponent<Rigidbody2D>();
            var playerCollider = inputHost.GetComponent<Collider2D>();
            var trainingSpawn = FindSceneComponent<TutorialTrainingSpawnHost>(tutorialScene);
            var jumpTraining = FindSceneComponent<TutorialJumpTrainingHost>(tutorialScene);
            var phaseController = FindSceneComponent<TutorialTrainingPhaseControllerHost>(tutorialScene);
            var emergencyTransition = FindSceneComponent<TutorialEmergencyZoneTransitionHost>(tutorialScene);
            var meetingArrival = FindSceneComponent<TutorialEmergencyMeetingArrivalHost>(tutorialScene);
            var invasionView = FindSceneComponent<TutorialExteriorInvasionViewHost>(tutorialScene);
            var encounterA = FindSceneTransform(tutorialScene, "F01_EncounterController")
                .GetComponent<TutorialSimultaneousEncounterHost>();
            var encounterB = FindSceneTransform(tutorialScene, "G01_EncounterController")
                .GetComponent<TutorialWaveEncounterHost>();
            var encounterBPhaseTrigger = FindSceneTransform(tutorialScene, "G01_후반부진입_TRIGGER")
                .GetComponent<TutorialEncounterPhaseTriggerHost>();
            var combatSystem = FindSceneComponent<CombatSystemHost>(tutorialScene);
            var bossEncounter = FindSceneComponent<TutorialBossEncounterHost>(tutorialScene);
            var bossArena = FindSceneTransform(tutorialScene, "BossArena_Controller")
                .GetComponent<TutorialBossArenaHost>();
            var helteDialogue = FindSceneTransform(tutorialScene, "H01_헬테조우대화_TRIGGER")
                .GetComponent<TutorialHelteEncounterDialogueHost>();
            var bossHealth = FindSceneComponent<BossHealthBarPresenter>(tutorialScene);
            var completionFlow = FindSceneComponent<TutorialCompletionFlowHost>(tutorialScene);
            var saveSystem = FindSceneComponent<SaveSystemHost>(tutorialScene);
            var stageCaption = FindSceneTransform(tutorialScene, "TutorialStageCaptionText")
                .GetComponent<Text>();
            var cameraFollow = FindSceneComponent<CameraFollowHost>(tutorialScene);
            var statusPresenter = FindSceneComponent<TutorialStatusPresenter>(tutorialScene);
            var c03ExteriorTransition = FindSceneTransform(tutorialScene, "C03_Exit_ExteriorSide")
                .GetComponent<TutorialZoneTransitionHost>();
            var exteriorToFTransition = FindSceneTransform(tutorialScene, "E01_Exit_ToF")
                .GetComponent<TutorialZoneTransitionHost>();

            Assert.That(serviceRoot, Is.Not.Null);
            Assert.That(playerCollider, Is.Not.Null);
            Assert.That(trainingSpawn.HasValidSetup, Is.True);
            Assert.That(jumpTraining.HasValidSetup, Is.True);
            Assert.That(phaseController.HasValidSetup, Is.True);
            Assert.That(emergencyTransition.HasValidSetup, Is.True);
            Assert.That(meetingArrival.HasValidSetup, Is.True);
            Assert.That(invasionView.HasValidSetup, Is.True);
            Assert.That(encounterA.HasValidSetup, Is.True);
            Assert.That(encounterA.ActivatesAllEnemiesAtOnce, Is.True);
            Assert.That(encounterB.HasValidSetup, Is.True);
            Assert.That(encounterB.RequiresTraversalForNextWave, Is.True);
            Assert.That(encounterBPhaseTrigger.HasValidSetup, Is.True);
            Assert.That(bossEncounter.HasValidSetup, Is.True);
            Assert.That(bossArena.HasValidSetup, Is.True);
            Assert.That(
                FindSceneTransformOrDefault(tutorialScene, "BossArena_EntryGate_ART_SLOT"),
                Is.Null,
                "The obsolete Helte entry gate must not reappear and block the dock approach.");
            Assert.That(helteDialogue.HasValidSetup, Is.True);
            Assert.That(bossHealth.HasValidSetup, Is.True);
            Assert.That(completionFlow.HasValidSetup, Is.True);
            Assert.That(stageCaption, Is.Not.Null);
            Assert.That(cameraFollow, Is.Not.Null);
            Assert.That(statusPresenter, Is.Not.Null);
            Assert.That(c03ExteriorTransition.LadderMovesUp, Is.True,
                "The C03 exterior ladder must move Prome upward.");
            Assert.That(exteriorToFTransition.DestinationTracksVertical, Is.True,
                "Entering F must enable vertical camera tracking.");
            Assert.That(cameraFollow.CinematicMovementEnabled, Is.False,
                "Tutorial camera look-ahead, shake, smoothing, and boss framing must remain disabled.");

            yield return ReachDashTraining(
                tutorialScene,
                dialogue,
                introductionCard,
                questSequence,
                playerBody);
            Assert.That(stageCaption.text, Is.EqualTo("복도"));

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            yield return UseZoneTransition(
                tutorialScene,
                playerBody,
                inputHost,
                "TUTORIAL-CORRIDOR-TO-TRAINING");
            Assert.That(stageCaption.text, Is.EqualTo("훈련장"));
            MovePlayer(playerBody, new Vector2(170f, -3.4f));
            var activeDashFires = GetPrivateField<GameObject[]>(phaseController, "phaseContentRoots")[0]
                .transform
                .GetComponentsInChildren<TutorialTrainingDashFireHost>(true);
            Assert.That(activeDashFires.Length, Is.EqualTo(3));
            Assert.That(activeDashFires.All(fire => fire.gameObject.activeInHierarchy), Is.True);
            MovePlayer(playerBody, new Vector2(170f, -3.4f));
            serviceRoot.Events.Publish(new GameplaySignal(
                QuestSignalType.PortalUsed,
                "TRAINING-DASH-FINISH"));
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-004"),
                "Dash arrival outside the marker-authored training room must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Dash").position);
            serviceRoot.Events.Publish(new GameplaySignal(
                QuestSignalType.PortalUsed,
                "TRAINING-DASH-FINISH"));
            yield return WaitForQuest(questSequence, "QST-TUTO-006");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            yield return WaitForTrainingPhase(phaseController, 1);
            MovePlayer(playerBody, new Vector2(170f, -3.4f));
            serviceRoot.Events.Publish(new GameplaySignal(
                QuestSignalType.PortalUsed,
                "TRAINING-DOUBLE-JUMP-SUMMIT"));
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-006"),
                "Double jump outside the marker-authored room must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_DoubleJump").position);
            serviceRoot.Events.Publish(new GameplaySignal(
                QuestSignalType.PortalUsed,
                "TRAINING-DOUBLE-JUMP-SUMMIT"));
            yield return WaitForQuest(questSequence, "QST-TUTO-002");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            yield return WaitForTrainingPhase(phaseController, 2);
            var jumpProjectile = FindSceneTransform(tutorialScene, "ART_SLOT_JumpProjectile").gameObject;
            yield return WaitForConditionRealtime(
                () => jumpProjectile.activeInHierarchy,
                2f,
                "Jump training projectile did not become visible.");
            MovePlayer(playerBody, new Vector2(170f, -3.4f));
            PublishSignals(serviceRoot, QuestSignalType.JumpPerformed, "PLAYER-001", 3);
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-002"),
                "Jump actions outside the authored jump lane must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Jump").position);
            PublishSignals(serviceRoot, QuestSignalType.JumpPerformed, "PLAYER-001", 3);
            yield return WaitForQuest(questSequence, "QST-TUTO-003");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            yield return WaitForTrainingPhase(phaseController, 3);
            yield return WaitForConditionRealtime(
                () => trainingSpawn.EnemySequenceStarted,
                2f,
                "Attack-training enemy arrival did not start.");
            MovePlayer(playerBody, new Vector2(170f, -3.4f));
            PublishSignals(serviceRoot, QuestSignalType.AttackPerformed, "PLAYER-001", 3);
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-003"),
                "Melee hits before entering their lesson scope must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Attack").position);
            PublishSignals(serviceRoot, QuestSignalType.AttackPerformed, "PLAYER-001", 3);
            yield return WaitForQuest(questSequence, "QST-TUTO-005");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            yield return WaitForTrainingPhase(phaseController, 4);
            MovePlayer(playerBody, new Vector2(170f, -3.4f));
            serviceRoot.Events.Publish(new GameplaySignal(
                QuestSignalType.RangedTripleHitPerformed,
                "PLAYER-001"));
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-005"),
                "Ranged completion before entering its lesson scope must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Ranged").position);
            serviceRoot.Events.Publish(new GameplaySignal(
                QuestSignalType.RangedTripleHitPerformed,
                "PLAYER-001"));
            yield return WaitForQuest(questSequence, "QST-TUTO-007");
            Assert.That(phaseController.CurrentPhaseIndex, Is.EqualTo(-1));
            Assert.That(phaseController.IsExitLocked, Is.False);
            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);

            yield return UseEmergencyTransition(
                emergencyTransition,
                playerBody,
                playerCollider,
                inputHost);
            Assert.That(stageCaption.text, Is.EqualTo("복도"));
            yield return UseEmergencyMeetingArrival(
                meetingArrival,
                playerBody,
                playerCollider,
                inputHost,
                dialogue);
            Assert.That(stageCaption.text, Is.EqualTo("회의장"));
            yield return DismissCurrentPresentation(dialogue, introductionCard, 6f);

            yield return UseZoneTransition(
                tutorialScene,
                playerBody,
                inputHost,
                "TUTORIAL-A03-TO-C03");
            Assert.That(stageCaption.text, Is.EqualTo("복도"));
            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            yield return UseZoneTransition(
                tutorialScene,
                playerBody,
                inputHost,
                "TUTORIAL-C03-TO-E01");
            Assert.That(stageCaption.text, Is.EqualTo("본부 외곽"));
            Assert.That(statusPresenter.CurrentProgressId, Is.EqualTo("TUTO_E_01"));
            Assert.That(playerBody.simulated, Is.True,
                "The ladder transition must restore Rigidbody2D simulation after reaching the exterior.");
            Assert.That(
                Vector2.Distance(
                    playerBody.position,
                    FindSceneTransform(tutorialScene, "E01_Spawn_HQExit").position),
                Is.LessThan(0.1f),
                "The ladder transition must finish at the authored exterior spawn.");
            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);

            MovePlayer(playerBody, invasionView.GetComponent<Collider2D>().bounds.center);
            yield return WaitForConditionRealtime(
                () => GetPrivateField<bool>(invasionView, "presented"),
                2f,
                "The exterior invasion camera/subtitle beat did not play.");
            Assert.That(invasionView.PreservesPlayerControl, Is.True);

            yield return UseZoneTransition(
                tutorialScene,
                playerBody,
                inputHost,
                "TUTORIAL-EXTERIOR-TO-ENCOUNTER-A");
            Assert.That(stageCaption.text, Is.EqualTo("본부 외곽 통로"));
            yield return WaitForQuest(questSequence, "QST-TUTO-007-A");
            Assert.That(statusPresenter.CurrentProgressId, Is.EqualTo("TUTO_F_01"));
            Assert.That(cameraFollow.TracksVertical, Is.True,
                "The F camera must follow Prome while rising through the opening wind.");
            var fOpeningWind = FindSceneTransform(tutorialScene, "F01_시작활공바람_MARKER")
                .GetComponent<Collider2D>();
            var fStageRenderers = FindSceneTransform(tutorialScene, "F스테이지")
                .GetComponentsInChildren<Renderer>(true);
            Assert.That(fOpeningWind.bounds.max.y,
                Is.GreaterThanOrEqualTo(fStageRenderers.Max(renderer => renderer.bounds.max.y) - 0.5f),
                "The F opening wind must continue to the top of the authored ascent route.");
            var fCameraTestY = Mathf.Clamp(
                fOpeningWind.bounds.max.y - 0.75f,
                cameraFollow.MinimumY,
                cameraFollow.MaximumY);
            MovePlayer(playerBody, new Vector2(fOpeningWind.bounds.center.x, fCameraTestY));
            cameraFollow.SnapToTarget();
            Assert.That(cameraFollow.transform.position.y, Is.EqualTo(fCameraTestY).Within(0.05f),
                "The F camera must vertically follow Prome during the ascent.");
            var fSpawn = FindSceneTransform(tutorialScene, "F01_Spawn_ExteriorSide");
            var fFallRecovery = FindSceneTransform(tutorialScene, "F01_낙사복귀_MARKER")
                .GetComponent<TutorialFallRestartHost>();
            var restartHost = FindSceneComponent<TutorialRestartHost>(tutorialScene);
            Assert.That(fFallRecovery, Is.Not.Null);
            Assert.That(fFallRecovery.HasValidSetup, Is.True);
            MovePlayer(
                playerBody,
                new Vector2(fFallRecovery.transform.position.x, fFallRecovery.RecoveryHeight - 1f));
            yield return WaitForConditionRealtime(
                () => !restartHost.IsRestarting &&
                      Vector2.Distance(playerBody.position, fSpawn.position) < 0.15f,
                3f,
                "F-stage falling did not restore Prome to the authored stage checkpoint.");
            yield return null;
            MovePlayer(
                playerBody,
                new Vector2(fFallRecovery.transform.position.x, fFallRecovery.RecoveryHeight - 1f));
            yield return WaitForConditionRealtime(
                () => !restartHost.IsRestarting &&
                      Vector2.Distance(playerBody.position, fSpawn.position) < 0.15f,
                3f,
                "F-stage recovery did not remain reusable after a second fall.");
            MovePlayer(playerBody, fSpawn.position);
            Assert.That(GetPrivateField<GameObject>(bossEncounter, "bossRoot").activeSelf, Is.False,
                "Helte must remain hidden until both exterior encounters are cleared.");
            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            yield return WaitForConditionRealtime(
                () => GetPrivateField<CombatActorHost[]>(encounterA, "enemies")
                    .All(enemy => enemy.gameObject.activeInHierarchy),
                3f,
                "Exterior encounter A did not activate all enemies together.");
            var encounterAEnemies = GetPrivateField<CombatActorHost[]>(encounterA, "enemies");
            var fExitGate = GetPrivateField<Collider2D>(encounterA, "exitGateCollider");
            yield return VerifyPursuingEnemyStopsAtAuthoredGate(
                encounterAEnemies[0],
                playerBody,
                fExitGate);
            foreach (var enemy in encounterAEnemies)
                KillActor(combatSystem, enemy);
            yield return WaitForConditionRealtime(() => encounterA.IsCleared, 1f, "Exterior encounter A did not clear.");
            yield return WaitForQuest(questSequence, "QST-TUTO-007-B");

            yield return UseZoneTransition(tutorialScene, playerBody, inputHost, "TUTORIAL-ENCOUNTER-A-EXIT");
            Assert.That(stageCaption.text, Is.EqualTo("나디르 선착장 진입로"));
            Assert.That(statusPresenter.CurrentProgressId, Is.EqualTo("TUTO_G_01"));
            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            var encounterBEnemies = GetPrivateField<CombatActorHost[]>(encounterB, "enemies");
            var waveEnemyCounts = GetPrivateField<int[]>(encounterB, "waveEnemyCounts");
            var enemyOffset = 0;
            for (var waveIndex = 0; waveIndex < waveEnemyCounts.Length; waveIndex++)
            {
                var expectedWave = waveIndex;
                yield return WaitForConditionRealtime(
                    () => encounterB.CurrentWaveIndex == expectedWave && encounterB.ActiveEnemyCount == waveEnemyCounts[expectedWave],
                    3f,
                    $"Exterior encounter B did not spawn wave {expectedWave + 1}.");
                for (var offset = 0; offset < waveEnemyCounts[waveIndex]; offset++)
                    KillActor(combatSystem, encounterBEnemies[enemyOffset + offset]);
                enemyOffset += waveEnemyCounts[waveIndex];
                if (waveIndex == 0)
                {
                    yield return WaitForConditionRealtime(
                        () => encounterB.CurrentWaveIndex == 1 &&
                              encounterB.ActiveEnemyCount == waveEnemyCounts[1],
                        2f,
                        "Exterior encounter B did not automatically activate wave 2 after opening its internal passage.");
                }
            }
            yield return WaitForConditionRealtime(() => encounterB.IsCleared, 1f, "Exterior encounter B did not clear.");
            yield return WaitForQuest(questSequence, "QST-TUTO-008");
            Assert.That(GetPrivateField<GameObject>(bossEncounter, "bossRoot").activeSelf, Is.True,
                "Clearing both exterior encounters must unlock the pre-placed boss root.");

            yield return UseZoneTransition(tutorialScene, playerBody, inputHost, "TUTORIAL-ENCOUNTER-B-EXIT");
            Assert.That(stageCaption.text, Is.EqualTo("나디르 선착장"));
            Assert.That(statusPresenter.CurrentProgressId, Is.EqualTo("TUTO_H_01"));
            yield return DismissCurrentPresentation(dialogue, introductionCard, 6f);
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-008"));
            Assert.That(helteDialogue.isActiveAndEnabled, Is.True);
            MovePlayer(playerBody, helteDialogue.GetComponent<Collider2D>().bounds.center);
            InvokePrivateMethod(helteDialogue, "Update");
            yield return WaitForConditionRealtime(
                () => helteDialogue.EncounterPresented && dialogue.IsShowing,
                2f,
                "Walking toward Helte did not start the encounter conversation.");
            yield return DismissCurrentPresentation(dialogue, introductionCard, 6f);
            var arenaTrigger = GetPrivateField<Collider2D>(bossArena, "arenaStartTrigger");
            MovePlayer(playerBody, arenaTrigger.bounds.center);
            yield return WaitForConditionRealtime(
                () => bossArena.FightStarted && bossArena.CombatActive,
                3f,
                "Helte encounter did not enter active combat after the arena warning.");
            yield return null;
            Assert.That(bossHealth.IsVisible, Is.True, "The boss health bar must be visible during Helte combat.");

            var helte = GetPrivateField<CombatActorHost>(bossArena, "bossActor");
            KillActor(combatSystem, helte);
            var resultOverlay = FindSceneTransform(tutorialScene, "TutorialResultOverlay").gameObject;
            yield return WaitForConditionRealtime(
                () => resultOverlay.activeSelf && saveSystem.System.Current.Permanent.TutorialCompleted,
                2f,
                "Helte defeat did not enter the tutorial result state.");
            yield return null;

            Assert.That(bossArena.FightCompleted, Is.True);
            Assert.That(bossHealth.gameObject.activeInHierarchy && bossHealth.IsVisible, Is.False,
                "The boss health bar must not remain visible over the result overlay.");
            AssertResultHudIsClean(tutorialScene);
            Assert.That(saveSystem.System.Current.Run.CurrentStageId, Is.EqualTo("CHAPTER_01"));
            Assert.That(saveSystem.System.Current.Permanent.BossKillRecords, Contains.Item("BOSS-TUTO-HELTE"));
            Assert.That(serviceRoot.StateMachine.Current, Is.EqualTo(GameState.Result));
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            return Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(candidate => candidate != null && candidate.gameObject.scene == scene);
        }

        private static IEnumerator ReachDashTraining(
            Scene tutorialScene,
            TutorialDialoguePresenter dialogue,
            DialogueIntroductionCardModule introductionCard,
            TutorialQuestSequenceHost questSequence,
            Rigidbody2D playerBody)
        {
            var introFlow = FindSceneComponent<TutorialChapter0IntroFlowHost>(tutorialScene);
            yield return WaitForCondition(() => dialogue.IsShowing, 120, "Opening dialogue did not start.");
            AdvanceDialogue(dialogue, 10);
            SetPrivateField(introductionCard, "promptReady", true);
            introductionCard.TryDismiss();
            yield return WaitForConditionRealtime(
                () => !introductionCard.IsShowing && dialogue.IsShowing,
                1f,
                "The departure dialogue did not follow the Theus introduction card.");
            AdvanceDialogue(dialogue, 1);
            Assert.That(introFlow.State, Is.EqualTo(TutorialChapter0IntroState.SeekHiddenRoom));

            var hiddenRoomEntry = FindSceneTransformOrDefault(
                tutorialScene,
                "A01_HiddenRoomEntryTarget");
            MovePlayer(
                playerBody,
                hiddenRoomEntry != null
                    ? hiddenRoomEntry.position
                    : new Vector2(-37f, 1.5f));
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.HiddenRoomEntryDialogue && dialogue.IsShowing,
                2f,
                "The hidden glide room transition did not complete.");
            AdvanceDialogue(dialogue, 3);
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.SeekLedge,
                3f,
                "The hidden-room entry dialogue did not release the ledge objective.");
            MovePlayer(
                playerBody,
                GetPrivateField<Transform>(introFlow, "ledgeTarget").position);
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.HiddenRoomBriefing && dialogue.IsShowing,
                3f,
                "The hidden-room glide briefing did not start.");
            AdvanceDialogue(dialogue, 6);
            MovePlayer(
                playerBody,
                GetPrivateField<Transform>(introFlow, "passkeyTarget").position);
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.ReturnToMeeting && dialogue.IsShowing,
                1f,
                "The passkey route did not enter its return state.");
            AdvanceDialogue(dialogue, 1);
            yield return WaitForConditionRealtime(
                () => !dialogue.IsShowing,
                1f,
                "The passkey pickup line did not close.");
            MovePlayer(
                playerBody,
                GetPrivateField<Transform>(introFlow, "hiddenRoomReturnTarget").position);
            InvokePrivateMethod(introFlow, "HandleInteractRequested");
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.SeekTrainingExit && dialogue.IsShowing,
                2f,
                "The meeting-room return did not complete.");
            AdvanceDialogue(dialogue, 2);

            var hqExit = FindZoneTransition(tutorialScene, "TUTORIAL-HQ-EXIT").GetComponent<Collider2D>();
            MovePlayer(playerBody, hqExit.bounds.center);
            yield return WaitForConditionRealtime(
                () => questSequence.CurrentQuestId == "QST-TUTO-004",
                3f,
                "The HQ exit did not start dash training.");
        }

        private static IEnumerator DismissCurrentPresentation(
            TutorialDialoguePresenter dialogue,
            DialogueIntroductionCardModule introductionCard,
            float timeoutSeconds)
        {
            var timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;
            while ((dialogue.IsShowing || dialogue.PendingNarrativeCount > 0 || introductionCard.IsShowing) &&
                   Time.realtimeSinceStartup < timeoutAt)
            {
                if (introductionCard.IsShowing)
                {
                    SetPrivateField(introductionCard, "promptReady", true);
                    introductionCard.TryDismiss();
                    yield return new WaitForSecondsRealtime(0.3f);
                }
                else
                {
                    AdvanceDialogue(dialogue, 1);
                    yield return null;
                }
            }

            Assert.That(dialogue.IsShowing, Is.False, "Dialogue or an introduction card did not close before timeout.");
            Assert.That(dialogue.PendingNarrativeCount, Is.Zero, "A queued narrative remained after presentation cleanup.");
            Assert.That(introductionCard.IsShowing, Is.False, "An introduction card remained after presentation cleanup.");
        }

        private static IEnumerator UseEmergencyTransition(
            TutorialEmergencyZoneTransitionHost transition,
            Rigidbody2D playerBody,
            Collider2D playerCollider,
            PlayerInputHost inputHost)
        {
            var destination = GetPrivateField<Transform>(transition, "destinationSpawn");
            var currentZoneRoot = GetPrivateField<GameObject>(transition, "currentZoneRoot");
            var nextZoneRoot = GetPrivateField<GameObject>(transition, "nextZoneRoot");

            Assert.That(transition.enabled && transition.gameObject.activeInHierarchy, Is.True);
            InvokePrivateMethod(transition, "OnTriggerEnter2D", playerCollider);
            yield return WaitForConditionRealtime(
                () => inputHost.enabled && nextZoneRoot.activeInHierarchy && !currentZoneRoot.activeSelf &&
                      Vector2.Distance(playerBody.position, destination.position) < 0.5f,
                4f,
                "The training emergency blackout transition did not reach the reused corridor.");
        }

        private static IEnumerator UseEmergencyMeetingArrival(
            TutorialEmergencyMeetingArrivalHost transition,
            Rigidbody2D playerBody,
            Collider2D playerCollider,
            PlayerInputHost inputHost,
            TutorialDialoguePresenter dialogue)
        {
            var destination = GetPrivateField<Transform>(transition, "meetingSpawn");
            var corridorRoot = GetPrivateField<GameObject>(transition, "corridorRoot");
            var meetingRoot = GetPrivateField<GameObject>(transition, "meetingRoot");

            Assert.That(transition.enabled && transition.gameObject.activeInHierarchy, Is.True);
            InvokePrivateMethod(transition, "TryBegin", playerCollider);
            yield return WaitForConditionRealtime(
                () => inputHost.enabled && meetingRoot.activeInHierarchy && !corridorRoot.activeSelf &&
                      dialogue.IsShowing &&
                      Vector2.Distance(playerBody.position, destination.position) < 0.5f,
                3f,
                "The reused corridor did not return to the emergency meeting dialogue.");
        }

        private static IEnumerator UseZoneTransition(
            Scene scene,
            Rigidbody2D playerBody,
            PlayerInputHost inputHost,
            string portalTargetId)
        {
            var transition = FindZoneTransition(scene, portalTargetId);
            var trigger = transition.GetComponent<Collider2D>();
            var destination = GetPrivateField<Transform>(transition, "destinationSpawn");
            var currentZoneRoot = GetPrivateField<GameObject>(transition, "currentZoneRoot");
            var nextZoneRoot = GetPrivateField<GameObject>(transition, "nextZoneRoot");
            var requiredQuestId = GetPrivateField<string>(transition, "requiredQuestId");
            var questSequence = GetPrivateField<TutorialQuestSequenceHost>(transition, "questSequenceHost");
            var dialogue = GetPrivateField<TutorialDialoguePresenter>(transition, "dialoguePresenter");
            Assert.That(trigger.enabled, Is.True, $"Transition '{portalTargetId}' is disabled.");
            Assert.That(transition.enabled && transition.gameObject.activeInHierarchy, Is.True,
                $"Transition '{portalTargetId}' is not active in the current zone.");
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo(requiredQuestId),
                $"Transition '{portalTargetId}' requires a different quest.");
            Assert.That(dialogue.IsShowing, Is.False,
                $"Transition '{portalTargetId}' is correctly blocked while dialogue is visible.");
            var playerCollider = playerBody.GetComponent<Collider2D>();
            Assert.That(playerCollider, Is.Not.Null);
            MovePlayer(playerBody, trigger.bounds.center);
            yield return new WaitForFixedUpdate();
            InvokePrivateMethod(transition, "TryBeginTransition", playerCollider);
            if (transition.RequiresInteraction)
            {
                yield return null;
                InvokePrivateMethod(transition, "HandleInteractRequested");
            }
            var timeoutAt = Time.realtimeSinceStartup + 5f;
            bool ReachedDestination() =>
                inputHost.enabled && nextZoneRoot.activeInHierarchy && !currentZoneRoot.activeSelf &&
                Mathf.Abs(playerBody.position.x - destination.position.x) < 0.5f;
            while (!ReachedDestination() && Time.realtimeSinceStartup < timeoutAt) yield return null;
            Assert.That(
                ReachedDestination(),
                Is.True,
                $"Transition '{portalTargetId}' did not reach its destination. " +
                $"input={inputHost.enabled}, nextActive={nextZoneRoot.activeInHierarchy}, " +
                $"currentActive={currentZoneRoot.activeSelf}, playerX={playerBody.position.x:F2}, " +
                $"destinationX={destination.position.x:F2}, transitionActive={transition.gameObject.activeInHierarchy}.");
        }

        private static TutorialZoneTransitionHost FindZoneTransition(Scene scene, string portalTargetId)
        {
            var candidates = Resources.FindObjectsOfTypeAll<TutorialZoneTransitionHost>()
                .Where(candidate => candidate != null && candidate.gameObject.scene == scene &&
                                    GetPrivateField<string>(candidate, "portalSignalTargetId") == portalTargetId)
                .ToArray();
            var transition = candidates.FirstOrDefault(candidate =>
                                 candidate.enabled && candidate.gameObject.activeInHierarchy)
                             ?? candidates.FirstOrDefault();
            Assert.That(transition, Is.Not.Null, $"No transition publishes '{portalTargetId}'.");
            return transition;
        }

        private static IEnumerator WaitForQuest(TutorialQuestSequenceHost questSequence, string questId)
        {
            yield return WaitForCondition(
                () => questSequence.CurrentQuestId == questId,
                30,
                $"Expected quest '{questId}', but current quest is '{questSequence.CurrentQuestId}'.");
        }

        private static IEnumerator WaitForTrainingPhase(
            TutorialTrainingPhaseControllerHost phaseController,
            int phaseIndex)
        {
            yield return WaitForConditionRealtime(
                () => phaseController.CurrentPhaseIndex == phaseIndex &&
                      !phaseController.IsTransitioning,
                3f,
                $"Training phase {phaseIndex} did not finish its marker transition.");
        }

        private static void PublishSignals(
            ServiceRoot serviceRoot,
            QuestSignalType signalType,
            string targetId,
            int count)
        {
            for (var index = 0; index < count; index++)
                serviceRoot.Events.Publish(new GameplaySignal(signalType, targetId));
        }

        private static void KillActor(CombatSystemHost combatSystem, CombatActorHost actor)
        {
            Assert.That(actor, Is.Not.Null);
            Assert.That(actor.Runtime, Is.Not.Null, $"Actor '{actor.name}' has no runtime state.");
            Assert.That(
                combatSystem.System.TryApplyDamage(
                    actor.ActorId,
                    new DamagePacket("PLAYER-001", "PLAYMODE-INTEGRATION", actor.Runtime.MaxHealth)),
                Is.True,
                $"Actor '{actor.ActorId}' could not be defeated through CombatSystem.");
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        private static void InvokePrivateMethod(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Method '{methodName}' was not found on {target.GetType().Name}.");
            method.Invoke(target, arguments);
        }

        private static Transform FindSceneTransform(Scene scene, string objectName)
        {
            return Resources.FindObjectsOfTypeAll<Transform>()
                .First(candidate => candidate != null && candidate.gameObject.scene == scene && candidate.name == objectName);
        }

        private static Transform FindSceneTransformAny(Scene scene, params string[] objectNames)
        {
            foreach (var objectName in objectNames)
            {
                var candidate = FindSceneTransformOrDefault(scene, objectName);
                if (candidate != null) return candidate;
            }

            Assert.Fail($"None of the required scene objects were found: {string.Join(", ", objectNames)}");
            return null;
        }

        private static Transform FindSceneTransformOrDefault(Scene scene, string objectName)
        {
            return Resources.FindObjectsOfTypeAll<Transform>()
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.gameObject.scene == scene &&
                    candidate.name == objectName);
        }

        private static void AssertResultHudIsClean(Scene scene)
        {
            var suppressedHudNames = new[]
            {
                "TutorialObjectivePanel", "TutorialObjectiveDivider", "TutorialStatusText",
                "TutorialKeyPromptText", "TutorialInteractionPromptPanel", "PlayerHealthText",
                "EnemyHealthText", "InventoryOpenButton", "TutorialStageCaptionText",
                "TutorialDialoguePanel", "TutorialIntroductionCard", "InventoryPanel",
                "ModuleTreePanel", "TutorialLoreSubtitlePanel", "BossHealthBarPanel"
            };

            foreach (var objectName in suppressedHudNames)
            {
                var hudObject = FindSceneTransform(scene, objectName).gameObject;
                Assert.That(IsVisuallyVisible(hudObject), Is.False,
                    $"HUD object '{objectName}' must not remain visible over the tutorial result overlay.");
            }

            var beaconVisual = FindSceneTransform(scene, "TutorialObjectiveBeacon").Find("Visual");
            Assert.That(beaconVisual, Is.Not.Null);
            Assert.That(IsVisuallyVisible(beaconVisual.gameObject), Is.False,
                "The objective beacon must not remain visible over the tutorial result overlay.");
        }

        private static bool IsVisuallyVisible(GameObject target)
        {
            if (target == null || !target.activeInHierarchy) return false;
            for (var current = target.transform; current != null; current = current.parent)
            {
                var canvasGroup = current.GetComponent<CanvasGroup>();
                if (canvasGroup != null && canvasGroup.alpha <= 0.001f) return false;
            }

            return true;
        }

        private static void MovePlayer(Rigidbody2D playerBody, Vector2 position)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = position;
            playerBody.transform.position = position;
            Physics2D.SyncTransforms();
        }

        private static void AdvanceDialogue(TutorialDialoguePresenter dialogue, int count)
        {
            var showNextLine = typeof(TutorialDialoguePresenter).GetMethod(
                "ShowNextLine",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(showNextLine, Is.Not.Null);
            for (var index = 0; index < count; index++) showNextLine.Invoke(dialogue, null);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static IEnumerator WaitForCondition(Func<bool> condition, int maximumFrames, string failureMessage)
        {
            for (var frame = 0; frame < maximumFrames && !condition(); frame++) yield return null;
            Assert.That(condition(), Is.True, failureMessage);
        }

        private static IEnumerator WaitForConditionRealtime(
            Func<bool> condition,
            float timeoutSeconds,
            string failureMessage)
        {
            var timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < timeoutAt) yield return null;
            Assert.That(condition(), Is.True, failureMessage);
        }

        private static bool HasAncestor(Transform transform, string ancestorName)
        {
            for (var current = transform; current != null; current = current.parent)
                if (current.name == ancestorName) return true;
            return false;
        }

        private static string GetTransformPath(Transform transform)
        {
            var names = new System.Collections.Generic.List<string>();
            for (var current = transform; current != null; current = current.parent)
                names.Add(current.name);
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
