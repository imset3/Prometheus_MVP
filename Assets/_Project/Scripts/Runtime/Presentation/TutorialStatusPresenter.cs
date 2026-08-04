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
        private int meetingVisitCount = 1;
        private int corridorVisitCount;
        private TutorialObjectiveChanged currentObjective;

        public string CurrentLocationName => currentLocationName;
        public string CurrentProgressId => ResolveNotionProgressId(currentObjective.QuestId);

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
            RefreshFromCurrentQuest();
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
            RefreshFromCurrentQuest();
        }

        private void LateUpdate()
        {
            if (questSequenceHost != null && questSequenceHost.CurrentQuestId != currentObjective.QuestId)
                RefreshFromCurrentQuest();
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
            var normalized = NormalizeLocation(message.LocationName);
            if (normalized != currentLocationName)
            {
                if (normalized == "회의장") meetingVisitCount++;
                if (normalized == "복도") corridorVisitCount++;
            }
            UpdateLocationCaption(normalized);
            RefreshCurrentStatus();
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
            var totalSteps = questSequenceHost != null && questSequenceHost.TotalStepCount > 0
                ? questSequenceHost.TotalStepCount
                : questCount;
            var progress = string.IsNullOrWhiteSpace(progressFormat)
                ? string.Empty
                : string.Format(progressFormat, message.StepIndex + 1, totalSteps);
            var notionId = ResolveNotionProgressId(message.QuestId);
            var objective = ResolveContextualObjective(message.QuestId, message.ObjectiveText);

            var header = progress;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!string.IsNullOrWhiteSpace(notionId))
                header = string.IsNullOrWhiteSpace(header) ? $"[{notionId}]" : $"{header}  <color=#888888>[{notionId}]</color>";
