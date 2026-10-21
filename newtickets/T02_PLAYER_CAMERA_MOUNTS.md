# T02 — Player camera mounts, fade, release and breathing

**Repository:** `artwhaley/DazPoseWizard`, branch `main`.
**Plan reference:** `docs/plan/ARCHITECTURE_SPRINT_PLAN.md`. This ticket **replaces** the plan's original T02 ("four player states"), which was narrowed in conversation on October 21 (see "Decisions" below).
**Runs after:** T01 and S01. Can overlap S02. Assumes the sandbox rule Art accepted on October 21 (`AGENTS.md`; report in `docs/plan/S01_UNITY_SPIKE.md`): headless EditMode tests, read-only probes, and dry runs of your own editor commands, all in the single sandbox only.
**Owner of decisions:** Art.

Paths starting with `UP/` mean `validation/DazPoseUnityValidation/`.

---

## Goal

Give the player a fixed, tuned viewpoint that every later ticket can judge Lara from, with clean VR-friendly camera moves:

1. Two **camera mounts** in the player body prefab: **High** and **Low**, above the player's head, looking toward his feet.
2. `Camera.StartAt(High|Low)` places the camera on a mount instead of a scene mark.
3. `Camera.SetHeight(High|Low)` moves between mounts by **fading to black, moving, and fading back in**.
4. `Camera.Track(...)` keeps working from a mount: position stays, view rotates.
5. New `Camera.Release()` returns the view to the current mount's authored orientation.
6. Cheap **programmatic breathing** on the player body.
7. Split the mislabelled `World.Posture` into `Lara.Posture` and `Player.Posture`.
8. A **temporary Player Setup scene** with a test panel for tuning and checking all of the above.

## Decisions (October 21, Art)

Record these in the plan's decisions log (section 2) as part of this ticket.

- **No new player poses.** The player keeps the one proven lying-down body. Seated, standing and kneeling player poses are deferred. (Evidence: the body has no Animator, and its anatomy shell, contact profile and dissolve parts were prepared against this one mesh.)
- **Two camera mounts live in the player body prefab**, so they go wherever the body is placed:
  - **High:** about 0.8 m above the head, looking **level** (parallel to the floor) toward the feet.
  - **Low:** about 0.2 m above the head, angled **down the body** toward the feet.
  - Both stay vertically aligned with the head.
- **First vs third person is body visibility**, using the existing body dissolve (`PlayerBody.DissolveIn/Out`). No new verb for it.
- **Camera moves are fade to black → move → fade in.** No glide. This is the VR-friendly pattern.
- **Only the story changes camera height** for now.
- **Track from a mount** keeps the mount position and rotates the view to follow. **`Camera.Release()`** returns the view to the mount's authored orientation. Release isn't used in First Contact; it is tested in the Player Setup scene.
- **Verb shape:** `Camera.StartAt(High)` / `Camera.StartAt(Low)` with an explicit argument for clarity and symmetry; `Camera.SetHeight(High)` / `Camera.SetHeight(Low)`.
- **Breathing** is included, programmatic only, no DAZ export.
- **The Player Setup scene is temporary.** It exists to tune the mounts (which end up in the prefab) and to test. Art decides at acceptance whether to keep or retire it.

## Observable when done

**Player Setup scene (Art, Play mode):**
- Buttons for High and Low: the screen fades to black, the camera moves to that mount, the screen fades back in. Pressing the current height does nothing.
- Body on / body off: the body dissolves in and out with the existing particle effect, seen from either mount.
- Track: the view follows a movable target while staying at the mount. Release: the view turns smoothly back to the mount's authored direction.
- The body visibly breathes, slowly and subtly. A toggle turns it off.

**First Contact (Art, Play mode):** the intro starts from the High mount above the player's head and tracks Lara. At the climax the body dissolves in and is seen from High. Nothing else changes. (Lara's marks were placed for the old camera, so framing will differ; that's expected and gets tuned later.)

