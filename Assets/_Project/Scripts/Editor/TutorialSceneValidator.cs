using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Content;
using Narthex.Core;
using Narthex.Gameplay;
using Narthex.Presentation;
using Narthex.Save;
using Narthex.SceneFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Tools
{
    public static class TutorialSceneValidator
    {
        [MenuItem("sragon000/Validation/Validate Active Tutorial Scene")]
        public static void ValidateActiveTutorialScene()
        {
            var issues = new List<string>();

            var systems = RequireObject("StageSystems", issues);
            var levelRoot = RequireObject("TutorialLevelRoot", issues);
            var tutorialHudRoot = RequireObject("TutorialHUD", issues);
            RequireTransparentHudBackgrounds(tutorialHudRoot, issues);
            var obsoleteBackgrounds = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(candidate => candidate != null &&
                                    candidate.scene.IsValid() &&
                                    (candidate.name.IndexOf("Backdrop", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     candidate.name.IndexOf("Hologram", System.StringComparison.OrdinalIgnoreCase) >= 0))
                .Select(candidate => candidate.name)
                .ToArray();
            if (obsoleteBackgrounds.Length > 0)
                issues.Add("Tutorial blockout must not contain backdrop or hologram objects: " +
                           string.Join(", ", obsoleteBackgrounds));
            if (levelRoot != null)
            {
                foreach (var renderer in levelRoot.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null) continue;
                        var color = material.HasProperty("_BaseColor")
                            ? material.GetColor("_BaseColor")
                            : material.HasProperty("_Color")
                                ? material.GetColor("_Color")
                                : Color.white;
                        if (Mathf.Approximately(color.r, 1f) &&
                            Mathf.Approximately(color.g, 1f) &&
                            Mathf.Approximately(color.b, 1f) &&
                            Mathf.Approximately(color.a, 1f))
                            continue;
                        issues.Add($"Tutorial blockout renderer '{renderer.name}' must use a pure white material.");
                        break;
                    }
                }
            }
            RequireComponent<ServiceRoot>(systems, issues);
            RequireComponent<CombatSystemHost>(systems, issues);
            RequireComponent<SaveSystemHost>(systems, issues);
            RequireComponent<DevelopmentProgressResetManager>(systems, issues);
            RequireComponent<TutorialRestartHost>(systems, issues);
            var restartHost = systems != null ? systems.GetComponent<TutorialRestartHost>() : null;
            if (restartHost != null && (!restartHost.HasValidSceneRestartSetup || !restartHost.UsesInSceneRestart))
                issues.Add("Tutorial restart must use the in-scene fade/checkpoint flow instead of resetting scene progress.");
            var restartQuestIds = new[]
            {
                "QST-TUTO-001", "QST-TUTO-004", "QST-TUTO-002", "QST-TUTO-003", "QST-TUTO-005",
                "QST-TUTO-006", "QST-TUTO-007", "QST-TUTO-007-A", "QST-TUTO-007-B", "QST-TUTO-008"
            };
            if (restartHost != null)
                foreach (var questId in restartQuestIds)
                    if (!restartHost.HasCheckpointForQuest(questId))
                        issues.Add($"Tutorial restart is missing a checkpoint for {questId}.");
            RequireComponent<TutorialEncounterHost>(systems, issues);
            RequireComponent<TutorialCompletionFlowHost>(systems, issues);
            RequireComponent<QuestManagerHost>(systems, issues);
            RequireComponent<TutorialQuestSequenceHost>(systems, issues);
            RequireComponent<TutorialNarrativeSequenceHost>(systems, issues);
            RequireComponent<TutorialDialoguePresenter>(systems, issues);
            RequireComponent<TutorialChapter0IntroFlowHost>(systems, issues);
            var chapter0Intro = systems != null ? systems.GetComponent<TutorialChapter0IntroFlowHost>() : null;
            if (chapter0Intro != null && !chapter0Intro.HasValidSetup)
                issues.Add("Chapter0 A/B intro flow has missing room, passkey, camera, UI, or save references.");
            if (chapter0Intro != null && !chapter0Intro.HasValidUpdraftSetup)
                issues.Add("Chapter0 updraft must clear the return ledge, reach above the passkey, remain inside the hidden-room camera bounds, and use positive lift speeds.");
            if (chapter0Intro != null &&
                !chapter0Intro.UsesMarkerDrivenUpdraft &&
                (chapter0Intro.UpdraftLiftSpeed < 6.5f || chapter0Intro.UpdraftMaxRiseSpeed < 4.5f))
                issues.Add("Chapter0 hidden-room return updraft must use at least 6.5 lift and 4.5 maximum rise speed.");
            RequireComponent<ModuleSystemHost>(systems, issues);
            RequireComponent<ModuleTreeManagerHost>(systems, issues);
            RequireComponent<RewardExecutorHost>(systems, issues);
            RequireComponent<TutorialAccessibilityHost>(systems, issues);
            var accessibilityHost = systems != null ? systems.GetComponent<TutorialAccessibilityHost>() : null;
            if (accessibilityHost != null && (!accessibilityHost.HasValidSetup ||
                                              !accessibilityHost.UsesTextualCombatSemantics ||
                                              !accessibilityHost.HasFlashTargets ||
                                              accessibilityHost.MotionIntensity > 0.65f ||
                                              accessibilityHost.FlashIntensity > 0.45f ||
                                              accessibilityHost.MinimumSubtitleFontSize < 20))
                issues.Add("Tutorial accessibility profile requires bounded motion/flash, readable subtitles, contrast panels, and textual combat cues.");
            RequireComponent<TutorialBossEncounterHost>(systems, issues);
            RequireComponent<TutorialBossCompletionHost>(systems, issues);
            RequireComponent<Chapter01TransitionHost>(systems, issues);

            var progressResetManager = systems != null ? systems.GetComponent<DevelopmentProgressResetManager>() : null;
            if (progressResetManager != null && !progressResetManager.HasValidSetup)
                issues.Add("DevelopmentProgressResetManager has no valid SaveSystemHost reference.");
            if (restartHost != null && !restartHost.HasConfiguredResetActors)
                issues.Add("TutorialRestartHost has no valid resetActors references.");
            if (restartHost != null && !restartHost.IncludesResetActor("PLAYER-001"))
                issues.Add("TutorialRestartHost must reset PLAYER-001.");
            if (restartHost != null && !restartHost.IncludesResetActor("BOSS-TUTO-HELTE"))
                issues.Add("TutorialRestartHost must reset BOSS-TUTO-HELTE.");
            if (restartHost != null && !restartHost.HasValidCheckpointSetup)
                issues.Add("TutorialRestartHost has no valid relay checkpoint setup.");
            if (restartHost != null && !restartHost.HasValidSceneRestartSetup)
                issues.Add("TutorialRestartHost has no valid in-scene fade, input, motor, boss, or checkpoint references.");
            RequireObject("TutorialRelayCheckpoint", issues);

            var questSequence = systems != null ? systems.GetComponent<TutorialQuestSequenceHost>() : null;
            if (questSequence != null && !questSequence.HasValidSequence)
                issues.Add("TutorialQuestSequenceHost has no valid quest sequence.");
            if (questSequence != null)
            {
                var expectedOrder = new[]
                {
                    "QST-TUTO-001", "QST-TUTO-004", "QST-TUTO-006", "QST-TUTO-002", "QST-TUTO-003",
                    "QST-TUTO-005", "QST-TUTO-007", "QST-TUTO-007-A", "QST-TUTO-007-B", "QST-TUTO-008"
                };
                var sequenceSerialized = new SerializedObject(questSequence);
                var sequenceProperty = sequenceSerialized.FindProperty("questSequence");
                var actualOrder = new string[sequenceProperty.arraySize];
                for (var index = 0; index < sequenceProperty.arraySize; index++)
                {
                    var definition = sequenceProperty.GetArrayElementAtIndex(index).objectReferenceValue as QuestDefinition;
                    actualOrder[index] = definition != null ? definition.StableId : string.Empty;
                }
                if (!actualOrder.SequenceEqual(expectedOrder))
                    issues.Add($"Tutorial quest order must be {string.Join(" -> ", expectedOrder)}.");
            }

            var narrativeSequence = systems != null ? systems.GetComponent<TutorialNarrativeSequenceHost>() : null;
            var dialoguePresenter = systems != null ? systems.GetComponent<TutorialDialoguePresenter>() : null;
            if (narrativeSequence != null)
            {
                if (!narrativeSequence.HasValidSetup || narrativeSequence.BeatCount != 10)
                    issues.Add("TutorialNarrativeSequenceHost must contain all ten tutorial beats.");
                if (narrativeSequence.GetLineCount("QST-TUTO-001") != 10)
                    issues.Add("HQ narrative must contain the ten confirmed Notion A-scene lines; the departure line is published after the introduction card.");
                if (narrativeSequence.GetLineCount("QST-TUTO-008") != 2)
                    issues.Add("Helte's QST-TUTO-008 entry beat must contain only the two dock-approach lines; the encounter lines belong to the dock trigger.");
                if (!narrativeSequence.HasDeferredBeat("QST-TUTO-007-A", "TUTORIAL-EXTERIOR-TO-ENCOUNTER-A") ||
                    !narrativeSequence.HasDeferredBeat("QST-TUTO-007-B", "TUTORIAL-ENCOUNTER-A-EXIT") ||
                    !narrativeSequence.HasDeferredBeat("QST-TUTO-008", "TUTORIAL-ENCOUNTER-B-EXIT"))
                    issues.Add("Zone-entry narrative beats must wait for their matching portal transitions.");
            }
            if (dialoguePresenter != null &&
                !dialoguePresenter.HasIntroductionDefinition("QST-TUTO-008-HELTE", true))
                issues.Add("Helte's introduction card must appear after the dock encounter dialogue, not at the dock entrance.");

            var bossEncounter = systems != null ? systems.GetComponent<TutorialBossEncounterHost>() : null;
            if (bossEncounter != null && !bossEncounter.HasValidSetup)
                issues.Add("TutorialBossEncounterHost has no valid restore setup.");

            var chapterTransition = systems != null ? systems.GetComponent<Chapter01TransitionHost>() : null;
            if (chapterTransition != null && !chapterTransition.HasValidSetup)
                issues.Add("Chapter01TransitionHost has no valid result-button or scene setup.");

            var completionFlow = systems != null ? systems.GetComponent<TutorialCompletionFlowHost>() : null;
            if (completionFlow != null && (!completionFlow.HasValidSetup || completionFlow.GameplayHudObjectCount != 11))
                issues.Add("TutorialCompletionFlowHost must hide all eleven gameplay, dialogue, lore, and boss-health HUD objects on result.");
            var hudCoordinator = systems != null ? systems.GetComponent<TutorialHudStateCoordinator>() : null;
            RequireComponent<TutorialHudStateCoordinator>(systems, issues);
            if (hudCoordinator != null && (!hudCoordinator.HasValidSetup ||
                                           hudCoordinator.DialogueSuppressionCount != 10 ||
                                           hudCoordinator.BossSuppressionCount != 8 ||
                                           hudCoordinator.ResultSuppressionCount != 16))
                issues.Add("Tutorial HUD coordinator requires complete dialogue(10), boss(8), and result(16) suppression groups.");

            var player = RequireObject("PlayerRoot", issues);
            RequireComponent<PlayerInputHost>(player, issues);
            RequireComponent<PlayerMotorHost>(player, issues);
            RequireComponent<CombatActorHost>(player, issues);
            RequireComponent<MeleeAttackHost>(player, issues);
            RequireComponent<CombatVisualMotionHost>(player, issues);
            RequireChild(player, "GroundProbe", issues);
            var attackAnchor = RequireChild(player, "AttackAnchor", issues);
            var pulseHitbox = RequireChild(attackAnchor, "ModulePulseHitbox", issues);
            if (pulseHitbox != null && pulseHitbox.activeInHierarchy)
                issues.Add("Legacy pulse hitbox must remain disabled now that key 2 is assigned to Prome's ranged attack.");

            var playerInput = player != null ? player.GetComponent<PlayerInputHost>() : null;
            if (playerInput != null && !playerInput.UsesCSharpEvents)
                issues.Add("PlayerInputHost must use PlayerInput Invoke C# Events notification behavior.");
            if (playerInput != null && !playerInput.HasAimCamera)
                issues.Add("PlayerInputHost requires the tutorial camera for pointer-facing attacks.");
            if (playerInput != null && !playerInput.HasLookAction)
                issues.Add("PlayerInputHost requires the Look action for gamepad right-stick facing.");

            var meleeAttack = player != null ? player.GetComponent<MeleeAttackHost>() : null;
            if (meleeAttack != null && !meleeAttack.HasValidSetup)
                issues.Add("MeleeAttackHost requires the shared AttackAnchor for pointer-facing attacks.");
            if (meleeAttack != null && !meleeAttack.UsesSingleHitAttacks)
                issues.Add("Prome's basic melee input must produce one independent attack per accepted press.");
            if (meleeAttack != null &&
                (meleeAttack.CooldownSeconds < 0.22f || meleeAttack.DirectionLockSeconds < 0.2f))
                issues.Add("Prome's single melee attack must keep its 0.22 second visual readable before accepting a new attack.");

            var rangedRoot = RequireChild(player, "RangedAttackRoot", issues);
            RequireComponent<BoxCollider2D>(rangedRoot, issues);
            RequireComponent<PlayerRangedAttackHost>(rangedRoot, issues);
            var rangedAttack = rangedRoot != null ? rangedRoot.GetComponent<PlayerRangedAttackHost>() : null;
            RequireChild(rangedRoot, "RangedProjectileVisual_ART_SLOT", issues);
            if (rangedAttack != null && (!rangedAttack.HasValidSetup || !rangedAttack.HasAssignedInput ||
                                         !Mathf.Approximately(rangedAttack.CooldownSeconds, 1.5f)))
                issues.Add("Prome's ranged attack must be bound to key 2 with valid references and a 1.5 second cooldown.");

            var playerMotor = player != null ? player.GetComponent<PlayerMotorHost>() : null;
            if (playerMotor != null && !playerMotor.HasServiceRoot)
                issues.Add("PlayerMotorHost requires a ServiceRoot reference for tutorial signals.");
            if (playerMotor != null &&
                (!Mathf.Approximately(playerMotor.DashCooldownSeconds, 0.5f) ||
                 playerMotor.DashDurationSeconds <= 0f))
                issues.Add("Prome's invulnerable dash must use a positive duration followed by a 0.5 second cooldown.");
            if (playerMotor != null &&
                !Mathf.Approximately(
                    PlayerDashTimingPolicy.ResolveNextAllowedTime(
                        0f,
                        playerMotor.DashDurationSeconds,
                        playerMotor.DashCooldownSeconds),
                    playerMotor.DashDurationSeconds + 0.5f))
                issues.Add("Prome's dash cooldown must begin after the invulnerable dash duration ends.");

            var moduleUseHost = player != null ? player.GetComponent<TutorialModuleUseHost>() : null;
            if (moduleUseHost != null && moduleUseHost.enabled)
                issues.Add("Legacy tutorial pulse input must be disabled while the module system itself remains available.");

            var tutorialGuide = RequireObject("TutorialGuideCompanion", issues);
            RequireComponent<TutorialGuideCompanionHost>(tutorialGuide, issues);
            RequireComponent<TutorialTheusLightFormHost>(tutorialGuide, issues);
            var guideVisual = RequireChild(tutorialGuide, "Visual", issues);
            RequireChild(guideVisual, "ModelSlot", issues);
            RequireChild(guideVisual, "EffectsSlot", issues);
            RequireChild(guideVisual, "AttachmentSlot", issues);
            var theusLightFormRoot = RequireChild(tutorialGuide, "TheusLightFormRoot", issues);
            RequireChild(theusLightFormRoot, "LightCore_ART_SLOT", issues);
            RequireChild(theusLightFormRoot, "LightHalo_ART_SLOT", issues);
            RequireChild(theusLightFormRoot, "LightBeam_ART_SLOT", issues);
            var theusLightForm = tutorialGuide != null
                ? tutorialGuide.GetComponent<TutorialTheusLightFormHost>()
                : null;
            if (theusLightForm != null && !theusLightForm.HasValidSetup)
                issues.Add("TutorialTheusLightFormHost requires normal, light-core, beam, and passkey references.");

            var hqGuideRoute = RequireObject("HQGuideRouteController", issues);
            RequireComponent<TutorialGuideRouteHost>(hqGuideRoute, issues);
            var hqGuideRouteHost = hqGuideRoute != null ? hqGuideRoute.GetComponent<TutorialGuideRouteHost>() : null;
            if (hqGuideRouteHost != null && !hqGuideRouteHost.HasValidSetup)
                issues.Add("TutorialGuideRouteHost has invalid dialogue, companion, quest, or waypoint references.");
            if (hqGuideRouteHost != null && hqGuideRouteHost.enabled)
                issues.Add("Legacy HQ guide route must remain disabled until the hidden-room passkey flow returns to A.");

            var hiddenRoom = RequireObject("숨겨진방", issues);
            RequireObject("AirshipPasskey_ART_SLOT", issues);
            RequireObject("Updraft_ART_SLOT", issues);
            var passkeyTrigger = RequireObject("PasskeyPickupTrigger", issues);
            var ledgeTrigger = RequireObject("B02_LedgeBriefingTrigger", issues);
            var returnTrigger = RequireObject("HiddenRoomReturnTrigger", issues);
            RequireComponent<BoxCollider2D>(passkeyTrigger, issues);
            RequireComponent<BoxCollider2D>(ledgeTrigger, issues);
            RequireComponent<BoxCollider2D>(returnTrigger, issues);
            RequireObject("HiddenRoomSpawn", issues);
            RequireObject("LedgeStop", issues);
            RequireObject("PasskeyTarget", issues);
            RequireObject("HiddenReturnTarget", issues);
            RequireObject("HiddenRoomEntryTrigger", issues);
            RequireObject("TheusWrongWayAlarm_ART_SLOT", issues);

            var objectiveBeacon = RequireObject("TutorialObjectiveBeacon", issues);
            RequireComponent<TutorialObjectiveBeaconHost>(objectiveBeacon, issues);
            var beaconVisual = RequireChild(objectiveBeacon, "Visual", issues);
            RequireChild(beaconVisual, "ModelSlot", issues);
            RequireChild(beaconVisual, "EffectsSlot", issues);
            var beaconHost = objectiveBeacon != null ? objectiveBeacon.GetComponent<TutorialObjectiveBeaconHost>() : null;
            if (beaconHost != null && !beaconHost.HasValidSetup)
                issues.Add("TutorialObjectiveBeaconHost has invalid quest, player, or visual references.");
            if (beaconHost != null && !beaconHost.HasTarget("QST-TUTO-006"))
                issues.Add("TutorialObjectiveBeaconHost must guide the player to the double-jump practice platform.");
            if (beaconHost != null && !beaconHost.HasTarget("QST-TUTO-007"))
                issues.Add("TutorialObjectiveBeaconHost must guide the player to the emergency training-room exit.");

            var emergencyExit = RequireObject("D02_EmergencyExit_To_C02", issues);
            RequireComponent<BoxCollider2D>(emergencyExit, issues);
            RequireComponent<TutorialEmergencyZoneTransitionHost>(emergencyExit, issues);
            var emergencyTransition = emergencyExit != null
                ? emergencyExit.GetComponent<TutorialEmergencyZoneTransitionHost>()
                : null;
            if (emergencyTransition != null &&
                (!emergencyTransition.HasValidSetup || !emergencyTransition.UsesBlackoutRun))
                issues.Add("Training-room emergency exit requires blackout, running audio, C02 spawn, and camera references.");

            var c02MeetingExit = RequireObject("C02_Exit_MeetingSide", issues);
            RequireComponent<BoxCollider2D>(c02MeetingExit, issues);
            RequireComponent<TutorialEmergencyMeetingArrivalHost>(c02MeetingExit, issues);
            var meetingArrival = c02MeetingExit != null
                ? c02MeetingExit.GetComponent<TutorialEmergencyMeetingArrivalHost>()
                : null;
            if (meetingArrival != null &&
                (!meetingArrival.HasValidSetup || !meetingArrival.LocksDepartureUntilDialogue ||
                 meetingArrival.DialogueLineCount != 6))
                issues.Add("C02 meeting-side exit requires a six-line A03 reunion and a dialogue-locked departure.");

            RequireObject("A03_회의장_긴급복귀스폰", issues);
            RequireObject("C03_Spawn_MeetingSide", issues);
            var a03Departure = RequireObject("A03_Exit_To_C03", issues);
            RequireComponent<BoxCollider2D>(a03Departure, issues);
            RequireComponent<TutorialZoneTransitionHost>(a03Departure, issues);
            var a03DepartureHost = a03Departure != null
                ? a03Departure.GetComponent<TutorialZoneTransitionHost>()
                : null;
            if (a03DepartureHost != null && !a03DepartureHost.HasValidSetup)
                issues.Add("A03 meeting departure requires valid C03 spawn, camera, player, and fade references.");

            var c03State = RequireObject("C03_긴급이동_연출", issues);
            RequireObject("C03_LadderEntry", issues);
            RequireObject("C03_LadderExit", issues);
            var c03Lore = c03State != null
                ? c03State.GetComponentsInChildren<TutorialLoreSubtitleTriggerHost>(true)
                : Array.Empty<TutorialLoreSubtitleTriggerHost>();
            var c03Shakes = c03State != null
                ? c03State.GetComponentsInChildren<TutorialCameraShakeTriggerHost>(true)
                : Array.Empty<TutorialCameraShakeTriggerHost>();
            if (c03Lore.Length != 2 || c03Lore.Any(host => host == null || !host.HasValidSetup))
                issues.Add("C03 requires exactly two valid emergency subtitle triggers.");
            if (c03Shakes.Length != 2 || c03Shakes.Any(host => host == null || !host.HasValidSetup))
                issues.Add("C03 requires exactly two valid camera-shake triggers.");

            var c03ExteriorExit = RequireObject("C03_Exit_ExteriorSide", issues);
            RequireComponent<BoxCollider2D>(c03ExteriorExit, issues);
            RequireComponent<TutorialZoneTransitionHost>(c03ExteriorExit, issues);
            var c03ExteriorTransition = c03ExteriorExit != null
                ? c03ExteriorExit.GetComponent<TutorialZoneTransitionHost>()
                : null;
            if (c03ExteriorTransition != null &&
                (!c03ExteriorTransition.HasValidSetup || !c03ExteriorTransition.UsesLadderSequence ||
                 !c03ExteriorTransition.RequiresInteraction || !c03ExteriorTransition.HasValidLadderSetup))
                issues.Add("C03 exterior exit requires F interaction, the ladder sequence, and valid E01 transition references.");

            var exteriorViewAnchor = RequireObject("E01_InvasionViewAnchor", issues);
            RequireComponent<BoxCollider2D>(exteriorViewAnchor, issues);
            RequireComponent<TutorialExteriorInvasionViewHost>(exteriorViewAnchor, issues);
            var exteriorView = exteriorViewAnchor != null
                ? exteriorViewAnchor.GetComponent<TutorialExteriorInvasionViewHost>()
                : null;
            if (exteriorView != null &&
                (!exteriorView.HasValidSetup || !exteriorView.PreservesPlayerControl ||
                 exteriorView.SubtitleCount != 2))
                issues.Add("E01 invasion view requires two subtitles and must preserve player control.");

            var exteriorColliders = RequireObject("외부 충돌체", issues);
            if (exteriorColliders != null &&
                exteriorColliders.GetComponentsInChildren<BoxCollider2D>(true).Length == 0)
                issues.Add("E01 requires collision proxies for the level designer's white blockout shapes.");

            var tutorialAudioRoot = RequireObject("TutorialAudioRoot", issues);
            RequireComponent<TutorialSfxCueHost>(tutorialAudioRoot, issues);
            RequireChild(tutorialAudioRoot, "UIAudioSource", issues);
            RequireChild(tutorialAudioRoot, "WorldAudioSource", issues);
            var sfxCueHost = tutorialAudioRoot != null ? tutorialAudioRoot.GetComponent<TutorialSfxCueHost>() : null;
            if (sfxCueHost != null && !sfxCueHost.HasValidSetup)
                issues.Add("TutorialSfxCueHost has invalid ServiceRoot or audio source references.");

            var moduleTreeHost = systems != null ? systems.GetComponent<ModuleTreeManagerHost>() : null;
            if (moduleTreeHost != null && !moduleTreeHost.HasValidSetup)
                issues.Add("ModuleTreeManagerHost has no valid tree setup.");

            var enemy = RequireObject("TutorialEnemy", issues);
            RequireComponent<CombatActorHost>(enemy, issues);
            RequireComponent<EnemyAttackHost>(enemy, issues);
            RequireComponent<CombatVisualMotionHost>(enemy, issues);
            RequireChild(enemy, "EnemyAttackAnchor", issues);

            var trainingSpawnController = RequireObject("TrainingSpawnController", issues);
            RequireComponent<TutorialTrainingSpawnHost>(trainingSpawnController, issues);
            var trainingSpawnHost = trainingSpawnController != null
                ? trainingSpawnController.GetComponent<TutorialTrainingSpawnHost>()
                : null;
            if (trainingSpawnHost != null && !trainingSpawnHost.HasValidSetup)
                issues.Add("TutorialTrainingSpawnHost has invalid falling prop or enemy arrival references.");
            var actionScopes = RequireChild(trainingSpawnController, "TrainingActionScopes", issues);
            RequireComponent<TutorialTrainingActionScopeHost>(actionScopes, issues);
            var actionScopeHost = actionScopes != null
                ? actionScopes.GetComponent<TutorialTrainingActionScopeHost>()
                : null;
            if (actionScopeHost != null && !actionScopeHost.HasValidSetup)
                issues.Add("TutorialTrainingActionScopeHost requires five valid training-room trigger areas.");
            var scopeNames = new[] { "Dash", "Jump", "DoubleJump", "Attack", "Ranged" };
            for (var scopeIndex = 0; scopeIndex < scopeNames.Length; scopeIndex++)
            {
                var scopeObject = RequireChild(actionScopes, $"TrainingScope_{scopeNames[scopeIndex]}", issues);
                RequireComponent<BoxCollider2D>(scopeObject, issues);
                var scopeCollider = scopeObject != null ? scopeObject.GetComponent<BoxCollider2D>() : null;
                if (scopeCollider != null && !scopeCollider.isTrigger)
                    issues.Add($"TrainingScope_{scopeNames[scopeIndex]} must be a trigger area.");
            }
            var jumpController = RequireObject("JumpProjectileController", issues);
            RequireComponent<TutorialJumpTrainingHost>(jumpController, issues);
            var jumpTrainingHost = jumpController != null ? jumpController.GetComponent<TutorialJumpTrainingHost>() : null;
            if (jumpTrainingHost != null && !jumpTrainingHost.HasValidSetup)
                issues.Add("TutorialJumpTrainingHost has invalid quest, player, anchor, or projectile references.");
            var jumpProjectile = RequireObject("ART_SLOT_JumpProjectile", issues);
            RequireComponent<BoxCollider2D>(jumpProjectile, issues);
            RequireComponent<Rigidbody2D>(jumpProjectile, issues);
            RequireComponent<TutorialJumpProjectileHazardHost>(jumpProjectile, issues);
            var jumpHazard = jumpProjectile != null ? jumpProjectile.GetComponent<TutorialJumpProjectileHazardHost>() : null;
            if (jumpHazard != null && !jumpHazard.HasValidSetup)
                issues.Add("Jump training projectile has no valid trigger or training reference.");
            for (var fallingIndex = 1; fallingIndex <= 3; fallingIndex++)
            {
                RequireObject($"ART_SLOT_FallingWarning_0{fallingIndex}", issues);
                var fallingObject = RequireObject($"ART_SLOT_FallingCrate_0{fallingIndex}", issues);
                RequireComponent<BoxCollider2D>(fallingObject, issues);
                RequireComponent<Rigidbody2D>(fallingObject, issues);
                RequireComponent<TutorialFallingHazardHost>(fallingObject, issues);
                var hazard = fallingObject != null ? fallingObject.GetComponent<TutorialFallingHazardHost>() : null;
                if (hazard != null && !hazard.HasValidSetup)
                    issues.Add($"Falling crate 0{fallingIndex} has no valid armed trigger setup.");
            }

            var equipmentPickup = RequireObject("CryonBootsPickup", issues);
            RequireComponent<TutorialBootsPickupHost>(equipmentPickup, issues);
            RequireChild(equipmentPickup, "EquipmentPackageVisual_ART_SLOT", issues);
            var equipmentPickupHost = equipmentPickup != null ? equipmentPickup.GetComponent<TutorialBootsPickupHost>() : null;
            if (equipmentPickupHost != null && !equipmentPickupHost.HasValidSetup)
                issues.Add("TutorialBootsPickupHost has invalid equipment package, module, or player references.");
            RequireObject("ART_SLOT_Cryon", issues);
            var dashArrivalCondition = AssetDatabase.LoadAssetAtPath<QuestConditionDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Conditions/COND-TUTO-004-DASH.asset");
            if (dashArrivalCondition == null ||
                dashArrivalCondition.SignalType != QuestSignalType.PortalUsed ||
                dashArrivalCondition.TargetId != "TRAINING-DASH-FINISH" ||
                dashArrivalCondition.RequiredAmount != 1)
                issues.Add("QST-TUTO-004 must complete only at the marker-authored dash finish trigger.");
            var doubleJumpCondition = AssetDatabase.LoadAssetAtPath<QuestConditionDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Conditions/COND-TUTO-006-DOUBLE-JUMP.asset");
            var equipmentQuest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Quests/QST-TUTO-006.asset");
            if (doubleJumpCondition == null || doubleJumpCondition.SignalType != QuestSignalType.PortalUsed ||
                doubleJumpCondition.TargetId != "TRAINING-DOUBLE-JUMP-SUMMIT" ||
                doubleJumpCondition.RequiredAmount != 1 || equipmentQuest == null ||
                equipmentQuest.Conditions == null || equipmentQuest.Conditions.Length != 1 ||
                equipmentQuest.Conditions[0] != doubleJumpCondition)
                issues.Add("QST-TUTO-006 must complete only at the marker-authored double-jump summit.");
            var meleeAttackCondition = AssetDatabase.LoadAssetAtPath<QuestConditionDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Conditions/COND-TUTO-003-ATTACK.asset");
            if (meleeAttackCondition == null || meleeAttackCondition.SignalType != QuestSignalType.AttackPerformed ||
                meleeAttackCondition.RequiredAmount != 3)
                issues.Add("QST-TUTO-003 must count three successful melee hits against a target.");
            var rangedCondition = AssetDatabase.LoadAssetAtPath<QuestConditionDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Conditions/COND-TUTO-005-RANGED-TRIPLE-HIT.asset");
            if (rangedCondition == null || rangedCondition.SignalType != QuestSignalType.RangedTripleHitPerformed ||
                rangedCondition.RequiredAmount != 1)
                issues.Add("QST-TUTO-005 must require one projectile to penetrate all three ranged targets.");
            var importedTrainingFlow = RequireObject("TrainingFlowManager", issues);
            RequireComponent<TutorialImportedTrainingFlowHost>(importedTrainingFlow, issues);
            var importedTrainingHost = importedTrainingFlow != null
                ? importedTrainingFlow.GetComponent<TutorialImportedTrainingFlowHost>()
                : null;
            if (importedTrainingHost != null &&
                (!importedTrainingHost.HasValidSetup || importedTrainingHost.RangedTargetCount != 3))
                issues.Add("Imported training flow requires room bounds, automatic boots, and exactly three ranged targets.");
            var phaseController = importedTrainingFlow != null
                ? importedTrainingFlow.GetComponent<TutorialTrainingPhaseControllerHost>()
                : null;
            if (RequireObject("훈련장 배치 마커", issues) != null &&
                (phaseController == null || !phaseController.HasValidSetup))
                issues.Add("Imported training room requires one valid sequential phase controller.");
            var phaseContents = RequireChild(trainingSpawnController, "TrainingPhaseContents", issues);
            foreach (var phaseName in new[]
                     {
                         "01_대시", "02_더블점프", "03_점프", "04_근접공격", "05_원거리공격"
                     })
                RequireChild(phaseContents, phaseName, issues);
            var dashFinishMarker = RequireObject("훈련_대시_끝", issues);
            RequireComponent<TutorialFunctionMarkerHost>(dashFinishMarker, issues);
            RequireComponent<TutorialTrainingArrivalMarkerHost>(dashFinishMarker, issues);
            var dashFires = phaseContents != null
                ? phaseContents.GetComponentsInChildren<TutorialTrainingDashFireHost>(true)
                : Array.Empty<TutorialTrainingDashFireHost>();
            if (dashFires.Length != 3 || dashFires.Any(fire => !fire.HasValidSetup))
                issues.Add("Dash phase must bind exactly three red fire shapes to invulnerable-dash restart logic.");
            var doubleJumpPractice = RequireObject("DoubleJumpPracticeRoot", issues);
            var lowPracticePlatform = RequireChild(doubleJumpPractice, "DoubleJumpPlatform_Low_ART_SLOT", issues);
            var highPracticePlatform = RequireChild(doubleJumpPractice, "DoubleJumpPlatform_High_ART_SLOT", issues);
            var landingPracticePlatform = RequireChild(doubleJumpPractice, "DoubleJumpPlatform_Landing_ART_SLOT", issues);
            RequireComponent<BoxCollider2D>(lowPracticePlatform, issues);
            RequireComponent<BoxCollider2D>(highPracticePlatform, issues);
            RequireComponent<BoxCollider2D>(landingPracticePlatform, issues);
            if (lowPracticePlatform != null && highPracticePlatform != null)
            {
                var lowCollider = lowPracticePlatform.GetComponent<BoxCollider2D>();
                var highCollider = highPracticePlatform.GetComponent<BoxCollider2D>();
                if (lowCollider != null && highCollider != null &&
                    GetColliderTop(highCollider) - GetColliderTop(lowCollider) <= 2.55f)
                    issues.Add("The high equipment platform must exceed the player's single-jump rise and require double jump.");
            }

            var helte = RequireObject("TutorialHelte", issues);
            RequireComponent<CombatActorHost>(helte, issues);
            RequireComponent<EnemyAttackHost>(helte, issues);
            RequireComponent<HelteBossPatternHost>(helte, issues);
            RequireComponent<CombatVisualMotionHost>(helte, issues);
            RequireChild(helte, "BossAttackAnchor", issues);
            RequireChild(helte, "HelteBasicHitbox", issues);
            RequireChild(helte, "HelteBlinkCrossHitbox", issues);
            RequireChild(helte, "HelteSwordHitbox0", issues);
            RequireChild(helte, "HelteSwordHitbox1", issues);
            RequireChild(helte, "HelteSwordHitbox2", issues);

            var bossArenaController = RequireObject("BossArena_Controller", issues);
            RequireComponent<TutorialBossArenaHost>(bossArenaController, issues);
            var bossArenaHost = bossArenaController != null
                ? bossArenaController.GetComponent<TutorialBossArenaHost>()
                : null;
            if (bossArenaHost != null && !bossArenaHost.HasValidSetup)
                issues.Add("Tutorial boss arena has invalid trigger, boss, warning, or pattern-lane references.");
            if (bossArenaHost != null && !Mathf.Approximately(bossArenaHost.IntroWarningSeconds, 1.1f))
                issues.Add("Helte's pre-combat camera and warning presentation must last 1.1 seconds.");
            RequireObject("BossArena_StartTrigger", issues);
            if (FindObject("BossArena_EntryGate_ART_SLOT") != null)
                issues.Add("The obsolete Helte entry gate must not block the dock approach.");
            RequireObject("BossWarning_ART_SLOT", issues);
            var bossArenaFloor = RequireObject("BossArena_Floor_ART_SLOT", issues);
            RequireComponent<BoxCollider2D>(bossArenaFloor, issues);
            if (bossArenaFloor != null && (bossArenaFloor.transform.lossyScale.x < 20f || bossArenaFloor.transform.lossyScale.x > 24f))
                issues.Add("Helte's tutorial arena must remain a compact 20-24 unit flat floor.");
            var bossCenterMarker = RequireObject("Storage_BossMarker", issues);
            if (bossCenterMarker != null && bossCenterMarker.GetComponent<Collider2D>() != null)
                issues.Add("Helte's center marker must be visual-only and must not obstruct the flat arena.");
            for (var laneIndex = 1; laneIndex <= 3; laneIndex++)
                RequireObject($"BossPatternLane_{laneIndex:00}_ART_SLOT", issues);

            var helteStageAnchors = RequireObject("HelteStageAnchors", issues);
            RequireChild(helteStageAnchors, "ApproachCheckpointAnchor", issues);
            RequireChild(helteStageAnchors, "ArenaEntryAnchor", issues);
            RequireChild(helteStageAnchors, "BossDialogueAnchor", issues);
            RequireChild(helteStageAnchors, "BossCenterAnchor", issues);
            RequireChild(helteStageAnchors, "BossCameraFocusAnchor", issues);
            RequireChild(helteStageAnchors, "BossDefeatAnchor", issues);
            var bossBlinkLeft = RequireChild(helteStageAnchors, "BossBlinkLeftAnchor", issues);
            var bossBlinkRight = RequireChild(helteStageAnchors, "BossBlinkRightAnchor", issues);
            RequireChild(helteStageAnchors, "SwordSpawn_Left", issues);
            RequireChild(helteStageAnchors, "SwordSpawn_Right", issues);
            RequireChild(helteStageAnchors, "SwordSpawn_Center", issues);
            var heltePresentation = RequireObject("HelteCombatPresentation_ART_SLOTS", issues);
            RequireChild(heltePresentation, "BlinkAfterimage_ART_SLOT", issues);
            RequireChild(heltePresentation, "DashPath_ART_SLOT", issues);
            RequireChild(heltePresentation, "CrossSlashWarning_ART_SLOT", issues);
            RequireChild(heltePresentation, "PhaseTransition_ART_SLOT", issues);
            RequireChild(heltePresentation, "SwordVisual_Left_ART_SLOT", issues);
            RequireChild(heltePresentation, "SwordVisual_Right_ART_SLOT", issues);
            RequireChild(heltePresentation, "SwordVisual_Center_ART_SLOT", issues);
            var heltePatternHost = helte != null ? helte.GetComponent<HelteBossPatternHost>() : null;
            if (heltePatternHost != null && bossBlinkLeft != null && bossBlinkRight != null)
            {
                var patternSerialized = new SerializedObject(heltePatternHost);
                if (patternSerialized.FindProperty("blinkLeftAnchor").objectReferenceValue != bossBlinkLeft.transform ||
                    patternSerialized.FindProperty("blinkRightAnchor").objectReferenceValue != bossBlinkRight.transform)
                    issues.Add("Helte's blink pattern must use the external stage anchors.");
                if (patternSerialized.FindProperty("bossCenterAnchor").objectReferenceValue == null ||
                    patternSerialized.FindProperty("bossVisualSlot").objectReferenceValue == null ||
                    patternSerialized.FindProperty("blinkAfterimageSlot").objectReferenceValue == null ||
                    patternSerialized.FindProperty("dashPathSlot").objectReferenceValue == null ||
                    patternSerialized.FindProperty("crossSlashWarningSlot").objectReferenceValue == null ||
                    patternSerialized.FindProperty("phaseTransitionSlot").objectReferenceValue == null ||
                    patternSerialized.FindProperty("swordSpawnAnchors").arraySize != 3 ||
                    patternSerialized.FindProperty("swordVisualSlots").arraySize != 3)
                    issues.Add("Helte's FSM must have all pre-placed phase, blink, dash, cross-slash, and sword presentation references.");
            }
            var bossStartTrigger = RequireObject("BossArena_StartTrigger", issues);
            if (bossStartTrigger != null && helte != null &&
                Mathf.Abs(bossStartTrigger.transform.position.x - helte.transform.position.x) > 12f)
                issues.Add("Helte must be visible from the arena start trigger in the compact tutorial arena.");
            var helteDialogueTrigger = RequireObject("H01_헬테조우대화_TRIGGER", issues);
            RequireComponent<BoxCollider2D>(helteDialogueTrigger, issues);
            RequireComponent<TutorialHelteEncounterDialogueHost>(helteDialogueTrigger, issues);
            var helteDialogue = helteDialogueTrigger != null
                ? helteDialogueTrigger.GetComponent<TutorialHelteEncounterDialogueHost>()
                : null;
            if (helteDialogue != null && (!helteDialogue.HasValidSetup || helteDialogue.LineCount != 2))
                issues.Add("Helte's dock encounter must split the two encounter lines from the entry dialogue and set the retry checkpoint.");

            var goal = RequireObject("GoalMarker", issues);
            RequireComponent<TutorialGoalHost>(goal, issues);
            RequireChild(goal, "GoalTrigger", issues);

            var relay = RequireObject("TutorialRelay", issues);
            RequireComponent<TutorialRelayHost>(relay, issues);
            RequireChild(relay, "RelayTrigger", issues);

            var relayHost = relay != null ? relay.GetComponent<TutorialRelayHost>() : null;
            if (relayHost != null && !relayHost.HasValidSetup)
                issues.Add("TutorialRelayHost has no valid relay setup.");

            var terrainRoot = RequireObject("TerrainLayoutRoot", issues);
            if (terrainRoot != null)
            {
                var terrainBlocks = terrainRoot.GetComponentsInChildren<TerrainBlock>(true);
                if (terrainBlocks.Length == 0) issues.Add("TerrainLayoutRoot has no TerrainBlock children.");
                foreach (var terrainBlock in terrainBlocks)
                    if (!terrainBlock.HasValidSetup)
                        issues.Add($"TerrainBlock '{terrainBlock.name}' has invalid collision or visual references.");
            }
            RequireObject("TerrainBase", issues);
            RequireObject("TerrainPlatformLeft", issues);
            RequireObject("TerrainPlatformMid", issues);
            RequireObject("TerrainPlatformRight", issues);
            RequireObject("TerrainPlatformBossApproach", issues);
            RequireObject("TerrainBoundaryLeft", issues);
            RequireObject("TerrainBoundaryRight", issues);

            var narrativeRoot = RequireObject("NarrativeStageRoot", issues);
            RequireChild(narrativeRoot, "AdamasHeadquartersRoot", issues);
            RequireChild(narrativeRoot, "TrainingGroundNarrativeRoot", issues);
            RequireChild(narrativeRoot, "ExteriorApproachRoot", issues);
            RequireChild(narrativeRoot, "OreStorageNarrativeRoot", issues);

            var mainCamera = RequireObject("Main Camera", issues);
            RequireComponent<CameraFollowHost>(mainCamera, issues);
            var sceneCamera = mainCamera != null ? mainCamera.GetComponent<Camera>() : null;
            if (sceneCamera != null &&
                (sceneCamera.clearFlags != CameraClearFlags.SolidColor ||
                 !Mathf.Approximately(sceneCamera.backgroundColor.a, 0f)))
                issues.Add("Tutorial blockout camera must clear to a transparent background.");
            var cameraFollow = mainCamera != null ? mainCamera.GetComponent<CameraFollowHost>() : null;
            if (cameraFollow != null && !cameraFollow.HasValidSetup)
                issues.Add("CameraFollowHost has no valid target or horizontal bounds.");
            if (cameraFollow != null && !cameraFollow.HasReviewSetup)
                issues.Add("CameraFollowHost requires player velocity, Helte framing, separate boss lens, and camera references.");

            foreach (var attackHost in Resources.FindObjectsOfTypeAll<EnemyAttackHost>())
            {
                if (attackHost != null && attackHost.gameObject.scene.IsValid() && !attackHost.HasReadableSetup)
                    issues.Add($"EnemyAttackHost '{attackHost.name}' requires a warning slot and longer telegraph than active hitbox time.");
            }

            foreach (var actor in Resources.FindObjectsOfTypeAll<CombatActorHost>())
            {
                if (actor == null || !actor.gameObject.scene.IsValid()) continue;
                var artContract = actor.GetComponent<ArtReplacementContractHost>();
                if (artContract == null)
                {
                    issues.Add($"Combat actor '{actor.name}' is missing ArtReplacementContractHost.");
                    continue;
                }
                if (!artContract.HasValidSetup)
                    issues.Add($"Art contract '{actor.name}' has missing visual, collider, renderer, foot, or trigger references.");
                if (!artContract.IsFootAligned)
                    issues.Add($"Art contract '{actor.name}' foot anchor is not aligned to the body collider bottom.");
                if (!artContract.HasConsistentSorting)
                    issues.Add($"Art contract '{actor.name}' renderers do not share the expected sorting layer.");
            }

            var hud = RequireObject("TutorialHUD", issues);
            var zoneFadeOverlay = RequireChild(hud, "TutorialZoneFadeOverlay", issues);
            RequireComponent<CanvasGroup>(zoneFadeOverlay, issues);
            RequireChild(hud, "TutorialStatusText", issues);
            var objectivePanel = RequireChild(hud, "TutorialObjectivePanel", issues);
            RequireChild(hud, "TutorialKeyPromptText", issues);
            var stageCaption = RequireChild(hud, "TutorialStageCaptionText", issues);
            var interactionPromptPanel = RequireChild(hud, "TutorialInteractionPromptPanel", issues);
            RequireChild(interactionPromptPanel, "PromptText", issues);
            var playerHealth = RequireChild(hud, "PlayerHealthText", issues);
            var enemyHealth = RequireChild(hud, "EnemyHealthText", issues);
            var bossHealthBar = RequireChild(hud, "BossHealthBarPanel", issues);
            RequireComponent<CanvasGroup>(bossHealthBar, issues);
            RequireComponent<BossHealthBarPresenter>(bossHealthBar, issues);
            var bossHealthPresenter = bossHealthBar != null ? bossHealthBar.GetComponent<BossHealthBarPresenter>() : null;
            if (bossHealthPresenter != null && !bossHealthPresenter.HasValidSetup)
                issues.Add("Boss health bar requires the Helte arena, actor, fill image, and numeric health text references.");
            RequireComponent<BossCombatCuePresenter>(bossHealthBar, issues);
            var bossCombatCue = bossHealthBar != null ? bossHealthBar.GetComponent<BossCombatCuePresenter>() : null;
            if (bossCombatCue != null && !bossCombatCue.HasValidSetup)
                issues.Add("Boss combat cue requires arena, FSM, root, and text references.");
            RequireChild(bossHealthBar, "BossCombatCueText", issues);
            var bossHealthTrack = RequireChild(bossHealthBar, "BossHealthBarTrack", issues);
            RequireChild(bossHealthTrack, "BossHealthBarFill_ART_SLOT", issues);
            RequireChild(bossHealthTrack, "PhaseDivider_ART_SLOT", issues);
            RequireChild(bossHealthBar, "BossHealthValueText", issues);
            if (bossHealthBar != null)
            {
                var rect = bossHealthBar.transform as RectTransform;
                if (rect == null || rect.anchorMin.y != 0f || rect.anchorMax.y != 0f || rect.sizeDelta.x < 800f)
                    issues.Add("Boss health bar must remain a wide bottom-anchored combat HUD element.");
            }
            RequireComponent<CombatHealthTextPresenter>(enemyHealth, issues);
            var enemyHealthPresenter = enemyHealth != null
                ? enemyHealth.GetComponent<CombatHealthTextPresenter>()
                : null;
            if (enemyHealthPresenter != null && (!enemyHealthPresenter.HasValidSetup || enemyHealthPresenter.AlternateActorCount != 8))
                issues.Add("Enemy health UI must track the training enemy, seven exterior enemies, and tutorial boss.");
            if (enemyHealthPresenter != null && !enemyHealthPresenter.HidesBossActors)
                issues.Add("Compact enemy health text must hide bosses when the dedicated bottom boss bar is available.");
            RequireChild(hud, "TutorialResultOverlay", issues);
            RequireChild(hud, "ModuleTreePanel", issues);
            var inventoryPanel = RequireChild(hud, "InventoryPanel", issues);
            var inventoryOpenButton = RequireChild(hud, "InventoryOpenButton", issues);
            RequireChild(inventoryPanel, "InventoryCloseButton", issues);
            var introductionCard = RequireChild(hud, "TutorialIntroductionCard", issues);
            var glideInstruction = RequireChild(hud, "HiddenRoomGlideInstruction", issues);
            RequireChild(glideInstruction, "SpaceKey_ART_SLOT", issues);
            var dialoguePanel = RequireChild(hud, "TutorialDialoguePanel", issues);
            var lorePanel = RequireChild(hud, "TutorialLoreSubtitlePanel", issues);
            var loreText = RequireChild(lorePanel, "SubtitleText", issues);
            RequireComponent<CanvasGroup>(lorePanel, issues);
            RequireComponent<UnityEngine.UI.Text>(loreText, issues);
            RequireComponent<TutorialLoreSubtitlePresenter>(lorePanel, issues);
            var lorePresenter = lorePanel != null ? lorePanel.GetComponent<TutorialLoreSubtitlePresenter>() : null;
            if (lorePresenter != null && !lorePresenter.HasValidSetup)
                issues.Add("Tutorial lore subtitle panel has invalid CanvasGroup or Text references.");
            RequireComponent<DialogueViewModule>(dialoguePanel, issues);
            var dialogueView = dialoguePanel != null ? dialoguePanel.GetComponent<DialogueViewModule>() : null;
            if (dialogueView != null && (!dialogueView.HasDialogueLabel || !dialogueView.HasSpeakerPresentation))
                issues.Add("DialogueViewModule requires left and right speaker portrait presentation references.");
            var dialogueStageText = RequireChild(dialoguePanel, "StageText", issues);
            RequireChild(dialoguePanel, "DialogueText", issues);
            RequireChild(dialoguePanel, "ContinueText", issues);
            var dialogueSpeakerLeft = RequireChild(dialoguePanel, "DialogueSpeakerLeft", issues);
            var dialogueSpeakerRight = RequireChild(dialoguePanel, "DialogueSpeakerRight", issues);
            RequireChild(dialogueSpeakerLeft, "Portrait_ART_SLOT", issues);
            RequireChild(dialogueSpeakerRight, "Portrait_ART_SLOT", issues);
            RequireComponent<ModuleTreePanelPresenter>(hud, issues);
            var moduleTreePresenter = hud != null ? hud.GetComponent<ModuleTreePanelPresenter>() : null;
            if (moduleTreePresenter != null && !moduleTreePresenter.HasValidSetup)
                issues.Add("ModuleTreePanelPresenter has invalid service, input, manager, or UI references.");
            var interactionPromptHost = hud != null ? hud.GetComponent<TutorialInteractionPromptHost>() : null;
            RequireComponent<TutorialInteractionPromptHost>(hud, issues);
            if (interactionPromptHost != null && !interactionPromptHost.HasValidSetup)
                issues.Add("TutorialInteractionPromptHost has invalid player, quest, or UI references.");
            var inventoryPresenter = hud != null ? hud.GetComponent<InventoryPanelPresenter>() : null;
            RequireComponent<InventoryPanelPresenter>(hud, issues);
            if (inventoryPresenter != null && !inventoryPresenter.HasValidSetup)
                issues.Add("InventoryPanelPresenter has invalid UI or module references.");
            RequireComponent<InventoryPanelButtonHost>(inventoryOpenButton, issues);
            var inventoryCloseButton = inventoryPanel != null ? inventoryPanel.transform.Find("InventoryCloseButton") : null;
            RequireComponent<InventoryPanelButtonHost>(inventoryCloseButton == null ? null : inventoryCloseButton.gameObject, issues);
            RequireComponent<DialogueIntroductionCardModule>(introductionCard, issues);
            RequireComponent<CanvasGroup>(introductionCard, issues);
            var introductionCardModule = introductionCard != null
                ? introductionCard.GetComponent<DialogueIntroductionCardModule>()
                : null;
            if (introductionCardModule != null &&
                (!introductionCardModule.UsesTimedCollapse ||
                 Mathf.Abs(introductionCardModule.PromptDelay - 1f) > 0.01f))
                issues.Add("Introduction cards must allow any-key dismissal after one second and use the vertical collapse close animation.");

            var loreTriggers = Resources.FindObjectsOfTypeAll<TutorialLoreSubtitleTriggerHost>()
                .Where(candidate => candidate != null && candidate.gameObject.scene.IsValid())
                .ToArray();
            if (loreTriggers.Length < 5)
                issues.Add($"Tutorial route must retain at least five Teus lore subtitle triggers, but found {loreTriggers.Length}.");
            foreach (var loreTrigger in loreTriggers)
                if (!loreTrigger.HasValidSetup)
                    issues.Add($"Lore subtitle trigger '{loreTrigger.name}' has invalid quest, player, text, presenter, or collider setup.");
            var relayLoreTrigger = RequireObject("LoreTrigger_05_RelayTower", issues);
            var bossZone = RequireObject("Z06_OreStorage_Boss", issues);
            if (relayLoreTrigger != null && bossZone != null && !relayLoreTrigger.transform.IsChildOf(bossZone.transform))
                issues.Add("The final relay-tower lore subtitle must play in the Helte approach corridor.");

            var zoneTransitions = Resources.FindObjectsOfTypeAll<TutorialZoneTransitionHost>();
            if (zoneTransitions.Length == 0)
            {
                issues.Add("Tutorial scene has no TutorialZoneTransitionHost.");
            }
            else
            {
                var ladderTransitionCount = 0;
                foreach (var zoneTransition in zoneTransitions)
                {
                    if (zoneTransition != null && !zoneTransition.HasValidSetup)
                        issues.Add($"TutorialZoneTransitionHost '{zoneTransition.name}' has invalid zone, player, camera, or fade references.");
                    if (zoneTransition != null && !zoneTransition.UsesSweptPlayerDetection)
                        issues.Add($"TutorialZoneTransitionHost '{zoneTransition.name}' must detect fast player crossings.");
                    if (zoneTransition != null && zoneTransition.UsesLadderSequence)
                    {
                        ladderTransitionCount++;
                        if (!zoneTransition.HasValidLadderSetup)
                            issues.Add("HQ ladder transition has invalid entry, exit, or visual references.");
                    }
                }
                if (ladderTransitionCount != 1)
                    issues.Add($"Tutorial scene must have exactly one ladder transition, but found {ladderTransitionCount}.");
            }

            var ladderVisual = RequireObject("HQ_LadderTransition_ART_SLOT", issues);
            RequireChild(ladderVisual, "LadderRail_Left", issues);
            RequireChild(ladderVisual, "LadderRail_Right", issues);
            RequireChild(ladderVisual, "LadderEntry", issues);
            RequireChild(ladderVisual, "LadderExit", issues);

            var trainingGateRoot = RequireObject("TrainingExitGates", issues);
            var sequentialTraining = RequireObject("훈련장 배치 마커", issues) != null;
            if (sequentialTraining)
            {
                var singleTrainingGate = RequireObject("Gate_Block_TrainingExit", issues);
                RequireComponent<Collider2D>(singleTrainingGate, issues);
                RequireComponent<Renderer>(singleTrainingGate, issues);
                var activeLegacyGates = trainingGateRoot != null
                    ? trainingGateRoot.GetComponentsInChildren<TutorialQuestGateHost>(true)
                        .Count(host => host.enabled && host.gameObject.activeInHierarchy)
                    : 0;
                if (activeLegacyGates != 0)
                    issues.Add("Sequential training must disable all five legacy quest-gate behaviours.");
            }
            else
            {
                var trainingGates = Resources.FindObjectsOfTypeAll<TutorialQuestGateHost>();
                if (trainingGates.Length != 5)
                    issues.Add($"Tutorial training room must have exactly 5 quest gates, but found {trainingGates.Length}.");
                foreach (var trainingGate in trainingGates)
                    if (trainingGate != null && !trainingGate.HasValidSetup)
                        issues.Add($"TutorialQuestGateHost '{trainingGate.name}' has invalid quest, collider, or visual references.");
            }

            var encounterAController = RequireObject("F01_EncounterController", issues);
            RequireComponent<TutorialSimultaneousEncounterHost>(encounterAController, issues);
            var encounterAHost = encounterAController != null
                ? encounterAController.GetComponent<TutorialSimultaneousEncounterHost>()
                : null;
            if (encounterAHost != null &&
                (!encounterAHost.HasValidSetup || !encounterAHost.ActivatesAllEnemiesAtOnce ||
                 encounterAHost.EnemyCount != 3))
                issues.Add("Exterior encounter A must activate three valid enemies together and lock its exit gate.");
            for (var enemyIndex = 1; enemyIndex <= 3; enemyIndex++)
            {
                var exteriorEnemy = RequireObject($"ExteriorA_Enemy_0{enemyIndex}_ART_SLOT", issues);
                RequireComponent<CombatActorHost>(exteriorEnemy, issues);
                RequireComponent<EnemyAttackHost>(exteriorEnemy, issues);
                RequireComponent<CombatVisualMotionHost>(exteriorEnemy, issues);
                RequireComponent<TutorialEnemyPursuitHost>(exteriorEnemy, issues);
                var pursuit = exteriorEnemy != null
                    ? exteriorEnemy.GetComponent<TutorialEnemyPursuitHost>()
                    : null;
                if (pursuit != null && !pursuit.HasValidSetup)
                    issues.Add($"Exterior encounter A enemy 0{enemyIndex} has an invalid player-pursuit setup.");
            }
            RequireObject("F01_Spawn_ExteriorSide", issues);
            RequireObject("F01_Exit_ToG", issues);
            var fGate = RequireObject("F01_출구잠금문_PROXY", issues);
            RequireComponent<BoxCollider2D>(fGate, issues);
            RequireComponent<TutorialGateVisualBindingHost>(fGate, issues);
            if (fGate != null && fGate.GetComponent<TutorialGateVisualBindingHost>()?.BoundRenderer == null)
                issues.Add("F01 exit gate proxy must reference the level designer's thin door renderer.");
            var fGateBinding = fGate != null ? fGate.GetComponent<TutorialGateVisualBindingHost>() : null;
            if (fGateBinding?.BoundRenderer != null &&
                fGateBinding.BoundRenderer.GetComponents<Collider2D>().Any(collider => collider.enabled))
                issues.Add(
                    $"{fGateBinding.BoundRenderer.name} must not keep an enabled authored collider beside its F gate proxy.");
            var exteriorToF = RequireObject("E01_Exit_ToF", issues);
            RequireComponent<BoxCollider2D>(exteriorToF, issues);
            RequireComponent<TutorialZoneTransitionHost>(exteriorToF, issues);
            var exteriorToFHost = exteriorToF != null
                ? exteriorToF.GetComponent<TutorialZoneTransitionHost>()
                : null;
            if (exteriorToFHost != null && !exteriorToFHost.HasValidSetup)
                issues.Add("E01 to F01 transition has invalid player, camera, zone, or checkpoint references.");

            var encounterBController = RequireObject("G01_EncounterController", issues);
            RequireComponent<TutorialWaveEncounterHost>(encounterBController, issues);
            var encounterBHost = encounterBController != null
                ? encounterBController.GetComponent<TutorialWaveEncounterHost>()
                : null;
            if (encounterBHost != null &&
                (!encounterBHost.HasValidSetup || !encounterBHost.RequiresTraversalForNextWave ||
                 !encounterBHost.AutoAdvancesNextWaveAfterGateOpens ||
                 encounterBHost.WaveCount != 2 || encounterBHost.EnemyCount != 4))
                issues.Add(
                    "Exterior encounter B must automatically start its second enemy group after opening the internal gate.");
            for (var enemyIndex = 1; enemyIndex <= 4; enemyIndex++)
            {
                var exteriorEnemy = RequireObject($"ExteriorB_Enemy_0{enemyIndex}_ART_SLOT", issues);
                RequireComponent<CombatActorHost>(exteriorEnemy, issues);
                RequireComponent<EnemyAttackHost>(exteriorEnemy, issues);
                RequireComponent<CombatVisualMotionHost>(exteriorEnemy, issues);
                RequireComponent<TutorialEnemyPursuitHost>(exteriorEnemy, issues);
                var pursuit = exteriorEnemy != null
                    ? exteriorEnemy.GetComponent<TutorialEnemyPursuitHost>()
                    : null;
                if (pursuit != null && !pursuit.HasValidSetup)
                    issues.Add($"Exterior encounter B enemy 0{enemyIndex} has an invalid player-pursuit setup.");
            }
            var encounterBPhaseTrigger = RequireObject("G01_후반부진입_TRIGGER", issues);
            RequireComponent<BoxCollider2D>(encounterBPhaseTrigger, issues);
            RequireComponent<TutorialEncounterPhaseTriggerHost>(encounterBPhaseTrigger, issues);
            var phaseTriggerHost = encounterBPhaseTrigger != null
                ? encounterBPhaseTrigger.GetComponent<TutorialEncounterPhaseTriggerHost>()
                : null;
            if (phaseTriggerHost != null &&
                (!phaseTriggerHost.HasValidSetup || !phaseTriggerHost.HasDynamicGuidance))
                issues.Add("Exterior encounter B phase trigger has invalid encounter, player, or objective guidance references.");
            foreach (var gateName in new[] { "G01_내부통로잠금문_PROXY", "G01_출구잠금문_PROXY" })
            {
                var gate = RequireObject(gateName, issues);
                RequireComponent<BoxCollider2D>(gate, issues);
                RequireComponent<TutorialGateVisualBindingHost>(gate, issues);
                if (gate != null && gate.GetComponent<TutorialGateVisualBindingHost>()?.BoundRenderer == null)
                    issues.Add($"{gateName} must reference the level designer's thin door renderer.");
                var binding = gate != null ? gate.GetComponent<TutorialGateVisualBindingHost>() : null;
                if (binding?.BoundRenderer != null &&
                    binding.BoundRenderer.GetComponents<Collider2D>().Any(collider => collider.enabled))
                    issues.Add(
                        $"{binding.BoundRenderer.name} must not keep an enabled authored collider beside its gate proxy.");
            }
            foreach (var transitionName in new[] { "F01_Exit_ToG", "G01_Exit_ToH" })
            {
                var transitionObject = RequireObject(transitionName, issues);
                RequireComponent<BoxCollider2D>(transitionObject, issues);
                RequireComponent<TutorialZoneTransitionHost>(transitionObject, issues);
                var transition = transitionObject != null
                    ? transitionObject.GetComponent<TutorialZoneTransitionHost>()
                    : null;
                if (transition != null && !transition.HasValidSetup)
                    issues.Add($"{transitionName} has invalid player, zone, camera, or checkpoint references.");
            }
            RequireObject("G01_Spawn_FromF", issues);
            RequireObject("H01_Spawn_FromG", issues);

            var hazardCoordinatorObject = RequireObject("G02_HazardController", issues);
            RequireComponent<TutorialEnvironmentHazardCoordinatorHost>(hazardCoordinatorObject, issues);
            var hazardCoordinator = hazardCoordinatorObject != null
                ? hazardCoordinatorObject.GetComponent<TutorialEnvironmentHazardCoordinatorHost>()
                : null;
            if (hazardCoordinator != null &&
                (!hazardCoordinator.HasValidSetup ||
                 !Mathf.Approximately(hazardCoordinator.LavaDamageFraction, 0.2f)))
                issues.Add("G environment hazard coordinator must apply 20% lava damage and have a valid safe point.");

            var fireHazards = Resources.FindObjectsOfTypeAll<TutorialFireHazardHost>()
                .Where(host => host != null && host.gameObject.scene.IsValid())
                .ToArray();
            if (fireHazards.Length != 2 ||
                fireHazards.Any(host => !host.HasValidSetup ||
                                        !Mathf.Approximately(host.DamageFraction, 0.1f)))
                issues.Add("G must have exactly two valid cyclic fire hazards dealing 10% max-health damage.");

            var gHazardRoot = RequireObject("G02_EnvironmentHazards", issues);
            var windHazards = gHazardRoot != null
                ? gHazardRoot.GetComponentsInChildren<TutorialWindHazardHost>(true)
                : Array.Empty<TutorialWindHazardHost>();
            var requiredWindNames = new[]
            {
                "G02_바람_시작_MARKER",
                "G02_바람_01_MARKER",
                "G02_바람_중간통로_MARKER",
                "G02_바람_02_MARKER"
            };
            if (windHazards.Length != requiredWindNames.Length ||
                windHazards.Any(host => !host.HasValidSetup || !host.RequiresGlideInput) ||
                requiredWindNames.Any(requiredName =>
                    windHazards.All(host => host.gameObject.name != requiredName)))
                issues.Add("G must have four valid route-specific wind markers requiring held Space/glide input.");
            var gExit = RequireObject("G01_Exit_ToH", issues);
            foreach (var windHazard in windHazards)
            {
                var windCollider = windHazard.GetComponent<BoxCollider2D>();
                if (windCollider == null) continue;
                var windHeight = Mathf.Abs(windCollider.size.y * windCollider.transform.lossyScale.y);

                if (windHazard.gameObject.name == "G02_바람_시작_MARKER" &&
                    windHeight < 6f)
                    issues.Add("G entry wind must provide a readable lift into the stage.");
                else if (windHazard.gameObject.name == "G02_바람_중간통로_MARKER" &&
                         windHeight < 30f)
                    issues.Add("G middle-passage wind must cover its full vertical shaft.");
                else if (gExit != null &&
                         (windHazard.gameObject.name == "G02_바람_01_MARKER" ||
                          windHazard.gameObject.name == "G02_바람_02_MARKER") &&
                         GetColliderTop(windCollider) < gExit.transform.position.y + 1.5f)
                    issues.Add($"{windHazard.gameObject.name} must continue above the G-to-H exit height.");
            }

            var lavaHazards = Resources.FindObjectsOfTypeAll<TutorialLavaHazardHost>()
                .Where(host => host != null && host.gameObject.scene.IsValid())
                .ToArray();
            if (lavaHazards.Length != 1 ||
                lavaHazards.Any(host => !host.HasValidSetup || !host.ReturnsToLatestSafePoint))
                issues.Add("G must have exactly one valid lava volume that returns the player to the latest safe point.");
            var safePointTriggers = Resources.FindObjectsOfTypeAll<TutorialSafePointTriggerHost>()
                .Where(host => host != null && host.gameObject.scene.IsValid())
                .ToArray();
            if (safePointTriggers.Length < lavaHazards.Length ||
                safePointTriggers.Any(host => !host.HasValidSetup))
                issues.Add("Every G lava volume must have a valid preceding safe-point trigger.");

            RequireNoScreenOverlap(objectivePanel, dialoguePanel, "Tutorial objective panel overlaps the dialogue panel.", issues);
            RequireNoScreenOverlap(objectivePanel, lorePanel, "Tutorial objective panel overlaps the lore subtitle panel.", issues);
            RequireNoScreenOverlap(lorePanel, dialoguePanel, "Lore subtitle panel overlaps the dialogue panel.", issues);
            RequireNoScreenOverlap(interactionPromptPanel, dialoguePanel, "Interaction prompt overlaps the dialogue panel.", issues);
            RequireNoScreenOverlap(inventoryOpenButton, enemyHealth, "Inventory button overlaps enemy health UI.", issues);
            RequireNoScreenOverlap(stageCaption, playerHealth, "Stage caption overlaps player health UI.", issues);
            RequireNoScreenOverlap(dialogueStageText, dialogueSpeakerLeft, "Dialogue location overlaps the left speaker label.", issues);
            RequireNoScreenOverlap(dialogueStageText, dialogueSpeakerRight, "Dialogue location overlaps the right speaker label.", issues);

            if (issues.Count == 0)
            {
                Debug.Log("Tutorial scene validation passed.");
                return;
            }

            Debug.LogError("Tutorial scene validation failed (" + issues.Count + "): " + string.Join(" | ", issues));
        }

        private static GameObject RequireObject(string name, List<string> issues)
        {
            var candidate = FindObject(name);
            if (candidate != null) return candidate;

            issues.Add($"Missing GameObject '{name}'.");
            return null;
        }

        private static GameObject FindObject(string name)
        {
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate.name == name && candidate.scene.IsValid()) return candidate;
            }

            return null;
        }

        private static void RequireComponent<T>(GameObject target, List<string> issues) where T : Component
        {
            if (target == null || target.GetComponent<T>() != null) return;
            issues.Add($"{target.name} is missing {typeof(T).Name}.");
        }

        private static GameObject RequireChild(GameObject target, string childName, List<string> issues)
        {
            if (target == null) return null;
            var child = target.transform.Find(childName);
            if (child != null) return child.gameObject;
            issues.Add($"{target.name} is missing child '{childName}'.");
            return null;
        }

        private static void RequireNoScreenOverlap(GameObject first, GameObject second, string issue, List<string> issues)
        {
            if (first == null || second == null) return;
            if (first.transform is not RectTransform firstRect || second.transform is not RectTransform secondRect) return;

            if (GetWorldRect(firstRect).Overlaps(GetWorldRect(secondRect)))
                issues.Add(issue);
        }

        private static void RequireTransparentHudBackgrounds(GameObject hud, List<string> issues)
        {
            if (hud == null) return;
            var requiredTransparentImages = new[]
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
            var images = hud.GetComponentsInChildren<Image>(true);
            foreach (var imageName in requiredTransparentImages)
            {
                var image = images.FirstOrDefault(candidate => candidate != null &&
                                                               candidate.name == imageName);
                if (image == null)
                {
                    issues.Add($"Tutorial HUD background '{imageName}' is missing.");
                    continue;
                }
                if (!Mathf.Approximately(image.color.a, 0f))
                    issues.Add($"Tutorial HUD background '{imageName}' must remain transparent.");
            }
        }

        private static Rect GetWorldRect(RectTransform rectTransform)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static float GetColliderTop(BoxCollider2D collider)
        {
            var scaleY = Mathf.Abs(collider.transform.lossyScale.y);
            return collider.transform.position.y + collider.offset.y * scaleY + collider.size.y * scaleY * 0.5f;
        }
    }
}
