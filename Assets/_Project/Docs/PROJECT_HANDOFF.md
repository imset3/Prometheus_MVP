# Prometheus MVP Project Handoff

## 1. Project Identity

- Game title: `Prometheus` (Korean: `프로메테우스`)
- Current project directory: `/Users/limseth/Unity/Unity_Projects/Prometheus_MVP`
- Main tutorial scene: `Assets/Scenes/SampleScene.unity`
- Chapter 1 connection scene: `Assets/Scenes/Chapter01.unity`
- Unity version last confirmed: `6000.3.14f1`
- Namespace and legacy assembly name: `Narthex`

`Narthex` remains in namespaces, assembly names, and several save identifiers because the project was renamed from an earlier sample. Treat `Prometheus` as the product name; do not bulk-rename namespaces unless a deliberate migration task is scheduled.

## 2. Working Principles

1. All visible UI, gameplay anchors, triggers, hitboxes, narrative roots, and buttons must be pre-placed in the Unity scene hierarchy.
2. Do not create gameplay or UI GameObjects at runtime from scripts in `Assets/_Project/Scripts/Runtime`.
3. Use serialized scene references for runtime components.
4. Keep player-facing text in Korean unless an English subtitle/name is explicitly required.
5. The tutorial scene is the current MVP target. Chapter 1 is connected but not the active development target.

## 3. Current Tutorial Flow

| Quest | Objective | Main implementation |
| --- | --- | --- |
| `QST-TUTO-001` | Opening / movement | Theus introduction card, opening dialogue |
| `QST-TUTO-002` | Jump and glide | Player movement and glide |
| `QST-TUTO-003` | Basic attack | Melee combat training |
| `QST-TUTO-004` | Dash | Dash training |
| `QST-TUTO-005` | Pulse module | Narthex Pulse introduction card and `2` key module use |
| `QST-TUTO-006` | Module tree | `I` key module tree |
| `QST-TUTO-007` | Relay / exterior | Cryon introduction, boots pickup, double jump, relay activation |
| `QST-TUTO-008` | Helte fight | Helte introduction, boss combat, tutorial completion |

Opening, Cryon, Pulse, and Helte all use the same introduction-card system. `F` closes an introduction card and resumes its original dialogue without advancing the quest.

## 4. Scene Layout

### Core Roots

- `StageRoot/StageSystems`
  - `ServiceRoot`
  - `SaveSystemHost`
  - `DevelopmentProgressResetManager`
  - Quest, combat, module, boss, tutorial completion, and chapter-transition hosts
- `PlayerRoot`
  - Input, motor, combat actor, melee attack, module use, collision and attack anchors
- `TerrainLayoutRoot`
  - Terrain spans approximately 300 world units with boundary objects and camera limits
- `NarrativeStageRoot`
  - `AdamasHeadquartersRoot`
  - `TrainingGroundNarrativeRoot`
  - `ExteriorApproachRoot`
  - `OreStorageNarrativeRoot`
- `TutorialHUD`
  - Tutorial status and health text
  - `TutorialDialoguePanel`
  - `TutorialIntroductionCard`
  - `ModuleTreePanel`
  - `InventoryPanel`
  - `InventoryOpenButton`
  - `TutorialResultOverlay`

### Important Pre-Placed Interactions

- `TutorialRelay`: activate with `F`
- `CryonBootsPickup`: collect with `F`; unlocks double jump
- `TutorialHelte`: boss encounter
- `GoalMarker`: legacy completion target; current boss completion also publishes the tutorial completion event

## 5. Player Controls

| Input | Function |
| --- | --- |
| `A / D` | Move |
| `Space` | Jump; hold in air to glide |
| `Enter` | Basic attack |
| `Left Shift` | Dash |
| `2` | Narthex Pulse module |
| `I` | Open/close module tree |
| `Tab` | Open/close inventory |
| `F` | Interact; advance dialogue while dialogue/card is active |

Input asset: `Assets/InputSystem_Actions.inputactions`.

## 6. Key Systems and Files

### Narrative and Dialogue

- `Assets/_Project/Scripts/Runtime/Gameplay/TutorialNarrativeSequenceHost.cs`
  - Applies each quest beat and publishes narrative events.
- `Assets/_Project/Scripts/Runtime/Presentation/TutorialDialoguePresenter.cs`
  - Handles dialogue lines and serialized introduction-card definitions.
- `Assets/_Project/Scripts/Runtime/Presentation/DialogueViewModule.cs`
  - Scene adapter for dialogue labels.
- `Assets/_Project/Scripts/Runtime/Presentation/DialogueIntroductionCardModule.cs`
  - Reusable pre-placed card view.

### Progression and Save

- `Assets/_Project/Scripts/Runtime/Save/SaveSystem.cs`
  - `ResetProgressForSceneStart()` clears permanent/run progression and retains settings.
