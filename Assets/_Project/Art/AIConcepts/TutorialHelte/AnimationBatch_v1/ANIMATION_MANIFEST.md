# Helte boss animation — production batch v1

Status: generated, imported, and connected to `TutorialScene_ArtCandidate.unity`.

## Animation set

| Motion | Frames | Timing | Loop | Intent |
|---|---:|---:|---|---|
| Idle | 8 | 8 × 140 ms | Yes | Breathing, attention shift, tail/coat settle |
| Attack | 8 | 760 ms | No | Anticipation, first slash, recoil, cross slash, recovery |
| Dash | 8 | 770 ms | No | Compression, push-off, airborne acceleration, planted brake |
| Hit | 8 | 860 ms | No | Chest recoil, rear-foot catch, guarded recovery |
| Death | 8 | 1,530 ms | No | Knees buckle, saber drop, grounded non-gory final pose |

Every action uses a fixed 512 × 512 canvas and 256 PPU. Hair, cape, coat hem,
pocket watch, feather charm, belt cords, and the heavy fox tail use delayed
follow-through so the body does not move as a rigid cutout.

## Unity assets

- `UnityGenerated/Helte_Idle.anim`
- `UnityGenerated/Helte_Attack.anim`
- `UnityGenerated/Helte_Dash.anim`
- `UnityGenerated/Helte_Hit.anim`
- `UnityGenerated/Helte_Death.anim`
- `UnityGenerated/HelteBoss.controller`

The controller exposes 26 exact runtime states. All 25 values of
`HelteCombatState` resolve to an existing state (`Disabled` and `Waiting` share
`Idle`); `Hit` and `Death` are driven by combat events.

The active `HelteBoss.controller` uses 23 additional pattern subclips under
`UnityGenerated/PatternClips`. The superseded pre-pattern-sync controller was
removed after the active controller passed pattern mapping and Play Mode smoke
validation.

## Pattern synchronization

| Pattern flow | Exact animation states |
|---|---|
| Basic combo | `BasicWindup` → `BasicLeftSlash` → `BasicAdvance` → `BasicRightSlash` → `Recover` |
| Blink dash | `BlinkVanish` → `BlinkReappear` → `DashTelegraph` → `DashApproach` → `CrossSlashTelegraph` → `CrossSlash` → `Recover` |
| Sword summon | `SwordFocus` → looping `SwordVolley` → `Recover` |
| Fake blink | `FakeBlinkVanish` → `FakeBlinkReappear` → looping `FakeBlinkPause` |
| Counter | `CounterTelegraph` → `CounterStance` → `CounterSucceeded` or `CounterOpen` |
| Mercy | `MercyRetreat` |
| Phase changes | `PhaseTransition`, `FinalRushTransition` |

Each short clip is timed to its matching `HelteBossPatternHost` phase. Unity's
60 Hz sampling adds at most one rendered frame (about 16.7 ms) to the requested
duration. `SwordVolley` and `FakeBlinkPause` loop because their gameplay states
can remain active for variable durations.

Gameplay timing and damage do not depend on animation events. The existing
`HelteBossPatternHost`, hitboxes, boss collider, and damage paths remain the
authority; the Animator is presentation-only.

## Scene integration

Applied only to:

`Assets/Scenes/AIReview/TutorialScene_ArtCandidate.unity`

The original scene remains untouched. One animated body child and eight
SpriteRenderer-only effect children were added. Snapshot comparison:

`added=9; removed=0; modified=0`

No colliders were added or changed. A Play Mode smoke test completed with zero
Unity console errors.

Pattern-sync validation: 25/25 enum mappings resolved, 26/26 Animator states
were available after runtime initialization, and 8/8 boss/effect SpriteRenderer
slots had valid sprites. The scene snapshot remained unchanged after the
controller upgrade (`added=0; removed=0; modified=0`).

Detached fragments caused by characters crossing source-sheet cell boundaries
were removed from all 40 production frames, and the five GIF previews were
rebuilt from the cleaned frames.

## Preview files

- `Previews/HELTE_Idle_Preview.gif`
- `Previews/HELTE_Attack_Preview.gif`
- `Previews/HELTE_Dash_Preview.gif`
- `Previews/HELTE_Hit_Preview.gif`
- `Previews/HELTE_Death_Preview.gif`

## Generation method

Built-in ImageGen was used with the approved Helte base sprite as the strict
identity reference. Each action was generated as a 4 × 2 chroma-key pose sheet,
converted locally to alpha, normalized into eight fixed-canvas frames, and then
assembled into Unity clips with hand-authored variable frame durations.
