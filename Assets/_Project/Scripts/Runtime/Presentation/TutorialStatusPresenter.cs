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
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private string initialMessage = "훈련용 적을 처치하세요.";
        [SerializeField] private string completedMessage = "튜토리얼 완료";
        [SerializeField] private string progressFormat = "튜토리얼 {0}/{1}";
        [SerializeField, Min(1)] private int questCount = 8;

        private string currentLocationName = "회의장";
        private TutorialObjectiveChanged currentObjective;

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
            UpdateLocationCaption(currentLocationName);
        }

        private void OnEnable()
        {
            if (serviceRoot == null) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            serviceRoot.Events.Subscribe<TutorialLocationChanged>(HandleLocationChanged);
            serviceRoot.Events.Subscribe<QuestProgressChanged>(HandleQuestProgressChanged);
            serviceRoot.Events.Subscribe<TutorialCompleted>(HandleTutorialCompleted);
            if (playerInputHost != null) playerInputHost.BindingDisplayChanged += RefreshBindings;
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            serviceRoot?.Events?.Unsubscribe<TutorialLocationChanged>(HandleLocationChanged);
            serviceRoot?.Events?.Unsubscribe<QuestProgressChanged>(HandleQuestProgressChanged);
            serviceRoot?.Events?.Unsubscribe<TutorialCompleted>(HandleTutorialCompleted);
            if (playerInputHost != null) playerInputHost.BindingDisplayChanged -= RefreshBindings;
        }

        private void Start()
        {
            if (questSequenceHost != null && !string.IsNullOrWhiteSpace(questSequenceHost.CurrentObjectiveText))
            {
                currentObjective = new TutorialObjectiveChanged(
                    questSequenceHost.CurrentQuestId,
                    questSequenceHost.CurrentObjectiveText,
                    0);
                statusText.text = FormatObjective(currentObjective);
                UpdateKeyPrompt(questSequenceHost.CurrentQuestId);
                if (string.IsNullOrWhiteSpace(currentLocationName))
                    UpdateLocationCaption(ResolveFallbackLocation(questSequenceHost.CurrentQuestId));
            }
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message)
        {
            currentObjective = message;
            statusText.text = FormatObjective(message);
            UpdateKeyPrompt(message.QuestId);
            if (string.IsNullOrWhiteSpace(currentLocationName))
                UpdateLocationCaption(ResolveFallbackLocation(message.QuestId));
        }

        private void HandleLocationChanged(TutorialLocationChanged message)
        {
            UpdateLocationCaption(message.LocationName);
        }

        private void HandleQuestProgressChanged(QuestProgressChanged message)
        {
            if (message.QuestId != currentObjective.QuestId) return;
            statusText.text =
                $"{FormatObjective(currentObjective)}\n진행  {message.CurrentAmount}/{message.RequiredAmount}";
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
                "QST-TUTO-001" => $"이동  [ {Binding("Move", "A / D")} ]",
                "QST-TUTO-002" => $"점프 · 활공  [ {Binding("Jump", "SPACE")} ]",
                "QST-TUTO-003" => $"기본 공격  [ {Binding("Attack", "LMB")} ]",
                "QST-TUTO-004" => $"대시  [ {Binding("Sprint", "LEFT SHIFT")} ]",
                "QST-TUTO-005" => $"원거리 공격  [ {Binding("Next", "2")} ]",
                "QST-TUTO-006" => $"더블 점프  [ {Binding("Jump", "SPACE")} ×2 ]",
                "QST-TUTO-007" => $"상호작용  [ {Binding("Interact", "F")} ]",
                "QST-TUTO-007-A" or "QST-TUTO-007-B" => $"기본 공격 [ {Binding("Attack", "LMB")} ]  ·  원거리 공격 [ {Binding("Next", "2")} ]",
                "QST-TUTO-008" => $"기본 공격 [ {Binding("Attack", "LMB")} ]  ·  원거리 공격 [ {Binding("Next", "2")} ]",
                _ => string.Empty
            };
        }

        private string Binding(string actionName, string fallback)
        {
            return playerInputHost != null ? playerInputHost.GetBindingDisplayName(actionName, fallback) : fallback;
        }

        private void RefreshBindings()
        {
            if (questSequenceHost != null) UpdateKeyPrompt(questSequenceHost.CurrentQuestId);
        }

        private void ClearKeyPrompt()
        {
            if (keyPromptText != null)
                keyPromptText.text = string.Empty;
        }

        private void UpdateLocationCaption(string rawLocationName)
        {
            if (stageCaptionText == null) return;

            currentLocationName = NormalizeLocation(rawLocationName);
            stageCaptionText.text = currentLocationName;
        }

        private static string NormalizeLocation(string rawLocationName)
        {
            if (string.IsNullOrWhiteSpace(rawLocationName)) return string.Empty;
            if (rawLocationName.Contains("숨겨진")) return "숨겨진 방";
            if (rawLocationName.Contains("훈련장")) return "훈련장";
            if (rawLocationName.Contains("회의장")) return "회의장";
            if (rawLocationName.Contains("복도")) return "복도";
            if (rawLocationName.Contains("선착장")) return "선착장";
            if (rawLocationName.Contains("F스테이지")) return "외부 전투 구역 1";
            if (rawLocationName.Contains("G스테이지")) return "외부 전투 구역 2";
            if (rawLocationName.Contains("전투") && rawLocationName.Contains("1")) return "외부 전투 구역 1";
            if (rawLocationName.Contains("전투") && rawLocationName.Contains("2")) return "외부 전투 구역 2";
            if (rawLocationName.Contains("외부")) return "외부";
            return rawLocationName;
        }

        private static string ResolveFallbackLocation(string questId)
        {
            return questId switch
            {
                "QST-TUTO-001" => "회의장",
                "QST-TUTO-002" or "QST-TUTO-003" or "QST-TUTO-004" or "QST-TUTO-005" or "QST-TUTO-006" =>
                    "훈련장",
                "QST-TUTO-007" => "복도",
                "QST-TUTO-007-A" => "외부 전투 구역 1",
                "QST-TUTO-007-B" => "외부 전투 구역 2",
                "QST-TUTO-008" => "선착장",
                _ => string.Empty
            };
        }
    }
}
