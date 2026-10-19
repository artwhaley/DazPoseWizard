# Native directing execution report — October 19, 2026

**Branch:** main. **Recovery baseline:** `4656eb56fe1cfa2201104e960b4d7e8dc41cb766` (matches revised packet). **Overall:** PARTIAL. **Workflow:** WAITING ON ART (G2). G1 is accepted. N02–N04 source delivery is ready; N05–N11 are held at the next mandatory native gate.

## Ticket and manual gate ledger

| Ticket | Source / managed | Native | Evidence |
|---|---|---|---|
| N00 | PASS | Inspector confirmation deferred to G1 | Baseline/protection fingerprints, saved-scene/content maps, writer audit, isolated physical project, 196 tests and seven source assemblies |
| N01 | Implemented; seven assemblies compile | PASS — Art reported camera shift | NativeStageContext, minimal read-only runtime accessors, Studio foundation and user-invoked camera diagnostic; current code/meta sync verified |
| N02 | Source implemented; managed restoration regressions pass | NOT RUN — provisional until G2/G3/G4 | Exclusive lease/driver, per-property restore, save/lifecycle callbacks, guarded editor writers |
| N03 | Source implemented; extracted camera formula parity checked | NOT RUN — G2 required | Real PlayerView root/rig adapter, actual camera origin, post-sample focus |
| N04 | Source implemented; seven assemblies compile | NOT RUN — G2 requested | Real Lara manual-graph sampling, standing/visibility/material adapter and G2 fixture |
| N05–N11 | NOT STARTED | NOT RUN | G2/G3/G4 remain mandatory |

| Gate | Art action | Response / evidence | Native status | Dependency |
|---|---|---|---|---|
| G1 | G1_ART_HANDOFF.md, real camera Shift -> observe Game -> Restore -> observe -> Export | Art: "Pass… The camera shifted correctly. We're good to proceed… Continue." Screenshots explicitly declined; verbal PASS accepted. No exported restoration report supplied. | PASS — user reported | N02 |
| G2 | G2_ART_HANDOFF.md: standing/camera/drag + normal and forced save + Undo/Stop/reopen | Source and synchronized fixture ready; no screenshots required per Art preference | NOT RUN / WAITING ON ART | N05 |
| G3 | Default seating and persistent sampled-hip proof after N05 | Not requested yet | NOT RUN | N06 |
| G4 | Full regression during N10 | Not requested yet | NOT RUN | N11 final acceptance |

## Delivered source

- `NativeStageContext.cs`: resolves only the active saved scene's explicit StorySceneBindings/PerformanceSceneBindings, same-actor Animator, same prepared body, player root and configured rig/head/camera; rejects cross-scene, missing, duplicate, preview and Prefab Mode contexts. Acquiring context does not change transforms, discover runtime services, or start gameplay.
- `PlayerController.ConfiguredView` / `PlayerView.ConfiguredViewRig`: read-only serialized-reference accessors. Existing runtime controls/interpolation/policy are unchanged.
- `SceneDirectorStudioWindow.Native.cs`: native foundation/control window with source, saved sequence and host selectors, context connect/disconnect and real-object inspection. Its visible path cannot build the cloned stage. Old layout/renderer code remains unreachable from this foundation pending the explicitly gated N09 deletion. Full cue transport is not claimed at N01.
- `NativeDirectingG1Window.cs`: opt-in menu, verified independent-project marker/code hashes, disposable-scene guard, explicit camera shift held across frames and separate restoration. Owns only player local position/ViewRig local rotation; no AnimationMode, poses, renderer/material writes, app session, async runtime commands, hardware, new camera or custom renderer. Rejects missing/inactive/competing cameras instead of changing authored configuration.
- G1 cleanup is subscribed only while its panel is open: panel close, scene closing/change, assembly reload, Undo/Redo and a user-requested Play transition restore owned transforms. Saving while shifted filters matching OnWillSaveAssets paths; direct sceneSaving restores first and invalidates the test. This is a narrowly scoped diagnostic guard, not the N02 preview lease or general save-safety proof.
- Native reports are written only under the isolated project's Temp/NativeDirectingDiagnostics/G1_timestamp directory. PASS needs Art's explicit shifted/restored observations plus matching serialized restoration, scene dirty state and file hash. Detailed object/bone/renderer/prefab comparisons and source/code identities are exported.

## Baseline, isolation and ownership

