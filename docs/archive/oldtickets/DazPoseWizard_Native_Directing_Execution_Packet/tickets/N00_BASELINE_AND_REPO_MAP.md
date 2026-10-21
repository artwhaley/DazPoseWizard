# N00 — Baseline, recovery, and actual serialized hierarchy map

**Dependencies:** None. **Deliverable:** `docs/native-directing/N00_BASELINE.md`, immutable hashes and a **fully isolated Unity project copy** for Art's later manual tests; executor runs no Unity.

## Execute

1. Verify repository origin/current HEAD against pinned `4656eb56fe1cfa2201104e960b4d7e8dc41cb766`. Compare newer touched paths without force resetting or overwriting dirty/untracked work. Read root `AGENTS.md` and update **only its top active-packet reference** according to `AGENTS_MD_HANDOFF.md`; if it is conflicted/protected, ask Art before editing. **Retain absolute no-Unity prohibition**. Local commits allowed; no push.
2. Read `PRODUCT_CONTEXT.md`, `ARCHITECTURAL_DECISION.md`, `CUE_BEHAVIOR_MATRIX.md`, `CONTRACT.md`. Read actual saved FirstContact scene YAML, `StorySceneBindings`, `PerformanceSceneBindings`, `PlayerController`/`PlayerView`, `SuccubusPerformer`, `PerformerSeat`, `PerformerSeatingProfile` and the source/companion/global cards. Inspect their serialized component references, actual root and rig hierarchy, camera FOV/settings/enabled state and pose/seat clip ownership. Do not assume all scene objects are active in Edit Mode.
3. Record a machine-readable mapping of actual Lara root/Animator/renderers, body, player/viewRig/headPose/MainCamera, seat/focus/placement refs. Scene YAML establishes **saved wiring only**; real Game rendering is unproven until Art's G1. Camera child local offsets currently zero; don't assume different runtime instance behavior.
4. Audit **all Edit Mode writers** of saved objects, especially `[ExecuteAlways]` `LaraWardrobe`, `LaraFirstOutfit` and `LaraHeelReview` `LateUpdate → Apply()` toggling renderer enabled/blendshape/material values. Record their affected properties, callback order and competing owners. Record `PerformerMagicShaderPresentation` runtime material-reference substitution. Map `laraFocus` as a child of Lara head, not an independent world-space fixed anchor.
5. Hash protected scene/source/binding assets/global index, GUID/meta and original prefab overrides. Inspect `SourceBindingReconciler` and DazStory importer: missing `ownerSourceGuid` can trigger automatic `AssetDatabase.SaveAssetIfDirty` in **shared** `.bindings.asset`; scratch global-card test updates shared index. Prepare a **separate physical project copy** (no Asset symlinks) with its own `Assets`/`ProjectSettings`/index/linked assets and disposable saved scene; **never run Unity to prepare it**. Define a **code-only source + `.meta` synchronization** step for each N01/G1, N04/G2, N05/G3 and N10/G4 gate, so Art tests current implementation while fixture protected data stays isolated. If safe isolation is impossible, stop/ask Art to approve a metadata migration before rebasing. No project cleanup.
6. Capture Story/Player/Rehearsal/Application .NET and static source checks; 196 tests/7 assemblies are historical claims, not new native evidence. Record installed Unity metadata from files only. Map the exact G1 report/menu implementation path; **do not launch Unity**.

## Gate

- Baseline includes serialized object paths. Native Inspector confirmation is **deferred to Art at G1**, not performed by executor.
- Separate test **project** and disposable scene paths documented; protected originals and indexed/binding assets hash unchanged. If not isolated, N00 is BLOCKED; ask Art.
- `[ExecuteAlways]` property owners and MagicShader material clone behavior enumerated; laraFocus head hierarchy recorded.
- `PreviewRenderUtility` use sites and exact caller list are enumerated for removal in N09.
- No new staging architecture installed, no production asset edits, **no Unity process launched**.

## Failure handling

If real scene components are miswired, record the exact missing object/field and stop. Do not fabricate a new player camera or copy the environment. Repair only genuine invalid reference through a separate reviewed fixture/prod binding update, not silent code fallbacks.
