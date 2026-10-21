# T01 — Workspace reset

**Repository:** `artwhaley/DazPoseWizard`, local working copy on Art's machine, branch `main`.
**Plan reference:** `ARCHITECTURE_SPRINT_PLAN.md` (delivered with this ticket; you will commit it in step B1).
**Owner of decisions:** Art. You propose; Art approves.

---

## Goal

Get the workspace back to a state where a fresh agent can work from current, correct instructions, and where nothing stale in the repo or the Unity project is actively misleading anyone.

Concretely:

1. Find and remove retired material: cloned Unity projects, retired test scenes, disposable fixtures, dead tool droppings.
2. Give the Unity project one clear folder rule (code, content, scenes) and apply it where it is cheap and safe.
3. Archive the stacked historical docs and ticket packets out of the way.
4. Rewrite `AGENTS.md` as one short, current document and fix `README.md`.
5. Commit the sprint plan into the repo.

## Observable when done

- **Art:** open Unity, open `Assets/Scenes/FirstContact.unity`. The Console shows no missing-script or missing-reference errors that were not there before. Press Play: the intro plays exactly as it did before this ticket. Also open `ClothingSetup.unity` and `Assets/Production/Scenes/MainMenu.unity` (or wherever it ends up) and confirm the same.
- **Agent check:** a fresh agent session that reads only `AGENTS.md` can correctly state the First Contact scene path, the folder rule, the boundaries, where the plan lives, and what the current ticket is.

---

## Boundaries

These are absolute for this ticket.

- **Do not operate Unity.** No Editor, Hub, batch mode, `-executeMethod`, tests in Unity, Play, builds or players. Art does every Unity check. (Spike S01 will revisit this rule; it has not happened yet.)
- **Do not delete or move anything before Art approves the manifest** (Phase A ends with a hard stop).
- **Preserve GUIDs.** Every asset move carries its `.meta` file. Folder moves carry the folder's `.meta`. Art must have Unity **closed** while you move files under `Assets/`.
- **Do not move these (protected):**
  - Lara's imported/source materials, textures and manifests. Their `AssetDatabase` paths feed `WardrobeCatalogBuilder.CharacterSignature`; moving them breaks released wardrobe generations. See `docs/cleanup-task1/path-consumers.md`.
  - Immutable wardrobe releases under `Assets/Production/Wardrobe/` and its rig folders.
  - Second-outfit toe fixtures and anything listed in `docs/cleanup-task1/protected-manifest.json`.
  - The contents and GUID of `Assets/Scenes/FirstContact.unity`. You may not edit the scene.
  - Production art, bound story source (`*.dazstory`) and their `*.bindings.asset` companions, and the story global index.
- **Do not regenerate, reimport or rebake** any asset.
- **Local commits only.** No push, PR or merge without Art's explicit say-so.
- **No subagents** unless Art authorizes them.
- Paths below starting with `UP/` mean `validation/DazPoseUnityValidation/`.

---

## Known starting facts

Verified from source at commit `2dee09b`. Re-verify against the working copy; it may have moved on.

- `README.md` says First Contact is `Assets/Production/Scenes/FirstContact.unity`. It is actually `UP/Assets/Scenes/FirstContact.unity`.
- `AGENTS.md` is a stack of dated "latest direction" / "active execution" blocks from October 7–20, several superseded and several naming packets that are complete.
- Scenes are split: `UP/Assets/Scenes/` holds `FirstContact`, `ClothingSetup`, `FirstPerformanceVoid`; `UP/Assets/Production/Scenes/` holds `MainMenu` and `RemediationToeValidation` (a remediation test scene). `UP/Assets/Production/Environments/VoidEnvironment.unity` is the art-only template used by **New Performance Scene**; it is not a stray.
- `.dazposewizard/` is gitignored and contains at least one full cloned Unity project at `.dazposewizard/native-directing/G1Project/` (a native-directing gate fixture). There may be more.
- The docs mention an "independent fixture" project that code was synchronized into ("Synchronized 1132 code/meta files to the existing independent fixture"). These clones were created and synced by `scripts/native-directing-fixture.py` (`prepare`, `sync-code`). Find every project it created, and include the script itself and any similar fixture scripts under `scripts/` in the manifest.
- Build Settings contain exactly two scenes: `Assets/Production/Scenes/MainMenu.unity` and `Assets/Scenes/FirstContact.unity`. If a scene moves, Build Settings must follow.
- The seating baker lives under `UP/Assets/DazPose/Validation/Editor/Legacy/AnimationAudit/PerformerSeatingBaker.cs`. "Legacy" code that still produces production bakes is not dead code.
- Root-level clutter: `AUTHORING_READINESS_BUILD_PLAN.md`, `CONTENT_AUTHORING_API.md`, `CONTENT_AUTHORING_WALKTHROUGH.md`, `DazPoseWizard_Deferred_Work.md`, `UNITY_PROJECT_CLEANUP_PLAN.md`, `oldtickets/`, `newtickets/`, `TestOutput/` (~16 MB, tracked), `output/`, `.codex-remote-attachments/`, `.freebuff/`.
- `docs/` holds about twelve campaign folders (`cleanup-task1`, `native-directing`, `p2-p3`, `production-consolidation`, `sprint1 wrapup reviews`, `story-runtime`, `studio-corrective`, `visual-authoring`, `wardrobe-directing`, `preauthoring patch review`, and dated single files).
- `UP/Assets/` root contains, besides `DazPose/` (code) and `Production/` (content): `Content/` (~24 KB), `Player/` (player runtime code and assets), `Wardrobe/`, `TestCharacter/`, `PartnerProof/`, `FirstPerformanceVoid/`, `Examples/`, `Daz3D/` (~51 MB), `Gizmos/`, `Resources/`, `Settings/`, `TextMesh Pro/`, and third-party packs.
- Third-party packs and what is known about them:

  | Folder | Size | Known use |
  |---|---|---|
  | `KAWAII_ANIMATIOMS_100` | ~381 MB | Source for the baked seating and locomotion motions in `Production/Content/Motions/` |
  | `INab Studio` | ~335 MB | Dissolve effect, in use |
  | `FemaleMovementAnimsetPro` | ~84 MB | Unknown — establish |
  | `Crazy Minnow Studio` | <1 MB | SALSA lip sync, in use |
  | `Daz3D` | ~51 MB | DAZ bridge scripts/assets — establish |

