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

        [Header("Optional Ladder Sequence")]
        [SerializeField] private bool useLadderSequence;
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

        private bool transitionRunning;
        private Collider2D transitionTrigger;
        private Vector2 previousPlayerPosition;
        private bool hasPreviousPlayerPosition;

        public bool UsesLadderSequence => useLadderSequence;
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

        private void LateUpdate()
        {
            if (player == null || transitionTrigger == null) return;
            var currentPlayerPosition = (Vector2)player.position;
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

        private void TryBeginTransition(Collider2D other)
        {
            if (transitionRunning || dialoguePresenter.IsShowing || !IsTransitionUnlocked() || !IsPlayer(other)) return;
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
            var playerMotor = player.GetComponent<PlayerMotorHost>();
            if (playerMotor != null) playerMotor.ResetTransientInput();
            playerInputHost.enabled = false;
            playerBody.linearVelocity = Vector2.zero;
            fadeCanvasGroup.blocksRaycasts = true;

            var restoreSimulation = playerBody.simulated;
            if (useLadderSequence)
            {
                guideCompanion.CancelGuide();
                playerBody.simulated = false;
                yield return PlayLadderSequence();
            }

            yield return FadeTo(1f, fadeOutDuration);

            nextZoneRoot.SetActive(true);
            guideCompanion.CancelGuide();
            playerBody.position = destinationSpawn.position;
            player.position = destinationSpawn.position;
            previousPlayerPosition = destinationSpawn.position;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.simulated = restoreSimulation;
            Physics2D.SyncTransforms();
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

            if (blackHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(blackHoldDuration);

            yield return FadeTo(0f, fadeInDuration);

            fadeCanvasGroup.blocksRaycasts = false;
            playerInputHost.enabled = true;
            transitionRunning = false;
            currentZoneRoot.SetActive(false);
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
                player.position = position;
                guideCompanion.transform.position = position + new Vector3(-0.85f, 0.75f, 0f);
                yield return null;
            }

            player.position = ladderExit.position;
            guideCompanion.transform.position = ladderExit.position + new Vector3(-0.85f, 0.75f, 0f);
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
