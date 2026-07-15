using Narthex.Core;
using Narthex.Save;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class TutorialRestartHost : MonoBehaviour
    {
        [SerializeField] private CombatSystemHost combatSystemHost;
        [SerializeField] private SaveSystemHost saveSystemHost;
        [SerializeField] private CombatActorHost playerActor;
        [SerializeField] private Transform tutorialSpawnPoint;
        [SerializeField] private Transform relayCheckpointSpawnPoint;
        [SerializeField] private string relayCheckpointId = "RELAY-TUTO-001";
        [SerializeField] private string relayCheckpointQuestId = "QST-TUTO-007";
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private CombatActorHost[] resetActors;

        private Transform activeSpawnPoint;

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
        public string ActiveSpawnPointName => activeSpawnPoint != null ? activeSpawnPoint.name : string.Empty;

        private void Awake()
        {
            if (combatSystemHost == null || saveSystemHost == null || playerActor == null || tutorialSpawnPoint == null || !HasValidCheckpointSetup || playerBody == null)
            {
                Debug.LogError("TutorialRestartHost requires pre-placed combat, save, player, start/checkpoint spawns, and Rigidbody2D references.", this);
                enabled = false;
                return;
            }

            combatSystemHost.Initialize();
            if (!saveSystemHost.Initialize())
            {
                enabled = false;
                return;
            }

            activeSpawnPoint = TutorialProgressRestore.IsRelayProgressRestored(
                saveSystemHost.System.Current.Run,
                relayCheckpointId,
                relayCheckpointQuestId)
                ? relayCheckpointSpawnPoint
                : tutorialSpawnPoint;
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
            if (message.PlayerId != playerActor.ActorId) return;

            MovePlayerToActiveSpawn();
            foreach (var actor in resetActors) actor.ResetRuntime();
            combatSystemHost.Events.Publish(new PlayerRespawned(playerActor.ActorId));
        }

        private void HandleTowerActivated(TowerActivated message)
        {
            if (message.TowerId == relayCheckpointId) activeSpawnPoint = relayCheckpointSpawnPoint;
        }

        private void MovePlayerToActiveSpawn()
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = activeSpawnPoint.position;
            playerActor.transform.position = activeSpawnPoint.position;
            Physics2D.SyncTransforms();
        }
    }
}
