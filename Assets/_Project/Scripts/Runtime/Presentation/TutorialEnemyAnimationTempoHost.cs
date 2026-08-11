using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Plays the authored Work cycle only while the enemy actually moves.
    /// This prevents large walk poses from reading as restless idle motion.
    /// </summary>
    public sealed class TutorialEnemyAnimationTempoHost : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform actorRoot;
        [SerializeField, Range(0.1f, 1f)] private float walkPlaybackSpeed = 0.62f;
        [SerializeField, Min(0.00001f)] private float movementThreshold = 0.00015f;

        private Vector3 previousPosition;
        private bool wasInWorkState;

        public bool HasValidSetup => animator != null && actorRoot != null;

        public void Configure(Animator configuredAnimator, Transform configuredActorRoot)
        {
            animator = configuredAnimator;
            actorRoot = configuredActorRoot;
        }

        private void OnEnable()
        {
            previousPosition = actorRoot != null ? actorRoot.position : transform.position;
            wasInWorkState = false;
        }

        private void OnDisable()
        {
            if (animator != null) animator.speed = 1f;
        }

        private void LateUpdate()
        {
            if (!HasValidSetup) return;

            var moved = Mathf.Abs(actorRoot.position.x - previousPosition.x) > movementThreshold;
            previousPosition = actorRoot.position;
            var inWork = IsOnlyWorkStatePlaying();
            if (!inWork)
            {
                animator.speed = 1f;
                wasInWorkState = false;
                return;
            }

            if (moved)
            {
                animator.speed = walkPlaybackSpeed;
            }
            else
            {
                if (!wasInWorkState || animator.speed > 0f)
                {
                    animator.Play("Work", 0, 0f);
                    animator.Update(0f);
                }
                animator.speed = 0f;
            }
            wasInWorkState = true;
        }

        private bool IsOnlyWorkStatePlaying()
        {
            var current = animator.GetCurrentAnimatorStateInfo(0);
            if (!current.IsName("Work") && !current.IsName("Base Layer.Work")) return false;
            if (!animator.IsInTransition(0)) return true;
            var next = animator.GetNextAnimatorStateInfo(0);
            return next.IsName("Work") || next.IsName("Base Layer.Work");
        }
    }
}
