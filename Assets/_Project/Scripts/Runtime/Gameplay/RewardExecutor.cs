using System;
using System.Collections.Generic;
using Narthex.Content;
using Narthex.Core;
using Narthex.Save;

namespace Narthex.Gameplay
{
    public sealed class RewardExecutor : IDisposable
    {
        private readonly GameEventBus events;
        private readonly PermanentSaveData permanentData;
        private readonly RunSaveData runData;
        private readonly Dictionary<string, RewardDefinition> definitions = new Dictionary<string, RewardDefinition>();

        public RewardExecutor(GameEventBus events, PermanentSaveData permanentData, RunSaveData runData)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.permanentData = permanentData ?? throw new ArgumentNullException(nameof(permanentData));
            this.runData = runData ?? throw new ArgumentNullException(nameof(runData));
            events.Subscribe<QuestCompleted>(OnQuestCompleted);
            events.Subscribe<BossKilled>(OnBossKilled);
        }

        public void Register(RewardDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.StableId)) throw new ArgumentException("Reward definition must have a stableId.");
            definitions.Add(definition.StableId, definition);
        }

        public bool Grant(string rewardId)
        {
            if (!definitions.TryGetValue(rewardId, out var definition)) return false;
            switch (definition.RewardType)
            {
                case RewardType.ModulePoint:
                    runData.ModulePoints += definition.Amount;
                    break;
                case RewardType.TreeUnlock:
                case RewardType.BossModuleTreeUnlock:
                    if (!permanentData.UnlockedTreeIds.Contains(definition.TargetId)) permanentData.UnlockedTreeIds.Add(definition.TargetId);
                    break;
                case RewardType.StageUnlock:
                    if (!runData.QuestIds.Contains(definition.TargetId)) runData.QuestIds.Add(definition.TargetId);
                    break;
                case RewardType.None:
                    break;
                default:
                    return false;
            }
            events.Publish(new RewardGranted(definition.StableId, definition.TargetId, definition.Amount));
            return true;
        }

        private void OnQuestCompleted(QuestCompleted message)
        {
            foreach (var rewardId in message.RewardIds) Grant(rewardId);
        }

        private void OnBossKilled(BossKilled message)
        {
            if (string.IsNullOrEmpty(message.UnlockTreeId)) return;
            if (!permanentData.UnlockedTreeIds.Contains(message.UnlockTreeId)) permanentData.UnlockedTreeIds.Add(message.UnlockTreeId);
            events.Publish(new RewardGranted("BOSS_TREE_UNLOCK", message.UnlockTreeId, 1));
        }

        public void Dispose()
        {
            events.Unsubscribe<QuestCompleted>(OnQuestCompleted);
            events.Unsubscribe<BossKilled>(OnBossKilled);
        }
    }
}
