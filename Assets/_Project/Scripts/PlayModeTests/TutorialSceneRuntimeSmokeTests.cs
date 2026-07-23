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

namespace Narthex.PlayModeTests
{
    public sealed class TutorialSceneRuntimeSmokeTests
    {
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
            var playerBody = FindSceneComponent<PlayerInputHost>(tutorialScene).GetComponent<Rigidbody2D>();

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

            MovePlayer(playerBody, new Vector2(-37f, 1.5f));
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.HiddenRoomEntryDialogue && dialogue.IsShowing,
                2f,
                "The hidden glide room transition did not complete.");

            AdvanceDialogue(dialogue, 2);
            Assert.That(introFlow.State, Is.EqualTo(TutorialChapter0IntroState.SeekLedge));
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "LedgeStop").position);
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.HiddenRoomBriefing && dialogue.IsShowing,
                1f,
                "The glide briefing did not start at the ledge.");

            AdvanceDialogue(dialogue, 5);
            Assert.That(dialogue.IsShowing, Is.True, "The glide launch line must follow the briefing.");
            AdvanceDialogue(dialogue, 1);
            Assert.That(introFlow.State, Is.EqualTo(TutorialChapter0IntroState.SeekPasskey));

            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "PasskeyTarget").position);
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.ReturnToMeeting && introFlow.HasPasskey,
                1f,
                "The airship passkey was not collected.");
            Assert.That(dialogue.IsShowing, Is.True);
            AdvanceDialogue(dialogue, 1);

            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "HiddenReturnTarget").position);
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.SeekTrainingExit && dialogue.IsShowing,
                2f,
                "The meeting-room return transition did not complete.");
            AdvanceDialogue(dialogue, 2);

            var hqExit = Resources.FindObjectsOfTypeAll<Collider2D>()
                .First(candidate => candidate != null && candidate.gameObject.scene == tutorialScene &&
                                    candidate.name == "ExitTrigger" && HasAncestor(candidate.transform, "Z01_HQ_Prologue"));
            Assert.That(hqExit.enabled, Is.True);
            MovePlayer(playerBody, hqExit.bounds.center);
            yield return WaitForConditionRealtime(
                () => questSequence.CurrentQuestId == "QST-TUTO-004",
                3f,
                "The HQ ladder exit did not advance the tutorial into dash training.");
        }

        [UnityTest]
        public IEnumerator TrainingThroughHelte_CompletesTheTutorialThroughLiveSceneSystems()
        {
            var loadOperation = SceneManager.LoadSceneAsync("TutorialScene", LoadSceneMode.Single);
            Assert.That(loadOperation, Is.Not.Null);
            while (!loadOperation.isDone) yield return null;

            var tutorialScene = SceneManager.GetActiveScene();
            var serviceRoot = FindSceneComponent<ServiceRoot>(tutorialScene);
            var dialogue = FindSceneComponent<TutorialDialoguePresenter>(tutorialScene);
            var introductionCard = FindSceneComponent<DialogueIntroductionCardModule>(tutorialScene);
            var questSequence = FindSceneComponent<TutorialQuestSequenceHost>(tutorialScene);
            var inputHost = FindSceneComponent<PlayerInputHost>(tutorialScene);
            var playerBody = inputHost.GetComponent<Rigidbody2D>();
            var playerMotor = inputHost.GetComponent<PlayerMotorHost>();
            var trainingSpawn = FindSceneComponent<TutorialTrainingSpawnHost>(tutorialScene);
            var jumpTraining = FindSceneComponent<TutorialJumpTrainingHost>(tutorialScene);
            var moduleSystem = FindSceneComponent<ModuleSystemHost>(tutorialScene);
            var moduleTree = FindSceneComponent<ModuleTreeManagerHost>(tutorialScene);
            var bootsPickup = FindSceneComponent<TutorialBootsPickupHost>(tutorialScene);
            var relay = FindSceneComponent<TutorialRelayHost>(tutorialScene);
            var encounterA = FindSceneComponent<TutorialSequentialEncounterHost>(tutorialScene);
            var encounterB = FindSceneComponent<TutorialWaveEncounterHost>(tutorialScene);
            var combatSystem = FindSceneComponent<CombatSystemHost>(tutorialScene);
            var bossEncounter = FindSceneComponent<TutorialBossEncounterHost>(tutorialScene);
            var bossArena = FindSceneComponent<TutorialBossArenaHost>(tutorialScene);
            var bossHealth = FindSceneComponent<BossHealthBarPresenter>(tutorialScene);
            var completionFlow = FindSceneComponent<TutorialCompletionFlowHost>(tutorialScene);
            var saveSystem = FindSceneComponent<SaveSystemHost>(tutorialScene);

            Assert.That(serviceRoot, Is.Not.Null);
            Assert.That(trainingSpawn.HasValidSetup, Is.True);
            Assert.That(jumpTraining.HasValidSetup, Is.True);
            Assert.That(bootsPickup.HasValidSetup, Is.True);
            Assert.That(relay.HasValidSetup, Is.True);
            Assert.That(encounterA.HasValidSetup, Is.True);
            Assert.That(encounterB.HasValidSetup, Is.True);
            Assert.That(bossEncounter.HasValidSetup, Is.True);
            Assert.That(bossArena.HasValidSetup, Is.True);
            Assert.That(bossHealth.HasValidSetup, Is.True);
            Assert.That(completionFlow.HasValidSetup, Is.True);

            yield return ReachDashTraining(
                tutorialScene,
                dialogue,
                introductionCard,
                questSequence,
                playerBody);

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            yield return WaitForConditionRealtime(
                () => trainingSpawn.FallingSequenceStarted,
                2f,
                "Dash training did not start its falling-object sequence after dialogue closed.");
            var fallingWarnings = Enumerable.Range(1, 3)
                .Select(index => FindSceneTransform(tutorialScene, $"ART_SLOT_FallingWarning_0{index}").gameObject)
                .ToArray();
            yield return WaitForConditionRealtime(
                () => fallingWarnings.Any(warning => warning.activeInHierarchy),
                2f,
                "Dash training must telegraph each falling object's landing lane before the drop.");
            MovePlayer(playerBody, new Vector2(170f, -3.4f));
            PublishSignals(serviceRoot, QuestSignalType.DashPerformed, "PLAYER-001", 3);
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-004"),
                "Dash actions before the authored dash lane must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Dash").position);
            PublishSignals(serviceRoot, QuestSignalType.DashPerformed, "PLAYER-001", 3);
            yield return WaitForQuest(questSequence, "QST-TUTO-002");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            var jumpProjectile = FindSceneTransform(tutorialScene, "ART_SLOT_JumpProjectile").gameObject;
            yield return WaitForConditionRealtime(
                () => jumpProjectile.activeInHierarchy,
                2f,
                "Jump training projectile did not become visible.");
            PublishSignals(serviceRoot, QuestSignalType.JumpPerformed, "PLAYER-001", 3);
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-002"),
                "Jump actions outside the authored jump lane must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Jump").position);
            PublishSignals(serviceRoot, QuestSignalType.JumpPerformed, "PLAYER-001", 3);
            yield return WaitForQuest(questSequence, "QST-TUTO-003");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            yield return WaitForConditionRealtime(
                () => trainingSpawn.EnemySequenceStarted,
                2f,
                "Attack-training enemy arrival did not start.");
            PublishSignals(serviceRoot, QuestSignalType.AttackPerformed, "PLAYER-001", 3);
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-003"),
                "Attack actions outside the authored attack lane must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Attack").position);
            PublishSignals(serviceRoot, QuestSignalType.AttackPerformed, "PLAYER-001", 3);
            yield return WaitForQuest(questSequence, "QST-TUTO-005");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            serviceRoot.Events.Publish(new GameplaySignal(QuestSignalType.ModuleUsed, "MOD-TUTO-001"));
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-005"),
                "Pulse use outside the authored pulse lane must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Pulse").position);
            Assert.That(moduleSystem.System.TryUse("PLAYER-001", "MOD-TUTO-001"), Is.True,
                "The equipped tutorial pulse module must be usable during pulse training.");
            yield return WaitForQuest(questSequence, "QST-TUTO-006");

            yield return UseZoneTransition(
                tutorialScene,
                playerBody,
                inputHost,
                "TUTORIAL-TRAINING-EXIT");
            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            var pickupTrigger = GetPrivateField<Collider2D>(bootsPickup, "pickupTrigger");
            MovePlayer(playerBody, pickupTrigger.bounds.center);
            yield return new WaitForFixedUpdate();
            InvokePrivateMethod(bootsPickup, "TryCollect");
            Assert.That(bootsPickup.IsCollected, Is.True, "Cryon's equipment package was not collected.");
            Assert.That(playerMotor.IsDoubleJumpUnlocked, Is.True);
            moduleTree.System.NotifyTreeOpened("TREE-BASIC-001");
            MovePlayer(playerBody, pickupTrigger.bounds.center + Vector3.up * 5f);
            playerMotor.RequestJump();
            yield return new WaitForFixedUpdate();
            yield return WaitForQuest(questSequence, "QST-TUTO-007");
            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);

            var relayTrigger = GetPrivateField<Collider2D>(relay, "activationTrigger");
            MovePlayer(playerBody, relayTrigger.bounds.center);
            InvokePrivateMethod(relay, "TryActivate");
            Assert.That(relay.State, Is.EqualTo(TutorialRelayState.Player));
            yield return WaitForQuest(questSequence, "QST-TUTO-007-A");
            Assert.That(GetPrivateField<GameObject>(bossEncounter, "bossRoot").activeSelf, Is.False,
                "Helte must remain hidden until both exterior encounters are cleared.");

            yield return UseZoneTransition(tutorialScene, playerBody, inputHost, "TUTORIAL-Z03-EXIT");
            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            yield return WaitForConditionRealtime(
                () => encounterA.EncounterStarted && encounterA.ActiveEnemyIndex == 0,
                3f,
                "Exterior encounter A did not spawn its first enemy.");
            var encounterAEnemies = GetPrivateField<CombatActorHost[]>(encounterA, "enemies");
            for (var index = 0; index < encounterAEnemies.Length; index++)
            {
                var enemyIndex = index;
                yield return WaitForConditionRealtime(
                    () => encounterA.ActiveEnemyIndex == enemyIndex,
                    3f,
                    $"Exterior encounter A did not advance to enemy {enemyIndex + 1}.");
                KillActor(combatSystem, encounterAEnemies[enemyIndex]);
            }
            yield return WaitForConditionRealtime(() => encounterA.IsCleared, 1f, "Exterior encounter A did not clear.");
            yield return WaitForQuest(questSequence, "QST-TUTO-007-B");

            yield return UseZoneTransition(tutorialScene, playerBody, inputHost, "TUTORIAL-ENCOUNTER-A-EXIT");
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
            }
            yield return WaitForConditionRealtime(() => encounterB.IsCleared, 1f, "Exterior encounter B did not clear.");
            yield return WaitForQuest(questSequence, "QST-TUTO-008");
            Assert.That(GetPrivateField<GameObject>(bossEncounter, "bossRoot").activeSelf, Is.True,
                "Clearing both exterior encounters must unlock the pre-placed boss root.");

            yield return UseZoneTransition(tutorialScene, playerBody, inputHost, "TUTORIAL-ENCOUNTER-B-EXIT");
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

            MovePlayer(playerBody, new Vector2(-37f, 1.5f));
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.HiddenRoomEntryDialogue && dialogue.IsShowing,
                2f,
                "The hidden glide room transition did not complete.");
            AdvanceDialogue(dialogue, 2);
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "LedgeStop").position);
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.HiddenRoomBriefing && dialogue.IsShowing,
                1f,
                "The hidden-room glide briefing did not start.");
            AdvanceDialogue(dialogue, 6);
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "PasskeyTarget").position);
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.ReturnToMeeting && dialogue.IsShowing,
                1f,
                "The passkey route did not enter its return state.");
            AdvanceDialogue(dialogue, 1);
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "HiddenReturnTarget").position);
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
            while ((dialogue.IsShowing || dialogue.PendingNarrativeCount > 0) && Time.realtimeSinceStartup < timeoutAt)
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
            InvokePrivateMethod(transition, "TryBeginTransition", playerCollider);
            yield return WaitForConditionRealtime(
                () => inputHost.enabled && nextZoneRoot.activeInHierarchy && !currentZoneRoot.activeSelf &&
                      Mathf.Abs(playerBody.position.x - destination.position.x) < 0.5f,
                3f,
                $"Transition '{portalTargetId}' did not reach its destination.");
        }

        private static TutorialZoneTransitionHost FindZoneTransition(Scene scene, string portalTargetId)
        {
            var transition = Resources.FindObjectsOfTypeAll<TutorialZoneTransitionHost>()
                .FirstOrDefault(candidate => candidate != null && candidate.gameObject.scene == scene &&
                                             GetPrivateField<string>(candidate, "portalSignalTargetId") == portalTargetId);
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
    }
}
