# Checks and acceptance

Execution evidence is in `docs/visual-authoring/IMPLEMENTATION_REPORT.md`. Managed/source checks have run; native cases below remain pending until Art reports them.

## Source and managed checks — executor

Use the existing `tests/DazPose.Rehearsal.Tests/DazPose.Rehearsal.Tests.csproj` linking convention for the new managed authoring files. Add focused authoring test classes; do not replace or weaken canonical runtime tests to match authoring semantics.

Required cases:

1. FirstContact setup -> explicit `play Intro` -> sequence preserves known placement/pose/camera. `SexyVamp` inherits `stepforward` and tracking, even though Intro is declared separately.
2. Nonvisual statements, waits, unawaited speech/gesture, profile assignments, `WaitAny`, and random expressions cannot execute or block index building/selection.
3. Nested if/else, while/repeat, same-line statements, and multiline calls retain correct source identity and scope.
4. A choice block without persistent visual writes preserves outer state. A choice with such writes flags the violation and only affected fields become unknown. Selecting inside it does not change outside state.
5. Loops are scanned once, never iterated. Infinite/runtime-dependent loops terminate analysis. Visual writes/calls in them produce appropriate uncertainty.
6. Literal explicit calls compose visual summaries; dynamic calls, multiple incoming contexts and recursion terminate with local ambiguity. No path expansion explosion.
7. Dynamic Director summaries use possible visual writes, never eligibility/RNG. Missing/invalid global source data leaves root rows/controls available and makes relevant uncertainty explicit.
8. Later explicit pose/location/camera assignments resolve only their own fields; seated pose without a known seat remains unresolved. Camera tracking depends on a known actual target.
9. Identically named pose aliases from two source owners resolve to distinct companion slots. Missing companion/pose/audio remains editable and does not block unrelated fields.
10. Standing initial/reset/sample conventions, full versus yaw rotation, locomotion inherited height, two seating rules, and camera tracking order have regression cases grounded in the shared endpoint helpers.
11. Sparse standing pose B does not retain contributions from previous clicked pose A. A nonconstant pose clip distinguishes time zero from the runtime 0.5-second convention. Pure tests verify policy; actual graph/AnimationMode behavior requires Art's native check.
12. Identical-state dialogue selection causes no pose sample/rebuild. Spatial-only changes cause zero pose samples. Pose assignment does not rebuild source index or lease.
13. Rapid select A -> B -> C projects only the current generation. Stop/source edit/close invalidates queued work. Save/resume after spatial and pose changes reapplies newest desired state.
14. Unknown values use deterministic baseline fallback; clicking order cannot change the displayed result. Field warnings remain visible.
15. Build a synthetic 10,000-cue source with long no-op runs, skipped dynamic loops and explicit visual changes. Random lookup does not visit preceding statements; verify operation counters/allocation structure as well as record timings. Repeated source edits cancel stale indexing.

Run, as appropriate to changed code:

```powershell
dotnet test tests/DazPose.Rehearsal.Tests/DazPose.Rehearsal.Tests.csproj --no-restore
dotnet test tests/DazPose.Story.Tests/DazPose.Story.Tests.csproj --no-restore
python scripts/check-unity-source.py
python scripts/native-directing-fixture.py verify
git diff --check
```

Inspect the actual source checker before using it. The existing script invokes standalone Roslyn/dotnet using installed reference inputs; it does not launch Unity. If reference inputs are missing, report that limit and ask Art to supply them through normal Unity import; do not run Unity to obtain them.

Retain existing relevant save/ledger, resource ownership, seating and camera math tests. Additional Player tests are needed if shared player endpoint code changes. Do not run Unity-based checks.

Static dependency audit: the active Studio entry, selection, binding edit, source refresh, Undo/redo, anchor polling and lifecycle paths must not construct/call rehearsal transport. Keep any remaining runtime/rehearsal code explicitly unreachable from authoring; don't achieve a passing audit by renaming the interpreter.

Record results in `docs/visual-authoring/` with HEAD and fixture code identity. Do not overwrite old failure evidence or claim old test totals as current runs.

## Responsiveness contract

Targets below are acceptance targets, not measured claims:

- A source row and its inspector respond to selection on the next editor UI update; no global busy lock.
- Warm selection with unchanged visual state: target <= 50 ms selection-to-inspector, zero native resampling.
- Warm changed-pose/shot selection: target <= 200 ms to apply the native state on Art's fixture. Record actual Scene/Game presentation observation separately from projection CPU time.
- Spatial handler: target <= 16 ms of editor callback work; release-only mode performs no full rig sample during dragging. Record Game rendering separately.
- No multi-second synchronous operation on any normal click, assignment or mark edit. If a cold clip/import dominates, show its specific pending operation, keep navigation responsive, and coalesce further requests.
- Source indexing reports build time separately and stays cancellable. It is never included anew in every cue click.
- Cue depth must not increase lookup/projection work for an equivalent shot.

