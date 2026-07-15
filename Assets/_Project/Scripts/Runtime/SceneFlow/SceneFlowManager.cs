using System;
using Narthex.Core;

namespace Narthex.SceneFlow
{
    public enum SceneFlowRequestType
    {
        NewGame,
        Continue,
        EnterTutorial,
        EnterStage,
        UsePortal,
        RestartTutorial,
        RestartRun,
        ShowResult
    }

    public readonly struct SceneFlowRequest
    {
        public readonly SceneFlowRequestType Type;
        public readonly string TargetId;

        public SceneFlowRequest(SceneFlowRequestType type, string targetId = null)
        {
            Type = type;
            TargetId = targetId;
        }
    }

    public sealed class SceneFlowManager
    {
        private readonly GameStateMachine stateMachine;
        private bool transitionLocked;

        public SceneFlowManager(GameStateMachine stateMachine)
        {
            this.stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        }

        public bool TryBegin(SceneFlowRequest request)
        {
            if (transitionLocked) return false;
            if (stateMachine.Current != GameState.Loading && !stateMachine.TryTransition(GameState.Loading))
                return false;
            transitionLocked = true;
            return true;
        }

        public void Complete()
        {
            transitionLocked = false;
        }

        public bool IsTransitionLocked => transitionLocked;
    }
}
