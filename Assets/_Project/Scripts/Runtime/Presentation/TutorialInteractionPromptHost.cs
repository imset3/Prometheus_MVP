using System;
using Narthex.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    [Serializable]
    public sealed class TutorialInteractionPromptTarget
    {
        [SerializeField] private Collider2D trigger;
        [SerializeField] private GameObject availabilityRoot;
        [SerializeField] private string promptText;
        [SerializeField] private string requiredQuestId;

        public bool IsAvailable => trigger != null && (availabilityRoot == null || availabilityRoot.activeInHierarchy);
        public Collider2D Trigger => trigger;
        public string PromptText => promptText;
        public bool IsAvailableForQuest(string currentQuestId, string fallbackQuestId)
        {
            var questId = string.IsNullOrWhiteSpace(requiredQuestId) ? fallbackQuestId : requiredQuestId;
            return IsAvailable && currentQuestId == questId;
        }
    }

    /// <summary>
    /// Shows a pre-placed interaction prompt only while the player is inside an active
    /// tutorial interaction trigger. Presentation stays separate from interaction logic.
    /// </summary>
    public sealed class TutorialInteractionPromptHost : MonoBehaviour
    {
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private Text promptLabel;
        [SerializeField] private TutorialInteractionPromptTarget[] targets = Array.Empty<TutorialInteractionPromptTarget>();
        [SerializeField] private string interactionQuestId = "QST-TUTO-007";

        public bool HasValidSetup => questSequenceHost != null && playerCollider != null && promptPanel != null && promptLabel != null;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialInteractionPromptHost requires pre-placed quest, player collider, panel, and label references.", this);
                enabled = false;
                return;
            }

            promptPanel.SetActive(false);
        }

        private void LateUpdate()
        {
            foreach (var target in targets)
            {
                if (target != null && target.IsAvailableForQuest(questSequenceHost.CurrentQuestId, interactionQuestId) &&
                    target.Trigger.Distance(playerCollider).isOverlapped)
                {
                    SetPrompt(target.PromptText);
                    return;
                }
            }

            SetPrompt(null);
        }

        private void SetPrompt(string text)
        {
            var show = !string.IsNullOrWhiteSpace(text);
            if (show) promptLabel.text = text;
            if (promptPanel.activeSelf != show) promptPanel.SetActive(show);
        }
    }
}
