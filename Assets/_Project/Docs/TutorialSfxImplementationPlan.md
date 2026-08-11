# Tutorial SFX Implementation

## Implemented pass

The tutorial uses 43 deterministic, original synthesis prototypes under
`Assets/_Project/Audio/Sfx/Tutorial/Prototypes`.

- 13 player, impact, and enemy combat cues
- 19 Helte encounter cues
- 11 movement, UI, reward, relay, gate, and encounter cues
- 48 kHz stereo WAV, 0.18–1.35 seconds, peaks from -10 to -2 dBFS
- Standalone import override: ADPCM, Decompress On Load, preserved sample rate

`TutorialSfxDirector` routes existing gameplay and presentation events to five
pre-placed 2D AudioSources. It applies `MasterVolume × SfxVolume` on every play
and keeps player, enemy, boss, UI, and world priority/level controls separate.
Enemy attack, impact, and death cues additionally resolve their originating
actor and require its renderer center to be inside the active camera viewport.
Inactive zones and off-screen enemies therefore remain silent even though the
shared enemy AudioSource is 2D.

## Connected timing

| Area | Timing source |
| --- | --- |
| Player melee and ranged fire | `AttackStarted`, `RangedAttackStarted` |
| Impacts, damage, and deaths | `HitConfirmed`, `PlayerHit`, kill/death events |
| Enemy warning and attack | melee/ranged `PhaseChanged` events |
| Helte patterns | `HelteBossPatternHost.StateChanged` |
| Helte intro and victory | boss arena state and `BossKilled` |
| Jump and dash | tutorial `GameplaySignal` |
| Objective, dialogue input, panels, completion | tutorial/UI events |
| Item, relay, encounter clear, gate | gameplay and tower signals |

Helte cues follow a readable warning → movement → attack → opportunity grammar.
Repeated sword projectiles intentionally reuse one launch cue, while high-value
phase changes and counter success have unique signatures.

## Verification

- 21 EditMode mapping and volume tests pass.
- Scene Toolkit dry-run reported six intended configuration changes.
- Snapshot comparison reported only three new SFX source objects and one
  `TutorialAudioRoot` component change.
- Scene Doctor reported no errors; its 54 collider warnings predate this pass.
- Play Mode initialization produced no new errors.

## Next mix pass

The generated landing and panel-close cues are held for dedicated landing and
panel visibility events. Encounter-start audio also needs a public wave-start
presentation event rather than inferring internal coroutine timing.

Environmental loops should be produced after the first combat mix review:

1. Adamas meeting room air and distant machinery.
2. Corridor ventilation and emergency power texture.
3. Training-room electronics and relay hum.
4. Exterior wind with distant invasion impacts.
5. Nadir dock and Helte arena low mechanical air.

Keep ambience on separate looping sources so it can crossfade by
`TutorialLocationChanged` without consuming one-shot SFX voices.
