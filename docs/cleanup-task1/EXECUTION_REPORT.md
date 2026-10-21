# Follow-up status — 18 October

The new user-authorized F00–F04/U01–U07/S01–S11 packet supersedes this initial partial cleanup state. All eleven obsolete scene/meta pairs are now retired after scene-free caller/test extraction. No relocation into a lab was performed. [Current follow-up report](../archive/docs/p2-p3/PHASE1_RETIREMENT_REPORT.md) records actual results and current native checks NOT RUN; [active progress](../archive/docs/p2-p3/PROGRESS.md) remains authoritative. The initial report below is preserved as historical evidence, not rewritten as an earlier success.

# Task 1 cleanup — execution report

Updated 18 October 2026. **First Contact is prepared for user-controlled manual Play in Unity. Overall Task 1 technical status: PARTIAL**, because consolidation of eleven retained diagnostic scenes into one development lab was rejected by automatic approval review and was not executed. Delivered changes pass the checks below. This is not a claim of full cleanup completion or human visual acceptance.

## Delivery and ownership

- Starting branch/HEAD: main / bc596962aff6206fb54cc189de42defd270ddeda.
- Current branch/HEAD: main / f74d026 (Record Task 1 recovery and dependency baseline). Only C00 is committed. Later implementation is local and uncommitted; no branch, push, Play, automated scene playback, build or player was started.
- Recovery ref: 18b268afe4df1ce11046a760b757002571a104c6. Baseline archive SHA256 eb3f7b480bd7a6e6fccce7b8927ec00ed8d54806a82b9fdbb1125e937a2575b7; C00 delta SHA256 898727bc829c155fcd3dc8c0e19a04bc53aa2646877417f4efd39cfecf2a0aa6. Private records: .dazposewizard/cleanup-task1/recovery-manifest.json and starting-status.txt.
- Pre-existing expression archive/import cleanup, original ticket-folder moves, untracked ticket packet, and unrelated ProjectAuditorSettings edits remain separately owned. Raw Git totals include those changes and do not represent Task 1 deletion savings. No blanket staging, reset or stash.
- Protected HOLDs: canonical FBXs/manifests/texture sources; configured import roots; Lara material trees whose paths participate in wardrobe generation identity; immutable releases, published reviews, user wardrobe layers, live/referenced draft closures and pending toe fixtures. See asset-dispositions.json and abandoned-draft-audit.json.
- Required unexecuted action: [exact lab consolidation plan](PENDING_LAB_CONSOLIDATION.md). This is a blocker for full packet completion, distinct from protected HOLDs.

## Ticket disposition

| Ticket | Status | Evidence / delivery |
|---|---|---|
| C00 | PASS, f74d026 | Recovery files/hashes, GUID baseline, actual Unity dependencies and baseline tests |
| C01 | PASS, local | Historical automatic migrations and stale playback resumes removed; supported import reconciliation and unchanged-failure suppression retained |
| C02 | PASS, local | Twelve helper/script moves preserve metas/GUIDs; Production Editor no longer references Validation/Examples; relocated navigation has Presentation/Content references |
| C03 | PASS, local | Production ClothingSetupStartup; explicit saved startup migration; durable identity-aware wardrobe review metadata with stale/corrupt handling |
| C04 | PASS, local | Supported top-level DazPose menus, one nondestructive advanced repair, source/typed-binding navigation; no active old Tools MenuItem declarations |
| C05 | PASS, local | Global tag-discovered cards, one generated runtime index, source-owned typed bindings/private helpers and one flat interpreter/Director/history/RNG/Stop scope |
| C06 | PASS, local | Only MainMenu/FirstContact public; Showcase under Examples, obsolete Welcome/Companion producers detached, saved original environment retained |
| C07 | PASS with protected HOLDs, local | 163 original asset moves verified; exact current producer/loader paths repaired, no art rebakes/model imports |
| C08 | PARTIAL, local | Eight scenes, inactive JSON and two abandoned draft roots retired; exact dependent candidates retained; required single-lab consolidation rejected/not executed |
| C09 | PASS, local | Current authoring/import/ownership/menu/global-card guidance and handoffs updated; historical evidence retained |
| C10 | PARTIAL | Delivered-scope technical gates pass; final manifests/checklist prepared; lab consolidation remains blocked; human Play/visual acceptance pending |

