using System;
using System.Collections.Generic;
using Narthex.Content;
using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Activates every pre-placed F-stage enemy together and keeps the exit locked
    /// until all of them are defeated.
    /// </summary>
    public sealed class TutorialSimultaneousEncounterHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private CombatSystemHost combatSystemHost;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private string encounterQuestId = "QST-TUTO-007-A";
        [SerializeField] private string clearSignalTargetId = "ENCOUNTER-A-CLEAR";
        [SerializeField] private CombatActorHost[] enemies = Array.Empty<CombatActorHost>();
        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
        [SerializeField] private Collider2D exitGateCollider;
        [SerializeField] private Renderer exitGateRenderer;

        private readonly HashSet<string> defeatedEnemyIds = new HashSet<string>();
        private bool encounterStarted;
        private bool cleared;

        public bool HasValidSetup => serviceRoot != null && combatSystemHost != null &&
                                     questSequenceHost != null && !string.IsNullOrWhiteSpace(encounterQuestId) &&
                                     !string.IsNullOrWhiteSpace(clearSignalTargetId) &&
                                     enemies != null && spawnPoints != null && enemies.Length > 0 &&
                                     enemies.Length == spawnPoints.Length && enemies.AllValid() &&
                                     spawnPoints.AllValid() && exitGateCollider != null && exitGateRenderer != null;
        public bool ActivatesAllEnemiesAtOnce => true;
        public int EnemyCount => enemies?.Length ?? 0;
        public int ActiveEnemyCount
        {
            get
            {
                if (enemies == null) return 0;
                var count = 0;
                foreach (var enemy in enemies)
                    if (enemy != null && enemy.gameObject.activeInHierarchy)
                        count++;
                return count;
            }
        }
        public bool IsCleared => cleared;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError(
                    "TutorialSimultaneousEncounterHost requires quest, enemies, spawn points, and exit gate references.",
                    this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            combatSystemHost.Initialize();
            SetGateLocked(true);
            foreach (var enemy in enemies)
                enemy.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (!HasValidSetup) return;
            serviceRoot.Events.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            combatSystemHost.Events.Subscribe<EnemyKilled>(HandleEnemyKilled);
            combatSystemHost.Events.Subscribe<PlayerRespawned>(HandlePlayerRespawned);
            RefreshForCurrentQuest();
        }

        private void Start() => TryStartEncounter(questSequenceHost.CurrentQuestId);

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            combatSystemHost?.Events?.Unsubscribe<EnemyKilled>(HandleEnemyKilled);
            combatSystemHost?.Events?.Unsubscribe<PlayerRespawned>(HandlePlayerRespawned);
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message) => TryStartEncounter(message.QuestId);

        public void RefreshForCurrentQuest()
        {
            if (!HasValidSetup || questSequenceHost.CurrentQuestId != encounterQuestId) return;
            if (!encounterStarted || !HasAnyActiveEnemy())
            {
                encounterStarted = true;
                cleared = false;
                ResetEnemiesAndGate();
            }
        }

        private void TryStartEncounter(string questId)
        {
            if (encounterStarted || cleared || questId != encounterQuestId) return;
            encounterStarted = true;
            ResetEnemiesAndGate();
        }

        private void HandleEnemyKilled(EnemyKilled message)
        {
            if (!encounterStarted || cleared || string.IsNullOrWhiteSpace(message.EnemyId)) return;
            var matchedEnemy = FindEnemy(message.EnemyId);
            if (matchedEnemy == null || !defeatedEnemyIds.Add(message.EnemyId)) return;
            matchedEnemy.gameObject.SetActive(false);
            if (defeatedEnemyIds.Count >= enemies.Length)
                CompleteEncounter();
        }

        private void HandlePlayerRespawned(PlayerRespawned message)
        {
            if (cleared || questSequenceHost.CurrentQuestId != encounterQuestId) return;
            encounterStarted = true;
            ResetEnemiesAndGate();
        }

        private void ResetEnemiesAndGate()
        {
            defeatedEnemyIds.Clear();
            SetGateLocked(true);
            for (var index = 0; index < enemies.Length; index++)
            {
                var enemy = enemies[index];
                enemy.transform.position = spawnPoints[index].position;
                enemy.gameObject.SetActive(true);
                enemy.ResetRuntime();
            }
            Physics2D.SyncTransforms();
        }

        private CombatActorHost FindEnemy(string actorId)
        {
            foreach (var enemy in enemies)
                if (enemy != null && enemy.ActorId == actorId)
                    return enemy;
            return null;
        }

        private bool HasAnyActiveEnemy()
        {
            foreach (var enemy in enemies)
                if (enemy != null && enemy.gameObject.activeInHierarchy)
                    return true;
            return false;
        }

        private void CompleteEncounter()
        {
            cleared = true;
            SetGateLocked(false);
            serviceRoot.Events.Publish(new GameplaySignal(QuestSignalType.PortalUsed, clearSignalTargetId));
        }

        private void SetGateLocked(bool locked)
        {
            exitGateCollider.enabled = locked;
            exitGateRenderer.enabled = locked;
        }
    }

    internal static class TutorialEncounterArrayExtensions
    {
        public static bool AllValid<T>(this T[] values) where T : UnityEngine.Object
        {
            if (values == null || values.Length == 0) return false;
            foreach (var value in values)
                if (value == null) return false;
            return true;
        }
    }
}
