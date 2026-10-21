# Execution sequence and mandatory gates

This is **one coordinated replacement sprint**, broken into reviewable, stoppable commits. Tickets N00–N11 are individually executable in this order. Advance only when the associated gate passes; log partial native evidence honestly. Make no new architectural decisions ad hoc: `ARCHITECTURAL_DECISION.md`, `CONTRACT.md` and `CUE_BEHAVIOR_MATRIX.md` decide scope/ownership.

| Order | Ticket | Gate outcome |
|---|---|---|
| N00 | Baseline, source/Unity scene audit | Pinned clean checkpoint, authoritative component/scene mapping and recovery hashes |
| N01 | Native Edit Mode foundation; **MANUAL GATE G1** | Art confirms saved Game camera renders and responds without Play; **stop here until PASS** |
| N02 | Reversible preview lease + save protection | Source tests and diagnostics; property-family safety proof is **provisional**, retested as stage grows |
| N03 | Real-camera endpoint staging | `Camera.StartAt` / `Camera.Track` use actual PlayerView; prepare native camera proof |
| N04 | Native Lara standing/root/pose staging; **MANUAL GATE G2** | Art verifies real camera + standing Lara + drag + Ctrl+S/Undo/Stop before seating |
| N05 | Seat/contact endpoint staging; **MANUAL GATE G3** | Art verifies default SitAt vs persistent Lounging and seated dissolve, with root/hip contact and save safety |
| N06 | Projection and cumulative cue state | Cues can be navigated in any order by replay; authoring marks instantly re-evaluate projection |
| N07 | Native-mark workflow and Studio chrome | Select/edit/save native marks with Inspector/Scene gizmos; straightforward UI |
| N08 | Global-card navigation and nonvisual cues | Runtime semantics remain canonical; no resets through dialogue, UI, nested global cards |
| N09 | Remove duplicate renderer and old Studio input | No `ScenePreviewSession` dependency, `PreviewRenderUtility` or replacement viewport |
| N10 | Native and managed regression; **MANUAL GATE G4** | Art runs final native reports and walks FirstContact; no automated Unity launch |
| N11 | Delivery, docs, reviewer evidence and handoff | No unresolved native gates called PASS; files, repro and author guide included |

## Integration strategy

- N00 creates a **separate isolated Unity project copy** for native gates, or stops to obtain Art's deliberate consent for asset migration and index writes first. A saved **scene copy alone is unsafe** because binding reconciler/global index reference shared assets.
- N01 prepares the smallest user-invoked menu check. **STOP G1:** ask Art to open the isolated project in Unity, inspect standard Game view, click diagnostic menu and return its report. Do **not** start N02 if G1 is unrun/failed.
- N02–N04 make one tiny scripted standing/camera path before seating. **STOP G2 after N04:** ask Art to stage `camera_start` and `stepforward`, drag, Ctrl+S, Undo, Stop, reopen and return evidence. No N05–N11 until G2 passes. This is a technical/visual go-no-go before UI polish.
- N02 save safety is a release gate. If automatic Ctrl+S suspend/restore is not safe in the current Unity version, **do not hide that limitation** or build an unsafe partial design. Review/repair the native transaction before proceeding to production rollout. Any temporary safety guard must prevent persistence, but is not final acceptance.
- N05 seats must use **two distinct runtime oracles**: default SitAt stable CrossLegs/baked solve and `PersistentSitPose`/seated dissolve with **sampled live hip-to-seat correction** after pose/root yaw. **STOP G3**, require Art's first Warmup sitting and later Main seated-dissolve evidence.
- N06–N08 wire the now-working native stage into source clicks, state history and mark handles, preserving deterministic scenarios; moving an anchor must not re-run the Director.
- N09 removes the old custom viewport **after** native implementation has passed its basic gates, with no shipped toggles supporting two competing modes.
- N10 prepares and requests **G4** for full save/undo, renderer/material/wardrobe interactions, native import and lifecycle. Art owns all Unity actions. Executor cannot self-run tests in batch mode. Missing Art response = `WAITING ON ART` and final `PARTIAL`, not permission to mark tests PASS or continue to N11 as complete.
- At every manual gate, **pause the turn and wait for Art's response**. If Art reports failure, log it, fix source, and re-request the same gate. If Art explicitly ends the run, deliver a partial report without pretending completion.

## Source ownership and commit rhythm

- `Assets/DazPose/Editor/StoryStudio/`: editor staging, lease, bridge, window, mark tools and native acceptance helpers.
- `Assets/Player/Runtime/PlayerView.cs`: only minimal read-only editor-safe binding access or pure endpoint math refactor; **never** change live camera control policy without a parity test.
- `Assets/DazPose/Runtime/Performer/`: only shared pure seating endpoint helper(s) if required; **no** changes to production animation/state machine policy.
- `Assets/DazPose/Runtime/Story/Language/` and `StoryRunner`: leave behavior intact; add no second interpreter.
- `tests/` and disposable Editor validation: regression coverage.
- `docs/`: accurate author guide, migration notes and evidence.

Commit logically at milestone groups with your executor's normal git protocol, but **do not push without authorization**. Report actual SHA. Do not overwrite user modifications or self-approve visual appearance.
