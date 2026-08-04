using System.Collections;
using UnityEngine;

namespace Narthex.Gameplay
{
    public enum HelteCombatState
    {
        Disabled,
        Waiting,
        PhaseTransition,
        FinalRushTransition,
        MercyRetreat,
        BasicWindup,
        BasicLeftSlash,
        BasicAdvance,
        BasicRightSlash,
        BlinkVanish,
        BlinkReappear,
        DashTelegraph,
        DashApproach,
        CrossSlashTelegraph,
        CrossSlash,
        SwordFocus,
        SwordVolley,
        FakeBlinkVanish,
        FakeBlinkReappear,
        FakeBlinkPause,
        CounterTelegraph,
        CounterStance,
        CounterSucceeded,
        CounterOpen,
        Recover
    }

    /// <summary>
    /// Tutorial-safe Helte encounter FSM. Every visible object and hitbox is pre-placed in the scene so art can
    /// replace the placeholder slots without changing combat logic.
    /// </summary>
    public sealed class HelteBossPatternHost : MonoBehaviour
    {
        [Header("Pre-placed actors")]
        [SerializeField] private CombatActorHost sourceActor;
        [SerializeField] private CombatActorHost playerActor;
        [SerializeField] private Collider2D bossBodyCollider;
        [SerializeField] private Collider2D playerMeleeHitbox;

        [Header("Pre-placed attack objects")]
        [SerializeField] private Collider2D basicHitbox;
        [SerializeField] private Collider2D blinkCrossHitbox;
        [SerializeField] private Collider2D[] swordHitboxes = new Collider2D[0];
        [SerializeField] private Transform blinkLeftAnchor;
        [SerializeField] private Transform blinkRightAnchor;
        [SerializeField] private Transform bossCenterAnchor;
        [SerializeField] private Transform[] swordSpawnAnchors = new Transform[0];
        [SerializeField] private LayerMask targetLayers = -1;

        [Header("Replaceable presentation slots")]
        [SerializeField] private GameObject bossVisualSlot;
        [SerializeField] private GameObject blinkAfterimageSlot;
        [SerializeField] private GameObject dashPathSlot;
        [SerializeField] private GameObject crossSlashWarningSlot;
        [SerializeField] private GameObject phaseTransitionSlot;
        [SerializeField] private GameObject[] swordVisualSlots = new GameObject[0];

        [Header("Development prototype")]
        [SerializeField] private bool enableFriendlyPatternPrototype;
        [SerializeField, Range(0.05f, 0.5f)] private float mercyHealthRatio = 0.25f;
        [SerializeField, Range(0.1f, 0.75f)] private float mercyRecoveryHealthRatio = 0.35f;
        [SerializeField, Min(0f)] private float mercyPauseSeconds = 1.4f;
        [SerializeField, Min(0f)] private float mercyCooldownSeconds = 30f;
        [SerializeField, Min(0f)] private float fakeBlinkPauseSeconds = 0.9f;
        [SerializeField, Min(0f)] private float counterTelegraphSeconds = 0.35f;
        [SerializeField, Min(0f)] private float counterStanceSeconds = 0.75f;
        [SerializeField, Min(0f)] private float counterOpenSeconds = 1.2f;
        [SerializeField, Min(0f)] private float counterSuccessRecoverySeconds = 0.45f;
        [SerializeField, Min(0f)] private float counterPushDistance = 1.5f;

        [Header("Phase and movement")]
        [SerializeField, Range(0.1f, 0.9f)] private float phaseTwoHealthRatio = 0.5f;
        [SerializeField, Range(0.05f, 0.5f)] private float finalRushHealthRatio = 0.2f;
        [SerializeField, Min(0.1f)] private float basicAdvanceDistance = 1f;
        [SerializeField, Min(0.1f)] private float blinkSideDistance = 3f;
        [SerializeField, Min(0.1f)] private float dashDistance = 4f;
        [SerializeField, Min(0.1f)] private float swordProjectileSpeed = 14f;
        [SerializeField, Min(0.1f)] private float swordMaximumTravelDistance = 14f;