- `Assets/_Project/Scripts/Runtime/Save/DevelopmentProgressResetManager.cs`
  - Development-only scene-start reset policy.
- `Assets/_Project/Scripts/Runtime/Save/SaveSystemHost.cs`
  - Loads and exposes the save system only; no longer decides reset policy.
- `Assets/_Project/Scripts/Runtime/Gameplay/TutorialBootsPickupHost.cs`
  - Persists and applies double-jump acquisition.

### Module and Inventory

- `Assets/_Project/Scripts/Runtime/Gameplay/ModuleSystemHost.cs`
- `Assets/_Project/Scripts/Runtime/Gameplay/ModuleTreeManagerHost.cs`
- `Assets/_Project/Scripts/Runtime/Presentation/ModuleTreePanelPresenter.cs`
- `Assets/_Project/Scripts/Runtime/Presentation/InventoryPanelPresenter.cs`
- `Assets/_Project/Scripts/Runtime/Presentation/InventoryPanelButtonHost.cs`

### Completion and Chapter Handoff

- `Assets/_Project/Scripts/Runtime/Gameplay/TutorialBossCompletionHost.cs`
- `Assets/_Project/Scripts/Runtime/Gameplay/TutorialBossCompletion.cs`
- `Assets/_Project/Scripts/Runtime/SceneFlow/TutorialCompletionFlowHost.cs`
- `Assets/_Project/Scripts/Runtime/SceneFlow/Chapter01TransitionHost.cs`

## 7. Development Save Reset Policy

The scene currently starts from a clean progression state each time.

- Scene component: `StageRoot/StageSystems/DevelopmentProgressResetManager`
- Current setting: `Reset Progress On Scene Start = true`
- It clears quest history, module/boss unlock records, double jump, and tutorial completion state.
- It preserves user settings such as audio and input binding data.

For production persistence, disable the component or uncheck `Reset Progress On Scene Start`. The manager is intentionally isolated so it can be removed without editing `SaveSystemHost`.

## 8. Validation and Test Status

### Automated Tests

- Latest confirmed Unity EditMode test run: `21/21 passed`.
- Save reset has a focused test in `Assets/_Project/Scripts/Tests/CoreAndSaveTests.cs`.
- Last local assembly build completed successfully for `Narthex.Tools.csproj` and dependencies.
- The build emitted existing warnings from Unity Input System package sources only; no project compile errors.

### Scene Validator

- File: `Assets/_Project/Scripts/Editor/TutorialSceneValidator.cs`
- Unity menu: `Narthex/Validation/Validate Active Tutorial Scene`
- Validates key systems, terrain, player, boss, narrative roots, dialogue, introduction card, inventory controls, and the development save reset manager.

Run this validator and the EditMode suite after moving or recreating the tutorial scene in a new project.

## 9. Remaining Work

### Highest Priority

1. Import or recreate the tutorial scene in the new project while preserving all serialized references.
2. Run the scene validator, then complete an end-to-end manual tutorial playthrough.
3. Confirm the development save-reset manager is enabled only while repeated tutorial testing is desired.

### Visual Pass (Deferred by Current Direction)

1. Replace primitive player/enemy/terrain visuals with final 2D sprites and animation controllers.
2. Add portrait or illustration images to `TutorialIntroductionCard`.
3. Apply final panel frame/background assets to dialogue, introduction, inventory, and module-tree UI.
4. Create the planned sprite-sheet automation tool: input a grid such as `4 x 4` or `6 x 10`, slice frames, remove background, and create animation assets.

### System Expansion

1. Expand inventory from its current tutorial module view to multiple item and module slots.
2. Add further module effects and growth content based on the game design documents.
3. Expand Chapter 1 after tutorial completion flow is finalized.

## 10. Transfer Checklist

1. Copy `Assets/_Project`, `Assets/Scenes/SampleScene.unity`, `Assets/Scenes/Chapter01.unity`, and `Assets/InputSystem_Actions.inputactions`.
2. Keep the project packages and Input System enabled.
3. Open `SampleScene`, allow Unity to compile, and resolve any missing serialized references before playing.
4. Check `StageRoot/StageSystems` contains all hosts listed above, especially `DevelopmentProgressResetManager` and `SaveSystemHost`.
5. Check `TutorialHUD` contains the dialogue, introduction, module-tree, inventory, and result UI objects.
6. Run `Narthex/Validation/Validate Active Tutorial Scene`.
7. Run EditMode tests.
8. Manually verify the control sequence through Helte completion and Chapter 1 transition.

## 11. Known Operational Note

The Unity MCP connection was unavailable to the current Codex session at the final handoff stage. The project itself compiled through its generated `.csproj` files, but after opening this project in a new Unity session, reconnect Unity MCP and rerun the Unity scene validator plus EditMode tests.
