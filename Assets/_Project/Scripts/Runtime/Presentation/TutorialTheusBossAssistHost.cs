using System.Collections;
using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>Boss-only Theus support: one revive and a full heal at phase two.</summary>
    public sealed class TutorialTheusBossAssistHost : MonoBehaviour
    {
        [SerializeField] private CombatActorHost playerActor;
        [SerializeField] private TutorialBossArenaHost arenaHost;
        [SerializeField] private HelteBossPatternHost heltePatternHost;
        [SerializeField] private TutorialRestartHost restartHost;
        [SerializeField, Range(0.1f, 1f)] private float reviveHealthRatio = 0.5f;
        [SerializeField, Min(0f)] private float reviveInvulnerabilitySeconds = 1.5f;
        [SerializeField] private GameObject reviveVfx;
        [SerializeField] private GameObject phaseHealVfx;

        private bool reviveConsumed;

        public bool HasValidSetup => playerActor != null && arenaHost != null && heltePatternHost != null && restartHost != null;
        public bool ReviveConsumed => reviveConsumed;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialTheusBossAssistHost requires player, arena, Helte FSM, and restart references.", this);
                enabled = false;
                return;
            }
            SetEffect(reviveVfx, false);
            SetEffect(phaseHealVfx, false);
        }

        private void OnEnable()
        {
            if (!HasValidSetup) return;
            restartHost.SetDeathInterceptor(TryInterceptDeath);
            heltePatternHost.StateChanged += HandleHelteStateChanged;
        }

        private void OnDisable()
        {
            restartHost?.ClearDeathInterceptor(TryInterceptDeath);
            if (heltePatternHost != null) heltePatternHost.StateChanged -= HandleHelteStateChanged;
        }

        private void Update()
        {
            if (arenaHost != null && !arenaHost.FightStarted) reviveConsumed = false;
        }

        private bool TryInterceptDeath(PlayerDead message)
        {
            if (reviveConsumed || !arenaHost.CombatActive || message.PlayerId != playerActor.ActorId) return false;
            var health = Mathf.CeilToInt(playerActor.Runtime.MaxHealth * reviveHealthRatio);
            if (!playerActor.Revive(health, reviveInvulnerabilitySeconds)) return false;
            reviveConsumed = true;
            PulseEffect(reviveVfx);
            playerActor.Events?.Publish(new PlayerRespawned(playerActor.ActorId));
            return true;
        }

        private void HandleHelteStateChanged(HelteCombatState state)
        {
            if (state != HelteCombatState.PhaseTransition || playerActor.Runtime?.IsAlive != true) return;
            playerActor.RestoreFullHealth();
            PulseEffect(phaseHealVfx);
        }

        private void PulseEffect(GameObject effect)
        {
            if (effect == null) return;
            effect.transform.position = playerActor.transform.position + Vector3.up * 0.8f;
            effect.SetActive(false);
            effect.SetActive(true);
            StartCoroutine(HideEffectAfterDelay(effect));
        }

        private static IEnumerator HideEffectAfterDelay(GameObject effect)
        {
            yield return new WaitForSeconds(0.7f);
            if (effect != null) effect.SetActive(false);
        }

        private static void SetEffect(GameObject effect, bool active)
        {
            if (effect != null) effect.SetActive(active);
        }
    }
}