        [Header("Pattern timing")]
        [SerializeField, Min(0f)] private float openingDelaySeconds = 0.4f;
        [SerializeField, Min(0f)] private float basicWindupSeconds = 0.28f;
        [SerializeField, Min(0f)] private float basicSecondHitDelaySeconds = 0.18f;
        [SerializeField, Min(0.01f)] private float basicAdvanceSeconds = 0.12f;
        [SerializeField, Min(0f)] private float normalAttackCooldownSeconds = 2f;
        [SerializeField, Min(0f)] private float blinkVanishSeconds = 0.22f;
        [SerializeField, Min(0f)] private float blinkTelegraphSeconds = 0.25f;
        [SerializeField, Min(0f)] private float dashTelegraphSeconds = 0.3f;
        [SerializeField, Min(0.01f)] private float dashDurationSeconds = 0.3f;
        [SerializeField, Min(0f)] private float crossSlashWarningSeconds = 0.18f;
        [SerializeField, Min(0f)] private float phaseTransitionSeconds = 1f;
        [SerializeField, Min(0f)] private float finalRushTransitionSeconds = 0.65f;
        [SerializeField, Min(0f)] private float swordFocusSeconds = 0.55f;
        [SerializeField, Min(0f)] private float swordIntervalSeconds = 0.28f;
        [SerializeField, Min(0f)] private float swordRecoverySeconds = 1f;
        [SerializeField, Min(0f)] private float specialRecoverySeconds = 0.7f;
        [SerializeField, Min(0.01f)] private float hitboxActiveSeconds = 0.1f;

        [Header("Phase pacing")]
        [SerializeField, Range(0.25f, 1f)] private float phaseTwoRecoveryMultiplier = 0.75f;
        [SerializeField, Range(0.25f, 1f)] private float finalRushRecoveryMultiplier = 0.55f;
        [SerializeField, Range(0.5f, 1f)] private float phaseTwoMovementDurationMultiplier = 0.9f;
        [SerializeField, Range(0.5f, 1f)] private float finalRushMovementDurationMultiplier = 0.78f;
        [SerializeField, Min(1f)] private float phaseTwoProjectileSpeedMultiplier = 1.1f;
        [SerializeField, Min(1f)] private float finalRushProjectileSpeedMultiplier = 1.2f;

        [Header("Pattern damage")]
        [SerializeField, Min(1)] private int basicDamage = 8;
        [SerializeField, Min(1)] private int blinkDamage = 15;
        [SerializeField, Min(1)] private int swordDamage = 10;
        [SerializeField, Min(1f)] private float phaseTwoDamageMultiplier = 1.15f;
        [SerializeField, Min(1f)] private float finalRushDamageMultiplier = 1.3f;
        [SerializeField] private bool applyTutorialBalance = true;
        [SerializeField] private float visualGroundOffsetY;

        private readonly Collider2D[] overlapResults = new Collider2D[8];
        private readonly HeltePatternPlanner planner = new HeltePatternPlanner();
        private Coroutine combatRoutine;
        private int activeSwordCount;
        private bool phaseTwoPresented;
        private bool finalRushPresented;
        private bool phaseTwoHealthRestored;
        private float nextMercyAvailableTime;
        private Vector3 basicHitboxLocalPosition;
        private Vector3 initialBossPosition;
        private Quaternion initialBossRotation;

        public HeltePattern CurrentPattern { get; private set; }
        public HelteCombatState CurrentState { get; private set; } = HelteCombatState.Disabled;
        public bool IsPhaseTwo => IsPhaseTwoHealth();
        public bool IsFinalRush => IsFinalRushHealth();
        public HelteCombatTempo CurrentTempo => ResolveCombatTempo();
        public float PhaseTwoHealthRatio => phaseTwoHealthRatio;
        public float FinalRushHealthRatio => finalRushHealthRatio;
        public float MercyCooldownSeconds => mercyCooldownSeconds;
        public bool FriendlyPatternPrototypeEnabled => enableFriendlyPatternPrototype;
        public event System.Action<HeltePattern> PatternStarted;
        public event System.Action<HelteCombatState> StateChanged;

        private void Awake()
        {
            if (!HasValidSetup())
            {
                Debug.LogError("HelteBossPatternHost requires all pre-placed actors, hitboxes, anchors, and art slots.", this);
                enabled = false;
                return;
            }

            basicHitboxLocalPosition = basicHitbox.transform.localPosition;
            initialBossPosition = transform.position;
            initialBossRotation = transform.rotation;
            ApplyTutorialBalance();
            AlignVisualToArenaFloor();
            ResetPresentation();
        }

