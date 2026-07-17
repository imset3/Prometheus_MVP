# Tutorial SFX Plan

## Integration status

`StageRoot/TutorialAudioRoot/TutorialSfxCueHost` owns pre-placed `UIAudioSource` and
`WorldAudioSource`. The host is already subscribed to narrative, objective, and tutorial
completion events. Assign the first three clips below to its serialized fields when audio
assets are ready.

## Priority 1 — required for the current tutorial flow

| Cue ID | Trigger | Direction |
| --- | --- | --- |
| `UI_NARRATIVE_OPEN` | Narrative or introduction card opens | Short holographic rise; 0.3–0.6s. |
| `UI_OBJECTIVE_UPDATE` | Quest objective changes | Clear teal confirmation tick; 0.15–0.3s. |
| `UI_TUTORIAL_COMPLETE` | Helte is defeated and result UI opens | Warm resolve sting; 1.0–1.8s. |
| `UI_DIALOGUE_ADVANCE` | `F` advances a dialogue line | Soft digital tick; low in the mix. |
| `UI_DIALOGUE_CLOSE` | Last line closes or card is dismissed | Short release/confirm sound. |
| `UI_INTERACT_AVAILABLE` | Player enters boots/relay interaction range | Subtle proximity ping; do not loop. |
| `UI_INTERACT_CONFIRM` | Boots pickup or relay activation succeeds | Distinct confirm chime. |
| `UI_MODULE_TREE_OPEN` / `CLOSE` | `I` opens or closes the module tree | Layered hologram unfold/fold. |
| `UI_INVENTORY_OPEN` / `CLOSE` | `Tab` opens or closes inventory | Lighter mechanical UI cue. |

## Priority 2 — player feedback

| Cue ID | Trigger | Direction |
| --- | --- | --- |
| `PLY_JUMP`, `PLY_GLIDE_START`, `PLY_GLIDE_LOOP`, `PLY_LAND` | Movement tutorial | Crisp jump, airy glide loop, soft landing. |
| `PLY_DASH` | Dash | Fast compressed whoosh. |
| `PLY_ATTACK_SWING`, `PLY_ATTACK_HIT` | Basic attack | Separate swing and impact layers. |
| `PLY_PULSE_CAST`, `PLY_PULSE_HIT` | Narthex Pulse | Teal energy charge plus impact. |
| `PLY_DAMAGE`, `PLY_DEFEAT` | Player damage/death | Keep damage brief and readable. |

## Priority 3 — world and boss feedback

| Cue ID | Trigger | Direction |
| --- | --- | --- |
| `WRLD_GUIDE_HOVER` | Teus companion idle | Very quiet optional hologram hum. |
| `WRLD_GUIDE_BEACON` | Objective beacon appears | One-shot sonar ping, not a loop. |
| `WRLD_BOOTS_PICKUP` | Cryon boots collected | Cryogenic crystalline pickup. |
| `WRLD_RELAY_ACTIVATE` | Relay changes to player control | Mechanical power-up plus short electrical tail. |
| `BOSS_HELTE_SPAWN`, `BOSS_HELTE_SWING`, `BOSS_HELTE_BLINK`, `BOSS_HELTE_HIT`, `BOSS_HELTE_DEFEAT` | Helte encounter | Keep blink and attack telegraphs distinct. |

## Mixing rules

- UI cues should remain intelligible under combat: peak around -12 to -8 dBFS.
- Reserve stereo width and long tails for milestones, not repeated dialogue input.
- `PLY_GLIDE_LOOP` and `WRLD_GUIDE_HOVER` are the only candidate loops; all other listed
  tutorial cues should be one-shots.
- Use the user-facing `SfxVolume` setting for both pre-placed sources.
