# Day 5 report

Status: complete and accepted by the user. Technical execution evidence and subsequent user acceptance are recorded separately. Day 4 manual validation also passed. Independent clean-context import acceptance remains separate.

## Delivered behavior

Wardrobe Save, Compile, preview and Publish share a defensive resolved snapshot of the preset plus authoring configuration. Saved assignments determine populated and reachable masks. Explicit compilation updates preset layer metadata and fit references together; runtime captures the resolved data rather than selecting a different layer source silently.

Save Draft persists structurally valid edits independently of fitting. Editing a release creates separate mutable preset/configuration assets. Compile creates a unique attempt directory and advances references only after all reachable states validate. Publication prepares self-contained mesh, material, binding, fit and footwear snapshots before advancing the catalog. Existing immutable generation directories cannot be overwritten. Failures report their attempt location and retain the previous release. A supplied configuration with a mismatched release generation now produces an explicit generation/rebind diagnostic. A release may bind without any draft configuration.

Clothing Setup now has an outfit list, garment rows grouped into Base/1/2 with explicit selectors, named removal/addition sequences, top preview controls and selected/current revision indicators. Removal is 2 → 1 → Base. Naked preview keeps the remembered selection; Select Unclothed clears it. Undo/Redo, dirty indication, Save Draft, Revert, explicit Compile and Publish are distinct. Material opacity and footwear calibration have focused advanced panels. Unsaved edits invalidate preview controls while the order display updates immediately.

Stable source IDs retain layer/material/footwear edits through package refresh. Newly imported clothing starts at Base. Removed referenced pieces report Needs Repair and preserve the existing rows instead of silently discarding artist edits. Refresh invalidates compiled candidates; releases remain immutable.

Clothing Setup also uses the reusable production actor in the saved authored Void composition. Its saved startup component equips the initial outfit and reveals the actor on Play; the old setup started hidden without a reveal control. The original authored scene and Lara prefab remain protected. Camera framing is set for the single standing preview actor.

## Defects found during verification

The old migration assertion combined catalog GUID stability with an obsolete fixed fit-state count. There was also a real saved-data discrepancy: preset layer metadata lagged behind the saved configuration. The setup candidate is explicitly recompiled to resolve that discrepancy. Migration now reports GUID/idempotence, saved assignment parity, reachable masks and prepared binding validity separately.

Imported uDTU clothing shaders use a scalar `_Alpha` opacity control. The previous preview path only changed color alpha, and publication could not bake the imported opacity. Preview and snapshot publication now support the actual scalar control, with color-alpha fallback for other supported materials. The regression reads shader properties directly to verify the released value independently of the production helper.

Exact saved-scene playback exposed a prefab persistence defect that a raw imported actor fixture missed. `ConfigureBinding` changed the scene actor in memory, but its caller did not record the prefab property override. After reopening, the actor inherited the prefab's old release and rejected the candidate preset. Setup compilation and scene creation now explicitly record wardrobe overrides. The handler regression now saves and reopens a copy of the actual prefab-based setup scene and checks both catalog and configuration references; runtime runners also check saved draft/binding parity.

A release-derived working draft referenced snapshot package assets, so reference-only reimport matching skipped it when the original package changed. Candidate refresh now matches the package's stable source ID, including variants whose preset IDs differ. The automatic import path also checks that the package ID agrees with its source directory. This identity policy is scoped to the project's single import registry; released catalogs are excluded.

Headless verification initially encountered scene-unload references, Windows long-path hashing and editor-window teardown errors. Those were test infrastructure failures, retained in the intermediate logs; they are not claimed as product passes. The test now loads assets after opening its scene, includes long paths and disposes its headless window instance correctly. Both wardrobe validation wrappers synchronize the Player source dependencies as well as DazPose and FirstPerformanceVoid.

## Verification and remaining gates

| Executed check | Result |
| --- | --- |
| Transactional and actual setup-handler round trips | 11 assertions passed |
| Wardrobe contract / migration | 13 / 10 assertions passed against final source |
| Exact saved wardrobe scene, editor / Windows player | 39 / 39 assertions passed |
| Exact saved Clothing Setup scene, editor / Windows player | 9 / 9 assertions passed |
| Windows wardrobe / setup builds | Succeeded; zero errors; 13 / 9 warnings |
| Windows graphics and process completion | Direct3D12; both players exit 0 |
| Actual first / second / third outfit reimport | GUID, edited opacity and footwear preservation passed |
| Export and source preservation | See `export-check.json`, `source-manifest.json` and `environment-preservation.json` |

Evidence directory: `.dazposewizard/day5-evidence/`. The two tested scenes are exported with their dependency closures and original metadata. The original Void scene, Lara prefab, Day 3/4 fixtures and imported wardrobe sources are checked against their recorded hashes. Exact YAML comparisons against the accepted Day 2 composition retain authored environment records; actor placement/binding, removal of the second actor, camera framing and the root list are the permitted composition changes.

The saved wardrobe runner tests candidate/release layer parity, independent hair, hosiery without shoe support, fresh release binding and recovery after failed attempts. Hosiery retains the accepted bent-foot pose when shoes are removed. Build warnings remain visible in the retained logs; a successful build is not human appearance approval. Reproduction helpers remain under `docs/production-consolidation/validation/`; pressing Play requires none of them.

Use `DAY_5_MANUAL_CHECKS.md` for the separate human UI/appearance review. General wardrobe integration, 100-cycle cache/disposal, 181-frame walking/culling, and assembly separation remain Day 6 work. Independent clean-context import acceptance and physical hardware certification remain separate. No commit, push, GUI automation or external publication occurred.

