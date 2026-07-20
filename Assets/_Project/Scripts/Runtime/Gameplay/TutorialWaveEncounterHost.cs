using System.Collections;
using System.Collections.Generic;
using Narthex.Content;
using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Runs a multi-wave encounter with multiple pre-placed enemies active at once.
    /// Wave composition is defined by contiguous counts in the enemy array.
    /// </summary>
    public sealed class TutorialWaveEncounterHost : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private CombatSystemHost combatSystemHost;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;

        [Header("Encounter")]
        [SerializeField] private string encounterQuestId = "QST-TUTO-007-B";
        [SerializeField] private string clearSignalTargetId = "ENCOUNTER-B-CLEAR";
        [SerializeField] private CombatActorHost[] enemies = new CombatActorHost[0];
        [SerializeField] private Transform[] spawnPoints = new Transform[0];
        [SerializeField] private GameObject[] spawnWarnings = new GameObject[0];
        [SerializeField] private int[] waveEnemyCounts = new int[0];
        [SerializeField, Min(0f)] private float initialDelay = 0.4f;
        [SerializeField, Min(0f)] private float warningDuration = 0.55f;
        [SerializeField, Min(0f)] private float nextWaveDelay = 0.75f;

        [Header("Exit Gate")]
        [SerializeField] private Collider2D exitGateCollider;
        [SerializeField] private Renderer exitGateRenderer;

        private readonly HashSet<string> activeEnemyIds = new HashSet<string>();
        private bool encounterStarted;
        private bool cleared;
        private int currentWaveIndex = -1;
        private Coroutine waveRoutine;

        public bool HasValidSetup => serviceRoot != null && combatSystemHost != null && questSequenceHost != null &&
                                     !string.IsNullOrWhiteSpace(encounterQuestId) &&
                                     !string.IsNullOrWhiteSpace(clearSignalTargetId) && enemies != null &&
                                     spawnPoints != null && spawnWarnings != null && waveEnemyCounts != null &&
                                     enemies.Length > 0 && enemies.Length == spawnPoints.Length &&
                                     enemies.Length == spawnWarnings.Length && HasValidWaveCounts() &&
                                     exitGateCollider != null && exitGateRenderer != null;
        public bool EncounterStarted => encounterStarted;
        public bool IsCleared => cleared;
        public int CurrentWaveIndex => currentWaveIndex;
        public int ActiveEnemyCount => activeEnemyIds.Count;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialWaveEncounterHost requires valid wave, enemy, warning, and gate references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            combatSystemHost.Initialize();
            SetGateLocked(true);
            for (var index = 0; index < enemies.Length; index++)
            {
                enemies[index].gameObject.SetActive(false);
                spawnWarnings[index].SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (!HasValidSetup) return;
            serviceRoot.Events.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            combatSystemHost.Events.Subscribe<EnemyKilled>(HandleEnemyKilled);
        }

        private void Start() => TryStartEncounter(questSequenceHost.CurrentQuestId);

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            combatSystemHost?.Events?.Unsubscribe<EnemyKilled>(HandleEnemyKilled);
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message) => TryStartEncounter(message.QuestId);

        private void TryStartEncounter(string questId)
        {
            if (encounterStarted || cleared || questId != encounterQuestId) return;
            encounterStarted = true;
            waveRoutine = StartCoroutine(SpawnWaveAfterDelay(0, initialDelay));
        }

        private void HandleEnemyKilled(EnemyKilled message)
        {
            if (!encounterStarted || cleared || !activeEnemyIds.Remove(message.EnemyId)) return;
            foreach (var enemy in enemies)
            {
                if (enemy != null && enemy.ActorId == message.EnemyId)
                {
                    enemy.gameObject.SetActive(false);
                    break;
                }
            }

            if (activeEnemyIds.Count > 0) return;
            var nextWaveIndex = currentWaveIndex + 1;
            if (nextWaveIndex >= waveEnemyCounts.Length)
            {
                CompleteEncounter();
                return;
            }

            if (waveRoutine != null) StopCoroutine(waveRoutine);
            waveRoutine = StartCoroutine(SpawnWaveAfterDelay(nextWaveIndex, nextWaveDelay));
        }

        private IEnumerator SpawnWaveAfterDelay(int waveIndex, float delay)
        {
            currentWaveIndex = -1;
            if (delay > 0f) yield return new WaitForSeconds(delay);

            var startIndex = GetWaveStartIndex(waveIndex);
            var enemyCount = waveEnemyCounts[waveIndex];
            for (var offset = 0; offset < enemyCount; offset++)
            {
                var enemyIndex = startIndex + offset;
                spawnWarnings[enemyIndex].transform.position = spawnPoints[enemyIndex].position + Vector3.down * 0.45f;
                spawnWarnings[enemyIndex].SetActive(true);
            }

            if (warningDuration > 0f) yield return new WaitForSeconds(warningDuration);

            activeEnemyIds.Clear();
            for (var offset = 0; offset < enemyCount; offset++)
            {
                var enemyIndex = startIndex + offset;
                spawnWarnings[enemyIndex].SetActive(false);
                var enemy = enemies[enemyIndex];
                enemy.transform.position = spawnPoints[enemyIndex].position;
                enemy.gameObject.SetActive(true);
                activeEnemyIds.Add(enemy.ActorId);
            }

            currentWaveIndex = waveIndex;
            waveRoutine = null;
        }

        private void CompleteEncounter()
        {
            cleared = true;
            currentWaveIndex = -1;
            activeEnemyIds.Clear();
            SetGateLocked(false);
            serviceRoot.Events.Publish(new GameplaySignal(QuestSignalType.PortalUsed, clearSignalTargetId));
        }

        private int GetWaveStartIndex(int waveIndex)
        {
            var startIndex = 0;
            for (var index = 0; index < waveIndex; index++) startIndex += waveEnemyCounts[index];
            return startIndex;
        }

        private bool HasValidWaveCounts()
        {
            if (waveEnemyCounts == null || waveEnemyCounts.Length == 0) return false;
            var total = 0;
            foreach (var count in waveEnemyCounts)
            {
                if (count <= 0) return false;
                total += count;
            }

            return enemies != null && total == enemies.Length;
        }

        private void SetGateLocked(bool locked)
        {
            exitGateCollider.enabled = locked;
            exitGateRenderer.enabled = locked;
        }
    }
}
