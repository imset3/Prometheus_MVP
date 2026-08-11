using Narthex.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    public sealed class RangedAttackCooldownPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerRangedAttackHost rangedAttack;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private Text cooldownText;
        [SerializeField] private TutorialBossArenaHost bossArenaHost;

        public bool HasValidSetup => rangedAttack != null && canvasGroup != null && cooldownOverlay != null && cooldownText != null;

        private void Awake()
        {
            ResolveBossArena();
            if (HasValidSetup) return;
            Debug.LogError("RangedAttackCooldownPresenter requires ranged attack, overlay, and text references.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (!HasValidSetup) return;
            ResolveBossArena();
            var encounterCompleted = bossArenaHost != null && bossArenaHost.FightCompleted;
            canvasGroup.alpha = rangedAttack.IsAvailable && !encounterCompleted ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            var remaining = rangedAttack.CooldownRemaining;
            cooldownOverlay.fillAmount = rangedAttack.CooldownNormalized;
            cooldownOverlay.enabled = remaining > 0.01f;
            cooldownText.text = remaining > 0.01f ? remaining.ToString("0.0") : string.Empty;
        }

        private void ResolveBossArena()
        {
            bossArenaHost ??= FindFirstObjectByType<TutorialBossArenaHost>(FindObjectsInactive.Include);
        }
    }
}
