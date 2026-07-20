using System.Collections;
using Narthex.Content;
using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Runs a pre-placed tutorial encounter one enemy at a time. The next enemy
    /// appears only after the current one dies, then the exit gate opens.
    /// </summary>
    public sealed class TutorialSequentialEncounterHost : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private CombatSystemHost combatSystemHost;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;

        [Header("Encounter")]
        [SerializeField] private string encounterQuestId = "QST-TUTO-007-A";
        [SerializeField] private string clearSignalTargetId = "ENCOUNTER-A-CLEAR";
        [SerializeField] private CombatActorHost[] enemies = new CombatActorHost[0];
        [SerializeField] private Transform[] spawnPoints = new Transform[0];
        [SerializeField] private GameObject spawnWarning;
        [SerializeField, Min(0f)] private float initialDelay = 0.35f;
        [SerializeField, Min(0f)] private float spawnWarningDuration = 0.45f;
        [SerializeField, Min(0f)] private float nextEnemyDelay = 0.55f;

        [Header("Exit Gate")]
        [SerializeField] private Collider2D exitGateCollider;
        [SerializeField] private Renderer exitGateRenderer;

        private int activeEnemyIndex = -1;
        private bool encounterStarted;
        private bool cleared;
        private Coroutine spawnRoutine;

        public bool HasValidSetup => serviceRoot != null && combatSystemHost != null && questSequenceHost != null &&
                                     !string.IsNullOrWhiteSpace(encounterQuestId) &&
                                     !string.IsNullOrWhiteSpace(clearSignalTargetId) && enemies != null &&
                                     spawnPoints != null && enemies.Length > 0 && enemies.Length == spawnPoints.Length &&
                                     spawnWarning != null && exitGateCollider != null && exitGateRenderer != null;
        public bool EncounterStarted => encounterStarted;
        public bool IsCleared => cleared;
        public int ActiveEnemyIndex => activeEnemyIndex;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialSequentialEncounterHost requires quest, enemy, spawn, warning, and gate references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            combatSystemHost.Initialize();
            spawnWarning.SetActive(false);
            SetGateLocked(true);
            foreach (var enemy in enemies)
                if (enemy != null) enemy.gameObject.SetActive(false);
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
            spawnRoutine = StartCoroutine(SpawnEnemyAfterDelay(0, initialDelay));
        }

        private void HandleEnemyKilled(EnemyKilled message)
        {
            if (!encounterStarted || cleared || activeEnemyIndex < 0 || activeEnemyIndex >= enemies.Length) return;
            var activeEnemy = enemies[activeEnemyIndex];
            if (activeEnemy == null || message.EnemyId != activeEnemy.ActorId) return;

            activeEnemy.gameObject.SetActive(false);
            var nextIndex = activeEnemyIndex + 1;
            if (nextIndex >= enemies.Length)
            {
                CompleteEncounter();
                return;
            }

            if (spawnRoutine != null) StopCoroutine(spawnRoutine);
            spawnRoutine = StartCoroutine(SpawnEnemyAfterDelay(nextIndex, nextEnemyDelay));
        }

        private IEnumerator SpawnEnemyAfterDelay(int enemyIndex, float delay)
        {
            activeEnemyIndex = -1;
            if (delay > 0f) yield return new WaitForSeconds(delay);

            spawnWarning.transform.position = spawnPoints[enemyIndex].position + Vector3.down * 0.45f;
            spawnWarning.SetActive(true);
            if (spawnWarningDuration > 0f) yield return new WaitForSeconds(spawnWarningDuration);
            spawnWarning.SetActive(false);

            var enemy = enemies[enemyIndex];
            enemy.transform.position = spawnPoints[enemyIndex].position;
            enemy.gameObject.SetActive(true);
            activeEnemyIndex = enemyIndex;
            spawnRoutine = null;
        }

        private void CompleteEncounter()
        {
            cleared = true;
            activeEnemyIndex = -1;
            spawnWarning.SetActive(false);
            SetGateLocked(false);
            serviceRoot.Events.Publish(new GameplaySignal(QuestSignalType.PortalUsed, clearSignalTargetId));
        }

        private void SetGateLocked(bool locked)
        {
            exitGateCollider.enabled = locked;
            exitGateRenderer.enabled = locked;
        }
    }
}
