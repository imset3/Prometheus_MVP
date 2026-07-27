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
            Assert.That(introFlow.State, Is.EqualTo(TutorialChapter0IntroState.SeekLedge));
            MovePlayer(
                playerBody,
                FindSceneTransformAny(
                    tutorialScene,
                    "B02_LedgeTarget",
                    "LedgeStop").position);
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
                FindSceneTransformAny(
                    tutorialScene,
                    "B03_PasskeyTarget",
                    "PasskeyTarget").position);
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.ReturnToMeeting && introFlow.HasPasskey,
                1f,
                "The airship passkey was not collected.");
            Assert.That(dialogue.IsShowing, Is.True);
            AdvanceDialogue(dialogue, 1);

            MovePlayer(
                playerBody,
                FindSceneTransformAny(
                    tutorialScene,
                    "B04_HiddenRoomReturnTarget",
                    "HiddenReturnTarget").position);
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
            var playerBody = inputHost.GetComponent<Rigidbody2D>();
            var playerCollider = inputHost.GetComponent<Collider2D>();
            var trainingSpawn = FindSceneComponent<TutorialTrainingSpawnHost>(tutorialScene);
            var jumpTraining = FindSceneComponent<TutorialJumpTrainingHost>(tutorialScene);
            var phaseController = FindSceneComponent<TutorialTrainingPhaseControllerHost>(tutorialScene);
            var actionScopes = FindSceneComponent<TutorialTrainingActionScopeHost>(tutorialScene);
            var meleeAttack = FindSceneComponent<MeleeAttackHost>(tutorialScene);
            var rangedAttack = FindSceneComponent<PlayerRangedAttackHost>(tutorialScene);

            Assert.That(trainingSpawn.HasValidSetup, Is.True);
            Assert.That(jumpTraining.HasValidSetup, Is.True);
            Assert.That(phaseController.HasValidSetup, Is.True);
            Assert.That(actionScopes.HasValidSetup, Is.True);
            Assert.That(meleeAttack.HasValidSetup, Is.True);
            Assert.That(rangedAttack.HasValidSetup, Is.True);
            Assert.That(introductionCard.PromptDelay, Is.EqualTo(1f).Within(0.01f));
            Assert.That(playerCollider, Is.Not.Null);

            yield return ReachDashTraining(
                tutorialScene,
                dialogue,
                introductionCard,
                questSequence,
                playerBody);
            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);

            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Dash").position);
            yield return WaitForConditionRealtime(
                () => trainingSpawn.FallingSequenceStarted,
                2f,
                "Entering the dash lesson did not start the falling-object sequence.");
            var fallingWarnings = Enumerable.Range(1, 3)
                .Select(index => FindSceneTransform(
                    tutorialScene,
                    $"ART_SLOT_FallingWarning_0{index}").gameObject)
                .ToArray();
            yield return WaitForConditionRealtime(
                () => fallingWarnings.Any(warning => warning.activeInHierarchy),
                2f,
                "The dash lesson did not show a falling-object warning.");
            Assert.That(phaseController.CurrentPhaseIndex, Is.EqualTo(0));
            Assert.That(phaseController.ActivePhaseAreaCount, Is.EqualTo(1));
            Assert.That(phaseController.IsExitLocked, Is.True);

            serviceRoot.Events.Publish(new GameplaySignal(
                QuestSignalType.DashPerformed,
                "PLAYER-001"));
            Assert.That(trainingSpawn.TryRestartDashSection(playerCollider), Is.True);
            yield return new WaitForFixedUpdate();
            Assert.That(
                Vector2.Distance(
                    playerBody.position,
                    FindSceneTransform(tutorialScene, "Restart_QST-TUTO-004").position),
                Is.LessThan(0.05f),
                "Dash failure must return the player to the dash checkpoint.");

            MovePlayer(playerBody, new Vector2(170f, -3.4f));
            PublishSignals(serviceRoot, QuestSignalType.DashPerformed, "PLAYER-001", 3);
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-004"),
                "Dash actions outside the active lesson scope must not count after retry.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Dash").position);
            PublishSignals(serviceRoot, QuestSignalType.DashPerformed, "PLAYER-001", 3);
            yield return WaitForQuest(questSequence, "QST-TUTO-002");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
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
                    FindSceneTransform(tutorialScene, "Restart_QST-TUTO-002").position),
                Is.LessThan(0.05f),
                "Jump failure must return the player to the jump checkpoint.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Dash").position);
            PublishSignals(serviceRoot, QuestSignalType.JumpPerformed, "PLAYER-001", 3);
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-002"));
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Jump").position);
            PublishSignals(serviceRoot, QuestSignalType.JumpPerformed, "PLAYER-001", 3);
            yield return WaitForQuest(questSequence, "QST-TUTO-006");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Jump").position);
            serviceRoot.Events.Publish(new GameplaySignal(
                QuestSignalType.DoubleJumpPerformed,
                "PLAYER-001"));
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-006"),
                "Double jump before entering its lesson scope must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_DoubleJump").position);
            serviceRoot.Events.Publish(new GameplaySignal(
                QuestSignalType.DoubleJumpPerformed,
                "PLAYER-001"));
            yield return WaitForQuest(questSequence, "QST-TUTO-003");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
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
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_DoubleJump").position);
            PublishSignals(serviceRoot, QuestSignalType.AttackPerformed, "PLAYER-001", 3);
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-003"),
                "Melee hits before entering their lesson scope must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Attack").position);
            tutorialEnemyAttack.enabled = false;
            tutorialEnemy.GetComponent<CombatActorHost>().ResetRuntime();
            GetPrivateField<CombatActorHost>(meleeAttack, "sourceActor").ResetRuntime();
            var attackHitbox = GetPrivateField<Collider2D>(meleeAttack, "attackHitbox");
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
            Assert.That(
                overlapResults.Take(overlapCount).Contains(tutorialEnemyCollider),
                Is.True,
                "The pre-placed melee hitbox does not overlap the training enemy collider.");
            attackHitbox.enabled = false;
            for (var hitIndex = 1; hitIndex <= 3; hitIndex++)
            {
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
                Assert.That(
                    meleeAttack.CurrentComboStage,
                    Is.EqualTo(hitIndex),
                    $"Melee input {hitIndex} did not advance the combo stage.");
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
                    yield return new WaitForSeconds(0.27f);
            }
            yield return WaitForQuest(questSequence, "QST-TUTO-005");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            Assert.That(phaseController.CurrentPhaseIndex, Is.EqualTo(4));
            var rangedDirection = Vector2.zero;
            rangedAttack.RangedAttackStarted += direction => rangedDirection = direction;
            InvokePrivateMethod(inputHost, "UpdateAimDirection", -1f);
            Assert.That(rangedAttack.TryFire(), Is.True);
            Assert.That(rangedDirection.x, Is.LessThan(-0.99f),
                "The ranged projectile must launch toward the player's current facing direction.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Attack").position);
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
            Assert.That(helteDialogue.HasValidSetup, Is.True);
            Assert.That(bossHealth.HasValidSetup, Is.True);
            Assert.That(completionFlow.HasValidSetup, Is.True);
            Assert.That(stageCaption, Is.Not.Null);
            Assert.That(cameraFollow, Is.Not.Null);
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
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Dash").position);
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
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Dash").position);
            PublishSignals(serviceRoot, QuestSignalType.JumpPerformed, "PLAYER-001", 3);
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-002"),
                "Jump actions outside the authored jump lane must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Jump").position);
            PublishSignals(serviceRoot, QuestSignalType.JumpPerformed, "PLAYER-001", 3);
            yield return WaitForQuest(questSequence, "QST-TUTO-006");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Jump").position);
            serviceRoot.Events.Publish(new GameplaySignal(
                QuestSignalType.DoubleJumpPerformed,
                "PLAYER-001"));
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-006"),
                "Double jump before entering its lesson scope must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_DoubleJump").position);
            serviceRoot.Events.Publish(new GameplaySignal(
                QuestSignalType.DoubleJumpPerformed,
                "PLAYER-001"));
            yield return WaitForQuest(questSequence, "QST-TUTO-003");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            yield return WaitForConditionRealtime(
                () => trainingSpawn.EnemySequenceStarted,
                2f,
                "Attack-training enemy arrival did not start.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_DoubleJump").position);
            PublishSignals(serviceRoot, QuestSignalType.AttackPerformed, "PLAYER-001", 3);
            yield return null;
            Assert.That(questSequence.CurrentQuestId, Is.EqualTo("QST-TUTO-003"),
                "Melee hits before entering their lesson scope must not count.");
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Attack").position);
            PublishSignals(serviceRoot, QuestSignalType.AttackPerformed, "PLAYER-001", 3);
            yield return WaitForQuest(questSequence, "QST-TUTO-005");

            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            MovePlayer(playerBody, FindSceneTransform(tutorialScene, "TrainingScope_Attack").position);
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
            Assert.That(stageCaption.text, Is.EqualTo("외부"));
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
            Assert.That(stageCaption.text, Is.EqualTo("외부 전투 구역 1"));
            yield return WaitForQuest(questSequence, "QST-TUTO-007-A");
            Assert.That(GetPrivateField<GameObject>(bossEncounter, "bossRoot").activeSelf, Is.False,
                "Helte must remain hidden until both exterior encounters are cleared.");
            yield return DismissCurrentPresentation(dialogue, introductionCard, 5f);
            yield return WaitForConditionRealtime(
                () => GetPrivateField<CombatActorHost[]>(encounterA, "enemies")
                    .All(enemy => enemy.gameObject.activeInHierarchy),
                3f,
                "Exterior encounter A did not activate all enemies together.");
            var encounterAEnemies = GetPrivateField<CombatActorHost[]>(encounterA, "enemies");
            foreach (var enemy in encounterAEnemies)
                KillActor(combatSystem, enemy);
            yield return WaitForConditionRealtime(() => encounterA.IsCleared, 1f, "Exterior encounter A did not clear.");
            yield return WaitForQuest(questSequence, "QST-TUTO-007-B");

            yield return UseZoneTransition(tutorialScene, playerBody, inputHost, "TUTORIAL-ENCOUNTER-A-EXIT");
            Assert.That(stageCaption.text, Is.EqualTo("외부 전투 구역 2"));
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
                        () => encounterB.IsWaitingForTraversal,
                        2f,
                        "Exterior encounter B did not open its internal passage after wave 1.");
                    InvokePrivateMethod(encounterBPhaseTrigger, "OnTriggerEnter2D", playerCollider);
                }
            }
            yield return WaitForConditionRealtime(() => encounterB.IsCleared, 1f, "Exterior encounter B did not clear.");
            yield return WaitForQuest(questSequence, "QST-TUTO-008");
            Assert.That(GetPrivateField<GameObject>(bossEncounter, "bossRoot").activeSelf, Is.True,
                "Clearing both exterior encounters must unlock the pre-placed boss root.");

            yield return UseZoneTransition(tutorialScene, playerBody, inputHost, "TUTORIAL-ENCOUNTER-B-EXIT");
            Assert.That(stageCaption.text, Is.EqualTo("선착장"));
            yield return DismissCurrentPresentation(dialogue, introductionCard, 6f);
            MovePlayer(playerBody, helteDialogue.GetComponent<Collider2D>().bounds.center);
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
                FindSceneTransformAny(
                    tutorialScene,
                    "B02_LedgeTarget",
                    "LedgeStop").position);
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.HiddenRoomBriefing && dialogue.IsShowing,
                3f,
                "The hidden-room glide briefing did not start.");
            AdvanceDialogue(dialogue, 6);
            MovePlayer(
                playerBody,
                FindSceneTransformAny(
                    tutorialScene,
                    "B03_PasskeyTarget",
                    "PasskeyTarget").position);
            yield return WaitForConditionRealtime(
                () => introFlow.State == TutorialChapter0IntroState.ReturnToMeeting && dialogue.IsShowing,
                1f,
                "The passkey route did not enter its return state.");
            AdvanceDialogue(dialogue, 1);
            MovePlayer(
                playerBody,
                FindSceneTransformAny(
                    tutorialScene,
                    "B04_HiddenRoomReturnTarget",
                    "HiddenReturnTarget").position);
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
            yield return WaitForConditionRealtime(
                () => inputHost.enabled && nextZoneRoot.activeInHierarchy && !currentZoneRoot.activeSelf &&
                      Mathf.Abs(playerBody.position.x - destination.position.x) < 0.5f,
                5f,
                $"Transition '{portalTargetId}' did not reach its destination.");
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
    }
}