**Studio (Art):** the `Camera.StartAt(High)` cue previews the camera at the High mount.

---

## Boundaries

- Follow `AGENTS.md` for Unity operation. In the **sandbox only** you may run headless EditMode tests, read-only probes, and **dry runs of your own editor commands**. Never open the main project in Unity and never change its assets through Unity.
- Anything that changes the main project's prefab or scenes is delivered as an **editor menu command under `DazPose > Player`** that Art runs. Commands must be idempotent, must report what they did, and must be invocable headlessly (a static method usable with `-executeMethod`) as well as from the menu.
- **Dry-run protocol, before handing any command to Art:**
  1. Sync the sandbox with the main working copy.
  2. Run the command headlessly in the sandbox.
  3. Run it a second time to prove it's idempotent (the second run changes nothing and says so).
  4. Show Art a text diff of every asset file the first run changed (prefab, scene, `.meta`), plus the command's report.
  5. Restore the sandbox's assets to match the main working copy and confirm they match.
  6. Only then hand the command to Art. If the diff touches anything outside what the command is supposed to change, stop and report instead.
- Sandbox results are evidence for logic and wiring only. Visual acceptance stays with Art.
- **Story source edits are limited to:**
  - `UP/Assets/Production/Story/Programs/FirstContact.dazstory`: the one line `Camera.StartAt(camera_start)` becomes `Camera.StartAt(High)`.
  - `UP/Assets/Production/Story/Cards/FirstContactEngineering.dazstory`: each `requires World.Posture == ...` becomes `requires Lara.Posture == ...`. No other change.
  - Their `.bindings.asset` companions only as far as the existing tooling requires after those edits; report any change.
- **Do not edit `UP/Assets/Scenes/FirstContact.unity`.** The mounts arrive through the PlayerBody prefab instance. The `camera_start` mark and its location binding stay in the scene, unused (marks are never removed).
- **Do not rename serialized fields** (for example `StoryPoseContent.posture` in the bindings assets) without `FormerlySerializedAs`. Existing assets must load unchanged.
- Edit `UP/Assets/Production/Player/PlayerBody.prefab` (the variant), **never** `UP/Assets/Production/Partners/Partner.prefab`.
- Breathing must not move the **pelvis**, hip or shaft bones. The contact target is measured relative to the pelvis (`PartnerContactTarget`), and the shaft bones are driven by `PartnerBody`.
- Follow the plan's **section 5.8 VR-forward rules**: the director moves only the Player root; nothing writes `HeadPose`; the fade is drawn by the camera, not a screen-space overlay canvas.
- Local commits only; no push.

---

## Known starting facts

Verified at commit `34659b7`. Re-check against the working copy.

**Player body**
- `UP/Assets/Production/Player/PlayerBody.prefab` is a variant of `UP/Assets/Production/Partners/Partner.prefab`. It has **no Animator**. Components: `PartnerBody`, `PartnerContactTarget`, three `PerformerDissolveRig` + `PerformerParticleBody` pairs, and `PlayerBodyPresence`.
- The lying pose comes from placement: First Contact's `player_start` mark is rotated -90° about X at about 1.17 m. `PlayerBody.StartHiddenAt(player_start)` places the body there.
- `PartnerBody` runs at execution order -100 and drives the shaft bones. `PartnerContactTarget` evaluates relative to the bound pelvis bone.

**Player rig and camera**
- Hierarchy: `Player` (PlayerController + PlayerView, owns world position) → `ViewRig` (director orientation) → `HeadPose` (reserved for VR tracking; never written) → `MainCamera`. See `UP/docs/PlayerView.md`.
- `PlayerController.BodyPresence` references the body's `PlayerBodyPresence`. Use that to find the mounts; no new scene binding is needed.
- `ScopedPlayer.StartAt(Transform)` calls `PlayerView.ResetPerformanceView(position, rotation)`. `Track` rotates `ViewRig` toward the target (`PlayerView.cs`). `ScopedPlayer.Track` registers a persistent orientation cleanup.
- **No screen fade exists anywhere in the project.**

