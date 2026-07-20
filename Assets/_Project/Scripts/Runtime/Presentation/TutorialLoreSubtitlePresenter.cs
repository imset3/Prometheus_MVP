using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    public sealed class TutorialLoreSubtitlePresenter : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text subtitleText;
        [SerializeField, Min(0f)] private float fadeDuration = 0.2f;
        [SerializeField, Min(0.1f)] private float visibleDuration = 4.2f;

        private Coroutine presentationRoutine;
        private readonly Queue<string> pendingSubtitles = new Queue<string>();

        public bool HasValidSetup => canvasGroup != null && subtitleText != null;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialLoreSubtitlePresenter requires a CanvasGroup and subtitle Text.", this);
                enabled = false;
                return;
            }

            SetAlpha(0f);
        }

        public void ShowSubtitle(string text)
        {
            if (!enabled || !HasValidSetup || string.IsNullOrWhiteSpace(text)) return;
            pendingSubtitles.Enqueue(text);
            if (presentationRoutine == null)
                presentationRoutine = StartCoroutine(PresentQueue());
        }

        private IEnumerator PresentQueue()
        {
            while (pendingSubtitles.Count > 0)
            {
                subtitleText.text = pendingSubtitles.Dequeue();
                yield return Fade(0f, 1f);
                yield return new WaitForSecondsRealtime(visibleDuration);
                yield return Fade(1f, 0f);
            }
            presentationRoutine = null;
        }

        private void OnDisable()
        {
            presentationRoutine = null;
            pendingSubtitles.Clear();
            if (HasValidSetup) SetAlpha(0f);
        }

        private IEnumerator Fade(float from, float to)
        {
            if (fadeDuration <= 0f)
            {
                SetAlpha(to);
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration)));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            canvasGroup.alpha = alpha;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
