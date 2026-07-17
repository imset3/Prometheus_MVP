# Prometheus MVP TODO

## Tutorial Character Introduction Card

Add a short character or system introduction card during tutorial dialogue, following the reference layout:

- Pause the dialogue sequence at the first meaningful appearance of a character or system.
- Show a centered, translucent cyan information card over the gameplay scene.
- Include a portrait or scene illustration, Korean display name, optional English name, and a concise 1-3 line explanation.
- Advance or close with the same dialogue input. Closing the card resumes the current tutorial dialogue without advancing the quest.
- Use pre-placed UI objects under `TutorialHUD`; do not create the card, portrait, or text at runtime.
- Build the card as a reusable `DialogueIntroductionCardModule` so Theus, Cryon, Helte, modules, and world records can share the layout.

### Initial Content

1. [x] Theus introduction: after the Adamas headquarters opening dialogue. Implemented as the pre-placed `TutorialHUD/TutorialIntroductionCard`; `F` closes the card and resumes the opening dialogue.
2. [x] Cryon introduction: at the exterior-area transition immediately before the boots pickup objective. Uses the shared card definition list on `TutorialDialoguePresenter`.
3. [x] Narthex pulse module introduction: at the start of `QST-TUTO-005`, before the pulse training beat.
4. [x] Helte introduction: at the start of `QST-TUTO-008`, before the ore-storage encounter.

### Required Asset Fields

- Background panel and frame image
- Portrait or illustration image
- Korean name label
- Optional English name label
- Description label
- Continue prompt label
