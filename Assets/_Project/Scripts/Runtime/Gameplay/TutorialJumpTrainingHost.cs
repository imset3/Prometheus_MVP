using System.Collections;
using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Repeats a low, forward-facing projectile during the jump lesson. A hit resets
    /// both the player's position and the active jump quest's accumulated progress.
    /// </summary>
    public sealed class TutorialJumpTrainingHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private QuestManagerHost questManagerHost;
        [SerializeField] private string jumpQuestId = "QST-TUTO-002";
        [SerializeField] private Transform player;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private PlayerMotorHost playerMotor;
        [SerializeField] private Transform restartPoint;
        [SerializeField] private Transform launchPoint;
        [SerializeField] private Transform endPoint;
        [SerializeField] private GameObject projectile;
        [SerializeField] private Rigidbody2D projectileBody;
        [SerializeField] private TutorialJumpProjectileHazardHost projectileHazard;
        [SerializeField] private GameObject[] projectilePool = System.Array.Empty<GameObject>();
        [SerializeField] private Rigidbody2D[] projectileBodyPool = System.Array.Empty<Rigidbody2D>();
        [SerializeField] private TutorialJumpProjectileHazardHost[] projectileHazardPool =
            System.Array.Empty<TutorialJumpProjectileHazardHost>();
        [SerializeField, Min(0f)] private float initialDelay = 0.45f;
        [SerializeField, Min(0.1f)] private float travelDuration = 1.55f;
        [SerializeField, Min(0.1f)] private float launchInterval = 1f;
        [SerializeField, Min(0f)] private float restartDelay = 0.4f;

        private Coroutine trainingRoutine;
        private int nextProjectileIndex;

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null && questManagerHost != null &&
                                     !string.IsNullOrWhiteSpace(jumpQuestId) && player != null && playerBody != null &&
                                     playerMotor != null && restartPoint != null && launchPoint != null && endPoint != null &&
                                     HasValidProjectilePool();

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialJumpTrainingHost requires quest, player, anchor, and projectile references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            questManagerHost.Initialize();
            HideProjectiles();
        }

        private void OnEnable()
        {
            if (serviceRoot == null) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            // The phase root itself is activated by the same objective-change event.
            // Starting from the current quest here avoids depending on subscriber order.
            SetTrainingActive(questSequenceHost.CurrentQuestId == jumpQuestId);
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            if (trainingRoutine != null) StopCoroutine(trainingRoutine);
            trainingRoutine = null;
            StopAllCoroutines();
            HideProjectiles();
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message) => SetTrainingActive(message.QuestId == jumpQuestId);

        private void SetTrainingActive(bool active)
        {
            if (trainingRoutine != null)
            {
                StopCoroutine(trainingRoutine);
                trainingRoutine = null;
            }
            StopAllCoroutines();
            HideProjectiles();
            if (active) trainingRoutine = StartCoroutine(TrainingLoop(initialDelay));
        }

        private IEnumerator TrainingLoop(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            while (questSequenceHost.CurrentQuestId == jumpQuestId)
            {
                var index = nextProjectileIndex;
                nextProjectileIndex = (nextProjectileIndex + 1) % projectilePool.Length;
                if (projectilePool[index].activeSelf) HideProjectile(index);
                StartCoroutine(FlyProjectile(index));
                yield return new WaitForSeconds(launchInterval);
            }
            trainingRoutine = null;
        }

        private IEnumerator FlyProjectile(int index)
        {
            var projectileObject = projectilePool[index];
            var body = projectileBodyPool[index];
            var hazard = projectileHazardPool[index];
            body.position = launchPoint.position;
            projectileObject.transform.position = launchPoint.position;
            projectileObject.SetActive(true);
            hazard.SetArmed(true);

            var elapsed = 0f;
            while (elapsed < travelDuration && questSequenceHost.CurrentQuestId == jumpQuestId)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / travelDuration);
                body.position = Vector3.Lerp(launchPoint.position, endPoint.position, progress);
                yield return null;
            }

            HideProjectile(index);
        }

        public bool TryRestartJumpSection(Collider2D other)
        {
            if (other == null || questSequenceHost.CurrentQuestId != jumpQuestId ||
                (other.transform != player && !other.transform.IsChildOf(player)))
                return false;

            StopAllCoroutines();
            trainingRoutine = null;
            HideProjectiles();
            questManagerHost.System.ResetProgress(jumpQuestId);
            playerMotor.ResetTransientInput();
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = restartPoint.position;
            player.position = restartPoint.position;
            Physics2D.SyncTransforms();
            trainingRoutine = StartCoroutine(TrainingLoop(restartDelay));
            return true;
        }

        private void HideProjectiles()
        {
            nextProjectileIndex = 0;
            if (projectilePool == null) return;
            for (var index = 0; index < projectilePool.Length; index++)
                HideProjectile(index);
        }

        private void HideProjectile(int index)
        {
            if (projectileHazardPool != null && index < projectileHazardPool.Length &&
                projectileHazardPool[index] != null)
                projectileHazardPool[index].SetArmed(false);
            if (projectilePool != null && index < projectilePool.Length && projectilePool[index] != null)
                projectilePool[index].SetActive(false);
        }

        private bool HasValidProjectilePool()
        {
            if (projectilePool == null || projectileBodyPool == null || projectileHazardPool == null ||
                projectilePool.Length < 2 || projectileBodyPool.Length != projectilePool.Length ||
                projectileHazardPool.Length != projectilePool.Length)
                return false;
            for (var index = 0; index < projectilePool.Length; index++)
                if (projectilePool[index] == null || projectileBodyPool[index] == null ||
                    projectileHazardPool[index] == null)
                    return false;
            return true;
        }
    }
}
