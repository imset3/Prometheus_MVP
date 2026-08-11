using System;
using System.Collections.Generic;
using Narthex.Content;
using Narthex.Core;
using Narthex.Gameplay;
using Narthex.Save;
using UnityEngine;

namespace Narthex.Presentation
{
    public enum TutorialSfxCue
    {
        None,
        BossPhaseTwo,
        BossFinalRush,
        BossMercy,
        BossBasicWindup,
        BossSlash,
        BossBlinkOut,
        BossBlinkIn,
        BossDashTelegraph,
        BossDash,
        BossCrossTelegraph,
        BossCrossSlash,
        BossSwordFocus,
        BossSwordFire,
        BossCounterTelegraph,
        BossCounter
    }

    /// <summary>
    /// Event-driven tutorial SFX router. All AudioSources and clips are pre-placed;
    /// gameplay remains authoritative and this component only presents its events.
    /// </summary>
    public sealed class TutorialSfxDirector : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private SaveSystemHost saveSystemHost;
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private CombatActorHost playerActor;
        [SerializeField] private CombatActorHost bossActor;
        [SerializeField] private MeleeAttackHost playerMeleeAttack;
        [SerializeField] private PlayerRangedAttackHost playerRangedAttack;
        [SerializeField] private TutorialBossArenaHost bossArenaHost;
        [SerializeField] private HelteBossPatternHost heltePatternHost;
        [SerializeField] private EnemyAttackHost[] enemyMeleeAttacks = Array.Empty<EnemyAttackHost>();
        [SerializeField] private TutorialRangedEnemyHost[] enemyRangedAttacks = Array.Empty<TutorialRangedEnemyHost>();
        [SerializeField] private PromeBossSkillHost promeBossSkillHost;
        [SerializeField] private TutorialTheusRangedSupportHost theusRangedSupportHost;

        [Header("Pre-placed sources")]
        [SerializeField] private AudioSource uiSource;
        [SerializeField] private AudioSource playerSource;
        [SerializeField] private AudioSource enemySource;
        [SerializeField] private AudioSource bossSource;
        [SerializeField] private AudioSource worldSource;

        [Header("Player and impact")]
        [SerializeField] private AudioClip playerMeleeSwingA;
        [SerializeField] private AudioClip playerMeleeSwingB;
        [SerializeField] private AudioClip playerRangedFire;
        [SerializeField] private AudioClip impactLightA;
        [SerializeField] private AudioClip impactLightB;
        [SerializeField] private AudioClip impactHeavy;
        [SerializeField] private AudioClip playerHit;
        [SerializeField] private AudioClip playerDeath;
        [SerializeField] private AudioClip enemyDeath;
        [SerializeField] private AudioClip playerJump;
        [SerializeField] private AudioClip playerDash;
        [SerializeField] private AudioClip focusedVolleyStart;
        [SerializeField] private AudioClip focusedVolleyShot;
        [SerializeField] private AudioClip fourSlashStart;
        [SerializeField] private AudioClip fourSlashHit;

        [Header("Enemy")]
        [SerializeField] private AudioClip enemyMeleeTelegraph;
        [SerializeField] private AudioClip enemyMeleeAttack;
        [SerializeField] private AudioClip enemyRangedTelegraph;
        [SerializeField] private AudioClip enemyRangedFire;

        [Header("Helte")]
        [SerializeField] private AudioClip helteIntroWarning;
        [SerializeField] private AudioClip heltePhaseTwo;
        [SerializeField] private AudioClip helteFinalRush;
        [SerializeField] private AudioClip helteBasicWindup;
        [SerializeField] private AudioClip helteSlash;
        [SerializeField] private AudioClip helteBlinkOut;
        [SerializeField] private AudioClip helteBlinkIn;
        [SerializeField] private AudioClip helteDashTelegraph;
        [SerializeField] private AudioClip helteDash;
        [SerializeField] private AudioClip helteCrossTelegraph;
        [SerializeField] private AudioClip helteCrossSlash;
        [SerializeField] private AudioClip helteSwordFocus;
        [SerializeField] private AudioClip helteSwordFire;
        [SerializeField] private AudioClip helteCounterTelegraph;
        [SerializeField] private AudioClip helteCounter;
        [SerializeField] private AudioClip helteMercy;
        [SerializeField] private AudioClip helteVictory;

