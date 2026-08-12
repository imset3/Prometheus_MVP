using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>Frame-rate independent horizontal motor that keeps tutorial enemies on their authored platform.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class TutorialGroundedEnemyMotorHost : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private LayerMask groundLayers = -1;
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.24f;
        [SerializeField, Min(0.001f)] private float collisionSkin = 0.03f;

        private readonly RaycastHit2D[] hits = new RaycastHit2D[16];

        public Rigidbody2D Body => body;
        public Collider2D BodyCollider => bodyCollider;
        public bool HasValidSetup => body != null && bodyCollider != null && !bodyCollider.isTrigger &&
                                     body.bodyType == RigidbodyType2D.Dynamic && body.gravityScale > 0f;

        public void Configure(Rigidbody2D configuredBody, Collider2D configuredCollider)
        {
            body = configuredBody;
            bodyCollider = configuredCollider;
        }

        private void Awake()
        {
            body ??= GetComponent<Rigidbody2D>();
            bodyCollider ??= GetComponent<Collider2D>();
            if (HasValidSetup) return;
            Debug.LogError("TutorialGroundedEnemyMotorHost requires a dynamic Rigidbody2D and solid body Collider2D.", this);
            enabled = false;
        }

        public bool TrySetHorizontalSpeed(float requestedSpeed)
        {
            if (!HasValidSetup) return false;
            if (Mathf.Abs(requestedSpeed) <= 0.001f)
            {
                StopHorizontal();
                return false;
            }

            var direction = requestedSpeed > 0f ? Vector2.right : Vector2.left;
            if (!IsGrounded() || HasWall(direction, requestedSpeed) || !HasGroundAhead(direction))
            {
                StopHorizontal();
                return false;
            }

            var velocity = body.linearVelocity;
            velocity.x = requestedSpeed;
            body.linearVelocity = velocity;
            return true;
        }

        public void StopHorizontal()
        {
            if (body == null) return;
            var velocity = body.linearVelocity;
            velocity.x = 0f;
            body.linearVelocity = velocity;
        }

        public void ResetMotion()
        {
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private bool IsGrounded() => CastBody(Vector2.down, groundProbeDistance);

        private bool HasWall(Vector2 direction, float requestedSpeed) =>
            CastBody(direction, Mathf.Abs(requestedSpeed) * Time.fixedDeltaTime + collisionSkin);

        private bool HasGroundAhead(Vector2 direction)
        {
            var bounds = bodyCollider.bounds;
            var origin = new Vector2(
                bounds.center.x + direction.x * (bounds.extents.x + collisionSkin),
                bounds.min.y + groundProbeDistance * 0.5f);
            var filter = SolidFilter();
            var count = Physics2D.Raycast(origin, Vector2.down, filter, hits, groundProbeDistance * 1.5f);
            return HasExternalHit(count);
        }

        private bool CastBody(Vector2 direction, float distance)
        {
            var count = bodyCollider.Cast(direction, SolidFilter(), hits, Mathf.Max(distance, collisionSkin));
            return HasExternalHit(count);
        }

        private bool HasExternalHit(int count)
        {
            for (var index = 0; index < count; index++)
            {
                var candidate = hits[index].collider;
                if (candidate == null || candidate == bodyCollider || candidate.transform.IsChildOf(transform)) continue;
                return true;
            }
            return false;
        }

        private ContactFilter2D SolidFilter() => new()
        {
            useLayerMask = true,
            layerMask = groundLayers,
            useTriggers = false
        };

        private void OnDisable() => ResetMotion();
    }
}
