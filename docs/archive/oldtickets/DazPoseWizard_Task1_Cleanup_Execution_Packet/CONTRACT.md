# Non-negotiable engineering contract

## 1. Original content and saved composition

- `Assets/Scenes/FirstPerformanceVoid.unity` is an **untouched artistic reference**. The production Void is a preserved copy, not permission to retune lighting/fog/floor/materials/camera.
- `Assets/Production/Environments/VoidEnvironment.unity` must remain visually equivalent and compositionally intact; no generic replacement stage.
- First Contact's **full** `.dazstory` oracle remains in order: 19 fixed dialogue beats, all branching, Warmup/Main/Climax/Resolution, card calls, source locations and player's prepared body. Do not replace any step with a representative beat.
- Preserve Lara's imported rig, binding IDs, animator defaults, performer calibration, original source/`.meta` GUIDs, accepted user pose selections, facial/morph data, SALSA, shaders/material references, and visible authored settings.
- Preserve prepared **male** `PlayerBody` prefab, measured penis/contact target and rig/surface setup. Do not substitute a synthetic rod or a Lara visual copy.
- Preserve draft vs released wardrobe configurations separately; the first-outfit layer choices differ. Keep immutable releases permanently; user acceptance of the pending second-outfit toe review does not authorize changing or deleting released data. Keep its exact fixture until separately eligible for retirement.
- Preserve references to `TestCharacter/lara.fbx`, `TestCharacter/playererect.fbx`, live `TestData/...`, `PartnerProof/...`, and `Production/Wardrobe/Remediation/R03/second-outfit/` until consumers are migrated and verified.
- Preserve C# `PerformanceSequence`, `PerformanceHost`, `PerformanceRun`, the `.dazstory` compiler/flat interpreter, scene/runtime player host, and supported C# extension APIs; don't remove runtime compatibility simply because test scenes are retired.

## 2. Global card pool (USER CORRECTION)

- **There is one global selectable-card pool.** All authored cards carrying selection tags and published for normal content discovery participate in that pool, regardless of the scene in which they were first developed.
- Scene scripts request card predicates such as `[Warmup]`, `[Main, Sitting]`, or active/prepared equipment tags. The Director evaluates tags, card requirements, session/player/world state, recency, use limits and seeded weights **at runtime**.
- A new tagged card must be discoverable by pre-existing scenes **with no scene source, scene asset, scene catalog or authored library list change**. Updating a generated global index is allowed if it is automatic and derives from authored content; it is not a manual scene registration step.
- An explicitly addressed narrative `Intro` or `Director.Execute(CardName)` can remain part of an authored program. A local untagged card does not automatically enter random selection.
- Don't introduce per-scene card library declarations, opt-in card IDs, card sets or dependency lists, and don't hide a per-scene card list in a `SceneDefinition`/profile/asset.
- Equipment definitions expose open identity/theme tags, including future tags unknown to old scenes. Universal physical state is a separate closed vocabulary. Do not add one boolean per equipment type.
- Preserve submission-score formula, existing session random streams and predictable seeded selection, progress/pacing and intensity behavior.
- If a global card requires a scene resource that is unavailable, preflight/eligibility must report it clearly; it must not explode mid-card. Do not silently restrict the global catalog just because a scene is old.

## 3. Separation of ownership

- Game scenes: saved gameplay compositions (`MainMenu`, `FirstContact` and future scenes).
- Authoring workspaces: e.g. Clothing Setup and later Scene Studio, not listed as public game scenes.
- Reusable content: poses, actions, gestures, shaders, motion, audio, tag-bearing global cards. Not buried in one particular scene's private configuration.
- Example(s): maintained educational/diagnostic showcases, not a player's game menu.
- Protected references: historical artistic scenes, pending visual-acceptance fixtures, licensed sources and immutable release data.
- Disposable fixtures: removable only with direct and indirect dependency evidence + recovery.

## 4. Editor safety

- Edit Mode only for cleanup operations. No gameplay Play, scene playback, build or player launch. Unity batch imports/compilation and EditMode checks are permissible when they don't invoke gameplay; use documented command line.
- Historical `[InitializeOnLoad]`, asset postprocessor, reload, scene opening and `.once` hooks must not mutate current artwork, wardrobe tuning, saved scenes or player profiles unexpectedly.
- No silent prefab recreation or rebake. No work that assumes imported `.fbx`/ignored artifacts can be recovered from Git alone.
- For asset moves prefer `AssetDatabase.MoveAsset`, preserving `.meta` GUID and Unity remapping. Fix path-string consumers atomically. Don't hand-edit GUIDs to produce the desired result.
- Run after each risky move: source/import path scan, Unity serialization dependency check, compile, baseline regression tests; rollback that individual move if contract fails.

## 5. Publication and verification

- Protect pre-existing dirty work. Maintain a recovered snapshot and an exact move/delete manifest.
- Work on existing `main`; no new branch, push or history rewrite. On a later authorized implementation run, commit only bounded verified cleanup changes; never sweep in pre-existing work.
- A technical acceptance record is not the user's aesthetic approval. Provide the exact saved-scene paths for the user's subsequent manual Play.
- `AGENTS.md` and newer direct user instructions override any less recent ticket directions where conflict exists.

## 6. Reviewed preservation rules

- A namespace, folder name, absent menu, disabled catalog entry or zero raw GUID matches is not proof of dead code/data. Classify by assembly, serialized references, import/preparation consumers and retained reference-scene closure. `DazPose.UnityValidation` contains live production types.
- Preserve the legacy scripts/asmdefs needed to load frozen artistic and published review scenes without missing scripts. Removing production dependency edges does not mean deleting those assemblies. Do not replace retained components with stubs or strip them from frozen scenes.
- Immutable release locations and the pending toe fixture's dependency closure remain pinned in Task 1. Document ownership at their existing paths; do not move/rewrite them just to satisfy the proposed folder layout.
- Preserve the completed failed-expression cleanup: automatic reconciliation skips unchanged failures; the explicit retry command remains explicit. Keep the supported archive command, failed-input recovery manifests and archive contents outside watched Assets. Never restore archived inputs to the queue as a cleanup/import check.
- Protect actual unsaved editor work as well as disk files. Never auto-save, discard or replace the user's open dirty scenes. Use temporary fixtures or an isolated source copy for checks that mutate; do not start another Unity instance against a project already open.
- Protected frozen scene and immutable release bytes must match their baseline exactly. For authorized editable assets, compare GUID **and fileID**, prefab overrides, importer settings and serialized values; document an explicit field/path diff rather than excusing arbitrary changes as serialization noise.
- Do not turn C05 card resource eligibility into a generalized performer/toy preflight gate. Toy failures remain silent successful no-ops; application-owned connection settings, Stop cancellation and persistent tuning remain unchanged.
