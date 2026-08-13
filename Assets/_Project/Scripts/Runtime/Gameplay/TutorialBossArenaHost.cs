using System.Collections;
using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Gates the tutorial boss until the player has fully entered the arena.
    /// All visuals are replaceable scene slots; this host only owns encounter state.
    /// </summary>
    public sealed class TutorialBossArenaHost : MonoBehaviour, ITutorialRetryParticipant
    {
        [Header("Runtime")]
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private CombatSystemHost combatSystemHost;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private PlayerInputHost playerInputHost;

        [Header("Arena Entry")]
        [SerializeField] private string bossQuestId = "QST-TUTO-008";
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private Collider2D arenaStartTrigger;

        [Header("Boss")]
        [SerializeField] private CombatActorHost bossActor;
        [SerializeField] private Collider2D bossBodyCollider;
        [SerializeField] private EnemyAttackHost bossAttackHost;
        [SerializeField] private HelteBossPatternHost bossPatternHost;

        [Header("Replaceable presentation slots")]
        [SerializeField] private GameObject bossWarningSlot;
        [SerializeField] private GameObject[] patternLaneSlots = new GameObject[0];
        [SerializeField, Min(0f)] private float introWarningSeconds = 1.1f;

        private bool fightStarted;
        private bool fightCompleted;
        private bool combatActive;
        private Coroutine introRoutine;
        private Vector2 previousPlayerPosition;

        public bool HasValidSetup => serviceRoot != null && combatSystemHost != null && questSequenceHost != null &&
                                     playerInputHost != null && !string.IsNullOrWhiteSpace(bossQuestId) &&
                                     playerCollider != null &&
                                     arenaStartTrigger != null && arenaStartTrigger.isTrigger && bossActor != null &&
                                     bossBodyCollider != null && bossAttackHost != null && bossPatternHost != null &&
                                     bossWarningSlot != null && patternLaneSlots != null && patternLaneSlots.Length == 3 &&
                                     HasCompleteLaneSlots();
        public bool FightStarted => fightStarted;
        public bool FightCompleted => fightCompleted;
        public bool CombatActive => combatActive;
        public bool EncounterPresentationActive => fightStarted && !fightCompleted;
        public float IntroWarningSeconds => introWarningSeconds;

        public void ResetForRetry()
        {
            if (!HasValidSetup) return;
            if (introRoutine != null) StopCoroutine(introRoutine);
            introRoutine = null;
            fightStarted = false;
            fightCompleted = false;
            SetPresentationVisible(false);
            SetBossCombatEnabled(false);
            bossActor.ResetRuntime();
            bossPatternHost.ResetForEncounter();
            previousPlayerPosition = playerCollider.transform.position;
        }

        public void ResetForTutorialRetry() => ResetForRetry();

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialBossArenaHost requires valid entry, boss, warning, and lane-slot references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            combatSystemHost.Initialize();
            previousPlayerPosition = playerCollider.transform.position;
            SetPresentationVisible(false);
            SetBossCombatEnabled(false);
        }

        private void OnEnable()
        {
            if (!HasValidSetup) return;
            combatSystemHost.Events.Subscribe<BossKilled>(HandleBossKilled);
        }

        private void OnDisable()
        {
            combatSystemHost?.Events?.Unsubscribe<BossKilled>(HandleBossKilled);
            if (introRoutine != null) StopCoroutine(introRoutine);
            introRoutine = null;
        }

        private void Update()
        {
            var reachedArena = HasPlayerReachedArena();
            previousPlayerPosition = playerCollider.transform.position;
            if (fightStarted || fightCompleted || questSequenceHost.CurrentQuestId != bossQuestId) return;
            if (playerInputHost.IsDialogueInputClaimed) return;
            if (!reachedArena) return;

            fightStarted = true;
            introRoutine = StartCoroutine(BeginFightAfterWarning());
        }

        private bool HasPlayerReachedArena()
        {
            if (arenaStartTrigger.Distance(playerCollider).isOverlapped) return true;

            var playerPoint = (Vector2)playerCollider.transform.position;
            return arenaStartTrigger.bounds.Contains(playerPoint) ||
                   arenaStartTrigger.bounds.SqrDistance(playerPoint) <= 0.04f ||
                   TutorialTriggerSweepPolicy.Intersects(
                       arenaStartTrigger.bounds,
                       previousPlayerPosition,
                       playerPoint);
        }

        private IEnumerator BeginFightAfterWarning()
        {
            SetPresentationVisible(true);
            if (introWarningSeconds > 0f) yield return new WaitForSeconds(introWarningSeconds);
            SetPresentationVisible(false);
            SetBossCombatEnabled(true);
            introRoutine = null;
        }

        private void HandleBossKilled(BossKilled message)
        {
            if (!fightStarted || fightCompleted || message.BossId != bossActor.ActorId) return;

            fightCompleted = true;
            if (introRoutine != null) StopCoroutine(introRoutine);
            introRoutine = null;
            SetPresentationVisible(false);
            SetBossCombatEnabled(false);
        }

        private bool HasCompleteLaneSlots()
        {
            foreach (var laneSlot in patternLaneSlots)
            {
                if (laneSlot == null) return false;
            }

            return true;
        }

        private void SetBossCombatEnabled(bool combatEnabled)
        {
            combatActive = combatEnabled;
            bossBodyCollider.enabled = combatEnabled;
            // HelteBossPatternHost owns every attack during this encounter. Keep the legacy interval attacker
            // disabled so it cannot add invisible damage between authored FSM patterns.
            bossAttackHost.enabled = false;
            bossPatternHost.enabled = combatEnabled;
        }

        private void SetPresentationVisible(bool visible)
        {
            // The authored Helte warning-frame art is now the fixed HUD health-bar frame.
            // Do not show a second world-space copy over Helte during the intro.
            bossWarningSlot.SetActive(false);
            foreach (var laneSlot in patternLaneSlots) laneSlot.SetActive(visible);
        }
    }
}
