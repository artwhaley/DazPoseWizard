# Architectural decision — Native Edit Mode performance staging

**Status: DECIDED.** This is the design for implementation and external review. Challenges should identify a specific correctness or safety failure and propose a *native-scene* remedy, not a return to a bespoke renderer.

## 1. The model in one sentence

The existing **canonical virtual rehearsal** evaluates cue order/logic; a new **NativeSceneStage** projects its accumulated visual end-state onto the actual open Unity scene's saved Lara, male body, real player-view rig and camera; Unity's existing Scene/Game views display it; the real scene's marks remain normal authored, persistent objects.

```
.dazstory + global tagged cards + typed content bindings
            ↓
canonical StoryMachine / StoryDirector
     deterministic virtual RehearsalSession
     events / visit IDs / branch and card decisions
            ↓
    RehearsalVisualState (pure accumulated cue presentation; no new program interpreter)
       ├─ resolved Lara root, pose, posture, visibility, material look
       ├─ resolved male-body root/visibility
       ├─ resolved camera root, view-rig orientation, track target
       └─ unresolved/approximation diagnostics
            ↓
NativeSceneStage (EDITOR ONLY; preview transaction + resampling)
       ├─ actual saved-scene Lara + male body
       ├─ actual scene-bound PlayerView rig + MainCamera
       ├─ scene's own HDRP environment, lights, volumes and particles (never copied)
       └─ source-linked authored marks (never preview-modified)
            ↓
     Unity Scene view  <-->  Unity Game view
        Unity native Selection / Inspector / Handles / Undo
```

**Important distinction:** The `RehearsalVisualState` is not a duplicate story machine. It records *effects that the real interpreter already produced* as declarative presentation instructions, so the latest selected cue can be **re-applied** when an anchor moves without re-running random selection or mutating session state. Logical decisions never live in this state type.

## 2. Ownership

| Item | Durable owner | Stage actions | Save policy |
|---|---|---|---|
| `.dazstory`, global cards, content refs | Source/typed binding assets | Read or explicitly edited through binding UI | Normal asset authoring |
| Scene `StoryLocationBinding`, marks, `PerformerSeat` anchors/focus | Saved Unity scene | Resolve and **observe**; user moves with native gizmos/Inspector | Undo + save |
| Lara saved performer, skeletal pose, material/visibility preview | Saved scene object + temporary preview transaction | Reversible staging on **existing saved Lara** | Never persist rehearsal state |
| Player male-body saved object | Saved scene + preview transaction | Reversible staging | Never persist rehearsal state |
| `PlayerView` root, separate `viewRig`, `headPose`, actual `MainCamera` | Saved scene + preview transaction | Reversible stage from cue-end camera rule | Never persist preview state |
| Editor selection, scene navigation and overlays | Native Unity UI + Studio chrome | Selection and read-only cue highlights | Normal editor; no synthetic scene |
| Random, card history, world/session state | Canonical RehearsalSession | Advance/run/back/reset | In-memory or explicit test-only scenario file |

### No parallel scene copy

Delete the dependency from Studio to `ScenePreviewSession`/`PreviewRenderUtility`. The scene's environment is not cloned. The camera in the Game view is **the real scene camera**, not a `PreviewRenderUtility` camera, and **not** the Scene view navigation camera. A temporary visual-only actor proxy is *not* the default architecture; the same saved Lara and PlayerBody are staged under a reversible editor-preview lease. If temporary actors are later introduced for a specific Unity safety limitation, that is a separately justified, narrowly scoped change—not a new stage or renderer.

## 3. Editor preview state is *not* an authored edit

Implement a single `NativePreviewLease`/`NativeSceneStage` (names illustrative) with explicit acquire/apply/suspend/restore/release:

