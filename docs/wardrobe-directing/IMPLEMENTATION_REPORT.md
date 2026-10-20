# Reusable appearance and protected cards — execution status

## Current status

**Gate A PASS; W00-W06 complete; W07 source-ready and WAITING_ON_ART at Gate B.** Single Lara only. Art must check native return before W08. The Gate B scene is created by Art's delivery-window button; the executor has not operated Unity. Production appearance remains unchanged until W09. See [exact Gate B directions](GATE_B_HANDOFF.md).

Entry: [START_HERE](../../newtickets/DazPoseWizard_Wardrobe_Protected_Cards_Execution_Packet/START_HERE.md).
Design: [IMPLEMENTATION_PLAN](IMPLEMENTATION_PLAN.md).
Machine status: [progress.json](progress.json).
Coverage seed: [WRITER_COVERAGE](WRITER_COVERAGE.md).

## Prepared deliverables

- Fixed product contract: every card returns, sequences persist; all six appearance/authoring outcomes required.
- Fixed API/subsystem ownership, scoped operation lifecycle, exact native return order, failure/Stop handling and source schema migration.
- Ten bounded tickets W00–W09, ordered dependencies, four explicit Art-operated gate stops.
- Concrete appearance/card/Studio/production check IDs and future source/native evidence requirements.
- Exact future delivery menu, saved diagnostic scenes, runner controls, production property-diff template and standalone executor prompt.
- MPB safety decision: native appearance writes affect lease-owned renderer proxies sharing the real skeleton; original blocks are not overwritten/restored.
- Diagnostic draw isolation: explicit source inputs to the same linking/runtime factory, no diagnostic cards added to production index membership.

## What has not happened

At W00 start, no runtime/editor implementation, prefab generation, candidate publication, production reference change, new test implementation/run, Unity/app operation, native test, hardware output, local commit or remote write had occurred. W00 records preservation baselines only.

## Execution log

- **2026-10-20 — W00 COMPLETE:** confirmed `main` at `110e2b4aef1422f0739cb8ca4d370cb4792c1ec2`; wrote per-file SHA-256 manifest for all released wardrobe generations plus Lara, prepared male PlayerBody, FirstContact scene/program/bindings/dialogue map, marks prefab, Void environment, wardrobe catalog/config and global card index. Captured all 19 dialogue transcripts and placeholder audio hashes (19/19 files present), source/binding/scene/prefab GUIDs, global source membership (one source: `FirstContactEngineering.dazstory`) and expected bounded W04/W09 property-path exceptions. Both the released and existing candidate first-outfit mappings have panties at layer 2 and dress at layer 1; correction must be a new release. `WRITER_COVERAGE.md` now records current authoring seams and explicitly marks missing full snapshots/restore as W06 work. No assets or runtime code changed in W00; no Unity operation or test run.
- **2026-10-20 — W01 COMPLETE / SOURCE READY:** added immutable wardrobe selection/facts, deterministic selection resolver and side-effect-free fit preparer; routed runtime outfit/hair/layer request reduction through the shared resolver; retained the existing queue and transaction semantics while making its production mutation sequence use `WardrobeAtomicCommit` so rollback stages can be failure-injected in pure tests. Added clothing-only `SetOutfitAsync` to the performer and scoped APIs. Nine wardrobe tests pass; 90 story tests and 93 rehearsal tests pass; all seven assemblies pass `scripts/check-unity-source.py`. The rehearsal project reports its pre-existing CS0649 warning. Protected input hash comparison: 42/42 unchanged. No Unity operation, native fitting, generated asset, or production appearance change.
- **2026-10-20 — W02/W03 SOURCE_READY:** implemented the shared wardrobe attachment descriptor/manifest/binder and routed legacy synthesized inventory through the same binder. Added independent per-owner clothing/hair caches, deterministic prefab identities, canonical/local bone preflight, and a saved Gate A runner/composer. The delivery window now loads production Lara's release/configuration instead of `Assets/Wardrobe/WardrobeCatalog.asset`, can fork the three production catalog entries into a diagnostic candidate changing only first-outfit dress=2/panties=1, compile and publish using the existing `WardrobeFitCompiler`/`WardrobeGenerationPublisher`, generate the rig and save the Gate A scene. Gate scene creation is user-clicked; it clones First Contact, disables only the copied auto-start host and applies references only to the diagnostic scene instance. `dotnet test tests/DazPose.Wardrobe.Tests/DazPose.Wardrobe.Tests.csproj --no-restore`: 15 passed. `python scripts/check-unity-source.py`: all seven assemblies pass. Unity and native asset generation were not run. The W00 hash audit shows all 42 protected files unchanged; all new source assets have sidecar metas with no duplicate GUIDs. Art must perform Gate A before W02/W03 can be marked COMPLETE or W04 can start.

