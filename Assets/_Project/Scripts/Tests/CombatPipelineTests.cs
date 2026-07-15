using NUnit.Framework;
using Narthex.Content;
using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Tests
{
    public sealed class CombatPipelineTests
    {
        [Test]
        public void Hitbox_OnlyDamagesSameTargetOncePerCast()
        {
            var events = new GameEventBus();
            var combat = new CombatSystem(events);
            var target = new PlayerRuntimeState("PLAYER", 10);
            combat.Register(target);
            var definition = ScriptableObject.CreateInstance<HitboxDefinition>();
            definition.ConfigureIdentity("HITBOX-TEST");
            definition.Damage = 3;
            definition.MaxHitsPerCast = 1;
            var hitbox = new HitboxRuntime(combat, definition, "ENEMY");

            Assert.That(hitbox.TryHit("PLAYER"), Is.True);
            Assert.That(hitbox.TryHit("PLAYER"), Is.False);
            Assert.That(target.CurrentHealth, Is.EqualTo(7));
            Assert.That(target.HitCount, Is.EqualTo(1));

            events.Dispose();
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void CombatSystem_PublishesPlayerDeathAndBossKill()
        {
            var events = new GameEventBus();
            var combat = new CombatSystem(events);
            var player = new PlayerRuntimeState("PLAYER", 5) { StageId = "STG-001" };
            var boss = new ActorRuntimeState("BOSS-TUTO-HELTE", CombatActorKind.Boss, 4)
            {
                StageId = "TUTO-006",
                UnlockTreeId = "TREE-BOSS-HELTE"
            };
            combat.Register(player);
            combat.Register(boss);
            var playerDied = false;
            var bossKilled = false;
            var bossQuestSignal = false;
            events.Subscribe<PlayerDead>(_ => playerDied = true);
            events.Subscribe<BossKilled>(message => bossKilled = message.UnlockTreeId == "TREE-BOSS-HELTE");
            events.Subscribe<GameplaySignal>(message => bossQuestSignal = message.SignalType == QuestSignalType.BossKilled && message.TargetId == "BOSS-TUTO-HELTE");

            combat.TryApplyDamage("PLAYER", new DamagePacket("ENEMY", "HIT-1", 5));
            combat.TryApplyDamage("BOSS-TUTO-HELTE", new DamagePacket("PLAYER", "HIT-2", 4));

            Assert.That(playerDied, Is.True);
            Assert.That(bossKilled, Is.True);
            Assert.That(bossQuestSignal, Is.True);
            Assert.That(player.State, Is.EqualTo(CombatState.Dead));
            Assert.That(boss.State, Is.EqualTo(CombatState.Dead));
            events.Dispose();
        }

        [Test]
        public void Projectile_DeactivatesAfterSuccessfulImpact()
        {
            var events = new GameEventBus();
            var combat = new CombatSystem(events);
            combat.Register(new ActorRuntimeState("ENEMY", CombatActorKind.Enemy, 2));
            var definition = ScriptableObject.CreateInstance<HitboxDefinition>();
            definition.ConfigureIdentity("PROJECTILE-HITBOX");
            definition.Damage = 2;
            var projectile = new ProjectileRuntime(new HitboxRuntime(combat, definition, "PLAYER"));

            Assert.That(projectile.TryImpact("ENEMY"), Is.True);
            Assert.That(projectile.IsActive, Is.False);
            Assert.That(projectile.TryImpact("ENEMY"), Is.False);

            events.Dispose();
            Object.DestroyImmediate(definition);
        }
    }
}
