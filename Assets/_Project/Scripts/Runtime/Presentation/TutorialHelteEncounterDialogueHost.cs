using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Presents Helte's first encounter after the player has walked through the dock approach.
    /// The in-scene presented flag intentionally survives checkpoint retries so the player returns
    /// to the pre-fight checkpoint without repeating the conversation.
    /// </summary>
    public sealed class TutorialHelteEncounterDialogueHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private TutorialDialoguePresenter dialoguePresenter;
        [SerializeField] private TutorialRestartHost restartHost;
        [SerializeField] private TutorialObjectiveBeaconHost objectiveBeacon;
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private Collider2D encounterTrigger;
        [SerializeField] private Transform retryCheckpoint;
        [SerializeField] private Transform postDialogueObjective;
        [SerializeField] private string questId = "QST-TUTO-008";
        [SerializeField] private string stageId = "선착장 · 헬테 조우";
        [TextArea(2, 5)] [SerializeField] private string[] lines =
        {
            "헬테: 아다마스의 아이가 여기까지 들어왔군.",
            "프로메: 길을 비켜 줘. 우리는 판도라 공장으로 가야 해."
        };

        private bool encounterPresented;

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null &&
                                     dialoguePresenter != null && restartHost != null &&
                                     objectiveBeacon != null &&
                                     playerCollider != null && encounterTrigger != null &&
                                     encounterTrigger.isTrigger && retryCheckpoint != null &&
                                     postDialogueObjective != null &&
                                     !string.IsNullOrWhiteSpace(questId) &&
                                     lines != null && lines.Length == 2;
        public bool EncounterPresented => encounterPresented;
        public int LineCount => lines?.Length ?? 0;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError(
                    "TutorialHelteEncounterDialogueHost requires quest, dialogue, restart, player, trigger, and retry checkpoint references.",
                    this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
        }

        private void Update()
        {
            if (encounterPresented || dialoguePresenter.IsShowing ||
                questSequenceHost.CurrentQuestId != questId ||
                !encounterTrigger.Distance(playerCollider).isOverlapped)
                return;

            encounterPresented = true;
            restartHost.SetRuntimeCheckpoint(questId, retryCheckpoint);
            objectiveBeacon.SetExternalTarget(postDialogueObjective);
            serviceRoot.Events.Publish(new TutorialNarrativeChanged(questId + "-HELTE", stageId, lines));
        }
    }
}
