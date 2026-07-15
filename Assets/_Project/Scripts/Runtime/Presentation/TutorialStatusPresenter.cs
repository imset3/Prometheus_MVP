using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    public sealed class TutorialStatusPresenter : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private Text statusText;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private string initialMessage = "훈련용 적을 처치하세요.";
        [SerializeField] private string completedMessage = "튜토리얼 완료";

        private void Awake()
        {
            if (serviceRoot == null || statusText == null)
            {
                Debug.LogError("TutorialStatusPresenter requires pre-placed ServiceRoot and UI Text references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            statusText.text = initialMessage;
        }

        private void OnEnable()
        {
            if (serviceRoot == null) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            serviceRoot.Events.Subscribe<TutorialCompleted>(HandleTutorialCompleted);
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            serviceRoot?.Events?.Unsubscribe<TutorialCompleted>(HandleTutorialCompleted);
        }

        private void Start()
        {
            if (questSequenceHost != null && !string.IsNullOrWhiteSpace(questSequenceHost.CurrentObjectiveText))
                statusText.text = questSequenceHost.CurrentObjectiveText;
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message)
        {
            statusText.text = message.ObjectiveText;
        }

        private void HandleTutorialCompleted(TutorialCompleted message)
        {
            statusText.text = completedMessage;
        }
    }
}
