# Day 1 executable baseline

Execution date: October 5, 2026. This ledger records current execution, not the earlier review agents' results. Product repairs remain assigned to the seven-day plan.

**Identity and isolation**

- HEAD `fff898b5a89cd23971a58704e4fad9e78cd650ea`, branch `main`; Unity `6000.5.9f1`.
- Main project: `validation/DazPoseUnityValidation`. Disposable project: `.dazposewizard/p0c-native-generation`.
- Initial working changes: root `AGENTS.md`; untracked seven-day plan, consolidation documentation and `docs/sprint1 wrapup reviews/`. No commit/stage/push was performed.
- User closed Unity and permitted batch execution, including main if necessary; all mutation actually remained in isolation. No computer control or Unity UI editing was used.
- Full Assets/Packages/ProjectSettings staging preserved asset GUIDs, embedded packages and licensed local inputs; Library was rebuilt. Existing wardrobe wrapper's selective synchronization alone omits Player source and is insufficient to establish fresh asset identity.
- Source/input manifest: `.dazposewizard/day1-evidence/input-manifest.json`, 3,791 files; SHA256 `2266934118c0ec17e7996a4b214c01cf61d0f6836aea360be5a5a84130db8578`. Coverage includes first-party runtime/editor/Player, scenes, wardrobe, partner proof, TestData, Packages and ProjectSettings. It is not a complete vendor texture/binary hash manifest. Staged sizes and launch identity are in `asset-sizes.json` and `identity.json` beside it.
- Initial status snapshot: `.dazposewizard/day1-evidence/git-status-before.txt`.

**Existing validator ledger**

| Check | Command / entry | Observed result | Evidence |
| --- | --- | --- | --- |
| .NET core | `dotnet test tests/DazPose.Tests/DazPose.Tests.csproj --no-restore --logger 'trx;LogFileName=day1-core.trx' --results-directory .dazposewizard/day1-evidence/dotnet` | 69 passed, zero failed/skipped | `day1-evidence/dotnet/day1-core.trx` |
| Wardrobe Runner | `scripts/run-wardrobe-execution.ps1 -Stage Runner` | complete, passed; 3/3 assertions | isolated `TestOutput/wardrobe-execution/runner/report.json` |
| Wardrobe Contract | same wrapper, `-Stage Contract` | complete, passed; 13/13 assertions | isolated `TestOutput/wardrobe-execution/contract/report.json` |
| Wardrobe Migration | same wrapper, `-Stage Migration` | complete, failed; 6/7 assertions pass | isolated `TestOutput/wardrobe-execution/migration/report.json` |
| Runtime/Layers/Setup/Persistence/Integration/Player | prerequisite-ordered wrapper stages | **not_run** after Migration failure; no inferred passes | first failed prerequisite above |
| Grip geometry | `Day1Diagnostics.GripProbe` wraps existing `HandGripValidationRunner.Run` | 60 geometry/palm/reach/synthetic-contact assertions pass | isolated `TestOutput/day1/grip-probe.json`; `TestOutput/TargetDrivenGrip/tests.txt` |
| Current saved grip profile | same diagnostic invokes `IsReady` and existing `HandGripSpatialProof.Run` | ready; expression regressions and all 15 radius/path + 10 tilt/twist cases pass; saved profile bytes unchanged | `TestOutput/day1/grip-probe.json` |
| Full grip live proof | existing `HandGripLiveProof.Run` | **not_run**: enforces a different `handgrip-validation/project` path; Day 1 used current saved-profile/spatial evidence rather than recalibration or bypassing that guard | `Assets/DazPose/Editor/HandGrip/HandGripLiveProof.cs:37` |

Wardrobe reports are fresh and record source hash `e7a6f2667263d40567a7a2e4ba282ab8dc662d49c9d4550e9b3565115638ccc2` and input hash `86e5fe53ecb9dff8c71b4a0f94d3d0a192766831daec7a0788b3216ead2d2298`. These runner hashes have narrower coverage than the explicit manifest. Fresh isolated observation helpers were introduced only after those baseline gates.

**Migration failure diagnosis**

`stableSecondGeneration` claims to expect “same catalog GUID,” but its boolean also requires every known preset to have exactly two fit states, with masks 0 and 1. Current first-outfit has three validated states, masks **6, 2, 0**, because saved assignments place its pieces on layers 2 and 1. Thus the first-outfit fit-count condition demonstrably fails. The observed catalog GUID is unchanged: `31ff3f8225fdb364482ac21d466610e3`. Do not diagnose GUID instability from this misleading composite assertion. Other composite terms were not separately reported by the existing runner.