1. **Acquire:** Require Edit Mode, one saved scene host and resolved saved objects. Take the **complete preview-property ledger before the first pose sample** (root/bones, renderer enabled/`forceRenderingOff`/materials/property blocks, skinned blendshape weights, active/enabled state if touched, rig/camera, other writer-owned values). Record dirty and prefab overrides for **comparison only**, not whole-state rollback. Do not start `GameApplication`. Audit `[ExecuteAlways]` `LaraWardrobe`, `LaraFirstOutfit` and `LaraHeelReview`: their `LateUpdate` can write visibility, morphs and materials while preview is active. Define precedence/coexistence; don't casually switch Lara root `SetActive(false)` because that changes lifecycle.
2. **Animation:** Use Unity's `AnimationMode` **with a dedicated `AnimationModeDriver`** (Unity 6000.5) for generated pose sampling, with conflict detection for Animation Window/Timeline. Guard root curves. **On suspend/teardown, stop the Studio-owned AnimationMode first, then restore the explicit ledger** and confirm no AnimationMode-owned value reappears. For interim pose changes, reestablish a known baseline before resampling. The exact tested sequence is authoritative; a driver cannot be assumed to restore unrelated material/root properties.
3. **Non-animation effects:** Reversibly stage saved actor/camera transforms and renderer/material states in a **per-property ledger**; don't put stage changes into Undo or Prefab modifications. Never write authored mark transforms as stage outputs. When Art edits a shared object/property, track conflicts by ownership, **preserve authored values and newly created prefab overrides**, not blindly restore an earlier entire override list. Reapply from baseline plus projection and detect `[ExecuteAlways]` writer interference.
4. **Save safety:** Normal Ctrl+S must suspend temporary state **before** serialized bytes are captured, preserve authored edits, let Editor save, then restage; handle save failure/cancel/Save As/close and reentrancy. `sceneSaving`/`sceneSaved` and `OnWillSaveAssets` are possible signals, **not proof** of ordering or coverage (notably `SaveAssetIfDirty`). A native Art-run check on an isolated disposable scene must prove semantics, including **forced SaveScene when a clean Ctrl+S otherwise skips serialization**. Fail closed if unsafe; normal Ctrl+S remains a mandatory final feature. **Preview writes introduce no dirty flags; stopping preview must preserve outstanding authored edits and their dirty state**; never restore an old scene-dirty baseline over user edits.
5. **Undo:** Native Undo for authored marks/bindings. Preview writes add none. Observe targeted transform/Undo changes and restage without rerunning story logic. Never clear user edits or overwrite legitimate chair prefab overrides.
6. **Lifecycle:** Release/suspend on Studio Stop/close, source or host swap, scene close/open/unload, Unity Play transition, assembly reload, compile/import invalidation and exceptions. Detect recovered stale leases at domain reload: the exact scene/actor/camera must remain uncorrupted. Scene selection and focused editor windows should remain usable after Stop.
7. **Safety proof:** Hash the **separate isolated project/scene** before preview. After preview stage/scrub/save/reload with no authored change, require byte-identical or semantically identical saved scene. After a mark edit, whitelist expected new instance overrides and authored dirty state, while asserting no pose/visibility/camera material leakage. Rerun Art-operated tests **for each new staged property family** rather than assuming N02 covered future pose/magic/seat writers.

Do **not** assert that `AnimationMode` automatically handles scene camera/root/material/visibility changes; it samples animated properties. Those other properties need explicit reversible ownership. Do **not** depend on the renderer preview's clone-specific sampling technique to prove safety on real saved actors.

## 4. Real Game view in Edit Mode — camera wiring

Unity's Game view can render enabled cameras in the open scene in Edit Mode, but whether **this project's HDRP Game view** does so is an **unproven G1 gate for Art**. The production shot is wired through `PerformanceSceneBindings.Player` -> `PlayerController.View` (`PlayerView`) -> serialized **`viewRig`, `headPose`, `mainCamera`**. In the current runtime:
- `Camera.StartAt` resolves a `StoryLocationBinding.placement` and calls `PlayerView.ResetPerformanceView` (player root position to anchor position, viewRig rotation to anchor rotation).
- `Camera.Track(Lara)` maintains a live look direction to `StorySceneBindings.laraFocus`, using the camera/head view origin. In Edit Mode choose the **settled aim at the current cue** (no need to replay acquisition easing), but preserve tracking across subsequent Lara/mark movement.

**Implementation:** Prefer existing `PlayerView.ResetPerformanceView`'s straightforward transform endpoint if confirmed Edit Mode-safe in source; add only minimal edit-safe serialized rig access and expose the pure `DesiredRotation` tracking computation as required. Do not refactor camera mathematics broadly if parity already exists. Stage `Camera.StartAt` with **full placement rotation** and player root position; track from actual camera/head origin with current hierarchy offsets. Do **not** call async `PlayerController.MoveToAsync/Track` or gameplay session methods in Edit Mode.

