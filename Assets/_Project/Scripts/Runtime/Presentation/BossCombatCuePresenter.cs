using Narthex.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
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

            switch (state)
            {
                case HelteCombatState.PhaseTransition:
                    SetCue("2 PHASE · 패턴 강화", new Color(0.78f, 0.52f, 1f, 1f), true);
                    break;
                case HelteCombatState.BasicWindup:
                    SetCue("예고 · 연속 베기", new Color(1f, 0.78f, 0.24f, 1f), true);
                    break;
                case HelteCombatState.BlinkVanish:
                case HelteCombatState.BlinkReappear:
                    SetCue("예고 · 블링크 재진입", new Color(1f, 0.78f, 0.24f, 1f), true);
                    break;
                case HelteCombatState.CrossSlash:
                    SetCue("위험 · X 베기 판정", new Color(1f, 0.28f, 0.32f, 1f), true);
                    break;
                case HelteCombatState.SwordFocus:
                    SetCue("예고 · 칼 3개 소환", new Color(1f, 0.78f, 0.24f, 1f), true);
                    break;
                case HelteCombatState.SwordVolley:
                    SetCue("위험 · 좌 → 우 → 중앙", new Color(1f, 0.28f, 0.32f, 1f), true);
                    break;
                case HelteCombatState.Recover:
                    SetCue("기회 · 헬테 후딜", new Color(0.3f, 0.92f, 0.86f, 1f), true);
                    break;
                default:
                    SetCue(string.Empty, Color.white, false);
                    break;
            }
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
