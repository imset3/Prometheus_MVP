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
        [SerializeField, Min(0f)] private float initialDelay = 0.45f;
        [SerializeField, Min(0.1f)] private float travelDuration = 1.55f;
        [SerializeField, Min(0f)] private float repeatDelay = 0.5f;
        [SerializeField, Min(0f)] private float restartDelay = 0.4f;

        private Coroutine trainingRoutine;

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null && questManagerHost != null &&
                                     !string.IsNullOrWhiteSpace(jumpQuestId) && player != null && playerBody != null &&
                                     playerMotor != null && restartPoint != null && launchPoint != null && endPoint != null &&
                                     projectile != null && projectileBody != null && projectileHazard != null;

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
            HideProjectile();
        }

        private void OnEnable()
        {
            if (serviceRoot == null) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
        }

        private void Start() => SetTrainingActive(questSequenceHost.CurrentQuestId == jumpQuestId);

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            if (trainingRoutine != null) StopCoroutine(trainingRoutine);
            trainingRoutine = null;
            HideProjectile();
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message) => SetTrainingActive(message.QuestId == jumpQuestId);

        private void SetTrainingActive(bool active)
        {
            if (trainingRoutine != null)
            {
                StopCoroutine(trainingRoutine);
                trainingRoutine = null;
            }
            HideProjectile();
            if (active) trainingRoutine = StartCoroutine(TrainingLoop(initialDelay));
        }

        private IEnumerator TrainingLoop(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            while (questSequenceHost.CurrentQuestId == jumpQuestId)
            {
                projectileBody.position = launchPoint.position;
                projectile.transform.position = launchPoint.position;
                projectile.SetActive(true);
                projectileHazard.SetArmed(true);

                var elapsed = 0f;
                while (elapsed < travelDuration && questSequenceHost.CurrentQuestId == jumpQuestId)
                {
                    elapsed += Time.deltaTime;
                    var progress = Mathf.Clamp01(elapsed / travelDuration);
                    projectileBody.position = Vector3.Lerp(launchPoint.position, endPoint.position, progress);
                    yield return null;
                }

                HideProjectile();
                if (repeatDelay > 0f) yield return new WaitForSeconds(repeatDelay);
            }
            trainingRoutine = null;
        }

        public bool TryRestartJumpSection(Collider2D other)
        {
            if (other == null || questSequenceHost.CurrentQuestId != jumpQuestId ||
                (other.transform != player && !other.transform.IsChildOf(player)))
                return false;

            if (trainingRoutine != null) StopCoroutine(trainingRoutine);
            trainingRoutine = null;
            HideProjectile();
            questManagerHost.System.ResetProgress(jumpQuestId);
            playerMotor.ResetTransientInput();
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = restartPoint.position;
            player.position = restartPoint.position;
            Physics2D.SyncTransforms();
            trainingRoutine = StartCoroutine(TrainingLoop(restartDelay));
            return true;
        }

        private void HideProjectile()
        {
            if (projectileHazard != null) projectileHazard.SetArmed(false);
            if (projectile != null) projectile.SetActive(false);
        }
    }
}
