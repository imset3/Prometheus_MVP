using System.Collections.Generic;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Prome's basic ranged attack. Key 1 launches the pre-placed projectile in the
    /// player's current facing direction; projectile objects are never created at runtime.
    /// </summary>
    public sealed class PlayerRangedAttackHost : MonoBehaviour
    {
        [SerializeField] private PlayerInputHost inputHost;
        [SerializeField] private CombatActorHost sourceActor;
        [SerializeField] private Collider2D projectileHitbox;
        [SerializeField] private GameObject projectileVisualSlot;
        [SerializeField] private LayerMask targetLayers = -1;
        [SerializeField] private string attackId = "WPN-PROME-RANGED-BASIC";
        [SerializeField, Min(1)] private int damage = 20;
        [SerializeField, Min(0.1f)] private float spawnOffset = 1f;
        [SerializeField, Min(0.1f)] private float travelDistance = 10f;
        [SerializeField, Min(0.05f)] private float travelSeconds = 0.55f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 0.6f;
        [SerializeField] private bool startsUnlocked = true;
        [SerializeField, Min(1)] private int trainingMultiHitTargetCount = 3;
        [SerializeField] private string trainingSignalTargetId = "PLAYER-001";

        private readonly Collider2D[] overlapResults = new Collider2D[8];
        private readonly RaycastHit2D[] sweepResults = new RaycastHit2D[16];
        private readonly HashSet<string> hitActorIds = new HashSet<string>();
        private Vector3 originLocalPosition;
        private Vector3 launchPosition;
        private Vector3 launchDirection;
        private float launchedAt;
        private float cooldownEndsAt;
        private bool projectileActive;
        private bool trainingSignalPublished;
        private bool bossSkillOverrideActive;
        private bool isUnlocked;

        public bool HasValidSetup => inputHost != null && sourceActor != null && projectileHitbox != null && projectileVisualSlot != null;
        public bool IsProjectileActive => projectileActive;
        public bool HasAssignedInput => inputHost != null;
        public float CooldownSeconds => cooldownSeconds;
        public float CooldownRemaining => Mathf.Max(0f, cooldownEndsAt - Time.time);
        public float CooldownNormalized => cooldownSeconds <= 0f
            ? 0f
            : Mathf.Clamp01(CooldownRemaining / cooldownSeconds);
        public bool IsUnlocked => isUnlocked;
        public bool IsAvailable => isUnlocked && !bossSkillOverrideActive;
        public bool IsReady => IsAvailable && !projectileActive && CooldownRemaining <= 0f;
        public event System.Action<Vector2> RangedAttackStarted;
        public void SetBossSkillOverride(bool active)
        {
            bossSkillOverrideActive = active;
            if (active && projectileActive) ResetProjectile();
        }

        public void SetUnlocked(bool unlocked)
        {
            isUnlocked = unlocked;
            if (!unlocked && projectileActive) ResetProjectile();
        }

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("PlayerRangedAttackHost requires pre-placed input, actor, projectile hitbox, and visual references.", this);
                enabled = false;
                return;
            }

            originLocalPosition = transform.localPosition;
            isUnlocked = startsUnlocked;
            ResetProjectile();
        }

        private void OnEnable()
        {
            if (inputHost != null) inputHost.ModuleRequested += HandleRangedInput;
        }

        private void OnDisable()
        {
            if (inputHost != null) inputHost.ModuleRequested -= HandleRangedInput;
            ResetProjectile();
        }

        private void Update()
        {
            if (!projectileActive) return;

            var progress = Mathf.Clamp01((Time.time - launchedAt) / travelSeconds);
            var nextPosition = launchPosition + launchDirection * (travelDistance * progress);
            ApplySweepHits(nextPosition - transform.position);
            transform.position = nextPosition;
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
            if (!isActiveAndEnabled || !IsAvailable || projectileActive || Time.time < cooldownEndsAt ||
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
            trainingSignalPublished = false;
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
                TryApplyHit(overlapResults[index]);

            PublishTrainingSignalIfReady();
        }

        private void ApplySweepHits(Vector3 movement)
        {
            var distance = movement.magnitude;
            if (distance <= 0.001f) return;

            var filter = ContactFilter2D.noFilter;
            filter.SetLayerMask(targetLayers);
            filter.useTriggers = true;
            var count = projectileHitbox.Cast(movement / distance, filter, sweepResults, distance);
            for (var index = 0; index < count; index++)
                if (sweepResults[index].collider != null)
                    TryApplyHit(sweepResults[index].collider);

            PublishTrainingSignalIfReady();
        }

        private void TryApplyHit(Collider2D candidate)
        {
            var target = candidate != null ? candidate.GetComponentInParent<CombatActorHost>() : null;
            if (target == null || target.Kind == sourceActor.Kind || !hitActorIds.Add(target.ActorId)) return;
            sourceActor.CombatSystem.TryApplyDamage(
                target.ActorId,
                new DamagePacket(sourceActor.ActorId, attackId, damage));
        }

        private void PublishTrainingSignalIfReady()
        {
            if (!trainingSignalPublished && hitActorIds.Count >= trainingMultiHitTargetCount)
            {
                trainingSignalPublished = true;
                sourceActor.Events?.Publish(new GameplaySignal(
                    Narthex.Content.QuestSignalType.RangedTripleHitPerformed,
                    trainingSignalTargetId));
            }
        }

        private void ResetProjectile()
        {
            projectileActive = false;
            if (projectileHitbox != null) projectileHitbox.enabled = false;
            if (projectileVisualSlot != null) projectileVisualSlot.SetActive(false);
            transform.localPosition = originLocalPosition;
            hitActorIds.Clear();
            trainingSignalPublished = false;
        }

        private void HandleRangedInput() => TryFire();
    }
}
