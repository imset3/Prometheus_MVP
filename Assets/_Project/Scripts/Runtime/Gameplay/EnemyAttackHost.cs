using UnityEngine;

namespace Narthex.Gameplay
{
    public enum EnemyAttackPhase
    {
        Ready,
        Telegraph,
        Active,
        Recovery
    }

    public sealed class EnemyAttackHost : MonoBehaviour
    {
        [SerializeField] private CombatActorHost sourceActor;
        [SerializeField] private Collider2D attackHitbox;
        [SerializeField] private GameObject warningVisualSlot;
        [SerializeField] private Renderer warningRenderer;
        [SerializeField] private Color warningColor = new Color(1f, 0.28f, 0.08f, 0.9f);
        [SerializeField, Range(0f, 1f)] private float warningFlashIntensity = 0.45f;
        [SerializeField] private LayerMask targetLayers = -1;
        [SerializeField] private string attackId = "ENEMY-TUTO-BASIC";
        [SerializeField] private int damage = 15;
        [SerializeField, Min(0.05f)] private float telegraphSeconds = 0.28f;
        [SerializeField, Min(0.05f)] private float intervalSeconds = 0.8f;
        [SerializeField, Min(0.01f)] private float activeSeconds = 0.1f;

        private readonly Collider2D[] results = new Collider2D[8];
        private float phaseEndsAt;
        private Vector3 warningBaseScale;
        private MaterialPropertyBlock warningProperties;

        public bool HasReadableSetup => sourceActor != null && attackHitbox != null && warningVisualSlot != null &&
                                        telegraphSeconds > activeSeconds;
        public EnemyAttackPhase CurrentPhase { get; private set; } = EnemyAttackPhase.Ready;
        public event System.Action<EnemyAttackPhase> PhaseChanged;

        private void Awake()
        {
            if (sourceActor == null || attackHitbox == null)
            {
                Debug.LogError("EnemyAttackHost requires pre-placed source actor and attack hitbox references.", this);
                enabled = false;
                return;
            }

            if (warningRenderer == null && warningVisualSlot != null)
                warningRenderer = warningVisualSlot.GetComponentInChildren<Renderer>(true);
            if (warningVisualSlot != null) warningBaseScale = warningVisualSlot.transform.localScale;
            warningProperties = new MaterialPropertyBlock();
            ApplyWarningColor(warningColor);
            ResetAttackState();
        }

        private void OnDisable() => ResetAttackState();

        private void Update()
        {
            if (sourceActor.Runtime == null || sourceActor.CombatSystem == null || !sourceActor.Runtime.IsAlive)
            {
                ResetAttackState();
                return;
            }

            if (Time.time < phaseEndsAt) return;

            switch (CurrentPhase)
            {
                case EnemyAttackPhase.Ready:
                    BeginTelegraph();
                    break;
                case EnemyAttackPhase.Telegraph:
                    ActivateHitbox();
                    break;
                case EnemyAttackPhase.Active:
                    BeginRecovery();
                    break;
                case EnemyAttackPhase.Recovery:
                    SetPhase(EnemyAttackPhase.Ready, 0f);
                    break;
            }
        }

        private void LateUpdate()
        {
            if (CurrentPhase != EnemyAttackPhase.Telegraph || warningVisualSlot == null) return;
            var pulse = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 4f) + 1f) * 0.5f;
            warningVisualSlot.transform.localScale = warningBaseScale *
                                                     (1f + pulse * 0.12f * warningFlashIntensity);
        }

        public void SetWarningFlashIntensity(float intensity)
        {
            warningFlashIntensity = Mathf.Clamp01(intensity);
        }

        private void BeginTelegraph()
        {
            if (warningVisualSlot != null) warningVisualSlot.SetActive(true);
            SetPhase(EnemyAttackPhase.Telegraph, telegraphSeconds);
        }

        private void ActivateHitbox()
        {
            if (warningVisualSlot != null) warningVisualSlot.SetActive(false);
            attackHitbox.enabled = true;
            Physics2D.SyncTransforms();
            TryDamagePlayer();
            SetPhase(EnemyAttackPhase.Active, activeSeconds);
        }

        private void BeginRecovery()
        {
            attackHitbox.enabled = false;
            SetPhase(EnemyAttackPhase.Recovery, intervalSeconds);
        }

        private void TryDamagePlayer()
        {
            var filter = ContactFilter2D.noFilter;
            filter.SetLayerMask(targetLayers);
            filter.useTriggers = true;
            var count = attackHitbox.Overlap(filter, results);
            for (var index = 0; index < count; index++)
            {
                var target = results[index].GetComponentInParent<CombatActorHost>();
                if (target == null || target.Kind != CombatActorKind.Player) continue;

                sourceActor.CombatSystem.TryApplyDamage(
                    target.ActorId,
                    new DamagePacket(sourceActor.ActorId, attackId, damage));
                break;
            }
        }

        private void ResetAttackState()
        {
            if (attackHitbox != null) attackHitbox.enabled = false;
            if (warningVisualSlot != null)
            {
                warningVisualSlot.transform.localScale = warningBaseScale;
                warningVisualSlot.SetActive(false);
            }
            CurrentPhase = EnemyAttackPhase.Ready;
            phaseEndsAt = 0f;
        }


        private void ApplyWarningColor(Color color)
        {
            if (warningRenderer == null) return;
            warningRenderer.GetPropertyBlock(warningProperties);
            warningProperties.SetColor("_Color", color);
            warningProperties.SetColor("_BaseColor", color);
            warningRenderer.SetPropertyBlock(warningProperties);
        }

        private void SetPhase(EnemyAttackPhase phase, float duration)
        {
            CurrentPhase = phase;
            phaseEndsAt = Time.time + duration;
            PhaseChanged?.Invoke(phase);
        }
    }
}
