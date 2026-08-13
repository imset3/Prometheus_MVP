using System;
using System.Collections;
using Narthex.Gameplay;
using Narthex.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Narthex.SceneFlow
{
    /// <summary>
    /// Plays the demo epilogue after Helte is defeated. Every visual and route endpoint is
    /// scene-authored so designers can replace the airship or move the markers without code edits.
    /// </summary>
    public sealed class TutorialDemoEndingSequenceHost : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] private TutorialCompletionFlowHost completionFlow;
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private Renderer[] playerRenderers = Array.Empty<Renderer>();

        [Header("World boarding")]
        [SerializeField] private Transform boardingPointMarker;
        [SerializeField] private GameObject dockedAirshipVisual;
        [SerializeField] private GameObject[] worldRootsToHideAfterBoarding = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] hudRootsToHideOnDefeat = Array.Empty<GameObject>();

        [Header("Presentation")]
        [SerializeField] private CanvasGroup cinematicCanvas;
        [SerializeField] private RectTransform boardingAirshipVisual;
        [SerializeField] private RectTransform flightAirshipVisual;
        [SerializeField] private RectTransform flightStartMarker;
        [SerializeField] private RectTransform flightEndMarker;
        [SerializeField] private Behaviour zenithApproachPresenter;
        [SerializeField] private SpriteRenderer worldZenithRenderer;
        [SerializeField] private Transform zenithCenterMarker;
        [SerializeField] private CanvasGroup fadeCanvas;
        [SerializeField] private Text captionText;

        [Header("Result")]
        [SerializeField] private CanvasGroup resultContentCanvas;
        [SerializeField] private RectTransform resultTextRect;
        [SerializeField] private Button returnToTitleButton;
        [SerializeField] private Text returnToTitleButtonLabel;
        [SerializeField] private string titleSceneName = "TitleScene";

        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float autoBoardSeconds = 2.8f;
        [SerializeField, Min(0.05f)] private float boardingFadeSeconds = 0.65f;
        [SerializeField, Min(0f)] private float boardingHoldSeconds = 0.45f;
        [SerializeField, Min(0.1f)] private float flightSeconds = 7f;
        [SerializeField, Min(0.05f)] private float finalFadeSeconds = 1.25f;
        [SerializeField, Min(0.01f)] private float startScale = 1f;
        [SerializeField, Min(0.01f)] private float endScale = 0.12f;
        [SerializeField, Min(0.01f)] private float zenithEndScaleMultiplier = 1.18f;
        [SerializeField, Min(0.1f)] private float resultRiseSeconds = 1.2f;
        [SerializeField, Min(0f)] private float resultRiseDistance = 120f;
        [SerializeField] private string boardingCaption = "프로메가 정박한 비행정으로 향합니다.";
        [SerializeField] private string flightCaption = "제니스를 향해 출항합니다.";

        private Coroutine endingRoutine;
        private bool finished;
        private bool reachedBoardingPoint;
        private bool worldPresentationHidden;
        private bool resultAnchorCaptured;
        private Vector2 resultTextEndPosition;
        private float originalGravityScale;
        private bool[] worldRootInitialStates = Array.Empty<bool>();
        private bool[] hudRootInitialStates = Array.Empty<bool>();
        private bool zenithStateCaptured;
        private bool zenithPresenterInitiallyEnabled;
        private bool zenithRendererInitiallyEnabled;
        private Vector3 zenithInitialLocalPosition;
        private Quaternion zenithInitialLocalRotation;
        private Vector3 zenithInitialLocalScale;
        private Color zenithInitialColor;

        public bool HasValidSetup => completionFlow != null && playerInputHost != null && playerBody != null &&
                                     playerRenderers != null && playerRenderers.Length > 0 &&
                                     Array.TrueForAll(playerRenderers, item => item != null) &&
                                     boardingPointMarker != null && dockedAirshipVisual != null &&
                                     worldRootsToHideAfterBoarding != null &&
                                     Array.TrueForAll(worldRootsToHideAfterBoarding, item => item != null) &&
                                     hudRootsToHideOnDefeat != null &&
                                     Array.TrueForAll(hudRootsToHideOnDefeat, item => item != null) &&
                                     cinematicCanvas != null && boardingAirshipVisual != null &&
                                     flightAirshipVisual != null &&
                                     flightStartMarker != null && flightEndMarker != null &&
                                     zenithApproachPresenter != null && worldZenithRenderer != null &&
                                     zenithCenterMarker != null &&
                                     fadeCanvas != null && captionText != null && resultContentCanvas != null &&
                                     resultTextRect != null && returnToTitleButton != null &&
                                     returnToTitleButtonLabel != null;
        public bool IsPlaying => endingRoutine != null;
        public bool Finished => finished;
        public float FlightSeconds => flightSeconds;
        public bool ReachedBoardingPoint => reachedBoardingPoint;
        public bool WorldPresentationHidden => worldPresentationHidden;
        public bool AreAllWorldRootsHidden => worldRootsToHideAfterBoarding != null &&
                                              Array.TrueForAll(worldRootsToHideAfterBoarding,
                                                  item => item != null && !item.activeSelf);
        public bool ReturnToTitleButtonVisible => returnToTitleButton != null &&
                                                  returnToTitleButton.gameObject.activeInHierarchy;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialDemoEndingSequenceHost requires boarding, cinematic, result, and title-button references.", this);
                enabled = false;
                return;
            }

            originalGravityScale = playerBody.gravityScale;
            EnsureResultAnchorCaptured();
            SetCinematicVisible(false);
            fadeCanvas.alpha = 0f;
            fadeCanvas.blocksRaycasts = false;
            fadeCanvas.interactable = false;
            returnToTitleButton.gameObject.SetActive(false);
            returnToTitleButton.onClick.AddListener(HandleReturnToTitleRequested);
        }

        private void OnDestroy()
        {
            if (returnToTitleButton != null)
                returnToTitleButton.onClick.RemoveListener(HandleReturnToTitleRequested);
        }

        private void OnDisable()
        {
            if (endingRoutine != null) StopCoroutine(endingRoutine);
            endingRoutine = null;
            if (finished || !HasValidSetup) return;
            playerInputHost.ReleaseInputLock(PlayerInputLockReason.Ending);
            playerBody.gravityScale = originalGravityScale;
            SetPlayerVisible(true);
            RestoreWorldPresentation();
            RestoreHudPresentation();
            RestoreWorldZenithState();
            completionFlow.SetGameplayHudVisible(true);
            SetCinematicVisible(false);
            fadeCanvas.alpha = 0f;
            fadeCanvas.blocksRaycasts = false;
            returnToTitleButton.gameObject.SetActive(false);
        }

        public bool TryBeginEnding()
        {
            if (!isActiveAndEnabled || !HasValidSetup || endingRoutine != null || finished) return false;
            CaptureWorldPresentationState();
            endingRoutine = StartCoroutine(RunEnding());
            return true;
        }

        private IEnumerator RunEnding()
        {
            playerInputHost.AcquireInputLock(PlayerInputLockReason.Ending);
            playerBody.linearVelocity = Vector2.zero;
            completionFlow.SetGameplayHudVisible(false);
            HideHudPresentation();
            captionText.text = boardingCaption;

            cinematicCanvas.alpha = 1f;
            cinematicCanvas.blocksRaycasts = false;
            captionText.gameObject.SetActive(true);
            boardingAirshipVisual.gameObject.SetActive(false);
            flightAirshipVisual.gameObject.SetActive(false);
            yield return MovePlayerToBoardingPoint();

            fadeCanvas.blocksRaycasts = true;
            yield return Fade(fadeCanvas, 0f, 0.72f, boardingFadeSeconds);
            SetPlayerVisible(false);
            HideWorldPresentation();
            PrepareWorldZenithForFlight();
            boardingAirshipVisual.gameObject.SetActive(true);
            boardingAirshipVisual.anchoredPosition = flightStartMarker.anchoredPosition;
            boardingAirshipVisual.localScale = Vector3.one * startScale;
            yield return new WaitForSecondsRealtime(boardingHoldSeconds);
            boardingAirshipVisual.gameObject.SetActive(false);
            flightAirshipVisual.gameObject.SetActive(true);
            captionText.gameObject.SetActive(false);
            flightAirshipVisual.anchoredPosition = flightStartMarker.anchoredPosition;
            flightAirshipVisual.localScale = Vector3.one * startScale;
            yield return Fade(fadeCanvas, 0.72f, 0f, boardingFadeSeconds);

            captionText.text = flightCaption;
            var elapsed = 0f;
            while (elapsed < flightSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / flightSeconds);
                var eased = SmoothProgress(progress);
                flightAirshipVisual.anchoredPosition = CalculateFlightPosition(
                    flightStartMarker.anchoredPosition,
                    flightEndMarker.anchoredPosition,
                    eased);
                flightAirshipVisual.localScale = Vector3.one * CalculateFlightScale(startScale, endScale, eased);
                AnimateWorldZenith(eased);
                fadeCanvas.alpha = Mathf.Lerp(0f, 0.78f, Mathf.Clamp01((progress - 0.48f) / 0.52f));
                yield return null;
            }

            yield return Fade(fadeCanvas, fadeCanvas.alpha, 1f, finalFadeSeconds);
            SetCinematicVisible(false);
            finished = true;
            endingRoutine = null;
            completionFlow.PresentResultNow();
            PrepareResultPresentation(false);
            yield return PresentResultAndRevealButton();
            fadeCanvas.blocksRaycasts = false;
        }

        private IEnumerator MovePlayerToBoardingPoint()
        {
            originalGravityScale = playerBody.gravityScale;
            var start = playerBody.position;
            var targetX = boardingPointMarker.position.x;
            var elapsed = 0f;
            while (elapsed < autoBoardSeconds)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedUnscaledDeltaTime;
                var progress = SmoothProgress(elapsed / Mathf.Max(0.1f, autoBoardSeconds));
                var nextX = Mathf.LerpUnclamped(start.x, targetX, progress);
                var deltaTime = Mathf.Max(0.001f, Time.fixedUnscaledDeltaTime);
                playerBody.linearVelocity = new Vector2((nextX - playerBody.position.x) / deltaTime,
                    playerBody.linearVelocity.y);
                playerBody.position = new Vector2(nextX, playerBody.position.y);
                Physics2D.SyncTransforms();
            }

            playerBody.position = new Vector2(targetX, playerBody.position.y);
            playerBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            reachedBoardingPoint = true;
        }

        private IEnumerator PresentResultAndRevealButton()
        {
            EnsureResultAnchorCaptured();
            var startPosition = resultTextEndPosition + Vector2.down * resultRiseDistance;
            var elapsed = 0f;
            while (elapsed < resultRiseSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = SmoothProgress(elapsed / Mathf.Max(0.1f, resultRiseSeconds));
                resultTextRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, resultTextEndPosition, progress);
                resultContentCanvas.alpha = progress;
                fadeCanvas.alpha = Mathf.Lerp(1f, 0f, progress);
                yield return null;
            }

            resultTextRect.anchoredPosition = resultTextEndPosition;
            resultContentCanvas.alpha = 1f;
            fadeCanvas.alpha = 0f;
            returnToTitleButton.gameObject.SetActive(true);
            returnToTitleButton.interactable = true;
            returnToTitleButtonLabel.text = "타이틀 화면으로";
        }

        public void PrepareImmediateResult()
        {
            if (!HasValidSetup) return;
            finished = true;
            PrepareResultPresentation(true);
        }

        private void PrepareResultPresentation(bool visible)
        {
            EnsureResultAnchorCaptured();
            resultContentCanvas.alpha = visible ? 1f : 0f;
            resultTextRect.anchoredPosition = visible
                ? resultTextEndPosition
                : resultTextEndPosition + Vector2.down * resultRiseDistance;
            returnToTitleButton.gameObject.SetActive(visible);
            returnToTitleButton.interactable = visible;
            returnToTitleButtonLabel.text = "타이틀 화면으로";
        }

        private void HandleReturnToTitleRequested()
        {
            GameLaunchSession.MarkDemoFinished();
            if (!string.IsNullOrWhiteSpace(titleSceneName) && Application.CanStreamedLevelBeLoaded(titleSceneName))
            {
                SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
                return;
            }

            returnToTitleButton.interactable = false;
            returnToTitleButtonLabel.text = "타이틀 화면 준비 중";
        }

        private static IEnumerator Fade(CanvasGroup canvas, float from, float to, float seconds)
        {
            canvas.alpha = from;
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                canvas.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, seconds)));
                yield return null;
            }
            canvas.alpha = to;
        }

        private void SetCinematicVisible(bool visible)
        {
            cinematicCanvas.alpha = visible ? 1f : 0f;
            cinematicCanvas.blocksRaycasts = false;
            cinematicCanvas.interactable = false;
            boardingAirshipVisual.gameObject.SetActive(visible);
            flightAirshipVisual.gameObject.SetActive(false);
            captionText.gameObject.SetActive(visible);
        }

        private void SetPlayerVisible(bool visible)
        {
            foreach (var renderer in playerRenderers) renderer.enabled = visible;
        }

        private void CaptureWorldPresentationState()
        {
            worldRootInitialStates = new bool[worldRootsToHideAfterBoarding.Length];
            for (var index = 0; index < worldRootsToHideAfterBoarding.Length; index++)
                worldRootInitialStates[index] = worldRootsToHideAfterBoarding[index].activeSelf;
            reachedBoardingPoint = false;
            worldPresentationHidden = false;
            zenithStateCaptured = false;
            hudRootInitialStates = new bool[hudRootsToHideOnDefeat.Length];
            for (var index = 0; index < hudRootsToHideOnDefeat.Length; index++)
                hudRootInitialStates[index] = hudRootsToHideOnDefeat[index].activeSelf;
        }

        private void HideHudPresentation()
        {
            foreach (var root in hudRootsToHideOnDefeat)
                if (root != null) root.SetActive(false);
        }

        private void RestoreHudPresentation()
        {
            for (var index = 0; index < hudRootsToHideOnDefeat.Length; index++)
            {
                if (hudRootsToHideOnDefeat[index] == null) continue;
                var active = index < hudRootInitialStates.Length && hudRootInitialStates[index];
                hudRootsToHideOnDefeat[index].SetActive(active);
            }
        }

        private void HideWorldPresentation()
        {
            dockedAirshipVisual.SetActive(false);
            foreach (var root in worldRootsToHideAfterBoarding) root.SetActive(false);
            worldPresentationHidden = true;
        }

        private void RestoreWorldPresentation()
        {
            for (var index = 0; index < worldRootsToHideAfterBoarding.Length; index++)
            {
                var active = index < worldRootInitialStates.Length && worldRootInitialStates[index];
                worldRootsToHideAfterBoarding[index].SetActive(active);
            }
            dockedAirshipVisual.SetActive(true);
            worldPresentationHidden = false;
        }

        private void PrepareWorldZenithForFlight()
        {
            var zenithTransform = worldZenithRenderer.transform;
            zenithPresenterInitiallyEnabled = zenithApproachPresenter.enabled;
            zenithRendererInitiallyEnabled = worldZenithRenderer.enabled;
            zenithInitialLocalPosition = zenithTransform.localPosition;
            zenithInitialLocalRotation = zenithTransform.localRotation;
            zenithInitialLocalScale = zenithTransform.localScale;
            zenithInitialColor = worldZenithRenderer.color;
            zenithStateCaptured = true;

            zenithApproachPresenter.enabled = false;
            worldZenithRenderer.enabled = true;
            var color = worldZenithRenderer.color;
            color.a = Mathf.Max(color.a, 0.01f);
            worldZenithRenderer.color = color;
        }

        private void AnimateWorldZenith(float progress)
        {
            if (!zenithStateCaptured) return;
            var zenithTransform = worldZenithRenderer.transform;
            var parent = zenithTransform.parent;
            var centerLocalPosition = parent != null
                ? parent.InverseTransformPoint(zenithCenterMarker.position)
                : zenithCenterMarker.position;
            zenithTransform.localPosition = Vector3.LerpUnclamped(
                zenithInitialLocalPosition,
                centerLocalPosition,
                Mathf.Clamp01(progress));
            zenithTransform.localRotation = zenithInitialLocalRotation;
            zenithTransform.localScale = Vector3.LerpUnclamped(
                zenithInitialLocalScale,
                zenithInitialLocalScale * zenithEndScaleMultiplier,
                Mathf.Clamp01(progress));
            var color = zenithInitialColor;
            color.a = Mathf.Lerp(Mathf.Max(0.01f, zenithInitialColor.a), 1f, Mathf.Clamp01(progress));
            worldZenithRenderer.color = color;
        }

        private void RestoreWorldZenithState()
        {
            if (!zenithStateCaptured || worldZenithRenderer == null || zenithApproachPresenter == null) return;
            var zenithTransform = worldZenithRenderer.transform;
            zenithTransform.localPosition = zenithInitialLocalPosition;
            zenithTransform.localRotation = zenithInitialLocalRotation;
            zenithTransform.localScale = zenithInitialLocalScale;
            worldZenithRenderer.color = zenithInitialColor;
            worldZenithRenderer.enabled = zenithRendererInitiallyEnabled;
            zenithApproachPresenter.enabled = zenithPresenterInitiallyEnabled;
            zenithStateCaptured = false;
        }

        private void EnsureResultAnchorCaptured()
        {
            if (resultAnchorCaptured || resultTextRect == null) return;
            resultTextEndPosition = resultTextRect.anchoredPosition;
            resultAnchorCaptured = true;
        }

        public static float SmoothProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            return progress * progress * (3f - 2f * progress);
        }

        public static Vector2 CalculateFlightPosition(Vector2 start, Vector2 end, float progress) =>
            Vector2.LerpUnclamped(start, end, Mathf.Clamp01(progress));

        public static float CalculateFlightScale(float start, float end, float progress) =>
            Mathf.Lerp(Mathf.Max(0.01f, start), Mathf.Max(0.01f, end), Mathf.Clamp01(progress));
    }
}
