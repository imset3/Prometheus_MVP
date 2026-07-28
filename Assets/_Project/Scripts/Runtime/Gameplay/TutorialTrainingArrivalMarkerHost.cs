using Narthex.Content;
using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Completes a marker-driven traversal objective when the player reaches this
    /// trigger. The marker transform and collider are the complete authoring surface.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialTrainingArrivalMarkerHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private Transform player;
        [SerializeField] private string questId = "QST-TUTO-004";
        [SerializeField] private string signalTargetId = "TRAINING-DASH-FINISH";

        private bool published;

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null && player != null &&
                                     !string.IsNullOrWhiteSpace(questId) &&
                                     !string.IsNullOrWhiteSpace(signalTargetId);

        private void Awake()
        {
            var trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.isTrigger = true;
            if (HasValidSetup) return;
            Debug.LogError("TutorialTrainingArrivalMarkerHost requires quest, player, and signal references.", this);
            enabled = false;
        }

        private void OnEnable() => published = false;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (published || questSequenceHost.CurrentQuestId != questId || !IsPlayer(other)) return;
            published = true;
            serviceRoot.Initialize();
            serviceRoot.Events.Publish(new GameplaySignal(QuestSignalType.PortalUsed, signalTargetId));
        }

        private bool IsPlayer(Collider2D other)
        {
            return other != null && (other.transform == player || other.transform.IsChildOf(player));
        }
    }
}
