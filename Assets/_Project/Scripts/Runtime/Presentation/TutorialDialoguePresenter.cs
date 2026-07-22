using System;
using System.Collections.Generic;
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
        [SerializeField] private bool showAfterDialogue;

        public string QuestId => questId;
        public string DisplayName => displayName;
        public string EnglishName => englishName;
        public string Description => description;
        public bool ShowAfterDialogue => showAfterDialogue;
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
        [SerializeField] private string continuePrompt = "SPACE: 다음";
        [SerializeField] private string closePrompt = "SPACE: 닫기";
        [SerializeField] private TutorialIntroductionDefinition[] introductionDefinitions = Array.Empty<TutorialIntroductionDefinition>();

        private string[] lines;
        private int lineIndex;
        private string currentStageId;
        private bool introductionShowing;
        private bool finishAfterIntroduction;
        private bool completingNarrative;
        private TutorialIntroductionDefinition pendingIntroduction;
        private readonly Queue<TutorialNarrativeChanged> pendingNarratives = new Queue<TutorialNarrativeChanged>();

        public bool IsShowing => introductionShowing || (lines != null && lineIndex < lines.Length);
        public int PendingNarrativeCount => pendingNarratives.Count;
        public event Action DialogueClosed;

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
            playerInputHost.AnyDialogueInputRequested += HandleAnyDialogueInput;
            if (introductionCard != null) introductionCard.Dismissed += HandleIntroductionDismissed;
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialNarrativeChanged>(HandleNarrativeChanged);
            if (introductionCard != null) introductionCard.Dismissed -= HandleIntroductionDismissed;
            if (playerInputHost != null)
            {
                playerInputHost.DialogueAdvanceRequested -= ShowNextLine;
                playerInputHost.AnyDialogueInputRequested -= HandleAnyDialogueInput;
                playerInputHost.SetDialogueInputClaimed(false);
            }
            pendingNarratives.Clear();
            completingNarrative = false;
        }

        private void HandleNarrativeChanged(TutorialNarrativeChanged message)
        {
            if (IsShowing || completingNarrative)
            {
                pendingNarratives.Enqueue(message);
                return;
            }

            BeginNarrative(message);
        }

        private void BeginNarrative(TutorialNarrativeChanged message)
        {
            currentStageId = message.StageId;
            lines = message.Lines;
            lineIndex = 0;
            introductionShowing = false;
            finishAfterIntroduction = false;
            pendingIntroduction = null;
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
                if (introduction.ShowAfterDialogue)
                    pendingIntroduction = introduction;
                else
                {
                    ShowIntroduction(introduction, false);
                    return;
                }
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
                introductionCard?.TryDismiss();
                return;
            }

            lineIndex++;
            if (!IsShowing)
            {
                dialogueView.SetVisible(false);
                if (pendingIntroduction != null)
                {
                    var introduction = pendingIntroduction;
                    pendingIntroduction = null;
                    ShowIntroduction(introduction, true);
                }
                else CompleteNarrative();
                return;
            }

            ShowCurrentLine();
        }

        private void ShowIntroduction(TutorialIntroductionDefinition introduction, bool completesNarrative)
        {
            introductionShowing = true;
            finishAfterIntroduction = completesNarrative;
            dialogueView.SetVisible(false);
            playerInputHost.SetDialogueInputClaimed(true);
            introductionCard.Show(
                introduction.DisplayName,
                introduction.EnglishName,
                introduction.Description,
                "아무 키나 누르세요");
        }

        private void HandleIntroductionDismissed()
        {
            if (!introductionShowing) return;
            introductionShowing = false;
            if (finishAfterIntroduction)
            {
                finishAfterIntroduction = false;
                CompleteNarrative();
            }
            else ShowDialogue();
        }

        private void HandleAnyDialogueInput()
        {
            if (introductionShowing) introductionCard?.TryDismiss();
        }

        private void CompleteNarrative()
        {
            dialogueView.SetVisible(false);
            playerInputHost.SetDialogueInputClaimed(false);
            lines = Array.Empty<string>();
            lineIndex = 0;
            completingNarrative = true;
            DialogueClosed?.Invoke();
            completingNarrative = false;
            TryShowPendingNarrative();
        }

        private void TryShowPendingNarrative()
        {
            if (IsShowing || completingNarrative || pendingNarratives.Count == 0) return;
            BeginNarrative(pendingNarratives.Dequeue());
        }

        private void ShowCurrentLine()
        {
            var line = lines[lineIndex] ?? string.Empty;
            var speakerSeparator = line.IndexOf(':');
            if (speakerSeparator > 0)
            {
                var speaker = line.Substring(0, speakerSeparator).Trim();
                dialogueView.SetStage(currentStageId);
                dialogueView.SetSpeaker(speaker);
                dialogueView.SetDialogue(line.Substring(speakerSeparator + 1).Trim());
            }
            else
            {
                dialogueView.SetStage(currentStageId);
                dialogueView.SetSpeaker(string.Empty);
                dialogueView.SetDialogue(line);
            }
            dialogueView.SetContinue(ResolvePrompt(lineIndex + 1 >= lines.Length));
        }

        private string ResolvePrompt(bool closing)
        {
            var fallback = closing ? closePrompt : continuePrompt;
            if (playerInputHost == null) return fallback;
            var binding = playerInputHost.GetBindingDisplayName("Jump", "SPACE");
            return $"{binding}: {(closing ? "닫기" : "다음")}";
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
