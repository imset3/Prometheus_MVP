using UnityEngine;

namespace Narthex.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialJumpProjectileHazardHost : MonoBehaviour
    {
        [SerializeField] private TutorialJumpTrainingHost trainingHost;
        private bool armed;

        public bool HasValidSetup => trainingHost != null && TryGetComponent<Collider2D>(out var hitbox) && hitbox.isTrigger;

        private void Awake()
        {
            var trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.isTrigger = true;
            if (trainingHost != null) return;
            Debug.LogError("TutorialJumpProjectileHazardHost requires a TutorialJumpTrainingHost reference.", this);
            enabled = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!armed || trainingHost == null || !trainingHost.TryRestartJumpSection(other)) return;
            armed = false;
        }

        public void SetArmed(bool value) => armed = value;
    }
}