Day 5 must make Migration validate the current supported assignments/reachable masks and report idempotence/GUID/fit failures separately. Preserve genuine idempotence and geometric acceptance checks; do not waive the gate.

**Actual Clothing Setup Save/Publish reproduction**

Local editor diagnostic uses the actual `ClothingSetupWindow` working session, private Save handler and Publish handler through reflection. It opens the staged real ClothingSetup scene; it does not replace Save with direct asset setters. Evidence: isolated `TestOutput/day1/save-probe.json`, log `.dazposewizard/day1-evidence/save-probe-r2.log`. First attempt failed solely because the diagnostic referenced unavailable ScreenCapture; removing that diagnostic API allowed observation. No product-code repair occurred.

| Observation | Configuration / preset / fit result |
| --- | --- |
| Original candidate | revision 2; panty layer 2, PeekaBoo layer 1; preset metadata lists both at 0; compiled masks 6/2/0 |
| Change panty 2 → Base; actual Save | revision 3 persisted; compiled masks 3/1/0; preset metadata remains 0/0 |
| Reopen scene/assets | saved assignment and compiled masks retained |
| Actual Publish | original catalog released generation `beb88e27609530f2c7b39399d46cd14f9ec8a95adf6f14e74a32c7ffd48f5045`; revision 4; preset metadata now matches 0/1; 3 presets, 7 fit states |
| Change panty Base → 1; Save released catalog | rebinds to candidate `Assets/Wardrobe/Candidates/working-6776ce8afe357ada/WardrobeCatalog.asset`; revision 5; compiled masks 2/0; candidate preset metadata remains 0/1 |
| Prior release preservation | original catalog/config/preset byte hashes remain unchanged after forked Save |
| Publish changed candidate | generation `48caa3b738db3be5c159d686e20a9f8d40e3ee71f2cd40a7cf9682e2e8c81044`; revision 6; preset metadata now matches 1/1; 3 presets, 6 fit states |
| Mismatched configuration generation | actual edit-mode resolver returns `ConfigurationApplies=False`; falls back to preserved release's preset layer 0. Guard retained; this is an intentional fallback observation, not runtime visual acceptance |
| Unsupported layer 3 | actual edit session rejects with “Layer must be Base, 1 or 2.” |
| Deliberately duplicated working assignment | actual Save reports duplicate-key failure before configuration commit; bound config bytes unchanged and revision remains 6. The duplicate was diagnostic corruption, not an ordinary UI-produced edit |

Candidate preset metadata lag is a real author-facing consistency problem; publication does synchronize it. It does **not** prove published outfits ignore saved assignments. Keep this distinction in Day 5's resolved-snapshot redesign.

**Runtime wardrobe observations**

Independent headless Play Mode observation does not constitute a pass of the prerequisite-blocked wardrobe suite. Evidence: `TestOutput/day1/wardrobe-runtime.json`; log `day1-evidence/wardrobe-runtime.log`.

Fresh binding to changed release applies first-outfit at revision 6, populated/visible mask 2, both garments in layer 1. Removal applies mask 0, retains first-outfit and `CanAddLayer=True`; addition restores mask 2. Explicit `OutfitAsync("unclothed")` clears selected outfit/populated mask and `CanAddLayer=False`; subsequent addition returns `NoChange`. The window's “Naked” button dispatches this explicit-unclothed command. Its label hides the distinction from temporarily removing the selected outfit's layers; Day 5 should make the artist's intention clear.

An initial secondary actor clone retained the old catalog generation but failed Outfit with `EffectRebindFailed`. That diagnostic duplicated an actor without remapping its outside effect-rig references; it is **inconclusive fixture evidence**, not an established old-release product defect. A separate whole-scene binding probe removes that ambiguity: `TestOutput/day1/prior-release-runtime.json` applies original generation `beb88e…`, revision 4, mask 3 with panty Base and PeekaBoo layer 1. Removal yields masks 1 then 0; addition restores Base. The preserved release actually runs its original layering, independently of the changed release's mask 2.

Another actual Save forks a revision-7 candidate with panty Base and PeekaBoo layer 1, compiles masks 3/1/0 and persists/reopens its binding. Evidence: `candidate-save.json`, `candidate-runtime.json`, `candidate-runtime-r2.json` in the same output directory. String `OutfitAsync("first-outfit")` returns `Failed/NotReleased` even with preview enabled because catalog ID resolution requires release. The actual window's Load button uses the **preset asset overload**: that preview applies revision 7, mask 3, removes masks 1 then 0, and adds Base back. This is an overload distinction, not a broken Load-button reproduction. Future preview tooling should make it explicit; ordinary content still requires a release.

