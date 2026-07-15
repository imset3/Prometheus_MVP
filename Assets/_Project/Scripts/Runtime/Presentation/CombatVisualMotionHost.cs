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
            if (playerInput != null) playerInput.AttackRequested += StartAttackMotion;
            if (actor != null && actor.Events != null) actor.Events.Subscribe<HitConfirmed>(HandleHitConfirmed);
            if (bossPatternHost != null) bossPatternHost.PatternStarted += HandleBossPatternStarted;
        }

        private void OnDisable()
        {
            if (playerInput != null) playerInput.AttackRequested -= StartAttackMotion;
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
            var scale = baseLocalScale;

            if (movementBody != null && Mathf.Abs(movementBody.linearVelocity.x) > 0.05f)
            {
                rotation = Quaternion.Euler(0f, 0f, -4f * Mathf.Sign(movementBody.linearVelocity.x));
            }

            if (Time.time < attackEndsAt)
            {
                var progress = 1f - ((attackEndsAt - Time.time) / attackDuration);
                var arc = Mathf.Sin(progress * Mathf.PI);
                position += Vector3.right * (0.18f * arc);
                rotation = Quaternion.Euler(0f, 0f, -48f * arc);
                scale = Vector3.Scale(baseLocalScale, new Vector3(1f + (0.16f * arc), 1f - (0.1f * arc), 1f));
            }

            if (Time.time < hitEndsAt)
            {
                var progress = 1f - ((hitEndsAt - Time.time) / hitDuration);
                var shake = Mathf.Sin(progress * Mathf.PI * 4f) * 0.06f;
                position += Vector3.right * shake;
                scale = Vector3.Scale(baseLocalScale, new Vector3(1.12f, 0.88f, 1f));
            }

            visualRoot.localPosition = position;
            visualRoot.localRotation = rotation;
            visualRoot.localScale = scale;
            if (attackEffect != null) attackEffect.SetActive(Time.time < attackEndsAt);
        }

        private void HandleHitConfirmed(HitConfirmed message)
        {
            if (actor == null) return;
            if (message.AttackerId == actor.ActorId) StartAttackMotion();
            if (message.TargetId == actor.ActorId) hitEndsAt = Time.time + hitDuration;
        }

        private void HandleBossPatternStarted(HeltePattern pattern)
        {
            if (pattern != HeltePattern.None) StartAttackMotion();
        }

        private void StartAttackMotion()
        {
            if (actor == null || actor.Runtime == null || !actor.Runtime.IsAlive) return;
            attackEndsAt = Time.time + attackDuration;
        }
    }
}