#endif

            return string.IsNullOrWhiteSpace(objective)
                ? header
                : string.IsNullOrWhiteSpace(header)
                    ? objective
                    : $"{header}\n<size=18><b>{objective}</b></size>";
        }

        private void RefreshCurrentStatus()
        {
            if (statusText == null || string.IsNullOrWhiteSpace(currentObjective.QuestId)) return;
            statusText.text = FormatObjective(currentObjective);
            UpdateKeyPrompt(currentObjective.QuestId);
        }

        public void RefreshFromCurrentQuest()
        {
            if (statusText == null || questSequenceHost == null ||
                string.IsNullOrWhiteSpace(questSequenceHost.CurrentQuestId))
                return;

            currentObjective = new TutorialObjectiveChanged(
                questSequenceHost.CurrentQuestId,
                questSequenceHost.CurrentObjectiveText,
                questSequenceHost.CurrentStepIndex);
            statusText.text = FormatObjective(currentObjective);
            UpdateKeyPrompt(currentObjective.QuestId);
            var fallbackLocation = ResolveFallbackLocation(currentObjective.QuestId);
            if (string.IsNullOrWhiteSpace(currentLocationName) ||
                currentObjective.QuestId is "QST-TUTO-007-A" or "QST-TUTO-007-B" or "QST-TUTO-008")
                UpdateLocationCaption(fallbackLocation);
        }

        private void UpdateKeyPrompt(string questId)
        {
            if (keyPromptText == null) return;

            keyPromptText.text = questId switch
            {
                "QST-TUTO-001" => $"이동  <color=#FFD700><b>[ {Binding("Move", "A / D")} ]</b></color>",
                "QST-TUTO-002" => $"점프 · 활공  <color=#FFD700><b>[ {Binding("Jump", "SPACE")} ]</b></color>",
                "QST-TUTO-003" => $"기본 공격  <color=#FFD700><b>[ {Binding("Attack", "LMB")} ]</b></color>",
                "QST-TUTO-004" => $"대시  <color=#FFD700><b>[ {Binding("Sprint", "LEFT SHIFT")} ]</b></color>",
                "QST-TUTO-005" => $"원거리 공격  <color=#FFD700><b>[ {Binding("Next", "2")} ]</b></color>",
                "QST-TUTO-006" => $"더블 점프  <color=#FFD700><b>[ {Binding("Jump", "SPACE")} ×2 ]</b></color>",
                "QST-TUTO-007" => $"상호작용  <color=#FFD700><b>[ {Binding("Interact", "F")} ]</b></color>",
                "QST-TUTO-007-A" or "QST-TUTO-007-B" => $"기본 공격 <color=#FFD700><b>[ {Binding("Attack", "LMB")} ]</b></color>  ·  원거리 공격 <color=#FFD700><b>[ {Binding("Next", "2")} ]</b></color>",
                "QST-TUTO-008" => $"기본 공격 <color=#FFD700><b>[ {Binding("Attack", "LMB")} ]</b></color>  ·  원거리 공격 <color=#FFD700><b>[ {Binding("Next", "2")} ]</b></color>",
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
            if (rawLocationName.Contains("F스테이지")) return "본부 외곽 통로";
            if (rawLocationName.Contains("G스테이지")) return "나디르 선착장 진입로";
            if (rawLocationName.Contains("전투") && rawLocationName.Contains("1")) return "본부 외곽 통로";
            if (rawLocationName.Contains("전투") && rawLocationName.Contains("2")) return "나디르 선착장 진입로";
            if (rawLocationName.Contains("진입로")) return "나디르 선착장 진입로";
            if (rawLocationName.Contains("선착장")) return "나디르 선착장";
            if (rawLocationName.Contains("외부")) return "본부 외곽";
            return rawLocationName;
        }

        private string ResolveNotionProgressId(string questId)
        {
            return questId switch
            {
                "QST-TUTO-001" when currentLocationName == "숨겨진 방" => "TUTO_B_01",
                "QST-TUTO-001" when currentLocationName == "복도" => "TUTO_C_01",
                "QST-TUTO-001" when currentLocationName == "회의장" && meetingVisitCount >= 2 => "TUTO_A_02",
                "QST-TUTO-001" => "TUTO_A_01",
                "QST-TUTO-004" => "TUTO_D_02",
                "QST-TUTO-006" => "TUTO_D_03",
                "QST-TUTO-002" => "TUTO_D_04",
                "QST-TUTO-003" => "TUTO_D_05",
                "QST-TUTO-005" => "TUTO_D_06",
                "QST-TUTO-007" when currentLocationName == "훈련장" => "TUTO_D_07",
                "QST-TUTO-007" when currentLocationName == "회의장" => "TUTO_A_03",
                "QST-TUTO-007" when currentLocationName == "복도" && corridorVisitCount >= 3 => "TUTO_C_03",
                "QST-TUTO-007" when currentLocationName == "복도" => "TUTO_C_02",
                "QST-TUTO-007" when currentLocationName == "본부 외곽" => "TUTO_E_01",
                "QST-TUTO-007-A" => "TUTO_F_01",
                "QST-TUTO-007-B" => "TUTO_G_01",
                "QST-TUTO-008" => "TUTO_H_01",
                _ => string.Empty
            };
        }

        private string ResolveContextualObjective(string questId, string fallback)
        {
            return ResolveNotionProgressId(questId) switch
            {
                "TUTO_A_01" => "테우스와 함께 비행선 패스키를 찾으러 이동",
                "TUTO_B_01" => "활공으로 높은 곳의 비행선 패스키를 획득하고 회의장으로 복귀",
                "TUTO_A_02" => "복도를 지나 훈련장 입구로 이동",
                "TUTO_C_01" => "복도 끝의 훈련장 입구에 도달",
                "TUTO_D_02" => "무적 대시로 불기둥 3개를 통과해 도착점에 도달",
                "TUTO_D_03" => "더블 점프로 가장 높은 발판의 도착 마커에 도달",
                "TUTO_D_04" => "전방 투사체를 점프로 3회 회피",
                "TUTO_D_05" => "훈련용 에너미에게 기본 공격 3콤보 적중",
                "TUTO_D_06" => "원거리 공격 한 발로 훈련용 에너미 3기 동시 타격",
                "TUTO_D_07" => "습격 경보에 따라 훈련을 중단하고 회의장으로 대피",
                "TUTO_C_02" => "복도 끝의 회의장 방향에 도달",
                "TUTO_A_03" => "에온·아르온과 대화를 마치고 외부 출구로 이동",
                "TUTO_C_03" => "복도 끝 사다리를 올라 본부 외곽으로 이동",
                "TUTO_E_01" => "습격 상황을 확인하고 본부 외곽 통로로 이동",
                "TUTO_F_01" => "본부 외곽 통로의 판도라 개체를 모두 처치",
                "TUTO_G_01" => "나디르 선착장 진입로의 판도라 개체를 모두 처치",
                "TUTO_H_01" => "헬테와 조우해 전투를 완료",
                _ => fallback
            };
        }

        private static string ResolveFallbackLocation(string questId)
        {
            return questId switch
            {
                "QST-TUTO-001" => "회의장",
                "QST-TUTO-002" or "QST-TUTO-003" or "QST-TUTO-004" or "QST-TUTO-005" or "QST-TUTO-006" =>
                    "훈련장",
                "QST-TUTO-007" => "복도",
                "QST-TUTO-007-A" => "본부 외곽 통로",
                "QST-TUTO-007-B" => "나디르 선착장 진입로",
                "QST-TUTO-008" => "나디르 선착장",
                _ => string.Empty
            };
        }
    }
}
