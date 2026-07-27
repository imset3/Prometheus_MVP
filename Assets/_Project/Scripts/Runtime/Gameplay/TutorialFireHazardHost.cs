using System.Collections;
using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Repeats warning, burst, and rest phases. Only the burst phase damages
    /// the player through the shared combat system.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialFireHazardHost : MonoBehaviour
    {
        [SerializeField] private CombatSystemHost combatSystemHost;
        [SerializeField] private CombatActorHost playerActor;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private Transform player;
        [SerializeField] private Renderer sourceRenderer;
        [SerializeField] private string hazardId = "G-FIRE-01";
        [SerializeField, Range(0.01f, 1f)] private float damageFraction = 0.1f;
        [SerializeField, Min(0f)] private float warningDuration = 0.65f;
        [SerializeField, Min(0.05f)] private float burstDuration = 1f;
        [SerializeField, Min(0.05f)] private float restDuration = 1.2f;
        [SerializeField, Min(0f)] private float horizontalKnockback = 4.5f;
        [SerializeField, Min(0f)] private float verticalKnockback = 5f;

        private Coroutine cycleRoutine;

        public bool HasValidSetup => combatSystemHost != null && playerActor != null &&
                                     playerBody != null && player != null &&
                                     sourceRenderer != null && !string.IsNullOrWhiteSpace(hazardId);
        public bool IsBurstActive { get; private set; }
        public float DamageFraction => damageFraction;

        private void Awake()
        {
            var trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.isTrigger = true;
            if (HasValidSetup && combatSystemHost.Initialize()) return;

            Debug.LogError(
                "TutorialFireHazardHost requires combat, player, body, renderer, and hazard id references.",
                this);
            enabled = false;
        }

        private void OnEnable()
        {
            if (!HasValidSetup) return;
            combatSystemHost.Events?.Subscribe<PlayerRespawned>(HandlePlayerRespawned);
            RestartCycle();
        }

        private void OnDisable()
        {
            combatSystemHost?.Events?.Unsubscribe<PlayerRespawned>(HandlePlayerRespawned);
            if (cycleRoutine != null) StopCoroutine(cycleRoutine);
            cycleRoutine = null;
            IsBurstActive = false;
            if (sourceRenderer != null) sourceRenderer.enabled = true;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryDamage(other);
        private void OnTriggerStay2D(Collider2D other) => TryDamage(other);

        private void TryDamage(Collider2D other)
        {
            if (!IsBurstActive || !IsPlayer(other) || playerActor.Runtime == null) return;
            var damage = TutorialEnvironmentHazardPolicy.ResolveFractionalDamage(
                playerActor.Runtime.MaxHealth,
                damageFraction);
            if (!playerActor.CombatSystem.TryApplyDamage(
                    playerActor.ActorId,
                    new DamagePacket("ENVIRONMENT", hazardId, damage)))
                return;

            var direction = Mathf.Approximately(player.position.x, transform.position.x)
                ? 1f
                : Mathf.Sign(player.position.x - transform.position.x);
            playerBody.linearVelocity = new Vector2(
                direction * horizontalKnockback,
                verticalKnockback);
        }

        private IEnumerator Cycle()
        {
            while (true)
            {
                IsBurstActive = false;
                var warningElapsed = 0f;
                while (warningElapsed < warningDuration)
                {
                    warningElapsed += Time.deltaTime;
                    sourceRenderer.enabled =
                        Mathf.FloorToInt(warningElapsed / 0.12f) % 2 == 0;
                    yield return null;
                }

                IsBurstActive = true;
                sourceRenderer.enabled = true;
                yield return new WaitForSeconds(burstDuration);

                IsBurstActive = false;
                sourceRenderer.enabled = false;
                yield return new WaitForSeconds(restDuration);
            }
        }

        private void HandlePlayerRespawned(PlayerRespawned message)
        {
            if (message.PlayerId == playerActor.ActorId) RestartCycle();
        }

        private void RestartCycle()
        {
            if (!isActiveAndEnabled) return;
            if (cycleRoutine != null) StopCoroutine(cycleRoutine);
            cycleRoutine = StartCoroutine(Cycle());
        }

        private bool IsPlayer(Collider2D other)
        {
            return other != null && (other.transform == player || other.transform.IsChildOf(player));
        }
    }
}
