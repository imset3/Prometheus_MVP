using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Presentation-only bridge for the ranged guard. Gameplay timing remains
    /// authoritative in TutorialRangedEnemyHost.
    /// </summary>
    public sealed class TutorialRangedEnemyAnimationBridge : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TutorialRangedEnemyHost rangedEnemy;
        [SerializeField] private CombatActorHost actor;
        [SerializeField] private Transform facingTarget;
        [SerializeField] private bool sourceFramesFaceRight = true;
        [SerializeField, Min(0f)] private float crossFadeSeconds = 0.035f;

        private string currentState = string.Empty;
        private bool deathPresented;

        public bool HasValidSetup => animator != null && spriteRenderer != null && rangedEnemy != null && actor != null;

        public void Configure(
            Animator configuredAnimator,
            SpriteRenderer configuredRenderer,
            TutorialRangedEnemyHost configuredRangedEnemy,
            CombatActorHost configuredActor,
            Transform configuredFacingTarget)
        {
            animator = configuredAnimator;
            spriteRenderer = configuredRenderer;
            rangedEnemy = configuredRangedEnemy;
            actor = configuredActor;
            facingTarget = configuredFacingTarget;
        }

        private void Awake()
        {
            if (HasValidSetup) return;
            Debug.LogError("TutorialRangedEnemyAnimationBridge requires animator, renderer, ranged host, and actor.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            if (rangedEnemy != null) rangedEnemy.PhaseChanged += HandlePhaseChanged;
            UpdateFacing();
            PlayState("Work", 0f);
        }

        private void OnDisable()
        {
            if (rangedEnemy != null) rangedEnemy.PhaseChanged -= HandlePhaseChanged;
        }

        private void Update()
        {
            UpdateFacing();
            if (actor == null || actor.Runtime == null) return;
            if (!actor.Runtime.IsAlive)
            {
                if (!deathPresented)
                {
                    deathPresented = true;
                    PlayState("Death", 0f, true);
                }
                return;
            }

            deathPresented = false;
        }

        private void HandlePhaseChanged(TutorialRangedEnemyPhase phase)
        {
            if (phase == TutorialRangedEnemyPhase.Telegraph)
                PlayState("Attack", 0f, true);
            else if (phase == TutorialRangedEnemyPhase.Ready || phase == TutorialRangedEnemyPhase.Recovery)
                PlayState("Work");
        }

        private void UpdateFacing()
        {
            if (spriteRenderer == null || facingTarget == null) return;
            var delta = facingTarget.position.x - transform.position.x;
            if (Mathf.Approximately(delta, 0f)) return;
            spriteRenderer.flipX = sourceFramesFaceRight ? delta < 0f : delta > 0f;
        }

        private void PlayState(string stateName, float? transition = null, bool restart = false)
        {
            if (animator == null || (!restart && currentState == stateName)) return;
            var hash = Animator.StringToHash("Base Layer." + stateName);
            if (!animator.HasState(0, hash)) return;
            animator.CrossFadeInFixedTime(hash, Mathf.Max(0f, transition ?? crossFadeSeconds), 0, 0f);
            currentState = stateName;
        }
    }
}
