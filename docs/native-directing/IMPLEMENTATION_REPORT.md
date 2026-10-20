# Native directing execution report — October 19, 2026

## Current G4 disposition — interface rejected, correction awaiting Art

Art rejected the integrated G4 Studio on October 19: the tabbed interface, centered unwrapped cues, disconnected binding workflow, exposed interpreter terminology and navigation lag were unacceptable. **G4 is not accepted; N11 remains held.** The earlier “ready” section below is historical delivery, not acceptance.

The correction replaces the main window with a resizable, wrapped, left-aligned cue sidebar and a contextual inspector. Direct source-owned pose/audio/material/motion slots and scene mark assignment/creation/select/frame controls appear alongside inherited pose, performer, camera and body marks at the executed moment. Seat/approach anchors can be assigned/created there. Binding edits and Undo preserve the selected cue and refresh its preview. No permanent focus button or Cues/Bindings/Scenario tabs; tracking target controls are conditional on camera context, path options folded away.

Navigation keeps the native lease/graph, advances the existing canonical session for forward cues, rebuilds logical history only when necessary for backward/repeated navigation, validates native inputs per executed effect, and applies the selected native endpoint once. Dialogue/gesture-only changes reuse the unchanged settled shot. Owned material instances survive restaging/save suspension but are detached before serialization and destroyed on Stop. Script text/manifests are cached for repaint; transform gizmo edits avoid whole-asset-graph revision hashing. Actual speed, layout, save safety and responsiveness still require Art's native retry.

Source-only evidence: all seven assemblies compile; 75 pure managed rehearsal tests pass, including new forward-navigation/state retention, exact repeated-occurrence and presentation-key checks. See G4_UI_SOURCE_COMPILE.json and G4_UI_MANAGED_TESTS.log. Original protected files remain unchanged. No Unity operation, remote write or modification of preexisting packet archival work. G4_ART_HANDOFF.md now starts with one concrete pose/mark/camera authoring pass.


**Branch:** main. **Recovery baseline:** `4656eb56fe1cfa2201104e960b4d7e8dc41cb766`. **Overall:** PARTIAL. **Workflow:** WAITING ON ART (G4). G1/G2/G3 accepted from Art’s reports. N06–N09 source is integrated into the actual Studio; N10 source/managed checks and G4 recorder/handoff are ready. N11 completion is blocked until G4 native acceptance. Local commits only; no push and no executor Unity operation.

## Ticket and manual gate ledger

| Ticket | Source / managed | Native | Evidence |
|---|---|---|---|
| N00 | PASS | Inspector confirmation deferred to G1 | Baseline/protection fingerprints, saved-scene/content maps, writer audit, isolated physical project, 196 tests and seven source assemblies |
| N01 | Implemented; seven assemblies compile | PASS — Art reported camera shift | NativeStageContext, minimal read-only runtime accessors, Studio foundation and user-invoked camera diagnostic; current code/meta sync verified |
| N02 | Source implemented; managed restoration regressions pass | G2 PASS — additional seat/material families remain provisional until G3/G4 | Exclusive lease/driver, per-property restore, save/lifecycle callbacks, guarded editor writers |
| N03 | Source implemented; extracted camera formula parity checked | G2 PASS — Art reported | Real PlayerView root/rig adapter, actual camera origin, post-sample focus |
| N04 | Source implemented; seven assemblies compile | G2 PASS — standing/camera; later material proof pending G4 | Real Lara manual-graph sampling, standing/visibility/material adapter and G2 fixture |
| N05 | Source implemented; managed geometry/oracle checks and seven source compiles pass | G3 PASS — Art reported, seat-focus label deferred | Distinct baked default and sampled-hip seating adapters; G3 native save/contact proof |
| N06 | Implemented; cumulative intent / before-after exact replay / fail-fast managed tests PASS | WAITING ON ART G4 | RehearsalVisualState and actual-scene cumulative adapter; no second logic engine |
| N07 | Native Studio cue/binding/scenario chrome implemented | WAITING ON ART G4 | Auto-resolution, source rows, native mark Select/Frame and head-local focus |
| N08 | Canonical global card/branch transport retained; scratch test tooling prepared | WAITING ON ART G4 | Explicit isolated scratch global-card add/remove/index hash buttons |
| N09 | Retired renderer and callers deleted; seven assemblies compile | WAITING ON ART G4 import/menu check | N09_RETIREMENT_AUDIT.json; zero old renderer source matches |
| N10 | 255 managed tests PASS; seven source compiles PASS; static audit PASS | WAITING ON ART G4 | G4_ART_HANDOFF.md and opt-in actual-Studio evidence recorder |
| N11 | HELD at G4 | NOT RUN | Final delivery/acceptance cannot be declared before Art’s G4 |

