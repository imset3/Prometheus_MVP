using System;
using System.Collections;
using System.Collections.Generic;
using Narthex.Core;
using Narthex.Gameplay;
using Narthex.Save;
using UnityEngine;

namespace Narthex.Presentation
{
    public enum TutorialChapter0IntroState
    {
        MeetingDialogue,
        SeekHiddenRoom,
        HiddenRoomEntryDialogue,
        SeekLedge,
        HiddenRoomBriefing,
        SeekPasskey,
        ReturnToMeeting,
        SeekTrainingExit,
        Complete
    }

    public static class TutorialChapter0IntroProgress
    {
        public const string MeetingStageId = "TUTO_A_01";
        public const string HiddenRoomStageId = "TUTO_B_01";
        public const string ReturnStageId = "TUTO_A_RETURN";
        public const string PasskeyItemId = "ITEM-ZENITH-AIRSHIP-PASSKEY";

        public static TutorialChapter0IntroState Resolve(string savedStageId, bool hasPasskey)
        {
            if (hasPasskey || savedStageId == ReturnStageId) return TutorialChapter0IntroState.SeekTrainingExit;
            if (savedStageId == HiddenRoomStageId) return TutorialChapter0IntroState.HiddenRoomEntryDialogue;
            if (savedStageId == MeetingStageId) return TutorialChapter0IntroState.SeekHiddenRoom;
            return TutorialChapter0IntroState.MeetingDialogue;
        }

        public static bool ContainsPasskey(ICollection<string> itemIds)
        {
            return itemIds != null && itemIds.Contains(PasskeyItemId);
        }

        public static bool HasReachedHiddenRoomEntry(Vector3 playerPosition, Vector3 targetPosition, bool isOverlapping)
        {
            if (isOverlapping) return true;
            var delta = playerPosition - targetPosition;
            return delta.x <= 1.5f && Mathf.Abs(delta.y) <= 4f;
        }
    }

    public static class TutorialUpdraftPolicy
    {
        public static float ResolveVerticalVelocity(
            float currentVelocity,
            float liftAcceleration,
            float maximumRiseSpeed,
            float gravityMagnitude,
            float fixedDeltaTime)
        {
            var upwardVelocity = Mathf.Max(0f, currentVelocity);
            var accelerated = Mathf.MoveTowards(
                upwardVelocity,
                maximumRiseSpeed,
                Mathf.Max(0f, liftAcceleration) * fixedDeltaTime);
            var gravityCompensation = Mathf.Max(0f, gravityMagnitude) * fixedDeltaTime;
            return Mathf.Min(maximumRiseSpeed, accelerated + gravityCompensation);
        }
    }

    /// <summary>
    /// Owns the revised Notion A/B introduction inside the existing single TutorialScene.
    /// Every referenced visual and trigger is authored by the editor setup; runtime only toggles state.
    /// </summary>
    public sealed class TutorialChapter0IntroFlowHost : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private SaveSystemHost saveSystemHost;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private TutorialDialoguePresenter dialoguePresenter;
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private PlayerMotorHost playerMotorHost;
        [SerializeField] private CameraFollowHost cameraFollowHost;
        [SerializeField] private TutorialGuideCompanionHost guideCompanion;
        [SerializeField] private TutorialGuideRouteHost legacyGuideRoute;
        [SerializeField] private TutorialObjectiveBeaconHost objectiveBeacon;

        [Header("Player and transition")]
        [SerializeField] private Transform player;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private CanvasGroup fadeCanvasGroup;
        [SerializeField] private Collider2D trainingExitTransitionTrigger;
        [SerializeField] private GameObject hiddenRoomRoot;
        [SerializeField] private Transform hiddenRoomSpawn;
        [SerializeField] private Transform meetingReturnSpawn;
        [SerializeField] private Transform hiddenRoomEntryTarget;
        [SerializeField] private Transform ledgeTarget;
        [SerializeField] private Transform passkeyTarget;
        [SerializeField] private Transform hiddenRoomReturnTarget;
        [SerializeField] private Transform trainingExitTarget;
        [SerializeField] private Collider2D hiddenRoomEntryTrigger;
        [SerializeField] private Collider2D ledgeTrigger;
        [SerializeField] private Collider2D passkeyTrigger;
        [SerializeField] private Collider2D hiddenRoomReturnTrigger;

