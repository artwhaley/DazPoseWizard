# T12 — Debug Surface Static Validation and Handoff

**Roadmap mapping:** D1–D5 closeout  
**Depends on:** T01–T11

## Goal

Make the new architecture inspectable and hand it to the user without claiming playback.

## Debug surface

Add a compact optional Story debug panel/presenter visible in development builds/Editor only.

Show:
- story ID;
- current step ID/op;
- checkpoint;
- phase;
- phase completion;
- mood;
- intensity;
- random seed;
- prepared equipment;
- active equipment;
- clothing count;
- last event.

One toggle/key/button is enough.
Do not build a console or command editor.

## Logging

Log:
- story start;
- story completion/checkpoint;
- story failure with step/op context;
- profile load fallback diagnostic;
- profile save failure.

Do not log every frame.
Do not reintroduce toy warnings.

## Static validation

Before final handoff:

1. Validate all new JSON with a normal JSON parser.
2. Search production Story source for `TODO`, `NotImplementedException`, placeholder exceptions.
3. Search for reflection-based execution (`Invoke`, method-name lookup, `GetMethod`) and remove it.
4. Verify Story runner only reaches performer/player actions through `PerformanceRun` scoped APIs.
5. Verify original Void scene was not modified.
6. Verify no new package dependency was added.
7. Verify profile/settings/tuning storage files are separate.
8. Verify equipment compatibility is symmetric.
9. Verify the D1–D5 story contains the explicit checkpoint and no fake Director/card execution.
10. Verify no Unity Play Mode automation occurred.

If Unity compile/import diagnostics can be run without Play Mode, do so and record the exact result. Do not block the final source status solely because the environment cannot launch Unity; report that limitation.

## Documentation

Add/update:
- a short `docs/story-runtime/D1_D5_HANDOFF.md`
- optionally `docs/story-runtime/SCHEMA_V1.md` using the packet reference
- current production-consolidation handoff/progress only if repository conventions require every implementation sprint to record there.

Do not rewrite historical plans.

## Final report format

```text
Starting commit:
Ending commit:

D1: complete/incomplete — notes
D2: complete/incomplete — notes
D3: complete/incomplete — notes
D4: complete/incomplete — notes
D5: complete/incomplete — notes

Changed files:
New files:

Static checks actually run:
Unity import/compile checks actually run:
Play Mode: NOT RUN (required)

Manual scene:
Assets/Production/Scenes/FirstContactD1D5.unity

Known manual inspector/setup items:
<should be empty if setup automation completed>

Status:
SOURCE COMPLETE — AWAITING USER PLAYBACK
```
