# Tutorial Guide Model Integration

The tutorial guide is already placed in `TutorialScene` at:

`StageRoot/TutorialGuideCompanion/Visual`

`TutorialGuideCompanionHost` owns the follow offset and hover motion. Do not replace the
`TutorialGuideCompanion` root or the `Visual` transform, because the host serializes those
pre-placed scene references.

## Final-art slots

Place final assets under these children of `Visual`, keeping their local transform at zero:

- `ModelSlot`: final guide model, animator, and any model-specific renderers.
- `EffectsSlot`: hologram glow, particles, trail, or other replaceable VFX.
- `AttachmentSlot`: equipment or interaction-marker transforms attached to the guide.

The final model should face right at local scale `(1, 1, 1)`. If the source model has a
different forward convention, correct it inside `ModelSlot`; do not rotate `Visual` or the
companion root. This preserves the existing player-follow offset and hover presentation.