        [Header("UI and flow")]
        [SerializeField] private AudioClip dialogueAdvance;
        [SerializeField] private AudioClip objectiveUpdated;
        [SerializeField] private AudioClip tutorialCompleted;
        [SerializeField] private AudioClip panelOpen;
        [SerializeField] private AudioClip itemPickup;
        [SerializeField] private AudioClip relayActivate;
        [SerializeField] private AudioClip gateOpen;
        [SerializeField] private AudioClip encounterStart;
        [SerializeField] private AudioClip encounterClear;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float outputLevel = 0.82f;
        [SerializeField, Range(0f, 1f)] private float uiLevel = 0.7f;
        [SerializeField, Range(0f, 1f)] private float playerLevel = 0.9f;
        [SerializeField, Range(0f, 1f)] private float enemyLevel = 0.78f;
        [SerializeField, Range(0f, 1f)] private float bossLevel = 0.92f;
        [SerializeField, Range(0f, 1f)] private float worldLevel = 0.75f;

        private bool alternateSwing;
        private bool alternateImpact;
        private bool bossIntroPresented;
        private Camera cachedWorldCamera;
        private readonly Dictionary<EnemyAttackHost, Action<EnemyAttackPhase>> meleePhaseHandlers = new();
        private readonly Dictionary<TutorialRangedEnemyHost, Action<TutorialRangedEnemyPhase>> rangedPhaseHandlers = new();

