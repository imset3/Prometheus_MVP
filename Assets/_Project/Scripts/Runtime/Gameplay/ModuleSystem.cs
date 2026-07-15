using System;
using System.Collections.Generic;
using Narthex.Content;
using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class ModuleRuntimeState
    {
        public bool Unlocked { get; internal set; }
        public bool Equipped { get; internal set; }
        public int UpgradeLevel { get; internal set; }
        public int SlotIndex { get; internal set; } = -1;
    }

    public sealed class ModuleSystem
    {
        private readonly GameEventBus events;
        private readonly AbilityExecutor abilityExecutor;
        private readonly Dictionary<string, ModuleDefinition> definitions = new Dictionary<string, ModuleDefinition>();
        private readonly Dictionary<string, ModuleRuntimeState> states = new Dictionary<string, ModuleRuntimeState>();
        private readonly Dictionary<string, float> cooldownEndsAt = new Dictionary<string, float>();

        public ModuleSystem(GameEventBus events, AbilityExecutor abilityExecutor)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.abilityExecutor = abilityExecutor ?? throw new ArgumentNullException(nameof(abilityExecutor));
        }

        public void Register(ModuleDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.StableId)) throw new ArgumentException("Module definition must have a stableId.");
            if (definitions.ContainsKey(definition.StableId)) return;
            definitions.Add(definition.StableId, definition);
            states[definition.StableId] = new ModuleRuntimeState();
        }

        public bool IsRegistered(string moduleId) => definitions.ContainsKey(moduleId);

        public bool Unlock(string moduleId)
        {
            if (!definitions.ContainsKey(moduleId)) return false;
            var state = states[moduleId];
            if (state.Unlocked) return false;
            state.Unlocked = true;
            var definition = definitions[moduleId];
            events.Publish(new ModuleUnlocked(moduleId, definition.TreeId));
            return true;
        }

        public bool Equip(string moduleId, int slotIndex)
        {
            if (!states.TryGetValue(moduleId, out var state) || !state.Unlocked) return false;
            foreach (var candidate in states.Values)
            {
                if (candidate.SlotIndex != slotIndex) continue;
                candidate.Equipped = false;
                candidate.SlotIndex = -1;
            }
            state.Equipped = true;
            state.SlotIndex = slotIndex;
            events.Publish(new ModuleEquipped(moduleId, slotIndex));
            return true;
        }

        public bool TryUse(string casterId, string moduleId)
        {
            if (!definitions.TryGetValue(moduleId, out var definition)) return false;
            var state = states[moduleId];
            if (!state.Unlocked || !state.Equipped || definition.Ability == null) return false;
            if (GetCooldownRemaining(moduleId) > 0f) return false;
            if (!abilityExecutor.Execute(casterId, definition.Ability)) return false;
            cooldownEndsAt[moduleId] = Time.time + Mathf.Max(0f, definition.Ability.Cooldown);
            events.Publish(new ModuleUsed(moduleId));
            events.Publish(new GameplaySignal(QuestSignalType.ModuleUsed, moduleId));
            return true;
        }

        public bool TryGetState(string moduleId, out ModuleRuntimeState state) => states.TryGetValue(moduleId, out state);

        public float GetCooldownRemaining(string moduleId)
        {
            return cooldownEndsAt.TryGetValue(moduleId, out var cooldownEnd)
                ? Mathf.Max(0f, cooldownEnd - Time.time)
                : 0f;
        }
    }
}
