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
        private bool jumpRequested;
        private bool dashRequested;
        private bool glideHeld;
        private bool movementSignalArmed;
        private float dashEndsAt;
        private float dashCooldownEndsAt;
        private float dashDirection = 1f;

        public bool HasServiceRoot => serviceRoot != null;
        public bool IsGliding { get; private set; }

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

            var grounded = Physics2D.OverlapCircle(groundProbe.position, motorDefinition.GroundProbeRadius, groundLayers) != null;
            if (jumpRequested && grounded)
            {
                velocity.y = motorDefinition.JumpVelocity;
                serviceRoot?.Events.Publish(new GameplaySignal(QuestSignalType.JumpPerformed, playerId));
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

        private void OnDisable()
        {
            glideHeld = false;
            IsGliding = false;
        }
    }
}
