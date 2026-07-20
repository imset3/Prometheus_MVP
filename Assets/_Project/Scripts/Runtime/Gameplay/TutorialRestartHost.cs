using System;
using System.Collections;
using Narthex.Core;
using Narthex.Save;
using UnityEngine;

namespace Narthex.Gameplay
{
    [Serializable]
    public sealed class TutorialRestartCheckpointDefinition
    {
        [SerializeField] private string questId;
        [SerializeField] private Transform spawnPoint;

        public string QuestId => questId;
        public Transform SpawnPoint => spawnPoint;
    }

    public sealed class TutorialRestartHost : MonoBehaviour
    {
        [SerializeField] private CombatSystemHost combatSystemHost;
        [SerializeField] private SaveSystemHost saveSystemHost;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private TutorialBossArenaHost bossArenaHost;
        [SerializeField] private CombatActorHost playerActor;
        [SerializeField] private Transform tutorialSpawnPoint;
        [SerializeField] private Transform relayCheckpointSpawnPoint;
        [SerializeField] private string relayCheckpointId = "RELAY-TUTO-001";
        [SerializeField] private string relayCheckpointQuestId = "QST-TUTO-007";
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private CombatActorHost[] resetActors;
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private PlayerMotorHost playerMotorHost;
        [SerializeField] private CanvasGroup fadeCanvasGroup;
        [SerializeField] private TutorialRestartCheckpointDefinition[] questCheckpoints =
            Array.Empty<TutorialRestartCheckpointDefinition>();
        [SerializeField] private bool restartSceneOnDeath;
        [SerializeField, Min(0f)] private float restartDelay = 0.35f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.25f;

        private Transform activeSpawnPoint;
        private bool restarting;

        public bool HasConfiguredResetActors
        {
            get
            {
                if (resetActors == null || resetActors.Length == 0) return false;
                foreach (var actor in resetActors)
                    if (actor == null) return false;
                return true;
            }
        }

