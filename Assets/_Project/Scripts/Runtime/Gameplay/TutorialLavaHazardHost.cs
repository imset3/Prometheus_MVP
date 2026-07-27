using UnityEngine;

namespace Narthex.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialLavaHazardHost : MonoBehaviour
    {
        [SerializeField] private TutorialEnvironmentHazardCoordinatorHost coordinator;
        [SerializeField] private Transform player;
        [SerializeField] private string hazardId = "G-LAVA-01";

        public bool HasValidSetup => coordinator != null && player != null &&
                                     !string.IsNullOrWhiteSpace(hazardId);
        public bool ReturnsToLatestSafePoint => true;

        private void Awake()
        {
            var trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.isTrigger = true;
            if (HasValidSetup) return;

            Debug.LogError(
                "TutorialLavaHazardHost requires coordinator, player, and hazard id references.",
                this);
            enabled = false;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryHandle(other);
        private void OnTriggerStay2D(Collider2D other) => TryHandle(other);

        private void TryHandle(Collider2D other)
        {
            if (other == null || (other.transform != player && !other.transform.IsChildOf(player))) return;
            coordinator.TryHandleLava(hazardId);
        }
    }
}
