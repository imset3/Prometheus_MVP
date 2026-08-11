using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Pooled presentation-friendly projectile used by the tutorial ranged guard.
    /// Damage still flows through the shared CombatSystem.
    /// </summary>
    public sealed class TutorialEnemyProjectileHost : MonoBehaviour
    {
        [SerializeField] private Collider2D projectileCollider;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private CombatActorHost sourceActor;
        private Vector2 direction;
        private float speed;
        private float expiresAt;
        private int damage;
        private string attackId;

        public bool HasValidSetup => projectileCollider != null && body != null && spriteRenderer != null;
        public bool HasVisibleSetup => HasValidSetup && spriteRenderer.sprite != null && spriteRenderer.color.a > 0.01f;

        public void Configure(Collider2D configuredCollider, Rigidbody2D configuredBody, SpriteRenderer configuredRenderer)
        {
            projectileCollider = configuredCollider;
            body = configuredBody;
            spriteRenderer = configuredRenderer;
        }

        public void Launch(
            CombatActorHost source,
            Vector2 launchPosition,
            Vector2 launchDirection,
            float launchSpeed,
            float lifetime,
            int configuredDamage,
            string configuredAttackId)
        {
            if (!HasValidSetup || source == null || source.CombatSystem == null) return;

            sourceActor = source;
            direction = launchDirection.sqrMagnitude > 0.001f ? launchDirection.normalized : Vector2.right;
            speed = Mathf.Max(0.1f, launchSpeed);
            damage = Mathf.Max(1, configuredDamage);
            attackId = string.IsNullOrWhiteSpace(configuredAttackId)
                ? "ENEMY-TUTO-RANGED"
                : configuredAttackId;
            expiresAt = Time.time + Mathf.Max(0.1f, lifetime);
            transform.position = launchPosition;
            spriteRenderer.enabled = true;
            var visibleColor = spriteRenderer.color;
            visibleColor.a = 1f;
            spriteRenderer.color = visibleColor;
            spriteRenderer.flipX = direction.x < 0f;
            gameObject.SetActive(true);
            Physics2D.SyncTransforms();
        }

        private void Awake()
        {
            if (projectileCollider == null) projectileCollider = GetComponent<Collider2D>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (HasValidSetup) return;

            Debug.LogError("TutorialEnemyProjectileHost requires collider, rigidbody, and sprite renderer references.", this);
            enabled = false;
        }

        private void FixedUpdate()
        {
            if (Time.time >= expiresAt)
            {
                Deactivate();
                return;
            }

            body.MovePosition(body.position + direction * (speed * Time.fixedDeltaTime));
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!gameObject.activeSelf || other == null || sourceActor == null) return;
            if (other.transform.IsChildOf(sourceActor.transform)) return;

            var target = other.GetComponentInParent<CombatActorHost>();
            if (target != null && target.Kind == CombatActorKind.Player)
            {
                sourceActor.CombatSystem?.TryApplyDamage(
                    target.ActorId,
                    new DamagePacket(sourceActor.ActorId, attackId, damage));
                Deactivate();
                return;
            }

            if (!other.isTrigger) Deactivate();
        }

        private void OnDisable()
        {
            sourceActor = null;
            direction = Vector2.zero;
            expiresAt = 0f;
        }

        private void Deactivate()
        {
            gameObject.SetActive(false);
        }
    }
}