Maintain the actual camera's projection, FOV, culling mask, post processing and resolution settings. Ensure one intended camera feeds Game view, and do not introduce a second enabled camera that changes rendering priority. Request refresh with `EditorApplication.QueuePlayerLoopUpdate` and native window `Repaint`; never fake a render by importing HDRP into a custom preview utility. If camera rendering in Edit Mode is suppressed due the saved actor hierarchy being inactive, stage the required enable state under the preview lease and restore it.

**Live author edits:** When the selected camera mark is moved or rotated via native handles, recompute stage from that mark so Game view updates during the drag. Tracking follows Lara/focus when she moves while camera root retains the most recent established anchor. Undo/Redo immediately reprojects. The Stage should expose a clear 'current camera anchor/track' indicator in the Studio chrome and select/frustum-highlight its saved transform in Scene view.

## 5. Lara and seating — actual geometry

Position Lara's *real scene-bound performer* at selected location end state and sample actual generated `PerformerPose` using owned AnimationMode. Preserve state where runtime does: `PersistentPose` survives dialogue, but **standing `DissolveTo(location)` resets to runtime `initialPose` and uses yaw-only facing**, while `StartHiddenAt`/`DissolveInAt` use full placement rotation. `StartIdle` requires the actual configured `initialPose`, never an invented neutral fallback. Awaited one-shot gesture's **after state** returns to underlying persistent pose after additive masked overlay fades; do not sample its terminal clip as a permanent pose. Non-awaited gesture has an explicit observation rule/diagnostic, optionally separate audition with correct mask/additive reference.

**Seated cues are not one generic teleport.** `StorySceneBindings.RequireLocation(id)` returns `PerformerSeat` (ApproachAnchor, SeatAnchor, baked profile). Implement **two runtime-equivalent stable endpoints**: (a) `Lara.SitAt` defaults to seat's style/CrossLegs stable sit, with appropriate baked sitting solve (`PelvisOffsetAt(1)` used for initial/transition or default path as actually verified); (b) `Lara.PersistentSitPose(pose)` and `Lara.DissolveTo(location, seated: pose)` sample the **specific pose first**, orient Lara root to **seat yaw**, then translate the root by `SeatAnchor.position - sampledHip.position` using the **live pose-sampled hip world transform**, matching `PerformerSeating.AnchorPersistentPose()`. Do not equate baked `PelvisOffsetAt(1)` with final persistent Lounging alignment. Perform same stable-root math on re-stage, no drift, including scaled actor. `TryCaptureFrames` is internal but existing `InternalsVisibleTo("DazPose.Editor")` permits Edit assembly access; do not refactor visibility without need.

In FirstContact **Intro**, `DissolveTo(Lounger)` is **standing**. First seating is **Warmup** `SitAt(Lounger)` → `PersistentSitPose(Lounging)`. Later **Main** has `DissolveTo(Lounger, seated: Lounging)` after Director. `SeatAnchor` lives on a Validated Lounge Chair prefab instance, so deliberate editor moves create **legitimate new prefab overrides** (allowed; stage-induced overrides forbidden). `laraFocus` is a **descendant of Lara's head bone**: its world position moves with pose sampling; preserve its authored *local* offset, recompute actual world aim **after** pose, and do not let this change trigger anchor-change feedback loops.

Do not make the Studio's generic pose sampling or camera update drive the runtime state machine itself in Edit Mode. Evaluate and stage a **known post-command resting frame**. If a command's physical end frame genuinely needs a runtime-only solver, provide the nearest verified baked endpoint and a non-silent diagnostic; do not pretend it is exact. First Contact seated arrival's pelvis alignment must pass native visual inspection, not just a managed math test.

## 6. Cue history, branches, and live re-anchoring

Continue to use `RehearsalSession` and canonical `StoryMachine`/`StoryDirector`; retain current visit-instance identity, one-time forced choices, global-card linking and deterministic scenarios. Convert host-emitted visual effects to a compact **typed ordered cue-state projection** representing current end state; no source text control-flow logic in the adapter.

