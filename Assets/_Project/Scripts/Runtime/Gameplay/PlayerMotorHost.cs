using Narthex.Content;
using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class PlayerMotorHost : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Transform groundProbe;
        [SerializeField] private LayerMask groundLayers = -1;
        [SerializeField] private PlayerMotorDefinition motorDefinition;
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private string playerId = "PLAYER-001";
        [SerializeField, Min(0f)] private float movementSignalSpeed = 0.1f;

        private Vector2 movementInput;
        private readonly Collider2D[] groundOverlapResults = new Collider2D[8];
        private bool jumpRequested;
        private bool dashRequested;
        private bool glideHeld;
        private bool movementSignalArmed;
        private bool doubleJumpUnlocked;
        private bool doubleJumpAvailable;
        private float dashEndsAt;
        private float dashCooldownEndsAt;
        private float dashDirection = 1f;

        public bool HasServiceRoot => serviceRoot != null;
        public bool IsGliding { get; private set; }
        public bool IsGlideHeld => glideHeld;
        public bool IsDoubleJumpUnlocked => doubleJumpUnlocked;

        private void Awake()
        {
            if (body == null || groundProbe == null || motorDefinition == null || serviceRoot == null)
                Debug.LogError("PlayerMotorHost requires pre-placed Rigidbody2D, GroundProbe, PlayerMotorDefinition, and ServiceRoot references.", this);
            else serviceRoot.Initialize();
        }

        public void SetMovementInput(Vector2 input)
        {
            movementInput = input;
            if (Mathf.Abs(input.x) > 0.01f) dashDirection = Mathf.Sign(input.x);
            if (Mathf.Abs(input.x) <= 0.01f) movementSignalArmed = false;
            else if (!movementSignalArmed) movementSignalArmed = true;
        }

        public void RequestJump() => jumpRequested = true;
        public void RequestDash() => dashRequested = true;
        public void SetGlideHeld(bool held) => glideHeld = held;

        public void ResetTransientInput()
        {
            movementInput = Vector2.zero;
            jumpRequested = false;
            dashRequested = false;
            glideHeld = false;
            movementSignalArmed = false;
            dashEndsAt = 0f;
            IsGliding = false;
        }

        public void StopHorizontalMotion()
        {
            if (body == null) return;
            var velocity = body.linearVelocity;
            velocity.x = 0f;
            body.linearVelocity = velocity;
            dashEndsAt = 0f;
        }

        public void UnlockDoubleJump()
        {
            doubleJumpUnlocked = true;
            doubleJumpAvailable = true;
        }

        private void FixedUpdate()
        {
            if (body == null || groundProbe == null || motorDefinition == null) return;

            var velocity = body.linearVelocity;
            var now = Time.time;
            if (dashRequested && now >= dashCooldownEndsAt)
            {
                dashEndsAt = now + motorDefinition.DashDuration;
                dashCooldownEndsAt = now + motorDefinition.DashCooldown;
                serviceRoot?.Events.Publish(new GameplaySignal(QuestSignalType.DashPerformed, playerId));
            }

            if (now < dashEndsAt)
            {
                velocity.x = dashDirection * motorDefinition.DashSpeed;
            }
            else
            {
                var targetSpeed = movementInput.x * motorDefinition.MaxRunSpeed;
                velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, motorDefinition.GroundAcceleration * Time.fixedDeltaTime);
            }

            var grounded = IsStandingOnGround();
            if (grounded) doubleJumpAvailable = doubleJumpUnlocked;
            if (jumpRequested && (grounded || (doubleJumpUnlocked && doubleJumpAvailable)))
            {
                var performedDoubleJump = !grounded;
                velocity.y = motorDefinition.JumpVelocity;
                if (!grounded) doubleJumpAvailable = false;
                serviceRoot?.Events.Publish(new GameplaySignal(QuestSignalType.JumpPerformed, playerId));
                if (performedDoubleJump)
                    serviceRoot?.Events.Publish(new GameplaySignal(QuestSignalType.DoubleJumpPerformed, playerId));
            }

            IsGliding = glideHeld && !grounded && velocity.y < 0f;
            if (IsGliding)
                velocity.y = Mathf.Max(velocity.y, -motorDefinition.GlideFallSpeed);

            if (movementSignalArmed && Mathf.Abs(velocity.x) >= movementSignalSpeed)
            {
                movementSignalArmed = false;
                serviceRoot?.Events.Publish(new GameplaySignal(QuestSignalType.MovementPerformed, playerId));
            }

            jumpRequested = false;
            dashRequested = false;
            body.linearVelocity = velocity;
        }

        private bool IsStandingOnGround()
        {
            var filter = ContactFilter2D.noFilter;
            filter.SetLayerMask(groundLayers);
            filter.useTriggers = false;
            var count = Physics2D.OverlapCircle(
                groundProbe.position,
                motorDefinition.GroundProbeRadius,
                filter,
                groundOverlapResults);

            for (var index = 0; index < count; index++)
            {
                var candidate = groundOverlapResults[index];
                if (candidate == null || candidate.attachedRigidbody == body ||
                    candidate.transform == transform || candidate.transform.IsChildOf(transform))
                    continue;
                return true;
            }

            return false;
        }

        private void OnDisable()
        {
            glideHeld = false;
            IsGliding = false;
        }
    }
}
