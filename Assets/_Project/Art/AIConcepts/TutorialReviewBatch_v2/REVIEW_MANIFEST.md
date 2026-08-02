# Tutorial art — four-group review batch v1

Status: applied to `Assets/Scenes/AIReview/TutorialScene_ArtCandidate.unity`
for review. The original tutorial scene remains untouched.

## Shared production prompt

Hand-drawn anime storybook steampunk-fantasy 2D game art matching the supplied
Prometheus references. Use clean slightly imperfect dark-brown or charcoal
linework, simplified readable silhouettes, two or three cel-shaded values per
material, gentle painterly wear, muted walnut/charcoal/antique-brass materials,
and restrained cyan, amber, or ruby aether accents. Avoid text, logos,
watermarks, photorealism, glossy 3D rendering, excessive micro-detail, cast
shadows, and scenery inside cutout sprites.

All cutouts were generated on a flat chroma background, converted locally to
alpha PNG, and imported as Sprite assets with PPU 256, Bilinear filtering,
Uncompressed texture data, Mipmaps disabled, Alpha Is Transparency enabled,
and Clamp wrapping.

## Group 1 — Training and reward props

| # | Asset | Target use |
|---|---|---|
| 1 | `TUTO_TR_FallingCrate_v1.png` | Three falling-crate hazard slots |
| 2 | `TUTO_TR_PulseTarget_v1.png` | Pulse training targets |
| 3 | `TUTO_TR_FallWarning_v1.png` | Falling-object warning marker |
| 4 | `TUTO_TR_CryonBootsPackage_v1.png` | Cryon boots reward pickup |
| 5 | `TUTO_TR_DoubleJumpPlatform_v1.png` | Low, landing, and high practice platforms |

## Group 2 — Player and warning VFX

| # | Asset | Target use |
|---|---|---|
| 6 | `TUTO_VFX_PulseProjectile_v1.png` | Cyan pulse projectile |
| 7 | `TUTO_VFX_RangedProjectile_v1.png` | Ruby/amber piercing projectile |
| 8 | `TUTO_VFX_HitImpact_v1.png` | Compact hit impact |
| 9 | `TUTO_VFX_AttackWarning_v1.png` | Enemy attack warning ring |

VFX use opaque cel-shaped glow bands so the alpha edge remains stable in Unity.

## Group 3 — Adamas HQ transition art

| # | Asset | Target use |
|---|---|---|
| 10 | `TUTO_HQ_LadderAssembly_v1.png` | HQ vertical ladder/service structure |
| 11 | `TUTO_HQ_ExitFrame_v1.png` | Open HQ exit vestibule frame |

These are visual-only structures. Existing traversal colliders remain separate.

## Group 4 — Character review concepts

| # | Asset | Status |
|---|---|---|
| 12 | `TUTO_CHAR_FriendA_v1.png` | Adapted from supplied orange-haired goggle character |
| 13 | `TUTO_CHAR_FriendC_Provisional_v1.png` | Provisional, no established design |
| 14 | `TUTO_CHAR_Cryon_Provisional_v1.png` | Provisional, no established design |
| 15 | `TUTO_CHAR_Theus_v1.png` | Faithful standalone adaptation from supplied Theus art |

Friend A and Theus preserve supplied design anchors. The former Friend B asset
was deleted during cleanup because its source was the Helte concept; the
canonical asset is tracked under `TutorialHelte/ReviewBatch_v1`. Friend C and
Cryon must not be treated as canon until explicitly approved.

## Candidate-scene application

- Training/reward assets are bound to the existing falling-hazard, warning,
  pulse-target, equipment-pickup, and double-jump platform slots.
- Player VFX are bound to the existing pulse/ranged projectile slots and all
  non-Helte enemy warning slots. `TUTO_VFX_HitImpact_v1.png` is driven by
  `TutorialHitImpactVfxHost` from the existing `HitConfirmed` event.
- HQ ladder and exit-frame art are visual-only children; original traversal
  colliders and serialized gameplay references remain unchanged.
- Friend A and provisional Friend C are placed behind the meeting table at
  `A-MEETING-SEAT-01` and `A-MEETING-SEAT-05`, with no colliders.
- Provisional Cryon is bound to `ART_SLOT_Cryon`; Theus is bound to the guide
  companion's `Visual/ModelSlot`.
- The removed Friend B is not instantiated. The already-applied canonical Helte
  body resolves that source concept without duplicating Helte in the meeting
  room.

Verification on 2026-08-02: 67 visual-only bindings, 15 distinct batch sprites,
zero added colliders, Scene Doctor unchanged at the 54 known baseline collider
warnings, and snapshot delta `added=67; removed=0; modified=0`.
Play-mode smoke verification also confirmed the ranged projectile, unlock-gated
pulse projectile, hit-impact playback/fade, and ordinary-enemy telegraph sprite.
The Helte warning slot remains free of the generic enemy-warning asset.

## Review board

`FourGroups_Review_v1.png`