- **Selecting cue**: choose exact owner/source/declaration/PC/visit, run canonical rehearsal to that cue and show **after-state**. Offer Before for comparison.
- **Stepping**: logically execute each selected instruction and update stage from the newest projection. A `Lara.Say` cue leaves prior pose, location and camera intact (no reset to defaults).
- **Branching**: pause/choose valid conditional state and continue; invalid/unreachable cues should be labelled instead of presenting stale visuals.
- **Global cards**: selecting a Director card navigates into its owner source with the same staged scene and host. No per-scene library membership. Non-selected ineligible cards must remain out of selectable candidates.
- **Anchor edits**: do not rerun story on mouse movement. Split **logical content revision** (source/program/bindings/index/scene host) from **spatial invalidation** (referenced anchor world matrix and parent transforms). Replace old `CaptureStamp()` scene-wide Transform/dirty-count walk; target watched anchors and bounded update/poll, ignore preview-owned Lara bones/rig and head-dependent focus world movement. Adapt old `StudioCorrectiveChecks` expectation that mark changes alter full stamp. Reproject without consuming RNG/Director visits.
- **Invalidate**: source/binding/global-index/host semantic change may require logical replay; simple anchor transform edit does not. **Fail-fast visual bridge**: current `DrainRehearsal` catches effects errors then proceeds; new bridge must validate ordered visual effects and propagate first failure to transport, prevent later queued effects overwriting the failed cue, and show source owner/line/diagnostic. Regression: bad seat/camera followed by valid motion/Director must leave transport halted **at** bad cue.

## 7. UI and interaction

Studio becomes a **docked companion window**, optionally Scene view overlay for cue title/Next/Back/Stage current mark. Keep its source list, typed slots, Director candidate inspector, scenario controls, state and diagnostics. **Remove the private 3D viewport and its camera/drag inputs**. Provide a clearly labelled `Open Scene`/`Find source`/`Bind host` and `Start directing` path. Let Unity's Inspector/Hierarchy/Transform tool do normal spatial editing. Selecting a location cue selects its real transform, without changing the canonical scene content.

Required always-visible status: `[EDIT MODE: LIVE STAGING]`, active scene, story source, cue owner/line/visit, current Lara position/posture, active camera/track, `Preview only` vs `Authored change`, unresolved requirements. Avoid wizard-style setup and new modal screens for daily staging.

## 8. Executor/native division and early stop points

Executor writes source and non-Unity tests but **never opens, starts or controls Unity** (including batch tests, Play and builds). See `MANUAL_GATE_PROTOCOL.md`. G1 (real Game image) stops after N01; G2 (standing/camera drag and Ctrl+S) stops after N04; G3 (both seat modes/save) after N05; G4 (full native) at N10. Art performs every click and returns evidence. If Art cannot verify a gate, source is `PARTIAL / WAITING ON ART` and dependent tickets do not advance. No approval is implied by the existence of Unity API documentation.

## 9. Exit criteria before deleting old renderer

Build the new native stage and prove: actual First Contact standing/camera/Lounger in Scene **and** Game views; live mark drag; save/Undo; teardown; branch and global card stability. Then remove `ScenePreviewSession` and all Studio references, custom camera/orbit/ground dragging/`Build preview` button. Do not permanently support dual stages. Other independent asset preview tools may continue using their own asset-specific preview utilities; the ban is on a **second representation of the performance scene**.

## Unity API evidence (inspect version-specific signatures when coding)

- Unity 6000.0 Game view, Edit Mode camera rendering: https://docs.unity.com/en-us/engine/6000.0/manual/unity-editor/editor-windows-views-reference/game-view
- Unity 6000.5 `AnimationMode.StartAnimationMode(AnimationModeDriver)`: https://docs.unity.com/en-us/engine/6000.5/script-reference/unityeditor/animationmode/startanimationmode
- Unity 6000.5 `AnimationMode.StopAnimationMode(AnimationModeDriver)`: https://docs.unity.com/ja-jp/engine/6000.5/script-reference/unityeditor/animationmode/stopanimationmode
- Unity editor player-loop refresh: https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/editorapplication/queueplayerloopupdate
- `AssetModificationProcessor.OnWillSaveAssets`: https://docs.unity.com/ko-kr/engine/6000.5/script-reference/unityeditor/assetmodificationprocessor/onwillsaveassets
- Unity `SceneView` API: https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/sceneview

**These APIs support a native approach but do not by themselves establish that the current project is correctly wired. Native Unity integration tests and saved-scene verification are mandatory.** See `UNITY_API_AND_RISK_NOTES.md` for exact risk/evidence boundaries.