**Story vocabulary and where camera verbs are implemented**
- Declarations: `UP/Assets/DazPose/Runtime/Story/Language/CompiledStoryProgram.cs` (`StoryVocabulary.Build`). Currently `Camera.StartAt` takes a `location` bound as `StoryBindingKind.Location` (instant, setup-only). `Camera.Track` takes `performer`.
- Compile-time literal validation precedent: `DazStoryCompiler.cs` validates `Popup.Show` against a fixed set (`Kinks`, `Equipment`, `Clothing`). Use the same pattern for `High`/`Low`.
- Runtime host: `UP/Assets/DazPose/Runtime/Story/Runtime/PerformanceStoryHost.cs` (`case "Camera.StartAt"`, `case "Camera.Track"`).
- Editor rehearsal host: `UP/Assets/DazPose/Editor/StoryStudio/RehearsalStoryHost.cs` (~line 205).
- Descriptions: `UP/Assets/DazPose/Runtime/Story/Language/StoryCommandDescription.cs`.
- Studio state: `UP/Assets/DazPose/Editor/StoryStudio/AuthoringVisualState.cs` (`Writes` and `Apply` for `Camera.StartAt` / `Camera.Track`). Studio stage resolves the camera through `RequireLocation` in `NativeSceneStage.Authoring.cs`; the stage context exposes `Body`, `PlayerRoot`, `ViewRig` and `Camera` (`NativeStageContext.cs`).
- Other `camera_start` consumers: `UP/Assets/DazPose/Editor/Application/FirstContactRepair.cs`, `UP/Assets/DazPose/Validation/Editor/Authoring/StoryFoundationChecks.cs`, the retired `NativeDirectingG2Window.cs` / `G3Window.cs`, and docs (`DAZSTORY_AUTHORING.md`, `FIRST_CONTACT_D8_ORACLE.md`, `PRODUCTION_SCENES.md`, `SCENE_DIRECTOR_STUDIO_GUIDE.md`). Update the live ones; leave the retired G2/G3 windows alone unless they stop compiling.

**Posture**
- `PlayerPosture` (Unknown, Standing, Sitting, Lying, Kneeling) in `Runtime/Story/State/PlayerWorldState.cs` is stored as `world.posture` and exposed as `World.Posture`. It is written **only by Lara's commands** (`StartIdle`, `PersistentPose`, `PersistentSitPose`, `DissolveTo`, `SitAt` in `PerformanceStoryHost.cs`), and is also writable by assignment. Readers include `StoryValueAccessor.State.cs`, `RehearsalStoryHost.cs`, `CardPresentationScope.cs`, `PerformancePresentationService.cs` and `PerformancePresentationSnapshot.cs`. `StoryPoseContent.posture` in the bindings assets uses the same enum.

**Breathing**
- Lara's breathing (`Runtime/Performer/PerformerBreathing.cs`) runs as a job inside her animation graph, so it can't drive the player directly. Its data type `BreathingBoneChannel` (bone name, full-inhale rotation and position delta) can be reused.
- Lara's tuned values (`UP/Assets/Production/Performers/Lara.prefab`): 10 breaths per minute, bone strength 0.3, channels:

  | Bone | Rotation delta (deg) | Position delta (m) |
  |---|---|---|
  | abdomenLower | (-0.2, 0, 0) | 0 |
  | abdomenUpper | (-0.3, 0, 0) | (0, 0.0003, 0) |
  | chestLower | (-0.4, 0, 0) | (0, 0.0005, 0) |
  | chestUpper | (-0.25, 0, 0) | (0, 0.0003, 0) |

  These are a starting point. They may be too subtle for a body seen from above; Art tunes.

---

## Work, in three sessions

Each session ends with something Art can see. Stop after each one for Art's check before starting the next.

### Session 1 — Mounts and `Camera.StartAt(High|Low)`

