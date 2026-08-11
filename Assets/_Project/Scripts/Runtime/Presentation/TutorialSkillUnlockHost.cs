using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    public static class TutorialSkillUnlockPolicy
    {
        public static bool HasReachedStep(int currentStepIndex, int unlockStepIndex) =>
            unlockStepIndex >= 0 && currentStepIndex >= unlockStepIndex;
    }

    /// <summary>
    /// Connects tutorial progression to player-facing skill availability. Unlocks are
    /// derived from the ordered quest sequence, so checkpoint restores remain stable.
    /// </summary>
    public sealed class TutorialSkillUnlockHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private PlayerRangedAttackHost rangedAttack;
        [SerializeField] private TutorialTheusRangedSupportHost theusRangedSupport;
        [SerializeField] private TutorialBossArenaHost bossArenaHost;
        [SerializeField] private TutorialLoreSubtitlePresenter subtitlePresenter;
        [SerializeField] private string rangedUnlockQuestId = "QST-TUTO-005";
        [SerializeField] private string focusedVolleyUnlockQuestId = "QST-TUTO-007-A";
        [SerializeField] private string rangedUnlockMessage = "테우스 · 원거리 공격을 사용할 수 있어!  [1]";
        [SerializeField] private string focusedVolleyUnlockMessage = "테우스 · 이제 나도 제대로 도울게. 집중포화 해금!  [2]";
        [SerializeField] private string bossSkillUnlockMessage = "테우스 · 프로메, 연속 동작을 써 봐. 4연속 참격 해금!  [3]";

        private bool rangedUnlockAnnounced;
        private bool focusedVolleyUnlockAnnounced;
        private bool bossSkillUnlockAnnounced;
        private bool previousFightStarted;

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null && rangedAttack != null &&
                                     theusRangedSupport != null &&
                                     bossArenaHost != null && subtitlePresenter != null &&
                                     !string.IsNullOrWhiteSpace(rangedUnlockQuestId) &&
                                     !string.IsNullOrWhiteSpace(focusedVolleyUnlockQuestId);

        private void Awake()
        {
            if (HasValidSetup) return;
            Debug.LogError("TutorialSkillUnlockHost requires progression, ranged, Theus volley, boss arena, and subtitle references.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            if (!HasValidSetup) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
        }

        private void Start()
        {
            if (!HasValidSetup) return;
            RefreshRangedUnlock(false);
            RefreshFocusedVolleyUnlock(false);
            previousFightStarted = bossArenaHost.FightStarted;
            if (previousFightStarted) AnnounceBossSkillUnlock();
        }

        private void Update()
        {
            if (!HasValidSetup) return;
            var fightStarted = bossArenaHost.FightStarted && !bossArenaHost.FightCompleted;
            if (fightStarted && !previousFightStarted) AnnounceBossSkillUnlock();
            previousFightStarted = fightStarted;
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message)
        {
            RefreshRangedUnlock(message.QuestId == rangedUnlockQuestId);
            RefreshFocusedVolleyUnlock(message.QuestId == focusedVolleyUnlockQuestId);
        }

        private void RefreshFocusedVolleyUnlock(bool announce)
        {
            var unlockStepIndex = questSequenceHost.FindStepIndex(focusedVolleyUnlockQuestId);
            var unlocked = TutorialSkillUnlockPolicy.HasReachedStep(
                questSequenceHost.CurrentStepIndex,
                unlockStepIndex);
            theusRangedSupport.SetFocusedVolleyUnlocked(unlocked);
            if (unlocked && announce && !focusedVolleyUnlockAnnounced)
            {
                focusedVolleyUnlockAnnounced = true;
                subtitlePresenter.ShowSubtitle(focusedVolleyUnlockMessage);
            }
        }

        private void RefreshRangedUnlock(bool announce)
        {
            var unlockStepIndex = questSequenceHost.FindStepIndex(rangedUnlockQuestId);
            var unlocked = TutorialSkillUnlockPolicy.HasReachedStep(
                questSequenceHost.CurrentStepIndex,
                unlockStepIndex);
            rangedAttack.SetUnlocked(unlocked);
            if (unlocked && announce && !rangedUnlockAnnounced)
            {
                rangedUnlockAnnounced = true;
                subtitlePresenter.ShowSubtitle(rangedUnlockMessage);
            }
        }

        private void AnnounceBossSkillUnlock()
        {
            if (bossSkillUnlockAnnounced) return;
            bossSkillUnlockAnnounced = true;
            subtitlePresenter.ShowSubtitle(bossSkillUnlockMessage);
        }
    }
}