## Implementation

Historical lighting/smoke/outfit/partner reload migrations and stale validation auto-resume paths no longer execute. Useful explicit diagnostics and supported import queue behavior remain. The expiring local Editor request broker belongs to Validation.Editor and never starts Play.

Clothing Setup uses saved art-only Void data and production startup ownership. Review metadata is stored in ProjectSettings/DazPose/WardrobeReviewMetadata.json by outfit/release/generation identity; actual legacy review pointers were migrated without retuning. Isolated metadata checks cover authoritative stale/corrupt identity and legacy fallback.

Seventeen tagged selectable cards now live in Assets/Production/Story/Cards/FirstContactEngineering.dazstory. Local setup, Intro and sequence remain in Assets/Production/Story/Programs/FirstContact.dazstory. Each source owns its typed companion; scene roles resolve through active scene bindings. Import changes discover tagged cards globally and refresh Assets/Resources/DazPose/GlobalStoryCards.asset. No per-scene library registration exists. Missing scene resources reject the affected card; invalid owned code/assets invalidate publication. Cross-source calls retain one machine, Director, random stream, history and cancellation scope. Public tag IDs are global; private helpers remain source-local. Indexing handles add/edit/move/remove and synchronously catches publication up when the user initiates Play.

FirstContactD1D5.unity became Assets/Production/Scenes/FirstContact.unity with saved bytes/GUID/persistence IDs preserved. Showcase moved to Assets/Examples/Scenes/AuthoringShowcase.unity; fourteen unused pure-transform markers were explicitly pruned and its art wrapper renamed Void Environment. PlayerBody still uses the prepared male Partner/contact setup. New scenes copy saved VoidEnvironment art, not a test hierarchy.

The 163 verified asset moves include the two scene moves, Void art, male mesh/shell/shader/support plus imported material/texture outputs, Lara mesh/surface/dissolve configuration, locomotion, seating, normal gesture/motion content, sound/motion source, grip profile and published magic assets. Exact producers/loaders now use their owned destinations. See completed-moves.json; final-production-move-plan.json records the last 113 moves and execution IDs. Twelve C02 script moves are separately recorded in path-consumers.md and are not added to the 163 content count.

## Verification