1. **Mount component.** Add a small runtime component on the PlayerBody root (for example `PlayerCameraMounts`) exposing `High` and `Low` transforms by an enum, with validation (both assigned, both children of the body root, usable forward direction). Mounts are child transforms of the **body root**, not of a bone.
2. **Editor command `DazPose > Player > Add Camera Mounts to PlayerBody`.** Idempotent. Adds the component and two children (`CameraMount_High`, `CameraMount_Low`) to `PlayerBody.prefab`. Initial placement, computed with the body in its lying placement:
   - Find the head bone's world position.
   - High: head + 0.8 m world-up, looking level toward the feet (head-to-feet direction projected onto the floor plane).
   - Low: head + 0.2 m world-up, looking toward the pelvis.

   These are starting values. Art tunes them by moving the transforms in the prefab. Report the computed values.
3. **Verb.** `Camera.StartAt` takes one argument, `High` or `Low`, validated at compile time. It is no longer bound to a scene location. Still instant and setup-only. It places the Player root and `ViewRig` at the mount's position and rotation, and clears tracking, as today.
4. **Ordering.** The mounts move with the body, so `PlayerBody.StartHiddenAt` must run before `Camera.StartAt` in setup. First Contact's setup already does. Document it in `DAZSTORY_AUTHORING.md`.
5. **Update every consumer** listed under "Story vocabulary": runtime host, rehearsal host, descriptions, Studio state and stage (camera previews at the mount via `NativeStageContext.Body`), `FirstContactRepair.cs`, `StoryFoundationChecks.cs`, and the live docs.
6. **Edit First Contact** line 5 to `Camera.StartAt(High)`.
7. **Managed tests:** compiler accepts `High`/`Low` and rejects anything else; existing suites pass.
8. **Agent verification (sandbox):**
   - EditMode tests: mount validation (missing mount, mount outside the body root, vertical forward all fail clearly); placing the camera on a mount sets the Player root and `ViewRig` to the mount's pose and clears tracking, using synthetic transforms.
   - Dry-run the mount command per the protocol. The diff should touch only `PlayerBody.prefab` (and new `.meta` files if any); report the computed mount offsets from the head bone.
   - After Art has run the mount command and the prefab change is in the main working copy: sync, then a read-only probe that loads First Contact, compiles its story, resolves `High` and `Low` from the PlayerBody instance, and reports each mount's offset from the head bone. Confirms nothing in the scene still requires `camera_start`.

**Session 1 observable (Art):** run the menu command; open First Contact; Studio shows the `Camera.StartAt(High)` cue at the High mount; Play starts from High and tracks Lara.

### Session 2 — Fade, `SetHeight`, `Release`, Player Setup scene

1. **Fade.** A camera-attached fade-to-black that renders over everything the camera draws, including transparent dissolve particles. **Not** a Screen Space – Overlay canvas. Preferred: an HDRP fullscreen custom pass after post-processing; acceptable alternative: a camera-parented near-plane quad. State which you chose and why. Fade-out and fade-in durations are serialized defaults on the component (start around 0.35 s each; Art tunes). On cancellation or session Stop, the fade clears immediately. The screen must never be left black.
2. **`Camera.SetHeight(High|Low)`**, awaitable: fade out → move the Player root and `ViewRig` to the mount → fade in. If already at that height, it completes immediately with no fade.
   - If the view is **tracking**, tracking continues after the move (view keeps following from the new mount).
   - If not tracking, the view takes the new mount's authored orientation.
