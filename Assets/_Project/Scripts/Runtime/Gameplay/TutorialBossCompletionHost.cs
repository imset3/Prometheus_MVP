using Narthex.Core;
using Narthex.Save;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class TutorialBossCompletionHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private SaveSystemHost saveSystemHost;
        [SerializeField] private string tutorialBossId = "BOSS-TUTO-HELTE";
        [SerializeField] private string nextStageId = "CHAPTER_01";

        private TutorialBossCompletion completion;

        private void Awake()
        {
            if (serviceRoot == null || saveSystemHost == null || string.IsNullOrWhiteSpace(tutorialBossId) || string.IsNullOrWhiteSpace(nextStageId))
            {
                Debug.LogError("TutorialBossCompletionHost requires pre-placed service, save, and boss id references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            if (!saveSystemHost.Initialize())
            {
                enabled = false;
                return;
            }

            completion = new TutorialBossCompletion(serviceRoot.Events, saveSystemHost.System.Current.Permanent, saveSystemHost.System.Current.Run, tutorialBossId, nextStageId);
        }

        private void OnEnable()
        {
            if (serviceRoot == null) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<BossKilled>(HandleBossKilled);
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<BossKilled>(HandleBossKilled);
        }

        private void HandleBossKilled(BossKilled message)
        {
            if (completion == null || !completion.TryComplete(message)) return;
            saveSystemHost.System.Save("TutorialBossCompleted");
        }
    }
}