W00–W03 source work is ready for native Gate A. W02/W03 are not COMPLETE until the Gate A PASS response is recorded. W04–W09 remain NOT_STARTED; Gates B–D remain NOT_READY. Existing authoring acceptance remains historical and is not approval of this feature.

## Execution log

Continue recording ticket, revision/source hashes, owned files/asset GUIDs, check IDs/results, gate handoff and Art observations. Keep source readiness, actual native technical results and visual acceptance separate. Record invalidated/NOT_RUN cases and recovery refs.

## Preparation audit

Packet structure, relative links, ticket/gate/check references and initial progress state are checked as documentation integrity only. See evidence/PACKET_PREPARATION_REVIEW.md after preparation review. No implementation check is marked passing from this audit.

- **2026-10-20 — Gate A correction, source revision `fc31fe9`:** Art reported that **Prepare corrected candidate** stopped because the builder assumed imported role labels were exactly `dress` and `panties`. W00 already records the approved stable mapping: dress `PeekaBoo_16572`; panties `CharlenesCloset_Panty_1981`. Updated candidate preparation and idempotent reuse validation to use those stable IDs, require one matching piece and assignment for each, and include available piece IDs/roles in future mismatch diagnostics. The failed call stopped before candidate asset creation, so no cleanup is needed. `python scripts/check-unity-source.py` passed all seven assemblies; `dotnet test tests/DazPose.Wardrobe.Tests/DazPose.Wardrobe.Tests.csproj --no-restore --nologo --verbosity quiet` passed 15/15. Local source commit: `fc31fe9`. Gate A remains **WAITING_ON_ART**; no native operation was performed.

- **2026-10-20 — Gate A prefab generation correction, source revision `e703039`:** Art reported `Generate reusable prefabs` stopped with blank root/manifest, 0 created, 0 reused because the generator used `canonicalBody.GetComponentInParent<PerformerWardrobe>()` on a renderer loaded from the prefab asset. The delivery window now passes its already-resolved Lara `PerformerWardrobe` explicitly; generation verifies the owner’s serialized `Body` reference matches the canonical renderer. The button is disabled if the owner is missing. No generated rig output was written by the failed attempt. `python scripts/check-unity-source.py` passed all seven assemblies; `dotnet test tests/DazPose.Wardrobe.Tests/DazPose.Wardrobe.Tests.csproj --no-restore --nologo --verbosity quiet` passed 15/15. Local source commit: `e703039`. Gate A remains **WAITING_ON_ART**. The production Lara prefab has an uncommitted Unity serialization diff (`rigBinding: {fileID: 0}`); it was preserved and excluded from the commit.


- **2026-10-20 — Gate A complete delivery review / source `13492a7`:** fixed recovery of the already-published catalog at the candidate path (publication intentionally clears its authoring link and marks it released). Added one resumable preparation/publication/prefab/scene operation, disk recovery after reload, and safe versioning of incomplete outputs. Audited the delivered scene and runtime path: made the diagnostic actor initially visible; used its configured Body; aligned attachment frames; saved/reopened prefab overrides; supplied a clean inactive second-actor template; fixed Add expectations after naked and visible-garment reporting; hid retired cached renderers; pinned rollback leases; added actual projection/ownership/disposal evidence and required-case completion. The actual release has only Emiko hair, so the gate now exercises clear/set and two-owner independence with Emiko/none and explicitly records unavailable distinct-style swapping as NOT_RUN. No new hair asset was invented. Captured all 24 source piece bone plans (173 canonical bones) and all seven emitted fits; full/panties/naked sets are 6/2/0 with distinct prepared body refs. Added the previously Git-ignored wardrobe test project to version control. Final results: 25 wardrobe, 90 story and 93 rehearsal tests passed; all seven source assemblies passed. All 4,732 metadata GUIDs are unique. The only W00 protected-file mismatch is the previously observed user/native serialization of Lara.prefab's null rigBinding field; it is untouched and excluded from commits. Art-generated outputs and immutable release trees were preserved. Full findings, evidence and remaining native limitations are in GATE_A_REPAIR_REVIEW.md. No Unity process or native API was invoked by the executor.

