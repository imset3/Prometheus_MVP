using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Marker-authored fall recovery. Moving the marker changes both the visible
    /// trigger volume and the vertical recovery threshold without code changes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class TutorialFallRestartHost : MonoBehaviour
    {
        [SerializeField] private TutorialRestartHost restartHost;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private Transform player;
        [SerializeField] private string requiredQuestId;

        private BoxCollider2D recoveryTrigger;
        private bool restartRequested;

        public bool HasValidSetup => restartHost != null && questSequenceHost != null && player != null &&
                                     !string.IsNullOrWhiteSpace(requiredQuestId);
        public string RequiredQuestId => requiredQuestId;
        public float RecoveryHeight => transform.position.y;

        private void Awake()
        {
            recoveryTrigger = GetComponent<BoxCollider2D>();
            recoveryTrigger.isTrigger = true;
            if (HasValidSetup) return;
            Debug.LogError("TutorialFallRestartHost requires restart, quest, player, and quest-id references.", this);
            enabled = false;
        }

        private void Update()
        {
            if (!IsRecoveryActive())
            {
                restartRequested = false;
                return;
            }

            var bounds = recoveryTrigger.bounds;
            var playerPosition = player.position;
            if (restartRequested)
            {
                if (!restartHost.IsRestarting && playerPosition.y > RecoveryHeight)
                    restartRequested = false;
                return;
            }

            if (playerPosition.y > RecoveryHeight ||
                playerPosition.x < bounds.min.x || playerPosition.x > bounds.max.x)
                return;

            RequestRestart();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || other.transform != player && !other.transform.IsChildOf(player)) return;
            RequestRestart();
        }

        private bool IsRecoveryActive() =>
            isActiveAndEnabled && questSequenceHost.CurrentQuestId == requiredQuestId;

        private void RequestRestart()
        {
            if (!IsRecoveryActive() || restartRequested) return;
            restartRequested = restartHost.TryRestartAtCheckpoint();
        }
    }
}