| Gate | Art action | Response / evidence | Native status | Dependency |
|---|---|---|---|---|
| G1 | G1_ART_HANDOFF.md, real camera Shift -> observe Game -> Restore -> observe -> Export | Art: "Pass… The camera shifted correctly. We're good to proceed… Continue." Screenshots explicitly declined; verbal PASS accepted. No exported restoration report supplied. | PASS — user reported | N02 |
| G2 | G2_ART_HANDOFF.md: standing/camera/drag + normal and forced save + Undo/Stop/reopen | Initial run failed and was repaired in c8afbc2. Art then reported: "Okay, success this time." Accepted as G2 PASS; no screenshots or exported native report supplied | PASS — user reported | N05 |
| G3 | G3_ART_HANDOFF.md: A default CrossLegs, B persistent Lounging, C Main seated dissolve; live anchors, scale and save/Undo/Stop | Art: “mostly works”; clarified shared focus/SeatAnchor and deferred label detail, explicitly authorized proceeding through G4. No screenshots/export supplied. | PASS — user reported | N06 |
| G4 | G4_ART_HANDOFF.md: actual Studio full regression | Prepared actual Studio, user-invoked recording and isolated scratch global-card proof; screenshots not required | WAITING ON ART / NOT RUN | N11 final acceptance |

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

## G2 native failure and repair

Art reported G2 FAIL: Save did not resume, moving a mark dropped the pose, and camera-mark rotation did not affect the shot; Stop/reopen was not verifiable. G2_FAILURE_EVIDENCE.json and G2_FAILURE_AND_REPAIR.md preserve the observations and read-only Unity log evidence. The failure supersedes any source-ready implication of native correctness above.

The v2 repair retains one sampling graph per lease across restaging/save, handles transient import without cancelling the watcher, scopes restoration callbacks, removes the unnecessary renderer setting write, distinguishes cosmetic YAML string normalization from real data changes, and starts in explicit camera-mark orientation with tracking separately labelled. All actor/material/override data checks remain. Existing edited fixture scene data is preserved; no original asset changes, Unity operation or remote publication by the executor. N02–N04 stay native-provisional and N05 remains held pending a successful G2 retry.

Repair validation: **223 managed tests PASS** (90 Story, 24 Player, 40 Rehearsal, 69 Application), zero failures/skips; seven source assemblies compile; static asset/meta audit PASS with zero issues; all 7,211 protected originals unchanged. These are source/managed checks, not native Unity acceptance.

## G2 acceptance after repair

Art reported: "Okay, success this time. What's our next sprint > gate?" This supersedes the failed v1 gate disposition while preserving its evidence. G2 is accepted from the explicit user report for repaired v2 code c8afbc2; no new screenshots or repeat approval requested. No native diagnostic export was supplied, and no executor Unity operation occurred. The next implementation is N05 (default SitAt, persistent seated pose, seated dissolve and live seat/contact anchors), followed by mandatory Art-operated G3. N05 has not begun in this status-answer turn. G3/G4 still govern their additional property families.

## N05 seating delivery — G3 requested

