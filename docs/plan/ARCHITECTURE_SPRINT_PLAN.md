# DazPoseWizard — Architecture Sprint Plan

Written October 20, 2026, from a planning session between Art and Claude, and from a source audit of `artwhaley/DazPoseWizard` at commit `2dee09b`.

This is the reference for the architecture sprint. It records the decisions made, what the audit found, and the ordered tickets. Ticket detail beyond the next ticket is intentionally thin. Each ticket gets written in full only when it is next, using what the previous ticket taught.

Paths below are relative to the repository root. `UP/` means the Unity project at `validation/DazPoseUnityValidation/`.

---

## 1. Purpose and working style

**Product goal.** A real spiritual successor to Virtual Succubus, with some inspiration from Lustbound. A virtual dominatrix: immersive, sexy, flexible, with persistent memory. This sprint covers scene playback only. Persistent and outside-scene behaviour comes later.

**Sprint goal.** Get the project to the point where authoring real content is clean and easy and playback is convincing. A pose should be drag-and-drop: "Lara on that chair in this pose" with nothing left to adjust in the Studio, because the pose already knows how to place itself.

**How we work.**
- Interactive and iterative. Art is present and tests. Nobody walks away expecting a finished game.
- Each session ends with a runnable observable: "Press Play and see X."
- Plan one ticket ahead. Each ticket builds on what the previous one taught rather than executing a frozen script.
- Decisions are made deliberately by Art, not guessed ten steps ahead. "We don't know yet" is a valid entry. Blank spaces stay blank until the right thing goes in them.

---

## 2. Decisions log

All decided October 20, 2026 unless noted.

| # | Decision |
|---|---|
| D1 | The First Contact scene (`UP/Assets/Scenes/FirstContact.unity`) becomes the host for the entire game. Marks are only ever added. Once added, a mark is left alone. Keeping marks clean is the author's responsibility, enforced by a mark lock (T08). |
| D2 | Placement is **mark, then per-pose offset, then pose**. Each pose stores its own translation and full-rotation offset from its reference frame. |
| D3 | Interactive (contact) poses are anchored to **both** a world mark and IK. The mark gives the world position; IK closes the last distance. |
| D4 | **Seat offsets are residuals on top of the hip-pinned pose.** Default `SitAt` also becomes hip-pinned, which unifies the two current seating rules. Reason: UMotion edits will move the pelvis; hip-pinning re-solves placement automatically, so zero offset always means "pelvis on the seat point" on any seat. |
| D5 | **One seat is sufficient.** Furniture is fitted to the seated animation set, not the system to the furniture. A chaise or bed would be its own posture with its own animation set. No multi-chair proof campaign. |
| D6 | Poses are imported from DAZ but **not edited in DAZ**. UMotion is the pose editor. A UMotion-edited pose becomes canonical and is locked against DAZ reimport. Pose editing should happen from inside the Pose Bench. |
| D7 | `World.Posture` is split into separate **Lara posture** and **player posture**. |
| D8 | The **player comes first**: player postures, camera heights and the visible player body exist before any mark or pose tuning, so every pose and mark can be judged from the player's actual viewpoint. |
| D9 | **VR is deferred** to its own mini-sprint immediately after this one. Everything in this sprint follows the VR-forward rules (section 5.8) so nothing blocks it. |
| D10 | Transitions: **try plain pose-to-pose blending first.** Only escalate (step, IK, procedural) if it visibly looks wrong. |
| D11 | Voice: Art records the VO in his own voice and converts it to a female voice with ElevenLabs. Claude does not generate placeholder voice. |
| D12 | The rule that agents must never run Unity is **up for revision** after spike S01. Codex was poor at running Unity. If agent-run Unity doesn't work reliably, Art does the testing. |
| D13 | Final IK is not bought now. Decide after the support-hand and contact-bench tickets show whether authored lean plus in-graph IK is enough. |
| D14 | Agents may run headless EditMode tests and read-only probes in the single sandbox (S01, accepted October 21). |
| D15 | **No new player poses (October 21).** The player keeps the one proven lying-down body. Seated, standing and kneeling player poses are deferred. The body has no Animator; its anatomy shell, contact profile and dissolve parts were prepared against this one mesh. This narrows the original T02. |
| D16 | **Two camera mounts live in the player body prefab (October 21)** so they go wherever the body is placed. **High:** about 0.8 m above the head (tuned to 0.6 m, D24), looking level toward the feet. **Low:** about 0.2 m above the head, angled down the body toward the feet. Both stay vertically aligned with the head. |
| D17 | **First vs third person is body visibility (October 21),** using the existing body dissolve (`PlayerBody.DissolveIn/Out`). No new verb. |
| D18 | **Camera moves are fade to black, move, fade in (October 21).** No glide: the VR-friendly pattern. Only the story changes camera height for now. |
| D19 | **Track from a mount (October 21)** keeps the mount position and rotates the view; `Camera.Release()` returns the view to the mount's authored orientation. Release is not used in First Contact; it is tested in the Player Setup scene. |
| D20 | **Verb shape (October 21):** `Camera.StartAt(High)` / `Camera.StartAt(Low)` and `Camera.SetHeight(High)` / `Camera.SetHeight(Low)`, with an explicit argument. The old scene-mark form of `Camera.StartAt` is gone; `camera_start` stays in the scene unused (marks are never removed). |
| D21 | **Breathing (October 21)** is included for the player body: programmatic only, no DAZ export, never touching the pelvis, hip or shaft bones. |
| D22 | **The Player Setup scene is temporary (October 21).** It exists to tune the mounts (which end up in the prefab) and to test. Art decides at acceptance whether to keep or retire it. |
| D23 | **Fade implementation (October 21):** a camera-parented quad with a small HDRP shader, not a fullscreen custom pass, so no scene has to be edited. Whether it covers the dissolve particles is checked by Art on screen. |
| D24 | **High mount tuned (October 21, Art):** 0.6 m above the head instead of 0.8 m. |

