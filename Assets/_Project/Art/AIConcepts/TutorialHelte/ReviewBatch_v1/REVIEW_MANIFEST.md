# Helte boss art — review batch v1

Status: approved. The static art and subsequent animation set are connected to
`Assets/Scenes/TutorialScene.unity`.

## Canonical source

- `260716.png` is the official Helte concept reference supplied by the user.
- Identity anchors preserved: blonde bob, large layered brown fox tail, forest-green gradient long coat, dark shoulder cape, black/brass belt, pocket watch, feather charm, black leggings, and brown boots.
- The two visible sword handles were expanded into a restrained dual-saber combat loadout.

## Review assets

| # | Asset | Intended Unity target |
|---|---|---|
| 1 | `HELTE_Body_Base.png` | `TutorialHelte` boss visual base |
| 2 | `HELTE_VFX_CrossSlash.png` | `CrossSlashWarning` / cross-slash impact presentation |
| 3 | `HELTE_VFX_DashPath.png` | `DashPath` stretched trail |
| 4 | `HELTE_VFX_BossWarning.png` | `BossWarning` long arena telegraph |
| 5 | `HELTE_VFX_PhaseTransition.png` | `PhaseTransition` ring/burst |
| 6 | `HELTE_Weapon_Saber.png` | `SwordVisual_Center`, `SwordVisual_Left`, `SwordVisual_Right` |

The existing `BlinkAfterimage` slot should reuse the approved body sprite with a runtime tint/alpha treatment; a duplicate painted asset is not needed.

## Style and production notes

Hand-drawn chibi anime boss art with slightly organic dark linework, flat opaque cel shapes, restrained one-step shadows, and a muted forest-green / ivory / antique-brass palette. The VFX remain readable against both bright exterior skies and darker interiors. Dash and warning assets were designed as long horizontal strips for stretching; collision and damage logic remain separate from visuals.

All cutouts were generated on a flat chroma background, converted locally to alpha PNG, and prepared for Unity as Sprite assets with PPU 256, Bilinear filtering, Uncompressed texture data, Mipmaps disabled, Alpha Is Transparency enabled, and Clamp wrapping.

## Scope boundary

This batch remains the static visual foundation. The approved Idle, Attack,
Dash, Hit, and Death animation frames are documented in
`../AnimationBatch_v1/ANIMATION_MANIFEST.md`. Boss colliders, damage paths, and
phase logic were not changed.

## Review board

`Helte_Review_v1.png`
