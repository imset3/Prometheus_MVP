using System.Collections;
using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Gates the tutorial boss until the player has fully entered the arena.
    /// All visuals are replaceable scene slots; this host only owns encounter state.
    /// </summary>
    public sealed class TutorialBossArenaHost : MonoBehaviour
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
        [SerializeField] private CombatActorHost playerActor;
        [SerializeField] private Collider2D bossBodyCollider;
        [SerializeField] private EnemyAttackHost bossAttackHost;
        [SerializeField] private HelteBossPatternHost bossPatternHost;

        [Header("Replaceable presentation slots")]
        [SerializeField] private GameObject bossWarningSlot;
        [SerializeField] private GameObject[] patternLaneSlots = new GameObject[0];
        [SerializeField, Min(0f)] private float introWarningSeconds = 1.1f;

        [Header("Tutorial balance")]
        [SerializeField, Min(1)] private int bossHealthOverride = 2500;
        [SerializeField, Min(1)] private int playerHealthOverride = 500;
        [SerializeField, Min(0f)] private float rescueInvulnerabilitySeconds = 1.25f;
        [SerializeField] private MonoBehaviour guideCompanion;

        private bool fightStarted;
        private bool fightCompleted;
        private bool combatActive;
        private Coroutine introRoutine;
        private Coroutine rescueRoutine;
        private Vector2 previousPlayerPosition;
        private bool rescueUsed;
        private TheusCombatSupportHost theusSupport;

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
        public int BossHealthOverride => bossHealthOverride;
        public int PlayerHealthOverride => playerHealthOverride;
        public bool RescueUsed => rescueUsed;

        public void ResetForRetry()
        {
            if (!HasValidSetup) return;
            if (introRoutine != null) StopCoroutine(introRoutine);
            introRoutine = null;
            fightStarted = false;
            fightCompleted = false;
            rescueUsed = false;
            SetPresentationVisible(false);
            SetBossCombatEnabled(false);
            bossActor.ResetRuntime();
            bossActor.SetMaximumHealth(bossHealthOverride, true);
            ResolvePlayerActor()?.SetMaximumHealth(playerHealthOverride, true);
            bossPatternHost.ResetForEncounter();
            theusSupport?.StopSupport();
            previousPlayerPosition = playerCollider.transform.position;
        }

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
            bossActor.SetMaximumHealth(bossHealthOverride, true);
            ResolvePlayerActor();
            if (theusSupport == null)
                theusSupport = FindFirstObjectByType<TheusCombatSupportHost>();
            if (guideCompanion == null && theusSupport != null)
                guideCompanion = theusSupport;
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
            if (rescueRoutine != null) StopCoroutine(rescueRoutine);
            introRoutine = null;
            rescueRoutine = null;
            theusSupport?.StopSupport();
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
            bossActor?.SetMaximumHealth(bossHealthOverride, true);
            var playerRuntime = ResolvePlayerActor();
            playerRuntime?.SetMaximumHealth(playerHealthOverride, true);
            SetPresentationVisible(true);
            if (introWarningSeconds > 0f) yield return new WaitForSeconds(introWarningSeconds);
            SetPresentationVisible(false);
            SetBossCombatEnabled(true);
            if (theusSupport == null && guideCompanion != null)
                theusSupport = guideCompanion.gameObject.GetComponent<TheusCombatSupportHost>();
            if (theusSupport == null && guideCompanion != null)
                theusSupport = guideCompanion.gameObject.AddComponent<TheusCombatSupportHost>();
            theusSupport?.StartSupport(bossActor, playerRuntime);
            introRoutine = null;
        }

        public bool TryRescuePlayer(string playerId)
        {
            var playerRuntime = ResolvePlayerActor();
            if (!combatActive || fightCompleted || rescueUsed || playerRuntime == null ||
                playerRuntime.ActorId != playerId || playerRuntime.Runtime == null)
                return false;

            rescueUsed = true;
            playerRuntime.SetMaximumHealth(playerHealthOverride, true);
            playerRuntime.SetScriptedInvulnerability(true);
            if (rescueRoutine != null) StopCoroutine(rescueRoutine);
            rescueRoutine = StartCoroutine(ClearRescueInvulnerability(playerRuntime));
            Debug.Log("[sragon000] 테우스가 프로메를 1회 구조했습니다.", this);
            return true;
        }

        private IEnumerator ClearRescueInvulnerability(CombatActorHost playerRuntime)
        {
            if (rescueInvulnerabilitySeconds > 0f)
                yield return new WaitForSeconds(rescueInvulnerabilitySeconds);
            playerRuntime?.SetScriptedInvulnerability(false);
            rescueRoutine = null;
        }

        private void HandleBossKilled(BossKilled message)
        {
            if (!fightStarted || fightCompleted || message.BossId != bossActor.ActorId) return;

            fightCompleted = true;
            if (introRoutine != null) StopCoroutine(introRoutine);
            introRoutine = null;
            SetPresentationVisible(false);
            SetBossCombatEnabled(false);
            theusSupport?.StopSupport();
        }

        private CombatActorHost ResolvePlayerActor()
        {
            if (playerActor != null) return playerActor;
            if (playerCollider != null)
                playerActor = playerCollider.GetComponentInParent<CombatActorHost>();
            return playerActor;
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
            bossWarningSlot.SetActive(visible);
            foreach (var laneSlot in patternLaneSlots) laneSlot.SetActive(visible);
        }
    }
}
