using Narthex.Save;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Handles the pre-placed Cryon boots pickup. The component only updates
    /// serialized scene references and never creates gameplay objects at runtime.
    /// </summary>
    public sealed class TutorialBootsPickupHost : MonoBehaviour
    {
        [SerializeField] private SaveSystemHost saveSystemHost;
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private PlayerMotorHost playerMotorHost;
        [SerializeField] private Collider2D pickupTrigger;
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private GameObject pickupVisual;

        public bool HasValidSetup => saveSystemHost != null && playerInputHost != null && playerMotorHost != null &&
                                     pickupTrigger != null && playerCollider != null && pickupVisual != null;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialBootsPickupHost requires pre-placed save, player, collider, and visual references.", this);
                enabled = false;
                return;
            }

            if (!saveSystemHost.Initialize())
            {
                enabled = false;
                return;
            }

            if (saveSystemHost.System.Current.Permanent.DoubleJumpUnlocked)
            {
                playerMotorHost.UnlockDoubleJump();
                gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (playerInputHost != null) playerInputHost.InteractRequested += TryCollect;
        }

        private void OnDisable()
        {
            if (playerInputHost != null) playerInputHost.InteractRequested -= TryCollect;
        }

        private void TryCollect()
        {
            if (!enabled || !pickupTrigger.Distance(playerCollider).isOverlapped) return;

            playerMotorHost.UnlockDoubleJump();
            saveSystemHost.System.Current.Permanent.DoubleJumpUnlocked = true;
            saveSystemHost.System.Save("CryonBootsCollected");
            pickupVisual.SetActive(false);
            gameObject.SetActive(false);
        }
    }
}
