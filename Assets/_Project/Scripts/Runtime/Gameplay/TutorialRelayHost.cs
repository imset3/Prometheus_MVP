using Narthex.Core;
using Narthex.Save;
using UnityEngine;

namespace Narthex.Gameplay
{
    public enum TutorialRelayState { Enemy, Player }

    public sealed class TutorialRelayHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private SaveSystemHost saveSystemHost;
        [SerializeField] private QuestManagerHost questManagerHost;
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private Collider2D activationTrigger;
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private GameObject enemyVisual;
        [SerializeField] private GameObject playerVisual;
        [SerializeField] private string relayId = "RELAY-TUTO-001";
        [SerializeField] private string activationQuestId = "QST-TUTO-007";
        [SerializeField] private string stageId = "TUTORIAL";

        public TutorialRelayState State { get; private set; } = TutorialRelayState.Enemy;
        public bool HasValidSetup => serviceRoot != null && saveSystemHost != null && questManagerHost != null && playerInputHost != null &&
                                     activationTrigger != null && playerCollider != null && enemyVisual != null &&
                                     playerVisual != null && !string.IsNullOrWhiteSpace(relayId) &&
                                     !string.IsNullOrWhiteSpace(activationQuestId);

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialRelayHost requires pre-placed service, save, input, collider, and visual references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            if (!saveSystemHost.Initialize() || !questManagerHost.Initialize())
            {
                enabled = false;
                return;
            }

            var isActivated = TutorialProgressRestore.IsRelayProgressRestored(
                saveSystemHost.System.Current.Run,
                relayId,
                activationQuestId);
            SetState(isActivated ? TutorialRelayState.Player : TutorialRelayState.Enemy);
        }

        private void OnEnable()
        {
            if (playerInputHost != null) playerInputHost.InteractRequested += TryActivate;
        }

        private void OnDisable()
        {
            if (playerInputHost != null) playerInputHost.InteractRequested -= TryActivate;
        }

        private void TryActivate()
        {
            if (State == TutorialRelayState.Player || !CanActivateForCurrentQuest() || !activationTrigger.Distance(playerCollider).isOverlapped) return;

            SetState(TutorialRelayState.Player);
            var activatedTowerIds = saveSystemHost.System.Current.Run.ActivatedTowerIds;
            if (activatedTowerIds == null)
            {
                activatedTowerIds = new System.Collections.Generic.List<string>();
                saveSystemHost.System.Current.Run.ActivatedTowerIds = activatedTowerIds;
            }
            if (!activatedTowerIds.Contains(relayId)) activatedTowerIds.Add(relayId);
            saveSystemHost.System.Save("TowerActivated");
            serviceRoot.Events.Publish(new TowerActivated(relayId, stageId));
            serviceRoot.Events.Publish(new TowerBuffRemoved(relayId));
            serviceRoot.Events.Publish(new GameplaySignal(Narthex.Content.QuestSignalType.TowerActivated, relayId));
        }

        private bool CanActivateForCurrentQuest()
        {
            return questManagerHost.System != null &&
                   questManagerHost.System.TryGetState(activationQuestId, out var state) &&
                   state.Status == QuestRuntimeStatus.InProgress;
        }

        private void SetState(TutorialRelayState nextState)
        {
            State = nextState;
            enemyVisual.SetActive(nextState == TutorialRelayState.Enemy);
            playerVisual.SetActive(nextState == TutorialRelayState.Player);
        }
    }
}