        public bool IncludesResetActor(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId) || resetActors == null) return false;
            foreach (var actor in resetActors)
                if (actor != null && actor.ActorId == actorId) return true;
            return false;
        }

        public bool HasValidCheckpointSetup => relayCheckpointSpawnPoint != null && !string.IsNullOrWhiteSpace(relayCheckpointId) &&
                                               !string.IsNullOrWhiteSpace(relayCheckpointQuestId);
        public bool HasValidSceneRestartSetup => !restartSceneOnDeath && playerInputHost != null && playerMotorHost != null &&
                                                 fadeCanvasGroup != null && questSequenceHost != null && bossArenaHost != null &&
                                                 questCheckpoints != null && questCheckpoints.Length >= 10;
        public bool UsesInSceneRestart => !restartSceneOnDeath;
        public string ActiveSpawnPointName => activeSpawnPoint != null ? activeSpawnPoint.name : string.Empty;

        public bool HasCheckpointForQuest(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId) || questCheckpoints == null) return false;
            foreach (var checkpoint in questCheckpoints)
                if (checkpoint != null && checkpoint.QuestId == questId && checkpoint.SpawnPoint != null) return true;
            return false;
        }

        private void Awake()
        {
            if (combatSystemHost == null || saveSystemHost == null || playerActor == null || tutorialSpawnPoint == null ||
                !HasValidCheckpointSetup || playerBody == null || !HasValidSceneRestartSetup)
            {
                Debug.LogError("TutorialRestartHost requires in-scene fade, quest checkpoints, combat, save, player, and boss retry references.", this);
                enabled = false;
                return;
            }

            combatSystemHost.Initialize();
            if (!saveSystemHost.Initialize())
            {
                enabled = false;
                return;
            }

            activeSpawnPoint = ResolveCurrentCheckpoint();
            MovePlayerToActiveSpawn();
        }

        private void OnEnable()
        {
            if (combatSystemHost == null) return;
            combatSystemHost.Events?.Subscribe<PlayerDead>(HandlePlayerDead);
            combatSystemHost.Events?.Subscribe<TowerActivated>(HandleTowerActivated);
        }

        private void OnDisable()
        {
            if (combatSystemHost == null) return;
            combatSystemHost.Events?.Unsubscribe<PlayerDead>(HandlePlayerDead);
            combatSystemHost.Events?.Unsubscribe<TowerActivated>(HandleTowerActivated);
        }

        private void HandlePlayerDead(PlayerDead message)
        {
            if (message.PlayerId != playerActor.ActorId || restarting) return;
            StartCoroutine(RestartAtCheckpoint());
        }

        private IEnumerator RestartAtCheckpoint()
        {
            restarting = true;
            playerInputHost.enabled = false;
            playerMotorHost.ResetTransientInput();
            playerBody.linearVelocity = Vector2.zero;
            fadeCanvasGroup.blocksRaycasts = true;

            if (restartDelay > 0f) yield return new WaitForSecondsRealtime(restartDelay);
            yield return FadeTo(1f, fadeDuration);

            activeSpawnPoint = ResolveCurrentCheckpoint();
            foreach (var actor in resetActors) actor.ResetRuntime();
            if (questSequenceHost.CurrentQuestId == "QST-TUTO-008") bossArenaHost.ResetForRetry();
            MovePlayerToActiveSpawn();
            combatSystemHost.Events.Publish(new PlayerRespawned(playerActor.ActorId));

            yield return FadeTo(0f, fadeDuration);
            fadeCanvasGroup.blocksRaycasts = false;
            playerInputHost.enabled = true;
            restarting = false;
        }

        private void HandleTowerActivated(TowerActivated message)
        {
            if (message.TowerId == relayCheckpointId) activeSpawnPoint = relayCheckpointSpawnPoint;
        }

        private Transform ResolveCurrentCheckpoint()
        {
            var currentQuestId = questSequenceHost != null ? questSequenceHost.CurrentQuestId : string.Empty;
            foreach (var checkpoint in questCheckpoints)
                if (checkpoint != null && checkpoint.QuestId == currentQuestId && checkpoint.SpawnPoint != null)
                    return checkpoint.SpawnPoint;

            // TutorialRestartHost and TutorialQuestSequenceHost both initialize during Awake. Do not rely on
            // their relative execution order when restoring a saved run; derive the active quest directly
            // from persisted completion data when the sequence has not initialized yet.
            if (questCheckpoints != null && questCheckpoints.Length > 0)
            {
                var checkpointQuestIds = new string[questCheckpoints.Length];
                for (var index = 0; index < questCheckpoints.Length; index++)
                    checkpointQuestIds[index] = questCheckpoints[index]?.QuestId ?? string.Empty;

                var restoredIndex = TutorialProgressRestore.FindFirstIncompleteQuestIndex(
                    saveSystemHost.System.Current.Run,
                    checkpointQuestIds);
                if (restoredIndex >= 0 && restoredIndex < questCheckpoints.Length &&
                    questCheckpoints[restoredIndex]?.SpawnPoint != null)
                    return questCheckpoints[restoredIndex].SpawnPoint;
            }

            return TutorialProgressRestore.IsRelayProgressRestored(
                saveSystemHost.System.Current.Run,
                relayCheckpointId,
                relayCheckpointQuestId)
                ? relayCheckpointSpawnPoint
                : tutorialSpawnPoint;
        }

        private void MovePlayerToActiveSpawn()
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = activeSpawnPoint.position;
            playerActor.transform.position = activeSpawnPoint.position;
            Physics2D.SyncTransforms();
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            var startAlpha = fadeCanvasGroup.alpha;
            if (duration <= 0f)
            {
                fadeCanvasGroup.alpha = targetAlpha;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            fadeCanvasGroup.alpha = targetAlpha;
        }
    }
}
