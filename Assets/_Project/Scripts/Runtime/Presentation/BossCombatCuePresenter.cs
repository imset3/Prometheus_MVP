using Narthex.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    public readonly struct BossCombatCue
    {
        public BossCombatCue(string text, Color color, bool visible)
        {
            Text = text;
            Color = color;
            Visible = visible;
        }

        public string Text { get; }
        public Color Color { get; }
        public bool Visible { get; }
    }

    /// <summary>
    /// Adds text semantics to Helte's color-coded placeholders so telegraph, active damage, recovery,
    /// and phase transition remain readable without relying on color alone.
    /// </summary>
    public sealed class BossCombatCuePresenter : MonoBehaviour
    {
        [SerializeField] private TutorialBossArenaHost arenaHost;
        [SerializeField] private HelteBossPatternHost patternHost;
        [SerializeField] private GameObject cueRoot;
        [SerializeField] private Text cueText;

        public bool HasValidSetup => arenaHost != null && patternHost != null && cueRoot != null && cueText != null;
        public string CurrentCue => cueText != null ? cueText.text : string.Empty;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("BossCombatCuePresenter requires arena, pattern, root, and text references.", this);
                enabled = false;
                return;
            }
            SetCue(string.Empty, Color.white, false);
        }

        private void OnEnable()
        {
            if (patternHost != null) patternHost.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (patternHost != null) patternHost.StateChanged -= HandleStateChanged;
            if (cueRoot != null) cueRoot.SetActive(false);
        }

        private void Update()
        {
            if (arenaHost == null || cueRoot == null) return;
            if (!arenaHost.CombatActive && cueRoot.activeSelf) cueRoot.SetActive(false);
        }

        private void HandleStateChanged(HelteCombatState state)
        {
            if (arenaHost == null || !arenaHost.CombatActive)
            {
                SetCue(string.Empty, Color.white, false);
                return;
            }

            var cue = ResolveCue(state);
            SetCue(cue.Text, cue.Color, cue.Visible);
        }

        public static BossCombatCue ResolveCue(HelteCombatState state)
        {
            var warning = new Color(1f, 0.78f, 0.24f, 1f);
            var danger = new Color(1f, 0.28f, 0.32f, 1f);
            var opportunity = new Color(0.3f, 0.92f, 0.86f, 1f);
            var movement = new Color(0.72f, 0.86f, 1f, 1f);
            var phase = new Color(0.78f, 0.52f, 1f, 1f);

            return state switch
            {
                HelteCombatState.PhaseTransition =>
                    new BossCombatCue("2 PHASE · 패턴 강화", phase, true),
                HelteCombatState.FinalRushTransition =>
                    new BossCombatCue("FINAL TEST · 헬테의 최종 시험", phase, true),
                HelteCombatState.MercyRetreat =>
                    new BossCombatCue("휴식 · 헬테가 거리를 둡니다", opportunity, true),
                HelteCombatState.BasicWindup =>
                    new BossCombatCue("예고 · 연속 베기", warning, true),
                HelteCombatState.BlinkVanish or HelteCombatState.BlinkReappear =>
                    new BossCombatCue("예고 · 블링크 재진입", warning, true),
                HelteCombatState.DashTelegraph =>
                    new BossCombatCue("예고 · 돌진 경로", warning, true),
                HelteCombatState.DashApproach =>
                    new BossCombatCue("이동 · 돌진 피해 없음", movement, true),
                HelteCombatState.CrossSlashTelegraph =>
                    new BossCombatCue("예고 · X 베기", warning, true),
                HelteCombatState.CrossSlash =>
                    new BossCombatCue("위험 · X 베기 판정", danger, true),
                HelteCombatState.SwordFocus =>
                    new BossCombatCue("예고 · 칼 3개 소환", warning, true),
                HelteCombatState.SwordVolley =>
                    new BossCombatCue("위험 · 좌 → 우 → 중앙", danger, true),
                HelteCombatState.FakeBlinkVanish or HelteCombatState.FakeBlinkReappear =>
                    new BossCombatCue("예고 · 페이크 블링크", warning, true),
                HelteCombatState.FakeBlinkPause =>
                    new BossCombatCue("기회 · 이번 블링크는 공격하지 않습니다", opportunity, true),
                HelteCombatState.CounterTelegraph =>
                    new BossCombatCue("예고 · 반격 자세", warning, true),
                HelteCombatState.CounterStance =>
                    new BossCombatCue("주의 · 지금 공격하면 밀려납니다", danger, true),
                HelteCombatState.CounterSucceeded =>
                    new BossCombatCue("반격 · 공격이 막혔습니다", danger, true),
                HelteCombatState.CounterOpen =>
                    new BossCombatCue("기회 · 카운터가 끝났습니다", opportunity, true),
                HelteCombatState.Recover =>
                    new BossCombatCue("기회 · 헬테 후딜", opportunity, true),
                _ => new BossCombatCue(string.Empty, Color.white, false)
            };
        }

        private void SetCue(string text, Color color, bool visible)
        {
            if (cueText != null)
            {
                cueText.text = text;
                cueText.color = color;
            }
            if (cueRoot != null) cueRoot.SetActive(visible);
        }
    }
}