N00 local commit: `69592db`. N01 source-ready commit is the subsequent Git history entry; fixture marker records current synchronized source revision plus exact code fingerprint. See `N00_BASELINE.md`, `N00_BASELINE.json`, `N00_SCENE_MAP.json`, `N00_CONTENT_MAP.json`, `N00_RENDERER_CALLERS.json` and `PROTECTED_CHECK.json`.

Independent project: `C:/Users/artwh/OneDrive/Documents/DazPoseWizard/.dazposewizard/native-directing/G1Project`. Disposable saved scene: `Assets/__NativeDirectingFixtures/G1_FirstContact.unity`. Source originals, companion bindings, global index and ignored imported inputs are separate physical copies. Packages/ProjectSettings and a physical package cache are present. Importer ownership backfill and global index writes in this fixture cannot mutate original asset files. No fixture content sync back is implemented.

Code-only synchronization: `python scripts/native-directing-fixture.py sync-code --gate G2` (omit --gate for the original G1 evidence filename). It copies changed source/metas/asmdefs/asmrefs and verifies every code hash, refuses independent fixture code edits/additions, and preserves content/scene/index files. `G1_CODE_SYNC.json` preserves G1; `G2_CODE_SYNC.json` records the current G2 code fingerprint and file count; the marker records the actual source revision used by Art. Original protection check: `python scripts/native-directing-fixture.py verify`.

Pre-existing user packet archival moves and Codex attachment are outside implementation ownership and remain unmodified/uncommitted. The supplied revised packet was committed with N00. Only AGENTS.md's active section was changed; its historical sections and absolute Unity restriction remain.

## Checks and limits

N00 managed baseline: Story 90, Player 24, Rehearsal 13, Application 69 — **196 PASS**, zero failed/skipped. Exact outputs are in N00_MANAGED_RESULTS.json and logs. Source-only compilation against installed reference inputs passes all **seven assemblies** with warnings-as-errors at baseline and after N01; N00_SOURCE_COMPILE.json and N01_SOURCE_COMPILE.json preserve results. Static asset audits at N00/N01 are recorded separately; historical docs/p2-p3/FINAL_ASSET_AUDIT.json is restored byte-for-byte after each audit.

No Unity executable, Hub, Editor GUI/CLI/batch/executeMethod/EditMode test runner, Play, build, standalone player or hardware was launched or operated by the executor. Commands used were filesystem preparation, Git local operations, dotnet managed tests, and Python/Roslyn source validators. Source compilation is not native Unity import/render/Undo acceptance.

G1 accepted on October 19 from Art's explicit PASS and observed camera shift. Art declined screenshots; none are required again for this gate. Detailed native restoration evidence was not exported; G2 will exercise integrated restoration/save behavior. N02–N04 source implementation and standalone checks are ready. Native behavior remains provisional. See N02_N04_SOURCE_NOTES.md, N04_* evidence and G2_ART_HANDOFF.md. Await G2 PASS before N05; no G1 repeat or screenshots requested.

## N02–N04 delivery

Exclusive NativePreviewLease + plain managed PreviewPropertyLedger restore baseline before each immutable projection and before scene/asset serialization, using only the owned AnimationModeDriver. Real saved Lara is sampled through a private manual graph appropriate to her controllerless Generic Animator. Editor writer guards, root-curve isolation, per-slot material restoration, clone cleanup and explicit unsupported-curve/dissolve errors protect authored data. Shared PlayerViewEndpoint preserves original runtime math/smoothing while the stage drives actual root/ViewRig and aims after pose sampling. No cloned renderer or old ScenePreviewSession dependency was added.

NativeDirectingG2Window is an explicitly fixed FirstContact technical fixture, with actual source/binding verification, native mark selection/drag reapplication, endpoint/track choices, normal save observation, user-invoked forced SaveScene, clean save byte comparison, actor/prefab diffs and report export. It does not claim N06 full transport. No screenshots required. Save/Undo, sampled rendering, wardrobe/heel appearance, material/MPB and all Unity callback ordering remain native acceptance items. N02 remains provisional for each later property family as well.

Final source-only compile: seven assemblies PASS. Managed checks: 212 PASS (90 Story, 24 Player, 29 Rehearsal including 16 new native-substrate/math checks, 69 Application); no failures/skips. Rehearsal test-only math shims do not establish native Unity behavior. Original assets/metas and user packet archival changes remain protected; fixture receives code-only updates. Implementation is locally committed on main; no push or Unity operation.
