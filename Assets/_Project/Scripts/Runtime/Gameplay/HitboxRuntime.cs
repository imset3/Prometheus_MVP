using System.Collections.Generic;
using Narthex.Content;

namespace Narthex.Gameplay
{
    public sealed class HitboxRuntime
    {
        private readonly CombatSystem combat;
        private readonly HitboxDefinition definition;
        private readonly string sourceId;
        private readonly HashSet<string> hitTargets = new HashSet<string>();

        public HitboxRuntime(CombatSystem combat, HitboxDefinition definition, string sourceId)
        {
            this.combat = combat;
            this.definition = definition;
            this.sourceId = sourceId;
        }

        public bool TryHit(string targetId)
        {
            if (definition == null || string.IsNullOrWhiteSpace(targetId)) return false;
            if (hitTargets.Contains(targetId)) return false;
            if (definition.MaxHitsPerCast > 0 && hitTargets.Count >= definition.MaxHitsPerCast) return false;

            var packet = new DamagePacket(sourceId, definition.StableId, definition.Damage);
            if (!combat.TryApplyDamage(targetId, packet)) return false;
            hitTargets.Add(targetId);
            return true;
        }
    }

    public sealed class ProjectileRuntime
    {
        private readonly HitboxRuntime hitbox;
        public bool IsActive { get; private set; } = true;

        public ProjectileRuntime(HitboxRuntime hitbox)
        {
            this.hitbox = hitbox;
        }

        public bool TryImpact(string targetId)
        {
            if (!IsActive || !hitbox.TryHit(targetId)) return false;
            IsActive = false;
            return true;
        }
    }
}
