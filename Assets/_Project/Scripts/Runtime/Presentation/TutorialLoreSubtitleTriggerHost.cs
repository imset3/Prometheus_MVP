using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialLoreSubtitleTriggerHost : MonoBehaviour
    {
        [SerializeField] private TutorialLoreSubtitlePresenter presenter;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private Transform player;
        [SerializeField] private string requiredQuestId;
        [SerializeField, TextArea(2, 4)] private string subtitleText;

        private Collider2D triggerCollider;
        private bool presented;
        private Vector2 previousPlayerPosition;
        private bool hasPreviousPlayerPosition;

        public bool HasValidSetup => presenter != null && questSequenceHost != null && player != null &&
                                     !string.IsNullOrWhiteSpace(requiredQuestId) &&
                                     !string.IsNullOrWhiteSpace(subtitleText) &&
                                     GetComponent<Collider2D>() is Collider2D candidate && candidate.isTrigger;

        private void Awake()
        {
            triggerCollider = GetComponent<Collider2D>();
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialLoreSubtitleTriggerHost has invalid presenter, quest, player, text, or trigger setup.", this);
                enabled = false;
            }
            previousPlayerPosition = player != null ? (Vector2)player.position : Vector2.zero;
            hasPreviousPlayerPosition = player != null;
        }

        private void LateUpdate()
        {
            if (player == null || triggerCollider == null) return;
            var currentPosition = (Vector2)player.position;
            if (!presented && hasPreviousPlayerPosition && questSequenceHost.CurrentQuestId == requiredQuestId &&
                TutorialTriggerSweepPolicy.Intersects(triggerCollider.bounds, previousPlayerPosition, currentPosition))
                Present();
            previousPlayerPosition = currentPosition;
            hasPreviousPlayerPosition = true;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryPresent(other);
        private void OnTriggerStay2D(Collider2D other) => TryPresent(other);

        private void TryPresent(Collider2D other)
        {
            if (presented || other == null || questSequenceHost.CurrentQuestId != requiredQuestId) return;
            var candidate = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
            if (candidate != player && !candidate.IsChildOf(player) && !player.IsChildOf(candidate)) return;

            Present();
        }

        private void Present()
        {
            if (presented) return;
            presented = true;
            presenter.ShowSubtitle(subtitleText);
            triggerCollider.enabled = false;
        }
    }
}
