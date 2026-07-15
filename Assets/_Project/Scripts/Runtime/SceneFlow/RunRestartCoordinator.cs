using System;
using Narthex.Core;
using Narthex.Save;

namespace Narthex.SceneFlow
{
    public sealed class RunRestartCoordinator : IDisposable
    {
        private readonly GameEventBus events;
        private readonly RunContext runContext;
        private readonly PermanentSaveData permanentData;
        private readonly string restartStageId;
        private bool restarting;

        public RunRestartCoordinator(GameEventBus events, RunContext runContext, PermanentSaveData permanentData, string restartStageId)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.runContext = runContext ?? throw new ArgumentNullException(nameof(runContext));
            this.permanentData = permanentData ?? throw new ArgumentNullException(nameof(permanentData));
            this.restartStageId = restartStageId;
            events.Subscribe<PlayerDead>(OnPlayerDead);
        }

        private void OnPlayerDead(PlayerDead message)
        {
            if (restarting) return;
            restarting = true;
            permanentData.TotalDeaths++;
            runContext.Reset(restartStageId);
            events.Publish(new RunRestarted(runContext.Data.RunNumber, restartStageId));
            restarting = false;
        }

        public void Dispose() => events.Unsubscribe<PlayerDead>(OnPlayerDead);
    }
}
