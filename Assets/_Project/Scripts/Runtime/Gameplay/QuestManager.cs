using System;
using System.Collections.Generic;
using Narthex.Content;
using Narthex.Core;

namespace Narthex.Gameplay
{
    public enum QuestRuntimeStatus { Locked, Available, InProgress, Completed, Rewarded }

    public sealed class QuestRuntimeState
    {
        public QuestRuntimeStatus Status { get; internal set; } = QuestRuntimeStatus.Locked;
        internal readonly Dictionary<string, int> Progress = new Dictionary<string, int>();
    }

    public sealed class QuestManager : IDisposable
    {
        private readonly GameEventBus events;
        private readonly Dictionary<string, QuestDefinition> definitions = new Dictionary<string, QuestDefinition>();
        private readonly Dictionary<string, QuestRuntimeState> states = new Dictionary<string, QuestRuntimeState>();

        public QuestManager(GameEventBus events)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            events.Subscribe<GameplaySignal>(OnSignal);
        }

        public void Register(QuestDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.StableId)) throw new ArgumentException("Quest definition must have a stableId.");
            definitions.Add(definition.StableId, definition);
            states[definition.StableId] = new QuestRuntimeState();
        }

        public bool Start(string questId)
        {
            if (!states.TryGetValue(questId, out var state)) return false;
            if (state.Status != QuestRuntimeStatus.Locked && state.Status != QuestRuntimeStatus.Available) return false;
            state.Status = QuestRuntimeStatus.InProgress;
            return true;
        }

        public bool TryGetState(string questId, out QuestRuntimeState state) => states.TryGetValue(questId, out state);

        private void OnSignal(GameplaySignal signal)
        {
            foreach (var pair in definitions)
            {
                var state = states[pair.Key];
                if (state.Status != QuestRuntimeStatus.InProgress) continue;
                var definition = pair.Value;
                var allComplete = true;
                foreach (var condition in definition.Conditions)
                {
                    if (condition == null) continue;
                    if (condition.SignalType == signal.SignalType && (string.IsNullOrEmpty(condition.TargetId) || condition.TargetId == signal.TargetId))
                    {
                        state.Progress[condition.StableId] = Math.Min(condition.RequiredAmount, GetProgress(state, condition.StableId) + signal.Amount);
                    }
                    if (GetProgress(state, condition.StableId) < condition.RequiredAmount) allComplete = false;
                }

                if (!allComplete) continue;
                state.Status = QuestRuntimeStatus.Completed;
                var rewardIds = new List<string>();
                foreach (var reward in definition.Rewards) if (reward != null) rewardIds.Add(reward.StableId);
                events.Publish(new QuestCompleted(pair.Key, rewardIds.ToArray()));
            }
        }

        private static int GetProgress(QuestRuntimeState state, string conditionId)
        {
            return state.Progress.TryGetValue(conditionId, out var value) ? value : 0;
        }

        public void Dispose() { events.Unsubscribe<GameplaySignal>(OnSignal); }
    }
}
