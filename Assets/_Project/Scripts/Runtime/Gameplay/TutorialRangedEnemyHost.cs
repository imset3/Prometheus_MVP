using System;
using UnityEngine;

namespace Narthex.Gameplay
{
    public enum TutorialRangedEnemyPhase
    {
        Ready,
        Telegraph,
        Fire,
        Recovery
    }

    /// <summary>
    /// Keeps a readable firing lane instead of using the melee pursuit loop.
    /// Spawn position remains authored by the encounter's hierarchy markers.
    /// </summary>
    public sealed class TutorialRangedEnemyHost : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatActorHost actor;
        [SerializeField] private Transform target;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private TutorialGroundedEnemyMotorHost groundMotor;
        [SerializeField] private Transform muzzleAnchor;
        [SerializeField] private GameObject warningVisualSlot;
        [SerializeField] private Renderer warningRenderer;
        [SerializeField] private TutorialEnemyProjectileHost[] projectilePool = new TutorialEnemyProjectileHost[0];

        [Header("Spacing")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.35f;
        [SerializeField, Min(0.1f)] private float retreatDistance = 3.2f;
        [SerializeField, Min(0.1f)] private float preferredDistance = 5.2f;
        [SerializeField, Min(0.1f)] private float maximumAttackDistance = 8.5f;
        [Header("Attack")]
        [SerializeField] private string attackId = "ENEMY-TUTO-RANGED";
        [SerializeField] private int damage = 12;
        [SerializeField, Min(0.05f)] private float telegraphSeconds = 0.52f;
        [SerializeField, Min(0.05f)] private float recoverySeconds = 1.2f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 7.5f;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 4f;
        [SerializeField] private Color warningColor = new Color(0.2f, 0.9f, 1f, 0.92f);

        private readonly RaycastHit2D[] sightHits = new RaycastHit2D[24];
        private float phaseEndsAt;
        private float muzzleX;
        private Vector3 warningBaseScale;
        private MaterialPropertyBlock warningProperties;

        public TutorialRangedEnemyPhase CurrentPhase { get; private set; } = TutorialRangedEnemyPhase.Ready;
        public int ShotsFired { get; private set; }
        public bool HasValidSetup => actor != null && target != null && bodyCollider != null && muzzleAnchor != null &&
                                     warningVisualSlot != null && projectilePool != null && projectilePool.Length > 0 &&
                                     groundMotor != null && groundMotor.HasValidSetup &&
                                     Array.TrueForAll(projectilePool, item => item != null);
        public event Action<TutorialRangedEnemyPhase> PhaseChanged;

        public void Configure(
            CombatActorHost configuredActor,
            Transform configuredTarget,
            Collider2D configuredBodyCollider,
            Transform configuredMuzzle,
            GameObject configuredWarning,
            Renderer configuredWarningRenderer,
            TutorialEnemyProjectileHost[] configuredPool)
        {
            actor = configuredActor;
            target = configuredTarget;
            bodyCollider = configuredBodyCollider;
            muzzleAnchor = configuredMuzzle;
            warningVisualSlot = configuredWarning;
            warningRenderer = configuredWarningRenderer;
            projectilePool = configuredPool ?? new TutorialEnemyProjectileHost[0];
            groundMotor ??= GetComponent<TutorialGroundedEnemyMotorHost>();
        }

        private void Awake()
        {
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            groundMotor ??= GetComponent<TutorialGroundedEnemyMotorHost>();
            if (warningRenderer == null && warningVisualSlot != null)
                warningRenderer = warningVisualSlot.GetComponentInChildren<Renderer>(true);
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialRangedEnemyHost requires actor, target, body, muzzle, warning, and projectile pool references.", this);
                enabled = false;
                return;
            }

            muzzleX = Mathf.Max(0.05f, Mathf.Abs(muzzleAnchor.localPosition.x));
            warningBaseScale = warningVisualSlot.transform.localScale;
            warningProperties = new MaterialPropertyBlock();
            ApplyWarningColor();
            ResetAttackState();
        }

        private void OnDisable()
        {
            groundMotor?.ResetMotion();
            ResetAttackState();
            if (projectilePool == null) return;
            foreach (var projectile in projectilePool)
                if (projectile != null) projectile.gameObject.SetActive(false);
        }