| Check | Actual result / evidence |
|---|---|
| Managed story tests | PASS: 60; dotnet test tests/DazPose.Story.Tests/DazPose.Story.Tests.csproj --no-restore --verbosity quiet |
| Managed application tests | PASS: 69; dotnet test tests/DazPose.Tests/DazPose.Tests.csproj --no-restore --verbosity quiet |
| Unity source compilation | PASS: seven assemblies, installed Unity 6000.5.9f1 csc, warnings as errors; .dazposewizard/cleanup-task1/compile/results.json |
| Actual Unity compilation/refresh | PASS: repeated idle Editor refreshes; latest ff42ca0d3f054e13aa8fa5c432101435; subsequent compiled checks executed in the live Editor |
| Global cards G1–G7 | PASS: 19 EditMode assertions; ae4d6c660b334a6d8128e02bdf2b5d07; TestOutput/CleanupTask1/global-card-index.json |
| Global lifecycle and unchanged existing scene | New source discovery/removal without original source/companion/scene/catalog edits, source edit/move, invalid binding/recovery, two distinct real pose owners, resource eligibility isolation, no-op second refresh, runtime dependency closure |
| Flat execution / host adapters / Stop | PASS: original full-session managed StoryProgramSequence/runner adapter regressions retained, linker/global regressions added; no actual Unity gameplay |
| Authored oracle | All 20 original declarations preserved after newline normalization; story-declaration-provenance.json; 18 cards and 19 fixed dialogue entries retained |
| Seeded Director continuity | Captured seed82017 selected order and Warmup110/Main105 progress reproduced; .dazposewizard/cleanup-task1/seed-82017-baseline.json |
| Final saved First Contact | PASS: 16 assertions; 132b2263e6ad46bb8fc7f9c58c3c9812; TestOutput/CleanupTask1/final-structure.json; six imported poses, seven marks, no cutoff, valid linked bindings and prepared male/contact |
| Retained saved references | PASS: a68f0c934dd044c29e18b84c784a56b1; ten scene/prefab roots including frozen Void, toe, FirstContact, Showcase, ClothingSetup, Lara, PlayerBody and Partner; no missing scripts/nonzero broken references; generated Lara output probe passed |
| Scene opens / repeated repair | PASS: seven assertions, latest report TestOutput/CleanupTask1/scene-open-and-repair.json; opens do not dirty scenes, repeated repair does not rewrite valid scene/bindings |
| Retained Showcase/FirstContact host check | PASS: fc73662a4ead448191d1eacb241572e4; already-loaded user scene remains open/clean |
| Import unchanged-failure guard | PASS: b838632ae5824bf29c2dc27b014107ff; failed/changed/explicit-retry and unchanged/changed successful typed fixtures |
| Real pose import | PASS: e725f8e526524919b9a073893bcfac04; copied Aiko StandA converted, unchanged call preserved status/report/time/GUIDs, changed copy reconverted with stable GUIDs; seven assertions including exact fixture/status/report cleanup |
| Import report | validation/DazPoseUnityValidation/.dazposewizard/reports/cleanup-import-pipeline-ce36d6a5c0e448b9a50c1f2898c39525.json |
| Archived failed expressions | All 34 C00 recovery files in archive closure hash-verified, including prior 33-file archival payload; no archived failed input rehydrated |
| Wardrobe review metadata | PASS: 17 isolated managed assertions plus actual migration dd5cf674ea234c28aa06c0cdb10b0668; pointer continuity retained |
| GUID/meta and move audit | PASS: 4,398 GUIDs, no duplicate GUIDs, no missing metas; every completed content move verified against final asset/meta hashes and original GUID |
| Protected content | PASS: 666 unchanged files, three explained changed files, two intentional JSON/meta removals; no unexpected protected changes |
| Art/rig/wardrobe | Frozen original, toe fixture, immutable releases and protected settings byte-identical; scene/prefab/subasset checks resolve after moves; visual acceptance remains pending |
| Catalog/build membership | MainMenu + FirstContact only; stable scene ID c3bbf40d9a2949ffac3e068b552ed71c and actor ID dcb35969338343078cf45bb33b726b3b |
| Git hygiene | git diff --check PASS with Windows CRLF handling; only three newly introduced story/startup whitespace defects removed; raw status/stat captured in ignored cleanup evidence |
| Play / build / player / hardware | NOT RUN by instruction; human acceptance MANUAL PENDING |

Actual request reports are under .dazposewizard/cleanup-task1/editor/<id>.result.json. Unity TestOutput paths are under validation/DazPoseUnityValidation. These results are EditMode or managed/fake-host evidence, not visual playback.

## Scene and retirement manifests

There are 28 retained .unity files under Assets. Eight scenes were actually retired: AuthoringWelcome, AuthoringCompanion and six unused vendor demonstrations. Package runtime/editor code, source animations, shaders, licenses/readmes and provenance remain. The inactive JSON sample is preserved byte-identically at docs/story-runtime/history/FirstContactD1D5.story.json, with runtime compatibility APIs retained.

Two abandoned draft roots (02aa66ebd298461cb41043fd44d637e1 and 94c58ce304554ea4bef67c908ad40e6c) were deleted only after recovery/hash and Unity incoming-dependency checks. ffc251, SetupCandidates/685f and live a659 roots remain for documented serialized consumers. The exact current consumer/replacement/recovery decisions are in retirement-manifest.csv, asset-dispositions.json, abandoned-draft-audit.json and remaining-retirement-decisions.json.

Actual verified deletion total: **116 files including metas, 287,660,094 bytes** (287.7 MB decimal). This excludes the user's earlier expression cleanup, mere asset moves, ignored data, Library, builds and hypothetical candidate savings. final-delivery-audit.json lists every deleted recovery record.

