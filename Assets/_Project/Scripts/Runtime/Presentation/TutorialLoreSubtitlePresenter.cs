using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    public static class TutorialSubtitleTimingPolicy
    {
        public static float ResolveVisibleDuration(float normalDuration, float minimumDuration, int queuedCount)
        {
            if (queuedCount <= 0) return normalDuration;
            return Mathf.Max(minimumDuration, normalDuration / (queuedCount + 1f));
        }
    }

    public sealed class TutorialLoreSubtitlePresenter : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text subtitleText;
        [SerializeField] private TutorialDialoguePresenter dialoguePresenter;
        [SerializeField, Min(0f)] private float fadeDuration = 0.2f;
        [SerializeField, Min(0.1f)] private float visibleDuration = 4.2f;
        [SerializeField, Min(0.1f)] private float minimumBacklogVisibleDuration = 2.8f;

        private Coroutine presentationRoutine;
        private readonly Queue<string> pendingSubtitles = new Queue<string>();

        public bool HasValidSetup => canvasGroup != null && subtitleText != null && dialoguePresenter != null;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialLoreSubtitlePresenter requires a CanvasGroup, subtitle Text, and dialogue presenter.", this);
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
                yield return WaitForDialogueToClose();
                subtitleText.text = pendingSubtitles.Dequeue();
                yield return Fade(0f, 1f);
                var duration = TutorialSubtitleTimingPolicy.ResolveVisibleDuration(
                    visibleDuration,
                    minimumBacklogVisibleDuration,
                    pendingSubtitles.Count);
                yield return HoldVisible(duration);
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
                if (DialogueIsShowing)
                {
                    SetAlpha(0f);
                    yield return null;
                    continue;
                }

                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration)));
                yield return null;
            }
            SetAlpha(to);
        }

        private IEnumerator HoldVisible(float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (DialogueIsShowing)
                {
                    SetAlpha(0f);
                    yield return null;
                    continue;
                }

                SetAlpha(1f);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitForDialogueToClose()
        {
            while (DialogueIsShowing)
            {
                SetAlpha(0f);
                yield return null;
            }
        }

        private bool DialogueIsShowing => dialoguePresenter != null && dialoguePresenter.IsShowing;

        private void SetAlpha(float alpha)
        {
            canvasGroup.alpha = alpha;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