        private void Update()
        {
            UpdateMuzzleFacing();
            if (!CanAct())
            {
                ResetAttackState();
                return;
            }

            if (Time.time < phaseEndsAt) return;

            switch (CurrentPhase)
            {
                case TutorialRangedEnemyPhase.Ready:
                    if (CanShoot()) BeginTelegraph();
                    break;
                case TutorialRangedEnemyPhase.Telegraph:
                    FireProjectile();
                    break;
                case TutorialRangedEnemyPhase.Fire:
                    BeginRecovery();
                    break;
                case TutorialRangedEnemyPhase.Recovery:
                    SetPhase(TutorialRangedEnemyPhase.Ready, 0f);
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (!CanAct() || CurrentPhase == TutorialRangedEnemyPhase.Telegraph ||
                CurrentPhase == TutorialRangedEnemyPhase.Fire)
            {
                groundMotor?.StopHorizontal();
                return;
            }

            var deltaX = target.position.x - transform.position.x;
            var distance = Mathf.Abs(deltaX);
            var direction = 0f;
            var requestedSpeed = 0f;
            if (distance < retreatDistance)
            {
                direction = deltaX >= 0f ? -1f : 1f;
                requestedSpeed = Mathf.Min(moveSpeed, (retreatDistance - distance) / Time.fixedDeltaTime);
            }
            else if (distance > preferredDistance)
            {
                direction = deltaX >= 0f ? 1f : -1f;
                requestedSpeed = Mathf.Min(moveSpeed, (distance - preferredDistance) / Time.fixedDeltaTime);
            }

            if (Mathf.Approximately(direction, 0f) || requestedSpeed <= 0f)
            {
                groundMotor.StopHorizontal();
                return;
            }
            groundMotor.TrySetHorizontalSpeed(direction * requestedSpeed);
        }

        private void LateUpdate()
        {
            if (CurrentPhase != TutorialRangedEnemyPhase.Telegraph || warningVisualSlot == null) return;
            var pulse = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 5f) + 1f) * 0.5f;
            warningVisualSlot.transform.localScale = warningBaseScale * (1f + pulse * 0.12f);
        }

        private bool CanAct()
        {
            return actor != null && actor.Runtime != null && actor.Runtime.IsAlive &&
                   actor.Runtime.State != CombatState.Hit && target != null;
        }

        private bool CanShoot()
        {
            var distance = Mathf.Abs(target.position.x - transform.position.x);
            return distance <= maximumAttackDistance && HasClearLineOfSight();
        }

        private void BeginTelegraph()
        {
            warningVisualSlot.SetActive(true);
            SetPhase(TutorialRangedEnemyPhase.Telegraph, telegraphSeconds);
        }

        private void FireProjectile()
        {
            warningVisualSlot.SetActive(false);
            var projectile = Array.Find(projectilePool, item => item != null && !item.gameObject.activeSelf);
            if (projectile != null)
            {
                var direction = target.position.x >= transform.position.x ? Vector2.right : Vector2.left;
                projectile.Launch(
                    actor,
                    muzzleAnchor.position,
                    direction,
                    projectileSpeed,
                    projectileLifetime,
                    damage,
                    attackId);
                ShotsFired++;
            }
            SetPhase(TutorialRangedEnemyPhase.Fire, 0.05f);
        }

        private void BeginRecovery()
        {
            SetPhase(TutorialRangedEnemyPhase.Recovery, recoverySeconds);
        }

        private void ResetAttackState()
        {
            if (warningVisualSlot != null)
            {
                warningVisualSlot.transform.localScale = warningBaseScale == Vector3.zero
                    ? warningVisualSlot.transform.localScale
                    : warningBaseScale;
                warningVisualSlot.SetActive(false);
            }
            CurrentPhase = TutorialRangedEnemyPhase.Ready;
            phaseEndsAt = 0f;
        }

        private void UpdateMuzzleFacing()
        {
            if (muzzleAnchor == null || target == null) return;
            var local = muzzleAnchor.localPosition;
            local.x = (target.position.x >= transform.position.x ? 1f : -1f) * muzzleX;
            muzzleAnchor.localPosition = local;
        }

        private bool HasClearLineOfSight()
        {
            var origin = (Vector2)muzzleAnchor.position;
            var targetCollider = target.GetComponentInChildren<Collider2D>();
            var destination = targetCollider != null ? (Vector2)targetCollider.bounds.center : (Vector2)target.position;
            var delta = destination - origin;
            var distance = delta.magnitude;
            if (distance <= 0.01f) return true;

            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = Physics2D.AllLayers,
                useTriggers = false
            };
            var count = Physics2D.Raycast(origin, delta / distance, filter, sightHits, distance);
            for (var index = 0; index < count; index++)
            {
                var hit = sightHits[index];
                if (hit.collider == null || hit.collider == bodyCollider ||
                    hit.collider.transform.IsChildOf(transform) ||
                    hit.collider.transform == target || hit.collider.transform.IsChildOf(target))
                    continue;
                return false;
            }
            return true;
        }

        private void ApplyWarningColor()
        {
            if (warningRenderer == null) return;
            warningRenderer.GetPropertyBlock(warningProperties);
            warningProperties.SetColor("_Color", warningColor);
            warningProperties.SetColor("_BaseColor", warningColor);
            warningRenderer.SetPropertyBlock(warningProperties);
        }

        private void SetPhase(TutorialRangedEnemyPhase phase, float duration)
        {
            CurrentPhase = phase;
            phaseEndsAt = Time.time + Mathf.Max(0f, duration);
            PhaseChanged?.Invoke(phase);
        }
    }
}
