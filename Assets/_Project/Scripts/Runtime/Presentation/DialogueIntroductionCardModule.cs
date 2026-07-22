using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    /// <summary>
    /// Renders a pre-placed tutorial character introduction card.
    /// The module only changes existing scene UI and never creates objects at runtime.
    /// </summary>
    public sealed class DialogueIntroductionCardModule : MonoBehaviour
    {
        [SerializeField] private GameObject cardRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text continueText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform cardRect;
        [SerializeField, Min(0f)] private float promptDelay = 3f;
        [SerializeField, Min(0.05f)] private float collapseDuration = 0.24f;

        private string pendingPrompt;
        private float promptAvailableAt;
        private bool showing;
        private bool promptReady;
        private bool collapsing;

        public event Action Dismissed;

        public bool HasValidSetup => cardRoot != null && titleText != null && descriptionText != null;
        public bool IsShowing => showing;
        public bool UsesTimedCollapse => promptDelay >= 3f && collapseDuration >= 0.2f &&
                                         canvasGroup != null && cardRect != null;

        public void Show(string title, string subtitle, string description, string prompt)
        {
            StopAllCoroutines();
            SetText(titleText, title);
            SetText(subtitleText, subtitle);
            SetText(descriptionText, description);
            pendingPrompt = string.IsNullOrWhiteSpace(prompt) ? "아무 키나 누르세요" : prompt;
            SetText(continueText, string.Empty);
            promptAvailableAt = Time.unscaledTime + promptDelay;
            promptReady = promptDelay <= 0f;
            collapsing = false;
            showing = true;
            if (cardRect == null && cardRoot != null) cardRect = cardRoot.GetComponent<RectTransform>();
            if (canvasGroup == null && cardRoot != null) canvasGroup = cardRoot.GetComponent<CanvasGroup>();
            if (cardRect != null) cardRect.localScale = Vector3.one;
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            if (cardRoot != null) cardRoot.SetActive(true);
            if (promptReady) SetText(continueText, pendingPrompt);
        }

        public void Hide()
        {
            StopAllCoroutines();
            showing = false;
            promptReady = false;
            collapsing = false;
            if (cardRect != null) cardRect.localScale = Vector3.one;
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            if (cardRoot != null) cardRoot.SetActive(false);
        }

        public void TryDismiss()
        {
            if (!showing || !promptReady || collapsing) return;
            StartCoroutine(CollapseRoutine());
        }

        private void Update()
        {
            if (!showing || collapsing) return;
            if (!promptReady && Time.unscaledTime >= promptAvailableAt)
            {
                promptReady = true;
                SetText(continueText, pendingPrompt);
            }

        }

        private IEnumerator CollapseRoutine()
        {
            collapsing = true;
            var elapsed = 0f;
            while (elapsed < collapseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / collapseDuration);
                var eased = progress * progress * (3f - 2f * progress);
                if (cardRect != null) cardRect.localScale = new Vector3(1f, 1f - eased, 1f);
                if (canvasGroup != null) canvasGroup.alpha = 1f - eased;
                yield return null;
            }

            showing = false;
            collapsing = false;
            if (cardRoot != null) cardRoot.SetActive(false);
            if (cardRect != null) cardRect.localScale = Vector3.one;
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            Dismissed?.Invoke();
        }

        private static void SetText(Text label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
