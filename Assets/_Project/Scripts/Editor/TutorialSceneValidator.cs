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
            RequireComponent<TutorialRestartHost>(systems, issues);
            RequireComponent<TutorialEncounterHost>(systems, issues);
            RequireComponent<TutorialCompletionFlowHost>(systems, issues);
            RequireComponent<QuestManagerHost>(systems, issues);
            RequireComponent<TutorialQuestSequenceHost>(systems, issues);
            RequireComponent<ModuleSystemHost>(systems, issues);
            RequireComponent<ModuleTreeManagerHost>(systems, issues);
            RequireComponent<RewardExecutorHost>(systems, issues);
            RequireComponent<TutorialBossEncounterHost>(systems, issues);
            RequireComponent<TutorialBossCompletionHost>(systems, issues);
            RequireComponent<Chapter01TransitionHost>(systems, issues);

            var restartHost = systems != null ? systems.GetComponent<TutorialRestartHost>() : null;
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

            var mainCamera = RequireObject("Main Camera", issues);
            RequireComponent<CameraFollowHost>(mainCamera, issues);
            var cameraFollow = mainCamera != null ? mainCamera.GetComponent<CameraFollowHost>() : null;
            if (cameraFollow != null && !cameraFollow.HasValidSetup)
                issues.Add("CameraFollowHost has no valid target or horizontal bounds.");

            var hud = RequireObject("TutorialHUD", issues);
            RequireChild(hud, "TutorialStatusText", issues);
            RequireChild(hud, "PlayerHealthText", issues);
            RequireChild(hud, "EnemyHealthText", issues);
            RequireChild(hud, "TutorialResultOverlay", issues);
            RequireChild(hud, "ModuleTreePanel", issues);
            RequireComponent<ModuleTreePanelPresenter>(hud, issues);

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

        private static void RequireChild(GameObject target, string childName, List<string> issues)
        {
            if (target == null || target.transform.Find(childName) != null) return;
            issues.Add($"{target.name} is missing child '{childName}'.");
        }
    }
}
