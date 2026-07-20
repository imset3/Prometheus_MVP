using System.Collections.Generic;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Input-agnostic ranged attack foundation. A future input/ability decision only needs to call TryFire.
    /// The projectile and visual are pre-placed scene objects and are never created at runtime.
    /// </summary>
    public sealed class PlayerRangedAttackHost : MonoBehaviour
    {
        [SerializeField] private PlayerInputHost inputHost;
        [SerializeField] private CombatActorHost sourceActor;
        [SerializeField] private Collider2D projectileHitbox;
        [SerializeField] private GameObject projectileVisualSlot;
        [SerializeField] private LayerMask targetLayers = -1;
        [SerializeField] private string attackId = "WPN-PROME-RANGED-PLACEHOLDER";
        [SerializeField, Min(1)] private int damage = 20;
        [SerializeField, Min(0.1f)] private float spawnOffset = 1f;
        [SerializeField, Min(0.1f)] private float travelDistance = 10f;
        [SerializeField, Min(0.05f)] private float travelSeconds = 0.55f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 0.6f;

        private readonly Collider2D[] overlapResults = new Collider2D[8];
        private readonly HashSet<string> hitActorIds = new HashSet<string>();
        private Vector3 originLocalPosition;
        private Vector3 launchPosition;
        private Vector3 launchDirection;
        private float launchedAt;
        private float cooldownEndsAt;
        private bool projectileActive;

        public bool HasValidSetup => inputHost != null && sourceActor != null && projectileHitbox != null && projectileVisualSlot != null;
        public bool IsProjectileActive => projectileActive;
        public bool HasAssignedInput => false;
        public event System.Action<Vector2> RangedAttackStarted;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("PlayerRangedAttackHost requires pre-placed input, actor, projectile hitbox, and visual references.", this);
                enabled = false;
                return;
            }

            originLocalPosition = transform.localPosition;
            ResetProjectile();
        }

        private void OnDisable()
        {
            ResetProjectile();
        }

        private void Update()
        {
            if (!projectileActive) return;

            var progress = Mathf.Clamp01((Time.time - launchedAt) / travelSeconds);
            transform.position = launchPosition + launchDirection * (travelDistance * progress);
            Physics2D.SyncTransforms();
            ApplyHits();
            if (progress >= 1f) ResetProjectile();
        }

        public bool TryFire()
        {
            return TryFire(Vector2.right * inputHost.AimDirectionX);
        }

        public bool TryFire(Vector2 direction)
        {
            if (!isActiveAndEnabled || projectileActive || Time.time < cooldownEndsAt ||
                sourceActor.Runtime == null || sourceActor.CombatSystem == null || !sourceActor.Runtime.IsAlive)
                return false;

            if (direction.sqrMagnitude < 0.01f) direction = Vector2.right * inputHost.AimDirectionX;
            launchDirection = ((Vector3)direction).normalized;
            launchDirection.z = 0f;
            launchPosition = sourceActor.transform.position + launchDirection * spawnOffset;
            transform.position = launchPosition;
            launchedAt = Time.time;
            cooldownEndsAt = Time.time + cooldownSeconds;
            projectileActive = true;
            projectileHitbox.enabled = true;
            projectileVisualSlot.SetActive(true);
            hitActorIds.Clear();
            Physics2D.SyncTransforms();
            ApplyHits();
            RangedAttackStarted?.Invoke(direction.normalized);
            return true;
        }

        private void ApplyHits()
        {
            var filter = ContactFilter2D.noFilter;
            filter.SetLayerMask(targetLayers);
            filter.useTriggers = true;
            var count = projectileHitbox.Overlap(filter, overlapResults);
            for (var index = 0; index < count; index++)
            {
                var target = overlapResults[index].GetComponentInParent<CombatActorHost>();
                if (target == null || target.Kind == sourceActor.Kind || !hitActorIds.Add(target.ActorId)) continue;
                sourceActor.CombatSystem.TryApplyDamage(
                    target.ActorId,
                    new DamagePacket(sourceActor.ActorId, attackId, damage));
            }
        }

        private void ResetProjectile()
        {
            projectileActive = false;
            if (projectileHitbox != null) projectileHitbox.enabled = false;
            if (projectileVisualSlot != null) projectileVisualSlot.SetActive(false);
            transform.localPosition = originLocalPosition;
            hitActorIds.Clear();
        }
    }
}
