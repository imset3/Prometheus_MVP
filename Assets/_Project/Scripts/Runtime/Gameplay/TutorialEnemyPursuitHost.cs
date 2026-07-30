using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Lightweight placeholder pursuit used until the final enemy FSM is supplied.
    /// </summary>
    public sealed class TutorialEnemyPursuitHost : MonoBehaviour
    {
        [SerializeField] private CombatActorHost actor;
        [SerializeField] private Transform target;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.8f;
        [SerializeField, Min(0.1f)] private float stopDistance = 1.15f;
        [SerializeField, Min(0.001f)] private float collisionSkin = 0.03f;

        private readonly RaycastHit2D[] castHits = new RaycastHit2D[12];
        private readonly RaycastHit2D[] sightHits = new RaycastHit2D[24];
        private Rigidbody2D body;

        private Collider2D ResolvedBodyCollider =>
            bodyCollider != null ? bodyCollider : GetComponent<Collider2D>();

        public bool HasValidSetup => actor != null && target != null && ResolvedBodyCollider != null &&
                                     moveSpeed > 0f && stopDistance > 0f && collisionSkin > 0f;

        private void Awake()
        {
            bodyCollider = ResolvedBodyCollider;
            body = GetComponent<Rigidbody2D>();
            if (HasValidSetup) return;
            Debug.LogError(
                "TutorialEnemyPursuitHost requires actor, target, body-collider, speed, stop-distance, and collision-skin references.",
                this);
            enabled = false;
        }

        private void FixedUpdate()
        {
            if (actor.Runtime == null || !actor.Runtime.IsAlive || actor.Runtime.State == CombatState.Hit) return;
            var deltaX = target.position.x - transform.position.x;
            if (Mathf.Abs(deltaX) <= stopDistance) return;
            if (!HasClearLineOfSight()) return;

            var direction = deltaX > 0f ? Vector2.right : Vector2.left;
            var requestedDistance = Mathf.Min(
                moveSpeed * Time.fixedDeltaTime,
                Mathf.Abs(deltaX) - stopDistance);
            var allowedDistance = FindAllowedDistance(direction, requestedDistance);
            if (allowedDistance <= 0f) return;

            var nextPosition = (Vector2)transform.position + direction * allowedDistance;
            if (body != null && body.bodyType != RigidbodyType2D.Static)
                body.MovePosition(nextPosition);
            else
                transform.position = nextPosition;
            Physics2D.SyncTransforms();
        }

        private float FindAllowedDistance(Vector2 direction, float requestedDistance)
        {
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = Physics2D.AllLayers,
                useTriggers = false
            };
            var hitCount = bodyCollider.Cast(
                direction,
                filter,
                castHits,
                requestedDistance + collisionSkin);
            var allowedDistance = requestedDistance;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = castHits[index];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
                if (Mathf.Abs(hit.normal.x) < 0.5f) continue;
                allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - collisionSkin));
            }

            return allowedDistance;
        }

        private bool HasClearLineOfSight()
        {
            var origin = (Vector2)bodyCollider.bounds.center;
            var targetCollider = target.GetComponentInChildren<Collider2D>();
            var destination = targetCollider != null
                ? (Vector2)targetCollider.bounds.center
                : (Vector2)target.position;
            var delta = destination - origin;
            var distance = delta.magnitude;
            if (distance <= 0.01f) return true;

            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = Physics2D.AllLayers,
                useTriggers = false
            };
            var hitCount = Physics2D.Raycast(
                origin,
                delta / distance,
                filter,
                sightHits,
                distance);
            for (var index = 0; index < hitCount; index++)
            {
                var hit = sightHits[index];
                if (hit.collider == null || hit.collider == bodyCollider ||
                    hit.collider.transform.IsChildOf(transform) ||
                    hit.collider.transform == target ||
                    hit.collider.transform.IsChildOf(target))
                    continue;
                return false;
            }

            return true;
        }
    }
}