The protected-file differences are only the authorized Showcase pruning/wrapper rename, local story extraction, and Clothing Setup startup component replacement/empty field whitespace. FirstContact scene bytes remained unchanged through its rename.

## Blocked action and protected HOLDs

Automatic approval review rejected the grouped eleven-scene lab relocation plus source/script path updates, citing active legacy callers and inadequate authorization for that exact migration. The command did not execute. Existing diagnostic scenes and their consumers remain intact at their original paths. [PENDING_LAB_CONSOLIDATION.md](PENDING_LAB_CONSOLIDATION.md) is the concrete path/GUID/consumer plan for approval; full C08/C10 acceptance remains PARTIAL until it is resolved.

The eleven scenes still provide specialized diagnostics without equivalent replacement coverage, so deletion is not proposed. They remain outside public catalog/build membership. Other fixture candidates remain only where actual regressions, generated-path producers, published reviews or protected toe closure require them. These conditional retention decisions do not authorize arbitrary removal.

Lara material paths affect WardrobeCatalogBuilder.CharacterSignature; moving them would change accepted generation identity. They remain intentional protected HOLDs with their source/release closures. General binding UX redesign, Scene Director Studio, UI modernization and new authored content remain out of Task 1 scope.

## Current authoring and manual Unity handoff

- **DazPose > Scene > New Performance Scene...** copies Assets/Production/Environments/VoidEnvironment.unity.
- **DazPose > Wardrobe > Open Clothing Setup** opens Assets/Scenes/ClothingSetup.unity.
- **DazPose > Story > New Global Card Source...** creates source and typed companion; tagged cards are discovered automatically.
- Source Inspector opens readable text and typed bindings; **DazPose > Content > Advanced > Reindex Published Cards** explicitly rebuilds the shared index.
- **DazPose > Content > Advanced > Set Up SALSA Lip Sync** is the current setup path; runtime/setup help now points to it.

The open Editor has Assets/Production/Scenes/FirstContact.unity loaded clean in Edit Mode. The user may perform the manual test now. Inspect MainMenu navigation, then full FirstContact Intro through Resolution, clear English Console flow, voice/lip sync, five standing/one sitting pose assignments, player reveal/contact, wardrobe, locations and Stop. Inspect ClothingSetup draft/release/review behavior and the maintained Showcase separately. Compare frozen original Void without saving over it. Second-outfit toe appearance remains a separate unapproved observation.

No human Play, aesthetics/pacing approval, standalone packaging or hardware result is inferred. Full cleanup completion remains partial for the explicitly blocked lab move; manual scene testing readiness is separate.

## Final Git attribution

Raw status: 371 entries. Raw tracked diff before this final report: 300 files, 717 insertions and 520,755 deletions; this includes pre-existing expression/ticket changes and unrecognized-as-rename moved files. It is not the cleanup savings metric. Exact captured files: .dazposewizard/cleanup-task1/final-status.txt, final-diff-stat.txt and final-diff-check.txt. New helpers/assets/docs are represented in status rather than tracked diff until staged. No later cleanup commit or push is claimed.

Earlier broad animation relocation and whitespace sweeps were rejected; only subsequently reviewed narrower per-asset moves and exact introduced-whitespace cleanup executed. The later lab relocation rejection remains unresolved; no indirect retry was performed.

## Explicit commit/push authorization — 18 October

The user explicitly requested committing and pushing this delivered state on main. The commit includes the reviewed cleanup, earlier failed-expression cleanup, and ticket archive relocation. The rejected lab move remains unexecuted and full Task 1 status remains PARTIAL. Large prepared motion animation outputs use Git LFS via .gitattributes; fresh checkouts require LFS assets. No Play/build/player execution is implied. Earlier statements about uncommitted work describe the pre-commit checkpoint.

Implementation checkpoint committed as a9a31be (`Consolidate production authoring and global story cards`) on main. This supersedes the pre-commit local/uncommitted entries above. C00 remains f74d026; the blocked lab action was not included.
