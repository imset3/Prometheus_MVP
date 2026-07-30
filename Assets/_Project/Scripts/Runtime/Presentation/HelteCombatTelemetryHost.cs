using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Measures real Helte encounter attempts without affecting combat. The development overlay and completion log
    /// make the five-minute pacing target observable while designers tune health, damage, and pattern timing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HelteCombatTelemetryHost : MonoBehaviour
    {
        [SerializeField] private TutorialBossArenaHost arenaHost;
        [SerializeField] private CombatActorHost bossActor;
        [SerializeField] private HelteBossPatternHost patternHost;
        [SerializeField, Min(1f)] private float targetDurationSeconds = 300f;
        [SerializeField] private bool showDevelopmentOverlay;

        private float attemptStartedAt;
        private bool wasCombatActive;

        public bool HasValidSetup => arenaHost != null && bossActor != null && patternHost != null &&
                                     targetDurationSeconds > 0f;
        public bool IsTiming { get; private set; }
        public int AttemptCount { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public float LastCompletedDurationSeconds { get; private set; }
        public float TargetDurationSeconds => targetDurationSeconds;
        public int BasicComboCount { get; private set; }
        public int BlinkDashCount { get; private set; }
        public int SwordVolleyCount { get; private set; }

        private void Awake()
        {
            if (HasValidSetup) return;
            Debug.LogError(
                "HelteCombatTelemetryHost requires arena, boss actor, pattern host, and a positive target duration.",
                this);
            enabled = false;
        }

        private void OnEnable()
        {
            if (!HasValidSetup) return;
            patternHost.PatternStarted += HandlePatternStarted;
            wasCombatActive = arenaHost.CombatActive;
            if (wasCombatActive) BeginAttempt();
        }

        private void OnDisable()
        {
            if (patternHost != null) patternHost.PatternStarted -= HandlePatternStarted;
            IsTiming = false;
            wasCombatActive = false;
        }

        private void Update()
        {
            if (!HasValidSetup) return;

            var combatActive = arenaHost.CombatActive;
            if (combatActive && !wasCombatActive)
                BeginAttempt();

            if (IsTiming)
                ElapsedSeconds = Mathf.Max(0f, Time.unscaledTime - attemptStartedAt);

            if (!combatActive && wasCombatActive && IsTiming)
                EndAttempt(arenaHost.FightCompleted);

            wasCombatActive = combatActive;
        }

        private void BeginAttempt()
        {
            AttemptCount++;
            attemptStartedAt = Time.unscaledTime;
            ElapsedSeconds = 0f;
            BasicComboCount = 0;
            BlinkDashCount = 0;
            SwordVolleyCount = 0;
            IsTiming = true;
        }

        private void EndAttempt(bool completed)
        {
            ElapsedSeconds = Mathf.Max(0f, Time.unscaledTime - attemptStartedAt);
            IsTiming = false;
            if (!completed) return;

            LastCompletedDurationSeconds = ElapsedSeconds;
            var difference = LastCompletedDurationSeconds - targetDurationSeconds;
            Debug.Log(
                $"[sragon000][Helte Balance] 완료 {FormatDuration(LastCompletedDurationSeconds)} / " +
                $"목표 {FormatDuration(targetDurationSeconds)} ({difference:+0.0;-0.0;0.0}초), " +
                $"패턴 기본 {BasicComboCount}, 블링크 {BlinkDashCount}, 칼 소환 {SwordVolleyCount}",
                this);
        }

        private void HandlePatternStarted(HeltePattern pattern)
        {
            if (!IsTiming) return;
            switch (pattern)
            {
                case HeltePattern.BasicCombo:
                    BasicComboCount++;
                    break;
                case HeltePattern.BlinkDash:
                    BlinkDashCount++;
                    break;
                case HeltePattern.SummonSwords:
                    SwordVolleyCount++;
                    break;
            }
        }

        private static string FormatDuration(float seconds)
        {
            var wholeSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{wholeSeconds / 60:00}:{wholeSeconds % 60:00}";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (!showDevelopmentOverlay || !HasValidSetup) return;
            if (!arenaHost.FightStarted && AttemptCount == 0) return;

            var currentHealth = bossActor.Runtime != null ? bossActor.Runtime.CurrentHealth : 0;
            var maximumHealth = bossActor.Runtime != null ? bossActor.Runtime.MaxHealth : 0;
            var status = arenaHost.FightCompleted ? "완료" : IsTiming ? "전투 중" : "대기";
            var text =
                $"헬테 밸런스 계측 · {status}\n" +
                $"시간 {FormatDuration(ElapsedSeconds)} / 목표 {FormatDuration(targetDurationSeconds)}\n" +
                $"체력 {currentHealth} / {maximumHealth}\n" +
                $"템포 {FormatTempo(patternHost.CurrentTempo)}\n" +
                $"패턴 기본 {BasicComboCount} · 블링크 {BlinkDashCount} · 칼 {SwordVolleyCount}";

            GUI.Box(new Rect(Screen.width - 292f, 16f, 276f, 112f), text);
        }

        private static string FormatTempo(HelteCombatTempo tempo)
        {
            return tempo switch
            {
                HelteCombatTempo.FinalRush => "최종 러시",
                HelteCombatTempo.PhaseTwo => "2페이즈",
                _ => "도입"
            };
        }
#endif
    }
}