        public bool HasValidSetup => serviceRoot != null && playerInputHost != null && playerActor != null &&
                                     bossActor != null && playerMeleeAttack != null && playerRangedAttack != null &&
                                     bossArenaHost != null && heltePatternHost != null && uiSource != null &&
                                     playerSource != null && enemySource != null && bossSource != null && worldSource != null;
        public float EffectiveSfxVolume => ResolveSfxVolume();

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialSfxDirector requires pre-placed runtime references and AudioSources.", this);
                enabled = false;
                return;
            }

            ConfigureSource(uiSource, 96);
            ConfigureSource(playerSource, 80);
            ConfigureSource(enemySource, 112);
            ConfigureSource(bossSource, 72);
            ConfigureSource(worldSource, 104);
        }

        private void OnEnable()
        {
            if (!HasValidSetup) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<HitConfirmed>(HandleHitConfirmed);
            serviceRoot.Events.Subscribe<PlayerHit>(HandlePlayerHit);
            serviceRoot.Events.Subscribe<EnemyKilled>(HandleEnemyKilled);
            serviceRoot.Events.Subscribe<BossKilled>(HandleBossKilled);
            serviceRoot.Events.Subscribe<PlayerDead>(HandlePlayerDead);
            serviceRoot.Events.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            serviceRoot.Events.Subscribe<TutorialCompleted>(HandleTutorialCompleted);
            serviceRoot.Events.Subscribe<GameplaySignal>(HandleGameplaySignal);
            serviceRoot.Events.Subscribe<TowerActivated>(HandleTowerActivated);
            playerMeleeAttack.AttackStarted += HandlePlayerMeleeAttack;
            playerRangedAttack.RangedAttackStarted += HandlePlayerRangedAttack;
            playerInputHost.DialogueAdvanceRequested += HandleDialogueAdvance;
            playerInputHost.ModuleTreeRequested += HandlePanelRequested;
            playerInputHost.InventoryRequested += HandlePanelRequested;
            heltePatternHost.StateChanged += HandleHelteStateChanged;
            if (promeBossSkillHost != null)
            {
                promeBossSkillHost.SkillStarted += HandleFourSlashStarted;
                promeBossSkillHost.StrikeResolved += HandleFourSlashStrike;
            }
            if (theusRangedSupportHost != null)
            {
                theusRangedSupportHost.FocusedVolleyStarted += HandleFocusedVolleyStarted;
                theusRangedSupportHost.FocusedVolleyShot += HandleFocusedVolleyShot;
            }
            SubscribeEnemyHosts(true);
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<HitConfirmed>(HandleHitConfirmed);
            serviceRoot?.Events?.Unsubscribe<PlayerHit>(HandlePlayerHit);
            serviceRoot?.Events?.Unsubscribe<EnemyKilled>(HandleEnemyKilled);
            serviceRoot?.Events?.Unsubscribe<BossKilled>(HandleBossKilled);
            serviceRoot?.Events?.Unsubscribe<PlayerDead>(HandlePlayerDead);
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            serviceRoot?.Events?.Unsubscribe<TutorialCompleted>(HandleTutorialCompleted);
            serviceRoot?.Events?.Unsubscribe<GameplaySignal>(HandleGameplaySignal);
            serviceRoot?.Events?.Unsubscribe<TowerActivated>(HandleTowerActivated);
            if (playerMeleeAttack != null) playerMeleeAttack.AttackStarted -= HandlePlayerMeleeAttack;
            if (playerRangedAttack != null) playerRangedAttack.RangedAttackStarted -= HandlePlayerRangedAttack;
            if (playerInputHost != null)
            {
                playerInputHost.DialogueAdvanceRequested -= HandleDialogueAdvance;
                playerInputHost.ModuleTreeRequested -= HandlePanelRequested;
                playerInputHost.InventoryRequested -= HandlePanelRequested;
            }
            if (heltePatternHost != null) heltePatternHost.StateChanged -= HandleHelteStateChanged;
            if (promeBossSkillHost != null)
            {
                promeBossSkillHost.SkillStarted -= HandleFourSlashStarted;
                promeBossSkillHost.StrikeResolved -= HandleFourSlashStrike;
            }
            if (theusRangedSupportHost != null)
            {
                theusRangedSupportHost.FocusedVolleyStarted -= HandleFocusedVolleyStarted;
                theusRangedSupportHost.FocusedVolleyShot -= HandleFocusedVolleyShot;
            }
            SubscribeEnemyHosts(false);
        }

        private void Update()
        {
            if (bossArenaHost == null) return;
            if (!bossArenaHost.FightStarted)
            {
                bossIntroPresented = false;
                return;
            }
            if (bossIntroPresented || !bossArenaHost.EncounterPresentationActive) return;
            bossIntroPresented = true;
            Play(bossSource, helteIntroWarning, bossLevel);
        }

        private void SubscribeEnemyHosts(bool subscribe)
        {
            if (!subscribe)
            {
                foreach (var pair in meleePhaseHandlers)
                    if (pair.Key != null) pair.Key.PhaseChanged -= pair.Value;
                foreach (var pair in rangedPhaseHandlers)
                    if (pair.Key != null) pair.Key.PhaseChanged -= pair.Value;
                meleePhaseHandlers.Clear();
                rangedPhaseHandlers.Clear();
                return;
            }

            if (enemyMeleeAttacks != null)
            {
                foreach (var host in enemyMeleeAttacks)
                {
                    if (host == null || meleePhaseHandlers.ContainsKey(host)) continue;
                    Action<EnemyAttackPhase> handler = phase => HandleEnemyMeleePhase(host, phase);
                    meleePhaseHandlers.Add(host, handler);
                    host.PhaseChanged += handler;
                }
            }
            if (enemyRangedAttacks == null) return;
            foreach (var host in enemyRangedAttacks)
            {
                if (host == null || rangedPhaseHandlers.ContainsKey(host)) continue;
                Action<TutorialRangedEnemyPhase> handler = phase => HandleEnemyRangedPhase(host, phase);
                rangedPhaseHandlers.Add(host, handler);
                host.PhaseChanged += handler;
            }
        }

        private void HandlePlayerMeleeAttack()
        {
            alternateSwing = !alternateSwing;
            Play(playerSource, alternateSwing ? playerMeleeSwingA : playerMeleeSwingB, playerLevel);
        }

        private void HandlePlayerRangedAttack(Vector2 direction) => Play(playerSource, playerRangedFire, playerLevel);

        private void HandleFourSlashStarted() => Play(playerSource, fourSlashStart, playerLevel);

        private void HandleFourSlashStrike(int index, bool hit) =>
            Play(hit ? bossSource : playerSource, fourSlashHit, hit ? bossLevel : playerLevel * 0.78f);

        private void HandleFocusedVolleyStarted() => Play(playerSource, focusedVolleyStart, playerLevel);

        private void HandleFocusedVolleyShot(int index, CombatActorHost target) =>
            Play(playerSource, focusedVolleyShot, playerLevel * 0.88f);

        private void HandleHitConfirmed(HitConfirmed message)
        {
            if (message.TargetId == playerActor.ActorId) return;
            if (message.TargetId != bossActor.ActorId && !IsActorAudible(message.TargetId)) return;
            var heavy = message.TargetId == bossActor.ActorId || message.Damage >= 25;
            alternateImpact = !alternateImpact;
            var clip = heavy ? impactHeavy : alternateImpact ? impactLightA : impactLightB;
            Play(message.TargetId == bossActor.ActorId ? bossSource : enemySource, clip,
                message.TargetId == bossActor.ActorId ? bossLevel : enemyLevel);
        }

        private void HandlePlayerHit(PlayerHit message)
        {
            if (message.PlayerId == playerActor.ActorId) Play(playerSource, playerHit, playerLevel);
        }

        private void HandleEnemyKilled(EnemyKilled message)
        {
            if (IsActorAudible(message.EnemyId)) Play(enemySource, enemyDeath, enemyLevel);
        }

        private void HandleBossKilled(BossKilled message)
        {
            if (message.BossId == bossActor.ActorId) Play(bossSource, helteVictory, bossLevel);
        }

        private void HandlePlayerDead(PlayerDead message)
        {
            if (message.PlayerId == playerActor.ActorId) Play(playerSource, playerDeath, playerLevel);
        }

        private void HandleEnemyMeleePhase(EnemyAttackHost host, EnemyAttackPhase phase)
        {
            if (!IsAudibleEmitter(host != null ? host.transform : null)) return;
            if (phase == EnemyAttackPhase.Telegraph) Play(enemySource, enemyMeleeTelegraph, enemyLevel);
            else if (phase == EnemyAttackPhase.Active) Play(enemySource, enemyMeleeAttack, enemyLevel);
        }

        private void HandleEnemyRangedPhase(TutorialRangedEnemyHost host, TutorialRangedEnemyPhase phase)
        {
            if (!IsAudibleEmitter(host != null ? host.transform : null)) return;
            if (phase == TutorialRangedEnemyPhase.Telegraph) Play(enemySource, enemyRangedTelegraph, enemyLevel);
            else if (phase == TutorialRangedEnemyPhase.Fire) Play(enemySource, enemyRangedFire, enemyLevel);
        }

        private bool IsActorAudible(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId)) return false;
            if (enemyMeleeAttacks != null)
            {
                foreach (var host in enemyMeleeAttacks)
                {
                    if (host == null) continue;
                    var actor = host.GetComponent<CombatActorHost>();
                    if (actor != null && actor.ActorId == actorId) return IsAudibleEmitter(host.transform);
                }
            }
            if (enemyRangedAttacks == null) return false;
            foreach (var host in enemyRangedAttacks)
            {
                if (host == null) continue;
                var actor = host.GetComponent<CombatActorHost>();
                if (actor != null && actor.ActorId == actorId) return IsAudibleEmitter(host.transform);
            }
            return false;
        }

        private bool IsAudibleEmitter(Transform emitter)
        {
            if (emitter == null) return false;
            if (cachedWorldCamera == null) cachedWorldCamera = Camera.main;
            if (cachedWorldCamera == null) return false;
            var renderer = emitter.GetComponentInChildren<Renderer>(true);
            var worldPosition = renderer != null ? renderer.bounds.center : emitter.position;
            return IsViewportAudible(cachedWorldCamera.WorldToViewportPoint(worldPosition));
        }

        private void HandleHelteStateChanged(HelteCombatState state)
        {
            var cue = ResolveBossCue(state);
            Play(bossSource, ResolveBossClip(cue), bossLevel);
        }

        private AudioClip ResolveBossClip(TutorialSfxCue cue) => cue switch
        {
            TutorialSfxCue.BossPhaseTwo => heltePhaseTwo,
            TutorialSfxCue.BossFinalRush => helteFinalRush,
            TutorialSfxCue.BossMercy => helteMercy,
            TutorialSfxCue.BossBasicWindup => helteBasicWindup,
            TutorialSfxCue.BossSlash => helteSlash,
            TutorialSfxCue.BossBlinkOut => helteBlinkOut,
            TutorialSfxCue.BossBlinkIn => helteBlinkIn,
            TutorialSfxCue.BossDashTelegraph => helteDashTelegraph,
            TutorialSfxCue.BossDash => helteDash,
            TutorialSfxCue.BossCrossTelegraph => helteCrossTelegraph,
            TutorialSfxCue.BossCrossSlash => helteCrossSlash,
            TutorialSfxCue.BossSwordFocus => helteSwordFocus,
            TutorialSfxCue.BossSwordFire => helteSwordFire,
            TutorialSfxCue.BossCounterTelegraph => helteCounterTelegraph,
            TutorialSfxCue.BossCounter => helteCounter,
            _ => null
        };

        private void HandleDialogueAdvance() => Play(uiSource, dialogueAdvance, uiLevel);
        private void HandleObjectiveChanged(TutorialObjectiveChanged message) => Play(uiSource, objectiveUpdated, uiLevel);
        private void HandleTutorialCompleted(TutorialCompleted message) => Play(uiSource, tutorialCompleted, uiLevel);
        private void HandlePanelRequested() => Play(uiSource, panelOpen, uiLevel);
        private void HandleTowerActivated(TowerActivated message) => Play(worldSource, relayActivate, worldLevel);

        private void HandleGameplaySignal(GameplaySignal message)
        {
            if (message.SignalType == QuestSignalType.JumpPerformed)
            {
                Play(playerSource, playerJump, playerLevel);
                return;
            }
            if (message.SignalType == QuestSignalType.DashPerformed)
            {
                Play(playerSource, playerDash, playerLevel);
                return;
            }
            if (message.SignalType != QuestSignalType.PortalUsed || string.IsNullOrWhiteSpace(message.TargetId)) return;

            var target = message.TargetId.ToUpperInvariant();
            if (target.Contains("PACKAGE")) Play(worldSource, itemPickup, worldLevel);
            else if (target.Contains("CLEAR"))
            {
                Play(worldSource, encounterClear, worldLevel);
                Play(worldSource, gateOpen, worldLevel * 0.8f);
            }
            else if (target.Contains("ENCOUNTER") || target.Contains("WAVE"))
                Play(worldSource, encounterStart, worldLevel);
        }

        private void Play(AudioSource source, AudioClip clip, float categoryLevel)
        {
            if (source == null || clip == null) return;
            source.PlayOneShot(clip, Mathf.Clamp01(ResolveSfxVolume() * categoryLevel));
        }

        private float ResolveSfxVolume()
        {
            var settingsMultiplier = 1f;
            if (saveSystemHost != null && saveSystemHost.Initialize() && saveSystemHost.System?.Current?.Settings != null)
            {
                var settings = saveSystemHost.System.Current.Settings;
                settingsMultiplier = Mathf.Clamp01(settings.MasterVolume) * Mathf.Clamp01(settings.SfxVolume);
            }
            return ResolveEffectiveVolume(outputLevel, settingsMultiplier);
        }

        private static void ConfigureSource(AudioSource source, int priority)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            source.priority = priority;
        }

        public static float ResolveEffectiveVolume(float output, float settingsMultiplier) =>
            Mathf.Clamp01(output) * Mathf.Clamp01(settingsMultiplier);

        public static bool IsViewportAudible(Vector3 viewportPoint) =>
            viewportPoint.z > 0f && viewportPoint.x >= 0f && viewportPoint.x <= 1f &&
            viewportPoint.y >= 0f && viewportPoint.y <= 1f;

        public static TutorialSfxCue ResolveBossCue(HelteCombatState state) => state switch
        {
            HelteCombatState.PhaseTransition => TutorialSfxCue.BossPhaseTwo,
            HelteCombatState.FinalRushTransition => TutorialSfxCue.BossFinalRush,
            HelteCombatState.MercyRetreat => TutorialSfxCue.BossMercy,
            HelteCombatState.BasicWindup => TutorialSfxCue.BossBasicWindup,
            HelteCombatState.BasicLeftSlash or HelteCombatState.BasicRightSlash => TutorialSfxCue.BossSlash,
            HelteCombatState.BlinkVanish or HelteCombatState.FakeBlinkVanish => TutorialSfxCue.BossBlinkOut,
            HelteCombatState.BlinkReappear or HelteCombatState.FakeBlinkReappear => TutorialSfxCue.BossBlinkIn,
            HelteCombatState.DashTelegraph => TutorialSfxCue.BossDashTelegraph,
            HelteCombatState.DashApproach => TutorialSfxCue.BossDash,
            HelteCombatState.CrossSlashTelegraph => TutorialSfxCue.BossCrossTelegraph,
            HelteCombatState.CrossSlash => TutorialSfxCue.BossCrossSlash,
            HelteCombatState.SwordFocus => TutorialSfxCue.BossSwordFocus,
            HelteCombatState.SwordVolley => TutorialSfxCue.BossSwordFire,
            HelteCombatState.CounterTelegraph => TutorialSfxCue.BossCounterTelegraph,
            HelteCombatState.CounterSucceeded => TutorialSfxCue.BossCounter,
            _ => TutorialSfxCue.None
        };
    }
}
