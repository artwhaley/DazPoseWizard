# N06 — Canonical cumulative visual state, stable cue visits and live re-anchoring

**Depends on:** N03–N05. **Deliverable:** `RehearsalVisualState` typed projection and one logical-to-native staging bridge.

## Execute

1. Keep `StoryMachine` / `StoryDirector` / current `RehearsalSession` and one-time encounter identity. Continue receiving exactly the *executed* `StoryCommand`s from virtual `RehearsalStoryHost`, preserving source `unit`, line/PC/visit and content owner. Make a typed, ordered **accumulator of visual intent**, not a secondary story parser/interpreter. `RehearsalVisualState` may contain references to location/pose/seat/profile symbols; it may NOT evaluate logical script conditions, Director eligibility or random draws.
2. Accumulate persistent actor root/facing, pose/posture, visibility/material mode, body root/visibility, real camera anchor and live tracking policy; nonvisual commands leave appearance unchanged. **Do not assign a terminal clip frame as gesture after-state**: awaited gesture resolves after additive masked layer weight reaches zero, so after = underlying persistent pose. Non-awaited `PlayOneShotGesture` needs a labelled observation boundary and optionally separately auditioned masked overlay, not a false settled gesture sample.
3. For **stage selected after**, replay canonical logical execution from scenario seed to the selected exact cue owner/entry/PC/encounter then apply final visual projection to NativeSceneStage. Before shows pre-cue. Back/Reset reproduces identical state; end-of-branch unvisited cue returns *unreachable* not stale unrelated image.
4. Replace `SceneDirectorStudioWindow.Revisions.cs CaptureStamp()`'s **all-scene transform + component dirty-count hashing** with separate (a) logical revision of source/content/bindings/index/host and (b) **watched referenced anchors' world matrices** (including parent chair movement, inspector input and Undo). Poll on bounded editor update or targeted notifications; stage-owned Lara bones/camera transforms must never count as authored changes. `laraFocus` is head-child: compare authored **local** transform for edits and explicitly re-aim **after** bone pose sampling; its animated world position is not an invalidating input. Adapt `StudioCorrectiveChecks` old assumption that every mark drag must change logical stamp.
5. **Fix `SceneDirectorStudioWindow.Rehearsal.cs DrainRehearsal` catch-and-continue behavior**. Process executed visual effects in order, validate, and on first visual error **propagate failure to transport** at exact owner/PC/visit, stop staging and clear/invalidates later queued effects; never let a valid movement/Director effect after an invalid cue overwrite the error display. Generation tokens prevent stale overwrite on source/host/selection changes. Dispose lease on source/host swap and Art-triggered attempted Play transition; do not run Play.
6. Provide snapshot/contract tests: standing pose -> dialogue -> camera start -> track -> Lounger -> additional dialogue; repeated card/branch path; look stays until changed; anchor shifts alter geometry only.

## Gate

- Select 3+ successive cues: never unexpectedly reset Lara/camera to defaults. Replaying same scenario creates identical projection; reapplying same projection is idempotent.
- Anchor drag never changes Director history, chosen cards, seed/draw count or scene source program.
- Source/global card/content changes invalidate projection safely; anchor move does not discard current cue or jump to start.
- Missing content/unsupported command is visible and cue marked incomplete rather than misrepresented as staged.
- Regression: invalid seat/camera visual cue followed by valid movement and Director: transport visibly stops **at** error, later effects never stage, and no RNG/card history is lost by anchor drag. World parent-chair movement restages; actor-head focus movement does not recursively invalidate.
