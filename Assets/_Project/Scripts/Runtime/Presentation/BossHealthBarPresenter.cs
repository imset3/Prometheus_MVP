using Narthex.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    public sealed class BossHealthBarPresenter : MonoBehaviour
    {
        private const float FillCatchupPerSecond = 2.5f;
        private const float DamageFlashSeconds = 0.12f;

        [SerializeField] private TutorialBossArenaHost arenaHost;
        [SerializeField] private CombatActorHost bossActor;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fillImage;
        [SerializeField] private Text healthValueText;
        [SerializeField] private string bossDisplayName = "헬테";

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

            baseFillColor = fillImage.color;
            SetVisible(false);
        }

        private void Update()
        {
            var runtime = bossActor != null ? bossActor.Runtime : null;
            var shouldShow = arenaHost != null && arenaHost.EncounterPresentationActive &&
                             runtime != null && runtime.IsAlive;
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
            healthValueText.text = $"{bossDisplayName}  {runtime.CurrentHealth} / {runtime.MaxHealth}";
        }

        private void SetVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
