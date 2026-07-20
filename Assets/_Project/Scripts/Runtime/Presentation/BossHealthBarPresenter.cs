using Narthex.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    public sealed class BossHealthBarPresenter : MonoBehaviour
    {
        [SerializeField] private TutorialBossArenaHost arenaHost;
        [SerializeField] private CombatActorHost bossActor;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fillImage;
        [SerializeField] private Text healthValueText;
        [SerializeField] private string bossDisplayName = "헬테";

        public bool HasValidSetup => arenaHost != null && bossActor != null && canvasGroup != null &&
                                     fillImage != null && healthValueText != null;
        public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.5f;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("BossHealthBarPresenter requires the arena, boss actor, CanvasGroup, fill, and value text.", this);
                enabled = false;
                return;
            }

            SetVisible(false);
        }

        private void Update()
        {
            var runtime = bossActor != null ? bossActor.Runtime : null;
            var shouldShow = arenaHost != null && arenaHost.CombatActive && runtime != null && runtime.IsAlive;
            SetVisible(shouldShow);
            if (!shouldShow) return;

            fillImage.fillAmount = runtime.MaxHealth > 0
                ? Mathf.Clamp01((float)runtime.CurrentHealth / runtime.MaxHealth)
                : 0f;
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
