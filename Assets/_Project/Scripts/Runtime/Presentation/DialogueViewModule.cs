using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
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

        public bool HasDialogueLabel => dialogueLabel != null;
        public bool HasSpeakerPresentation => leftSpeakerRoot != null && rightSpeakerRoot != null &&
                                              leftSpeakerLabel != null && rightSpeakerLabel != null &&
                                              leftPortraitImage != null && rightPortraitImage != null;

        public void SetVisible(bool visible)
        {
            if (panelRoot != null) panelRoot.SetActive(visible);
        }

        public void SetStage(string value)
        {
            SetText(stageLabel, value);
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
        }

        public void SetContinue(string value)
        {
            SetText(continueLabel, value);
        }

        public void SetSpeaker(string speaker)
        {
            var hasSpeaker = !string.IsNullOrWhiteSpace(speaker) && speaker != "시스템";
            var isPlayer = hasSpeaker && speaker == playerSpeakerName;

            if (leftSpeakerRoot != null) leftSpeakerRoot.SetActive(hasSpeaker && isPlayer);
            if (rightSpeakerRoot != null) rightSpeakerRoot.SetActive(hasSpeaker && !isPlayer);

            if (isPlayer)
            {
                SetText(leftSpeakerLabel, speaker);
                SetPlaceholderColor(leftPortraitImage, ResolveSpeakerColor(speaker));
            }
            else if (hasSpeaker)
            {
                SetText(rightSpeakerLabel, speaker);
                SetPlaceholderColor(rightPortraitImage, ResolveSpeakerColor(speaker));
            }
        }

        private static void SetPlaceholderColor(Image portraitImage, Color color)
        {
            if (portraitImage != null && portraitImage.sprite == null) portraitImage.color = color;
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
