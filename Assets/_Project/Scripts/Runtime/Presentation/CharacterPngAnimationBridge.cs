using System.Linq;
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
        [SerializeField] private EnemyAttackHost enemyAttack;
        [SerializeField] private CombatActorHost actor;
        [SerializeField] private HelteBossPatternHost heltePattern;
        [SerializeField] private PromeBossSkillHost bossSkill;
        [SerializeField] private CombatVisualMotionHost proceduralVisualMotion;
        [SerializeField] private bool sourceFramesFaceRight;
        [SerializeField] private Transform facingTarget;
        [SerializeField, Min(0f)] private float crossFadeSeconds = 0.04f;
        [SerializeField, Min(0f)] private float airborneVelocityThreshold = 0.15f;
        [SerializeField, Min(0f)] private float runVelocityThreshold = 0.05f;
        [SerializeField, Min(0f)] private float dashVelocityThreshold = 8f;
        [SerializeField, Min(0.01f)] private float attackOneDuration = 0.22f;
        [SerializeField, Min(0.01f)] private float attackTwoDuration = 0.22f;
        [SerializeField, Min(0.01f)] private float attackThreeDuration = 0.25f;
        [SerializeField, Min(0.01f)] private float hitDuration = 0.16f;
        [SerializeField] private int attackSortingOrder = 1000;
        [SerializeField, HideInInspector] private bool setupBackupCaptured;
        [SerializeField, HideInInspector] private Renderer[] originalRenderers;
        [SerializeField, HideInInspector] private bool[] originalRendererEnabledStates;
        [SerializeField, HideInInspector] private CombatVisualMotionHost originalVisualMotion;
        [SerializeField, HideInInspector] private bool originalVisualMotionEnabled;
        [SerializeField, HideInInspector] private Collider2D originalBodyCollider;
        [SerializeField, HideInInspector] private Vector2 originalColliderSize;
        [SerializeField, HideInInspector] private Vector2 originalColliderOffset;
        [SerializeField, HideInInspector] private Transform originalContractVisualRoot;
        [SerializeField, HideInInspector] private Renderer[] originalContractRenderers;

        private string currentState = string.Empty;
        private float actionLockedUntil;
        private float proceduralAttackStartedAt;
        private float proceduralAttackEndsAt;
        private float proceduralAttackDirection = 1f;
        private Vector3 proceduralAttackBasePosition;
        private Quaternion proceduralAttackBaseRotation;
        private Vector3 proceduralAttackBaseScale;
        private bool proceduralAttackBaseCaptured;
        private int baseSortingOrder;
        private bool sortingOrderCaptured;
        private bool attackSortingPriorityActive;
        private bool deathPresented;
        private bool subscribedToCombatEvents;
        private float externalPlaybackResetAt;

        public CharacterPngAnimationPreset Preset => preset;
        public bool HasValidSetup => animator != null && spriteRenderer != null;
        public bool HasAttack01Clip => HasAnimatorState("Attack01");
        public bool HasDashClip => HasAnimatorState("Dash");
        public bool HasJumpClip => HasAnimatorState("Jump");
        public bool IsSingleAttackMotionPlaying => Time.time < actionLockedUntil &&
                                                   (currentState == "Attack01" ||
                                                    Time.time < proceduralAttackEndsAt);
        public bool IsUsingProceduralAttackFallback => Time.time < proceduralAttackEndsAt;
        public bool IsAttackSortingPriorityActive => attackSortingPriorityActive;
        public int BaseSortingOrder => baseSortingOrder;
        public float FacingDirection => ResolveVisualFacingDirection();
        public int PresentedAttackCount { get; private set; }
        public bool HasSetupBackup => setupBackupCaptured;
        public Transform OriginalContractVisualRoot => originalContractVisualRoot;
        public Renderer[] OriginalContractRenderers => originalContractRenderers;

        public void CaptureSetupBackup(
            Renderer[] renderers,
            bool[] rendererEnabledStates,
            CombatVisualMotionHost visualMotion,
            bool visualMotionEnabled,
            Collider2D bodyCollider,
            Transform contractVisualRoot,
            Renderer[] contractRenderers)
        {
            if (setupBackupCaptured) return;

            originalRenderers = renderers ?? new Renderer[0];
            originalRendererEnabledStates = rendererEnabledStates ?? new bool[0];
            originalVisualMotion = visualMotion;
            originalVisualMotionEnabled = visualMotionEnabled;
            originalBodyCollider = bodyCollider;
            originalContractVisualRoot = contractVisualRoot;
            originalContractRenderers = contractRenderers ?? new Renderer[0];

            if (bodyCollider is CapsuleCollider2D capsule)
            {
                originalColliderSize = capsule.size;
                originalColliderOffset = capsule.offset;
            }
            else if (bodyCollider is BoxCollider2D box)
            {
                originalColliderSize = box.size;
                originalColliderOffset = box.offset;
            }

            setupBackupCaptured = true;
        }

        public void RestoreSetupBackup()
        {
            if (!setupBackupCaptured) return;

            var rendererCount = Mathf.Min(
                originalRenderers?.Length ?? 0,
                originalRendererEnabledStates?.Length ?? 0);
            for (var index = 0; index < rendererCount; index++)
                if (originalRenderers[index] != null)
                    originalRenderers[index].enabled = originalRendererEnabledStates[index];

            if (originalVisualMotion != null)
                originalVisualMotion.enabled = originalVisualMotionEnabled;

            if (originalBodyCollider is CapsuleCollider2D capsule)
            {
                capsule.size = originalColliderSize;
                capsule.offset = originalColliderOffset;
            }
            else if (originalBodyCollider is BoxCollider2D box)
            {
                box.size = originalColliderSize;
                box.offset = originalColliderOffset;
            }
        }

        public void Configure(
            CharacterPngAnimationPreset configuredPreset,
            Animator configuredAnimator,
            SpriteRenderer configuredRenderer,
            Rigidbody2D configuredBody,
            PlayerMotorHost configuredMotor,
            PlayerInputHost configuredInput,
            MeleeAttackHost configuredMelee,
            EnemyAttackHost configuredEnemyAttack,
            CombatActorHost configuredActor,
            HelteBossPatternHost configuredHeltePattern,
            CombatVisualMotionHost configuredProceduralVisualMotion,
            bool configuredSourceFramesFaceRight,
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
            enemyAttack = configuredEnemyAttack;
            actor = configuredActor;
            heltePattern = configuredHeltePattern;
            proceduralVisualMotion = configuredProceduralVisualMotion;
            sourceFramesFaceRight = configuredSourceFramesFaceRight;
            facingTarget = configuredFacingTarget;
            attackOneDuration = Mathf.Max(0.01f, configuredAttackOneDuration);
            attackTwoDuration = Mathf.Max(0.01f, configuredAttackTwoDuration);
            attackThreeDuration = Mathf.Max(0.01f, configuredAttackThreeDuration);
        }

        private void Awake()
        {
            ResolveMissingParentReferences();
            DisableProceduralVisualMotion();
            if (!HasValidSetup)
            {
                Debug.LogError("CharacterPngAnimationBridge requires an Animator and SpriteRenderer.", this);
                enabled = false;
                return;
            }

            CaptureProceduralAttackBasePose();
            CaptureSortingOrder();
        }

        private void OnEnable()
        {
            ResolveMissingParentReferences();
            DisableProceduralVisualMotion();
            if (playerInput != null) playerInput.AimDirectionChanged += HandleAimDirectionChanged;
            if (meleeAttack != null) meleeAttack.AttackStarted += HandleAttackStarted;
            if (enemyAttack != null) enemyAttack.PhaseChanged += HandleEnemyAttackPhaseChanged;
            if (heltePattern != null) heltePattern.StateChanged += HandleHelteStateChanged;
            TrySubscribeCombatEvents();
            SyncMeleeAttackPresentationLock();
            ApplyInitialFacing();
        }

        private void OnDisable()
        {
            if (playerInput != null) playerInput.AimDirectionChanged -= HandleAimDirectionChanged;
            if (meleeAttack != null) meleeAttack.AttackStarted -= HandleAttackStarted;
            if (enemyAttack != null) enemyAttack.PhaseChanged -= HandleEnemyAttackPhaseChanged;
            if (heltePattern != null) heltePattern.StateChanged -= HandleHelteStateChanged;
            if (subscribedToCombatEvents && actor != null && actor.Events != null)
                actor.Events.Unsubscribe<HitConfirmed>(HandleHitConfirmed);
            subscribedToCombatEvents = false;
            if (animator != null) animator.speed = 1f;
            externalPlaybackResetAt = 0f;
            RestoreProceduralAttackBasePose();
            RestoreSortingOrder();
        }

        private void Update()
        {
            if (!HasValidSetup) return;
            if (externalPlaybackResetAt > 0f && Time.time >= externalPlaybackResetAt)
            {
                animator.speed = 1f;
                externalPlaybackResetAt = 0f;
            }
            TrySubscribeCombatEvents();
            UpdateFacing();
            UpdateAttackSortingPriority();

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
            {
                UpdateProceduralAttackMotion();
                UpdatePromeLocomotion();
            }
            else if (preset == CharacterPngAnimationPreset.Generic && Time.time >= actionLockedUntil)
                PlayState("Work");
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
            {
                if (HasAnimatorState("Fall")) PlayState("Fall");
                else PlayState("Jump");
            }
            else if (Mathf.Abs(velocity.x) > runVelocityThreshold)
                PlayState("Run");
            else
                PlayState("Idle");
        }

        private void HandleAttackStarted()
        {
            if (spriteRenderer != null && playerInput != null)
                spriteRenderer.flipX = ShouldFlipForDirection(playerInput.AimDirectionX);
            proceduralAttackDirection = playerInput == null || playerInput.AimDirectionX >= 0f ? 1f : -1f;
            PresentedAttackCount++;
            ApplyAttackSortingPriority();
            if (HasAttack01Clip)
            {
                RestoreProceduralAttackBasePose();
                PlayLockedState("Attack01", ResolveAnimationDuration("Attack01", attackOneDuration));
                return;
            }

            BeginProceduralAttackMotion();
        }

        public void PresentBossSkillStrike(float playbackSpeed, float lockSeconds)
        {
            PresentBossSkillStrike(playbackSpeed, lockSeconds,
                playerInput == null ? ResolveVisualFacingDirection() : playerInput.AimDirectionX);
        }

        public void PresentBossSkillStrike(float playbackSpeed, float lockSeconds, float facingDirection)
        {
            if (!HasValidSetup || preset != CharacterPngAnimationPreset.Prome) return;
            var direction = facingDirection >= 0f ? 1f : -1f;
            if (spriteRenderer != null)
                spriteRenderer.flipX = ShouldFlipForDirection(direction);
            proceduralAttackDirection = direction;
            PresentedAttackCount++;
            ApplyAttackSortingPriority();
            var duration = Mathf.Max(0.05f, lockSeconds);
            animator.speed = Mathf.Max(0.1f, playbackSpeed);
            externalPlaybackResetAt = Time.time + duration;
            if (HasAttack01Clip)
            {
                RestoreProceduralAttackBasePose();
                PlayLockedState("Attack01", duration);
                return;
            }

            attackOneDuration = duration;
            BeginProceduralAttackMotion();
        }

        private void HandleEnemyAttackPhaseChanged(EnemyAttackPhase phase)
        {
            if (preset != CharacterPngAnimationPreset.Generic) return;
            if (phase == EnemyAttackPhase.Telegraph)
                PlayLockedState("Attack", attackOneDuration);
            else if (phase == EnemyAttackPhase.Ready && Time.time >= actionLockedUntil)
                PlayState("Work");
        }

        private void HandleHelteStateChanged(HelteCombatState state)
        {
            // Blink and dash can relocate Helte in the same frame as a state change.
            // Refresh the authored left-facing sprite before the attack frame is shown.
            UpdateFacing();
            PlayState(ResolveHelteAnimationState(state));
        }

        public static string ResolveHelteAnimationState(HelteCombatState state)
        {
            return state switch
            {
                HelteCombatState.Disabled => "Idle",
                HelteCombatState.Waiting => "Idle",
                HelteCombatState.FinalRushTransition => "PhaseTransition",
                HelteCombatState.FakeBlinkVanish => "BlinkVanish",
                HelteCombatState.FakeBlinkReappear => "BlinkReappear",
                HelteCombatState.FakeBlinkPause => "Recover",
                HelteCombatState.CounterSucceeded => "CounterStance",
                HelteCombatState.CounterOpen => "Recover",
                HelteCombatState.MercyRetreat => "Recover",
                _ => state.ToString()
            };
        }

        private void HandleHitConfirmed(HitConfirmed message)
        {
            if (actor == null || message.TargetId != actor.ActorId) return;
            if (ShouldSuppressHitReaction(bossSkill != null && bossSkill.IsExecuting)) return;
            PlayLockedState("Hit", hitDuration);
        }

        public static bool ShouldSuppressHitReaction(bool bossSkillExecuting) => bossSkillExecuting;

        private void HandleAimDirectionChanged(float direction)
        {
            if (preset == CharacterPngAnimationPreset.Prome &&
                ((meleeAttack != null && meleeAttack.IsAttackDirectionLocked) ||
                 Time.time < actionLockedUntil))
                return;
            if (spriteRenderer != null && !Mathf.Approximately(direction, 0f))
                spriteRenderer.flipX = ShouldFlipForDirection(direction);
        }

        private void UpdateFacing()
        {
            if (spriteRenderer == null) return;

            if (preset == CharacterPngAnimationPreset.Prome)
            {
                if ((meleeAttack != null && meleeAttack.IsAttackDirectionLocked) ||
                    Time.time < actionLockedUntil) return;
                if (movementBody == null) return;
                var horizontalVelocity = movementBody.linearVelocity.x;
                if (Mathf.Abs(horizontalVelocity) > runVelocityThreshold)
                    spriteRenderer.flipX = ShouldFlipForDirection(horizontalVelocity);
                return;
            }

            if ((preset != CharacterPngAnimationPreset.Helte && preset != CharacterPngAnimationPreset.Generic) ||
                facingTarget == null) return;
            var delta = facingTarget.position.x - transform.position.x;
            // Helte's authored frames currently face left.  Do not assume every
            // enemy source faces right: use the same authored-frame convention as
            // Prome so blink, dash, and slash states always look at the player.
            if (!Mathf.Approximately(delta, 0f))
                spriteRenderer.flipX = ShouldFlipForDirection(delta);
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
            if (restart)
                animator.Play(stateHash, 0, 0f);
            else
                animator.CrossFadeInFixedTime(
                    stateHash,
                    Mathf.Max(0f, transitionSeconds ?? crossFadeSeconds),
                    0,
                    0f);
            currentState = stateName;
        }

        private bool HasAnimatorState(string stateName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(stateName)) return false;
            return animator.HasState(0, Animator.StringToHash($"Base Layer.{stateName}"));
        }

        private float ResolveAnimationDuration(string stateName, float fallback)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return Mathf.Max(0.01f, fallback);

            var clip = animator.runtimeAnimatorController.animationClips.FirstOrDefault(candidate =>
                candidate != null && candidate.name.Equals(stateName, System.StringComparison.OrdinalIgnoreCase));
            return clip == null ? Mathf.Max(0.01f, fallback) : Mathf.Max(0.01f, clip.length);
        }

        private void SyncMeleeAttackPresentationLock()
        {
            if (preset != CharacterPngAnimationPreset.Prome || meleeAttack == null) return;
            meleeAttack.SetPresentationLockDuration(
                ResolveAnimationDuration("Attack01", attackOneDuration));
        }

        private void BeginProceduralAttackMotion()
        {
            CaptureProceduralAttackBasePose();
            var duration = Mathf.Max(0.01f, attackOneDuration);
            proceduralAttackStartedAt = Time.time;
            proceduralAttackEndsAt = Time.time + duration;
            actionLockedUntil = proceduralAttackEndsAt;
        }

        private void UpdateProceduralAttackMotion()
        {
            if (!proceduralAttackBaseCaptured || spriteRenderer == null) return;
            if (Time.time >= proceduralAttackEndsAt)
            {
                RestoreProceduralAttackBasePose();
                return;
            }

            var duration = Mathf.Max(0.01f, proceduralAttackEndsAt - proceduralAttackStartedAt);
            var progress = Mathf.Clamp01((Time.time - proceduralAttackStartedAt) / duration);
            float lunge;
            float rotation;
            float stretch;
            if (progress < 0.2f)
            {
                var pose = Mathf.SmoothStep(0f, 1f, progress / 0.2f);
                lunge = Mathf.Lerp(0f, -0.045f, pose);
                rotation = Mathf.Lerp(0f, 10f, pose);
                stretch = Mathf.Lerp(0f, -0.04f, pose);
            }
            else if (progress < 0.62f)
            {
                var pose = Mathf.SmoothStep(0f, 1f, (progress - 0.2f) / 0.42f);
                lunge = Mathf.Lerp(-0.045f, 0.2f, pose);
                rotation = Mathf.Lerp(10f, -28f, pose);
                stretch = Mathf.Lerp(-0.04f, 0.16f, pose);
            }
            else
            {
                var pose = Mathf.SmoothStep(0f, 1f, (progress - 0.62f) / 0.38f);
                lunge = Mathf.Lerp(0.2f, 0f, pose);
                rotation = Mathf.Lerp(-28f, 0f, pose);
                stretch = Mathf.Lerp(0.16f, 0f, pose);
            }

            var visual = spriteRenderer.transform;
            visual.localPosition = proceduralAttackBasePosition +
                                   Vector3.right * (lunge * proceduralAttackDirection);
            visual.localRotation = proceduralAttackBaseRotation *
                                   Quaternion.Euler(0f, 0f, rotation * proceduralAttackDirection);
            visual.localScale = Vector3.Scale(
                proceduralAttackBaseScale,
                new Vector3(1f + stretch, 1f - (stretch * 0.35f), 1f));
        }

        private void CaptureProceduralAttackBasePose()
        {
            if (proceduralAttackBaseCaptured || spriteRenderer == null) return;
            var visual = spriteRenderer.transform;
            proceduralAttackBasePosition = visual.localPosition;
            proceduralAttackBaseRotation = visual.localRotation;
            proceduralAttackBaseScale = visual.localScale;
            proceduralAttackBaseCaptured = true;
        }

        private void RestoreProceduralAttackBasePose()
        {
            if (!proceduralAttackBaseCaptured || spriteRenderer == null) return;
            var visual = spriteRenderer.transform;
            visual.localPosition = proceduralAttackBasePosition;
            visual.localRotation = proceduralAttackBaseRotation;
            visual.localScale = proceduralAttackBaseScale;
            proceduralAttackStartedAt = 0f;
            proceduralAttackEndsAt = 0f;
        }

        private void CaptureSortingOrder()
        {
            if (sortingOrderCaptured || spriteRenderer == null) return;
            baseSortingOrder = spriteRenderer.sortingOrder;
            sortingOrderCaptured = true;
        }

        private void ApplyAttackSortingPriority()
        {
            CaptureSortingOrder();
            if (!sortingOrderCaptured || spriteRenderer == null) return;
            spriteRenderer.sortingOrder = Mathf.Max(baseSortingOrder + 1, attackSortingOrder);
            attackSortingPriorityActive = true;
        }

        private void UpdateAttackSortingPriority()
        {
            if (!attackSortingPriorityActive || Time.time < actionLockedUntil) return;
            RestoreSortingOrder();
        }

        private void RestoreSortingOrder()
        {
            if (!sortingOrderCaptured || spriteRenderer == null) return;
            spriteRenderer.sortingOrder = baseSortingOrder;
            attackSortingPriorityActive = false;
        }

        private void ResolveMissingParentReferences()
        {
            if (movementBody == null) movementBody = GetComponentInParent<Rigidbody2D>(true);
            if (playerMotor == null) playerMotor = GetComponentInParent<PlayerMotorHost>(true);
            if (playerInput == null) playerInput = GetComponentInParent<PlayerInputHost>(true);
            if (meleeAttack == null) meleeAttack = GetComponentInParent<MeleeAttackHost>(true);
            if (enemyAttack == null) enemyAttack = GetComponentInParent<EnemyAttackHost>(true);
            if (actor == null) actor = GetComponentInParent<CombatActorHost>(true);
            if (heltePattern == null) heltePattern = GetComponentInParent<HelteBossPatternHost>(true);
            if (bossSkill == null) bossSkill = GetComponentInParent<PromeBossSkillHost>(true);
            if (proceduralVisualMotion == null)
                proceduralVisualMotion = GetComponentInParent<CombatVisualMotionHost>(true);
        }

        private void DisableProceduralVisualMotion()
        {
            if (proceduralVisualMotion != null)
                proceduralVisualMotion.enabled = false;
        }

        private bool ShouldFlipForDirection(float direction)
        {
            return ShouldFlipAuthoredSprite(sourceFramesFaceRight, direction);
        }

        public static bool ShouldFlipAuthoredSprite(bool sourceFacesRight, float direction) =>
            sourceFacesRight ? direction < 0f : direction > 0f;

        private float ResolveVisualFacingDirection()
        {
            if (spriteRenderer == null)
                return playerInput == null || playerInput.AimDirectionX >= 0f ? 1f : -1f;
            if (sourceFramesFaceRight)
                return spriteRenderer.flipX ? -1f : 1f;
            return spriteRenderer.flipX ? 1f : -1f;
        }
    }
}