        [Header("Pre-placed visuals")]
        [SerializeField] private GameObject passkeyVisual;
        [SerializeField] private GameObject theusFlashlightVisual;
        [SerializeField] private GameObject wrongWayAlarmVisual;
        [SerializeField] private GameObject glideInstructionRoot;
        [SerializeField] private RectTransform glideKeyVisual;
        [SerializeField] private GameObject updraftVisual;

        [Header("Room camera")]
        [SerializeField] private float hiddenCameraMinX = -213f;
        [SerializeField] private float hiddenCameraMaxX = -185f;
        [SerializeField] private float hiddenCameraMinY = -2.5f;
        [SerializeField] private float hiddenCameraMaxY = 4f;
        [SerializeField] private float meetingCameraMinX = -31.1f;
        [SerializeField] private float meetingCameraMaxX = 31.1f;
        [SerializeField] private float meetingCameraY = 1f;

        [Header("Interaction tuning")]
        [SerializeField] private string openingQuestId = "QST-TUTO-001";
        [SerializeField] private float wrongWayThresholdX = -18f;
        [SerializeField, Min(0f)] private float wrongWayResetDistance = 1.5f;
        [SerializeField, Min(0.1f)] private float transitionFadeOut = 0.35f;
        [SerializeField, Min(0f)] private float transitionBlackHold = 0.12f;
        [SerializeField, Min(0.1f)] private float transitionFadeIn = 0.45f;
        [SerializeField] private Vector2 updraftMin = new(-204f, -5f);
        [SerializeField] private Vector2 updraftMax = new(-190f, 3.8f);
        [SerializeField, Min(0f)] private float updraftLiftSpeed = 5.5f;
        [SerializeField, Min(0f)] private float updraftMaxRiseSpeed = 3.5f;

        private static readonly string[][] WrongWayResponses =
        {
            new[] { "테우스: 이쪽이 아니야!" },
            new[] { "테우스: 이쪽이 아니라고!" },
            new[] { "테우스: 이쪽이 아니라니까! (x_x)" },
            new[]
            {
                "테우스: 이ㅉ... 자폭 시퀀스 발동. 3... 2...",
                "테우스: 뻥!",
                "테우스: 이야, 이제 그만 저쪽으로 가자. 프로메! 나의 대사 알고리즘은 여기까지라고!"
            }
        };

        private TutorialChapter0IntroState state;
        private int wrongWayCount;
        private bool wrongWayArmed = true;
        private bool auxiliaryDialogue;
        private bool resumeGuideAfterDialogue;
        private bool transitionRunning;
        private bool meetingDepartureLineShown;
        private bool glideLaunchLineShown;
        private Vector2 previousPlayerPosition;
        private Vector3 glideKeyBaseScale = Vector3.one;

        public TutorialChapter0IntroState State => state;
        public bool HasPasskey => saveSystemHost != null && saveSystemHost.System != null &&
                                  TutorialChapter0IntroProgress.ContainsPasskey(
                                      saveSystemHost.System.Current.Run.CollectedItemIds);
        public bool HasValidSetup => serviceRoot != null && saveSystemHost != null && questSequenceHost != null &&
                                     dialoguePresenter != null && playerInputHost != null && playerMotorHost != null &&
                                     cameraFollowHost != null && guideCompanion != null && legacyGuideRoute != null &&
                                     objectiveBeacon != null && player != null && playerBody != null && playerCollider != null &&
                                     fadeCanvasGroup != null && trainingExitTransitionTrigger != null && hiddenRoomRoot != null &&
                                     hiddenRoomSpawn != null && meetingReturnSpawn != null && hiddenRoomEntryTarget != null &&
                                     ledgeTarget != null && passkeyTarget != null && hiddenRoomReturnTarget != null && trainingExitTarget != null &&
                                     hiddenRoomEntryTrigger != null && ledgeTrigger != null && passkeyTrigger != null && hiddenRoomReturnTrigger != null &&
                                     passkeyVisual != null && theusFlashlightVisual != null && wrongWayAlarmVisual != null &&
                                     glideInstructionRoot != null && updraftVisual != null;
        public bool HasValidUpdraftSetup => updraftMax.x > updraftMin.x &&
                                            updraftMax.y > updraftMin.y &&
                                            updraftMax.y <= hiddenCameraMaxY + 0.01f &&
                                            passkeyTarget != null && updraftMax.y > passkeyTarget.position.y &&
                                            updraftLiftSpeed > 0f && updraftMaxRiseSpeed > 0f;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialChapter0IntroFlowHost requires all pre-placed A/B room, UI, and system references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            if (!saveSystemHost.Initialize())
            {
                enabled = false;
                return;
            }

