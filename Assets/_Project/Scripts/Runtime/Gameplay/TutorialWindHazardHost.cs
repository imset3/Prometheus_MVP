using System.Collections.Generic;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Adds upward recovery only while the player is physically inside the wind
    /// volume and is holding the existing Space/glide input.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialWindHazardHost : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private Transform player;
        [SerializeField] private PlayerMotorHost playerMotor;
        [SerializeField, Min(0f)] private float liftAcceleration = 24f;
        [SerializeField, Min(0f)] private float maximumRiseSpeed = 8f;

        private readonly HashSet<int> overlappingPlayerColliders = new HashSet<int>();

        public bool HasValidSetup => playerBody != null && player != null && playerMotor != null &&
                                     liftAcceleration > 0f && maximumRiseSpeed > 0f;
        public bool RequiresGlideInput => true;
        public float MaximumRiseSpeed => maximumRiseSpeed;

        private void Awake()
        {
            var trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.isTrigger = true;
            if (HasValidSetup) return;

            Debug.LogError(
                "TutorialWindHazardHost requires player body, transform, motor, and positive lift values.",
                this);
            enabled = false;
        }

        private void FixedUpdate()
        {
            if (!TutorialEnvironmentHazardPolicy.ShouldApplyWind(
                    overlappingPlayerColliders.Count > 0,
                    playerMotor.IsGlideHeld))
                return;
            var velocity = playerBody.linearVelocity;
            velocity.y = TutorialEnvironmentHazardPolicy.ResolveWindVelocity(
                velocity.y,
                liftAcceleration,
                maximumRiseSpeed,
                Mathf.Abs(Physics2D.gravity.y * playerBody.gravityScale),
                Time.fixedDeltaTime);
            playerBody.linearVelocity = velocity;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsPlayer(other)) overlappingPlayerColliders.Add(other.GetInstanceID());
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other != null) overlappingPlayerColliders.Remove(other.GetInstanceID());
        }

        private void OnDisable() => overlappingPlayerColliders.Clear();

        private bool IsPlayer(Collider2D other)
        {
            return other != null && (other.transform == player || other.transform.IsChildOf(player));
        }
    }
}
