using System.Collections;
using Narthex.Content;
using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Fades between pre-placed tutorial zone roots inside one Unity scene.
    /// Attach this component to the source zone's trigger collider.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialZoneTransitionHost : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private TutorialDialoguePresenter dialoguePresenter;
        [SerializeField] private TutorialGuideCompanionHost guideCompanion;
        [SerializeField] private CameraFollowHost cameraFollowHost;
        [SerializeField] private TutorialRestartHost restartHost;
        [SerializeField] private TutorialObjectiveBeaconHost objectiveBeacon;
        [SerializeField] private Transform player;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private CanvasGroup fadeCanvasGroup;

        [Header("Zone")]
        [SerializeField] private GameObject currentZoneRoot;
        [SerializeField] private GameObject nextZoneRoot;
        [SerializeField] private Transform destinationSpawn;
        [SerializeField] private Vector3 guideArrivalOffset = new(-1.1f, 1.1f, 0f);
        [SerializeField] private string requiredQuestId = "QST-TUTO-001";
        [SerializeField] private string portalSignalTargetId = "TUTORIAL-HQ-EXIT";
        [SerializeField] private string destinationCheckpointQuestId;
        [SerializeField] private Transform destinationObjectiveTarget;
        [SerializeField] private GameObject[] activateOnArrival = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject[] deactivateOnArrival = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject[] deactivateOnCompletion = System.Array.Empty<GameObject>();

        [Header("Optional Ladder Sequence")]
        [SerializeField] private bool useLadderSequence;
        [SerializeField] private bool requireInteractInput;
        [SerializeField] private Transform ladderEntry;
        [SerializeField] private Transform ladderExit;
        [SerializeField] private GameObject ladderVisual;
        [SerializeField, Min(0.1f)] private float ladderMoveDuration = 1.15f;
        [SerializeField, Min(0f)] private float ladderExitHoldDuration = 0.12f;
        [SerializeField, Min(0f)] private float ladderStepSway = 0.07f;

        [Header("Destination Camera")]
        [SerializeField] private float destinationCameraMinX;
        [SerializeField] private float destinationCameraMaxX;
        [SerializeField] private float destinationCameraFixedY;
        [SerializeField] private bool destinationCameraTracksVertical;
        [SerializeField] private float destinationCameraMinY;
        [SerializeField] private float destinationCameraMaxY;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
        [SerializeField, Min(0f)] private float blackHoldDuration = 0.15f;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.45f;
        [SerializeField, Min(0.5f)] private float maximumSweptCrossingDistance = 6f;
        [SerializeField, Min(0f)] private float destinationSpawnVerticalClearance = 0.12f;
        [SerializeField, Range(1, 4)] private int destinationSettleFixedSteps = 2;

        private bool transitionRunning;
        private Collider2D transitionTrigger;
        private Vector2 previousPlayerPosition;
        private bool hasPreviousPlayerPosition;
        private bool playerInsideTrigger;

        public bool UsesLadderSequence => useLadderSequence;
        public bool RequiresInteraction => requireInteractInput;
        public bool LadderMovesUp => !useLadderSequence || ladderEntry != null && ladderExit != null &&
                                     ladderExit.position.y > ladderEntry.position.y;
        public bool DestinationTracksVertical => destinationCameraTracksVertical;
        public float DestinationCameraMinY => destinationCameraMinY;
        public float DestinationCameraMaxY => destinationCameraMaxY;
        public bool UsesSweptPlayerDetection => true;
        public bool HasValidLadderSetup => !useLadderSequence ||
                                           (ladderEntry != null && ladderExit != null && ladderVisual != null);

        public bool HasValidSetup => serviceRoot != null && playerInputHost != null && questSequenceHost != null &&
                                     dialoguePresenter != null &&
                                     guideCompanion != null && cameraFollowHost != null && player != null &&
                                     playerBody != null && fadeCanvasGroup != null && currentZoneRoot != null &&
                                     nextZoneRoot != null && destinationSpawn != null &&
                                     !string.IsNullOrWhiteSpace(requiredQuestId) &&
                                     destinationCameraMinX <= destinationCameraMaxX && HasValidLadderSetup &&
                                     maximumSweptCrossingDistance > 0f;

        private void Awake()
        {
            transitionTrigger = GetComponent<Collider2D>();
            if (transitionTrigger != null) transitionTrigger.isTrigger = true;

            if (!HasValidSetup)
            {
                Debug.LogError("TutorialZoneTransitionHost requires pre-placed zone, player, camera, dialogue, and fade references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.interactable = false;
            previousPlayerPosition = player.position;
            hasPreviousPlayerPosition = true;
        }

        private void OnEnable()
        {
            if (playerInputHost != null)
                playerInputHost.InteractRequested += HandleInteractRequested;
        }

        private void OnDisable()
        {
            if (playerInputHost != null)
                playerInputHost.InteractRequested -= HandleInteractRequested;
            playerInsideTrigger = false;
        }

        private void LateUpdate()
        {
            if (player == null || transitionTrigger == null) return;
            var currentPlayerPosition = (Vector2)player.position;
            if (requireInteractInput)
            {
                playerInsideTrigger = transitionTrigger.enabled &&
                                      transitionTrigger.bounds.Contains(
                                          new Vector3(currentPlayerPosition.x, currentPlayerPosition.y, 0f));
                previousPlayerPosition = currentPlayerPosition;
                hasPreviousPlayerPosition = true;
                return;
            }

            var displacement = currentPlayerPosition - previousPlayerPosition;
            if (transitionTrigger.enabled && !transitionRunning && !dialoguePresenter.IsShowing && IsTransitionUnlocked() &&
                hasPreviousPlayerPosition &&
                displacement.sqrMagnitude <= maximumSweptCrossingDistance * maximumSweptCrossingDistance &&
                TutorialTriggerSweepPolicy.Intersects(
                    transitionTrigger.bounds,
                    previousPlayerPosition,
                    currentPlayerPosition))
                StartCoroutine(TransitionRoutine());

            previousPlayerPosition = currentPlayerPosition;
            hasPreviousPlayerPosition = true;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryBeginTransition(other);
        private void OnTriggerStay2D(Collider2D other) => TryBeginTransition(other);
        private void OnTriggerExit2D(Collider2D other)
        {
            if (requireInteractInput && IsPlayer(other)) playerInsideTrigger = false;
        }

        private void TryBeginTransition(Collider2D other)
        {
            if (transitionRunning || dialoguePresenter.IsShowing || !IsTransitionUnlocked() || !IsPlayer(other)) return;
            if (requireInteractInput)
            {
                playerInsideTrigger = true;
                return;
            }
            StartCoroutine(TransitionRoutine());
        }

        private void HandleInteractRequested()
        {
            if (!requireInteractInput || !playerInsideTrigger || transitionRunning ||
                dialoguePresenter.IsShowing || !IsTransitionUnlocked())
                return;
            StartCoroutine(TransitionRoutine());
        }

        private bool IsTransitionUnlocked() => questSequenceHost.CurrentQuestId == requiredQuestId;

        private bool IsPlayer(Collider2D other)
        {
            if (other == null) return false;
            return other.transform == player || other.transform.IsChildOf(player);
        }

        private IEnumerator TransitionRoutine()
        {
            transitionRunning = true;
            playerInsideTrigger = false;
            var playerMotor = player.GetComponent<PlayerMotorHost>();
            if (playerMotor != null) playerMotor.ResetTransientInput();
            playerInputHost.enabled = false;
            playerBody.linearVelocity = Vector2.zero;
            fadeCanvasGroup.blocksRaycasts = true;

            var restoreSimulation = playerBody.simulated;
            if (useLadderSequence)
            {
                guideCompanion.CancelGuide();
                playerBody.linearVelocity = Vector2.zero;
                playerBody.angularVelocity = 0f;
                playerBody.simulated = false;
                playerBody.position = ladderEntry.position;
                player.position = ladderEntry.position;
                Physics2D.SyncTransforms();
                yield return PlayLadderSequence();
            }

            yield return FadeTo(1f, fadeOutDuration);

            // Source-zone technical roots can live outside currentZoneRoot. Disable their
            // colliders while the screen is fully black so they cannot overlap the
            // destination spawn. Do not deactivate the roots yet: this transition host can
            // itself be one of their children, which would stop this coroutine mid-fade.
            SetCollidersEnabled(deactivateOnCompletion, false);
            nextZoneRoot.SetActive(true);
            SetActive(deactivateOnArrival, false);
            SetActive(activateOnArrival, true);
            guideCompanion.CancelGuide();
            var safeDestination = destinationSpawn.position + Vector3.up * destinationSpawnVerticalClearance;
            playerBody.position = safeDestination;
            player.position = safeDestination;
            previousPlayerPosition = safeDestination;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
            playerBody.simulated = restoreSimulation;
            Physics2D.SyncTransforms();
            // Give Physics2D one simulation step with zero velocity before input is
            // restored. This prevents a carry-over contact impulse at destination
            // floors (notably the Nadir dock entry) from bouncing Prome into a tile.
            if (restoreSimulation)
            {
                for (var step = 0; step < destinationSettleFixedSteps; step++)
                {
                    yield return new WaitForFixedUpdate();
                    playerBody.linearVelocity = Vector2.zero;
                    playerBody.angularVelocity = 0f;
                    Physics2D.SyncTransforms();
                }
            }
            if (playerMotor != null) playerMotor.ResetTransientInput();
            guideCompanion.transform.position = destinationSpawn.position + guideArrivalOffset;
            if (destinationCameraTracksVertical)
            {
                cameraFollowHost.SetTrackingBounds(
                    destinationCameraMinX,
                    destinationCameraMaxX,
                    destinationCameraMinY,
                    destinationCameraMaxY,
                    true);
            }
            else
            {
                cameraFollowHost.SetBounds(
                    destinationCameraMinX,
                    destinationCameraMaxX,
                    destinationCameraFixedY,
                    true);
            }
            serviceRoot.Events.Publish(new GameplaySignal(QuestSignalType.PortalUsed, portalSignalTargetId));
            serviceRoot.Events.Publish(new TutorialLocationChanged(nextZoneRoot.name));
            if (restartHost != null && !string.IsNullOrWhiteSpace(destinationCheckpointQuestId))
                restartHost.SetRuntimeCheckpoint(destinationCheckpointQuestId, destinationSpawn);
            if (objectiveBeacon != null)
                objectiveBeacon.SetExternalTarget(destinationObjectiveTarget);

            if (blackHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(blackHoldDuration);

            yield return FadeTo(0f, fadeInDuration);

            fadeCanvasGroup.blocksRaycasts = false;
            playerInputHost.enabled = true;
            transitionRunning = false;
            currentZoneRoot.SetActive(false);
            SetActive(deactivateOnCompletion, false);
        }

        private static void SetActive(GameObject[] targets, bool active)
        {
            if (targets == null) return;
            foreach (var target in targets)
                if (target != null) target.SetActive(active);
        }

        private static void SetCollidersEnabled(GameObject[] targets, bool enabled)
        {
            if (targets == null) return;
            foreach (var target in targets)
            {
                if (target == null) continue;
                foreach (var collider in target.GetComponentsInChildren<Collider2D>(true))
                    collider.enabled = enabled;
            }
        }

        private IEnumerator PlayLadderSequence()
        {
            var elapsed = 0f;
            while (elapsed < ladderMoveDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / ladderMoveDuration);
                var eased = progress * progress * (3f - (2f * progress));
                var position = Vector3.Lerp(ladderEntry.position, ladderExit.position, eased);
                position.x += Mathf.Sin(progress * Mathf.PI * 8f) * ladderStepSway;
                playerBody.position = position;
                player.position = position;
                guideCompanion.transform.position = position + new Vector3(-0.85f, 0.75f, 0f);
                Physics2D.SyncTransforms();
                yield return null;
            }

            playerBody.position = ladderExit.position;
            player.position = ladderExit.position;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
            guideCompanion.transform.position = ladderExit.position + new Vector3(-0.85f, 0.75f, 0f);
            Physics2D.SyncTransforms();
            if (ladderExitHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(ladderExitHoldDuration);
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
