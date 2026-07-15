using Narthex.Content;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class MeleeAttackHost : MonoBehaviour
    {
        [SerializeField] private PlayerInputHost inputHost;
        [SerializeField] private CombatActorHost sourceActor;
        [SerializeField] private Collider2D attackHitbox;
        [SerializeField] private LayerMask targetLayers = -1;
        [SerializeField] private string attackId = "WPN-BAYONET-BASIC";
        [SerializeField] private int damage = 25;
        [SerializeField] private float cooldownSeconds = 0.25f;
        [SerializeField] private float activeSeconds = 0.08f;

        private readonly Collider2D[] results = new Collider2D[8];
        private float cooldownEndsAt;
        private float deactivateAt;

        private void Awake()
        {
            if (inputHost == null || sourceActor == null || attackHitbox == null)
            {
                Debug.LogError("MeleeAttackHost requires pre-placed input, source actor, and attack hitbox references.", this);
                enabled = false;
                return;
            }

            attackHitbox.enabled = false;
        }

        private void OnEnable()
        {
            if (inputHost != null) inputHost.AttackRequested += TryAttack;
        }

        private void OnDisable()
        {
            if (inputHost != null) inputHost.AttackRequested -= TryAttack;
            if (attackHitbox != null) attackHitbox.enabled = false;
        }

        private void Update()
        {
            if (attackHitbox != null && attackHitbox.enabled && Time.time >= deactivateAt)
                attackHitbox.enabled = false;
        }

        private void TryAttack()
        {
            if (Time.time < cooldownEndsAt || sourceActor.Runtime == null || sourceActor.CombatSystem == null) return;
            if (!sourceActor.Runtime.IsAlive || sourceActor.Runtime.State is CombatState.Hit or CombatState.Stun) return;

            cooldownEndsAt = Time.time + cooldownSeconds;
            deactivateAt = Time.time + activeSeconds;
            attackHitbox.enabled = true;
            sourceActor.Events?.Publish(new GameplaySignal(QuestSignalType.AttackPerformed, sourceActor.ActorId));

            var filter = ContactFilter2D.noFilter;
            filter.SetLayerMask(targetLayers);
            filter.useTriggers = true;
            var count = attackHitbox.Overlap(filter, results);
            for (var index = 0; index < count; index++)
            {
                var target = results[index].GetComponentInParent<CombatActorHost>();
                if (target == null || target.Kind == sourceActor.Kind) continue;

                sourceActor.CombatSystem.TryApplyDamage(
                    target.ActorId,
                    new DamagePacket(sourceActor.ActorId, attackId, damage));
            }
        }
    }
}
