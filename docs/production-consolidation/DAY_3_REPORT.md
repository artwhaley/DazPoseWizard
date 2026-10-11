# Day 3 report — command lifetime, ownership and replay

Implementation and automated runtime checks passed. The user accepted Day 3 with its camera framing issue assigned to Day 4; Day 4 validation was later reported working, and Day 5 is accepted. Remediation R01–R13 is technically complete; the second-outfit toe appearance check remains pending. Physical toy hardware remains unobserved. The seven-day plan remains authoritative, and Day 7 has not started.

## Delivered behavior

- `PerformanceExecutionScope` independently observes finite commands once, captures their exact cancellation handles, reports the first failure with actor/command context, and cancels remaining owned work. Persistent gaze, tracking and held-arrival ownership have explicit cleanup.
- Finite pose, expression, movement, seating, action, gesture, visibility, teleport and spell requests settle on cancellation without disposing the actor graph. Speech cancellation targets one request and preserves the remaining FIFO. Replacement requests survive stale handles.
- Public body/root commands coordinate immediate contact release. Strict `WalkToAsync` returns `PerformanceBusyException`. Contact's internal staging walk remains part of its acquisition. Immediate cancellation works while gameplay time is paused; ordinary animated grip release remains available.
- `DissolveInAtAsync` validates and places a hidden actor before revealing it. Locomotion arrival hold is a semantic option rather than a First Contact harness workaround.
- Toy playback requires an explicit owned driver. Following a driver grants consumption only. Output cancellation changes route ownership so a cancelled ramp cannot write again. Pause submits zero intensity/configured mapped neutral position; resume restores current intent. Backend cleanup continues while paused.
- First Contact supports Stop, ResetAsync and replay in the same Play session. Reset restores declared initial placement/view, actor defaults and initial wardrobe, and clears transient owned channels. It does not snapshot all live inspector tuning values.

See `DAY_3_COMMAND_BEHAVIOR.md` for channel-by-channel semantics. This is a reusable C# lifetime primitive, not the later content authoring language or scheduler.

## Saved scene and preservation

Delivered scene: `validation/DazPoseUnityValidation/Assets/Production/Scenes/Day3CommandValidation.unity`. It contains `Day3RegressionRunner` and `FirstContactPerformance` on **Day 3 Scene Runner**, serialized required fixtures and a straight contact rod. Pressing Play starts its suite without editor injection. The build uses that exact scene.

The scene retains the accepted authored Void floor, smoke, fog, lighting, camera and volumes through Day 2's scene. Comparing YAML records found only the scene root list changed among existing records; runner/rod records are additions. The original `Assets/Scenes/FirstPerformanceVoid.unity` remains byte-identical (SHA-256 `AD9C72A4D1D37729663C99883194A14A186F5F0189C127996EFE8873D2D8FA01`). Existing dependency assets were compared before export, and only missing Day 3 scene/expression fixture assets were copied. Character visibility lights remain owned by the unchanged reusable prefab (SHA-256 `0FD83904D9B1985C40F8841F25211B88507DDA83DBD0F7571BA4D2E26E49CB06`). All 160 staged runtime/demo/validation source files match the main project using canonical line endings; the facade retains its original CRLF style.

## Automated evidence

Evidence directory: `.dazposewizard/day3-evidence`. Isolated execution project: `.dazposewizard/production-consolidation`.

- Saved-scene Unity Play Mode suite: **23 passed** (`editor-runtime.json`).
- Exact-scene Windows development player, Mono, Direct3D 12: **23 passed** (`player-initial.json`); final build/runtime evidence recorded alongside it. The final player exited 0, the build had 0 errors/13 warnings. Warnings include existing shader/editor warnings and the strict Busy test deliberately calling a command expected to throw.
- The suite includes finite cancellation, speech FIFO and pause/resume, speech/gaze overlap, movement stale-handle isolation, scope cancellation, paused dissolve, placed reveal, teleport, pending grip/strict conflict, paused wardrobe cancellation with usable follow-up queue, mask safety after wardrobe, destroyed actor isolation, mock toy transport/ownership/pause and First Contact stop/reset during speech and movement followed by natural completion.
- Toy mock suite includes timed transport, dispatch races, owner replacement, explicit owned playback, external-clock detach, paused ramp cancellation and retained intensity/mapped position intent. It returns no failures. It does not certify a real device.
- Original asset preservation: 2,790 original non-code Asset files compared, zero differences. Dependency export: 487 dependency paths / 920 local Asset and meta files compared, zero differences. These manifests are recorded separately. No recalibration, authored lighting edit, wardrobe UI/compiler redesign or partner target integration is claimed.

## Defects caught and repaired

1. **Gesture unavailable after attached hair:** runtime mask validation included every skinned renderer, so hair-only bones expanded the expected mask beyond the saved body mask. It now uses `PerformerWardrobe.Body` as the skeletal authority when present. It still rejects missing required body paths and actor-root paths; the suite verifies valid/missing/root cases after outfit change. Rigs without wardrobe retain renderer-based collection.
2. **Seating cancel settled Superseded:** cancelling its approach walk synchronously resumed the seating waiter before its request was invalidated. Seating now clears request identity before cancelling the owned approach, then settles Cancelled after cleanup.
3. **Paused wardrobe could remain hidden or resume stale work:** request-specific visibility cleanup restores stable visibility immediately and old continuations cannot clear a replacement queue request. Already committed appearance is retained.
4. **Cancelled ramp could write again:** output cancellation previously reused the ramp's owner/revision. It now atomically takes ownership before submitting zero.
5. New toy tests initially had a C# local-name collision and attempted to add a second disallowed MotionDriver to one host. Both fixture defects were fixed; only subsequent passing runs count as acceptance.

The failed attempts remain in isolated `TestOutput/day3/runtime-r1.log` through `runtime-r4.log`. The passing editor run is `runtime-r5.log`. An editor Search indexing exception observed in one failed attempt was internal to Unity and did not appear as a product runtime failure in the accepted player.

## Manual checks still needed

Use the PowerShell commands in `DAY_3_MANUAL_CHECKS.md`, or open the delivered scene in the main project and press Play. Wait for **Day 3 regressions passed**, then use **Run First Contact** for an uninterrupted visual pass. Verify the authored Void appearance, speech/lip sync, approach/seating, and Stop followed by Run during speech or movement. The next run resets before starting. Report the visible status if a run aborts.

Physical hardware is optional for the scene's no-device alternate. If checking a connected toy, verify pause neutral/resume and Stop yourself. Timed-position neutral submission uses the backend's existing 50 ms fallback; physical hold/receipt is not established by mock commands. The rod is a cancellation fixture, not Day 4's partner adapter or a new contact geometry proof.

User acceptance: accepted enough. Initial camera cannot see either actor; this makes visual verification harder. Camera position/orientation and field of view correction is explicitly required in Day 4. This acceptance does not assert individual audio/visual checks or physical hardware observations that the user did not report.
