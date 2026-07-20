using Narthex.Content;
using Narthex.Core;
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
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private PlayerMotorHost playerMotorHost;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private ModuleTreeManagerHost moduleTreeManagerHost;
        [SerializeField] private Collider2D pickupTrigger;
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private GameObject pickupVisual;
        [SerializeField] private string equipmentQuestId = "QST-TUTO-006";
        [SerializeField] private string moduleId = "MOD-TUTO-001";
        [SerializeField, Min(0)] private int moduleSlotIndex;
        [SerializeField] private string packageSignalTargetId = "CRYON-EQUIPMENT-PACKAGE";

        private bool collected;
        private bool restoredSignalPublished;

        public bool HasValidSetup => saveSystemHost != null && serviceRoot != null && playerInputHost != null &&
                                     playerMotorHost != null && questSequenceHost != null && moduleTreeManagerHost != null &&
                                     pickupTrigger != null && playerCollider != null && pickupVisual != null &&
                                     !string.IsNullOrWhiteSpace(equipmentQuestId) && !string.IsNullOrWhiteSpace(moduleId) &&
                                     !string.IsNullOrWhiteSpace(packageSignalTargetId);
        public bool IsCollected => collected;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialBootsPickupHost requires pre-placed save, player, collider, and visual references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            if (!saveSystemHost.Initialize() || !moduleTreeManagerHost.Initialize())
            {
                enabled = false;
                return;
            }

            if (saveSystemHost.System.Current.Permanent.DoubleJumpUnlocked)
            {
                playerMotorHost.UnlockDoubleJump();
                collected = true;
                pickupTrigger.enabled = false;
                pickupVisual.SetActive(false);
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

        private void Start() => PublishRestoredPackageIfNeeded();

        private void Update() => PublishRestoredPackageIfNeeded();

        private void TryCollect()
        {
            if (!enabled || collected || questSequenceHost.CurrentQuestId != equipmentQuestId ||
                !pickupTrigger.Distance(playerCollider).isOverlapped) return;

            playerMotorHost.UnlockDoubleJump();
            moduleTreeManagerHost.System.TryEquipModule(moduleId, moduleSlotIndex);
            saveSystemHost.System.Current.Permanent.DoubleJumpUnlocked = true;
            saveSystemHost.System.Save("CryonBootsCollected");
            collected = true;
            pickupTrigger.enabled = false;
            pickupVisual.SetActive(false);
            serviceRoot.Events.Publish(new GameplaySignal(QuestSignalType.PortalUsed, packageSignalTargetId));
        }

        private void PublishRestoredPackageIfNeeded()
        {
            if (!collected || restoredSignalPublished || questSequenceHost.CurrentQuestId != equipmentQuestId) return;
            restoredSignalPublished = true;
            moduleTreeManagerHost.System.TryEquipModule(moduleId, moduleSlotIndex);
            serviceRoot.Events.Publish(new GameplaySignal(QuestSignalType.PortalUsed, packageSignalTargetId));
        }
    }
}
