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
        [SerializeField] private TutorialGroundedEnemyMotorHost groundMotor;
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.8f;
        [SerializeField, Min(0.1f)] private float stopDistance = 1.15f;
        private readonly RaycastHit2D[] sightHits = new RaycastHit2D[24];

        public bool HasValidSetup => actor != null && target != null && groundMotor != null &&
                                     groundMotor.HasValidSetup && moveSpeed > 0f && stopDistance > 0f;

        private void Awake()
        {
            groundMotor ??= GetComponent<TutorialGroundedEnemyMotorHost>();
            if (HasValidSetup) return;
            Debug.LogError(
                "TutorialEnemyPursuitHost requires actor, target, and a grounded enemy motor.",
                this);
            enabled = false;
        }

        private void FixedUpdate()
        {
            if (GetComponent<TutorialRangedEnemyHost>() is { enabled: true }) return;
            if (actor.Runtime == null || !actor.Runtime.IsAlive || actor.Runtime.State == CombatState.Hit)
            {
                groundMotor.StopHorizontal();
                return;
            }
            var deltaX = target.position.x - transform.position.x;
            if (Mathf.Abs(deltaX) <= stopDistance || !HasClearLineOfSight())
            {
                groundMotor.StopHorizontal();
                return;
            }

            var speed = Mathf.Min(moveSpeed, (Mathf.Abs(deltaX) - stopDistance) / Time.fixedDeltaTime);
            groundMotor.TrySetHorizontalSpeed(Mathf.Sign(deltaX) * speed);
        }

        private bool HasClearLineOfSight()
        {
            var bodyCollider = groundMotor.BodyCollider;
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