        private void OnEnable()
        {
            if (!HasValidSetup()) return;
            ResetCombatRunState();
            ResetPresentation();
            combatRoutine = StartCoroutine(RunCombat());
        }

        private void OnDisable()
        {
            if (combatRoutine != null) StopCoroutine(combatRoutine);
            combatRoutine = null;
            StopAllCoroutines();
            sourceActor?.SetScriptedInvulnerability(false);
            ResetPresentation();
            CurrentPattern = HeltePattern.None;
            SetState(HelteCombatState.Disabled);
        }

        public void ResetForEncounter()
        {
            if (combatRoutine != null) StopCoroutine(combatRoutine);
            combatRoutine = null;
            StopAllCoroutines();
            transform.SetPositionAndRotation(initialBossPosition, initialBossRotation);
            Physics2D.SyncTransforms();
            ResetCombatRunState();
            ResetPresentation();
            if (!isActiveAndEnabled) SetState(HelteCombatState.Disabled);
        }

        private IEnumerator RunCombat()
        {
            SetState(HelteCombatState.Waiting);
            if (openingDelaySeconds > 0f) yield return new WaitForSeconds(openingDelaySeconds);

            while (CanRunPattern())
            {
                if (ShouldOfferMercy())
                {
                    nextMercyAvailableTime = Time.time + mercyCooldownSeconds;
                    yield return RunMercyRetreat();
                    if (!CanRunPattern()) break;
                }

                if (IsPhaseTwoHealth() && !phaseTwoPresented)
                {
                    phaseTwoPresented = true;
                    if (!phaseTwoHealthRestored && playerActor != null)
                    {
                        playerActor.RestoreHealthToMax();
                        phaseTwoHealthRestored = true;
                    }
                    yield return RunPhaseTransition();
                    if (!CanRunPattern()) break;
                }

                if (IsFinalRushHealth() && !finalRushPresented)
                {
                    finalRushPresented = true;
                    yield return RunFinalRushTransition();
                    if (!CanRunPattern()) break;
                }

                CurrentPattern = planner.Next(ResolveCombatTempo(), enableFriendlyPatternPrototype);
                PatternStarted?.Invoke(CurrentPattern);
                switch (CurrentPattern)
                {
                    case HeltePattern.BasicCombo:
                        yield return RunBasicCombo();
                        break;
                    case HeltePattern.BlinkDash:
                        yield return RunBlinkDash();
                        break;
                    case HeltePattern.SummonSwords:
                        yield return RunSwordSummon();
                        break;
                    case HeltePattern.FakeBlink:
                        yield return RunFakeBlink();
                        break;
                    case HeltePattern.CounterStance:
                        yield return RunCounterStance();
                        break;
                }
            }

            CurrentPattern = HeltePattern.None;
            SetState(HelteCombatState.Waiting);
            combatRoutine = null;
        }

        private IEnumerator RunBasicCombo()
        {
            var facing = DirectionToPlayer();
            PositionBasicHitbox(facing);
            SetState(HelteCombatState.BasicWindup);
            if (basicWindupSeconds > 0f) yield return new WaitForSeconds(basicWindupSeconds);
            if (!CanRunPattern()) yield break;

            SetState(HelteCombatState.BasicLeftSlash);
            yield return PulseHitbox(basicHitbox, "PAT-HELTE-BASIC-LEFT", ScaleDamage(basicDamage));
            if (basicSecondHitDelaySeconds > 0f) yield return new WaitForSeconds(basicSecondHitDelaySeconds);
            if (!CanRunPattern()) yield break;

            SetState(HelteCombatState.BasicAdvance);
            var start = transform.position;
            var target = ClampToArena(start + Vector3.right * facing * basicAdvanceDistance);
            yield return MoveBoss(start, target, ScaleMovementDuration(basicAdvanceSeconds));
            if (!CanRunPattern()) yield break;

            facing = DirectionToPlayer();
            PositionBasicHitbox(facing);
            SetState(HelteCombatState.BasicRightSlash);
            yield return PulseHitbox(basicHitbox, "PAT-HELTE-BASIC-RIGHT", ScaleDamage(basicDamage));

            SetState(HelteCombatState.Recover);
            var recovery = ScaleRecovery(normalAttackCooldownSeconds);
            if (recovery > 0f) yield return new WaitForSeconds(recovery);
        }

