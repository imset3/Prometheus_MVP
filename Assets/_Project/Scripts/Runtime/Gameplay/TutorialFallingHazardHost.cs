using UnityEngine;

namespace Narthex.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialFallingHazardHost : MonoBehaviour
    {
        [SerializeField] private TutorialTrainingSpawnHost trainingHost;
        private bool armed;

        public bool HasValidSetup => trainingHost != null && TryGetComponent<Collider2D>(out var hitbox) && hitbox.isTrigger;

        private void Awake()
        {
            var trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.isTrigger = true;
            if (HasValidSetup) return;
            Debug.LogError("TutorialFallingHazardHost requires a TutorialTrainingSpawnHost reference.", this);
            enabled = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!armed || trainingHost == null || !trainingHost.TryRestartDashSection(other)) return;
            armed = false;
        }

        public void SetArmed(bool value) => armed = value;
    }
}
