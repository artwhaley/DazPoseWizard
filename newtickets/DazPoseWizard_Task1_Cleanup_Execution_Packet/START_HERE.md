# TASK 1 — DazPoseWizard production cleanup / execution packet

**Code-reviewed execution plan — execution authorized and started 18 October 2026.**

The subsequent user execute/continue requests authorize implementation. Current verified state and outstanding gates are recorded in docs/cleanup-task1/EXECUTION_REPORT.md. Read [REVIEW_RESOLUTION.md](REVIEW_RESOLUTION.md) for verified code findings and scope corrections; this packet does not itself authorize cleanup, commits, Play, builds or launches.

Repository: `https://github.com/artwhaley/DazPoseWizard`  
Baseline inspected: `main` at `bc596962aff6206fb54cc189de42defd270ddeda` (17 October 2026). **Re-read HEAD before doing anything.**  
Unity project: `validation/DazPoseUnityValidation` — Unity `6000.5.9f1`.  
Supporting audit (planning, not implementation authorization): repository-root `UNITY_PROJECT_CLEANUP_PLAN.md`.  
Authority: current `AGENTS.md`, complete D8 First Contact oracle, user-preserved artistic/release data, and `CONTRACT.md` here.  

## Mission

Make the existing Unity project a defensible production codebase: break historical dependencies, silence unwanted migration hooks, simplify editor commands, consolidate actual scene ownership, separate reusable content from fixtures, and retire demonstrably disposable material. Protect First Contact, the Void, Lara, the prepared male player, accepted wardrobe releases, pose/import tooling, and all provenance.

**Corrected card contract:** The runtime has **ONE GLOBAL POOL OF SELECTABLE CARDS**. Card tags plus runtime requirements determine eligibility. Scenes never declare or register card libraries. Newly added tagged cards can enter old scenes automatically. Scene-authored explicit beats (e.g. `play Intro`) remain explicit; nonselectable local cards do not join the pool. This is not negotiable and directly supersedes any previous roadmap suggestion of per-scene card library lists.

## Not in scope

- Designing/reskinning the scalable 50–100-item player interests/equipment UI (Task 4).
- Building a Scene Director Studio, source-first binding UI, staging/step rehearsal, or wardrobe UI redesign (Task 3).
- Writing the three new scenes or making new art/poses/audio (Task 2).
- Rewriting submission score, session state, toy errors, pacing, Director math, choreography, or consent/preferences.
- Launching game Play Mode, automated scene playback, a standalone player, or a build. `AGENTS.md` reserves Play and aesthetic acceptance for the user.
- Moving the project root itself, moving third-party package roots indiscriminately, replacing accepted art, or resetting any authored tuning.

## Immediate executor instructions

1. Read `AGENTS.md`, `UNITY_PROJECT_CLEANUP_PLAN.md`, `docs/story-runtime/D6_D8_HANDOFF.md`, and `docs/story-runtime/FIRST_CONTACT_D8_ORACLE.md` in the current checkout. Resolve any newer instructions before editing.
2. Run the preflight in `tickets/C00_BASELINE_AND_RECOVERY.md`. Refuse destructive operations without a recovery snapshot of both tracked and *needed ignored* inputs. Do **not** snapshot/delete all 77.8 GiB of `.dazposewizard` indiscriminately.
3. If `main` differs from the recorded SHA, inspect the intervening diff and update the baseline record; do not reset/revert new work. Work on the existing `main` checkout, as explicitly required by the user and current AGENTS.md. Do not create a branch or reset, stash, stage or commit pre-existing work as part of cleanup. Snapshot the actual working tree, including dirty and untracked inputs, and document ownership before edits.
4. Execute C00–C10 in order, satisfying each ticket's acceptance criteria before the next dependent stage. When implementation and commits are authorized, keep commits bounded by ticket; record hashes. Do not push: no remote publication is requested by this packet.
5. If a candidate scene/asset is in a verified dependency closure, **keep it**. Use `HOLD` for deliberately protected/out-of-scope data and `BLOCKED` for an unmet required migration; continue independent tickets. Never fudge reference checks just to reach a deletion count.
6. Finish with `VALIDATION_AND_HANDOFF.md`, a changed-file/move/deletion manifest, tests, preserved-reference evidence, and the user's short manual Unity checklist. Do not claim visual approval.

## File index

- `REVIEW_RESOLUTION.md` — source evidence and review decisions; not execution evidence.
- `CONTRACT.md` — invariants, boundaries and global-card ownership.
- `MIGRATION_MATRIX.md` — named scenes, assets, protected live dependencies, deletion gates.
- `tickets/C00_BASELINE_AND_RECOVERY.md` — starting and recovery evidence.
- `tickets/C01_HOOKS_AND_MENU_SAFETY.md` — automatic legacy migrations.
- `tickets/C02_ASSEMBLY_AND_HELPER_SEAMS.md` — remove production editor → validation/example dependencies.
- `tickets/C03_CLOTHING_SETUP_OWNERSHIP.md` — authoring workspace not a Day 2 validation scene.
- `tickets/C04_SUPPORTED_MENUS.md` — collapse overlapping authoring commands.
- `tickets/C05_GLOBAL_CARD_POOL.md` — global tagged-card auto-discovery, runtime linking, tests.
- `tickets/C06_SCENE_CATALOG_AND_SCENE_CLEANUP.md` — tidy game entry / surviving scenes.
- `tickets/C07_PRODUCTION_ASSET_MOVES.md` — GUID-preserving production ownership moves.
- `tickets/C08_FIXTURE_RETIREMENT.md` — individual verified deletions and archival.
- `tickets/C09_DOCUMENTATION.md` — current navigation and historical separation.
- `tickets/C10_FINAL_QA_AND_HANDOFF.md` — repeatable technical and manual gates.
- `VALIDATION_AND_HANDOFF.md` — exact checks and stop conditions.
- `templates/EXECUTION_REPORT.md` — required completed report structure.
- `EXECUTOR_PROMPT.md` — concise paste-ready instruction to a coding agent.

## Execution phases / commits

- **Gate A, C00:** baseline, full GUID inventory, live dependencies, recovery snapshot.
- **Gate B, C01–C04:** no surprising legacy behavior; clean editor dependencies and menus; Clothing Setup usable.
- **Gate C, C05:** add a new tagged card and an existing scene discovers it without a scene change; no scene-specific card library list.
- **Gate D, C06–C08:** small game scene list, supported scenes and content owned by correct layers, safe retirement manifest.
- **Gate E, C09–C10:** documentation, clean compiles/tests, manual Unity acceptance request, no unauthorized playback.

A ticket may be stopped as **BLOCKED** with evidence only when its acceptance is objectively impossible without protected data/approval. Continue unrelated safe work; finish with an explicit blocked-item list, not claims of success.

## Dependency clarification

C02 extracts the production authoring startup component needed to remove the validation assembly edge; C03 finishes its workspace and review-metadata integration. C06 changes navigation, retained scene structure and retirement *plans*, without deleting candidate scenes. C07 performs safe asset moves while those scenes remain available as dependency roots. C08 owns scene/fixture deletion after replacement coverage and dependency gates pass. Thus C06 does not wait for C08, and C07 does not require scenes to have been deleted.

No new product/story scenes are in scope. C06 may consolidate existing diagnostic scenes into one saved lab only when a concrete inspection need remains; that is the sole bounded scene-consolidation exception. It must use saved environment data and supported prefabs, with its required behavior saved in the scene, and is never a production scene prerequisite.
