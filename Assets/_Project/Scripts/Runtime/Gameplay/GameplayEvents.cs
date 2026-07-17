using Narthex.Content;

namespace Narthex.Gameplay
{
    public readonly struct AbilityRequested
    {
        public readonly string CasterId;
        public readonly string AbilityId;
        public AbilityRequested(string casterId, string abilityId) { CasterId = casterId; AbilityId = abilityId; }
    }

    public readonly struct AbilityExecuted
    {
        public readonly string CasterId;
        public readonly string AbilityId;
        public AbilityExecuted(string casterId, string abilityId) { CasterId = casterId; AbilityId = abilityId; }
    }

    public readonly struct ModuleUnlocked
    {
        public readonly string ModuleId;
        public readonly string TreeId;
        public ModuleUnlocked(string moduleId, string treeId) { ModuleId = moduleId; TreeId = treeId; }
    }

    public readonly struct ModuleEquipped
    {
        public readonly string ModuleId;
        public readonly int SlotIndex;
        public ModuleEquipped(string moduleId, int slotIndex) { ModuleId = moduleId; SlotIndex = slotIndex; }
    }

    public readonly struct ModuleUsed
    {
        public readonly string ModuleId;
        public ModuleUsed(string moduleId) { ModuleId = moduleId; }
    }

    public readonly struct ModuleTreeOpened
    {
        public readonly string TreeId;
        public ModuleTreeOpened(string treeId) { TreeId = treeId; }
    }

    public readonly struct ModuleTreeAccessGranted
    {
        public readonly string TreeId;
        public ModuleTreeAccessGranted(string treeId) { TreeId = treeId; }
    }

    public readonly struct GameplaySignal
    {
        public readonly QuestSignalType SignalType;
        public readonly string TargetId;
        public readonly int Amount;
        public GameplaySignal(QuestSignalType signalType, string targetId, int amount = 1)
        {
            SignalType = signalType;
            TargetId = targetId;
            Amount = amount;
        }
    }

    public readonly struct QuestCompleted
    {
        public readonly string QuestId;
        public readonly string[] RewardIds;
        public QuestCompleted(string questId, string[] rewardIds) { QuestId = questId; RewardIds = rewardIds; }
    }

    public readonly struct RewardGranted
    {
        public readonly string RewardId;
        public readonly string TargetId;
        public readonly int Amount;
        public RewardGranted(string rewardId, string targetId, int amount) { RewardId = rewardId; TargetId = targetId; Amount = amount; }
    }

    public readonly struct TutorialCompleted
    {
        public readonly string GoalId;
        public TutorialCompleted(string goalId) { GoalId = goalId; }
    }

    public readonly struct TutorialObjectiveChanged
    {
        public readonly string QuestId;
        public readonly string ObjectiveText;
        public readonly int StepIndex;

        public TutorialObjectiveChanged(string questId, string objectiveText, int stepIndex)
        {
            QuestId = questId;
            ObjectiveText = objectiveText;
            StepIndex = stepIndex;
        }
    }

    public readonly struct TutorialNarrativeChanged
    {
        public readonly string QuestId;
        public readonly string StageId;
        public readonly string[] Lines;

        public TutorialNarrativeChanged(string questId, string stageId, string[] lines)
        {
            QuestId = questId;
            StageId = stageId;
            Lines = lines;
        }
    }

    public readonly struct TowerActivated
    {
        public readonly string TowerId;
        public readonly string StageId;

        public TowerActivated(string towerId, string stageId)
        {
            TowerId = towerId;
            StageId = stageId;
        }
    }

    public readonly struct TowerBuffRemoved
    {
        public readonly string TowerId;
        public TowerBuffRemoved(string towerId) { TowerId = towerId; }
    }

    public readonly struct BossKilled
    {
        public readonly string BossId;
        public readonly string StageId;
        public readonly string UnlockTreeId;
        public BossKilled(string bossId, string stageId, string unlockTreeId)
        {
            BossId = bossId;
            StageId = stageId;
            UnlockTreeId = unlockTreeId;
        }
    }
}
