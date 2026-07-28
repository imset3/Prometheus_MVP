using Narthex.Content;
using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Marker-authored dash objective. The player must pass every fire trigger in
    /// left-to-right order while dashing, then reach the finish marker.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialTrainingDashObjectiveHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private TutorialTrainingPhaseControllerHost phaseController;
        [SerializeField] private Transform player;
        [SerializeField] private string questId = "QST-TUTO-004";
        [SerializeField] private string signalTargetId = "TRAINING-DASH-FINISH";
        [SerializeField, Min(1)] private int requiredFireCount = 3;
        [SerializeField] private TutorialTrainingDashFireHost[] fires =
            System.Array.Empty<TutorialTrainingDashFireHost>();

        private int nextFireIndex;
        private bool published;

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null &&
                                     phaseController != null && player != null &&
                                     !string.IsNullOrWhiteSpace(questId) &&
                                     !string.IsNullOrWhiteSpace(signalTargetId) &&
                                     requiredFireCount > 0 && fires != null &&
                                     fires.Length == requiredFireCount && HasCompleteFireReferences();
        public int PassedFireCount => nextFireIndex;
        public int RequiredFireCount => requiredFireCount;

        private void Awake()
        {
            var trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.isTrigger = true;
            if (HasValidSetup) return;
            Debug.LogError(
                "TutorialTrainingDashObjectiveHost requires services, phase controller, player, and objective ids.",
                this);
            enabled = false;
        }

        private void OnEnable() => ResetProgress();

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (published || questSequenceHost.CurrentQuestId != questId || !IsPlayer(other)) return;
            if (nextFireIndex < requiredFireCount)
            {
                ResetProgress();
                phaseController.TryRestartCurrentPhase();
                return;
            }

            published = true;
            serviceRoot.Initialize();
            serviceRoot.Events.Publish(new GameplaySignal(QuestSignalType.PortalUsed, signalTargetId));
        }

        public bool TryNotifyFirePassed(int fireIndex)
        {
            if (published || questSequenceHost.CurrentQuestId != questId || fireIndex != nextFireIndex)
                return false;
            nextFireIndex++;
            return true;
        }

        public void ResetProgress()
        {
            nextFireIndex = 0;
            published = false;
            if (fires == null) return;
            foreach (var fire in fires)
                if (fire != null)
                    fire.gameObject.SetActive(true);
        }

        private bool HasCompleteFireReferences()
        {
            foreach (var fire in fires)
                if (fire == null)
                    return false;
            return true;
        }

        private bool IsPlayer(Collider2D other)
        {
            return other != null && (other.transform == player || other.transform.IsChildOf(player));
        }
    }
}