- Tracked `.meta` files at `UP/Assets/` root whose folders are not in git: `DazExpressionImports.meta`, `Generated.meta`, `TestData.meta`. `Generated/` and `TestData/` are gitignored; `DazExpressionImports/` is not. Establish whether the folders exist locally and what they hold.
- `src/` (DazPose.App, the Avalonia converter, and DazPose.Core) and `tests/` are live. Keep them.
- Existing evidence you can reuse instead of redoing: `docs/cleanup-task1/` (`path-consumers.md`, `asset-dispositions.json`, `protected-manifest.json`, `completed-moves.json`, `retirement-manifest.csv`, `scene-manifest.md`).

---

## Phase A — Inventory (read-only)

Do not change anything in this phase.

**A1. Whole-workspace survey.**
- `git status --porcelain --ignored` from the repo root. List every untracked and ignored top-level path with its size.
- Under `.dazposewizard/` and anywhere else in the repo folder, find every directory containing `ProjectSettings/ProjectVersion.txt` (a Unity project). For each: path, size, what created it (from docs mentions), whether anything still references it.
- Find the "independent fixture" project location from the docs.
- Note any other Unity projects or large folders inside the repository directory that are not `UP/`.

**A2. Unity project survey.**
- For each top-level folder under `UP/Assets/`: size, a one-line description of what it is, and whether anything in `Production/`, `Scenes/`, `DazPose/` or `Player/` references its assets. Reference by GUID: collect the GUIDs from the folder's `.meta` files and search scene, prefab, asset and controller files for them. Search **all** GUIDs, not a sample.
- For each third-party pack, additionally: which editor bakers or importers reference it by path or GUID (for example `PerformerSeatingBaker`, the locomotion baker), and therefore whether deleting it means "never rebake".
- Every scene file in the project: path, purpose, whether it is in Build Settings (`UP/ProjectSettings/EditorBuildSettings.asset`), whether code opens it by path.
- The three orphan `.meta` files: what they are and whether their folders exist locally.

**A3. Path consumers.** Search `.cs`, `.json`, `.ps1`, `.asmdef`, `.asset` and `.md` files for string literals containing `Assets/`. For every folder you might propose moving, list the code or data that refers to it by path. Start from `docs/cleanup-task1/path-consumers.md`; confirm it is still accurate.

**A4. Docs and tickets survey.** For each root-level doc, each folder in `docs/`, `oldtickets/` and `newtickets/`: one line on what it is and whether it is current, reference, or historical. "Current" means it describes how things work today and an agent should read it. Expect very little to be current. Candidate current docs: `docs/story-runtime/DAZSTORY_AUTHORING.md`, `docs/story-runtime/SCENE_DIRECTOR_STUDIO_GUIDE.md`, `UP/README.md`, `UP/docs/LaraWardrobeImport*.md`, `UP/docs/PlayerView.md`, `UP/docs/HandGripImplementationNotes.md`. Check each against source before calling it current.

**A5. Write the manifest.** Create `docs/workspace/CLEANUP_MANIFEST.md` with one row per item:

| Item | Path | Size | Evidence | Proposed action | Risk |
|---|---|---|---|---|---|

Proposed action is one of: **keep**, **move to `<path>`**, **archive to `<path>`**, **delete**, or **ask Art**. Evidence must be concrete (GUID reference count, code path literal, doc statement). Anything you are unsure about is **ask Art**, never delete.

