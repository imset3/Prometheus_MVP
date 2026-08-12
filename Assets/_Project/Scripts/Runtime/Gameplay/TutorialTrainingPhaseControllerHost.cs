using System;
using System.Collections;
using System.Collections.Generic;
using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    public static class TutorialTrainingPhasePolicy
    {
        public static int ResolvePhaseIndex(string questId, IReadOnlyList<string> trainingQuestIds)
        {
            if (string.IsNullOrWhiteSpace(questId) || trainingQuestIds == null) return -1;
            for (var index = 0; index < trainingQuestIds.Count; index++)
                if (trainingQuestIds[index] == questId)
                    return index;
            return -1;
        }

        public static bool ShouldActivatePhase(int currentPhaseIndex, int candidatePhaseIndex)
        {
            return currentPhaseIndex >= 0 && currentPhaseIndex == candidatePhaseIndex;
        }

        public static bool ShouldLockExit(int currentPhaseIndex)
        {
            return currentPhaseIndex >= 0;
        }
    }

    /// <summary>
    /// Reuses the whole training room one lesson at a time. Phase content, action
    /// scopes, start positions, and completion triggers are authored as scene markers.
    /// The controller never owns level coordinates.
    /// </summary>
    public sealed class TutorialTrainingPhaseControllerHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private QuestManagerHost questManagerHost;
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private PlayerMotorHost playerMotor;
        [SerializeField] private Transform player;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private CanvasGroup fadeCanvasGroup;
        [SerializeField] private string[] trainingQuestIds = Array.Empty<string>();
        [SerializeField] private Collider2D[] phaseAreas = Array.Empty<Collider2D>();
        [SerializeField] private GameObject[] phaseContentRoots = Array.Empty<GameObject>();
        [SerializeField] private Transform[] phaseStartMarkers = Array.Empty<Transform>();
        [SerializeField] private TutorialImportedTrainingFlowHost importedTrainingFlow;
        [SerializeField] private TutorialTrainingSpawnHost trainingSpawnHost;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.18f;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.22f;

        [Header("Single exit gate")]
        [SerializeField] private Collider2D exitGateCollider;
        [SerializeField] private Renderer exitGateRenderer;

        private Coroutine transitionRoutine;

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null &&
                                     questManagerHost != null && playerInputHost != null &&
                                     playerMotor != null && player != null && playerBody != null &&
                                     fadeCanvasGroup != null &&
                                     trainingQuestIds != null && phaseAreas != null &&
                                     phaseContentRoots != null && phaseStartMarkers != null &&
                                     trainingQuestIds.Length == 5 && phaseAreas.Length == trainingQuestIds.Length &&
                                     phaseContentRoots.Length == trainingQuestIds.Length &&
                                     phaseStartMarkers.Length == trainingQuestIds.Length &&
                                     HasValidPhaseAreas() && exitGateCollider != null && exitGateRenderer != null &&
                                     HasCompleteObjects(phaseContentRoots) &&
                                     HasCompleteTransforms(phaseStartMarkers);
        public int CurrentPhaseIndex { get; private set; } = -1;
        public bool IsExitLocked { get; private set; }
        public bool IsTransitioning => transitionRoutine != null;
        public int ActivePhaseAreaCount
        {
            get
            {
                var count = 0;
                if (phaseAreas == null) return count;
                foreach (var area in phaseAreas)
                    if (area != null && area.enabled)
                        count++;
                return count;
            }
        }

        private void Awake()
        {
            if (importedTrainingFlow == null) importedTrainingFlow = GetComponent<TutorialImportedTrainingFlowHost>();
            if (trainingSpawnHost == null)
                trainingSpawnHost = FindFirstObjectByType<TutorialTrainingSpawnHost>(FindObjectsInactive.Include);
            RecoverRuntimePhaseMarkers();
            if (HasValidSetup) return;
            Debug.LogError(
                "TutorialTrainingPhaseControllerHost requires five marker-authored phases, player transition references, and one exit gate.",
                this);
            enabled = false;
        }

        private void RecoverRuntimePhaseMarkers()
        {
            if (phaseStartMarkers == null || phaseStartMarkers.Length == 0) return;
            Transform fallback = null;
            if (trainingSpawnHost != null)
                fallback = trainingSpawnHost.transform.Find("DashTrainingRestartPoint");
            fallback ??= player;
            for (var index = 0; index < phaseStartMarkers.Length; index++)
                phaseStartMarkers[index] ??= fallback;
        }

        private void OnEnable()
        {
            if (!HasValidSetup) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
        }

        private void Start()
        {
            if (HasValidSetup) Refresh(questSequenceHost.CurrentQuestId);
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            var interruptedOwnTransition = transitionRoutine != null;
            if (interruptedOwnTransition) StopCoroutine(transitionRoutine);
            transitionRoutine = null;
            SetPlayerLocked(false);
            if (interruptedOwnTransition) ResetFadeOverlay();
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message)
        {
            var nextPhaseIndex = TutorialTrainingPhasePolicy.ResolvePhaseIndex(
                message.QuestId,
                trainingQuestIds);
            if (CurrentPhaseIndex >= 0 && nextPhaseIndex >= 0 && nextPhaseIndex != CurrentPhaseIndex)
            {
                if (transitionRoutine != null) StopCoroutine(transitionRoutine);
                transitionRoutine = StartCoroutine(TransitionToPhase(nextPhaseIndex));
                return;
            }

            Refresh(message.QuestId);
        }

        public void Refresh(string questId)
        {
            CurrentPhaseIndex = TutorialTrainingPhasePolicy.ResolvePhaseIndex(questId, trainingQuestIds);
            for (var index = 0; index < phaseAreas.Length; index++)
            {
                phaseAreas[index].enabled =
                    TutorialTrainingPhasePolicy.ShouldActivatePhase(CurrentPhaseIndex, index);
                phaseContentRoots[index].SetActive(
                    TutorialTrainingPhasePolicy.ShouldActivatePhase(CurrentPhaseIndex, index));
            }

            IsExitLocked = TutorialTrainingPhasePolicy.ShouldLockExit(CurrentPhaseIndex);
            exitGateCollider.enabled = IsExitLocked;
            exitGateRenderer.enabled = IsExitLocked;
            importedTrainingFlow?.RefreshForQuest(questId);
            trainingSpawnHost?.RefreshForQuest(questId);
        }

        public void RefreshCurrentQuest()
        {
            if (HasValidSetup)
                Refresh(questSequenceHost.CurrentQuestId);
        }

        public bool TryRestartCurrentPhase()
        {
            if (!HasValidSetup || CurrentPhaseIndex < 0 || transitionRoutine != null) return false;
            transitionRoutine = StartCoroutine(RestartPhase(CurrentPhaseIndex));
            return true;
        }

        private IEnumerator TransitionToPhase(int nextPhaseIndex)
        {
            try
            {
                SetPlayerLocked(true);
                yield return FadeTo(1f, fadeOutDuration);
                Refresh(trainingQuestIds[nextPhaseIndex]);
                MovePlayerToMarker(phaseStartMarkers[nextPhaseIndex]);
                yield return FadeTo(0f, fadeInDuration);
            }
            finally
            {
                ResetFadeOverlay();
                SetPlayerLocked(false);
                transitionRoutine = null;
            }
        }

        private IEnumerator RestartPhase(int phaseIndex)
        {
            try
            {
                SetPlayerLocked(true);
                yield return FadeTo(1f, fadeOutDuration);
                questManagerHost.Initialize();
                questManagerHost.System.ResetProgress(trainingQuestIds[phaseIndex]);
                phaseContentRoots[phaseIndex].SetActive(false);
                MovePlayerToMarker(phaseStartMarkers[phaseIndex]);
                phaseContentRoots[phaseIndex].SetActive(true);
                phaseAreas[phaseIndex].enabled = true;
                yield return FadeTo(0f, fadeInDuration);
            }
            finally
            {
                ResetFadeOverlay();
                SetPlayerLocked(false);
                transitionRoutine = null;
            }
        }

        private void ResetFadeOverlay()
        {
            if (fadeCanvasGroup == null) return;
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.interactable = false;
        }

        private void MovePlayerToMarker(Transform marker)
        {
            playerMotor.ResetTransientInput();
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = marker.position;
            player.position = marker.position;
            Physics2D.SyncTransforms();
        }

        private void SetPlayerLocked(bool locked)
        {
            if (playerInputHost != null) playerInputHost.enabled = !locked;
            if (fadeCanvasGroup != null) fadeCanvasGroup.blocksRaycasts = locked;
            if (locked)
            {
                playerMotor?.ResetTransientInput();
                if (playerBody != null) playerBody.linearVelocity = Vector2.zero;
            }
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            if (fadeCanvasGroup == null) yield break;
            if (duration <= 0f)
            {
                fadeCanvasGroup.alpha = targetAlpha;
                yield break;
            }

            var startAlpha = fadeCanvasGroup.alpha;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            fadeCanvasGroup.alpha = targetAlpha;
        }

        private bool HasValidPhaseAreas()
        {
            foreach (var area in phaseAreas)
                if (area == null || !area.isTrigger)
                    return false;
            return true;
        }

        private static bool HasCompleteObjects(IReadOnlyList<GameObject> objects)
        {
            foreach (var target in objects)
                if (target == null)
                    return false;
            return true;
        }

        private static bool HasCompleteTransforms(IReadOnlyList<Transform> transforms)
        {
            foreach (var target in transforms)
                if (target == null)
                    return false;
            return true;
        }
    }
}
