using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class HelteBossPatternHost : MonoBehaviour
    {
        [Header("Pre-placed actors")]
        [SerializeField] private CombatActorHost sourceActor;
        [SerializeField] private CombatActorHost playerActor;

        [Header("Pre-placed attack objects")]
        [SerializeField] private Collider2D basicHitbox;
        [SerializeField] private Collider2D blinkCrossHitbox;
        [SerializeField] private Collider2D[] swordHitboxes = new Collider2D[0];
        [SerializeField] private Transform blinkLeftAnchor;
        [SerializeField] private Transform blinkRightAnchor;
        [SerializeField] private LayerMask targetLayers = -1;

        [Header("Pattern timing")]
        [SerializeField, Min(0f)] private float patternIntervalSeconds = 0.8f;
        [SerializeField, Min(0f)] private float basicSecondHitDelaySeconds = 0.18f;
        [SerializeField, Min(0f)] private float blinkTelegraphSeconds = 0.35f;
        [SerializeField, Min(0f)] private float swordTelegraphSeconds = 0.25f;
        [SerializeField, Min(0f)] private float swordIntervalSeconds = 0.2f;
        [SerializeField, Min(0.01f)] private float hitboxActiveSeconds = 0.1f;

        [Header("Pattern damage")]
        [SerializeField, Min(1)] private int basicDamage = 8;
        [SerializeField, Min(1)] private int blinkDamage = 15;
        [SerializeField, Min(1)] private int swordDamage = 10;

        private readonly Collider2D[] overlapResults = new Collider2D[8];
        private readonly HeltePatternPlanner planner = new HeltePatternPlanner();
        private float nextPatternAt;
        private float secondBasicHitAt = -1f;
        private float blinkHitAt = -1f;
        private float nextSwordAt = -1f;
        private int nextSwordIndex;
        private Collider2D activeHitbox;
        private float activeHitboxEndsAt;
        private bool blinkLeftNext = true;
        private Vector3 blinkLeftPosition;
        private Vector3 blinkRightPosition;

        public HeltePattern CurrentPattern { get; private set; }
        public event System.Action<HeltePattern> PatternStarted;

        private void Awake()
        {
            if (!HasValidSetup())
            {
                Debug.LogError("HelteBossPatternHost requires pre-placed actors, hitboxes, and blink anchors.", this);
                enabled = false;
                return;
            }

            blinkLeftPosition = blinkLeftAnchor.position;
            blinkRightPosition = blinkRightAnchor.position;
            DisableAllHitboxes();
        }

        private void OnDisable()
        {
            DisableAllHitboxes();
            ClearScheduledHits();
            CurrentPattern = HeltePattern.None;
        }

        private void Update()
        {
            DisableExpiredHitbox();
            if (!CanRunPattern()) return;

            if (secondBasicHitAt >= 0f && Time.time >= secondBasicHitAt)
            {
                secondBasicHitAt = -1f;
                ActivateHitbox(basicHitbox, "PAT-HELTE-BASIC-01", basicDamage);
            }

            if (blinkHitAt >= 0f && Time.time >= blinkHitAt)
            {
                blinkHitAt = -1f;
                ActivateHitbox(blinkCrossHitbox, "PAT-HELTE-BLINK-DASH-01", blinkDamage);
            }

            if (nextSwordAt >= 0f && Time.time >= nextSwordAt) ActivateNextSword();

            if (HasScheduledHit() || Time.time < nextPatternAt) return;
            StartNextPattern();
        }

        private bool HasValidSetup()
        {
            if (sourceActor == null || playerActor == null || basicHitbox == null || blinkCrossHitbox == null ||
                blinkLeftAnchor == null || blinkRightAnchor == null || swordHitboxes == null || swordHitboxes.Length != 3)
                return false;

            for (var index = 0; index < swordHitboxes.Length; index++)
            {
                if (swordHitboxes[index] == null) return false;
            }

            return true;
        }

        private bool CanRunPattern()
        {
            return sourceActor.Runtime != null && sourceActor.CombatSystem != null && sourceActor.Runtime.IsAlive &&
                   playerActor.Runtime != null && playerActor.Runtime.IsAlive;
        }

        private bool HasScheduledHit()
        {
            return secondBasicHitAt >= 0f || blinkHitAt >= 0f || nextSwordAt >= 0f;
        }

        private void StartNextPattern()
        {
            var phaseTwo = sourceActor.Runtime.CurrentHealth * 2 <= sourceActor.Runtime.MaxHealth;
            CurrentPattern = planner.Next(phaseTwo);
            PatternStarted?.Invoke(CurrentPattern);
            nextPatternAt = Time.time + patternIntervalSeconds;

            switch (CurrentPattern)
            {
                case HeltePattern.BasicCombo:
                    ActivateHitbox(basicHitbox, "PAT-HELTE-BASIC-01", basicDamage);
                    secondBasicHitAt = Time.time + basicSecondHitDelaySeconds;
                    break;
                case HeltePattern.BlinkDash:
                    sourceActor.transform.position = blinkLeftNext ? blinkLeftPosition : blinkRightPosition;
                    blinkLeftNext = !blinkLeftNext;
                    blinkHitAt = Time.time + blinkTelegraphSeconds;
                    break;
                case HeltePattern.SummonSwords:
                    nextSwordIndex = 0;
                    nextSwordAt = Time.time + swordTelegraphSeconds;
                    break;
            }
        }

        private void ActivateNextSword()
        {
            if (nextSwordIndex >= swordHitboxes.Length)
            {
                nextSwordAt = -1f;
                return;
            }

            var offset = nextSwordIndex switch
            {
                0 => new Vector3(-1.2f, 0f, 0f),
                1 => Vector3.zero,
                _ => new Vector3(1.2f, 0f, 0f)
            };
            swordHitboxes[nextSwordIndex].transform.position = playerActor.transform.position + offset;
            ActivateHitbox(swordHitboxes[nextSwordIndex], "PAT-HELTE-SUMMON-SWORD-01", swordDamage);
            nextSwordIndex++;
            nextSwordAt = nextSwordIndex < swordHitboxes.Length ? Time.time + swordIntervalSeconds : -1f;
        }

        private void ActivateHitbox(Collider2D hitbox, string patternId, int damage)
        {
            if (hitbox == null) return;
            if (activeHitbox != null && activeHitbox != hitbox) activeHitbox.enabled = false;

            activeHitbox = hitbox;
            activeHitbox.enabled = true;
            activeHitboxEndsAt = Time.time + hitboxActiveSeconds;

            var filter = ContactFilter2D.noFilter;
            filter.SetLayerMask(targetLayers);
            filter.useTriggers = true;
            var count = hitbox.Overlap(filter, overlapResults);
            for (var index = 0; index < count; index++)
            {
                var target = overlapResults[index].GetComponentInParent<CombatActorHost>();
                if (target == null || target.Kind != CombatActorKind.Player) continue;
                sourceActor.CombatSystem.TryApplyDamage(target.ActorId, new DamagePacket(sourceActor.ActorId, patternId, damage));
                break;
            }
        }

        private void DisableExpiredHitbox()
        {
            if (activeHitbox == null || Time.time < activeHitboxEndsAt) return;
            activeHitbox.enabled = false;
            activeHitbox = null;
        }

        private void DisableAllHitboxes()
        {
            if (basicHitbox != null) basicHitbox.enabled = false;
            if (blinkCrossHitbox != null) blinkCrossHitbox.enabled = false;
            if (swordHitboxes == null) return;
            for (var index = 0; index < swordHitboxes.Length; index++)
            {
                if (swordHitboxes[index] != null) swordHitboxes[index].enabled = false;
            }
            activeHitbox = null;
        }

        private void ClearScheduledHits()
        {
            secondBasicHitAt = -1f;
            blinkHitAt = -1f;
            nextSwordAt = -1f;
        }
    }
}
