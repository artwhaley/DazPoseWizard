# S07 — Rehearsal instrumentation inside existing StoryMachine

**Depends:** S06. **Goal:** common runtime semantics, not shadow VM.

Extend pure source layer with optional `IStoryRehearsalObserver` for `BeforeInstruction`, `AfterInstruction`, branch and wait/Director extension points, source call stack/entry and local state snapshots. The default null observer is a compile-time/test-visible no-op and must reproduce exact old traces/card seed/history/progress. Step gates are cancellable TaskCompletionSource barriers before next meaningful flat opcode, not editor frame polling; release exactly one instruction or continue until selected PC/source location. Expose `StorySource` line/decl and Program Entry, Card invocation stack, local expressions and host Read state as immutable preview snapshots, no leaking private mutable locals into UI. Rehearsal instrumentation may override computed `JumpFalse` only through explicit forced flag or virtual state, with source trace; normal runtime path unmodified. Support nested Card calls and repeat iteration identity. Stop/unload cancels gates/unawaited child work. Await vs fire-and-forget ownership rules remain unchanged; no unhandled Unity awaitable.

Tests pure StoryMachine with null hooks vs golden baseline, step through exact Intro, condition true/false, repeat/while, nested card call, cancellation at gate, invalid source, long zero-wait loop guard, saved FirstContact source.

**Gate:** Same canonical interpreter can pause, step and trace exact compiled program while no extra effect adapter runs live game code.
