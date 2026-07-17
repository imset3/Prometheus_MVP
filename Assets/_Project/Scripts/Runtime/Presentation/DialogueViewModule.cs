using UnityEngine;

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
        [SerializeField] private string playerSpeakerName = "프로메";

        public bool HasDialogueLabel => dialogueLabel != null;

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
            }

            SetText(dialogueLabel, dialogue);
            SetSpeaker(speaker);
        }

        public void SetContinue(string value)
        {
            SetText(continueLabel, value);
        }

        private void SetSpeaker(string speaker)
        {
            var hasSpeaker = !string.IsNullOrWhiteSpace(speaker) && speaker != "시스템";
            var isPlayer = hasSpeaker && speaker == playerSpeakerName;

            if (leftSpeakerRoot != null) leftSpeakerRoot.SetActive(hasSpeaker && isPlayer);
            if (rightSpeakerRoot != null) rightSpeakerRoot.SetActive(hasSpeaker && !isPlayer);

            if (isPlayer) SetText(leftSpeakerLabel, speaker);
            else SetText(rightSpeakerLabel, speaker);
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
