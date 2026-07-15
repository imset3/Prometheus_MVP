using System;
using Narthex.Content;
using Narthex.Core;

namespace Narthex.Gameplay
{
    public sealed class AbilityExecutor
    {
        private readonly GameEventBus events;

        public AbilityExecutor(GameEventBus events)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public bool Execute(string casterId, AbilityDefinition ability)
        {
            if (ability == null || string.IsNullOrWhiteSpace(ability.StableId)) return false;

            events.Publish(new AbilityRequested(casterId, ability.StableId));
            events.Publish(new AbilityExecuted(casterId, ability.StableId));
            return true;
        }
    }
}
