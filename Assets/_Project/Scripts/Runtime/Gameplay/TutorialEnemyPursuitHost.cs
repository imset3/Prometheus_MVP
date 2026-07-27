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
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.8f;
        [SerializeField, Min(0.1f)] private float stopDistance = 1.15f;

        public bool HasValidSetup => actor != null && target != null && moveSpeed > 0f && stopDistance > 0f;

        private void Awake()
        {
            if (HasValidSetup) return;
            Debug.LogError("TutorialEnemyPursuitHost requires actor, target, speed, and stop-distance references.", this);
            enabled = false;
        }

        private void Update()
        {
            if (actor.Runtime == null || !actor.Runtime.IsAlive || actor.Runtime.State == CombatState.Hit) return;
            var deltaX = target.position.x - transform.position.x;
            if (Mathf.Abs(deltaX) <= stopDistance) return;
            var position = transform.position;
            position.x = Mathf.MoveTowards(position.x, target.position.x, moveSpeed * Time.deltaTime);
            transform.position = position;
        }
    }
}
