using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    public sealed class PromeBossSkillPresenter : MonoBehaviour
    {
        [SerializeField] private PromeBossSkillHost skillHost;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private Text valueText;

        public bool HasValidSetup => skillHost != null && canvasGroup != null && iconImage != null &&
                                     cooldownOverlay != null;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("PromeBossSkillPresenter requires skill, CanvasGroup, icon, and cooldown overlay.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!HasValidSetup) return;
            var visible = skillHost.gameObject.activeInHierarchy && skillHost.IsEncounterActive;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            cooldownOverlay.fillAmount = skillHost.CooldownDuration <= 0f
                ? 0f
                : Mathf.Clamp01(skillHost.CooldownRemaining / skillHost.CooldownDuration);
            iconImage.color = skillHost.IsReady
                ? Color.white
                : new Color(0.58f, 0.68f, 0.72f, 0.9f);
            if (valueText != null)
            {
                valueText.text = string.Empty;
                valueText.gameObject.SetActive(false);
            }
        }
    }
}
