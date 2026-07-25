using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    public enum CharacterPngAnimationPreset
    {
        Generic,
        Prome,
        Helte
    }

    /// <summary>
    /// Runtime adapter installed by the PNG Sequence Setup editor tool.
    /// It only drives presentation. Physics, hitboxes, and gameplay remain on the actor root.
    /// </summary>
    public sealed class CharacterPngAnimationBridge : MonoBehaviour
    {
        [SerializeField] private CharacterPngAnimationPreset preset;
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Rigidbody2D movementBody;
        [SerializeField] private PlayerMotorHost playerMotor;
        [SerializeField] private PlayerInputHost playerInput;
        [SerializeField] private MeleeAttackHost meleeAttack;
        [SerializeField] private CombatActorHost actor;
        [SerializeField] private HelteBossPatternHost heltePattern;
        [SerializeField] private Transform facingTarget;
        [SerializeField, Min(0f)] private float crossFadeSeconds = 0.04f;
        [SerializeField, Min(0f)] private float airborneVelocityThreshold = 0.15f;
        [SerializeField, Min(0f)] private float runVelocityThreshold = 0.05f;
        [SerializeField, Min(0f)] private float dashVelocityThreshold = 8f;
        [SerializeField, Min(0.01f)] private float attackOneDuration = 0.22f;
        [SerializeField, Min(0.01f)] private float attackTwoDuration = 0.22f;
        [SerializeField, Min(0.01f)] private float attackThreeDuration = 0.25f;
        [SerializeField, Min(0.01f)] private float hitDuration = 0.16f;

        private string currentState = string.Empty;
        private float actionLockedUntil;
        private bool deathPresented;
        private bool subscribedToCombatEvents;

        public CharacterPngAnimationPreset Preset => preset;
        public bool HasValidSetup => animator != null && spriteRenderer != null;

        public void Configure(
            CharacterPngAnimationPreset configuredPreset,
            Animator configuredAnimator,
            SpriteRenderer configuredRenderer,
            Rigidbody2D configuredBody,
            PlayerMotorHost configuredMotor,
            PlayerInputHost configuredInput,
            MeleeAttackHost configuredMelee,
            CombatActorHost configuredActor,
            HelteBossPatternHost configuredHeltePattern,
            Transform configuredFacingTarget,
            float configuredAttackOneDuration,
            float configuredAttackTwoDuration,
            float configuredAttackThreeDuration)
        {
            preset = configuredPreset;
            animator = configuredAnimator;
            spriteRenderer = configuredRenderer;
            movementBody = configuredBody;
            playerMotor = configuredMotor;
            playerInput = configuredInput;
            meleeAttack = configuredMelee;
            actor = configuredActor;
            heltePattern = configuredHeltePattern;
            facingTarget = configuredFacingTarget;
            attackOneDuration = Mathf.Max(0.01f, configuredAttackOneDuration);
            attackTwoDuration = Mathf.Max(0.01f, configuredAttackTwoDuration);
            attackThreeDuration = Mathf.Max(0.01f, configuredAttackThreeDuration);
        }

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("CharacterPngAnimationBridge requires an Animator and SpriteRenderer.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (playerInput != null) playerInput.AimDirectionChanged += HandleAimDirectionChanged;
            if (meleeAttack != null) meleeAttack.ComboStageChanged += HandleComboStageChanged;
            if (heltePattern != null) heltePattern.StateChanged += HandleHelteStateChanged;
            TrySubscribeCombatEvents();
            ApplyInitialFacing();
        }

        private void OnDisable()
        {
            if (playerInput != null) playerInput.AimDirectionChanged -= HandleAimDirectionChanged;
            if (meleeAttack != null) meleeAttack.ComboStageChanged -= HandleComboStageChanged;
            if (heltePattern != null) heltePattern.StateChanged -= HandleHelteStateChanged;
            if (subscribedToCombatEvents && actor != null && actor.Events != null)
                actor.Events.Unsubscribe<HitConfirmed>(HandleHitConfirmed);
            subscribedToCombatEvents = false;
        }

        private void Update()
        {
            if (!HasValidSetup) return;
            TrySubscribeCombatEvents();
            UpdateFacing();

            if (actor != null && actor.Runtime != null && !actor.Runtime.IsAlive)
            {
                if (!deathPresented)
                {
                    deathPresented = true;
                    PlayState("Death", 0f);
                }
                return;
            }

            deathPresented = false;
            if (preset == CharacterPngAnimationPreset.Prome)
                UpdatePromeLocomotion();
        }

        private void UpdatePromeLocomotion()
        {
            if (Time.time < actionLockedUntil || movementBody == null) return;

            var velocity = movementBody.linearVelocity;
            if (playerMotor != null && playerMotor.IsGliding)
                PlayState("Glide");
            else if (Mathf.Abs(velocity.x) >= dashVelocityThreshold)
                PlayState("Dash");
            else if (velocity.y > airborneVelocityThreshold)
                PlayState("Jump");
            else if (velocity.y < -airborneVelocityThreshold)
                PlayState("Fall");
            else if (Mathf.Abs(velocity.x) > runVelocityThreshold)
                PlayState("Run");
            else
                PlayState("Idle");
        }

        private void HandleComboStageChanged(int comboStage)
        {
            var resolvedStage = Mathf.Clamp(comboStage, 1, 3);
            var stateName = $"Attack{resolvedStage:00}";
            var duration = resolvedStage switch
            {
                2 => attackTwoDuration,
                3 => attackThreeDuration,
                _ => attackOneDuration
            };
            PlayLockedState(stateName, duration);
        }

        private void HandleHelteStateChanged(HelteCombatState state)
        {
            var stateName = state switch
            {
                HelteCombatState.Disabled => "Idle",
                HelteCombatState.Waiting => "Idle",
                HelteCombatState.PhaseTransition => "PhaseTransition",
                HelteCombatState.BasicWindup => "BasicWindup",
                HelteCombatState.BasicLeftSlash => "BasicLeftSlash",
                HelteCombatState.BasicAdvance => "DashApproach",
                HelteCombatState.BasicRightSlash => "BasicRightSlash",
                HelteCombatState.BlinkVanish => "BlinkVanish",
                HelteCombatState.BlinkReappear => "BlinkReappear",
                HelteCombatState.DashApproach => "DashApproach",
                HelteCombatState.CrossSlash => "CrossSlash",
                HelteCombatState.SwordFocus => "SwordFocus",
                HelteCombatState.SwordVolley => "SwordVolley",
                HelteCombatState.Recover => "Recover",
                _ => "Idle"
            };
            PlayState(stateName);
        }

        private void HandleHitConfirmed(HitConfirmed message)
        {
            if (actor == null || message.TargetId != actor.ActorId) return;
            PlayLockedState("Hit", hitDuration);
        }

        private void HandleAimDirectionChanged(float direction)
        {
            if (spriteRenderer != null && !Mathf.Approximately(direction, 0f))
                spriteRenderer.flipX = direction < 0f;
        }

        private void UpdateFacing()
        {
            if (preset != CharacterPngAnimationPreset.Helte || spriteRenderer == null || facingTarget == null) return;
            var delta = facingTarget.position.x - transform.position.x;
            if (!Mathf.Approximately(delta, 0f)) spriteRenderer.flipX = delta < 0f;
        }

        private void ApplyInitialFacing()
        {
            if (preset == CharacterPngAnimationPreset.Prome && playerInput != null)
                HandleAimDirectionChanged(playerInput.AimDirectionX);
            else
                UpdateFacing();
        }

        private void TrySubscribeCombatEvents()
        {
            if (subscribedToCombatEvents || actor == null || actor.Events == null) return;
            actor.Events.Subscribe<HitConfirmed>(HandleHitConfirmed);
            subscribedToCombatEvents = true;
        }

        private void PlayLockedState(string stateName, float duration)
        {
            actionLockedUntil = Time.time + Mathf.Max(0.01f, duration);
            PlayState(stateName, 0f, true);
        }

        private void PlayState(string stateName, float? transitionSeconds = null, bool restart = false)
        {
            if (animator == null || string.IsNullOrWhiteSpace(stateName)) return;
            if (!restart && currentState == stateName) return;

            var stateHash = Animator.StringToHash($"Base Layer.{stateName}");
            if (!animator.HasState(0, stateHash)) return;
            animator.CrossFadeInFixedTime(
                stateHash,
                Mathf.Max(0f, transitionSeconds ?? crossFadeSeconds),
                0,
                0f);
            currentState = stateName;
        }
    }
}