---

## 3. Audit of the current state

Source-only audit. Nothing was run in Unity. "Verified" means read in source at `2dee09b`.

### 3.1 Excellent — trust these

- **`.dazstory` language and interpreter.** `UP/Assets/Production/Story/Programs/FirstContact.dazstory` reads like a script. Commands are typed, and binding errors say what to assign.
- **Card Director.** `UP/Assets/DazPose/Runtime/Story/Language/StoryCards.cs`: weighted draws with tags, progress, cooldown and recency; seeded, forceable, and every candidate logs why it was accepted or rejected.
- **Performer animation graph.** Separate layers: persistent pose → locomotion → seating → motion → gesture → breathing → arm IK → finger grip → gaze → expression → blink. Arm IK is isolated behind a replaceable seam.
- **Studio cue projection.** `AuthoringVisualState.cs` computes a symbolic before/after state per cue and reports "unknown" rather than guessing. Spatial-only edits reuse the cached pose sample (`NativeSceneStage.Authoring.cs`), so dragging an offset will be cheap.
- **`DissolveTo` as the relocation primitive.** Avoids navigation and collision entirely.

### 3.2 Well under way

- **Studio authoring.** Accepted by Art's practical check on October 19. About 20 native cases (A06, A09–A20 and others in `docs/native-directing/ACCEPTANCE_STATUS.md`) were never run.
- **Wardrobe in Studio and First Contact.** Implemented; native production use pending (`docs/wardrobe-directing/progress.json`).
- **Seating.** Works; Lounger states passed native checks.
- **Grip.** A solid first version of a narrow capability: right hand only, one cylindrical target, floor-plane staging.

### 3.3 Kludged