NativeSceneStage now resolves real PerformerSeat frames/profile and samples default CrossLegs from its actual rebased loop while retaining the baked SitStart landing root. Persistent Lounging and Main seated dissolve separately solve from the actual sampled hip. Geometry errors, readiness and posture metadata are diagnosed explicitly. The native stage preserves anchors and performs camera tracking after pose/contact placement. The retained G2 sampling graph now supports explicit sample seconds; existing standing calls remain at zero. No runtime state-machine policy, rig/prefab/content, baked data or artist setting was changed.

G3 Seating Proof provides the three source-bound endpoints, native seat/approach/chair/camera/focus selection, live mark and parent-matrix updates, contact diagnostics, a ledger-owned temporary nonuniform actor-scale probe, and forced/normal save evidence per endpoint family. Default's measured baked hip gap (0.0306574 m at canonical unit scale) is distinguished from its 0.01 m baked-geometry residual tolerance; Lounging uses 0.001 m direct hip-to-seat tolerance. G3 must validate rendering, real contact, dirty/Undo/override preservation, scale and save callback behavior; no native PASS is inferred from source or math tests.

See N05_SOURCE_NOTES.md, N05_SOURCE_ORACLE.json, N05_* evidence and G3_ART_HANDOFF.md. Source work stops here at G3; N06 transport has not begun. The user remains the Unity operator. No screenshots required, no launch/Play/build/player/hardware by the executor, local commits only and no push.

Final N05 source evidence: **244 managed tests PASS** (90 Story, 24 Player, 61 Rehearsal including 21 new seating/scale/oracle checks, 69 Application), zero failures/skips. All seven standalone source assembly compiles PASS. Static asset audit: zero issues, duplicate GUIDs or missing metas. All 7,211 protected original files unchanged. Native sampling, visual contact and Unity save/Undo acceptance are pending Art G3.

## G3 acceptance and runtime parity clarification

Art reported seating “mostly works”, identified that Select seat focus selects the same Transform as SeatAnchor, and explicitly deferred that detail and authorized N06–N10 through G4. G3 is accepted from this user report; no exported report or screenshots supplied. The product must reproduce executed runtime camera commands exactly: StartAt clears tracking; Track(Lara) tracks the actual laraFocus after pose sampling. G3 diagnostic framing controls must never become product defaults. G4 must exercise the integrated authoring Studio, not fixed diagnostic endpoints.

## N06–N10 integrated Studio ready — mandatory G4 pause

The actual Scene Director Studio now stages ordered executed effects cumulatively on the saved Lara/body/PlayerView. Camera behavior follows script order exactly, including StartAt cancelling tracking and Track(Lara) aiming at the actual laraFocus after pose sampling; diagnostic camera toggles/defaults do not enter product staging. Scene/native handles re-resolve semantic marks without changing the logical scenario, Director/RNG or selected cue. Before/After, exact nth visits, Previous/Next, source-aware failure and global owner references use canonical rehearsal.

The old cloned scene renderer, custom viewport/orbit/draft controls and splitters are removed. Binding reconciliation remains explicit authoring. Local head-focus offset is authored and excluded from preview ownership; all new focus/save/material/lifecycle behavior needs G4 native evidence. The G4 panel records actual Studio visits/saves and creates/removes a scratch tagged card only in the independent fixture on Art’s explicit button clicks. It supplies no alternate camera framing.

Final standalone checks: **255 managed tests PASS** (Story 90, Player 24, Rehearsal 72, Application 69), zero failures/skips. Seven assemblies compile from source. Static asset audit PASS with zero duplicate GUIDs/missing metas/new unresolved script refs. **7,211 original protected files unchanged**. See N10_MANAGED_RESULTS.json/logs, N10_SOURCE_COMPILE.json, N10_ASSET_AUDIT.json, N09_RETIREMENT_AUDIT.json, N06_N10_SOURCE_NOTES.md and ACCEPTANCE_STATUS.md.

Read G4_ART_HANDOFF.md for exact project/scene/menu/cue directions. No screenshots required. Native import/rendering, full cue/branch/global-card progression, focused asset/frame rules, save/Undo/overrides and lifecycle remain NOT RUN until Art reports them. N11 and overall completion remain held. No Unity launch/operation, Play/build/player/hardware or remote publication by executor.
