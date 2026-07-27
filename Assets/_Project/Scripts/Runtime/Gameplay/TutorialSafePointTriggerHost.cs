using UnityEngine;

namespace Narthex.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialSafePointTriggerHost : MonoBehaviour
    {
        [SerializeField] private TutorialEnvironmentHazardCoordinatorHost coordinator;
        [SerializeField] private Transform player;
        [SerializeField] private Transform safePoint;

        public bool HasValidSetup => coordinator != null && player != null && safePoint != null;

        private void Awake()
        {
            var trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.isTrigger = true;
            if (HasValidSetup) return;

            Debug.LogError(
                "TutorialSafePointTriggerHost requires coordinator, player, and safe point references.",
                this);
            enabled = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || (other.transform != player && !other.transform.IsChildOf(player))) return;
            coordinator.SetSafePoint(safePoint);
        }
    }
}