- **Pose assets are bare clips.** `PerformerPose.cs` is 12 lines holding one `AnimationClip`. Nowhere to store offset, frame type, provenance, tags, framing or contact data. Standing poses are sampled at a hardcoded `PoseSamplingConventions.StandingSeconds = 0.5`.
- **Hardcoded story vocabulary.** `StorySceneBindings.ValidateSymbol` requires the performer to be literally `"Lara"`, the body `"PlayerBody"` and the contact target `"Penis"`. `Lara.Grip(Penis)` in `PerformanceStoryHost.cs` ignores its argument and passes one global `run.ContactTarget`, which lives on a different component (`PerformanceSceneBindings`).
- **Two binding components plus legacy.** `StorySceneBindings` and `PerformanceSceneBindings` both carry scene bindings; legacy JSON target/asset arrays remain; legacy `StoryRunner.cs` is still compiled, used only by `StoryProgramSequence.cs`.
- **Seating.** One baked KAWAII profile (`PerformerSeatingProfile`), styles as an enum, yaw-only anchors (`PerformerSeat.YawRotation`). **Two different placement rules**: default `SitAt` uses a baked root, while `PersistentSitPose` / seated `DissolveTo` pin the live sampled hip to the seat anchor (`NativeSeatingMath.BakedRoot` vs `AlignLiveHip`). The Studio flags a standing pose layered under seating as unresolved.
- **Duplicated placement math.** The Studio has its own copies of runtime placement rules (`NativeSeatingMath.cs`, `NativeStandingRules.cs`, both commented as mirroring runtime). Adding offsets in both places would drift.
- **`SuccubusPerformer.cs` is 2,119 lines**, a facade over otherwise clean layers.
- **Marks are freely movable.** `SceneMarkAuthoring.Commit` moves any mark under `Authoring Marks`. "Never move a mark" is currently only a discipline.
- **Posture state is mislabelled.** `PlayerPosture` / `World.Posture` is written only by Lara commands (`StartIdle`, `PersistentPose`, `PersistentSitPose`, `DissolveTo`, `SitAt` in `PerformanceStoryHost.cs`). Cards that `require World.Posture == Sitting` are actually testing Lara.
- **Workspace and agent context.** README gives the wrong First Contact path. `AGENTS.md` is a stack of dated "latest direction" blocks. Six generations of ticket packets. A full cloned Unity project lives under the gitignored `.dazposewizard/`. All of this makes agents work from stale instructions.

### 3.4 Missing

- Per-pose spatial offsets and frame types.
- Pose metadata: compatibility, framing, contact set, provenance.
- Player postures beyond the existing recline; separate player posture state.
- Contact vocabulary beyond one global target.
- Left arm. Only `PerformerMotionMaskUtility.TryResolveRightArm` exists.
- Whole-body reach: lean, support limb, body keep-out.
- Card spatial requirements. The only hook today is `requires World.Posture == ...`.
- Pose transitions policy beyond the existing blend.
- Any XR support (no XR packages installed).

### 3.5 Cards and content

- **No real cards exist.** `UP/Assets/Production/Story/Cards/FirstContactEngineering.dazstory` has 17 engineering cards: 10 are `Popup.Ready("Engineering ...")` gates, several are `wait` loops with a mood bump, 5 call `PersistentPose`. None touches a mark, grip, camera, dialogue, gesture or funscript.
- **Six bound poses**: SexyVamp, LeaningForward, CrossedArmsAmused, Main1, Sassy (standing) and Lounging (sitting).
- **All dialogue audio is placeholder** (`engineeringPlaceholder: 1` in the bindings asset). Dialogue clips are keyed by a hash of the transcript text, so editing a transcript invalidates its audio binding.

### 3.6 Why Lara stands inside the player's leg

`GripReachPlanner.cs` searches only floor-plane stances (`position.y = root.position.y`). It accepts any 3D shoulder-to-wrist distance from 20% to 95% of arm length and checks nothing against the player's body. If the target is too far below the shoulder it returns `VerticalReachImpossible`, with no lean path to recover. `docs/HandGripImplementationNotes.md` confirms whole-body compensation, support limbs and foot anchoring are not implemented.

