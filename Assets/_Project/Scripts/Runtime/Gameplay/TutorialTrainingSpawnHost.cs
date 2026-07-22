using System.Collections;
using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Drives pre-placed training props and the tutorial enemy into the arena when
    /// their quest becomes active. Visual children remain replaceable art slots.
    /// </summary>
    public sealed class TutorialTrainingSpawnHost : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private QuestManagerHost questManagerHost;
        [SerializeField] private PlayerInputHost playerInputHost;

        [Header("Falling Props")]
        [SerializeField] private string fallingQuestId = "QST-TUTO-004";
        [SerializeField] private GameObject[] fallingObjects = new GameObject[0];
        [SerializeField] private Transform[] fallingStartPoints = new Transform[0];
        [SerializeField] private Transform[] fallingLandingPoints = new Transform[0];
        [SerializeField] private GameObject[] fallingWarnings = new GameObject[0];
        [SerializeField, Min(0.05f)] private float fallingWarningDuration = 0.45f;
        [SerializeField, Min(0f)] private float fallingStartDelay = 0.15f;
        [SerializeField, Min(0.05f)] private float fallingDuration = 0.55f;
        [SerializeField, Min(0f)] private float fallingStagger = 0.22f;
        [SerializeField, Min(0f)] private float fallingWaveDelay = 0.75f;
        [SerializeField, Min(0f)] private float postDialogueStartDelay = 0.35f;
        [SerializeField] private Transform dashRestartPoint;
        [SerializeField] private Transform player;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private PlayerMotorHost playerMotor;
        [SerializeField, Min(0f)] private float dashRestartDelay = 0.45f;

        [Header("Enemy Arrival")]
        [SerializeField] private string enemyQuestId = "QST-TUTO-003";
        [SerializeField] private GameObject tutorialEnemy;
        [SerializeField] private Transform enemySpawnPoint;
        [SerializeField] private Transform enemyLandingPoint;
        [SerializeField] private Collider2D enemyCollider;
        [SerializeField] private Behaviour enemyAttackBehaviour;
        [SerializeField] private GameObject enemySpawnWarning;
        [SerializeField, Min(0f)] private float enemyWarningDuration = 0.55f;
        [SerializeField, Min(0.05f)] private float enemyFallDuration = 0.45f;

        private bool fallingSequenceStarted;
        private bool enemySequenceStarted;
        private bool fallingStartPending;
        private bool enemyStartPending;

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null && questManagerHost != null &&
                                     playerInputHost != null &&
                                     !string.IsNullOrWhiteSpace(fallingQuestId) && fallingObjects != null &&
                                     fallingStartPoints != null && fallingLandingPoints != null &&
                                     fallingObjects.Length > 0 && fallingObjects.Length == fallingStartPoints.Length &&
                                     fallingObjects.Length == fallingLandingPoints.Length &&
                                     fallingWarnings != null && fallingObjects.Length == fallingWarnings.Length &&
                                     HasCompleteWarningReferences() && dashRestartPoint != null &&
                                     player != null && playerBody != null && playerMotor != null &&
                                     !string.IsNullOrWhiteSpace(enemyQuestId) && tutorialEnemy != null &&
                                     enemySpawnPoint != null && enemyLandingPoint != null && enemyCollider != null &&
                                     enemyAttackBehaviour != null && enemySpawnWarning != null;
        public bool FallingSequenceStarted => fallingSequenceStarted;
        public bool EnemySequenceStarted => enemySequenceStarted;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialTrainingSpawnHost requires quest, falling prop, and enemy arrival references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            questManagerHost.Initialize();
            PrepareFallingObjects();
            PrepareEnemy();
        }

        private void OnEnable()
        {
            if (serviceRoot == null) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
        }

        private void Start() => TryStartForQuest(questSequenceHost.CurrentQuestId);

        private void OnDisable() => serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);

        private void HandleObjectiveChanged(TutorialObjectiveChanged message) => TryStartForQuest(message.QuestId);

        private void TryStartForQuest(string questId)
        {
            if (questId == fallingQuestId && !fallingSequenceStarted && !fallingStartPending)
                StartCoroutine(StartFallingWhenDialogueCloses());
            if (questId == enemyQuestId && !enemySequenceStarted && !enemyStartPending)
                StartCoroutine(StartEnemyWhenDialogueCloses());
        }

        private void PrepareFallingObjects()
        {
            for (var index = 0; index < fallingObjects.Length; index++)
            {
                fallingObjects[index].transform.position = fallingStartPoints[index].position;
                fallingObjects[index].SetActive(false);
                fallingWarnings[index].SetActive(false);
            }
        }

        private void PrepareEnemy()
        {
            enemySpawnWarning.SetActive(false);
            enemyCollider.enabled = false;
            enemyAttackBehaviour.enabled = false;
            tutorialEnemy.transform.position = enemySpawnPoint.position;
            tutorialEnemy.SetActive(false);
        }

        private IEnumerator FallingSequence()
        {
            fallingSequenceStarted = true;
            if (fallingStartDelay > 0f) yield return new WaitForSeconds(fallingStartDelay);

            while (questSequenceHost.CurrentQuestId == fallingQuestId)
            {
                for (var index = 0; index < fallingObjects.Length; index++)
                {
                    yield return ShowFallingWarning(fallingWarnings[index], fallingWarningDuration);
                    StartCoroutine(DropObject(fallingObjects[index], fallingStartPoints[index], fallingLandingPoints[index], fallingDuration));
                    if (index < fallingObjects.Length - 1 && fallingStagger > 0f)
                        yield return new WaitForSeconds(fallingStagger);
                }

                yield return new WaitForSeconds(fallingDuration + fallingWaveDelay);
            }

            PrepareFallingObjects();
            fallingSequenceStarted = false;
        }

        private IEnumerator StartFallingWhenDialogueCloses()
        {
            fallingStartPending = true;
            while (playerInputHost.IsDialogueInputClaimed && questSequenceHost.CurrentQuestId == fallingQuestId)
                yield return null;
            if (postDialogueStartDelay > 0f && questSequenceHost.CurrentQuestId == fallingQuestId)
                yield return new WaitForSeconds(postDialogueStartDelay);
            fallingStartPending = false;
            if (questSequenceHost.CurrentQuestId == fallingQuestId && !fallingSequenceStarted)
                yield return FallingSequence();
        }

        private IEnumerator StartEnemyWhenDialogueCloses()
        {
            enemyStartPending = true;
            while (playerInputHost.IsDialogueInputClaimed && questSequenceHost.CurrentQuestId == enemyQuestId)
                yield return null;
            if (postDialogueStartDelay > 0f && questSequenceHost.CurrentQuestId == enemyQuestId)
                yield return new WaitForSeconds(postDialogueStartDelay);
            enemyStartPending = false;
            if (questSequenceHost.CurrentQuestId == enemyQuestId && !enemySequenceStarted)
                yield return EnemyArrivalSequence();
        }

        private IEnumerator EnemyArrivalSequence()
        {
            enemySequenceStarted = true;
            enemySpawnWarning.SetActive(true);
            if (enemyWarningDuration > 0f) yield return new WaitForSeconds(enemyWarningDuration);

            enemySpawnWarning.SetActive(false);
            tutorialEnemy.transform.position = enemySpawnPoint.position;
            tutorialEnemy.SetActive(true);
            enemyCollider.enabled = false;
            enemyAttackBehaviour.enabled = false;

            yield return MoveDown(tutorialEnemy.transform, enemySpawnPoint.position, enemyLandingPoint.position, enemyFallDuration);

            enemyCollider.enabled = true;
            enemyAttackBehaviour.enabled = true;
        }

        private IEnumerator DropObject(GameObject fallingObject, Transform startPoint, Transform landingPoint, float duration)
        {
            var originalScale = fallingObject.transform.localScale;
            fallingObject.transform.position = startPoint.position;
            fallingObject.SetActive(true);
            var hazard = fallingObject.GetComponent<TutorialFallingHazardHost>();
            if (hazard != null) hazard.SetArmed(true);
            yield return MoveDown(fallingObject.transform, startPoint.position, landingPoint.position, duration);
            fallingObject.transform.localScale = Vector3.Scale(originalScale, new Vector3(1.12f, 0.82f, 1f));
            yield return new WaitForSeconds(0.08f);
            fallingObject.transform.localScale = originalScale;
            if (hazard != null) hazard.SetArmed(false);
            yield return new WaitForSeconds(0.15f);
            fallingObject.SetActive(false);
        }

        private static IEnumerator ShowFallingWarning(GameObject warning, float duration)
        {
            var originalScale = warning.transform.localScale;
            warning.SetActive(true);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var pulse = 1f + Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI * 4f) * 0.18f;
                warning.transform.localScale = Vector3.Scale(originalScale, new Vector3(pulse, 1f, 1f));
                yield return null;
            }

            warning.transform.localScale = originalScale;
            warning.SetActive(false);
        }

        private bool HasCompleteWarningReferences()
        {
            for (var index = 0; index < fallingWarnings.Length; index++)
            {
                if (fallingWarnings[index] == null) return false;
            }

            return true;
        }

        public bool TryRestartDashSection(Collider2D other)
        {
            if (other == null || questSequenceHost.CurrentQuestId != fallingQuestId ||
                (other.transform != player && !other.transform.IsChildOf(player)))
                return false;

            StopAllCoroutines();
            fallingStartPending = false;
            enemyStartPending = false;
            questManagerHost.System.ResetProgress(fallingQuestId);
            PrepareFallingObjects();
            playerMotor.ResetTransientInput();
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = dashRestartPoint.position;
            player.position = dashRestartPoint.position;
            Physics2D.SyncTransforms();
            fallingSequenceStarted = false;
            StartCoroutine(RestartDashSequence());
            return true;
        }

        private IEnumerator RestartDashSequence()
        {
            if (dashRestartDelay > 0f) yield return new WaitForSeconds(dashRestartDelay);
            if (questSequenceHost.CurrentQuestId == fallingQuestId && !fallingSequenceStarted)
                yield return FallingSequence();
        }

        private static IEnumerator MoveDown(Transform target, Vector3 start, Vector3 end, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / duration);
                var eased = normalized * normalized;
                target.position = Vector3.LerpUnclamped(start, end, eased);
                yield return null;
            }

            target.position = end;
        }
    }
}