            legacyGuideRoute.enabled = false;
            trainingExitTransitionTrigger.enabled = false;
            hiddenRoomRoot.SetActive(false);
            theusFlashlightVisual.SetActive(false);
            wrongWayAlarmVisual.SetActive(false);
            glideInstructionRoot.SetActive(false);
            updraftVisual.SetActive(false);
            previousPlayerPosition = player.position;
            if (glideKeyVisual != null) glideKeyBaseScale = glideKeyVisual.localScale;
        }

        private void OnEnable()
        {
            if (dialoguePresenter != null) dialoguePresenter.DialogueClosed += HandleDialogueClosed;
        }

        private void OnDisable()
        {
            if (dialoguePresenter != null) dialoguePresenter.DialogueClosed -= HandleDialogueClosed;
            objectiveBeacon?.ClearExternalTarget();
        }

        private void Start()
        {
            if (questSequenceHost.CurrentQuestId != openingQuestId)
            {
                state = TutorialChapter0IntroState.Complete;
                trainingExitTransitionTrigger.enabled = true;
                return;
            }

            var run = saveSystemHost.System.Current.Run;
            state = TutorialChapter0IntroProgress.Resolve(run.TutorialIntroStageId, HasPasskey);
            switch (state)
            {
                case TutorialChapter0IntroState.HiddenRoomBriefing:
                case TutorialChapter0IntroState.HiddenRoomEntryDialogue:
                    RestoreHiddenRoom();
                    break;
                case TutorialChapter0IntroState.SeekTrainingExit:
                    RestoreMeetingReturn();
                    break;
                case TutorialChapter0IntroState.SeekHiddenRoom:
                    objectiveBeacon.SetExternalTarget(hiddenRoomEntryTarget);
                    break;
                default:
                    state = TutorialChapter0IntroState.MeetingDialogue;
                    objectiveBeacon.SetExternalTarget(null);
                    break;
            }
        }

        private void Update()
        {
            if (!enabled || questSequenceHost.CurrentQuestId != openingQuestId)
            {
                if (state != TutorialChapter0IntroState.Complete)
                {
                    state = TutorialChapter0IntroState.Complete;
                    objectiveBeacon.ClearExternalTarget();
                }
                return;
            }

            AnimateGlideInstruction();
            if (state == TutorialChapter0IntroState.MeetingDialogue)
                objectiveBeacon.SetExternalTarget(null);
            if (transitionRunning || dialoguePresenter.IsShowing) return;

            switch (state)
            {
                case TutorialChapter0IntroState.SeekHiddenRoom:
                    UpdateWrongWayResponse();
                    if (HasReachedHiddenRoomEntry()) StartCoroutine(TransitionToHiddenRoom());
                    break;
                case TutorialChapter0IntroState.SeekPasskey:
                    if (HasReachedTrigger(passkeyTrigger)) CollectPasskey();
                    break;
                case TutorialChapter0IntroState.SeekLedge:
                    if (HasReachedTrigger(ledgeTrigger)) BeginGlideBriefing();
                    break;
                case TutorialChapter0IntroState.ReturnToMeeting:
                    if (HasReachedTrigger(hiddenRoomReturnTrigger)) StartCoroutine(ReturnToMeetingRoom());
                    break;
            }
            previousPlayerPosition = player.position;
        }

        private void FixedUpdate()
        {
            if (!enabled || transitionRunning || dialoguePresenter.IsShowing) return;
            if (state == TutorialChapter0IntroState.SeekPasskey || state == TutorialChapter0IntroState.ReturnToMeeting)
                ApplyUpdraftRecovery();
        }

        private void HandleDialogueClosed()
        {
            if (auxiliaryDialogue)
            {
                auxiliaryDialogue = false;
                if (wrongWayAlarmVisual.activeSelf) wrongWayAlarmVisual.SetActive(false);
                if (resumeGuideAfterDialogue)
                {
                    resumeGuideAfterDialogue = false;
                    guideCompanion.BeginGuide(legacyGuideRoute.Waypoints);
                }
                return;
            }

            if (state == TutorialChapter0IntroState.MeetingDialogue)
            {
                if (!meetingDepartureLineShown)
                {
                    meetingDepartureLineShown = true;
                    serviceRoot.Events.Publish(new TutorialNarrativeChanged(
                        "TUTO_A_DEPARTURE",
                        "아다마스 본부",
                        new[] { "테우스: 일단 회의장을 나가 보자." }));
                    return;
                }

                state = TutorialChapter0IntroState.SeekHiddenRoom;
                SaveStage(TutorialChapter0IntroProgress.MeetingStageId, "TutorialIntroMeetingComplete");
                objectiveBeacon.SetExternalTarget(hiddenRoomEntryTarget);
            }
            else if (state == TutorialChapter0IntroState.HiddenRoomEntryDialogue)
            {
                state = TutorialChapter0IntroState.SeekLedge;
                objectiveBeacon.SetExternalTarget(ledgeTarget);
            }
            else if (state == TutorialChapter0IntroState.HiddenRoomBriefing)
            {
                if (!glideLaunchLineShown)
                {
                    glideLaunchLineShown = true;
                    glideInstructionRoot.SetActive(true);
                    updraftVisual.SetActive(true);
                    serviceRoot.Events.Publish(new TutorialNarrativeChanged(
                        "TUTO_B_GLIDE_LAUNCH",
                        "숨겨진 활공 훈련실",
                        new[] { "테우스: 러셀! 점프!" }));
                    return;
                }

                state = TutorialChapter0IntroState.SeekPasskey;
                objectiveBeacon.SetExternalTarget(passkeyTarget);
            }
        }

        private IEnumerator TransitionToHiddenRoom()
        {
            transitionRunning = true;
            LockPlayer();
            objectiveBeacon.SetExternalTarget(null);
            yield return FadeTo(1f, transitionFadeOut);
            hiddenRoomRoot.SetActive(true);
            MovePlayer(hiddenRoomSpawn.position);
            guideCompanion.CancelGuide();
            guideCompanion.transform.position = hiddenRoomSpawn.position + new Vector3(-1.1f, 1.1f, 0f);
            cameraFollowHost.SetTrackingBounds(
                hiddenCameraMinX,
                hiddenCameraMaxX,
                hiddenCameraMinY,
                hiddenCameraMaxY,
                true);
            theusFlashlightVisual.SetActive(true);
            passkeyVisual.SetActive(!HasPasskey);
            SaveStage(TutorialChapter0IntroProgress.HiddenRoomStageId, "TutorialIntroHiddenRoomEntered");
            if (transitionBlackHold > 0f) yield return new WaitForSecondsRealtime(transitionBlackHold);
            yield return FadeTo(0f, transitionFadeIn);
            UnlockPlayer();
            transitionRunning = false;
            state = TutorialChapter0IntroState.HiddenRoomEntryDialogue;
            serviceRoot.Events.Publish(new TutorialNarrativeChanged(
                TutorialChapter0IntroProgress.HiddenRoomStageId,
                "숨겨진 활공 훈련실",
                HiddenRoomEntryDialogue()));
        }

        private IEnumerator ReturnToMeetingRoom()
        {
            transitionRunning = true;
            LockPlayer();
            glideInstructionRoot.SetActive(false);
            updraftVisual.SetActive(false);
            yield return FadeTo(1f, transitionFadeOut);
            MovePlayer(meetingReturnSpawn.position);
            guideCompanion.CancelGuide();
            guideCompanion.transform.position = meetingReturnSpawn.position + new Vector3(-1.1f, 1.1f, 0f);
            cameraFollowHost.SetBounds(meetingCameraMinX, meetingCameraMaxX, meetingCameraY, true);
            hiddenRoomRoot.SetActive(false);
            theusFlashlightVisual.SetActive(false);
            trainingExitTransitionTrigger.enabled = true;
            SaveStage(TutorialChapter0IntroProgress.ReturnStageId, "TutorialIntroReturnedToMeeting");
            if (transitionBlackHold > 0f) yield return new WaitForSecondsRealtime(transitionBlackHold);
            yield return FadeTo(0f, transitionFadeIn);
            UnlockPlayer();
            transitionRunning = false;
            state = TutorialChapter0IntroState.SeekTrainingExit;
            objectiveBeacon.SetExternalTarget(trainingExitTarget);
            auxiliaryDialogue = true;
            resumeGuideAfterDialogue = true;
            serviceRoot.Events.Publish(new TutorialNarrativeChanged(
                TutorialChapter0IntroProgress.ReturnStageId,
                "아다마스 본부",
                new[]
                {
                    "테우스: 패스키 확보 완료! 이제 출구의 사다리를 타고 훈련장으로 내려가자.",
                    "프로메: 좋아. 마지막 훈련을 끝내고 바로 출발하자."
                }));
        }

        private void CollectPasskey()
        {
            if (HasPasskey) return;
            var run = saveSystemHost.System.Current.Run;
            run.CollectedItemIds ??= new List<string>();
            if (!run.CollectedItemIds.Contains(TutorialChapter0IntroProgress.PasskeyItemId))
                run.CollectedItemIds.Add(TutorialChapter0IntroProgress.PasskeyItemId);
            run.TutorialIntroStageId = TutorialChapter0IntroProgress.ReturnStageId;
            saveSystemHost.System.Save("TutorialAirshipPasskeyCollected");
            passkeyVisual.SetActive(false);
            glideInstructionRoot.SetActive(false);
            state = TutorialChapter0IntroState.ReturnToMeeting;
            objectiveBeacon.SetExternalTarget(hiddenRoomReturnTarget);
            auxiliaryDialogue = true;
            serviceRoot.Events.Publish(new TutorialNarrativeChanged(
                "TUTO_B_REWARD",
                "숨겨진 활공 훈련실",
                new[]
                {
                    "프로메: 이제 돌아가자."
                }));
        }

        private void UpdateWrongWayResponse()
        {
            if (player.position.x <= wrongWayThresholdX - wrongWayResetDistance)
            {
                wrongWayArmed = true;
                return;
            }
            if (!wrongWayArmed || player.position.x <= wrongWayThresholdX) return;
            wrongWayArmed = false;
            var responseIndex = Mathf.Min(wrongWayCount, WrongWayResponses.Length - 1);
            wrongWayCount++;
            auxiliaryDialogue = true;
            if (responseIndex == WrongWayResponses.Length - 1) wrongWayAlarmVisual.SetActive(true);
            serviceRoot.Events.Publish(new TutorialNarrativeChanged(
                "TUTO_A_WRONG_WAY",
                "아다마스 본부",
                WrongWayResponses[responseIndex]));
        }

        private void BeginGlideBriefing()
        {
            playerMotorHost.ResetTransientInput();
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = ledgeTarget.position;
            player.position = ledgeTarget.position;
            Physics2D.SyncTransforms();
            state = TutorialChapter0IntroState.HiddenRoomBriefing;
            objectiveBeacon.SetExternalTarget(null);
            serviceRoot.Events.Publish(new TutorialNarrativeChanged(
                "TUTO_B_GLIDE",
                "숨겨진 활공 훈련실",
                GlideBriefingDialogue()));
        }

        private void ApplyUpdraftRecovery()
        {
            var position = player.position;
            if (position.x < updraftMin.x || position.x > updraftMax.x ||
                position.y < updraftMin.y || position.y > updraftMax.y) return;
            var velocity = playerBody.linearVelocity;
            var gravityMagnitude = Mathf.Abs(Physics2D.gravity.y * playerBody.gravityScale);
            velocity.y = TutorialUpdraftPolicy.ResolveVerticalVelocity(
                velocity.y,
                updraftLiftSpeed,
                updraftMaxRiseSpeed,
                gravityMagnitude,
                Time.fixedDeltaTime);
            playerBody.linearVelocity = velocity;
        }

        private void AnimateGlideInstruction()
        {
            if (glideKeyVisual == null || !glideInstructionRoot.activeSelf) return;
            var pulse = 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.08f;
            glideKeyVisual.localScale = glideKeyBaseScale * pulse;
        }

        private void RestoreHiddenRoom()
        {
            hiddenRoomRoot.SetActive(true);
            MovePlayer(hiddenRoomSpawn.position);
            cameraFollowHost.SetTrackingBounds(hiddenCameraMinX, hiddenCameraMaxX, hiddenCameraMinY, hiddenCameraMaxY, true);
            guideCompanion.transform.position = hiddenRoomSpawn.position + new Vector3(-1.1f, 1.1f, 0f);
            theusFlashlightVisual.SetActive(true);
            passkeyVisual.SetActive(true);
            updraftVisual.SetActive(true);
            trainingExitTransitionTrigger.enabled = false;
            StartCoroutine(PublishRestoredHiddenRoomDialogue());
        }

        private IEnumerator PublishRestoredHiddenRoomDialogue()
        {
            yield return null;
            serviceRoot.Events.Publish(new TutorialNarrativeChanged(
                TutorialChapter0IntroProgress.HiddenRoomStageId,
                "숨겨진 활공 훈련실",
                HiddenRoomEntryDialogue()));
        }

        private void RestoreMeetingReturn()
        {
            hiddenRoomRoot.SetActive(false);
            MovePlayer(meetingReturnSpawn.position);
            cameraFollowHost.SetBounds(meetingCameraMinX, meetingCameraMaxX, meetingCameraY, true);
            passkeyVisual.SetActive(false);
            theusFlashlightVisual.SetActive(false);
            trainingExitTransitionTrigger.enabled = true;
            objectiveBeacon.SetExternalTarget(trainingExitTarget);
            guideCompanion.BeginGuide(legacyGuideRoute.Waypoints);
        }

        private static string[] HiddenRoomEntryDialogue()
        {
            return new[]
            {
                "테우스: 윽, 껌껌해.",
                "프로메: 일단 더 들어가 볼까?"
            };
        }

        private static string[] GlideBriefingDialogue()
        {
            return new[]
            {
                "테우스: 앞에는 더 이상 길이 없는 것 같은데?",
                "프로메: 아래에서 강한 바람이 불어오고 있어.",
                "테우스: 나 이해했어!",
                "테우스: 프로메! 우산을 써!",
                "프로메: 우산...?"
            };
        }

        private bool IsOverlapping(Collider2D trigger)
        {
            return trigger != null && trigger.enabled && trigger.Distance(playerCollider).isOverlapped;
        }

        private bool HasReachedTrigger(Collider2D trigger)
        {
            return IsOverlapping(trigger) ||
                   (trigger != null && trigger.enabled &&
                    TutorialTriggerSweepPolicy.Intersects(trigger.bounds, previousPlayerPosition, player.position));
        }

        private bool HasReachedHiddenRoomEntry()
        {
            if (hiddenRoomEntryTarget == null) return false;

            // The entrance sits exactly at the left edge of the briefing deck. Trigger a little
            // before that edge so a fast or low-frame-rate step cannot carry the player past the
            // floor and leave them falling before Physics2D reports an overlap.
            return TutorialChapter0IntroProgress.HasReachedHiddenRoomEntry(
                player.position,
                hiddenRoomEntryTarget.position,
                IsOverlapping(hiddenRoomEntryTrigger));
        }

        private void SaveStage(string stageId, string reason)
        {
            saveSystemHost.System.Current.Run.TutorialIntroStageId = stageId;
            saveSystemHost.System.Save(reason);
        }

        private void MovePlayer(Vector3 position)
        {
            playerBody.position = position;
            player.position = position;
            playerBody.linearVelocity = Vector2.zero;
            playerMotorHost.ResetTransientInput();
            previousPlayerPosition = position;
            Physics2D.SyncTransforms();
        }

        private void LockPlayer()
        {
            playerMotorHost.ResetTransientInput();
            playerInputHost.enabled = false;
            playerBody.linearVelocity = Vector2.zero;
            fadeCanvasGroup.blocksRaycasts = true;
        }

        private void UnlockPlayer()
        {
            fadeCanvasGroup.blocksRaycasts = false;
            playerInputHost.enabled = true;
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            var startAlpha = fadeCanvasGroup.alpha;
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
