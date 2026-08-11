using System;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    [Serializable]
    public sealed class DialogueSpeakerPortrait
    {
        [SerializeField] private string speakerName;
        [SerializeField] private Sprite portrait;

        public string SpeakerName => speakerName;
        public Sprite Portrait => portrait;
    }

    public enum PromeDialogueExpressionKind
    {
        Default,
        Stern,
        Sigh
    }

    [Serializable]
    public sealed class PromeDialogueExpressionSet
    {
        [SerializeField] private Sprite defaultClosed;
        [SerializeField] private Sprite defaultOpen;
        [SerializeField] private Sprite sternClosed;
        [SerializeField] private Sprite sternVShape;
        [SerializeField] private Sprite sternOpen;
        [SerializeField] private Sprite sighClosed;
        [SerializeField] private Sprite sighOpen;

        public bool HasAny => defaultClosed != null || defaultOpen != null || sternClosed != null ||
                              sternVShape != null || sternOpen != null || sighClosed != null || sighOpen != null;

        public Sprite Resolve(PromeDialogueExpressionKind kind, bool mouthOpen, bool alternateMouth)
        {
            return kind switch
            {
                PromeDialogueExpressionKind.Stern when mouthOpen => alternateMouth
                    ? sternVShape ?? sternOpen ?? sternClosed
                    : sternOpen ?? sternVShape ?? sternClosed,
                PromeDialogueExpressionKind.Stern => sternClosed ?? defaultClosed,
                PromeDialogueExpressionKind.Sigh when mouthOpen => sighOpen ?? sighClosed,
                PromeDialogueExpressionKind.Sigh => sighClosed ?? defaultClosed,
                _ when mouthOpen => defaultOpen ?? defaultClosed,
                _ => defaultClosed
            };
        }
    }

    /// <summary>
    /// Scene-facing adapter for a dialogue-window asset. Attach it to the pre-placed
    /// asset root and assign labels exposing a writable string 'text' property.
    /// </summary>
    public sealed class DialogueViewModule : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Component stageLabel;
        [SerializeField] private Component dialogueLabel;
        [SerializeField] private Component continueLabel;
        [Header("Speaker Presentation")]
        [SerializeField] private GameObject leftSpeakerRoot;
        [SerializeField] private GameObject rightSpeakerRoot;
        [SerializeField] private Component leftSpeakerLabel;
        [SerializeField] private Component rightSpeakerLabel;
        [SerializeField] private Image leftPortraitImage;
        [SerializeField] private Image rightPortraitImage;
        [SerializeField] private string playerSpeakerName = "프로메";
        [SerializeField] private DialogueSpeakerPortrait[] speakerPortraits = Array.Empty<DialogueSpeakerPortrait>();
        [Header("Prome Expressions")]
        [SerializeField] private PromeDialogueExpressionSet promeExpressions = new PromeDialogueExpressionSet();
        [SerializeField, Min(0.08f)] private float mouthFrameSeconds = 0.16f;

        private string activeSpeaker = string.Empty;
        private PromeDialogueExpressionKind activePromeExpression;
        private float nextMouthFrameAt;
        private bool mouthOpen;
        private bool alternateMouth;

        public bool HasDialogueLabel => dialogueLabel != null;
        public bool HasSpeakerPresentation => leftSpeakerRoot != null && rightSpeakerRoot != null &&
                                              leftSpeakerLabel != null && rightSpeakerLabel != null &&
                                              leftPortraitImage != null && rightPortraitImage != null;
        public bool HasPromeExpressions => promeExpressions != null && promeExpressions.HasAny;

        public void SetVisible(bool visible)
        {
            if (panelRoot != null) panelRoot.SetActive(visible);
            if (!visible)
            {
                activeSpeaker = string.Empty;
                mouthOpen = false;
            }
        }

        public void SetStage(string value)
        {
            SetText(stageLabel, SanitizeStageDisplayName(value));
        }

        public static string SanitizeStageDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var result = value.Trim();
            var markerIndex = result.IndexOf("TUTO_", StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
                markerIndex = result.IndexOf("TUTO-", StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0) return result;

            var separatorIndex = result.LastIndexOf('·', markerIndex);
            result = separatorIndex >= 0
                ? result.Substring(0, separatorIndex)
                : result.Substring(0, markerIndex);
            return result.Trim().TrimEnd('·', '-', '|').TrimEnd();
        }

        public void SetDialogue(string value)
        {
            var speaker = string.Empty;
            var dialogue = value ?? string.Empty;
            var separatorIndex = dialogue.IndexOf(':');
            if (separatorIndex > 0)
            {
                speaker = dialogue.Substring(0, separatorIndex).Trim();
                dialogue = dialogue.Substring(separatorIndex + 1).TrimStart();
                SetSpeaker(speaker);
            }

            SetText(dialogueLabel, dialogue);
            if (activeSpeaker == playerSpeakerName && promeExpressions != null && promeExpressions.HasAny)
            {
                activePromeExpression = ResolvePromeExpression(dialogue);
                mouthOpen = false;
                alternateMouth = false;
                nextMouthFrameAt = Time.unscaledTime + mouthFrameSeconds;
                ApplyPromeExpression();
            }
        }

        public void SetContinue(string value)
        {
            SetText(continueLabel, value);
        }

        public void SetSpeaker(string speaker)
        {
            var hasSpeaker = !string.IsNullOrWhiteSpace(speaker) && speaker != "시스템";
            var isPlayer = hasSpeaker && speaker == playerSpeakerName;
            activeSpeaker = hasSpeaker ? speaker : string.Empty;

            if (leftSpeakerRoot != null) leftSpeakerRoot.SetActive(hasSpeaker && isPlayer);
            if (rightSpeakerRoot != null) rightSpeakerRoot.SetActive(hasSpeaker && !isPlayer);

            if (isPlayer)
            {
                SetText(leftSpeakerLabel, speaker);
                SetPortrait(leftPortraitImage, speaker);
            }
            else if (hasSpeaker)
            {
                SetText(rightSpeakerLabel, speaker);
                SetPortrait(rightPortraitImage, speaker);
            }
        }

        private void Update()
        {
            if (activeSpeaker != playerSpeakerName || promeExpressions == null || !promeExpressions.HasAny ||
                panelRoot == null || !panelRoot.activeInHierarchy || Time.unscaledTime < nextMouthFrameAt)
                return;

            mouthOpen = !mouthOpen;
            if (mouthOpen) alternateMouth = !alternateMouth;
            nextMouthFrameAt = Time.unscaledTime + mouthFrameSeconds;
            ApplyPromeExpression();
        }

        private void ApplyPromeExpression()
        {
            if (leftPortraitImage == null) return;
            var sprite = promeExpressions.Resolve(activePromeExpression, mouthOpen, alternateMouth);
            if (sprite == null) return;
            leftPortraitImage.sprite = sprite;
            leftPortraitImage.preserveAspect = true;
            leftPortraitImage.color = Color.white;
        }

        public static PromeDialogueExpressionKind ResolvePromeExpression(string dialogue)
        {
            if (string.IsNullOrWhiteSpace(dialogue)) return PromeDialogueExpressionKind.Default;
            if (dialogue.Contains("하…") || dialogue.Contains("하...") || dialogue.Contains("휴…") ||
                dialogue.Contains("한숨"))
                return PromeDialogueExpressionKind.Sigh;

            string[] sternKeywords =
            {
                "습격", "적", "위험", "전투", "싸", "헬테", "당장", "막아", "도망", "공격"
            };
            foreach (var keyword in sternKeywords)
                if (dialogue.Contains(keyword))
                    return PromeDialogueExpressionKind.Stern;
            return PromeDialogueExpressionKind.Default;
        }

        private void SetPortrait(Image portraitImage, string speaker)
        {
            if (portraitImage == null) return;
            var portrait = ResolveSpeakerPortrait(speaker);
            portraitImage.sprite = portrait;
            portraitImage.preserveAspect = true;
            portraitImage.color = portrait != null ? Color.white : ResolveSpeakerColor(speaker);
        }

        private Sprite ResolveSpeakerPortrait(string speaker)
        {
            if (speakerPortraits == null) return null;
            foreach (var entry in speakerPortraits)
                if (entry != null && entry.SpeakerName == speaker)
                    return entry.Portrait;
            return null;
        }

        private static Color ResolveSpeakerColor(string speaker)
        {
            return speaker switch
            {
                "프로메" => new Color(0.39f, 0.88f, 0.83f, 1f),
                "에온" => new Color(0.88f, 0.34f, 0.31f, 1f),
                "아르온" => new Color(0.31f, 0.50f, 0.88f, 1f),
                "엘륨" => new Color(0.91f, 0.76f, 0.25f, 1f),
                "테우스" => new Color(0.35f, 0.94f, 0.66f, 1f),
                "크리온" => new Color(0.94f, 0.54f, 0.76f, 1f),
                "헬테" => new Color(0.58f, 0.61f, 0.66f, 1f),
                _ => new Color(0.55f, 0.60f, 0.65f, 1f)
            };
        }

        private static void SetText(Component label, string value)
        {
            if (label == null) return;
            var property = label.GetType().GetProperty("text");
            if (property != null && property.CanWrite && property.PropertyType == typeof(string))
                property.SetValue(label, value, null);
        }
    }
}
