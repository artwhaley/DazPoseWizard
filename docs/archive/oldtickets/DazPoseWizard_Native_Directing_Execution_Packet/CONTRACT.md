# Hard execution and preservation contract

## Scope and forbidden substitutes

**IN:** replace Studio's custom visual viewport with native Unity Scene view and Game view; correct cue-end-state staging; camera rig; pose/seating/anchor editing; safe visual state transaction; Studio UI/chrome; lifecycle/regressions; native validation and author guide.

**OUT:** new scenes, new card libraries or scene card registration, redefining Director selection policy, rewriting `.dazstory` vocabulary, changing the player-facing UI, general Unity project cleanup, importing new art, tuning existing material/light/wardrobe presets, hardware integrations.

Do not satisfy the request with SceneView camera synchronization alone. **The actual saved player's Camera must render the staged cue in Unity's Game view in Edit Mode.** Do not satisfy with a RenderTexture in a custom editor window, screenshots, or a rebuilt hierarchy. Do not require Play to inspect the endpoint of a cue or require a second pass to set staging.

## Accepted authoring semantics

- User opens a saved production scene; chooses the source/`StoryProgramSequence` and `StorySceneBindings`; Stage / Step / Back / Run-To works in Edit Mode.
- Studio carries **cumulative performance state** (position, posture, persistent pose, visible materials, scene camera position/aim) over sequential cues, including forced branches and global selected cards. After a cue, the visible Lara/camera reflect the **result** of that cue, not a disconnected preview of an asset.
- A *cue end* need not animate in real time; move/dissolve/camera-transition endpoints are required. No arbitrary invented camera endpoints. If a command is genuinely indefinite (tracking) compute current look direction from current performer state.
- Drag real authored standing marks, seat Approach/Seat/focus marks, camera anchors with Unity's normal Inspector and Scene gizmos. Game view and performer restage during drag, not after an explicit Commit button.
- Saved mark edits are normal Unity-authoring edits (Undo, dirty scene, Ctrl+S). Rehearsal-only motion, pose, visibility, material overrides, player rig and camera changes are temporary, not Undo author actions, not prefab overrides, and must be **absent from saved serialized scene data**.
- No output, app persistence, toy connects, real prompts or game session services in rehearsal. Unknown/unsupported cue semantics must be labelled with source and not silently treated as successfully staged.

## Absolute protected surfaces

- `Assets/Production/Scenes/FirstContact.unity` and `MainMenu.unity` (except separately approved intentional user edits); FirstContact `.dazstory`, its bindings, engineering cards and audio IDs. **A scene copy is not asset isolation:** importer reconciliation can write `ownerSourceGuid` to shared binding assets through `AssetDatabase.SaveAssetIfDirty`; scratch global-card reindexing can write the shared global index.
- Authored Void art-only template and original `FirstPerformanceVoid.unity`, volumes, particles, lighting, camera and material settings.
- Lara meshes, rig, pose wrappers/clips, shaders and visibility setup; prepared male body/contact profile; wardrobe releases, reviews and authored layer choices; current global card source/index.
- IDs/GUID/meta continuity; versioned story semantics, seed histories and profile scoring, existing manual test boundary.

## Execution methodology

- Snapshot/read-only hash protected surfaces before implementation; stop on pre-existing non-clean worktree and partition ownership, not blanket reset/stash.
- Work in tickets `N00`–`N11`; source-level and managed checks at every milestone. **Executor is categorically prohibited from launching/operating Unity** (GUI, batch mode, command line, automated EditMode tests, Play, builds, remote desktop/computer-use). Native evidence is supplied only by **Art**, using explicit opt-in diagnostic menu items/manual inspection. At each mandatory native gate, **pause execution** and ask Art to run it. An unrun or failed gate **blocks dependent tickets**; `PARTIAL` is a report outcome, not permission to bypass a gate.
- Make every native request short, deterministic and reversible: exact menu path, prerequisite isolated project/fixture, required clicks, report output, expected PASS/FAIL, safe cleanup, and copy/paste return instructions. No menu action runs on import or automatically enters Play. Never turn a diagnostic failure into scene edits.
- Tests against content or importer/global-card side effects run in a **separate isolated project copy** with copied content assets/index and a documented baseline; a disposable scene by itself does not protect shared assets. Never launch Unity to create this fixture: prepare files/tooling and ask Art to open it.
- Keep tests lean, targeting real regressions; temporary fixtures are deleted only after Art's evidence is captured and the correct source baseline is checked.
- User authorization to execute this packet permits **local commits grouped at ticket/milestone boundaries** after resolving pre-existing worktree conflicts. No push, merge, PR or remote mutation without additional explicit authorization. If newer commits exist, compare/reconcile instead of resetting.
- At N00 update the repository root `AGENTS.md` **top active execution reference** to this packet via `AGENTS_MD_HANDOFF.md`, preserving and strengthening the existing Unity prohibition. The packet itself does not authorize changing older historical sections or relaxing safety rules.
- Preserve all earlier working non-visual systems (global cards, SourceBindingReconciler, `StoryMachine`, `StoryRehearsalController` visit identity, app UI).

## Final evidence must include

Diff/inventory before/after, removed custom viewport code references, real Game view Edit Mode observations, native save/Undo/restoration captures, FirstContact cue tests (standing/sitting/camera), global card and branch regression, source/asset hashes, test commands/results, outstanding limitations and a **step-by-step new-user guide**. No long-term coexistence of two Studio preview engines.
