using System;
using Narthex.Core;
using Narthex.Save;

namespace Narthex.Gameplay
{
    public sealed class TutorialBossCompletion
    {
        private readonly GameEventBus events;
        private readonly PermanentSaveData permanentData;
        private readonly RunSaveData runData;
        private readonly string bossId;
        private readonly string nextStageId;

        public TutorialBossCompletion(GameEventBus events, PermanentSaveData permanentData, RunSaveData runData, string bossId, string nextStageId)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.permanentData = permanentData ?? throw new ArgumentNullException(nameof(permanentData));
            this.runData = runData ?? throw new ArgumentNullException(nameof(runData));
            this.bossId = string.IsNullOrWhiteSpace(bossId) ? throw new ArgumentException("Boss id is required.", nameof(bossId)) : bossId;
            this.nextStageId = string.IsNullOrWhiteSpace(nextStageId) ? throw new ArgumentException("Next stage id is required.", nameof(nextStageId)) : nextStageId;
        }

        public bool TryComplete(BossKilled message)
        {
            if (message.BossId != bossId || permanentData.TutorialCompleted) return false;

            permanentData.TutorialCompleted = true;
            if (!permanentData.BossKillRecords.Contains(bossId)) permanentData.BossKillRecords.Add(bossId);
            if (!string.IsNullOrWhiteSpace(message.UnlockTreeId) && !permanentData.UnlockedTreeIds.Contains(message.UnlockTreeId))
                permanentData.UnlockedTreeIds.Add(message.UnlockTreeId);
            runData.CurrentStageId = nextStageId;

            events.Publish(new TutorialCompleted(bossId));
            return true;
        }
    }
}
