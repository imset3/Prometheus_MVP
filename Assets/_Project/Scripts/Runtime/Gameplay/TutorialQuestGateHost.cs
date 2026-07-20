using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Keeps a pre-placed training gate closed only while its assigned quest is active.
    /// The visual and collider remain separately replaceable for the final art pass.
    /// </summary>
    public sealed class TutorialQuestGateHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private string blockingQuestId;
        [SerializeField] private Collider2D blockingCollider;
        [SerializeField] private Renderer gateRenderer;

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null &&
                                     !string.IsNullOrWhiteSpace(blockingQuestId) &&
                                     blockingCollider != null && gateRenderer != null;
        public bool IsLocked { get; private set; }

        private void Awake()
        {
            if (HasValidSetup) return;
            Debug.LogError("TutorialQuestGateHost requires service, quest, collider, renderer, and quest ID references.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            if (!HasValidSetup) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
        }

        private void Start() => Refresh(questSequenceHost.CurrentQuestId);

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message) => Refresh(message.QuestId);

        private void Refresh(string currentQuestId)
        {
            IsLocked = currentQuestId == blockingQuestId;
            blockingCollider.enabled = IsLocked;
            gateRenderer.enabled = IsLocked;
        }
    }
}
