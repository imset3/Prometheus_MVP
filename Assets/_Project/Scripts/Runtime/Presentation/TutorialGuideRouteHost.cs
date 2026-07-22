using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Starts a pre-placed companion route after the HQ introduction dialogue closes.
    /// Route points remain scene-authored so the final art pass can replace visuals freely.
    /// </summary>
    public sealed class TutorialGuideRouteHost : MonoBehaviour
    {
        [SerializeField] private TutorialDialoguePresenter dialoguePresenter;
        [SerializeField] private TutorialGuideCompanionHost guideCompanion;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private string activationQuestId = "QST-TUTO-001";
        [SerializeField] private Transform[] waypoints;

        public bool HasValidSetup => dialoguePresenter != null && guideCompanion != null &&
                                     questSequenceHost != null && waypoints != null && waypoints.Length > 0;
        public Transform[] Waypoints => waypoints;

        private void Awake()
        {
            if (HasValidSetup) return;
            Debug.LogError("TutorialGuideRouteHost requires dialogue, guide, quest sequence, and waypoint references.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            if (dialoguePresenter != null) dialoguePresenter.DialogueClosed += HandleDialogueClosed;
        }

        private void OnDisable()
        {
            if (dialoguePresenter != null) dialoguePresenter.DialogueClosed -= HandleDialogueClosed;
        }

        private void HandleDialogueClosed()
        {
            if (questSequenceHost.CurrentQuestId != activationQuestId) return;
            guideCompanion.BeginGuide(waypoints);
        }
    }
}
