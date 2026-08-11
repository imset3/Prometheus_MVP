using System;
using System.Collections;
using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Boss-only cooldown skill that reuses Prome's single attack sequence as a committed four-hit rush.
    /// The first miss ends the sequence, preserving counterplay against Helte's fake openings.
    /// </summary>
    public sealed class PromeBossSkillHost : MonoBehaviour
    {
        [SerializeField] private PlayerInputHost inputHost;
        [SerializeField] private CombatActorHost playerActor;
        [SerializeField] private CombatActorHost bossActor;
        [SerializeField] private TutorialBossArenaHost arenaHost;
        [SerializeField] private MeleeAttackHost meleeAttack;
        [SerializeField] private PlayerRangedAttackHost rangedAttack;
        [SerializeField] private CharacterPngAnimationBridge animationBridge;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private GameObject[] strikeVfx = Array.Empty<GameObject>();
        [SerializeField] private CameraFollowHost cameraFollowHost;
        [SerializeField] private AudioSource finalImpactSource;
        [SerializeField] private AudioClip finalImpactClip;
        [SerializeField] private bool suppressRangedDuringCombat = true;
        [SerializeField, Min(0.1f)] private float cooldownSeconds = 10f;
        [SerializeField, Min(0.1f)] private float maximumStartRange = 4.5f;
        [SerializeField, Min(0f)] private float preferredStrikeRange = 1.35f;
        [SerializeField, Min(0f)] private float lungePerStrike = 0.42f;
        [SerializeField, Min(0.05f)] private float impactDelay = 0.12f;
        [SerializeField, Min(0.05f)] private float strikeInterval = 0.34f;
        [SerializeField, Min(0.05f)] private float finalRecovery = 0.55f;
        [SerializeField] private int[] strikeDamage = { 35, 40, 45, 100 };
        [SerializeField] private float[] playbackSpeed = { 0.9f, 0.96f, 1.02f, 0.84f };
        [SerializeField, Range(0.01f, 1f)] private float finalHitstopTimeScale = 0.08f;
        [SerializeField, Min(0f)] private float finalHitstopSeconds = 0.075f;
        [SerializeField, Range(0f, 1f)] private float finalImpactVolume = 0.95f;
        [SerializeField, Min(0f)] private float finalShakeAmplitude = 0.18f;
        [SerializeField, Min(0f)] private float finalShakeDuration = 0.16f;

        private Coroutine skillRoutine;
        private Coroutine hitstopRoutine;
        private float cooldownEndsAt;
        private float skillFacingDirection = 1f;
        private float hitstopPreviousTimeScale = 1f;
        private float appliedHitstopTimeScale = 1f;

        public bool HasValidSetup => inputHost != null && playerActor != null && bossActor != null &&
                                     arenaHost != null && meleeAttack != null && animationBridge != null &&
                                     strikeDamage != null && strikeDamage.Length == 4 &&
                                     playbackSpeed != null && playbackSpeed.Length == 4;
        public float CooldownRemaining => Mathf.Max(0f, cooldownEndsAt - Time.time);
        public float CooldownDuration => cooldownSeconds;
        public bool IsCombatActive => arenaHost != null && arenaHost.CombatActive;
        public bool IsEncounterActive => arenaHost != null && arenaHost.FightStarted && !arenaHost.FightCompleted;
        public bool IsExecuting => skillRoutine != null;
        public bool IsReady => CanActivate(arenaHost != null && arenaHost.CombatActive,
            playerActor?.Runtime?.IsAlive == true, bossActor?.Runtime?.IsAlive == true,
            CooldownRemaining, IsExecuting);
        public event Action SkillStarted;
        public event Action<int, bool> StrikeResolved;
        public event Action SkillFinished;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("PromeBossSkillHost requires boss combat, input, actor, melee, and animation references.", this);
                enabled = false;
                return;
            }
            SetVfxActive(false);
        }

        private void OnEnable()
        {
            if (!HasValidSetup) return;
            inputHost.BossSkillRequested += TryActivate;
        }

        private void OnDisable()
        {
            if (inputHost != null) inputHost.BossSkillRequested -= TryActivate;
            if (skillRoutine != null) StopCoroutine(skillRoutine);
            skillRoutine = null;
            StopOwnedHitstop();
            rangedAttack?.SetBossSkillOverride(false);
            SetVfxActive(false);
        }

        private void Update()
        {
            rangedAttack?.SetBossSkillOverride(suppressRangedDuringCombat && arenaHost != null && arenaHost.CombatActive);
        }

        public void TryActivate()
        {
            var bossDelta = bossActor.transform.position.x - playerActor.transform.position.x;
            var facing = ResolveSkillFacing(
                inputHost == null ? 0f : inputHost.AimDirectionX,
                animationBridge.FacingDirection);
            if (!IsReady || !IsBossInForwardRange(bossDelta, facing, maximumStartRange))
                return;
            skillFacingDirection = facing;
            skillRoutine = StartCoroutine(RunSkill());
        }

        private IEnumerator RunSkill()
        {
            cooldownEndsAt = Time.time + cooldownSeconds;
            var totalLock = (impactDelay + strikeInterval) * 4f + finalRecovery + 0.2f;
            meleeAttack.LockExternalAttack(totalLock);
            rangedAttack?.SetBossSkillOverride(true);
            SkillStarted?.Invoke();

            for (var index = 0; index < 4; index++)
            {
                if (!arenaHost.CombatActive || playerActor.Runtime?.IsAlive != true || bossActor.Runtime?.IsAlive != true)
                    break;

                LungeTowardBoss();
                var lockSeconds = index == 3 ? finalRecovery : strikeInterval;
                animationBridge.PresentBossSkillStrike(playbackSpeed[index], lockSeconds, skillFacingDirection);
                ShowStrikeVfx(index);
                yield return new WaitForSeconds(impactDelay);
                var hit = playerActor.CombatSystem?.TryApplyDamage(
                    bossActor.ActorId,
                    new DamagePacket(playerActor.ActorId, $"PROME-BOSS-SKILL-{index + 1:00}", strikeDamage[index])) == true;
                if (hit && index == 3) PlayFinalImpactFeedback();
                StrikeResolved?.Invoke(index, hit);
                if (!hit) break;
                yield return new WaitForSeconds(index == 3 ? finalRecovery : strikeInterval);
            }

            SetVfxActive(false);
            skillRoutine = null;
            SkillFinished?.Invoke();
        }

        private void LungeTowardBoss()
        {
            var delta = bossActor.transform.position.x - playerActor.transform.position.x;
            var direction = Mathf.Sign(delta);
            var excess = Mathf.Max(0f, Mathf.Abs(delta) - preferredStrikeRange);
            var distance = Mathf.Min(lungePerStrike, excess);
            var destination = (Vector2)playerActor.transform.position + Vector2.right * (direction * distance);
            if (playerBody != null)
            {
                playerBody.linearVelocity = new Vector2(0f, playerBody.linearVelocity.y);
                playerBody.position = destination;
            }
            else playerActor.transform.position = destination;
            Physics2D.SyncTransforms();
        }

        private void ShowStrikeVfx(int index)
        {
            if (strikeVfx == null || index < 0 || index >= strikeVfx.Length || strikeVfx[index] == null) return;
            SetVfxActive(false);
            var effect = strikeVfx[index];
            var verticalOffset = index switch { 0 => -0.08f, 1 => 0.08f, 2 => -0.03f, _ => 0.04f };
            var rotation = (index switch { 0 => -18f, 1 => 16f, 2 => -8f, _ => 24f }) * skillFacingDirection;
            effect.transform.position = playerActor.transform.position +
                                        Vector3.right * (skillFacingDirection * 1.4f) +
                                        Vector3.up * (0.72f + verticalOffset);
            effect.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            var renderer = effect.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null) renderer.flipX = ShouldFlipStrikeSprite(skillFacingDirection);
            effect.SetActive(true);
        }

        private void PlayFinalImpactFeedback()
        {
            cameraFollowHost?.RequestShake(finalShakeAmplitude, finalShakeDuration);
            if (finalImpactSource != null && finalImpactClip != null)
                finalImpactSource.PlayOneShot(finalImpactClip, finalImpactVolume);
            if (finalHitstopSeconds <= 0f || Time.timeScale <= 0f) return;
            if (hitstopRoutine != null) StopCoroutine(hitstopRoutine);
            hitstopRoutine = StartCoroutine(RunHitstop());
        }

        private IEnumerator RunHitstop()
        {
            hitstopPreviousTimeScale = Time.timeScale;
            appliedHitstopTimeScale = Mathf.Min(hitstopPreviousTimeScale, finalHitstopTimeScale);
            Time.timeScale = appliedHitstopTimeScale;
            yield return new WaitForSecondsRealtime(finalHitstopSeconds);
            if (Mathf.Approximately(Time.timeScale, appliedHitstopTimeScale))
                Time.timeScale = hitstopPreviousTimeScale;
            hitstopRoutine = null;
        }

        private void StopOwnedHitstop()
        {
            if (hitstopRoutine == null) return;
            StopCoroutine(hitstopRoutine);
            if (Mathf.Approximately(Time.timeScale, appliedHitstopTimeScale))
                Time.timeScale = hitstopPreviousTimeScale;
            hitstopRoutine = null;
        }

        private void SetVfxActive(bool active)
        {
            if (strikeVfx == null) return;
            foreach (var effect in strikeVfx)
                if (effect != null) effect.SetActive(active);
        }

        public static bool CanActivate(bool combatActive, bool playerAlive, bool bossAlive,
            float cooldownRemaining, bool executing) =>
            combatActive && playerAlive && bossAlive && !executing && cooldownRemaining <= 0f;

        public static int ResolveStrikeDamage(int[] values, int strikeIndex) =>
            values == null || strikeIndex < 0 || strikeIndex >= values.Length ? 0 : Mathf.Max(0, values[strikeIndex]);

        public static float ResolveSkillFacing(float inputFacing, float visualFacing)
        {
            if (!Mathf.Approximately(inputFacing, 0f)) return Mathf.Sign(inputFacing);
            return Mathf.Approximately(visualFacing, 0f) ? 1f : Mathf.Sign(visualFacing);
        }

        public static bool IsBossInForwardRange(float bossDelta, float facing, float maximumRange) =>
            Mathf.Abs(bossDelta) <= Mathf.Max(0f, maximumRange) &&
            bossDelta * ResolveSkillFacing(facing, 1f) > 0f;

        // The generated slash arc points right in its source texture.
        public static bool ShouldFlipStrikeSprite(float facing) => facing < 0f;
    }
}
