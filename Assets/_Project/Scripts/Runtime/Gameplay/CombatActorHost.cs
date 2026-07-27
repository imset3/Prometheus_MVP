using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class CombatActorHost : MonoBehaviour
    {
        [SerializeField] private CombatSystemHost combatSystemHost;
        [SerializeField] private string actorId;
        [SerializeField] private CombatActorKind kind;
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private string stageId = "TUTORIAL";
        [SerializeField] private string unlockTreeId;
        [SerializeField, Min(0f)] private float hitRecoverySeconds = 0.15f;

        private float hitRecoveryEndsAt;
        private float dashInvulnerabilityEndsAt;
        private bool dashInvulnerabilityActive;

        public string ActorId => actorId;
        public CombatActorKind Kind => kind;
        public ActorRuntimeState Runtime { get; private set; }
        public CombatSystem CombatSystem => combatSystemHost != null ? combatSystemHost.System : null;
        public GameEventBus Events => combatSystemHost != null ? combatSystemHost.Events : null;
        public bool IsInHitRecovery => Runtime != null && Runtime.State == CombatState.Hit &&
                                       Time.time < hitRecoveryEndsAt;
        public bool IsDashInvulnerable => Runtime != null && dashInvulnerabilityActive &&
                                          Time.time < dashInvulnerabilityEndsAt;
        public float HitRecoverySeconds => hitRecoverySeconds;

        public void ResetRuntime()
        {
            if (Runtime == null) return;
            Runtime.CurrentHealth = Runtime.MaxHealth;
            Runtime.State = CombatState.Idle;
            Runtime.IsInvincible = false;
            hitRecoveryEndsAt = 0f;
            dashInvulnerabilityEndsAt = 0f;
            dashInvulnerabilityActive = false;
            if (Runtime is PlayerRuntimeState player) player.HitCount = 0;
        }

        public void BeginDashInvulnerability(float durationSeconds)
        {
            if (Runtime == null || !Runtime.IsAlive || durationSeconds <= 0f) return;
            dashInvulnerabilityEndsAt = Time.time + durationSeconds;
            dashInvulnerabilityActive = true;
            Runtime.IsInvincible = true;
        }

        public void EndDashInvulnerability()
        {
            if (Runtime == null) return;
            dashInvulnerabilityActive = false;
            dashInvulnerabilityEndsAt = 0f;
            Runtime.IsInvincible = IsHitRecoveryActive();
        }

        private void Awake()
        {
            if (combatSystemHost == null || string.IsNullOrWhiteSpace(actorId) || maxHealth <= 0)
            {
                Debug.LogError("CombatActorHost requires CombatSystemHost, actor id, and a positive max health.", this);
                enabled = false;
                return;
            }

            if (!combatSystemHost.Initialize())
            {
                enabled = false;
                return;
            }

            Runtime = kind == CombatActorKind.Player
                ? new PlayerRuntimeState(actorId, maxHealth)
                : new ActorRuntimeState(actorId, kind, maxHealth);
            Runtime.StageId = stageId;
            Runtime.UnlockTreeId = unlockTreeId;
            combatSystemHost.System.Register(Runtime);
        }

        private void OnEnable()
        {
            if (combatSystemHost == null || !combatSystemHost.Initialize()) return;
            combatSystemHost.Events?.Subscribe<HitConfirmed>(HandleHitConfirmed);
        }

        private void OnDisable()
        {
            combatSystemHost?.Events?.Unsubscribe<HitConfirmed>(HandleHitConfirmed);
            EndDashInvulnerability();
        }

        private void Update()
        {
            if (Runtime == null) return;

            var now = Time.time;
            if (dashInvulnerabilityActive && now >= dashInvulnerabilityEndsAt)
            {
                dashInvulnerabilityActive = false;
                dashInvulnerabilityEndsAt = 0f;
            }

            if (Runtime.State == CombatState.Hit && now >= hitRecoveryEndsAt)
                Runtime.State = CombatState.Idle;

            Runtime.IsInvincible = dashInvulnerabilityActive || IsHitRecoveryActive();
        }

        private void HandleHitConfirmed(HitConfirmed message)
        {
            if (Runtime == null || !Runtime.IsAlive || message.TargetId != actorId) return;
            hitRecoveryEndsAt = Time.time + hitRecoverySeconds;
            Runtime.IsInvincible = true;
        }

        private bool IsHitRecoveryActive()
        {
            return Runtime != null && Runtime.State == CombatState.Hit &&
                   Time.time < hitRecoveryEndsAt;
        }
    }
}