---

## 4. Verified code facts (reference)

Paths under `UP/Assets/DazPose/` unless noted.

| Fact | Where |
|---|---|
| Regenerating a pose **keeps** the existing `PerformerPose` asset and GUID and writes only its `clip` field. Metadata added to `PerformerPose` survives reimport. | `Editor/DazPoseAnimationClipGenerator.cs`, `EnsurePerformerPoseAsset` (~line 776) |
| Regenerating a pose **clears and rewrites** the destination clip's curves from the DAZ source. Edits made to the clip are lost on reimport. | same file, curve copy (~line 755–773) |
| `PerformerPose` holds only `AnimationClip clip`. | `Runtime/Performer/PerformerPose.cs` |
| Standing pose sample time is a constant 0.5 s. | `Runtime/Performer/PoseSamplingConventions.cs` |
| `PlayerPosture` enum: Unknown, Standing, Sitting, Lying, Kneeling. Written only by Lara commands. | `Runtime/Story/State/PlayerWorldState.cs`; `Runtime/Story/Runtime/PerformanceStoryHost.cs` lines ~119–143 |
| Contact symbol hardcoded to `"Penis"`; performer to `"Lara"`; body to `"PlayerBody"`. | `Runtime/Story/Runtime/StorySceneBindings.cs`, `ValidateSymbol` |
| `Lara.Grip` ignores its target argument. | `PerformanceStoryHost.cs`, `case "Lara.Grip"` |
| Arm IK: Animation Rigging 1.4.1 `TwoBoneIKConstraintJob` run inside the performer's playable graph. Right arm only. | `Runtime/Performer/HandGrip/PerformerArmIKLayer.cs` |
| Finger grip reads the post-IK stream; no LateUpdate bone rewrites. A post-process IK (e.g. Final IK) would conflict with this. | `docs`: `UP/docs/HandGripImplementationNotes.md` |
| Grip staging is floor-plane only; no body keep-out. | `Runtime/Performer/HandGrip/GripReachPlanner.cs` |
| The player contact target evaluates relative to the bound pelvis bone, so it follows a moving pelvis. It was measured in one pose. | `Runtime/Partner/PartnerContactTarget.cs` |
| Seat = approach anchor + seat anchor, yaw only, one baked profile. | `Runtime/Performer/PerformerSeat.cs`, `PerformerSeatingProfile.cs` |
| Two seating placement rules: baked root (default sit) and live hip alignment (persistent sit pose, seated dissolve). | `Editor/StoryStudio/NativeSeatingMath.cs` |
| Studio mirrors runtime facing rules separately. | `Editor/StoryStudio/NativeStandingRules.cs` |
| Marks can be moved freely by Commit. | `Editor/StoryStudio/SceneMarkAuthoring.cs` |
| First Contact locations: `lara_start`, `player_start`, `camera_start`, `stepforward`, `Lounger` (the only seat), `MainPosition1`, `Bedside`. | `UP/Assets/Scenes/FirstContact.unity`, `StorySceneBindings.locations` |
| Player hierarchy: `Player` (world position) → `ViewRig` (director orientation) → `HeadPose` (future tracked-head seam, never written) → `MainCamera`. `PlayerView` supports MoveTo, LookAt, Track. | `UP/Assets/Player/Runtime/PlayerView.cs`; `UP/docs/PlayerView.md` |
| Project: Unity 6000.5.9f1, HDRP 17.5.0, Animation Rigging 1.4.1, no XR packages. | `UP/ProjectSettings/ProjectVersion.txt`, `UP/Packages/manifest.json` |
| Managed tests: five projects under `tests/`; last recorded run 255 passing. | `docs/native-directing/N10_MANAGED_RESULTS.json` |
| `.dazposewizard/` is gitignored and contains at least one full cloned Unity project (`native-directing/G1Project`). | `.gitignore`; `docs/native-directing/*` |
| **Pose clips and their `PerformerPose` assets are gitignored.** They live in `UP/Assets/Animations/DazPoses/` (`poseOutputRoot` in `UP/DazPoseWizard.project.json`). Offsets (T04) and UMotion edits (T14) stored on them would not be version-controlled; S02 recommends a tracked location. | `.gitignore`; `UP/DazPoseWizard.project.json` |
| The Unity Test Framework is installed transitively but is not declared directly in the manifest; no project test assemblies exist. S01 adds them. | `UP/Packages/manifest.json` |
| Tracked `.meta` files at `UP/Assets/` root whose folders are absent from git: `DazExpressionImports.meta`, `Generated.meta`, `TestData.meta`. | `UP/Assets/` |
| Moving Lara's imported/source materials, textures or manifests changes `WardrobeCatalogBuilder.CharacterSignature` and breaks released wardrobe generations. | `docs/cleanup-task1/path-consumers.md` |

