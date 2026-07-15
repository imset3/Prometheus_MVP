using System;

namespace Narthex.Core
{
    public readonly struct GameStateChanged
    {
        public readonly GameState Previous;
        public readonly GameState Current;

        public GameStateChanged(GameState previous, GameState current)
        {
            Previous = previous;
            Current = current;
        }
    }

    public sealed class GameStateMachine
    {
        private readonly GameEventBus events;

        public GameState Current { get; private set; } = GameState.Booting;

        public GameStateMachine(GameEventBus events)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public bool TryTransition(GameState next)
        {
            if (Current == next) return false;
            if (!CanTransition(Current, next)) return false;

            var previous = Current;
            Current = next;
            events.Publish(new GameStateChanged(previous, next));
            return true;
        }

        private static bool CanTransition(GameState from, GameState to)
        {
            if (to == GameState.Error) return true;
            if (from == GameState.Error) return to == GameState.Booting;
            if (to == GameState.Paused) return from == GameState.Tutorial || from == GameState.InRun;
            if (from == GameState.Paused) return to == GameState.Tutorial || to == GameState.InRun;
            if (from == GameState.Booting) return to == GameState.Loading || to == GameState.Title;
            if (from == GameState.Loading) return to == GameState.Title || to == GameState.Tutorial || to == GameState.InRun;
            if (from == GameState.Title) return to == GameState.Loading || to == GameState.Tutorial;
            if (from == GameState.Tutorial) return to == GameState.Loading || to == GameState.Result || to == GameState.InRun;
            if (from == GameState.InRun) return to == GameState.Loading || to == GameState.Result;
            if (from == GameState.Result) return to == GameState.Loading || to == GameState.Title;
            return false;
        }
    }
}
