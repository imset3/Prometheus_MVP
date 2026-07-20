using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    public sealed class CombatVisualMotionHost : MonoBehaviour
    {
        [SerializeField] private CombatActorHost actor;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private GameObject attackEffect;
        [SerializeField] private PlayerInputHost playerInput;
        [SerializeField] private MeleeAttackHost meleeAttackHost;
        [SerializeField] private HelteBossPatternHost bossPatternHost;
        [SerializeField] private Rigidbody2D movementBody;
        [SerializeField, Min(0f)] private float idleBobDistance = 0.035f;
        [SerializeField, Min(0.01f)] private float idleBobFrequency = 3.5f;
        [SerializeField, Min(0.01f)] private float attackDuration = 0.22f;
        [SerializeField, Min(0.01f)] private float hitDuration = 0.16f;

        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private float attackEndsAt;
        private float hitEndsAt;
        private float facingDirection = 1f;
        private int activeComboStage = 1;

        private void Awake()
        {
            if (actor == null || visualRoot == null)
            {
                Debug.LogError("CombatVisualMotionHost requires pre-placed actor and visual references.", this);
                enabled = false;
                return;
            }

            baseLocalPosition = visualRoot.localPosition;
            baseLocalScale = visualRoot.localScale;
            if (attackEffect != null) attackEffect.SetActive(false);
        }

        private void OnEnable()
        {
            if (meleeAttackHost != null) meleeAttackHost.ComboStageChanged += StartComboAttackMotion;
            else if (playerInput != null) playerInput.AttackRequested += StartAttackMotion;
            if (playerInput != null) playerInput.AimDirectionChanged += HandleAimDirectionChanged;
            if (actor != null && actor.Events != null) actor.Events.Subscribe<HitConfirmed>(HandleHitConfirmed);
            if (bossPatternHost != null) bossPatternHost.PatternStarted += HandleBossPatternStarted;
        }

        private void OnDisable()
        {
            if (meleeAttackHost != null) meleeAttackHost.ComboStageChanged -= StartComboAttackMotion;
            else if (playerInput != null) playerInput.AttackRequested -= StartAttackMotion;
            if (playerInput != null) playerInput.AimDirectionChanged -= HandleAimDirectionChanged;
            if (actor != null && actor.Events != null) actor.Events.Unsubscribe<HitConfirmed>(HandleHitConfirmed);
            if (bossPatternHost != null) bossPatternHost.PatternStarted -= HandleBossPatternStarted;
            if (attackEffect != null) attackEffect.SetActive(false);
        }

        private void Update()
        {
            if (actor == null || actor.Runtime == null || visualRoot == null) return;

            if (!actor.Runtime.IsAlive)
            {
                visualRoot.localPosition = baseLocalPosition + Vector3.down * 0.18f;
                visualRoot.localRotation = Quaternion.Euler(0f, 0f, 78f);
                visualRoot.localScale = baseLocalScale;
                if (attackEffect != null) attackEffect.SetActive(false);
                return;
            }

            var position = baseLocalPosition + Vector3.up * (Mathf.Sin(Time.time * idleBobFrequency) * idleBobDistance);
            var rotation = Quaternion.identity;
            var scale = new Vector3(Mathf.Abs(baseLocalScale.x) * facingDirection, baseLocalScale.y, baseLocalScale.z);

            if (movementBody != null && Mathf.Abs(movementBody.linearVelocity.x) > 0.05f)
            {
                rotation = Quaternion.Euler(0f, 0f, -4f * Mathf.Sign(movementBody.linearVelocity.x));
            }

            if (Time.time < attackEndsAt)
            {
                var progress = 1f - ((attackEndsAt - Time.time) / attackDuration);
                var arc = Mathf.Sin(progress * Mathf.PI);
                switch (activeComboStage)
                {
                    case 2:
                        position += Vector3.right * (0.2f * arc * facingDirection) + Vector3.up * (0.08f * arc);
                        rotation = Quaternion.Euler(0f, 0f, 58f * arc * facingDirection);
                        scale = Vector3.Scale(scale, new Vector3(1f + (0.12f * arc), 1f - (0.08f * arc), 1f));
                        break;
                    case 3:
                        position += Vector3.right * (0.36f * arc * facingDirection);
                        rotation = Quaternion.Euler(0f, 0f, -18f * arc * facingDirection);
                        scale = Vector3.Scale(scale, new Vector3(1f + (0.3f * arc), 1f - (0.16f * arc), 1f));
                        break;
                    default:
                        position += Vector3.right * (0.18f * arc * facingDirection);
                        rotation = Quaternion.Euler(0f, 0f, -48f * arc * facingDirection);
                        scale = Vector3.Scale(scale, new Vector3(1f + (0.16f * arc), 1f - (0.1f * arc), 1f));
                        break;
                }
            }

            if (Time.time < hitEndsAt)
            {
                var progress = 1f - ((hitEndsAt - Time.time) / hitDuration);
                var shake = Mathf.Sin(progress * Mathf.PI * 4f) * 0.06f;
                position += Vector3.right * shake;
                scale = Vector3.Scale(scale, new Vector3(1.12f, 0.88f, 1f));
            }

            visualRoot.localPosition = position;
            visualRoot.localRotation = rotation;
            visualRoot.localScale = scale;
            if (attackEffect != null) attackEffect.SetActive(Time.time < attackEndsAt);
        }

        private void HandleHitConfirmed(HitConfirmed message)
        {
            if (actor == null) return;
            if (message.AttackerId == actor.ActorId && meleeAttackHost == null) StartAttackMotion();
            if (message.TargetId == actor.ActorId) hitEndsAt = Time.time + hitDuration;
        }

        private void HandleBossPatternStarted(HeltePattern pattern)
        {
            if (pattern != HeltePattern.None) StartAttackMotion();
        }

        private void StartAttackMotion()
        {
            if (actor == null || actor.Runtime == null || !actor.Runtime.IsAlive) return;
            activeComboStage = 1;
            attackEndsAt = Time.time + attackDuration;
        }

        private void StartComboAttackMotion(int comboStage)
        {
            if (actor == null || actor.Runtime == null || !actor.Runtime.IsAlive) return;
            activeComboStage = Mathf.Clamp(comboStage, 1, 3);
            attackEndsAt = Time.time + attackDuration;
        }

        private void HandleAimDirectionChanged(float direction)
        {
            facingDirection = direction < 0f ? -1f : 1f;
        }
    }
}
