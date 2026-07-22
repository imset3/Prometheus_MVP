using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    public static class TutorialTriggerSweepPolicy
    {
        public static bool Intersects(Bounds bounds, Vector2 start, Vector2 end)
        {
            var minimum = (Vector2)bounds.min;
            var maximum = (Vector2)bounds.max;
            var delta = end - start;
            var minimumTime = 0f;
            var maximumTime = 1f;
            return IntersectsAxis(start.x, delta.x, minimum.x, maximum.x, ref minimumTime, ref maximumTime) &&
                   IntersectsAxis(start.y, delta.y, minimum.y, maximum.y, ref minimumTime, ref maximumTime);
        }

        private static bool IntersectsAxis(
            float start, float delta, float minimum, float maximum, ref float minimumTime, ref float maximumTime)
        {
            if (Mathf.Approximately(delta, 0f)) return start >= minimum && start <= maximum;
            var inverse = 1f / delta;
            var first = (minimum - start) * inverse;
            var second = (maximum - start) * inverse;
            if (first > second) (first, second) = (second, first);
            minimumTime = Mathf.Max(minimumTime, first);
            maximumTime = Mathf.Min(maximumTime, second);
            return minimumTime <= maximumTime;
        }
    }

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