- **2026-10-20 � Art accepts Gate A / scope and cleanup correction:** Art explicitly accepted the first Lara's wardrobe behavior, rejected the second-Lara requirement, and authorized moving on. The saved native report has genuine successful core appearance projections and fixture-only false failures: treating an already-clear hair selection's benign NoChange as a test failure. The report is preserved unedited; human acceptance is recorded separately. Removed the disposable Gate A scene and three unused draft presets, retired the first-outfit correction builder, combined fixture workflow, saved-scene composer and runner, and removed tests that exercised that retired fixture orchestration. Kept the production wardrobe reducer/controller/binder/fit/publisher/import code, released geometry/fits/presets, clothing/hair prefabs and rig manifest. Moved the published catalog/config to the generation's production rig folder with unchanged GUIDs and contents so the diagnostic folder could be deleted. Appearance Delivery is now a generic released-catalog visual-asset generator, with no test-scene/second-actor/correction controls and no three-outfit limit. AGENTS/packet record no second Lara and retirement of each accepted gate composition. W04 is unblocked; W09 still owns production mount/startup changes.

- **Retirement checks:** all seven source assemblies compile and all 18 remaining wardrobe tests pass. Seven fixture-workflow-only tests were retired with their deleted orchestration helper; the 15 production selection/cache/ownership tests and three actual release-data tests remain. No external asset reference to the removed draft presets/scene/runner was found. The accepted new catalog and configuration keep their exact data/meta hashes after relocation. No Unity operation was performed.


## October 20 - First Contact scene location corrected

Moved FirstContact.unity and its meta from Assets/Production/Scenes to Assets/Scenes. Scene and meta bytes, GUID, authored contents and bindings are unchanged. Updated the scene catalog, build settings, source helpers and maintenance scripts. New Performance Scene now defaults to Assets/Scenes. Earlier evidence paths remain historical. Unity was not operated.


## W04 - playable sequences

Implemented kind-aware local sequence/card and public-card resolution, cross-kind collision rejection, shared 128-entry recursion bound, run-local unique card invocation numbers, source-aware dependency/window traversal and Studio helper-sequence summaries. Importer is version 3; executable program version 2 is required for runtime/linking and retained by filtered views. Intro is a sequence; byte comparison proves this is the only FirstContact source change, preserving all transcripts. Saved root remains FirstContact. Checks: 95 story, 93 rehearsal, seven source assemblies passed (existing rehearsal CS0649 warning). No native operation.


## W05 - typed wardrobe commands

Added five mandatory-awaited wardrobe commands, binding kinds 8/9, exact source-owned outfit/hair rows, reconciliation preserving existing rows and ownership, dependency tracking and cue-inspector assignment fields. Hair assignment requires a generated Project prefab root; actor diagnostics require its exact rig manifest/generation/signature. Outfit readiness reuses PerformerWardrobe.TryValidateReadiness for body, release and all reachable fit checks. Host uses the existing scoped queue, preserving benign layer extremes and clothing-only SetOutfit hair semantics. Visible changes request 0.25+0.25 seconds; hidden changes stay hidden. Central RequiredChannels handles Body/Visual/Wardrobe and existing dissolve locks. Bound appearance readiness feeds mandatory diagnostics and candidate exclusion. Checks: 98 story, 93 rehearsal, 18 wardrobe and seven source assemblies passed; native slot/transition observations await B/C/D. No production assets assigned and no Unity operation.


## W06 - ownership and snapshot foundation (in progress)

Added run-local parent/child scopes with explicit resource-keyed cleanup, single return/adoption states and linked cancellation. Accepted operations capture their scope before starting; completed operations leave the active list while retaining only current generation ownership. StoryMachine prunes completed global and entry child tasks while preserving its terminal failure signal. Root context/phase cleanup remains root-owned. Child UI actions have exact leases and cannot hide an ancestor action; event waits use the captured scope cancellation. Child StopPlayback cannot stop a parent-owned live driver. Finite presentation barriers exclude interpreter/Director tasks and stable tracking, and wait for native transition stability.

