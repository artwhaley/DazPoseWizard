# R04 — Coherent source/binding/host revision lifecycle

**Priority:** P0. **Targets:** `SceneDirectorStudioWindow.cs`, `.Rehearsal.cs`, binding/import event integration, and `RehearsalSession` teardown only as required.

## Verified defect
The window watches `.dazstory` last-write time in `Tick()` and calls `Refresh()` without stopping `rehearsal`. Changing its `StorySceneBindings` ObjectField also does not dispose the current preview. The visible tree can therefore describe a different program or scene than the running transport.

## Fixed revision/invalidation contract
1. Introduce an editor-only **rehearsal source stamp** comprising: source GUID + revision/content hash of actual compiled source, source-owned binding asset GUID and revision, *global index fingerprint* and relevant published content asset dependencies, scene GUID/host identity and a staging/binding version or explicit editor change signal. Include imported asset hashes where appropriate, not just filesystem mtimes. Calculate on known editor events/targeted refresh, not by scanning all Assets each GUI frame.
2. On a meaningful stamp mismatch, STOP/CANCEL the old rehearsal and release stale visual preview objects. Retain seed, current author scenario and source selection when safe. Display an explicit message e.g. `Story or scene changed — rebuild rehearsal`. Do not keep the old PC/highlight as if it belonged to the new program.
3. Source file change, source reimport, content binding change (pose/audio), global published card index change, host selection change, scene switch, saved scene-mark commit, Undo/Redo of a binding/mark, scene close/reload must all invalidate when they affect active rehearsal/preview.
4. Preview-only changes (orbit, scrubbing, uncommitted draft mark, forced decisions) must **not** dirty scene/asset or constantly invalidate themselves. No invalidation ping-pong during normal scrubbing or resampling.
5. Invalidation cleanup must cancel pending instruction gates, unsubscribe callbacks, drain/discard prior-generation visualization events and safely stop owned Animation Mode and audio. Unowned Unity Editor previews remain unaffected.
6. An active source *Refresh* may rebuild a new manifest/tree; a stale snapshot cannot be used for `RunToPC`, Step, Back or Director candidate inspection. Require explicit Start / Reset after invalidation.
7. Avoid a hidden auto-resume. Never start Play or save the scene automatically.

## Tests
T15–T16 and T24, including a mid-loop `.dazstory` edit, a pose/audio binding reassignment, global card addition/removal, host swap between two disposable saved scenes, and an Undo to a committed mark. Verify no serialized Lara/profile changes.

**Done:** no UI shows an old running program against newly refreshed authoring data, no leaked preview and zero unintended writes.