---

## 5. Architecture direction

These are directions, not specifications. Each ticket settles its own details.

### 5.1 One placement resolver

A single runtime function resolves where Lara's root goes: **mark → frame rule → per-pose offset → pose**. The Studio preview, the Pose Bench and runtime playback all call the same function. The Studio's mirrored math (`NativeSeatingMath`, `NativeStandingRules`) is retired into it.

### 5.2 Frame types

Each pose declares which frame its offset is measured in. This sprint needs **Standing** (relative to a stand mark) and **Seated** (residual on top of the hip pin at a seat anchor). Lying, kneeling and contact frames are added only when content needs them.

### 5.3 Seated rule (D4)

Every sit, default or custom, pins the pose's sampled hip to the seat anchor, then applies the pose's residual offset. The baked `Sit_Start` transition lands at its own baked root, so sitting into a custom pose needs a short root correction at the end of the transition.

### 5.4 Pose provenance and UMotion (D6)

`PerformerPose` gets a provenance record: the DAZ source it came from, and whether it has been edited. Once edited in UMotion, the clip is canonical. The DAZ regenerator must **refuse** to overwrite it and say so, not skip silently. Offsets stay as metadata on the pose even with UMotion available, because pose clips have their root stripped by design. UMotion edits shape; the bench sets placement.

### 5.5 Marks

Marks are typed (Lara stand, seat, player position, camera, contact surface), append-only, and locked once content depends on them. Player positions are marks like everything else.

### 5.6 Posture state (D7)

`Lara.Posture` is written by Lara's commands. `Player.Posture` is written by player commands. Cards can require either.

### 5.7 Contact (D3)

A contact pose is anchored to a world mark and closed by IK onto a named contact point. Direction for the lean: author it as a full-body pose (UMotion), and let IK close the last few centimetres for one or both hands. Contact points are named and live on the player's rig, so they follow the player's posture.

### 5.8 VR-forward rules (D9)

Nothing in this sprint may break these. They are the checklist the VR mini-sprint will run against.

1. The director moves the **player root** only. Nothing writes `HeadPose`; it is reserved for the tracked head.
2. Camera rotation forced onto the view (`Track`, `LookAt`) must be something VR can disable or replace with whole-body yaw. No story beat may depend on forcing the player's head direction.
3. Player position marks define where the player's body is and an **authored eye height per posture**. VR will reconcile authored eye height with real head height. Do not bake camera height into Lara-side marks.
4. Lara's gaze and attention target `HeadPose`, never the camera transform directly.
5. Any prompt or button needed to progress a scene must be presentable as world-space UI. No core flow may be screen-space only.
6. Watch frame budget on new per-frame features (overlays, IK, particles); HDRP stereo will roughly double render cost.
7. Pose framing (the "feel" relative to the viewer) is judged against the player position mark and posture, which VR inherits.