Added immutable entrance records and owner capture seams for Lara, seat/overlay/cached frames, wardrobe release/config/fit, exact material profile (including explicitly selected regular profiles), expression/gaze settings/target, aura, grip hold/follow driver, original player body, camera root/rig/offset/projection/tracking settings and posture observation. Added side-effect-free return validation/diff/verification and resource pin acquisition service. These are not yet connected to card execution; existing card completion behavior is unchanged.

Current checks: 98 story, 93 rehearsal, 22 wardrobe/scope tests, seven source assemblies PASS. Four new managed tests exercise the real scope type; they do not certify native presentation restoration. Existing rehearsal CS0649 warning remains. W06 stays IN_PROGRESS: native card scope lifecycle/bridge, exact endpoint return adapters, ownership adoption of restored writers and the full cancellation/return checks remain. W07 choreography and Gate B delivery are not ready. No new scene/test fixture, production asset change, Unity operation or remote write occurred.


## W06/W07 completed source delivery - October 20

Every direct, explicit Director, drawn and nested card now uses the same presentation boundary: settle entrance, capture typed controls/pin wardrobe resources, execute and settle finite work, prepare/restore/verify entrance, adopt ownership, then allow normal completion/progress. Sequences retain changes unless enclosed by a card. Missing presentation hosts fail before body execution. Stop/body/return faults skip normal completion; disposal does not animate recovery. Scope/resource and machine task collections remain bounded.

Native adapters restore base/seat/arrival poses, exact frames, original male body, wardrobe/hair/layers/fit, shader profile, expression, gaze, aura, grip/follow and camera fixed quaternion or original tracking rule. Changed pose/expression/gaze/view controls blend for 0.5 seconds; relocation and appearance share conceal/commit/reveal. Unchanged controls keep their owners. Parent motion remains on its live clock with no restart or hardware replay. Presentation-derived posture is reconciled; arbitrary story variables and tuning are not rewound.

Gate B uses a copy of Assets/Scenes/FirstContact.unity and isolated diagnostic wardrobe asset copies. The actual production interpreter/host/return service run its cases. The source templates compile/link in managed tests. The delivery window saves the composition only when Art requests it. An untouched interrupted scene copy can be retried; authored differences are preserved. No diagnostic sources enter global catalog membership. One diagnostic composition only, retired after acceptance.

Checks: 108 story, 93 rehearsal, 25 wardrobe/scope tests PASS; all seven source assemblies PASS. These are source/managed evidence, not native visual acceptance. Source hashes and preservation audit: evidence/w06-w07-checks.json. Native Gate B report has not been generated by the executor. W08/W09 have not started.

Existing working-tree serialization changes in Lara.prefab, the two production bindings, global catalog fingerprint (same source membership), ProjectAuditorSettings and unrelated ticket archives were preserved and excluded from this implementation commit.


### Gate B delivery failure - no controls in Play

Art reported no Gate B UI. Read-only inspection confirms the saved GateB_ProtectedCards.unity is byte-identical to the initial copy recorded by scene-copy.sha256 and contains no runner script GUID. The exact throwing operation is not recorded because the window previously caught exceptions into its report string without logging. No runtime/native pass is claimed. The generated copy/assets were preserved.

Preparation now reloads source/diagnostic assets after OpenScene (which can unload stack-only assets), names each setup stage, reports missing references individually, verifies the full active runner before and after save, and displays an explicit GATE B READY success dialog or a failure dialog with the full exception. Errors are logged to Console. The existing untouched copy is eligible for the already bounded retry path; edited content remains protected. Source compilation: all seven assemblies PASS. Executor performed no Unity operation. Art must exit Play and retry Create / open Gate B scene; enter Play only after GATE B READY. If preparation fails, its dialog now contains the actionable cause.


### Gate B confirmed destroyed-bindings error

Art supplied the preparation message: StoryContentBindings was destroyed but still accessed (target). The builder retained companion objects across WriteSource/ImportAsset as well as OpenScene. Import can recreate companions; the following SetDirty call then receives a destroyed target. Replaced retained companion mutation with path-based UpdateContent: load current object, mutate, mark/save immediately, never reuse after save/import. Story assets are reloaded after changed-pose companion saves before attaching the saved runner. Existing scene-open reload remains. All seven source assemblies compile. Native retry and Gate B acceptance remain pending; no Unity operation was performed by executor.