        private IEnumerator RunBlinkDash()
        {
            SetState(HelteCombatState.BlinkVanish);
            blinkAfterimageSlot.transform.position = bossVisualSlot.transform.position;
            blinkAfterimageSlot.SetActive(true);
            bossVisualSlot.SetActive(false);
            if (bossBodyCollider != null) bossBodyCollider.enabled = false;
            if (blinkVanishSeconds > 0f) yield return new WaitForSeconds(blinkVanishSeconds);
            if (!CanRunPattern())
            {
                ResetPresentation();
                yield break;
            }
            blinkAfterimageSlot.SetActive(false);

            var side = Random.value < 0.5f ? -1f : 1f;
            var destination = playerActor.transform.position + Vector3.right * side * blinkSideDistance;
            destination.y = bossCenterAnchor.position.y;
            transform.position = ClampToArena(destination);
            Physics2D.SyncTransforms();

            SetState(HelteCombatState.BlinkReappear);
            bossVisualSlot.SetActive(true);
            if (bossBodyCollider != null) bossBodyCollider.enabled = true;
            if (blinkTelegraphSeconds > 0f) yield return new WaitForSeconds(blinkTelegraphSeconds);

            var dashStart = transform.position;
            var dashDirection = DirectionToPlayer();
            var dashTarget = ClampToArena(dashStart + Vector3.right * dashDirection * dashDistance);
            ShowDashPath(dashStart, dashTarget);
            SetState(HelteCombatState.DashTelegraph);
            if (dashTelegraphSeconds > 0f) yield return new WaitForSeconds(dashTelegraphSeconds);
            if (!CanRunPattern())
            {
                dashPathSlot.SetActive(false);
                yield break;
            }

            SetState(HelteCombatState.DashApproach);
            yield return MoveBoss(dashStart, dashTarget, ScaleMovementDuration(dashDurationSeconds)); // Dash travel intentionally deals no damage.
            dashPathSlot.SetActive(false);
            if (!CanRunPattern()) yield break;

            SetState(HelteCombatState.CrossSlashTelegraph);
            crossSlashWarningSlot.transform.position = blinkCrossHitbox.transform.position;
            crossSlashWarningSlot.SetActive(true);
            if (crossSlashWarningSeconds > 0f) yield return new WaitForSeconds(crossSlashWarningSeconds);
            crossSlashWarningSlot.SetActive(false);
            if (!CanRunPattern()) yield break;

            SetState(HelteCombatState.CrossSlash);
            yield return PulseHitbox(blinkCrossHitbox, "PAT-HELTE-BLINK-CROSS", ScaleDamage(blinkDamage));

            SetState(HelteCombatState.Recover);
            var recovery = ScaleRecovery(specialRecoverySeconds);
            if (recovery > 0f) yield return new WaitForSeconds(recovery);
        }

        private IEnumerator RunSwordSummon()
        {
            SetState(HelteCombatState.SwordFocus);
            ResetSwordObjectsToSpawns(true);
            if (swordFocusSeconds > 0f) yield return new WaitForSeconds(swordFocusSeconds);

            SetState(HelteCombatState.SwordVolley);
            activeSwordCount = 0;
            for (var index = 0; index < swordHitboxes.Length; index++)
            {
                var capturedTarget = playerActor.transform.position;
                StartCoroutine(LaunchSword(index, capturedTarget));
                if (index < swordHitboxes.Length - 1 && swordIntervalSeconds > 0f)
                    yield return new WaitForSeconds(swordIntervalSeconds);
            }

            while (activeSwordCount > 0 && CanRunPattern()) yield return null;
            SetState(HelteCombatState.Recover);
            var recovery = ScaleRecovery(swordRecoverySeconds);
            if (recovery > 0f) yield return new WaitForSeconds(recovery);
        }

