using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    public static class TutorialEnvironmentHazardPolicy
    {
        public static int ResolveFractionalDamage(int maximumHealth, float fraction)
        {
            return Mathf.Max(1, Mathf.CeilToInt(
                Mathf.Max(1, maximumHealth) * Mathf.Clamp01(fraction)));
        }

        public static bool ShouldApplyWind(bool isInside, bool isGlideHeld)
        {
            return isInside && isGlideHeld;
        }

        public static float ResolveWindVelocity(
            float currentVelocity,
            float liftAcceleration,
            float maximumRiseSpeed,
            float gravityMagnitude,
            float fixedDeltaTime)
        {
            var upwardVelocity = Mathf.Max(0f, currentVelocity);
            var accelerated = Mathf.MoveTowards(
                upwardVelocity,
                Mathf.Max(0f, maximumRiseSpeed),
                Mathf.Max(0f, liftAcceleration) * Mathf.Max(0f, fixedDeltaTime));
            var gravityCompensation =
                Mathf.Max(0f, gravityMagnitude) * Mathf.Max(0f, fixedDeltaTime);
            return Mathf.Min(Mathf.Max(0f, maximumRiseSpeed), accelerated + gravityCompensation);
        }

        public static bool ShouldReturnToSafePoint(bool playerAlive)
        {
            return playerAlive;
        }
    }

    /// <summary>
    /// Owns the latest safe point inside G and applies lava damage through the
    /// normal combat/death pipeline.
    /// </summary>
    public sealed class TutorialEnvironmentHazardCoordinatorHost : MonoBehaviour
    {
        [SerializeField] private CombatSystemHost combatSystemHost;
        [SerializeField] private CombatActorHost playerActor;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private Transform defaultSafePoint;
        [SerializeField, Range(0.01f, 1f)] private float lavaDamageFraction = 0.2f;
        [SerializeField, Min(0f)] private float lavaRetriggerDelay = 0.35f;

        private Transform currentSafePoint;
        private float nextLavaTime;

        public bool HasValidSetup => combatSystemHost != null && playerActor != null &&
                                     playerBody != null && defaultSafePoint != null &&
                                     lavaDamageFraction > 0f;
        public float LavaDamageFraction => lavaDamageFraction;
        public string CurrentSafePointName =>
            currentSafePoint != null ? currentSafePoint.name : string.Empty;

        private void Awake()
        {
            if (!HasValidSetup || !combatSystemHost.Initialize())
            {
                Debug.LogError(
                    "TutorialEnvironmentHazardCoordinatorHost requires combat, player, body, and default safe point references.",
                    this);
                enabled = false;
                return;
            }

            currentSafePoint = defaultSafePoint;
        }

        private void OnEnable()
        {
            if (combatSystemHost != null)
                combatSystemHost.Events?.Subscribe<PlayerRespawned>(HandlePlayerRespawned);
        }

        private void OnDisable()
        {
            combatSystemHost?.Events?.Unsubscribe<PlayerRespawned>(HandlePlayerRespawned);
        }

        public void SetSafePoint(Transform safePoint)
        {
            if (safePoint != null) currentSafePoint = safePoint;
        }

        public bool TryHandleLava(string hazardId)
        {
            if (!enabled || Time.time < nextLavaTime || playerActor.Runtime == null ||
                !playerActor.Runtime.IsAlive)
                return false;

            nextLavaTime = Time.time + lavaRetriggerDelay;
            var damage = TutorialEnvironmentHazardPolicy.ResolveFractionalDamage(
                playerActor.Runtime.MaxHealth,
                lavaDamageFraction);
            var applied = playerActor.CombatSystem.TryApplyDamage(
                playerActor.ActorId,
                new DamagePacket("ENVIRONMENT", hazardId, damage));
            if (!applied ||
                !TutorialEnvironmentHazardPolicy.ShouldReturnToSafePoint(
                    playerActor.Runtime.IsAlive))
                return applied;

            var destination = currentSafePoint != null ? currentSafePoint : defaultSafePoint;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = destination.position;
            playerActor.transform.position = destination.position;
            Physics2D.SyncTransforms();
            return true;
        }

        private void HandlePlayerRespawned(PlayerRespawned message)
        {
            if (message.PlayerId != playerActor.ActorId) return;
            currentSafePoint = defaultSafePoint;
            nextLavaTime = 0f;
        }
    }
}
