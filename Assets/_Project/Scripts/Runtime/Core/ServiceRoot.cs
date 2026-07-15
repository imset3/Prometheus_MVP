using UnityEngine;

namespace Narthex.Core
{
    public sealed class ServiceRoot : MonoBehaviour
    {
        private bool initialized;

        public GameEventBus Events { get; private set; }
        public GameClock Clock { get; private set; }
        public GameStateMachine StateMachine { get; private set; }

private void Awake()
        {
            Initialize();
        }

public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Events = new GameEventBus();
            Clock = new GameClock();
            StateMachine = new GameStateMachine(Events);
        }


        private void OnDestroy()
        {
            Events?.Dispose();
        }
    }
}