        private IEnumerator RunFakeBlink()
        {
            SetState(HelteCombatState.FakeBlinkVanish);
            blinkAfterimageSlot.transform.position = bossVisualSlot.transform.position;
            blinkAfterimageSlot.SetActive(true);
            bossVisualSlot.SetActive(false);
            bossBodyCollider.enabled = false;
            sourceActor.SetScriptedInvulnerability(true);
            if (blinkVanishSeconds > 0f) yield return new WaitForSeconds(blinkVanishSeconds);
            if (!CanRunPattern())
            {
                sourceActor.SetScriptedInvulnerability(false);
                ResetPresentation();
                yield break;
            }

            blinkAfterimageSlot.SetActive(false);
            var side = playerActor.transform.position.x < transform.position.x ? 1f : -1f;
            var destination = playerActor.transform.position + Vector3.right * side * (blinkSideDistance * 0.6f);
            destination.y = bossCenterAnchor.position.y;
            transform.position = ClampToArena(destination);
            Physics2D.SyncTransforms();

            SetState(HelteCombatState.FakeBlinkReappear);
            bossVisualSlot.SetActive(true);
            bossBodyCollider.enabled = true;
            sourceActor.SetScriptedInvulnerability(false);
            if (blinkTelegraphSeconds > 0f) yield return new WaitForSeconds(blinkTelegraphSeconds);
            if (!CanRunPattern()) yield break;

            // Helte deliberately does not attack. The pause baits a panic dodge and exposes his playful intent.
            SetState(HelteCombatState.FakeBlinkPause);
            if (fakeBlinkPauseSeconds > 0f) yield return new WaitForSeconds(fakeBlinkPauseSeconds);
        }

        private IEnumerator RunCounterStance()
        {
            SetState(HelteCombatState.CounterTelegraph);
            crossSlashWarningSlot.transform.position = bossCenterAnchor.position;
            crossSlashWarningSlot.SetActive(true);
            if (counterTelegraphSeconds > 0f) yield return new WaitForSeconds(counterTelegraphSeconds);
            if (!CanRunPattern())
            {
                crossSlashWarningSlot.SetActive(false);
                yield break;
            }

            sourceActor.SetScriptedInvulnerability(true);
            SetState(HelteCombatState.CounterStance);
            var countered = false;
            var elapsed = 0f;
            while (elapsed < counterStanceSeconds && CanRunPattern())
            {
                elapsed += Time.deltaTime;
                if (IsPlayerMeleeTouchingBoss())
                {
                    countered = true;
                    SetState(HelteCombatState.CounterSucceeded);
                    PushPlayerAway();
                    break;
                }
                yield return null;
            }

            sourceActor.SetScriptedInvulnerability(false);
            crossSlashWarningSlot.SetActive(false);
            if (!CanRunPattern()) yield break;

            if (countered)
            {
                if (counterSuccessRecoverySeconds > 0f)
                    yield return new WaitForSeconds(counterSuccessRecoverySeconds);
                yield break;
            }

            SetState(HelteCombatState.CounterOpen);
            if (counterOpenSeconds > 0f) yield return new WaitForSeconds(counterOpenSeconds);
        }

        private IEnumerator RunMercyRetreat()
        {
            sourceActor.SetScriptedInvulnerability(true);
            SetState(HelteCombatState.MercyRetreat);
            var retreatAnchor = playerActor.transform.position.x < transform.position.x
                ? blinkRightAnchor
                : blinkLeftAnchor;
            var start = transform.position;
            var target = retreatAnchor.position;
            target.y = bossCenterAnchor.position.y;
            yield return MoveBoss(start, ClampToArena(target), ScaleMovementDuration(dashDurationSeconds));

            if (playerActor.Runtime != null && playerActor.Runtime.IsAlive)
            {
                var recoveryHealth = Mathf.CeilToInt(playerActor.Runtime.MaxHealth * mercyRecoveryHealthRatio);
                playerActor.Runtime.CurrentHealth = Mathf.Max(playerActor.Runtime.CurrentHealth, recoveryHealth);
                playerActor.Runtime.State = CombatState.Idle;
            }

            if (mercyPauseSeconds > 0f) yield return new WaitForSeconds(mercyPauseSeconds);
            sourceActor.SetScriptedInvulnerability(false);
        }