Include a short proposed **folder rule** for `UP/Assets/` at the top of the manifest. Starting suggestion, to be confirmed against what you find:

- `Assets/DazPose/` — all project code (runtime, editor, validation).
- `Assets/Production/` — all production content (performers, player, wardrobe, story, environments, motions).
- `Assets/Scenes/` — all user-facing scenes.
- Third-party packs stay at their vendor paths.
- Anything else is either moved into one of these, archived, or deleted.

Include a **disk total**: how much the proposed deletions free.

**A6. STOP.** Send Art the manifest. Do not proceed until Art marks each row approved, changed or rejected.

---

## Phase B — Execute approved actions

Ask Art to close Unity before starting B2.

**B1. Commit the plan.** Add `ARCHITECTURE_SPRINT_PLAN.md` at `docs/plan/ARCHITECTURE_SPRINT_PLAN.md`, unchanged. Commit.

**B2. Deletions and moves.** Apply approved rows only.
- Untracked or ignored items (cloned projects, tool droppings): delete as approved. These are not in git history; double-check each path before deleting.
- Tracked items: use `git mv` / `git rm`. Move each asset with its `.meta`; move folders with the folder `.meta`.
- After each group of moves, update every path literal found in A3 that pointed at the moved location.
- One commit per coherent group (for example "Archive historical ticket packets", "Move player content under Production"). Commit messages say what moved and why.

**B3. Archive docs.** Move historical docs, both ticket packet folders and superseded root plans into `docs/archive/` with their existing structure. Add `docs/archive/README.md`: one line per archived item saying what it was and when it was superseded. Nothing is deleted from history; the point is that agents stop reading it.

**B4. Rewrite `AGENTS.md`.** Move the current `AGENTS.md` verbatim to `docs/archive/AGENTS_2026-10-20.md`. Write a new `AGENTS.md`, under about 150 lines, containing only current truth:

1. What the project is, in two or three sentences.
2. Where the plan lives (`docs/plan/ARCHITECTURE_SPRINT_PLAN.md`) and the current ticket.
3. How we work: interactive, Art tests, each session ends with an observable, one ticket written ahead.
4. Repo map: `src/`, `tests/`, `UP/`, `docs/`, `scripts/`, with one line each.
5. Unity project folder rule and the key scenes with exact paths.
6. Boundaries: the Unity-operation rule (marked "pending spike S01"), commit/push rule, GUID rule, the protected list, no regeneration without asking.
7. How to build and run managed tests (`dotnet build`, `dotnet test` over `tests/`).
8. Where status is recorded: **one** file, `docs/plan/STATUS.md`, which you create with T01's outcome. Technical results and Art's visual acceptance are recorded separately; never claim a visual pass from source.
9. A pointer to the VR-forward rules in the plan (section 5.8): every change must respect them.
10. The short list of current reference docs confirmed in A4.

Do not carry over historical narrative, dated "latest direction" blocks, Codex sandbox launch recipes, or instructions for packets that are complete.

**B5. Fix `README.md`.** Correct the First Contact path. Point to `AGENTS.md` and the plan. Remove or correct any other statement A4 found to be stale. Keep the DazPose converter build/run instructions if still accurate.

---

## Phase C — Verify

**C1. Your checks.**
- `dotnet build` and `dotnet test` over `tests/` pass as before (record counts in `STATUS.md`).
- Every `.meta` under `UP/Assets/` has its asset and vice versa (except the protected or explicitly approved cases). No duplicate GUIDs.
- No remaining path literal points at a location that no longer exists.
- `git status` is clean apart from known untracked local inputs.
- Re-read the new `AGENTS.md` cold and confirm it answers: scene path, folder rule, boundaries, plan location, current ticket.

**C2. Art's checks.** Ask Art to:
1. Open Unity and let it import.
2. Open `Assets/Scenes/FirstContact.unity`. Confirm no new missing-script or missing-reference errors.
3. Press Play. Confirm the intro plays as before.
4. Open `ClothingSetup.unity` and `MainMenu.unity`. Confirm no new errors.
5. Open **DazPose > Story > Scene Director Studio** on First Contact and confirm cues still project.

Record Art's results in `STATUS.md` as Art reported them.

---

## Deliverables

- `docs/workspace/CLEANUP_MANIFEST.md` (approved version)
- `docs/plan/ARCHITECTURE_SPRINT_PLAN.md`
- `docs/plan/STATUS.md`
- `docs/archive/` with `README.md`
- New `AGENTS.md`, archived old `AGENTS.md`
- Corrected `README.md`
- Local commits, unpushed

## Out of scope

- Any code refactor, including `SuccubusPerformer.cs`, the legacy `StoryRunner`, or the binding components. Note candidates in `STATUS.md`; do not act.
- Changing scene contents, marks, bindings or story source.
- Anything from T02 onward.