---

## 6. The plan

Tickets in intended order. **Needs** lists hard dependencies. Spikes S01 and S02 run in parallel early. Only T01 is fully written; the rest get written one ahead.

| ID | Ticket | Needs | Observable |
|---|---|---|---|
| **T01** | **Workspace reset.** Inventory and clean the repo and Unity project: cloned projects, retired scenes, unused packs, stray folders, stacked docs. Rewrite `AGENTS.md` as one current block; fix README; commit this plan into the repo. Approval stop before anything is deleted. | — | Unity opens FirstContact with no new missing references, Play runs as before; a fresh agent reading only `AGENTS.md` states the scene path, folder rules and boundaries. |
| **S01** | **Spike: can a local agent run Unity usefully?** Headless EditMode tests on one persistent sandbox project (not many 30 GB clones). Measure reliability, import time and disk. Outcome: keep or rewrite the no-Unity rule. Must conclude before T03. | T01 | A placement-math EditMode test run by the agent, with the result Art can see; written verdict. |
| **S02** | **Spike: UMotion Pro on 6000.5.9 with Generic Lara.** Install in a sandbox project; open, edit and save a Lara pose clip; check it coexists with the bench preview approach. The store page lists Unity 2019.4 only, so this is a real go/no-go. Fallback if it fails: a per-bone delta asset stored beside the pose. Runs in S01's sandbox; must conclude before T04. | S01 | Art sees an edited Lara pose clip play correctly. |
| **T02** | **Player camera mounts, fade, release and breathing** ([ticket](../../newtickets/T02_PLAYER_CAMERA_MOUNTS.md); narrowed October 21, D15–D22). High and Low camera mounts in the player body prefab; `Camera.StartAt(High\|Low)`; `Camera.SetHeight` with a camera-drawn fade to black; `Camera.Release()`; cheap programmatic breathing on the body; `World.Posture` split into `Lara.Posture` and `Player.Posture`; a temporary Player Setup scene to tune and test. Follows the VR-forward rules. | T01, S01 | Player Setup (Play): fades and height changes between mounts, body in/out from either mount, Track and Release, visible breathing with a toggle. First Contact: the intro starts from the High mount above the player and tracks Lara. Studio previews `Camera.StartAt(High)` at the mount. |
| **T03** | **One placement resolver** (5.1). Studio preview and runtime call the same function. No behaviour change. | T01 | Studio preview and Play land Lara on the same spot for every existing cue. |
| **T04** | **Pose metadata.** Add frame type, offset (translation + full rotation) and provenance to `PerformerPose`. Confirm it survives regeneration. | T03 | A hand-set offset value is still present after regenerating that pose. |
| **T05** | **Standing offset at runtime.** `LocomoteTo` walks to the resolved spot, not the raw mark. | T04 | A hardcoded offset on SexyVamp visibly moves her at `stepforward` in Play and in the Studio. |
| **T06** | **Transitions: blend first** (D10). Observe pose-to-pose blends at one mark with different offsets. If skating looks wrong, the second option is a threshold rule: blend in place when the offset change is small; when larger, take a short step to the new resolved spot with the existing `WalkToAsync(position, facing)` overload, then blend. The bench can show foot travel between two poses so you know which pairs will skate. | T05 | Blend between two offset poses looks acceptable, or the fallback is chosen with evidence. |
| **T07** | **Unified seating** (D4, 5.3). Every sit hip-pinned with residual offset; sit-transition root correction. | T04 | Default SitAt and Lounging both land correctly on Lounger with one rule. |
| **T08** | **Typed, locked marks** (5.5). Commit refuses to move a mark content depends on. | T02 | Attempting to drag a used mark is refused with a clear message. |
| **T09** | **Pose Bench scene** with a written charter (like `ClothingSetup.unity`), using the existing preview and sampler; player reference in a chosen posture. | T02, T03 | Bench opens with Lara and the player body at a reference position. |
| **T10** | **Bench standing mode.** Reference stand mark plus player eye; drag the offset with live preview; save to the pose. | T05, T09 | Tune SexyVamp in the bench; Play shows the tuned placement. |
| **T11** | **Bench seat mode.** Canonical sit as reference; drag the residual offset; save. | T07, T09 | Tune Lounging in the bench; preview matches. |
| **T12** | **Bench quality of life.** Numeric readout, reset, copy offset from another pose, list of poses with no offset yet. | T10, T11 | Bench lists untuned poses; copy offset works. |
| **T13** | **Round trip.** Tune Lounging in the bench, open First Contact, Play, no Studio tweak. | T11, T08 | As stated. |
| **T14** | **UMotion canonical editing from the bench** (5.4). Bench hands the selected pose to UMotion; provenance lock; regenerator refuses to overwrite edited clips. | S02, T04, T09 | Edit LeaningForward from the bench; reimport from DAZ is refused with a clear message. |
| **T15** | **Studio pose browser** with drag-and-drop pose → cue. | T12 | Drag a pose onto a cue; it plays correctly with no tuning. |
| **T16** | **Studio offset display and warnings.** Show the offset in effect; warn when a pose has none for its frame type. | T15 | An untuned pose shows a warning on its cue. |
| **T17** | **Contact vocabulary.** Named contact points on the player rig; remove hardcoded `"Penis"`; `Lara.Grip` honours its argument. | T02 | Grip a second named point from script. |
| **T18** | **Bench contact mode, first pass.** Pose + mark + contact target in the bench. | T09, T17 | Preview a grip pose against the player in the bench. |
| **T19** | **Left arm and hand parameter.** Left-arm resolver and IK; either hand can grip. | T17 | Same grip performed with left and right hand. |
| **T20** | **Keep-out volume.** Player-body clearance used by the planner; overlay in bench. | T18 | The leg-intersection case is rejected or restaged. |
| **T21** | **Support-hand IK** onto a surface or the player's leg, arm or chest. | T19, T20 | Lara braces with one hand and grips with the other. |
| **T22** | **Contact bench, complete.** Mark + contact target anchoring, reach-error and penetration overlays. Then the D13 decision: in-graph IK enough, or evaluate Final IK? | T21 | Overlays show error and penetration for a lean pose; Final IK decision recorded. |
| **T23** | **Card spatial requirements.** Cards declare Lara posture, player posture, location kind and contact target; the Director rejects unsuitable cards before execution. | T02, T08, T17 | An incompatible card is rejected with a reason in the Director log. |
| **T24** | **One real grip card.** Lean, switch hands, funscript playback. Includes a check that cancelling mid-grip restores IK, grip and offset state. | T22, T23 | Draw the card in First Contact; it plays; cancel restores cleanly. |
| **T25** | **Polished cards and authoring integration.** Card authoring joins the Studio or bench, whichever fits by then. Needs Art's voice clips. | T24 | Author a new card from existing assets without code. |
| *V01* | *VR mini-sprint (after this sprint).* Run the VR-forward checklist (5.8): XR packages, tracked head under `HeadPose`, eye-height reconciliation, world-space prompts, gaze to headset, frame budget. | sprint end | — |

