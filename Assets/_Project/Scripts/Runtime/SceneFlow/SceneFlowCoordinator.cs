using System;
using Narthex.Core;

namespace Narthex.SceneFlow
{
    public sealed class SceneFlowCoordinator : IDisposable
    {
        private readonly GameEventBus events;
        private readonly SceneFlowManager manager;

        public SceneFlowCoordinator(GameEventBus events, SceneFlowManager manager)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            events.Subscribe<RunRestarted>(OnRunRestarted);
        }

        private void OnRunRestarted(RunRestarted message)
        {
            manager.TryBegin(new SceneFlowRequest(SceneFlowRequestType.RestartRun, message.StartStageId));
        }

        public void Dispose() { events.Unsubscribe<RunRestarted>(OnRunRestarted); }
    }
}
