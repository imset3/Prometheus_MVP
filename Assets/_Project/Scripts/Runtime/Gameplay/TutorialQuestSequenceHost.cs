using Narthex.Content;
using Narthex.Core;
using Narthex.Save;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class TutorialQuestSequenceHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private SaveSystemHost saveSystemHost;
        [SerializeField] private QuestManagerHost questManagerHost;
        [SerializeField] private QuestDefinition[] questSequence;
        [SerializeField] private string[] objectiveTexts;

        private int currentStep = -1;

        public string CurrentObjectiveText => currentStep >= 0 && currentStep < objectiveTexts.Length
            ? objectiveTexts[currentStep]
            : string.Empty;
        public string CurrentQuestId => currentStep >= 0 && currentStep < questSequence.Length && questSequence[currentStep] != null
            ? questSequence[currentStep].StableId
            : string.Empty;
        public bool HasValidSequence => serviceRoot != null && saveSystemHost != null && questManagerHost != null && questSequence != null &&
                                        objectiveTexts != null && questSequence.Length > 0 &&
                                        questSequence.Length == objectiveTexts.Length;

        private void Awake()
        {
            if (!HasValidSequence)
            {
                Debug.LogError("TutorialQuestSequenceHost requires matching pre-placed quest and objective arrays.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            if (!saveSystemHost.Initialize() || !questManagerHost.Initialize())
            {
                enabled = false;
                return;
            }

            foreach (var quest in questSequence)
            {
                if (quest == null || string.IsNullOrWhiteSpace(quest.StableId))
                {
                    Debug.LogError("TutorialQuestSequenceHost has an invalid quest definition.", this);
                    enabled = false;
                    return;
                }

                questManagerHost.System.Register(quest);
            }

            if (saveSystemHost.System.Current.Permanent.TutorialCompleted)
            {
                enabled = false;
                return;
            }

            StartStep(FindFirstIncompleteStep());
        }

        private void OnEnable()
        {
            if (serviceRoot == null) return;
            serviceRoot.Initialize();
            serviceRoot.Events?.Subscribe<QuestCompleted>(HandleQuestCompleted);
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<QuestCompleted>(HandleQuestCompleted);
        }

        private void Start()
        {
            PublishCurrentObjective();
        }

        private void HandleQuestCompleted(QuestCompleted message)
        {
            if (currentStep < 0 || currentStep >= questSequence.Length) return;
            if (message.QuestId != questSequence[currentStep].StableId) return;

            RecordQuestCompletion(message.QuestId);

            var nextStep = currentStep + 1;
            if (nextStep < questSequence.Length) StartStep(nextStep);
        }

        private void StartStep(int stepIndex)
        {
            currentStep = stepIndex;
            if (!questManagerHost.System.Start(questSequence[currentStep].StableId))
            {
                Debug.LogError($"Failed to start tutorial quest '{questSequence[currentStep].StableId}'.", this);
                enabled = false;
                return;
            }

            PublishCurrentObjective();
        }

        private void PublishCurrentObjective()
        {
            if (currentStep < 0 || currentStep >= questSequence.Length) return;
            serviceRoot.Events.Publish(new TutorialObjectiveChanged(
                questSequence[currentStep].StableId,
                CurrentObjectiveText,
                currentStep));
        }

        private int FindFirstIncompleteStep()
        {
            var questIds = new string[questSequence.Length];
            for (var index = 0; index < questSequence.Length; index++) questIds[index] = questSequence[index].StableId;
            return TutorialProgressRestore.FindFirstIncompleteQuestIndex(saveSystemHost.System.Current.Run, questIds);
        }

        private void RecordQuestCompletion(string questId)
        {
            var completedQuestIds = saveSystemHost.System.Current.Run.QuestIds;
            if (completedQuestIds == null)
            {
                completedQuestIds = new System.Collections.Generic.List<string>();
                saveSystemHost.System.Current.Run.QuestIds = completedQuestIds;
            }
            if (completedQuestIds.Contains(questId)) return;
            completedQuestIds.Add(questId);
            saveSystemHost.System.Save("TutorialQuestCompleted");
        }
    }
}
