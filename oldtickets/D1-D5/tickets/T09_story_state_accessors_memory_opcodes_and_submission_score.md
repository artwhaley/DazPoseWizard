# T09 — Story State Accessors Memory Opcodes and Submission Score

**Roadmap mapping:** D4  
**Depends on:** T07–T08

## Goal

Connect serialized story operations to the correct state scope without giving scripts unrestricted reflection access.

## Implement `StoryValueAccessor`

Explicitly support v1 keys needed by First Contact.

### Profile scope
Readable/writable:
- `submissionScore`
- `favor`
- `fact:<id>` generic fact access

Do not let arbitrary strings mutate kink/equipment arrays; those are edited through dedicated UI/service APIs.

### Contract scope
Readable:
- `pacingMultiplier`
- `preparedEquipmentCount`
- `preparedEquipment:<id>` contains/bool semantics

### Session scope
Readable/writable:
- `phase`
- `phaseCompletion`
- `mood`
- `intensityMultiplier`

### World scope
Readable/writable:
- `posture`
- `hands`
- `ankles`
- `oral`
- `anal`
- `genital`
- `clothing.shoes`
- `clothing.socks`
- `clothing.shirt`
- `clothing.pants`
- `clothing.bra`
- `clothing.underwear`
- `clothing.count`

`clothing.count` is derived read-only.

### Local scope
Arbitrary scalar local values for this story invocation only.

## Opcodes

`state.set` / `state.add` may target session/world only.
`memory.set` / `memory.add` may target profile known numeric/generic fact values only.
`local.set` / `local.add` target scene local state.

Reject invalid type/comparison at validation or execution with step context.

## Submission score

Implement one authoritative calculation:

```text
submissionScore = round(100 * enabledKinkCount / totalCatalogKinkCount)
```

Where enabled means frequency != `No`.

If catalog has zero entries, score = 0.

The frequency multiplier (1x/2x/4x) affects future selection frequency, not the submission score.

`session.calculateSubmissionScore`:
- calculates;
- writes profile submission score;
- persists profile;
- exposes the value to session/UI.

## Acceptance

No generic reflection setter exists.
Scope mistakes are explicit errors.