        private IEnumerator RunPhaseTransition()
        {
            sourceActor.SetScriptedInvulnerability(true);
            SetState(HelteCombatState.PhaseTransition);
            phaseTransitionSlot.transform.position = bossCenterAnchor.position;
            phaseTransitionSlot.SetActive(true);
            if (phaseTransitionSeconds > 0f) yield return new WaitForSeconds(phaseTransitionSeconds);
            phaseTransitionSlot.SetActive(false);
            sourceActor.SetScriptedInvulnerability(false);
        }

        private IEnumerator RunFinalRushTransition()
        {
            sourceActor.SetScriptedInvulnerability(true);
            SetState(HelteCombatState.FinalRushTransition);
            phaseTransitionSlot.transform.position = bossCenterAnchor.position;
            phaseTransitionSlot.SetActive(true);
            if (finalRushTransitionSeconds > 0f) yield return new WaitForSeconds(finalRushTransitionSeconds);
            phaseTransitionSlot.SetActive(false);
            sourceActor.SetScriptedInvulnerability(false);
        }

        private IEnumerator LaunchSword(int index, Vector3 capturedTarget)
        {
            activeSwordCount++;
            var hitbox = swordHitboxes[index];
            var visual = swordVisualSlots[index];
            var start = swordSpawnAnchors[index].position;
            var direction = (capturedTarget - start).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.down;
            var end = start + direction * swordMaximumTravelDistance;
            var duration = swordMaximumTravelDistance / (swordProjectileSpeed * ResolveProjectileSpeedMultiplier());
            var elapsed = 0f;
            var hasDealtDamage = false;

            hitbox.transform.position = start;
            visual.transform.position = start;
            visual.transform.up = -direction;
            hitbox.enabled = true;
            visual.SetActive(true);
            while (elapsed < duration && CanRunPattern())
            {
                elapsed += Time.deltaTime;
                var position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
                hitbox.transform.position = position;
                visual.transform.position = position;
                Physics2D.SyncTransforms();
                if (!hasDealtDamage)
                    hasDealtDamage = TryDamagePlayer(
                        hitbox,
                        $"PAT-HELTE-SWORD-{index + 1:00}",
                        ScaleDamage(swordDamage));
                yield return null;
            }

            hitbox.enabled = false;
            visual.SetActive(false);
            activeSwordCount--;
        }

        private IEnumerator PulseHitbox(Collider2D hitbox, string patternId, int damage)
        {
            hitbox.enabled = true;
            Physics2D.SyncTransforms();
            TryDamagePlayer(hitbox, patternId, damage);
            yield return new WaitForSeconds(hitboxActiveSeconds);
            hitbox.enabled = false;
        }

