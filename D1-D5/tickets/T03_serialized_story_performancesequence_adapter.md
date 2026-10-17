# T03 — Serialized Story PerformanceSequence Adapter

**Roadmap mapping:** D1/D2 seam  
**Depends on:** T01–T02

## Goal

Make serialized stories a first-class existing `PerformanceSequence`, preserving saved `PerformanceHost` ownership.

## Add

`Assets/DazPose/Runtime/Story/Runtime/StoryProgramSequence.cs`

```csharp
public sealed class StoryProgramSequence : PerformanceSequence
```

Serialized fields:
- `TextAsset program`
- `StorySceneBindings storyBindings`

It may cache a parsed/validated immutable document for the lifetime of the component, but must invalidate/reparse if the TextAsset reference changes in Editor/OnValidate.

`ExecuteAsync(PerformanceRun run)`:
1. require `program`;
2. parse;
3. validate against `storyBindings`;
4. obtain `GameApplication.EnsureStarted()`;
5. create a fresh `StoryRuntimeContext` for this invocation;
6. invoke `StoryRunner.ExecuteAsync`;
7. do not catch and hide non-cancellation story failures.

## Lifecycle

- Stop/cancel authority remains with `PerformanceRun`/host.
- A new Start executes the story from step 1.
- No serialized instruction stack is persisted for resume.
- Do not invent pause/resume.

## Compatibility

Do not remove or obsolete `PerformanceSequence`.
Existing C# sequences remain valid.

## Acceptance

A saved `PerformanceHost` can reference `StoryProgramSequence` exactly as it references a C# sequence.
No second host or session lifecycle exists.
