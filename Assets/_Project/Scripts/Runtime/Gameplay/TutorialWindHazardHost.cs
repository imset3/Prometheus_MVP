using System.Collections.Generic;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Adds marker-directed recovery only while the player is physically inside the
    /// wind volume and is holding the existing Space/glide input. Moving, rotating,
    /// or scaling this object changes the live wind without code changes.
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
        private Collider2D windTrigger;

        public bool HasValidSetup => playerBody != null && player != null && playerMotor != null &&
                                     liftAcceleration > 0f && maximumRiseSpeed > 0f;
        public bool RequiresGlideInput => true;
        public float MaximumRiseSpeed => maximumRiseSpeed;
        public Vector2 WorldDirection
        {
            get
            {
                var direction = (Vector2)transform.up;
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
            }
        }

        private void Awake()
        {
            windTrigger = GetComponent<Collider2D>();
            if (windTrigger != null) windTrigger.isTrigger = true;
            if (HasValidSetup) return;

            Debug.LogError(
                "TutorialWindHazardHost requires player body, transform, motor, and positive lift values.",
                this);
            enabled = false;
        }

        private void FixedUpdate()
        {
            var containsPlayerCenter = windTrigger != null && player != null &&
                                       windTrigger.OverlapPoint(player.position);
            if (!TutorialEnvironmentHazardPolicy.ShouldApplyWind(
                    overlappingPlayerColliders.Count > 0 || containsPlayerCenter,
                    playerMotor.IsGlideHeld))
                return;
            playerBody.linearVelocity = TutorialEnvironmentHazardPolicy.ResolveDirectionalWindVelocity(
                playerBody.linearVelocity,
                WorldDirection,
                liftAcceleration,
                maximumRiseSpeed,
                Physics2D.gravity * playerBody.gravityScale,
                Time.fixedDeltaTime);
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
