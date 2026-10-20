# W08 — Safe Studio appearance and Gate C

Depends Gate B PASS. Read CONTRACT C5, API section 7 (including MPB renderer isolation decision below), AU checks and manual Gate C.

## Files

AuthoringVisualState/Reducer, AuthoringCueIndex, AuthoringSourceCatalog, source-structure export/resolution helpers, SceneDirectorStudioWindow Authoring/VisualAuthoring/Revisions, NativeSceneStage.Authoring, NativePreviewLease, PreviewPropertyLedger/PreviewResourceOwner, NativePreviewWriterGuard; new NativeWardrobeProjection; GateC composer/script with source-owned companions. Existing single-window layout remains.

## Steps

1. Add typed outfit/layers/hair state, Count/AllMask constants and field-count-derived work bounds. Use pure selection/layer semantics. Eliminate 8/255 assumptions without causing new unbounded recipes.
2. Card call summary is identity across all protected fields; sequence summaries compose. Analyze internal card cues with explicit incoming contexts and joins. Dynamic card-only loops/choices do not contaminate following protected state. Do not enumerate candidate histories or execute anything to find a cue.
3. Add stable virtual Return to card entrance row. Preserve real source row IDs, bindings and current latest-selection coalescing. Root FirstContact is explicit; helper Intro sequence is not guessed as root.
4. Implement preview-owned appearance renderer proxies and shared skeleton binding exactly as the API amendment below requires. Shared preparer/binder supplies the same released endpoint. Never start Play-only controller, write original MPBs, apply serialized preview overrides, or build a duplicate actor/skeleton.
5. Route pose sampling, material/visibility projection and anatomy/coverage/footwear endpoint math coherently: sample original scene's existing graph/bones, copy needed sampled blendshape values by name into appearance proxies, keep source renderers preview-hidden under ledger, and target the effective preview renderer set. Camera/seat math still reads actual actor/bones/marks. Suppress competing coverage/footwear writers.
6. Separate appearance cache/revision from pose/spatial keys; mark drag must not prepare appearance/build instances. Add typed current/inherited slots and layer names/no-op descriptions in the adaptive inspector. Slot changes persist via normal source binding Undo; preview appearance stays transient.
7. Cover all save/dispose/conflict paths with ledger tests and native GateC. Generate saved GateC script with known initial look, sequence changes, direct/nested/drawn card boundaries, sparse layers, shader and seated changes, late cue 91+, plus a deliberate ambiguous choice.

## Checks / exit

AU01–AU13, existing preview/navigation regressions, appearance proxy ownership/disposal, counters, static compile and protected inputs. WAITING_ON_ART Gate C with exact scene/cues/controls and timings. Do not claim responsiveness from synthetic timing. Repair affected cases. No W09 before C PASS.
