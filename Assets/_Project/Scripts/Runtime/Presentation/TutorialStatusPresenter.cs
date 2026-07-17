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
        [SerializeField] private Text keyPromptText;
        [SerializeField] private Text stageCaptionText;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private string initialMessage = "훈련용 적을 처치하세요.";
        [SerializeField] private string completedMessage = "튜토리얼 완료";
        [SerializeField] private string progressFormat = "튜토리얼 {0}/{1}";
        [SerializeField, Min(1)] private int questCount = 8;

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
            {
                statusText.text = FormatObjective(new TutorialObjectiveChanged(
                    questSequenceHost.CurrentQuestId,
                    questSequenceHost.CurrentObjectiveText,
                    0));
                UpdateKeyPrompt(questSequenceHost.CurrentQuestId);
                UpdateStageCaption(questSequenceHost.CurrentQuestId);
            }
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message)
        {
            statusText.text = FormatObjective(message);
            UpdateKeyPrompt(message.QuestId);
            UpdateStageCaption(message.QuestId);
        }

        private void HandleTutorialCompleted(TutorialCompleted message)
        {
            statusText.text = completedMessage;
            ClearKeyPrompt();
            if (stageCaptionText != null) stageCaptionText.text = "탐사 준비 완료";
        }

        private string FormatObjective(TutorialObjectiveChanged message)
        {
            if (string.IsNullOrWhiteSpace(progressFormat))
                return message.ObjectiveText;

            var progress = string.Format(progressFormat, message.StepIndex + 1, questCount);
            return string.IsNullOrWhiteSpace(message.ObjectiveText)
                ? progress
                : $"{progress}\n{message.ObjectiveText}";
        }

        private void UpdateKeyPrompt(string questId)
        {
            if (keyPromptText == null) return;

            keyPromptText.text = questId switch
            {
                "QST-TUTO-001" => "이동  [ A ]  [ D ]",
                "QST-TUTO-002" => "점프 · 활공  [ SPACE ]",
                "QST-TUTO-003" => "기본 공격  [ ENTER ]",
                "QST-TUTO-004" => "대시  [ LEFT SHIFT ]",
                "QST-TUTO-005" => "나르텍스 펄스  [ 2 ]",
                "QST-TUTO-006" => "모듈 트리  [ I ]",
                "QST-TUTO-007" => "상호작용  [ F ]",
                "QST-TUTO-008" => "공격 [ ENTER ]  ·  펄스 [ 2 ]",
                _ => string.Empty
            };
        }

        private void ClearKeyPrompt()
        {
            if (keyPromptText != null)
                keyPromptText.text = string.Empty;
        }

        private void UpdateStageCaption(string questId)
        {
            if (stageCaptionText == null) return;

            stageCaptionText.text = questId switch
            {
                "QST-TUTO-001" => "ADAMAS HQ  /  오리엔테이션",
                "QST-TUTO-002" or "QST-TUTO-003" or "QST-TUTO-004" or "QST-TUTO-005" => "훈련 구역  /  전투 시뮬레이션",
                "QST-TUTO-006" => "모듈 제어실  /  시스템 동기화",
                "QST-TUTO-007" => "외곽 접근로  /  크리온 장비 회수",
                "QST-TUTO-008" => "광물 저장고  /  헬테 교전",
                _ => string.Empty
            };
        }
    }
}
