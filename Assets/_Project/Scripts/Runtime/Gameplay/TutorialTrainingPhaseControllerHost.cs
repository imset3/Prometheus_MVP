using System;
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
    /// Reuses the compact imported training room one lesson at a time. Existing
    /// quest-specific hazard hosts still own their sequences; this host owns phase
    /// scopes, cleanup, and the single room exit gate.
    /// </summary>
    public sealed class TutorialTrainingPhaseControllerHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private string[] trainingQuestIds = Array.Empty<string>();
        [SerializeField] private Collider2D[] phaseAreas = Array.Empty<Collider2D>();

        [Header("Single exit gate")]
        [SerializeField] private Collider2D exitGateCollider;
        [SerializeField] private Renderer exitGateRenderer;

        [Header("Previous phase cleanup")]
        [SerializeField] private GameObject[] fallingObjects = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] fallingWarnings = Array.Empty<GameObject>();
        [SerializeField] private GameObject jumpProjectile;
        [SerializeField] private GameObject meleeAreaRoot;
        [SerializeField] private GameObject meleeEnemy;
        [SerializeField] private GameObject[] rangedTargets = Array.Empty<GameObject>();

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null &&
                                     trainingQuestIds != null && phaseAreas != null &&
                                     trainingQuestIds.Length == 5 && phaseAreas.Length == trainingQuestIds.Length &&
                                     HasValidPhaseAreas() && exitGateCollider != null && exitGateRenderer != null &&
                                     fallingObjects != null && fallingObjects.Length == 3 &&
                                     fallingWarnings != null && fallingWarnings.Length == fallingObjects.Length &&
                                     HasCompleteObjects(fallingObjects) && HasCompleteObjects(fallingWarnings) &&
                                     jumpProjectile != null && meleeAreaRoot != null && meleeEnemy != null &&
                                     rangedTargets != null && rangedTargets.Length == 3 &&
                                     HasCompleteObjects(rangedTargets);
        public int CurrentPhaseIndex { get; private set; } = -1;
        public bool IsExitLocked { get; private set; }
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
            if (HasValidSetup) return;
            Debug.LogError(
                "TutorialTrainingPhaseControllerHost requires five phase areas, one exit gate, and all phase cleanup references.",
                this);
            enabled = false;
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
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message)
        {
            Refresh(message.QuestId);
        }

        public void Refresh(string questId)
        {
            CurrentPhaseIndex = TutorialTrainingPhasePolicy.ResolvePhaseIndex(questId, trainingQuestIds);
            for (var index = 0; index < phaseAreas.Length; index++)
                phaseAreas[index].enabled =
                    TutorialTrainingPhasePolicy.ShouldActivatePhase(CurrentPhaseIndex, index);

            IsExitLocked = TutorialTrainingPhasePolicy.ShouldLockExit(CurrentPhaseIndex);
            exitGateCollider.enabled = IsExitLocked;
            exitGateRenderer.enabled = IsExitLocked;
            CleanupInactivePhases();
        }

        public void RefreshCurrentQuest()
        {
            if (HasValidSetup)
                Refresh(questSequenceHost.CurrentQuestId);
        }

        private void CleanupInactivePhases()
        {
            if (CurrentPhaseIndex != 0)
            {
                SetObjectsActive(fallingObjects, false);
                SetObjectsActive(fallingWarnings, false);
            }

            if (CurrentPhaseIndex != 1 && jumpProjectile.activeSelf)
                jumpProjectile.SetActive(false);
            meleeAreaRoot.SetActive(CurrentPhaseIndex == 3);
            if (CurrentPhaseIndex != 3 && meleeEnemy.activeSelf)
                meleeEnemy.SetActive(false);
            if (CurrentPhaseIndex != 4)
                SetObjectsActive(rangedTargets, false);
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

        private static void SetObjectsActive(IReadOnlyList<GameObject> objects, bool active)
        {
            foreach (var target in objects)
                if (target != null && target.activeSelf != active)
                    target.SetActive(active);
        }
    }
}
