using System;
using System.Collections.Generic;
using Narthex.Content;
using Narthex.Core;

namespace Narthex.Gameplay
{
    public sealed class CombatSystem
    {
        private readonly GameEventBus events;
        private readonly Dictionary<string, ActorRuntimeState> actors = new Dictionary<string, ActorRuntimeState>();

        public CombatSystem(GameEventBus events)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public void Register(ActorRuntimeState actor)
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.ActorId)) throw new ArgumentException("Actor must have an id.");
            actors.Add(actor.ActorId, actor);
        }

        public bool TryApplyDamage(string targetId, DamagePacket packet)
        {
            if (!actors.TryGetValue(targetId, out var target)) return false;
            if (!target.IsAlive || target.IsInvincible || packet.Damage <= 0) return false;

            target.CurrentHealth = Math.Max(0, target.CurrentHealth - packet.Damage);
            target.State = target.CurrentHealth == 0 ? CombatState.Dead : CombatState.Hit;
            events.Publish(new HitConfirmed(packet.SourceId, target.ActorId, packet.HitboxId, packet.Damage));

            if (target.Kind == CombatActorKind.Player)
            {
                if (target is PlayerRuntimeState player) player.HitCount++;
                events.Publish(new PlayerHit(target.ActorId, packet.Damage));
            }

            if (target.CurrentHealth > 0) return true;

            switch (target.Kind)
            {
                case CombatActorKind.Player:
                    events.Publish(new PlayerDead(target.ActorId, "Damage", target.StageId));
                    break;
                case CombatActorKind.Enemy:
                    events.Publish(new EnemyKilled(target.ActorId, target.StageId));
                    break;
                case CombatActorKind.Boss:
                    events.Publish(new BossKilled(target.ActorId, target.StageId, target.UnlockTreeId));
                    events.Publish(new GameplaySignal(QuestSignalType.BossKilled, target.ActorId));
                    break;
            }

            return true;
        }
    }
}
