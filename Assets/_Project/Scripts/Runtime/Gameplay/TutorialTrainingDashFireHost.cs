using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Permanent training flame. A live invulnerability dash passes through; any
    /// other contact restarts the currently active marker-authored training phase.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialTrainingDashFireHost : MonoBehaviour
    {
        [SerializeField] private TutorialTrainingPhaseControllerHost phaseController;
        [SerializeField] private TutorialTrainingDashObjectiveHost dashObjective;
        [SerializeField] private PlayerMotorHost playerMotor;
        [SerializeField] private Transform player;
        [SerializeField, Min(0)] private int fireIndex;

        private bool restartRequested;
        private bool passReported;

        public bool HasValidSetup => phaseController != null && dashObjective != null &&
                                     playerMotor != null && player != null;

        private void Awake()
        {
            var trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.isTrigger = true;
            if (HasValidSetup) return;
            Debug.LogError("TutorialTrainingDashFireHost requires phase controller, player motor, and player.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            restartRequested = false;
            passReported = false;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (IsPlayer(other)) passReported = false;
        }

        private void OnTriggerEnter2D(Collider2D other) => HandleContact(other);
        private void OnTriggerStay2D(Collider2D other) => HandleContact(other);

        private void HandleContact(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            if (playerMotor.IsDashing)
            {
                if (!passReported)
                {
                    passReported = true;
                    if (dashObjective.TryNotifyFirePassed(fireIndex))
                        gameObject.SetActive(false);
                }
                return;
            }
            if (restartRequested) return;
            dashObjective.ResetProgress();
            restartRequested = phaseController.TryRestartCurrentPhase();
        }

        private bool IsPlayer(Collider2D other)
        {
            return other != null && (other.transform == player || other.transform.IsChildOf(player));
        }
    }
}
