using System;
using System.Collections;
using Narthex.Content;
using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Returns the player from the second corridor visit to the reused meeting room,
    /// then holds the next exit until the emergency reunion dialogue is complete.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialEmergencyMeetingArrivalHost : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private TutorialDialoguePresenter dialoguePresenter;
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private PlayerMotorHost playerMotor;
        [SerializeField] private Transform player;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private TutorialGuideCompanionHost guideCompanion;
        [SerializeField] private CameraFollowHost cameraFollowHost;
        [SerializeField] private TutorialObjectiveBeaconHost objectiveBeacon;
        [SerializeField] private TutorialRestartHost restartHost;
        [SerializeField] private CanvasGroup fadeCanvasGroup;

        [Header("Zone")]
        [SerializeField] private GameObject corridorRoot;
        [SerializeField] private GameObject meetingRoot;
        [SerializeField] private Transform meetingSpawn;
        [SerializeField] private GameObject meetingDepartureTrigger;
        [SerializeField] private Transform meetingDepartureTarget;
        [SerializeField] private string requiredQuestId = "QST-TUTO-007";
        [SerializeField] private string portalSignalTargetId = "TUTORIAL-C02-TO-A03";
        [SerializeField] private Vector3 guideArrivalOffset = new(-1.1f, 1.1f, 0f);
        [SerializeField] private float meetingCameraMinX = -6.5f;
        [SerializeField] private float meetingCameraMaxX = 6.5f;
        [SerializeField] private float meetingCameraY;

        [Header("Dialogue")]
        [SerializeField] private string stageId = "아다마스 본부 회의장 · TUTO_A_03";
        [SerializeField, TextArea(2, 5)] private string[] dialogueLines = Array.Empty<string>();

        [Header("Timing")]
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.28f;
        [SerializeField, Min(0f)] private float blackHoldDuration = 0.12f;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.38f;

        private Collider2D arrivalTrigger;
        private bool transitionRunning;

        public bool HasValidSetup =>
            serviceRoot != null && questSequenceHost != null && dialoguePresenter != null &&
            playerInputHost != null && playerMotor != null && player != null && playerBody != null &&
            guideCompanion != null && cameraFollowHost != null && objectiveBeacon != null &&
            restartHost != null && fadeCanvasGroup != null && corridorRoot != null && meetingRoot != null &&
            meetingSpawn != null && meetingDepartureTrigger != null && meetingDepartureTarget != null &&
            !string.IsNullOrWhiteSpace(requiredQuestId) &&
            !string.IsNullOrWhiteSpace(portalSignalTargetId) &&
            !string.IsNullOrWhiteSpace(stageId) && dialogueLines != null && dialogueLines.Length > 0 &&
            meetingCameraMinX <= meetingCameraMaxX;

        public int DialogueLineCount => dialogueLines?.Length ?? 0;
        public bool LocksDepartureUntilDialogue => true;

        private void Awake()
        {
            arrivalTrigger = GetComponent<Collider2D>();
            if (arrivalTrigger != null) arrivalTrigger.isTrigger = true;
            if (meetingDepartureTrigger != null) meetingDepartureTrigger.SetActive(false);

            if (!HasValidSetup)
            {
                Debug.LogError(
                    "TutorialEmergencyMeetingArrivalHost requires player, dialogue, zone, checkpoint, departure, and fade references.",
                    this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
        }

        private void OnTriggerEnter2D(Collider2D other) => TryBegin(other);
        private void OnTriggerStay2D(Collider2D other) => TryBegin(other);

        private void TryBegin(Collider2D other)
        {
            if (transitionRunning || dialoguePresenter.IsShowing ||
                questSequenceHost.CurrentQuestId != requiredQuestId || !IsPlayer(other))
                return;

            transitionRunning = true;
            if (arrivalTrigger != null) arrivalTrigger.enabled = false;
            StartCoroutine(ArrivalRoutine());
        }

        private bool IsPlayer(Collider2D other)
        {
            if (other == null) return false;
            var candidate = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
            return candidate == player || candidate.IsChildOf(player) || player.IsChildOf(candidate);
        }

        private IEnumerator ArrivalRoutine()
        {
            playerMotor.ResetTransientInput();
            playerInputHost.AcquireInputLock(PlayerInputLockReason.Cutscene);
            playerBody.linearVelocity = Vector2.zero;
            fadeCanvasGroup.blocksRaycasts = true;

            yield return FadeTo(1f, fadeOutDuration);
            if (blackHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(blackHoldDuration);

            meetingRoot.SetActive(true);
            playerBody.position = meetingSpawn.position;
            player.position = meetingSpawn.position;
            playerBody.linearVelocity = Vector2.zero;
            guideCompanion.CancelGuide();
            guideCompanion.transform.position = meetingSpawn.position + guideArrivalOffset;
            cameraFollowHost.SetBounds(meetingCameraMinX, meetingCameraMaxX, meetingCameraY, true);
            objectiveBeacon.SetExternalTarget(null);
            restartHost.SetRuntimeCheckpoint(requiredQuestId, meetingSpawn);
            Physics2D.SyncTransforms();
            serviceRoot.Events.Publish(new GameplaySignal(QuestSignalType.PortalUsed, portalSignalTargetId));
            serviceRoot.Events.Publish(new TutorialLocationChanged(meetingRoot.name));

            corridorRoot.SetActive(false);
            yield return FadeTo(0f, fadeInDuration);

            fadeCanvasGroup.blocksRaycasts = false;
            playerInputHost.ReleaseInputLock(PlayerInputLockReason.Cutscene);
            serviceRoot.Events.Publish(new TutorialNarrativeChanged(requiredQuestId, stageId, dialogueLines));

            yield return null;
            while (dialoguePresenter.IsShowing)
                yield return null;

            meetingDepartureTrigger.SetActive(true);
            objectiveBeacon.SetExternalTarget(meetingDepartureTarget);
            transitionRunning = false;
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