Actual runtime mismatch probe (`mismatch-runtime.json`) binds the original release to a cloned config with wrong generation and panty deliberately reassigned to layer 2. Runtime applies the release's preset layers Base/1 and mask 3, not the mismatched draft's mask 6. Its query reports the supplied configuration's revision 5 even though those assignments were ignored. That provenance ambiguity belongs in Day 5's resolved snapshot; the generation guard itself remains effective.

Actual invalid draft probe (`invalid-probe.json`) marks a working session dirty and duplicates one assignment. Save refuses before configuration commit; Publish refuses until edits are saved/reverted. Candidate config bytes and prior release catalog/config/preset bytes all remain unchanged. This extends the earlier duplicate-save observation through the real Publish boundary.

All six Task E cases have observations, including successful, intentionally rejected and provenance-ambiguous behavior. The wardrobe suite's blocked stages remain unrun; these independent probes do not retroactively pass them. Unused packaging fields in wardrobe-only report DTOs (for example `ready=false`) are defaults, not wardrobe assertions.

**Contact readiness**

Production partner contact adapter is absent. `GripContactRod` is the current production-source MonoBehaviour implementing `IGripTarget`; `HandGripRuntimeSelfTests.LinearTarget` is a synthetic test implementation. `PartnerAnatomyTestController` and the prepared PartnerCharacter prefab supply anatomy/morph/shell data but do not implement that contract. Grip exists in HandGripAcceptance and is absent from FirstPerformanceVoid's composition; wardrobe lives in ClothingSetup. Day 2 composition and Day 4 runtime partner extraction/target adapter are still required. Rod and saved-profile passes do not certify member contact or compliance.

**Windows probe, final identity and remaining observations**

Final independent Windows build **succeeds**, 0 errors/12 warnings, 305,983,412 bytes, Mono2x. Its graphics-enabled batch launch exits 0 on Direct3D12/NVIDIA RTX 4060 Ti: actorCount 1, runtime ready, pose `Settled`, visibility `Visible`, speech started and `Finished`, camera present, one skinned renderer. `visibleRendererCount=0` in the batch run; the subsequent manual check passes appearance/audio/lip sync and records a visible renderer (see completion below). These observations concern the packaging fixture, not full production composition or physical toys.

Reports: isolated `TestOutput/day1/build-probe.json`, `TestOutput/day1/player/day1-player.json`; immutable launch snapshot `.dazposewizard/day1-evidence/player-r3-result.json`; logs `build-probe-r3.log` and `player-r3.log`. Build scene/settings/packages/helper manifest `.dazposewizard/day1-evidence/build-inputs.json` SHA256 `dfeaf6241d9325b6c95baaccb1ce4188d9d5057627a6df04a7efe2937b6ddbb9`. Earlier build/launch attempts and diagnostic-only repairs are retained in `DEPENDENCIES.md`; their lack of reports was not a demonstrated product pose failure.

Source preservation check rehashed all 3,791 original manifest entries: **zero changed/missing files**, `.dazposewizard/day1-evidence/source-preservation.json`. Supplemental script/vendor hashes are in `supplemental-inputs.json`. Source code, accepted asset bytes and GUIDs remain intact. Tracked edits outside consolidation documents only update root authority and wardrobe execution documentation to distinguish historical passes from this baseline. No commit/stage/push, physical output, computer control or asset publication back to main occurred.

See `HANDOFF.md` for completed manual verification and the next action. No graphical appearance or user approval is inferred from batch execution.

Evidence ledger: .dazposewizard/day1-evidence/evidence-ledger.json; SHA256 214b8a2da7bc8813511ebe9d02900438ef11a3eb85f46fa088a495e45b803ae6. It hashes retained reports, logs, manifests and local diagnostic sources; generated mesh copies/build binaries are excluded. Final source preservation was verified at 2026-10-05T08:54:25Z with zero differences.

**Manual completion, October 5, 2026:** User confirms Lara appeared, speech was audible and lip sync worked. Fresh standalone report at 2026-10-05T09:14:26.8601323Z records runtime ready, Pose Settled, Speech Finished, visibility Visible and visibleRendererCount 1. This resolves the earlier batch appearance uncertainty. Immutable report: .dazposewizard/day1-evidence/player-manual-result.json; user/executable/log identity: manual-completion.json, SHA256 f916afc56336dfde19c3d14ff6fb9307957146e9fa458e4da29c91c20747e75e. Task F and Day 1 are complete; existing failed/unrun suite results are retained.