3. **`Camera.Release()`**: stops tracking and smoothly turns the view back to the current mount's authored orientation, using the view's existing default finite-turn duration. Awaitable; may run unawaited like `Track`.
4. Update compiler, runtime host, rehearsal host, descriptions and Studio state for both new verbs. The Studio shows end states; it doesn't need to preview the fade.
5. **Editor command `DazPose > Player > Create Player Setup Scene`.** Creates `UP/Assets/Scenes/PlayerSetup.unity` from the Void environment template with: the PlayerBody prefab at a lying mark, the Player rig, a movable track target at about standing head height near the player's feet, and a test panel (on-screen buttons) calling the **same** runtime methods the story verbs use: Start High, Start Low, SetHeight High, SetHeight Low, Body In, Body Out, Track target, Release, Breathing on/off (wired in Session 3). Idempotent: refuses to overwrite an existing scene unless Art confirms.
6. Managed tests for the new verbs.
7. **Agent verification (sandbox).** Build the fade's sequencing behind a seam that EditMode tests can drive without rendering, then test:
   - Order is fade out, then move, then fade in.
   - `SetHeight` to the current height completes immediately with no fade.
   - Cancellation at every stage leaves the fade fully clear.
   - Tracking survives `SetHeight`; without tracking, the view takes the new mount's orientation.
   - `Release` ends at the mount's authored orientation and stops tracking.

   Dry-run the scene command per the protocol. The diff should be only the new `PlayerSetup.unity` and its `.meta`. Before restoring the sandbox, run a read-only probe on the dry-run scene confirming every panel reference resolves. After Art runs the command in main, sync and repeat that probe.

**Session 2 observable (Art):** run the command, open Player Setup, Play: fades, height changes, body in/out from both mounts, Track and Release all behave as described. Tune the mounts in the prefab here.

### Session 3 — Breathing and posture split

1. **Breathing component** on the PlayerBody root (for example `PlayerBreathing`). Applies `BreathingBoneChannel` deltas to the listed bones on top of their rest local pose in `LateUpdate`, on a slow cycle. Serialized: enabled, breaths per minute, strength, channels (default: Lara's four channels above). Rules:
   - Caches rest local rotation and position at enable; applies rest + scaled delta each frame (no drift); restores rest on disable.
   - Validates at enable that every channel bone exists and is not the pelvis, a hip bone or a shaft bone. Fails loudly naming the bone.
   - Skips work while the body is stably hidden.
   - Added by an idempotent editor command, or by extending the Session 1 command. Either way, dry-run it per the protocol before handing it to Art.
2. **Posture split.**
   - Story-visible names become `Lara.Posture` (exactly what `World.Posture` is today: written by Lara's commands, still assignable) and `Player.Posture` (read-only).
   - `Player.Posture` comes from the player body: a serialized posture on a player component, set to `Lying` on the prefab.
   - `World.Posture` is removed, so stale uses fail at compile time with a message naming `Lara.Posture`.
   - Update the cards file's `requires` lines, every reader listed under "Posture", and the docs.
   - C# type and field naming is your call, as long as serialized data loads unchanged.
3. Managed tests: posture read/write behaviour unchanged under the new names; `Player.Posture` reads `Lying`; `World.Posture` is rejected with a helpful message.
4. **Agent verification (sandbox):**
   - EditMode tests for breathing on a synthetic bone chain: no drift after many cycles; rest pose restored on disable; pelvis, hip and shaft bones rejected by name.
   - A read-only probe that compiles both First Contact story files and reports that every card's `requires` line resolves under `Lara.Posture`.

**Session 3 observable (Art):** in Player Setup, the body breathes and the toggle stops it cleanly. In First Contact, the card Director still selects standing and seated cards correctly (now via `Lara.Posture`).

---

## Finish

- Update `docs/plan/ARCHITECTURE_SPRINT_PLAN.md`:
  - Add the decisions above to section 2.
  - Replace the T02 row in section 6 with this ticket's scope and observable.
  - Add the deferred player poses (seated, standing, kneeling) to section 7.
- Update `docs/plan/STATUS.md` and the current-ticket line in `AGENTS.md`.
- Record technical checks and Art's observations separately.

## Out of scope

- Any new player pose, rig or Animator.
- Retuning Lara's marks for the new camera.
- VR (the fade and mounts are built to carry over; nothing XR is installed).
- Changes to `Track`'s acquisition or follow behaviour, other than continuing across `SetHeight`.
- Erection state, the anatomy shell, the contact profile.
