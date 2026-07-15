using Narthex.Core;
using Narthex.Save;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class TutorialBossEncounterHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private SaveSystemHost saveSystemHost;
        [SerializeField] private GameObject bossRoot;
        [SerializeField] private string relayId = "RELAY-TUTO-001";
        [SerializeField] private string activationQuestId = "QST-TUTO-007";

        public bool HasValidSetup => serviceRoot != null && saveSystemHost != null && bossRoot != null &&
                                     !string.IsNullOrWhiteSpace(relayId) && !string.IsNullOrWhiteSpace(activationQuestId);

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialBossEncounterHost requires pre-placed service, save, boss, and activation quest references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            if (!saveSystemHost.Initialize())
            {
                enabled = false;
                return;
            }

            bossRoot.SetActive(TutorialProgressRestore.IsRelayProgressRestored(
                saveSystemHost.System.Current.Run,
                relayId,
                activationQuestId));
        }

        private void OnEnable()
        {
            if (serviceRoot == null) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<QuestCompleted>(HandleQuestCompleted);
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<QuestCompleted>(HandleQuestCompleted);
        }

        private void HandleQuestCompleted(QuestCompleted message)
        {
            if (message.QuestId == activationQuestId) bossRoot.SetActive(true);
        }
    }
}