Unity native calls cannot necessarily be preempted mid-call. Do not promise that a cancellation token interrupts a blocking sample; measure it and remove repeated work. Expensive preparation must happen outside OnGUI and outside a long single main-thread loop.

Report at least: index builds, index statements analyzed, cue lookup time, projection time, request-to-applied latency, pose samples, graph/lease acquisitions, spatial updates, source/dependency scans, stale requests discarded. Diagnostics are event-based and bounded, not per-frame Console spam.

Art's “cue 91” may mean a displayed line or position; no stable cue number was captured. The delivered handoff must identify actual late cues by source text and declaration, plus the exact previously slow cue if Art supplies it. Do not invent a mapping.

## Native G4 — Art only

Use Unity 6000.5.9f1 with the existing independent project:

`C:/Users/artwh/OneDrive/Documents/DazPoseWizard/.dazposewizard/native-directing/G1Project`

Scene: `Assets/__NativeDirectingFixtures/G1_FirstContact.unity`.
Menu: `DazPose > Story > Scene Director Studio`.

The executor prepares final directions matching delivered labels. Art opens/imports Unity and keeps Scene/Game visible, in Edit Mode. No screenshots are required.

### First check: responsiveness and one-pass editing

1. Click Intro's `await Lara.PersistentPose(SexyVamp)`, then a dialogue cue immediately after it. Lara remains at `stepforward`; camera tracks actual Lara focus. The dialogue click should not rebuild the pose.
2. Jump directly to Resolution's `await Lara.PersistentPose(Sassy)` near the bottom. Jump back to Intro, then to Warmup's `Lara.PersistentSitPose(Lounging)`, then to Main's `Lara.DissolveTo(Lounger, seated: Lounging)`. Each selects promptly without executing waits, choosing random cards, or asking for story responses. Resolved endpoints apply; genuine uncertainties are named.
3. Rapidly click several separated cues. The inspector always follows the selected row, and the final shot belongs to the final selection. No stale intermediate request can overwrite it.
4. At SexyVamp, drag another valid PerformerPose into its existing slot. Same cue, marks and camera remain; new pose updates. Undo and redo. Repeat with Lounging at a known seated cue.
5. Select `stepforward` through the same inspector, move with W, rotate with E. With Live drag off, handles remain smooth and preview follows on release. With it on, preview follows during drag where rendering permits. Repeat for `camera_start` and the seat position.
6. To test authored camera rotation, select setup's `Camera.StartAt(camera_start)`. Later tracked cues intentionally aim at Lara; rotating the start mark there is not a test of explicit-rotation mode.
7. Confirm direct missing-slot creation/Hierarchy binding and mark Select/Frame are available in the selected cue inspector. Keep deliberate fixture edits; use Undo for disposable experiments.

### Second check: ambiguity and protection

The executor must prepare small text-only managed fixtures and document any native scratch source exercise precisely. Do not silently alter FirstContact or global content to create an ambiguity test. If native scratch assets are needed, make creation/removal explicit Art-invoked actions confined to the independent fixture; restore its prior index/content state.

- A conditional containing only dialogue/waits is skipped for downstream state.
- A conditional with a persistent pose assignment produces a local warning downstream; it never executes a branch. A following unconditional pose resolves it.
- A dynamically reached card is selectable/editable without playing a Director loop. Its unknown incoming fields are labelled; the editor uses the documented baseline fallback.
- With one field missing/ambiguous, independent known controls and source selection remain available.
- Ctrl+S after pose/mark edits resumes the newest shot. Stop restores temporary actor/camera/material/bone state; deliberately authored marks/assignments persist.
- Undo/Redo mark and pose edits; inspect the same selected cue.
- Exercise existing forced save comparison in the independent fixture, Stop/reopen, unsaved mark Stop, window close, source/scene switch and Art-triggered compilation/reload. No preview data or transient materials may serialize; artist overrides/dirty state survive.
- Check both default SitAt baked placement and persistent seated hip alignment, actual camera tracking, player-body visibility, sparse/nonconstant pose sampling where a suitable disposable test asset is provided, wardrobe/heels/blendshapes/materials.

### Gate result

Art reports PASS / FAIL / BLOCKED and failing cue/action if any. Source checks and performance counters support that observation; they cannot replace it.

G4 stays unaccepted if navigation still grows with script depth, bindings require separate tabs, ambiguity is silently guessed, save/restore is unsafe, or native editing still stalls. Final N11 disposition waits for Art's acceptance. Play mode timing/branch/contact execution remains a separate workflow and is not part of this editor gate.
