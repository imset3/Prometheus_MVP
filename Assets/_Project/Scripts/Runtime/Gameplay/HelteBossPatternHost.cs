using System.Collections;
using UnityEngine;

namespace Narthex.Gameplay
{
    public enum HelteCombatState
    {
        Disabled,
        Waiting,
        PhaseTransition,
        BasicWindup,
        BasicLeftSlash,
        BasicAdvance,
        BasicRightSlash,
        BlinkVanish,
        BlinkReappear,
        DashApproach,
        CrossSlash,
        SwordFocus,
        SwordVolley,
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

        [Header("Phase and movement")]
        [SerializeField, Range(0.1f, 0.9f)] private float phaseTwoHealthRatio = 0.5f;
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
        [SerializeField, Min(0.01f)] private float dashDurationSeconds = 0.3f;
        [SerializeField, Min(0f)] private float crossSlashWarningSeconds = 0.18f;
        [SerializeField, Min(0f)] private float phaseTransitionSeconds = 1f;
        [SerializeField, Min(0f)] private float swordFocusSeconds = 0.55f;
        [SerializeField, Min(0f)] private float swordIntervalSeconds = 0.28f;
        [SerializeField, Min(0f)] private float swordRecoverySeconds = 1f;
        [SerializeField, Min(0f)] private float specialRecoverySeconds = 0.7f;
        [SerializeField, Min(0.01f)] private float hitboxActiveSeconds = 0.1f;

        [Header("Pattern damage")]
        [SerializeField, Min(1)] private int basicDamage = 8;
        [SerializeField, Min(1)] private int blinkDamage = 15;
        [SerializeField, Min(1)] private int swordDamage = 10;

        private readonly Collider2D[] overlapResults = new Collider2D[8];
        private readonly HeltePatternPlanner planner = new HeltePatternPlanner();
        private Coroutine combatRoutine;
        private int activeSwordCount;
        private bool phaseTwoPresented;
        private Vector3 basicHitboxLocalPosition;

        public HeltePattern CurrentPattern { get; private set; }
        public HelteCombatState CurrentState { get; private set; } = HelteCombatState.Disabled;
        public bool IsPhaseTwo => IsPhaseTwoHealth();
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
            ResetPresentation();
        }

        private void OnEnable()
        {
            if (!HasValidSetup()) return;
            ResetPresentation();
            combatRoutine = StartCoroutine(RunCombat());
        }

        private void OnDisable()
        {
            if (combatRoutine != null) StopCoroutine(combatRoutine);
            combatRoutine = null;
            StopAllCoroutines();
            ResetPresentation();
            CurrentPattern = HeltePattern.None;
            SetState(HelteCombatState.Disabled);
        }

        private IEnumerator RunCombat()
        {
            SetState(HelteCombatState.Waiting);
            if (openingDelaySeconds > 0f) yield return new WaitForSeconds(openingDelaySeconds);

            while (CanRunPattern())
            {
                if (IsPhaseTwoHealth() && !phaseTwoPresented)
                {
                    phaseTwoPresented = true;
                    yield return RunPhaseTransition();
                    if (!CanRunPattern()) break;
                }

                CurrentPattern = planner.Next(IsPhaseTwoHealth());
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

            SetState(HelteCombatState.BasicLeftSlash);
            yield return PulseHitbox(basicHitbox, "PAT-HELTE-BASIC-LEFT", basicDamage);
            if (basicSecondHitDelaySeconds > 0f) yield return new WaitForSeconds(basicSecondHitDelaySeconds);

            SetState(HelteCombatState.BasicAdvance);
            var start = transform.position;
            var target = ClampToArena(start + Vector3.right * facing * basicAdvanceDistance);
            yield return MoveBoss(start, target, basicAdvanceSeconds);

            facing = DirectionToPlayer();
            PositionBasicHitbox(facing);
            SetState(HelteCombatState.BasicRightSlash);
            yield return PulseHitbox(basicHitbox, "PAT-HELTE-BASIC-RIGHT", basicDamage);

            SetState(HelteCombatState.Recover);
            if (normalAttackCooldownSeconds > 0f) yield return new WaitForSeconds(normalAttackCooldownSeconds);
        }

        private IEnumerator RunBlinkDash()
        {
            SetState(HelteCombatState.BlinkVanish);
            blinkAfterimageSlot.transform.position = bossVisualSlot.transform.position;
            blinkAfterimageSlot.SetActive(true);
            bossVisualSlot.SetActive(false);
            if (bossBodyCollider != null) bossBodyCollider.enabled = false;
            if (blinkVanishSeconds > 0f) yield return new WaitForSeconds(blinkVanishSeconds);
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
            SetState(HelteCombatState.DashApproach);
            yield return MoveBoss(dashStart, dashTarget, dashDurationSeconds); // Dash travel intentionally deals no damage.
            dashPathSlot.SetActive(false);

            SetState(HelteCombatState.CrossSlash);
            crossSlashWarningSlot.transform.position = blinkCrossHitbox.transform.position;
            crossSlashWarningSlot.SetActive(true);
            if (crossSlashWarningSeconds > 0f) yield return new WaitForSeconds(crossSlashWarningSeconds);
            crossSlashWarningSlot.SetActive(false);
            yield return PulseHitbox(blinkCrossHitbox, "PAT-HELTE-BLINK-CROSS", blinkDamage);

            SetState(HelteCombatState.Recover);
            if (specialRecoverySeconds > 0f) yield return new WaitForSeconds(specialRecoverySeconds);
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
            if (swordRecoverySeconds > 0f) yield return new WaitForSeconds(swordRecoverySeconds);
        }

        private IEnumerator RunPhaseTransition()
        {
            SetState(HelteCombatState.PhaseTransition);
            phaseTransitionSlot.transform.position = bossCenterAnchor.position;
            phaseTransitionSlot.SetActive(true);
            if (phaseTransitionSeconds > 0f) yield return new WaitForSeconds(phaseTransitionSeconds);
            phaseTransitionSlot.SetActive(false);
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
            var duration = swordMaximumTravelDistance / swordProjectileSpeed;
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
                if (!hasDealtDamage) hasDealtDamage = TryDamagePlayer(hitbox, $"PAT-HELTE-SWORD-{index + 1:00}", swordDamage);
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
                sourceActor.CombatSystem.TryApplyDamage(target.ActorId, new DamagePacket(sourceActor.ActorId, patternId, damage));
                return true;
            }
            return false;
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

        private bool IsPhaseTwoHealth()
        {
            return sourceActor != null && sourceActor.Runtime != null &&
                   sourceActor.Runtime.CurrentHealth <= sourceActor.Runtime.MaxHealth * phaseTwoHealthRatio;
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

            for (var index = 0; index < 3; index++)
            {
                if (swordHitboxes[index] == null || swordSpawnAnchors[index] == null || swordVisualSlots[index] == null) return false;
            }
            return true;
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
