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
        [SerializeField] private GameObject[] activateOnStart = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] deactivateOnStart = Array.Empty<GameObject>();

        public string QuestId => questId;
        public string StageId => stageId;
        public string[] Lines => lines;
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

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null && beats != null && beats.Length > 0;

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
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
        }

        private void Start()
        {
            Present(questSequenceHost.CurrentQuestId);
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message)
        {
            Present(message.QuestId);
        }

        private void Present(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return;

            foreach (var beat in beats)
            {
                if (beat == null || beat.QuestId != questId) continue;
                ApplySceneState(beat);
                serviceRoot.Events.Publish(new TutorialNarrativeChanged(beat.QuestId, beat.StageId, beat.Lines));
                return;
            }
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
