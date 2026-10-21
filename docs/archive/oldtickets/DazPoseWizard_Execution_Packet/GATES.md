# Technical gates and honest delivery

## Before any deletion/move

Record clean or user-owned dirty worktree, source HEAD, SHA-256 protected source/meta manifest, Unity GUID uniqueness, dependency graph from serialized refs **and** code paths/strings, producer/loader/review indices, package/source imports, and current saved-scene identities. Maintain recoverable git history and out-of-tree archival snapshots for ignored sources without deleting their only copies. Never `git clean -fdx`.

## Cleanup gates

- Test exact callers extracted from eleven obsolete scenes; no default production importer/authoring command opens them, including hidden/development menu paths and triggered static hooks.
- No dead scene in `SceneCatalog` or Editor Build Settings; only Main Menu + First Contact as player entries at current baseline.
- Unity edit-mode saved MainMenu, FirstContact, VoidEnvironment, ClothingSetup, Showcase, Lara, male PlayerBody and Partner references resolve with no missing scripts. Protected original art/rig/releases have unchanged source/meta hashes except audited authorized migrations.
- `dotnet test tests/DazPose.Story.Tests/DazPose.Story.Tests.csproj --no-restore`, `dotnet test tests/DazPose.Tests/DazPose.Tests.csproj --no-restore` (install/restore if needed), Unity 6000.5.9f1 editor compile/test in EditMode. If GUI unavailable, use source compilation and report that precise limitation; never pretend an editor test passed.
- Golden seeded FirstContact draw/order/progress test and no-scene-edit card publication test; global index remains correct; import reconciliation and wardrobe review identity intact.
- Actual saved scenes open without dirtying unexpectedly. `git diff --check`. Unresolved retirements are listed as failures or protected actual-user-work exceptions with evidence, not silently labeled PASS.

## Phase 2 gates

- Start from MainMenu, select FirstContact, Stop/Return to menu; no duplicated `GameApplication` or session. Real profile edits from Settings or scene prompt yield same values.
- 100 synthetic kink and 100 equipment catalog fixtures (NOT written to real catalog), 1280x720, 1920x1080, 2560x1440; no clipped list/footer/control/scroll; search/filter persists draft edits, focus traversal sensible and choices directly set.
- Apply writes once and recomputes current unchanged submission formula; Cancel, Esc, scene change and Stop never commit. Existing user profile and unknown catalog IDs round-trip. Clothing prompt still updates World only on confirm. No change to owned/willing semantics.

## Phase 3 gates

- Bare new script with unbound `SexyVamp`, `Bedside`, and text dialogue compiles to requirement manifest with correct typed slots, diagnostics, and zero manually typed IDs. Existing FirstContact binding asset/source GUID/choices retained exactly after reconciliation.
- Drag-drop correct types binds; incorrect types rejected. Adds/removes/renames source symbols preserve stable assignments or show safe orphan, never silently delete. Recompile twice with unchanged source yields byte-/semantic-idempotent binding output; generated index not mutated spuriously.
- Studio preview uses actual Lara prefab, saved Void scene art and player body contact/seat anchors in isolation. Preview tick has no change to live scene/prefab/profile/catalog/settings. Explicit Commit Mark modifies only intended author object through Undo; Undo restores it. PlayMode never entered by Studio.
- Rehearse FirstContact Intro and setup, clothing branches (0,2,>2), Warmup Director tags, nested global card body, Main standing/seated transition, Climax event race and Resolution. Trace source locations and chosen/forced decision labels. Preview never sends toy/hardware output or writes profile; initial runtime machine/Director logic shared.
- Global new tagged card added in test fixture becomes candidate for unchanged FirstContact; removing it removes it. No scene membership list, no shadow card registry.

## Human manual acceptance

Executor leaves correct authored saved scenes ready. User alone enters Unity Play and validates staging, proportions, visual polish, prompt comfort, transitions and actual FirstContact. Do not start standalone build/player or Bluetooth commands; functional edit-mode evidence is separate from human visual acceptance.

## Delivery artifacts

`docs/p2-p3/PROGRESS.md` with per-ticket PASS/PARTIAL/FAIL and evidence; `docs/p2-p3/PHASE1_RETIREMENT_REPORT.md` with exact deleted/retained scene matrix; `docs/p2-p3/IMPLEMENTATION_REPORT.md`, `docs/p2-p3/MANUAL_ACCEPTANCE.md`, changed-file summary, test results, starting/ending git SHA and commits. No unapproved push.
