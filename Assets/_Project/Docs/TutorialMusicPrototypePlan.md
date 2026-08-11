# Tutorial Music Prototype Plan

## Purpose

This pass validates the musical identity before any scene wiring. The assets are
original procedural mockups intended for direction review, gameplay timing, and
adaptive-music implementation tests. They are not presented as final recorded
orchestration.

## Listening previews

| Asset | Length | Intended use |
| --- | ---: | --- |
| `MUS_TUTO_Adamas_Prototype_Loop.wav` | 57.14 s | Warm 6/8 Adamas HQ and sky-corridor identity |
| `MUS_TUTO_OuterCombat_Prototype_Preview.wav` | 61.94 s | F-to-G combat escalation demonstration |
| `MUS_BOSS_Helte_Prototype_Preview.wav` | 60.00 s | Helte opening, phase two, and final-rush escalation |

All previews are 48 kHz stereo WAV, mastered to approximately `-18 dBFS RMS`,
with a zero-valued loop boundary to prevent clicks.

## Runtime layer assets

The outer-combat preview is assembled from synchronized base and intensity
loops. The Helte preview is assembled from synchronized base, phase-two, and
final-rush loops. Every layer in a family has the exact same sample length and
must start at the same DSP time.

Suggested state mapping:

| Music family | Layer | Runtime condition |
| --- | --- | --- |
| Outer combat | Base | F/G encounter active |
| Outer combat | Intensity | G second group or equivalent high-density state |
| Helte | Base | Fight start through 55% health |
| Helte | Phase2 | `PhaseTransition`, active below 55% health |
| Helte | Final | `FinalRushTransition`, active below 20% health |

Do not restart the base clip when adding a layer. Start every layer together at
zero volume and fade the relevant source up on a bar boundary.

## Unity import settings

The Standalone override uses Vorbis, quality `0.75`, preserved sample rate, and
`Compressed In Memory`. This is intentional for prototype layer synchronization.
Mobile settings should be decided after memory and loop tests on target hardware.

## Source

`Tools/Audio/generate_music_prototypes.py` contains the deterministic renderer.
It uses a fixed random seed and can recreate every WAV asset without third-party
samples or copyrighted source recordings.

## Integration status

The direction review is approved and the adaptive runtime pass is integrated in
`TutorialScene`.

- `TutorialMusicDirector` crossfades the Adamas and outer-combat families from
  `TutorialLocationChanged` events.
- The outer intensity stem fades in for G-stage/high-density combat.
- Helte's three synchronized stems follow boss phase transition and final-rush
  states without restarting the base loop.
- Save data controls the music volume, and the mercy state applies a temporary
  music duck.
- Six pre-placed `AudioSource` children live under `TutorialAudioRoot`; scene
  setup is reproducible with the `audio.music.apply` AI Toolkit command.

The scene pass completed the AI Scene Toolkit snapshot, dry-run, mutation,
doctor-scan, and comparison workflow. The next review gate is an in-context mix
pass: dialogue/SFX masking, transition timing during real encounter pacing, and
mobile memory/loop behavior.
