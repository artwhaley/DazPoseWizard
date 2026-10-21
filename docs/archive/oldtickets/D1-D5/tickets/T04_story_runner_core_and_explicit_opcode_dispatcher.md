# T04 — Story Runner Core and Explicit Opcode Dispatcher

**Roadmap mapping:** D2  
**Depends on:** T01–T03

## Goal

Execute v1 story steps deterministically using existing scoped vocabulary.

## Add

Under `Story/Runtime/`:
- `StoryRunner.cs`
- `StoryRuntimeContext.cs`
- `StoryRuntimeSnapshot.cs`
- `StoryValueAccessor.cs`

### Runner

`StoryRunner.ExecuteAsync(StoryProgramDocument, StoryRuntimeContext)` walks steps in order.

Before every step:
- update snapshot currentStepId/currentOpcode;
- verify run/session still has authority using existing cancellation behavior;
- execute explicit opcode handler.

Do not use reflection or method-name parsing.

### Mapping

Map Lara/player opcodes to existing `PerformanceRun` facade methods.

Examples:
- `lara.say` → `await run.Performer.SayAsync(bindings.RequireAsset<AudioClip>(asset))`
- `lara.walkTo` → existing scoped walk-to target
- `lara.sitAt` → existing prepared seat asset/binding
- `lara.dissolveInAt` → existing scoped dissolve
- `player.track` → existing scoped player track
- `wait.seconds` → `await run.Delay(seconds)`

For pose/gesture/seat types, inspect actual class names in current source and bind those exact types. Do not add wrapper asset types just to satisfy this packet.

### Flow

`flow.if`:
- evaluate one `StoryCondition`;
- execute `steps` or `elseSteps`.

`flow.repeat`:
- use integer `number` rounded only if it is already integral; reject fractional count in validation;
- execute nested steps;
- hard cap 1000.

### Checkpoint

`story.checkpoint`:
- update `StoryRuntimeSnapshot.Checkpoint`;
- call the optional debug/prompt presenter to display the note;
- return normally from the story program.
- This is the D1–D5 boundary mechanism. It is not an exception.

### Failure

Wrap step failures in `StoryProgramException` without destroying the original exception:
- story ID;
- step ID;
- opcode;
- inner exception.

Do not convert existing toy output behavior into story failures.

## Debug snapshot

Expose read-only current:
- story ID;
- step ID/op;
- checkpoint;
- last event;
- session phase;
- completion;
- mood;
- intensity;
- seed;
- prepared equipment;
- active equipment.

A lightweight optional overlay may read it later. Runner must not depend on overlay presence.

## Acceptance

Code is single-threaded/main-thread at the Story layer unless an existing scoped API handles its own async work.
No fire-and-forget story step tasks.
Every awaited operation is consumed exactly once.
