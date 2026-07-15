using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class EnemyAttackHost : MonoBehaviour
    {
        [SerializeField] private CombatActorHost sourceActor;
        [SerializeField] private Collider2D attackHitbox;
        [SerializeField] private LayerMask targetLayers = -1;
        [SerializeField] private string attackId = "ENEMY-TUTO-BASIC";
        [SerializeField] private int damage = 15;
        [SerializeField] private float intervalSeconds = 0.8f;
        [SerializeField] private float activeSeconds = 0.1f;

        private readonly Collider2D[] results = new Collider2D[8];
        private float nextAttackAt;
        private float deactivateAt;

        private void Awake()
        {
            if (sourceActor == null || attackHitbox == null)
            {
                Debug.LogError("EnemyAttackHost requires pre-placed source actor and attack hitbox references.", this);
                enabled = false;
                return;
            }

            attackHitbox.enabled = false;
        }

        private void Update()
        {
            if (attackHitbox.enabled && Time.time >= deactivateAt) attackHitbox.enabled = false;
            if (Time.time < nextAttackAt || sourceActor.Runtime == null || sourceActor.CombatSystem == null) return;
            if (!sourceActor.Runtime.IsAlive) return;

            TryAttack();
        }

        private void TryAttack()
        {
            nextAttackAt = Time.time + intervalSeconds;
            attackHitbox.enabled = true;
            deactivateAt = Time.time + activeSeconds;

            var filter = ContactFilter2D.noFilter;
            filter.SetLayerMask(targetLayers);
            filter.useTriggers = true;
            var count = attackHitbox.Overlap(filter, results);
            for (var index = 0; index < count; index++)
            {
                var target = results[index].GetComponentInParent<CombatActorHost>();
                if (target == null || target.Kind != CombatActorKind.Player) continue;

                sourceActor.CombatSystem.TryApplyDamage(
                    target.ActorId,
                    new DamagePacket(sourceActor.ActorId, attackId, damage));
                break;
            }
        }
    }
}
