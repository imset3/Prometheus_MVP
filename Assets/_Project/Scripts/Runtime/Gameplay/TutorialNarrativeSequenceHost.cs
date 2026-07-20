using System;
using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    [Serializable]
    public sealed class TutorialNarrativeBeat
    {
        [SerializeField] private string questId;
        [SerializeField] private string stageId;
        [TextArea(2, 5)] [SerializeField] private string[] lines = Array.Empty<string>();
        [SerializeField] private string deferUntilPortalTargetId;
        [SerializeField] private GameObject[] activateOnStart = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] deactivateOnStart = Array.Empty<GameObject>();

        public string QuestId => questId;
        public string StageId => stageId;
        public string[] Lines => lines;
        public string DeferUntilPortalTargetId => deferUntilPortalTargetId;
        public GameObject[] ActivateOnStart => activateOnStart;
        public GameObject[] DeactivateOnStart => deactivateOnStart;
    }

    /// <summary>
    /// Applies the narrative layer to the existing quest sequence. All referenced UI,
    /// NPCs, terrain sections, and markers must be pre-placed in the scene.
    /// </summary>
    public sealed class TutorialNarrativeSequenceHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private TutorialNarrativeBeat[] beats = Array.Empty<TutorialNarrativeBeat>();

        private TutorialNarrativeBeat pendingBeat;

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null && beats != null && beats.Length > 0;
        public int BeatCount => beats?.Length ?? 0;

        public int GetLineCount(string questId)
        {
            if (beats == null) return 0;
            foreach (var beat in beats)
                if (beat != null && beat.QuestId == questId)
                    return beat.Lines?.Length ?? 0;
            return 0;
        }

        public bool HasDeferredBeat(string questId, string portalTargetId)
        {
            if (beats == null) return false;
            foreach (var beat in beats)
                if (beat != null && beat.QuestId == questId && beat.DeferUntilPortalTargetId == portalTargetId)
                    return true;
            return false;
        }

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialNarrativeSequenceHost requires pre-placed ServiceRoot, quest sequence, and narrative beats.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
        }

        private void OnEnable()
        {
            if (serviceRoot == null) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            serviceRoot.Events.Subscribe<GameplaySignal>(HandleGameplaySignal);
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            serviceRoot?.Events?.Unsubscribe<GameplaySignal>(HandleGameplaySignal);
        }

        private void Start()
        {
            Present(questSequenceHost.CurrentQuestId);
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message)
        {
            Present(message.QuestId);
        }

        private void HandleGameplaySignal(GameplaySignal message)
        {
            if (pendingBeat == null || message.SignalType != Narthex.Content.QuestSignalType.PortalUsed ||
                message.TargetId != pendingBeat.DeferUntilPortalTargetId)
                return;

            var beat = pendingBeat;
            pendingBeat = null;
            PresentBeat(beat);
        }

        private void Present(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return;

            pendingBeat = null;

            foreach (var beat in beats)
            {
                if (beat == null || beat.QuestId != questId) continue;
                if (!string.IsNullOrWhiteSpace(beat.DeferUntilPortalTargetId))
                {
                    pendingBeat = beat;
                    return;
                }

                PresentBeat(beat);
                return;
            }
        }

        private void PresentBeat(TutorialNarrativeBeat beat)
        {
            ApplySceneState(beat);
            serviceRoot.Events.Publish(new TutorialNarrativeChanged(beat.QuestId, beat.StageId, beat.Lines));
        }

        private static void ApplySceneState(TutorialNarrativeBeat beat)
        {
            SetActive(beat.DeactivateOnStart, false);
            SetActive(beat.ActivateOnStart, true);
        }

        private static void SetActive(GameObject[] targets, bool active)
        {
            if (targets == null) return;
            foreach (var target in targets)
                if (target != null) target.SetActive(active);
        }
    }
}