        private IEnumerator MoveBoss(Vector3 start, Vector3 target, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration && CanRunPattern())
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                Physics2D.SyncTransforms();
                yield return null;
            }
            if (!CanRunPattern()) yield break;
            transform.position = target;
            Physics2D.SyncTransforms();
        }

        private bool TryDamagePlayer(Collider2D hitbox, string patternId, int damage)
        {
            var filter = ContactFilter2D.noFilter;
            filter.SetLayerMask(targetLayers);
            filter.useTriggers = true;
            var count = hitbox.Overlap(filter, overlapResults);
            for (var index = 0; index < count; index++)
            {
                var target = overlapResults[index].GetComponentInParent<CombatActorHost>();
                if (target == null || target.Kind != CombatActorKind.Player) continue;
                var appliedDamage = LimitDamageUntilMercy(target, damage);
                if (appliedDamage > 0)
                    sourceActor.CombatSystem.TryApplyDamage(
                        target.ActorId,
                        new DamagePacket(sourceActor.ActorId, patternId, appliedDamage));
                return true;
            }
            return false;
        }

        private int LimitDamageUntilMercy(CombatActorHost target, int damage)
        {
            if (!enableFriendlyPatternPrototype || !IsMercyAvailable() || target?.Runtime == null)
                return damage;

            return HelteFriendlyCombatPolicy.LimitDamageBeforeMercy(
                target.Runtime.CurrentHealth,
                target.Runtime.MaxHealth,
                damage,
                mercyHealthRatio,
                true);
        }

        private void PositionBasicHitbox(float facing)
        {
            var local = basicHitboxLocalPosition;
            local.x = Mathf.Abs(local.x) * facing;
            basicHitbox.transform.localPosition = local;
        }

        private void ShowDashPath(Vector3 start, Vector3 end)
        {
            var distance = Vector3.Distance(start, end);
            dashPathSlot.transform.position = Vector3.Lerp(start, end, 0.5f);
            dashPathSlot.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg);
            var scale = dashPathSlot.transform.localScale;
            scale.x = distance;
            dashPathSlot.transform.localScale = scale;
            dashPathSlot.SetActive(true);
        }

        private Vector3 ClampToArena(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, blinkLeftAnchor.position.x, blinkRightAnchor.position.x);
            position.y = bossCenterAnchor.position.y;
            position.z = transform.position.z;
            return position;
        }

        private float DirectionToPlayer()
        {
            return playerActor.transform.position.x < transform.position.x ? -1f : 1f;
        }

        private bool ShouldOfferMercy()
        {
            return enableFriendlyPatternPrototype && IsMercyAvailable() &&
                   playerActor != null && playerActor.Runtime != null && playerActor.Runtime.IsAlive &&
                   playerActor.Runtime.CurrentHealth <= playerActor.Runtime.MaxHealth * mercyHealthRatio;
        }

        private bool IsMercyAvailable()
        {
            return HelteFriendlyCombatPolicy.IsMercyAvailable(Time.time, nextMercyAvailableTime);
        }

        private bool IsPlayerMeleeTouchingBoss()
        {
            return playerMeleeHitbox != null && playerMeleeHitbox.enabled && bossBodyCollider != null &&
                   bossBodyCollider.enabled && playerMeleeHitbox.Distance(bossBodyCollider).isOverlapped;
        }

        private void PushPlayerAway()
        {
            var direction = playerActor.transform.position.x < transform.position.x ? -1f : 1f;
            var position = playerActor.transform.position;
            position.x += direction * counterPushDistance;
            playerActor.transform.position = position;
            Physics2D.SyncTransforms();
        }

        private bool IsPhaseTwoHealth()
        {
            return sourceActor != null && sourceActor.Runtime != null &&
                   sourceActor.Runtime.CurrentHealth <= sourceActor.Runtime.MaxHealth * phaseTwoHealthRatio;
        }

        private bool IsFinalRushHealth()
        {
            return sourceActor != null && sourceActor.Runtime != null &&
                   sourceActor.Runtime.CurrentHealth <= sourceActor.Runtime.MaxHealth * finalRushHealthRatio;
        }

        private HelteCombatTempo ResolveCombatTempo()
        {
            if (IsFinalRushHealth()) return HelteCombatTempo.FinalRush;
            return IsPhaseTwoHealth() ? HelteCombatTempo.PhaseTwo : HelteCombatTempo.Opening;
        }

        private float ScaleRecovery(float duration)
        {
            return ResolveCombatTempo() switch
            {
                HelteCombatTempo.FinalRush => duration * finalRushRecoveryMultiplier,
                HelteCombatTempo.PhaseTwo => duration * phaseTwoRecoveryMultiplier,
                _ => duration
            };
        }

        private float ScaleMovementDuration(float duration)
        {
            return ResolveCombatTempo() switch
            {
                HelteCombatTempo.FinalRush => duration * finalRushMovementDurationMultiplier,
                HelteCombatTempo.PhaseTwo => duration * phaseTwoMovementDurationMultiplier,
                _ => duration
            };
        }

        private float ResolveProjectileSpeedMultiplier()
        {
            return ResolveCombatTempo() switch
            {
                HelteCombatTempo.FinalRush => finalRushProjectileSpeedMultiplier,
                HelteCombatTempo.PhaseTwo => phaseTwoProjectileSpeedMultiplier,
                _ => 1f
            };
        }

        private void ApplyTutorialBalance()
        {
            if (!applyTutorialBalance) return;

            // Slower pattern pacing to allow players to read and react
            normalAttackCooldownSeconds = 1.8f;
            swordRecoverySeconds = 1.2f;
            specialRecoverySeconds = 1.0f;

            // Generous, highly distinct warning windows and telegraphs
            basicWindupSeconds = 0.55f;
            blinkTelegraphSeconds = 0.65f;
            dashTelegraphSeconds = 0.70f;
            crossSlashWarningSeconds = 0.50f;
            swordFocusSeconds = 0.85f;

            // Align Helte visual to arena floor
            visualGroundOffsetY = -0.68f;
        }

        private void AlignVisualToArenaFloor()
        {
            if (bossVisualSlot == null) return;
            var localPosition = bossVisualSlot.transform.localPosition;
            localPosition.y = visualGroundOffsetY;
            bossVisualSlot.transform.localPosition = localPosition;
        }

        private int ScaleDamage(int damage)
        {
            var multiplier = ResolveCombatTempo() switch
            {
                HelteCombatTempo.FinalRush => finalRushDamageMultiplier,
                HelteCombatTempo.PhaseTwo => phaseTwoDamageMultiplier,
                _ => 1f
            };
            return Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
        }

        private bool CanRunPattern()
        {
            return isActiveAndEnabled && sourceActor.Runtime != null && sourceActor.CombatSystem != null &&
                   sourceActor.Runtime.IsAlive && playerActor.Runtime != null && playerActor.Runtime.IsAlive;
        }

        private bool HasValidSetup()
        {
            if (sourceActor == null || playerActor == null || basicHitbox == null || blinkCrossHitbox == null ||
                blinkLeftAnchor == null || blinkRightAnchor == null || bossCenterAnchor == null || bossVisualSlot == null ||
                blinkAfterimageSlot == null || dashPathSlot == null || crossSlashWarningSlot == null || phaseTransitionSlot == null ||
                swordHitboxes == null || swordSpawnAnchors == null || swordVisualSlots == null ||
                swordHitboxes.Length != 3 || swordSpawnAnchors.Length != 3 || swordVisualSlots.Length != 3)
                return false;
            if (enableFriendlyPatternPrototype && playerMeleeHitbox == null) return false;

            for (var index = 0; index < 3; index++)
            {
                if (swordHitboxes[index] == null || swordSpawnAnchors[index] == null || swordVisualSlots[index] == null) return false;
            }
            return true;
        }

        private void ResetCombatRunState()
        {
            planner.Reset();
            phaseTwoPresented = false;
            finalRushPresented = false;
            phaseTwoHealthRestored = false;
            nextMercyAvailableTime = float.NegativeInfinity;
            sourceActor?.SetScriptedInvulnerability(false);
            CurrentPattern = HeltePattern.None;
            SetState(HelteCombatState.Waiting);
        }

        private void ResetPresentation()
        {
            if (basicHitbox != null) basicHitbox.enabled = false;
            if (blinkCrossHitbox != null) blinkCrossHitbox.enabled = false;
            if (bossVisualSlot != null) bossVisualSlot.SetActive(true);
            if (bossBodyCollider != null && enabled) bossBodyCollider.enabled = true;
            if (blinkAfterimageSlot != null) blinkAfterimageSlot.SetActive(false);
            if (dashPathSlot != null) dashPathSlot.SetActive(false);
            if (crossSlashWarningSlot != null) crossSlashWarningSlot.SetActive(false);
            if (phaseTransitionSlot != null) phaseTransitionSlot.SetActive(false);
            ResetSwordObjectsToSpawns(false);
            activeSwordCount = 0;
        }

        private void ResetSwordObjectsToSpawns(bool showVisuals)
        {
            if (swordHitboxes == null || swordSpawnAnchors == null || swordVisualSlots == null) return;
            var count = Mathf.Min(swordHitboxes.Length, Mathf.Min(swordSpawnAnchors.Length, swordVisualSlots.Length));
            for (var index = 0; index < count; index++)
            {
                if (swordHitboxes[index] != null)
                {
                    swordHitboxes[index].enabled = false;
                    swordHitboxes[index].transform.position = swordSpawnAnchors[index].position;
                }
                if (swordVisualSlots[index] != null)
                {
                    swordVisualSlots[index].transform.position = swordSpawnAnchors[index].position;
                    swordVisualSlots[index].SetActive(showVisuals);
                }
            }
        }

        private void SetState(HelteCombatState state)
        {
            if (CurrentState == state) return;
            CurrentState = state;
            StateChanged?.Invoke(state);
        }
    }
}
