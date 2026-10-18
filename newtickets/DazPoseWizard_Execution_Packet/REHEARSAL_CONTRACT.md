# Rehearsal semantics, safety, and exact interpreter parity

## Single engine

Add a small pure (non-Unity) interface `IStoryRehearsalObserver` to the existing story-language layer, optional to normal `StoryMachine`. It receives `BeforeInstruction` (entry, PC, source, instruction, safe snapshot); `AfterInstruction` result/state; decision hooks for `JumpFalse`, `Director.Select`, `WaitAny`, and event injection. Production default null must be behavior-identical to old engine (golden deterministic seed + full source oracle regression). Do not fork/copy `StoryMachine`/`StoryDirector`; build `StoryRehearsalController` that drives same machine with a rehearsal host implementing existing `IStoryExecutionHost` plus optional instrumentation interfaces. Rehearsal should not use reflection to hack its private fields.

## Step semantics

`Step` stops **before** next selected meaningful source instruction with a completion gate; author presses Step to execute exactly one, then gate re-arms. Fast calls in one source declaration may be shown individually. `Preview Step` allows time scrub of one operation via effect adapter, independent of StoryMachine's interpreter stepping. `Run To Here` replays from initial simulated setup over a selected path using injected synthetic responses, pausing if path unknown, and bounded max-instructions max-loop count; it is not a debugger time-travel promise for arbitrary open async handles.

To avoid async deadlock: observer step gate must return a cancellable `Task`; stopping/closing must complete/cancel pending gates. Async spawned effects obey channel ownership; simulated host returns completed tasks or controlled virtual durations, but still reports lifecycle start/end and occupies meaningful channels while active. Check race/cancel handling; no `await` on an unobserved Editor task. No busy wait or frame-by-frame linear stepping through long waits.

## Rehearsal state/replay

Store serialized-in-memory `RehearsalScenario`:
```
sourceGuid
sequenceName
seed
profileDraft / session state simulation
branchOverrides: map (source instruction stable key -> selected arm)
directorOverrides: map (invocation/path key -> global card ID)
eventResponses: map (source use -> selected event / virtual timestamp)
```
Use fresh simulated `SessionContract/State`, world clothing, mood etc at Reset, not `PlayerProfileService.Current` mutating object. Snapshot every meaningful source step of visible logical state and preview host commands, with a bounded count; Back restores via deterministic replay to previous gate, not trying to rewind native animation/motion drivers. Selected cards/branch overrides must be part of replay recipe for deterministic reconstruction; replay both initial and modified path should be stable. A scenario fixture is test data only and never a story source or global card membership manifest.

## Director choice parity

Refactor `StoryDirector.Select(request, optional forcedSelection)` via optional injected selector policy receiving the **canonical computed candidate list** after hard checks and weighting; default policy uses existing seeded draw and advances random identically. Rehearsal forced choice can select **only eligible positive-weight cards**, with an explicit trace. Reason/weights/cooldown/recency remain canonical. Forcing does not persist card ID in scene config. No additional RNG draw unless explicitly chosen to maintain deterministic subsequent draws as declared and tested; document whether forced draw consumes a selection RNG value (choose: **consume exactly one normal selection draw even when forced**). Preserve use history and successful phase-progress credit. Untagged explicit local cards are not candidates. A newly published global tagged card must appear in old-scene candidate list without source modification.

## Preview host allowed effects

Allowed: update disposable Lara/PlayerBody clone position/visibility/pose, safe animation curve sampling, editor-controlled AudioClip preview, virtual toy state timeline, virtual UI screen, virtual profile and contract, virtual event bus, material instances owned by preview; record source-linked effect trace. **Forbidden**: `GameApplication.EnsureStarted`, `Profile.Commit`, network/toy service calls or Intiface connection, motion output, `EditorApplication.EnterPlaymode`, actual active session creation, `EditorSceneManager.SaveScene`/`AssetDatabase.SaveAssets` during command evaluation, mutating production scene materials, prefab, source or serialized bindings.

## Rehearsal unsupported effect behavior

A command that cannot be realistically animated in EditMode must NOT silently pretend it looks correct. It may apply a correct logical state transition and show "logical only: runtime visual verification needed" in context. This is still useful for complete branching and state traversal, but S04/S05 staging acceptance requires actual Lara pose and placement animation, not logical-only mock for everything. Implement author preview for supported commands first; for all other known verbs implement a typed virtual adapter reporting capability/limitations. Unknown verb is fatal compile error.

## Integration tests

- Old non-rehearsal runs produce identical instruction trace/seeded draw/progress and cancellation to baseline.
- Under rehearsal: branches naked/1–2/>2, Director candidate traces, forced legal vs illegal card choice, loop exits, nested global cards, two climax event-race outcomes, cancellation during pending dialog/audio and destroyed window.
- Replay `Back/Run To` deterministic across fresh Studio sessions for same scenario/seed. No stale static listeners or leaked preview objects after domain reload/scene switch.
- Unity saved FirstContact and MainMenu objects and content hashes unchanged after non-committing rehearsal; `Undo` only affects explicitly committed mark/binding.
