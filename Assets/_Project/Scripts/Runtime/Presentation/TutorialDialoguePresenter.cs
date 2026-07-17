using System;
using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    [Serializable]
    public sealed class TutorialIntroductionDefinition
    {
        [SerializeField] private string questId;
        [SerializeField] private string displayName;
        [SerializeField] private string englishName;
        [TextArea(2, 4)] [SerializeField] private string description;

        public string QuestId => questId;
        public string DisplayName => displayName;
        public string EnglishName => englishName;
        public string Description => description;
    }

    /// <summary>
    /// Displays narrative supplied by the pre-placed TutorialNarrativeSequenceHost.
    /// This component only updates pre-placed UI references; it never creates UI at runtime.
    /// </summary>
    public sealed class TutorialDialoguePresenter : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private DialogueViewModule dialogueView;
        [SerializeField] private DialogueIntroductionCardModule introductionCard;
        [SerializeField] private string continuePrompt = "F: 다음";
        [SerializeField] private string closePrompt = "F: 닫기";
        [SerializeField] private TutorialIntroductionDefinition[] introductionDefinitions = Array.Empty<TutorialIntroductionDefinition>();

        private string[] lines;
        private int lineIndex;
        private string currentStageId;
        private bool introductionShowing;

        public bool IsShowing => lines != null && lineIndex < lines.Length;

        private void Awake()
        {
            if (serviceRoot == null || playerInputHost == null || dialogueView == null || !dialogueView.HasDialogueLabel)
            {
                Debug.LogError("TutorialDialoguePresenter requires pre-placed ServiceRoot, PlayerInputHost, and DialogueViewModule references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            dialogueView.SetVisible(false);
            if (introductionCard != null) introductionCard.Hide();
            playerInputHost.SetDialogueInputClaimed(false);
        }

        private void OnEnable()
        {
            if (serviceRoot == null) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<TutorialNarrativeChanged>(HandleNarrativeChanged);
            playerInputHost.DialogueAdvanceRequested += ShowNextLine;
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialNarrativeChanged>(HandleNarrativeChanged);
            if (playerInputHost != null)
            {
                playerInputHost.DialogueAdvanceRequested -= ShowNextLine;
                playerInputHost.SetDialogueInputClaimed(false);
            }
        }

        private void HandleNarrativeChanged(TutorialNarrativeChanged message)
        {
            currentStageId = message.StageId;
            lines = message.Lines;
            lineIndex = 0;
            introductionShowing = false;
            if (introductionCard != null) introductionCard.Hide();
            if (lines == null || lines.Length == 0)
            {
                dialogueView.SetVisible(false);
                playerInputHost.SetDialogueInputClaimed(false);
                return;
            }

            TutorialIntroductionDefinition introduction;
            if (introductionCard != null && introductionCard.HasValidSetup && TryGetIntroduction(message.QuestId, out introduction))
            {
                introductionShowing = true;
                dialogueView.SetVisible(false);
                playerInputHost.SetDialogueInputClaimed(true);
                introductionCard.Show(
                    introduction.DisplayName,
                    introduction.EnglishName,
                    introduction.Description,
                    continuePrompt);
                return;
            }

            ShowDialogue();
        }

        private void ShowDialogue()
        {
            dialogueView.SetVisible(true);
            playerInputHost.SetDialogueInputClaimed(true);
            dialogueView.SetStage(currentStageId);
            ShowCurrentLine();
        }

        private void ShowNextLine()
        {
            if (introductionShowing)
            {
                introductionShowing = false;
                introductionCard.Hide();
                ShowDialogue();
                return;
            }

            lineIndex++;
            if (!IsShowing)
            {
                dialogueView.SetVisible(false);
                playerInputHost.SetDialogueInputClaimed(false);
                return;
            }

            ShowCurrentLine();
        }

        private void ShowCurrentLine()
        {
            dialogueView.SetDialogue(lines[lineIndex]);
            dialogueView.SetContinue(lineIndex + 1 < lines.Length ? continuePrompt : closePrompt);
        }

        private bool TryGetIntroduction(string questId, out TutorialIntroductionDefinition introduction)
        {
            introduction = null;
            if (introductionDefinitions == null) return false;

            foreach (var definition in introductionDefinitions)
            {
                if (definition == null || definition.QuestId != questId) continue;
                introduction = definition;
                return true;
            }

            return false;
        }
    }
}
