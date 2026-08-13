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
        [SerializeField] private bool useAnimationEventImpact;
        [SerializeField, Min(0.01f)] private float impactFallbackSeconds = 0.2f;

        private readonly Collider2D[] results = new Collider2D[8];
        private float cooldownEndsAt;
        private float deactivateAt;
        private float attackDirectionLockedUntil;
        private float presentationLockSeconds;
        private Vector3 attackAnchorLocalPosition;
        private Vector3 attackAnchorLocalScale;
        private uint attackSequence;
        private float externalAttackLockUntil;
        private bool impactQueued;
        private float impactFallbackAt;

        public bool HasValidSetup => inputHost != null && sourceActor != null && attackHitbox != null && attackAnchor != null;
        public bool UsesSingleHitAttacks => true;
        public float CooldownSeconds => cooldownSeconds;
        public float DirectionLockSeconds => directionLockSeconds;
        public float EffectiveCooldownSeconds => Mathf.Max(cooldownSeconds, presentationLockSeconds);
        public float EffectiveDirectionLockSeconds => Mathf.Max(directionLockSeconds, presentationLockSeconds);
        public bool IsAttackDirectionLocked => Time.time < attackDirectionLockedUntil;
        public event System.Action AttackStarted;
        public bool UsesAnimationEventImpact => useAnimationEventImpact;
        public bool HasQueuedImpact => impactQueued;

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
            impactQueued = false;
        }

        private void Update()
        {
            // The authored Animation Event is authoritative. This fallback prevents a broken
            // imported clip from permanently swallowing an accepted attack.
            if (impactQueued && Time.time >= impactFallbackAt)
                ResolveQueuedImpact();
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
            attackSequence++;
            AttackStarted?.Invoke();
            if (useAnimationEventImpact)
            {
                impactQueued = true;
                impactFallbackAt = Time.time + impactFallbackSeconds;
                return;
            }

            ResolveImpact();
        }

        public void ResolveQueuedImpact()
        {
            if (!impactQueued) return;
            impactQueued = false;
            ResolveImpact();
        }

        private void ResolveImpact()
        {
            if (attackHitbox == null || sourceActor == null || sourceActor.Runtime == null ||
                !sourceActor.Runtime.IsAlive)
                return;

            deactivateAt = Time.time + activeSeconds;
            attackHitbox.enabled = true;
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

#if UNITY_EDITOR
        public void ConfigureAnimationEventImpact(bool enabled, float fallbackSeconds)
        {
            useAnimationEventImpact = enabled;
            impactFallbackSeconds = Mathf.Max(0.01f, fallbackSeconds);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

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
