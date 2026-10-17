# dazpose.story v1

Canonical content is a JSON TextAsset with explicit `schema: "dazpose.story"`, `version: 1`, a nonblank `id`, optional `title`, and `steps`. Runtime DTOs are under `Assets/DazPose/Runtime/Story/Serialization`.

Each step has globally unique `id`, explicit `op`, and optional `note`, `target`, `asset`, `key`, `panel`, `eventId`, `value`, `number`, `seconds`, `flag`, `args`, `condition`, `steps`, and `elseSteps`. Dialogue notes annotate the bound audio; notes do not synthesize speech.

Targets and assets resolve by exact symbolic ID through saved `StorySceneBindings`. JSON contains no Unity GUIDs or hierarchy paths. Missing, duplicate, null, wrong-type, unknown-opcode, and invalid state contracts fail with story/step/opcode context before execution.

At most **eight nested step blocks** are supported. A lexical nesting guard rejects deeper input before JsonUtility can silently truncate it. The DTO graph also rejects deeper graphs before serialization. JsonUtility remains the field serializer; no managed-reference JSON, reflection dispatcher, or alternate execution engine is used. Unity emits a recursive-type metadata warning when it first registers this inline DTO type; the delivered 45-step program, including its deepest argument, passed round-trip checks.

Supported control operations are `story.checkpoint`, `wait.seconds`, `flow.if`, `flow.repeat`, `event.wait`, and `event.emit`. Repeats require an integral count 0–1000. Wait durations are finite and nonnegative. Dissolve durations must be positive. A checkpoint displays its note and returns from the entire program, including nested blocks.

Performance operations are `lara.say`, `lara.pose`, `lara.gesture`, `lara.lookAt`, `lara.clearGaze`, `lara.walkTo`, `lara.sitAt`, `lara.stand`, `lara.dissolveInAt`, `lara.dissolveOut`, `lara.dissolveTo`, `player.moveTo`, `player.lookAt`, `player.track`, `player.stopTracking`, `player.body.dissolveIn`, and `player.body.dissolveOut`. Seat references use `asset` and bind the actual scene `PerformerSeat`. Pose/gesture/audio references bind `PerformerPose`, `PerformerGesture`, and `AudioClip`. Plain poses retain the configured smooth default. `lara.magicShader` is rejected because there is no matching typed shader verb.

State operations use **scoped dotted keys**:

- `state.set` / `state.add`: `session.phase`, `session.phaseCompletion`, `session.mood`, `session.intensityMultiplier`; or `world.posture`, `world.hands`, `world.ankles`, `world.oral`, `world.anal`, `world.genital`, `world.clothing.<slot>`.
- `memory.set` / `memory.add`: `profile.submissionScore`, `profile.favor`, `profile.fact:<id>`. Each successful mutation commits atomically.
- `local.set` / `local.add`: the arbitrary nonblank local `key` without a scope prefix.

Known-key set values use `value` for strings/enums, `number` for numbers, and `flag` for booleans. Fact/local sets specify one named scalar argument:

```json
{"id":"remember","op":"memory.set","key":"profile.fact:met-lara",
 "args":[{"name":"value","valueType":"bool","boolValue":true}]}
```

Adds use `number` or a numeric `value` argument. Adding requires an existing numeric value. There is no implicit conversion. Submission score must be an integer from 0–100.

Conditions use `scope` (`profile`, `contract`, `session`, `world`, `local`), unprefixed `key`, `comparison`, `valueType`, and `stringValue` / `numberValue` / `boolValue`. Comparisons: `eq`, `neq`; numeric `gt`, `gte`, `lt`, `lte`; string `contains`, `notContains`. Unknown/unset facts and locals fail explicitly. Nest `flow.if` for compound conditions.

Read-only derived keys include `world.clothing.count`, `contract.pacingMultiplier`, `contract.preparedEquipmentCount`, and boolean `contract.preparedEquipment:<id>`. Physical enums use their exact named strings. Clothing slots are `shoes`, `socks`, `shirt`, `pants`, `bra`, `underwear`.

UI/session operations: `ui.showKinks`, `ui.showEquipment`, `ui.showClothing`, `ui.message`, `session.calculateSubmissionScore`, `session.selectPreparedEquipment`. Message title uses optional `panel`; body uses `value`; an optional string `confirm` argument overrides Continue. Fixed message substitutions are `{submissionScore}` and `{preparedEquipment}`. Equipment selection uses `number` as count and reports selected equipment only when nonempty.

Events are invocation-owned rendezvous without queued history. `StoryProgramSequence.EmitEvent` forwards external UI/script events only to the currently active run; the modal service also returns a finite awaited confirmation result. Stop closes pending prompts and event waits. Restart invokes step 1 with fresh transient state. A visibility-in opcode requires a hidden body; use Restart Scene after stopping an already revealed intro.

Profile schema v1 stores preferences, score, favor, facts, assignment placeholders, and up to 128 history summaries at `DazPose/player-profile.json` under persistentDataPath. It has a separate service and file from `game-settings.json` and `scene-tuning.json`. Corrupt input remains recoverable; explicit recovery commits retain a timestamped backup. Preferences commit only on Apply. Clothing is session-only.

D6 cards, D7 Director behavior, assignment gameplay, and a block editor remain outside this packet.
