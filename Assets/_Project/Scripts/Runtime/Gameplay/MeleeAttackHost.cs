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
        [SerializeField, Min(0.05f)] private float comboWindowSeconds = 0.5f;

        private readonly Collider2D[] results = new Collider2D[8];
        private float cooldownEndsAt;
        private float deactivateAt;
        private float attackDirectionLockedUntil;
        private Vector3 attackAnchorLocalPosition;
        private Vector3 attackAnchorLocalScale;
        private AttackComboTracker comboTracker;

        public bool HasValidSetup => inputHost != null && sourceActor != null && attackHitbox != null && attackAnchor != null;
        public int CurrentComboStage => comboTracker != null ? comboTracker.CurrentStage : 0;
        public float ComboWindowSeconds => comboWindowSeconds;
        public bool IsAttackDirectionLocked => Time.time < attackDirectionLockedUntil;
        public event System.Action<int> ComboStageChanged;

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
            comboTracker = new AttackComboTracker(comboWindowSeconds);
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
            comboTracker?.Reset();
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
            if (Time.time < cooldownEndsAt || sourceActor.Runtime == null || sourceActor.CombatSystem == null) return;
            if (!sourceActor.Runtime.IsAlive || sourceActor.Runtime.State is CombatState.Hit or CombatState.Stun) return;

            cooldownEndsAt = Time.time + cooldownSeconds;
            deactivateAt = Time.time + activeSeconds;
            attackDirectionLockedUntil = deactivateAt;
            attackHitbox.enabled = true;
            var comboStage = comboTracker.RegisterAttack(Time.time);
            ComboStageChanged?.Invoke(comboStage);
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
                    new DamagePacket(sourceActor.ActorId, $"{attackId}-COMBO-{comboStage:00}", damage));
            }
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
