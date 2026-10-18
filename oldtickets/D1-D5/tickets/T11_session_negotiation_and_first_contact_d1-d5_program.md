# T11 — Session Negotiation and First Contact D1-D5 Program

**Roadmap mapping:** D5 integration  
**Depends on:** T01–T10

## Goal

Build the first serialized First Contact integration through the first Director boundary.

This is the artifact the human will manually press Play on after the packet.

## Session creation

When `StoryProgramSequence` starts a new application session:
- create fresh `SessionContract` and `SessionState`;
- snapshot current kink preferences;
- assign one random seed;
- default mood = 50;
- default pacing = 1.0;
- clothing starts from the clothing prompt, not persistent profile;
- prepared/active equipment start empty.

Do not carry last session's physical state.

## Negotiation UI operations

### `ui.showKinks`
Open the reusable interests editor fullscreen.
When confirmed:
- persist edits to profile;
- refresh the session contract kink snapshot;
- continue.

### `session.calculateSubmissionScore`
Use T09 formula.

### `ui.showEquipment`
Open reusable equipment editor fullscreen.
When confirmed:
- persist;
- continue.

### `session.selectPreparedEquipment`

For First Contact:
- count = 1;
- candidates = owned/willing equipment;
- choose deterministically from session RNG;
- if none, prepared list remains empty;
- enforce compatibility helper even though count=1;
- write selected IDs to contract.

Then display a "Needed for this session" message when one item was selected.
If none are available, continue without error and without equipment-required message.

Future count/filter support belongs in the helper API now:
- desired count;
- optional required theme tags;
- optional required identity tags.
Do not build UI for 2–3 items yet.

### `ui.showClothing`

Fullscreen checkboxes:
- shoes
- socks
- shirt
- pants
- bra
- underwear

On confirm write world clothing state.

Apply First Contact branch:

- count == 0:
  - session mood += 5
  - execute positive response branch
- count 1 or 2:
  - no mood change
  - execute neutral response branch
- count > 2:
  - execute disapproval branch
  - choose least-risque currently worn item in this exact order:
    1. shoes
    2. socks
    3. shirt
    4. pants
    5. bra
    6. underwear
  - show instruction to remove that item
  - wait for Ready confirmation
  - update that clothing slot false.

Do not attempt computer vision or verification.

## First Contact program

Create:

`Assets/Production/Story/Programs/FirstContactD1D5.story.json`

Use stable step IDs.

It should express the real authored structure using currently available bound assets. Dialogue steps contain `note` with human-readable transcript even though runtime plays the bound AudioClip.

Because the repo currently has an accepted First Contact sequence with two bound speech clips, migrate/reuse those known clips/marks where useful rather than fabricating asset paths.

D1–D5 program should cover:

1. Lara/player hidden initial setup is saved scene configuration, not runtime discovery.
2. Lara dissolve in at `lara_start` or the closest existing semantic mark created for the copied scene.
3. player camera tracks Lara.
4. representative intro dialogue/movement using available audio/assets.
5. `ui.showKinks`.
6. `session.calculateSubmissionScore`.
7. display score/message.
8. `ui.showEquipment`.
9. `session.selectPreparedEquipment` count 1.
10. display prepared item if selected.
11. `ui.showClothing`.
12. clothing branch/mood update/removal prompt.
13. Lara moves/sits at lounge and reaches warmup introduction if the required existing assets are available.
14. `story.checkpoint` with message:
   `D1-D5 COMPLETE — First Director window begins here. Cards/Director are intentionally not implemented in this packet.`

Do not fake warmup cards.

## Saved integration scene

Create `Assets/Production/Scenes/FirstContactD1D5.unity` by copying the authored Void authority scene.

Configure:
- saved `PerformanceHost`;
- existing `PerformanceSceneBindings`;
- `StoryProgramSequence`;
- `StorySceneBindings`;
- prompt canvas/service;
- player body presence;
- symbolic bindings used by the program.

Do not modify original `FirstPerformanceVoid.unity`.

### Editor automation

If deterministic scene configuration is cumbersome by source YAML, add:
`DazPose → Story → Build/Refresh First Contact D1-D5`

The command:
- operates in Edit Mode only;
- copies/opens/configures/saves the target scene;
- uses Undo where appropriate for user scene edits;
- never enters Play Mode;
- is safe to rerun without duplicating objects/components.

Codex may execute this command through Unity batch mode if available. That is setup, not playback.

## Audio limitation handling

Do not synthesize voice.
Do not replace Lara.say with text-to-speech.
For lines without an authored AudioClip, represent them as `ui.message`/caption notes only in the D1–D5 prototype OR omit the nonessential line from this integration checkpoint. Keep the full transcript in the program `note` or companion reference so future audio can be bound without changing architecture.

Do not create null `lara.say` steps.

## Acceptance

Source/scene configuration is complete and ready for user manual Play Mode.
Executor does not run it.
