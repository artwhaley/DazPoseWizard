# Day 4 report

Status: complete. Automated editor/player verification passed; the user subsequently reported manual validation working. Human approval is recorded separately in shared progress. Day 5 is accepted, and remediation R01–R13 is technically complete. The second-outfit toe appearance review remains pending; Day 7 has not started.

## Delivered shape

- `PartnerBody` owns prepared body/shell bindings, endpoint state, zero-compliance defaults and donor-pelvis following. It has no camera control or diagnostic UI dependency. The old acceptance controller delegates to it.
- `PartnerContactTarget` provides live frames and an optional prepared mesh surface. `PartnerContactProfile` holds the measured interior frames, placement radius, section orientation, mesh identities and prepared surface asset.
- `GripContactSurfaceData` prepares a BVH from evaluated body and shell triangles. The animation layer copies native data on source/revision changes and updates the rigid pose during playback. Queries allocate no per-probe managed storage and do not bake renderers each frame. Full-scene performance benchmarking remains a separate consolidation check.
- The source-neutral straight-span capability replaces rod-type assumptions. Rod-only targets retain the original cylinder solver, independently checked by the legacy tests.
- The performer facade exposes `GripAsync(target)`, `HoldGrip(position01)`, `FollowGripMotion()` and `ReleaseGrip()`. Hold/follow neither restart nor replace the shared motion source. The existing scoped command ownership/cancellation contract remains in use.
- Invalid targets during staging cancel the exact owned walk. Failed acquisition completes after weighted release cleanup. Completed release clears hand weight and motion suppression. Lost driver, target destruction and stale cancellation generations fail or clean up without cancelling a replacement operation.

Prepared support is explicit: right hand, rigid prepared endpoint, zero compliance, unit actor/pelvis/shaft scale and intact calibrated renderer/bone bindings. Root translation and rotation are supported. Normalized author positions map over measured guide arc 0.20–0.75, approximately 10 cm, rather than exposing unsafe whole-source endpoints. Nonuniform/negative scale, independently scaled donor geometry, changed renderer transforms, missing bone bindings and unsupported endpoint/compliance states reject contact. Arbitrary deformation, arbitrary partner meshes, soft tissue and general full-body collision are not certified.

The main project now contains `Assets/Production/Partners/Partner.prefab`, `PreparedContact.asset`, `PreparedSurface.asset`, `Table.mat` and `Assets/Production/Scenes/Day4PartnerContactValidation.unity`. The runner and preview controls are saved in the scene. The partner is supine, with evaluated minimum body Y exactly 1.000 m above the authored floor. The overview camera shows both actors; a separate contact close-up is available. Original Void and Lara prefab hashes remain unchanged.

## What failed and what fixed it

The old canonical guide has several abrupt, approximately 90-degree adjacent segment changes. Planes using those tangents produced misleading 44–45 mm radial extrema. Triangle-plane sections using the overall guide orientation resolved that measurement problem; source geometry was not changed.

A circular approximation still allowed actual probe/triangle overlap across the hand footprint. Acquisition, valid frames and nominal cylinder diagnostics were insufficient proof. The rejected circular measurements are retained in the evidence directory. Contact now uses the prepared evaluated triangle surface for curl collision, while nominal radius/orientation only place the palm. The previous circular-fit 3 mm limit is consequently placement evidence, not the collision decision. Lara calibration, the solver's 3 mm clearance and its 0.01 mm collision epsilon were unchanged.

The independent brute-force witness also required two corrections. Signed distance uses the Euclidean closest-point distance with a normal-selected side, not a normal projection. Body and shell material patches are open surfaces, so taking the minimum of two separate signed fields falsely treated them as closed solid volumes near their boundary. The combined witness now selects the nearest triangle across both patches, records sign disagreement, and separately checks sphere/triangle separation against EACH patch. This preserves the actual intersection gate and the existing span. It does not claim a global inside/outside test or continuous collision proof. Shell texture alpha is not rasterized.

Automatic approval rejected an attempted narrower span because the packet forbids shrinking it to hide failure; that command did not run. The original 0.20–0.75 span was retained. A later review usage-limit failure temporarily blocked execution; validation resumed after the user continued the task.

## Evidence

The exact saved scene passed 96 assertions in the final editor run. It covers initial framing/height, 17 normalized target frames, acquisition, 17 held stations, shared-clock follow, root translation/rotation with actual surface measurement, release, unsupported states/scales/bindings, active and pending out-of-reach recovery, pending target destruction and reacquisition. There were 20 independent evaluated surface snapshots, 20 probes each; every digit had at least one probe within the unchanged 6 mm near-contact distance across all recorded snapshots. No digit exemption was used.

The legacy solver suite passed all 60 assertions. The Windows build succeeded with zero errors; warning count and byte size are in `.dazposewizard/day4-evidence/build.json`. The final Direct3D12 Windows player passed all 96 assertions and exited with code 0, with zero probe/triangle intersections across its 20 snapshots. The worst sampled clearance was approximately 2.99 mm. Final player verification is recorded in `player-runtime.json` and `player-exit.json`.

Export closure matched 514 asset dependencies / 1,028 files with no differences (source text comparison normalizes CRLF/LF only). All 243 first-party source files in the captured DazPose, Player and FirstPerformanceVoid trees matched staging. Existing main-project script GUIDs were preserved, and the canonicalized scene was rerun before building.

The environment comparison preserves 160 of 169 base composition records exactly. The only other changes are primary actor placement, removal of the second actor and its stripped component records, authorized camera transform/FOV and the scene root list. There are no unexpected environment changes. The original authored Void, Lara prefab and Day 3 scene hashes match their accepted values.

Evidence directory: `.dazposewizard/day4-evidence/`. Execution helpers are preserved under `docs/production-consolidation/validation/`. No commits, pushes, GUI automation or external publication were performed.

## Remaining gate

Human review of the overview, authored environment, close-up contact and stop/replay behavior is pending. Use `DAY_4_MANUAL_CHECKS.md`, which includes absolute Windows PowerShell paths. Automated geometry/visibility checks do not substitute for that approval. Independent wardrobe import acceptance and physical hardware checks remain separate tasks.
