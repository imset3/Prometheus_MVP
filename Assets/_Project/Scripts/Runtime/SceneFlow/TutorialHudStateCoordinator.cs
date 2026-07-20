using System;
using System.Collections.Generic;
using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.SceneFlow
{
    public enum TutorialHudMode
    {
        Normal,
        Dialogue,
        BossCombat,
        Result
    }

    public static class TutorialHudModeResolver
    {
        public static TutorialHudMode Resolve(bool result, bool dialogue, bool introduction, bool bossCombat)
        {
            if (result) return TutorialHudMode.Result;
            if (dialogue || introduction) return TutorialHudMode.Dialogue;
            if (bossCombat) return TutorialHudMode.BossCombat;
            return TutorialHudMode.Normal;
        }
    }

    /// <summary>
    /// Owns HUD-mode priority without replacing the individual presenters. It records the current active state
    /// before suppressing conflicting objects, then restores that state when the higher-priority mode ends.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class TutorialHudStateCoordinator : MonoBehaviour
    {
        [Header("State sources")]
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialBossArenaHost bossArenaHost;
        [SerializeField] private GameObject resultOverlay;
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private GameObject introductionCard;

        [Header("Mode suppression groups")]
        [SerializeField] private GameObject[] suppressDuringDialogue = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] suppressDuringBossCombat = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] suppressDuringResult = Array.Empty<GameObject>();

        private readonly Dictionary<GameObject, bool> recordedActiveStates = new Dictionary<GameObject, bool>();
        private TutorialHudMode currentMode = TutorialHudMode.Normal;

        public bool HasValidSetup => serviceRoot != null && bossArenaHost != null && resultOverlay != null &&
                                     dialoguePanel != null && introductionCard != null &&
                                     HasCompleteGroup(suppressDuringDialogue) &&
                                     HasCompleteGroup(suppressDuringBossCombat) &&
                                     HasCompleteGroup(suppressDuringResult);
        public TutorialHudMode CurrentMode => currentMode;
        public int DialogueSuppressionCount => suppressDuringDialogue?.Length ?? 0;
        public int BossSuppressionCount => suppressDuringBossCombat?.Length ?? 0;
        public int ResultSuppressionCount => suppressDuringResult?.Length ?? 0;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialHudStateCoordinator requires complete state sources and suppression groups.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
        }

        private void OnDisable()
        {
            RestoreRecordedStates();
            currentMode = TutorialHudMode.Normal;
        }

        private void LateUpdate()
        {
            var result = serviceRoot.StateMachine.Current == GameState.Result || resultOverlay.activeSelf;
            var nextMode = TutorialHudModeResolver.Resolve(
                result,
                dialoguePanel.activeInHierarchy,
                introductionCard.activeInHierarchy,
                bossArenaHost.CombatActive);

            if (nextMode != currentMode) ChangeMode(nextMode);
            EnforceCurrentMode();
        }

        private void ChangeMode(TutorialHudMode nextMode)
        {
            RestoreRecordedStates();
            currentMode = nextMode;
            RecordCurrentStates(GetSuppressionGroup(currentMode));
        }

        private void EnforceCurrentMode()
        {
            var group = GetSuppressionGroup(currentMode);
            for (var index = 0; index < group.Length; index++)
            {
                if (group[index].activeSelf) group[index].SetActive(false);
            }

            if (currentMode == TutorialHudMode.Result && !resultOverlay.activeSelf)
                resultOverlay.SetActive(true);
        }

        private void RecordCurrentStates(GameObject[] group)
        {
            for (var index = 0; index < group.Length; index++)
            {
                var item = group[index];
                if (!recordedActiveStates.ContainsKey(item)) recordedActiveStates.Add(item, item.activeSelf);
            }
        }

        private void RestoreRecordedStates()
        {
            foreach (var pair in recordedActiveStates)
            {
                if (pair.Key != null) pair.Key.SetActive(pair.Value);
            }
            recordedActiveStates.Clear();
        }

        private GameObject[] GetSuppressionGroup(TutorialHudMode mode)
        {
            return mode switch
            {
                TutorialHudMode.Dialogue => suppressDuringDialogue,
                TutorialHudMode.BossCombat => suppressDuringBossCombat,
                TutorialHudMode.Result => suppressDuringResult,
                _ => Array.Empty<GameObject>()
            };
        }

        private static bool HasCompleteGroup(GameObject[] group)
        {
            if (group == null || group.Length == 0) return false;
            for (var index = 0; index < group.Length; index++)
            {
                if (group[index] == null) return false;
            }
            return true;
        }
    }
}
