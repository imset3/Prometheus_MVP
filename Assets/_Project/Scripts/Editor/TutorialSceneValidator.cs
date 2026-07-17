using System.Collections.Generic;
using Narthex.Core;
using Narthex.Gameplay;
using Narthex.Presentation;
using Narthex.Save;
using Narthex.SceneFlow;
using UnityEditor;
using UnityEngine;

namespace Narthex.Tools
{
    public static class TutorialSceneValidator
    {
        [MenuItem("Narthex/Validation/Validate Active Tutorial Scene")]
        public static void ValidateActiveTutorialScene()
        {
            var issues = new List<string>();

            var systems = RequireObject("StageSystems", issues);
            RequireComponent<ServiceRoot>(systems, issues);
            RequireComponent<CombatSystemHost>(systems, issues);
            RequireComponent<SaveSystemHost>(systems, issues);
            RequireComponent<DevelopmentProgressResetManager>(systems, issues);
            RequireComponent<TutorialRestartHost>(systems, issues);
            RequireComponent<TutorialEncounterHost>(systems, issues);
            RequireComponent<TutorialCompletionFlowHost>(systems, issues);
            RequireComponent<QuestManagerHost>(systems, issues);
            RequireComponent<TutorialQuestSequenceHost>(systems, issues);
            RequireComponent<TutorialNarrativeSequenceHost>(systems, issues);
            RequireComponent<TutorialDialoguePresenter>(systems, issues);
            RequireComponent<ModuleSystemHost>(systems, issues);
            RequireComponent<ModuleTreeManagerHost>(systems, issues);
            RequireComponent<RewardExecutorHost>(systems, issues);
            RequireComponent<TutorialBossEncounterHost>(systems, issues);
            RequireComponent<TutorialBossCompletionHost>(systems, issues);
            RequireComponent<Chapter01TransitionHost>(systems, issues);

            var restartHost = systems != null ? systems.GetComponent<TutorialRestartHost>() : null;
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
            RequireObject("TutorialRelayCheckpoint", issues);

            var questSequence = systems != null ? systems.GetComponent<TutorialQuestSequenceHost>() : null;
            if (questSequence != null && !questSequence.HasValidSequence)
                issues.Add("TutorialQuestSequenceHost has no valid quest sequence.");

            var bossEncounter = systems != null ? systems.GetComponent<TutorialBossEncounterHost>() : null;
            if (bossEncounter != null && !bossEncounter.HasValidSetup)
                issues.Add("TutorialBossEncounterHost has no valid restore setup.");

            var chapterTransition = systems != null ? systems.GetComponent<Chapter01TransitionHost>() : null;
            if (chapterTransition != null && !chapterTransition.HasValidSetup)
                issues.Add("Chapter01TransitionHost has no valid result-button or scene setup.");

            var player = RequireObject("PlayerRoot", issues);
            RequireComponent<PlayerInputHost>(player, issues);
            RequireComponent<PlayerMotorHost>(player, issues);
            RequireComponent<CombatActorHost>(player, issues);
            RequireComponent<MeleeAttackHost>(player, issues);
            RequireComponent<TutorialModuleUseHost>(player, issues);
            RequireComponent<CombatVisualMotionHost>(player, issues);
            RequireChild(player, "GroundProbe", issues);
            RequireChild(player, "AttackAnchor", issues);

            var playerInput = player != null ? player.GetComponent<PlayerInputHost>() : null;
            if (playerInput != null && !playerInput.UsesCSharpEvents)
                issues.Add("PlayerInputHost must use PlayerInput Invoke C# Events notification behavior.");

            var playerMotor = player != null ? player.GetComponent<PlayerMotorHost>() : null;
            if (playerMotor != null && !playerMotor.HasServiceRoot)
                issues.Add("PlayerMotorHost requires a ServiceRoot reference for tutorial signals.");

            var moduleUseHost = player != null ? player.GetComponent<TutorialModuleUseHost>() : null;
            if (moduleUseHost != null && !moduleUseHost.HasValidSetup)
                issues.Add("TutorialModuleUseHost has no valid module setup.");

            var tutorialGuide = RequireObject("TutorialGuideCompanion", issues);
            RequireComponent<TutorialGuideCompanionHost>(tutorialGuide, issues);
            var guideVisual = RequireChild(tutorialGuide, "Visual", issues);
            RequireChild(guideVisual, "ModelSlot", issues);
            RequireChild(guideVisual, "EffectsSlot", issues);
            RequireChild(guideVisual, "AttachmentSlot", issues);

            var objectiveBeacon = RequireObject("TutorialObjectiveBeacon", issues);
            RequireComponent<TutorialObjectiveBeaconHost>(objectiveBeacon, issues);
            var beaconVisual = RequireChild(objectiveBeacon, "Visual", issues);
            RequireChild(beaconVisual, "ModelSlot", issues);
            RequireChild(beaconVisual, "EffectsSlot", issues);
            var beaconHost = objectiveBeacon != null ? objectiveBeacon.GetComponent<TutorialObjectiveBeaconHost>() : null;
            if (beaconHost != null && !beaconHost.HasValidSetup)
                issues.Add("TutorialObjectiveBeaconHost has invalid quest, player, or visual references.");

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

            var helte = RequireObject("TutorialHelte", issues);
            RequireComponent<CombatActorHost>(helte, issues);
            RequireComponent<EnemyAttackHost>(helte, issues);
            RequireComponent<HelteBossPatternHost>(helte, issues);
            RequireComponent<CombatVisualMotionHost>(helte, issues);
            RequireChild(helte, "BossAttackAnchor", issues);
            RequireChild(helte, "BlinkLeftAnchor", issues);
            RequireChild(helte, "BlinkRightAnchor", issues);
            RequireChild(helte, "HelteBasicHitbox", issues);
            RequireChild(helte, "HelteBlinkCrossHitbox", issues);
            RequireChild(helte, "HelteSwordHitbox0", issues);
            RequireChild(helte, "HelteSwordHitbox1", issues);
            RequireChild(helte, "HelteSwordHitbox2", issues);

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
            var cameraFollow = mainCamera != null ? mainCamera.GetComponent<CameraFollowHost>() : null;
            if (cameraFollow != null && !cameraFollow.HasValidSetup)
                issues.Add("CameraFollowHost has no valid target or horizontal bounds.");

            var hud = RequireObject("TutorialHUD", issues);
            RequireChild(hud, "TutorialStatusText", issues);
            var objectivePanel = RequireChild(hud, "TutorialObjectivePanel", issues);
            RequireChild(hud, "TutorialKeyPromptText", issues);
            var stageCaption = RequireChild(hud, "TutorialStageCaptionText", issues);
            var interactionPromptPanel = RequireChild(hud, "TutorialInteractionPromptPanel", issues);
            RequireChild(interactionPromptPanel, "PromptText", issues);
            var playerHealth = RequireChild(hud, "PlayerHealthText", issues);
            var enemyHealth = RequireChild(hud, "EnemyHealthText", issues);
            RequireChild(hud, "TutorialResultOverlay", issues);
            RequireChild(hud, "ModuleTreePanel", issues);
            var inventoryPanel = RequireChild(hud, "InventoryPanel", issues);
            var inventoryOpenButton = RequireChild(hud, "InventoryOpenButton", issues);
            RequireChild(inventoryPanel, "InventoryCloseButton", issues);
            var introductionCard = RequireChild(hud, "TutorialIntroductionCard", issues);
            var dialoguePanel = RequireChild(hud, "TutorialDialoguePanel", issues);
            RequireComponent<DialogueViewModule>(dialoguePanel, issues);
            RequireChild(dialoguePanel, "StageText", issues);
            RequireChild(dialoguePanel, "DialogueText", issues);
            RequireChild(dialoguePanel, "ContinueText", issues);
            RequireComponent<ModuleTreePanelPresenter>(hud, issues);
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

            RequireNoScreenOverlap(objectivePanel, dialoguePanel, "Tutorial objective panel overlaps the dialogue panel.", issues);
            RequireNoScreenOverlap(interactionPromptPanel, dialoguePanel, "Interaction prompt overlaps the dialogue panel.", issues);
            RequireNoScreenOverlap(inventoryOpenButton, enemyHealth, "Inventory button overlaps enemy health UI.", issues);
            RequireNoScreenOverlap(stageCaption, playerHealth, "Stage caption overlaps player health UI.", issues);

            if (issues.Count == 0)
            {
                Debug.Log("Tutorial scene validation passed.");
                return;
            }

            Debug.LogError("Tutorial scene validation failed (" + issues.Count + "): " + string.Join(" | ", issues));
        }

        private static GameObject RequireObject(string name, List<string> issues)
        {
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate.name == name && candidate.scene.IsValid()) return candidate;
            }

            issues.Add($"Missing GameObject '{name}'.");
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

        private static Rect GetWorldRect(RectTransform rectTransform)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
