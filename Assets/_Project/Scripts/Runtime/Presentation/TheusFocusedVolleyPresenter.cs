using UnityEngine;
using UnityEngine.UI;
using Narthex.Gameplay;

namespace Narthex.Presentation
{
    /// <summary>Displays Theus' active focused-volley skill after its tutorial unlock.</summary>
    public sealed class TheusFocusedVolleyPresenter : MonoBehaviour
    {
        [SerializeField] private TutorialTheusRangedSupportHost supportHost;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private Text cooldownText;
        [SerializeField] private TutorialBossArenaHost bossArenaHost;

        public bool HasValidSetup => supportHost != null && canvasGroup != null && iconImage != null &&
                                     cooldownOverlay != null;

        private void Awake()
        {
            ResolveBossArena();
            if (HasValidSetup) return;
            Debug.LogError("TheusFocusedVolleyPresenter requires support, CanvasGroup, icon, and cooldown references.", this);
            enabled = false;
        }

        private void Update()
        {
            if (!HasValidSetup) return;
            ResolveBossArena();
            var encounterCompleted = bossArenaHost != null && bossArenaHost.FightCompleted;
            var visible = supportHost.gameObject.activeInHierarchy && supportHost.IsFocusedVolleyUnlocked &&
                          !encounterCompleted;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var remaining = supportHost.FocusedVolleyCooldownRemaining;
            cooldownOverlay.fillAmount = supportHost.FocusedVolleyCooldownDuration <= 0f
                ? 0f
                : Mathf.Clamp01(remaining / supportHost.FocusedVolleyCooldownDuration);
            cooldownOverlay.enabled = remaining > 0.01f;
            iconImage.color = supportHost.IsFocusedVolleyReady
                ? Color.white
                : new Color(0.58f, 0.68f, 0.72f, 0.9f);
            if (cooldownText != null)
                cooldownText.text = remaining > 0.01f ? remaining.ToString("0.0") : string.Empty;
        }

        private void ResolveBossArena()
        {
            bossArenaHost ??= FindFirstObjectByType<TutorialBossArenaHost>(FindObjectsInactive.Include);
        }
    }
}
