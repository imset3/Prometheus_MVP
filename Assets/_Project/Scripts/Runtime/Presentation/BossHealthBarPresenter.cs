using System.Globalization;
using Narthex.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    public sealed class BossHealthBarPresenter : MonoBehaviour
    {
        private const float FillCatchupPerSecond = 2.5f;
        private const float DamageFlashSeconds = 0.12f;
        private static readonly Vector2 OrnateTrackSize = new(1080f, 320f);
        private static readonly Vector2 OrnateFillSize = new(820f, 20f);
        private static readonly Color ReadableFillColor = Color.white;
        // The HUD track now uses Helte's authored warning-frame sprite. Keep its original
        // red/gold palette instead of tinting it with the old near-black HUD color.
        private static readonly Color ReadableTrackColor = Color.white;
        private static readonly Color ReadableLabelColor = new(1f, 0.985f, 0.9f, 1f);

        [SerializeField] private TutorialBossArenaHost arenaHost;
        [SerializeField] private CombatActorHost bossActor;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fillImage;
        [SerializeField] private Text healthValueText;
        [SerializeField] private string bossDisplayName = "헬테";
        [SerializeField] private HelteBossPatternHost patternHost;
        [SerializeField] private Image phaseTwoMarker;
        [SerializeField] private Image finalRushMarker;
        [SerializeField] private Text stateText;

        public bool HasValidSetup => arenaHost != null && bossActor != null && canvasGroup != null &&
                                     fillImage != null && healthValueText != null;
        public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.5f;

        private Color baseFillColor;
        private float displayedFill;
        private float damageFlashUntil;
        private int previousHealth = -1;
        private bool wasVisible;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("BossHealthBarPresenter requires the arena, boss actor, CanvasGroup, fill, and value text.", this);
                enabled = false;
                return;
            }

            ApplyReadableTheme();
            baseFillColor = ReadableFillColor;
            transform.SetAsLastSibling();
            SetVisible(false);
        }

        private void ApplyReadableTheme()
        {
            // Keep the authored sprites, but enforce enough luminance contrast for the bright
            // Zenith background used by both the tutorial and boss-development scenes.
            fillImage.color = ReadableFillColor;

            var trackImage = fillImage.transform.parent != null
                ? fillImage.transform.parent.GetComponent<Image>()
                : null;
            if (trackImage != null && trackImage != fillImage)
            {
                trackImage.color = ReadableTrackColor;
                trackImage.type = Image.Type.Simple;
                trackImage.preserveAspect = false;
                trackImage.rectTransform.sizeDelta = OrnateTrackSize;

                // Keep one authored frame and one tightly-cropped golden fill sprite. Image.Filled
                // crops the fill from the right without stretching its glow or adding another track.
                var fillRect = fillImage.rectTransform;
                fillRect.anchorMin = Vector2.one * 0.5f;
                fillRect.anchorMax = Vector2.one * 0.5f;
                fillRect.pivot = Vector2.one * 0.5f;
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.sizeDelta = OrnateFillSize;
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = 0;
                var phaseDivider = trackImage.transform.Find("PhaseDivider_ART_SLOT");
                ConfigureMarker(phaseDivider != null ? phaseDivider.GetComponent<Image>() : null, 0.5f);
                ConfigureMarker(phaseTwoMarker, 0.55f);
                ConfigureMarker(finalRushMarker, 0.20f);
            }

            healthValueText.color = ReadableLabelColor;
            var outline = healthValueText.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
        }

        private static void ConfigureMarker(Image marker, float anchorX)
        {
            if (marker == null) return;
            var rect = marker.rectTransform;
            rect.anchorMin = new Vector2(anchorX, 0.5f);
            rect.anchorMax = new Vector2(anchorX, 0.5f);
            rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(4f, 22f);
        }

        private void LateUpdate()
        {
            var runtime = bossActor != null ? bossActor.Runtime : null;
            // The boss bar is a fixed HUD element, not a world-space popup. Once the fight starts,
            // keep it visible until the arena publishes its explicit completion state. Do not let
            // transient actor hit/death states or other presenters make it blink out mid-pattern.
            var shouldShow = arenaHost != null && ShouldKeepVisible(
                arenaHost.FightStarted,
                arenaHost.FightCompleted,
                runtime != null);
            SetVisible(shouldShow);
            if (!shouldShow)
            {
                wasVisible = false;
                previousHealth = -1;
                fillImage.color = baseFillColor;
                return;
            }

            var targetFill = runtime.MaxHealth > 0
                ? Mathf.Clamp01((float)runtime.CurrentHealth / runtime.MaxHealth)
                : 0f;
            if (!wasVisible)
            {
                displayedFill = targetFill;
                wasVisible = true;
            }
            else
            {
                displayedFill = Mathf.MoveTowards(
                    displayedFill,
                    targetFill,
                    FillCatchupPerSecond * Time.unscaledDeltaTime);
            }

            if (previousHealth >= 0 && runtime.CurrentHealth < previousHealth)
                damageFlashUntil = Time.unscaledTime + DamageFlashSeconds;

            previousHealth = runtime.CurrentHealth;
            fillImage.fillAmount = displayedFill;
            fillImage.color = Time.unscaledTime < damageFlashUntil ? Color.white : baseFillColor;
            healthValueText.text = ResolveHealthLabel(
                bossDisplayName,
                runtime.CurrentHealth,
                runtime.MaxHealth);
            if (phaseTwoMarker != null)
                phaseTwoMarker.color = patternHost != null && patternHost.IsPhaseTwo
                    ? new Color(0.25f, 0.95f, 1f, 1f)
                    : new Color(1f, 1f, 1f, 0.5f);
            if (finalRushMarker != null)
                finalRushMarker.color = patternHost != null && patternHost.IsFinalRush
                    ? new Color(1f, 0.32f, 0.2f, 1f)
                    : new Color(1f, 1f, 1f, 0.5f);
            if (stateText != null)
                stateText.text = patternHost == null ? string.Empty : ResolveStateLabel(patternHost.CurrentState);
        }

        private void SetVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        public static string ResolveStateLabel(HelteCombatState state) => state switch
        {
            HelteCombatState.FakeBlinkPause => "공격 기회",
            HelteCombatState.CounterTelegraph => "반격 자세 예고",
            HelteCombatState.CounterStance => "공격 금지",
            HelteCombatState.CounterOpen => "반격 기회",
            HelteCombatState.MercyRetreat => "헬테가 거리를 둡니다",
            HelteCombatState.PhaseTransition => "PHASE 2",
            HelteCombatState.FinalRushTransition => "FINAL TEST",
            HelteCombatState.Recover => "공격 기회",
            _ => string.Empty
        };

        public static bool ShouldKeepVisible(bool fightStarted, bool fightCompleted, bool runtimeAvailable) =>
            fightStarted && !fightCompleted && runtimeAvailable;

        public static string ResolveHealthLabel(string displayName, int currentHealth, int maxHealth)
        {
            var safeMax = Mathf.Max(1, maxHealth);
            var safeCurrent = Mathf.Clamp(currentHealth, 0, safeMax);
            var percent = Mathf.RoundToInt((float)safeCurrent / safeMax * 100f);
            return $"{displayName}   {safeCurrent.ToString("N0", CultureInfo.InvariantCulture)} / " +
                   $"{safeMax.ToString("N0", CultureInfo.InvariantCulture)}   ·   {percent}%";
        }
    }
}
