using Narthex.Content;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class MeleeAttackHost : MonoBehaviour
    {
        [SerializeField] private PlayerInputHost inputHost;
        [SerializeField] private CombatActorHost sourceActor;
        [SerializeField] private Collider2D attackHitbox;
        [SerializeField] private Transform attackAnchor;
        [SerializeField] private LayerMask targetLayers = -1;
        [SerializeField] private string attackId = "WPN-BAYONET-BASIC";
        [SerializeField] private int damage = 25;
        [SerializeField] private float cooldownSeconds = 0.25f;
        [SerializeField] private float activeSeconds = 0.08f;
        [SerializeField, Min(0.01f)] private float directionLockSeconds = 0.22f;

        private readonly Collider2D[] results = new Collider2D[8];
        private float cooldownEndsAt;
        private float deactivateAt;
        private float attackDirectionLockedUntil;
        private float presentationLockSeconds;
        private Vector3 attackAnchorLocalPosition;
        private Vector3 attackAnchorLocalScale;
        private uint attackSequence;
        private float externalAttackLockUntil;

        public bool HasValidSetup => inputHost != null && sourceActor != null && attackHitbox != null && attackAnchor != null;
        public bool UsesSingleHitAttacks => true;
        public float CooldownSeconds => cooldownSeconds;
        public float DirectionLockSeconds => directionLockSeconds;
        public float EffectiveCooldownSeconds => Mathf.Max(cooldownSeconds, presentationLockSeconds);
        public float EffectiveDirectionLockSeconds => Mathf.Max(directionLockSeconds, presentationLockSeconds);
        public bool IsAttackDirectionLocked => Time.time < attackDirectionLockedUntil;
        public event System.Action AttackStarted;

        public void LockExternalAttack(float durationSeconds)
        {
            externalAttackLockUntil = Mathf.Max(
                externalAttackLockUntil,
                Time.time + Mathf.Max(0f, durationSeconds));
        }

        public void SetPresentationLockDuration(float duration)
        {
            presentationLockSeconds = Mathf.Max(0f, duration);
        }

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("MeleeAttackHost requires pre-placed input, source actor, attack anchor, and attack hitbox references.", this);
                enabled = false;
                return;
            }

            attackHitbox.enabled = false;
            attackAnchorLocalPosition = attackAnchor.localPosition;
            attackAnchorLocalScale = attackAnchor.localScale;
            ApplyAimDirection(inputHost.AimDirectionX);
        }

        private void OnEnable()
        {
            if (inputHost != null) inputHost.AttackRequested += TryAttack;
            if (inputHost != null) inputHost.AimDirectionChanged += ApplyAimDirection;
        }

        private void OnDisable()
        {
            if (inputHost != null) inputHost.AttackRequested -= TryAttack;
            if (inputHost != null) inputHost.AimDirectionChanged -= ApplyAimDirection;
            if (attackHitbox != null) attackHitbox.enabled = false;
        }

        private void Update()
        {
            if (attackHitbox != null && attackHitbox.enabled && Time.time >= deactivateAt)
            {
                attackHitbox.enabled = false;
                ApplyAimDirection(inputHost.AimDirectionX);
            }
        }

        private void TryAttack()
        {
            if (Time.time < cooldownEndsAt || Time.time < externalAttackLockUntil ||
                sourceActor.Runtime == null || sourceActor.CombatSystem == null) return;
            if (!sourceActor.Runtime.IsAlive || sourceActor.Runtime.State is CombatState.Hit or CombatState.Stun) return;

            ApplyAimDirection(inputHost.AimDirectionX);
            cooldownEndsAt = Time.time + EffectiveCooldownSeconds;
            deactivateAt = Time.time + activeSeconds;
            attackDirectionLockedUntil = Time.time + Mathf.Max(activeSeconds, EffectiveDirectionLockSeconds);
            attackHitbox.enabled = true;
            attackSequence++;
            AttackStarted?.Invoke();
            Physics2D.SyncTransforms();

            var filter = ContactFilter2D.noFilter;
            filter.SetLayerMask(targetLayers);
            filter.useTriggers = true;
            var count = attackHitbox.Overlap(filter, results);
            var hitEnemy = false;
            for (var index = 0; index < count; index++)
            {
                var target = results[index].GetComponentInParent<CombatActorHost>();
                if (target == null || target.Kind == sourceActor.Kind) continue;

                hitEnemy |= sourceActor.CombatSystem.TryApplyDamage(
                    target.ActorId,
                    new DamagePacket(sourceActor.ActorId, $"{attackId}-{attackSequence:000000}", damage));
            }

            if (!hitEnemy) return;
            sourceActor.Events?.Publish(new GameplaySignal(QuestSignalType.AttackPerformed, sourceActor.ActorId));
        }

        private void ApplyAimDirection(float direction)
        {
            if (attackAnchor == null) return;
            if (Time.time < attackDirectionLockedUntil) return;
            var sign = direction < 0f ? -1f : 1f;
            attackAnchor.localPosition = new Vector3(
                Mathf.Abs(attackAnchorLocalPosition.x) * sign,
                attackAnchorLocalPosition.y,
                attackAnchorLocalPosition.z);
            attackAnchor.localScale = new Vector3(
                Mathf.Abs(attackAnchorLocalScale.x) * sign,
                attackAnchorLocalScale.y,
                attackAnchorLocalScale.z);
        }
    }
}