---

## 7. Deferred, open and blank

**Deferred**
- VR (V01), right after this sprint.
- Additional furniture classes (chaise, bed) as their own postures with their own animation sets.
- An interaction-definition framework. Build two interactions by hand first; extract the shared asset from what repeats.
- Final IK (decided at T22).
- Cleanup of the 2,119-line `SuccubusPerformer.cs` facade, unless a ticket is blocked by it.
- Player poses other than the lying body: seated, standing and kneeling (D15), with their camera heights, player position marks and per-posture contact checks.

**Open (Art decides when the ticket is next)**
- Whether `Track`/`LookAt` becomes whole-body yaw or is disabled in VR (V01; T02 must not depend on it).
- Exact transition policy (T06).

**Blank (we don't know yet)**
- Player-relative frames for VR "loom over the player" poses.
- Per-pose eye-height tolerances.

---

## 8. Tools

| Tool | Status | Role |
|---|---|---|
| Unity Animation Rigging 1.4.1 | Installed | In-graph arm IK today; left arm and support hand build on it. |
| UMotion Pro | To be tested (S02) | Pose editor (D6). [Store page](https://marketplace.unity.com/packages/tools/animation/umotion-pro-animation-editor-95991): $70, version 1.29p04 (March 2025), compatibility listed for Unity 2019.4 only. |
| Final IK | Not now (D13) | [Store page](https://assetstore.unity.com/packages/tools/animation/14290): $90, version 2.5 (April 2026), HDRP listed for 6000.4 and 6000.0, not 6000.5. Integration risk: it normally post-processes bones, which conflicts with the in-graph IK and the finger grip reading the post-IK stream. |
| Pose Bench | To build (T09–T14, T18–T22) | One permanent scene with modes: standing, seated, contact. |
| ElevenLabs | Art's VO pipeline (D11) | See the voice note below. |

**Voice note.** Performing the lines yourself and converting with speech-to-speech is a sound approach: it keeps your timing, emphasis and laughs, which text-to-speech struggles with. Before investing, read section 6 of the [ElevenLabs Prohibited Use Policy](https://elevenlabs.io/use-policy), titled "Prohibited Sexual and Graphic Content"; I could not read its body text, so I can't say what it allows. Practical points: keep transcripts stable once recorded, because dialogue bindings are keyed by a hash of the transcript text; SALSA lip sync is driven from the audio.

---

## 9. Process rules for this sprint

- Art is the tester and is present. Work in small steps with an observable at the end of each.
- One ticket written ahead. The next ticket is revised by what the current one taught.
- S01 accepted October 21: agents may run headless (`-batchmode -nographics`) EditMode tests and read-only `-executeMethod` probes in the single sandbox at `C:\Users\artwh\DazPoseWizard-Sandbox`, using `scripts/sandbox-sync.ps1`. Headless sandbox editor-command dry runs require a text diff of every changed asset, followed by restoration and confirmed parity with main. Never operate main in Unity, launch GUI/Hub/Play/builds/players, or create a second sandbox; no sandbox assets flow back to main. `AGENTS.md` states the full boundary; Art owns visual acceptance.
- Disk: at most **one** persistent sandbox Unity project for agent checks. Never clone the 30 GB project repeatedly.
- Local commits are fine; no push without Art's say-so.
- Preserve GUIDs. Move assets with their `.meta` files, with Unity closed, or inside the Unity editor.
- Protected: Lara imported/source materials, textures and manifests (character signature); immutable wardrobe releases; second-outfit toe fixtures; production art; story global index.
- Record status in one place (named in `AGENTS.md` by T01). Record technical results and Art's visual acceptance separately; never claim a visual pass from source.

---

## 10. Risks

| Risk | Where it bites | Mitigation |
|---|---|---|
| UMotion doesn't work on 6000.5.9 or fights the bench preview | T14 | S02 early; per-bone delta fallback |
| Offsets cause visible skating in blends | T06 | Blend first; threshold-step fallback |
| Hip-pin rule changes how existing sits look | T07 | Compare Lounger before/after in the bench |
| Contact target deforms badly in non-recline postures | T17 and any later player-pose ticket | T02 keeps the single lying body (D15); check each new posture when it is added |
| Cleanup moves break string paths or the wardrobe character signature | T01 | Path-consumer search; protected list; approval stop |
| Agent-run Unity unreliable | S01 | Art tests; rule stays |
| Studio's ~20 unrun native cases hide save/undo bugs | T15–T16 | Run the relevant ones when those tickets touch the Studio |
