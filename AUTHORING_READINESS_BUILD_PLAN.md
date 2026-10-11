# Authoring readiness build plan

Status: implementation authorized October 8, 2026; B01–B05 complete; B06–B07 implementation and Editor acceptance passed; B08 build, direct saved-scene player and user menu-start retest passed; remaining persistence/replay and visual observations pending. Reconciled against three reviews October 8, 2026.

This is the current plan for the authoring-readiness sprint. It incorporates the user's eight corrections to the architecture audit and supersedes conflicting requirements in the seven-day plan, remediation contract and previous audit proposal. Days 1–5 and recorded remediation evidence remain historical accomplishments; they do not prove this work is implemented. The user subsequently authorized implementation and execution. Current results are recorded in the implementation handoff below and the shared progress record.

Companion: [Content authoring API](CONTENT_AUTHORING_API.md). Review decisions and source evidence: [review resolution](docs/production-consolidation/AUTHORING_PLAN_REVIEW_RESOLUTION.md).

## Fixed product decisions

1. **Toy output is fire and forget.** All toy errors are silent successful no-ops from the application's perspective. No acknowledgement waits, failure exceptions, scene interruption, fallback scene branch, error log, toast or warning. Intiface owns device troubleshooting. A connected toy and a disconnected toy must produce the same scene execution.
2. **An explicit session Stop cancels everything locally.** Cancel active/queued work, ramps, followers, waits and future output dispatch. Submit best-effort hardware stop without waiting for or reacting to its result. A command already delivered to Intiface cannot be recalled; cancellation must prevent further local production of commands.
3. **The application owns toy connection and configuration.** Scenes consume the application service. They do not connect, scan, construct a backend or disconnect when unloaded.
4. **Production commands live under one top-level DazPose menu.** Initialize Game Session saves the required host and bindings and reports either Ready to Play or the exact author assignments still needed. Retired fixture commands leave the normal menu.
5. **Pose defaults remain smooth.** Omitted transition uses the actor's configured default. Explicit duration/transition is the only reason to change it.
6. **Tuning stays as set.** Stop, Start, completion, failure, Dispose, disable and scene navigation must not restore previous/default tuning. Only an explicit setting change changes tuning. No temporary override/restore policy.
7. **Use the Lara prefab for every actor.** No generalized character controller, capability certification, expanded portability or preflight framework. Judge the actual delivered scene by pressing Play.
8. **One authoring execution path.** Content uses typed scoped verbs; UI, editor windows and fixture code do not own gameplay.

Preserve baseline commit `18b268afe4df1ce11046a760b757002571a104c6` and its recorded ignored-input recovery snapshot, source/metas/GUIDs, wardrobe releases and user-edited layer assignments. Preserve Lara's visibility lights. Copy the saved `Assets/Scenes/FirstPerformanceVoid.unity` environment for new compositions; never regenerate its floor, smoke, lighting, camera or volume from builder defaults. Leave the original scene untouched.

## What the audit found (pre-implementation baseline)

Paths in the rest of this plan are relative to `validation/DazPoseUnityValidation/Assets/` unless prefixed with `docs/`.

| Actual source seam | Deficiency | Build item |
| --- | --- | --- |
| `DazPose/Runtime/Performer/Authoring/ScopedToys.cs`, `PerformanceRun.cs` | Toy commands enter the failure-producing execution kernel; missing toy binding can throw. | B01 |
| `DazPose/Runtime/Toys/ToyControlService.cs` | Ramp waits are not fully cancelled with their wrapper; position cancellation does not cover every route selected for timed hardware motion. | B01 |
| `DazPose/Runtime/Performer/Authoring/PerformanceRun.cs` | Failure cleanup skips the failing operation, which can have partially accepted output; retained cleanup and generation rules need one definition. | B01 |
| `DazPose/Runtime/Performer/Authoring/ScopedPerformer.cs` | `PoseAsync(pose)` explicitly supplies `PoseTransition.Snap`. | B02 |
| `DazPose/Runtime/Performer/Authoring/PerformanceTuningOverride.cs` | Captures previous values and returns restoration cleanup, registered by `OverrideTuning`. | B02 |
| `Player/Runtime/PlayerController.cs`, scene bindings and toy service | Toy access/service setup remains tied to scene components; settings and route selection have no application home. | B03 |
| `DazPose/Runtime/Performer/PerformanceSceneBindings.cs`, `FirstPerformanceVoid/Runtime/FirstContactPerformance.cs` | Capability/asset declarations duplicate sequence content; bespoke host configures bindings, resets state and branches on toy availability. | B04 |
| `DazPose/Runtime/Performer/PerformanceExecutionScope.cs` and scoped authoring classes | Competing authoring patterns remain; raw playback exposes clock mutation to consumers. | B04 |
| `DazPose/Editor/` and `DazPose/Validation/Editor/Legacy/` | Production tools and old fixtures are split between DazPose and Tools/DAZ Pose. | B05 |
| `ProjectSettings/EditorBuildSettings.asset` (project-relative) | No configured game scene list; no production main menu/settings/session navigation. | B06 |

These are static source findings. Nothing was launched or tested for this audit or documentation revision.

## Ownership after the build

| Owner | Responsibility |
| --- | --- |
| `GameApplication` | One application lifetime, current session, scene navigation, persistent toy service. |
| `GameSettingsService` | Saved app settings and explicit tuning persistence; no scene choreography. |
| `PerformanceHost` | One saved scene's bindings, sequence lifetime, Start/Stop and cleanup. |
| `PerformanceSequence` | Authored beats and content references only. |
| `PerformanceRun` / scoped facades | Typed commands, cancellation and the owned motion clock. |
| Existing performer/player/wardrobe/motion subsystems | Actual animation, speech, movement, wardrobe, camera and output implementation. |
| Presentation assembly | Display state and forward button actions to application/session/settings services. |
| Editor tools | Create/configure saved content with Undo; never supply a hidden runtime dependency. |

Do not build a new scripting language, timeline, generic service container or generalized actor system. C# sequences and serialized asset/target references are sufficient.

### Lifecycle meanings

- **Start:** create a new run and enter the sequence from its beginning, using current tuning and scene state. It does not call `ResetPerformance`. A sequence explicitly chooses its entrance, wardrobe and other authored changes.
- **Stop:** cancel the entire active game session and all locally scheduled toy output. Preserve tuning and committed wardrobe; do not teleport or reset pose/expression to an initial default. Relinquish active movement/contact/effect ownership using existing cancellation paths.
- **Start after Stop:** a new script invocation, with the tuning left by the previous invocation. It is not continuation of a discarded C# stack.
- **Resume motion:** where explicitly requested, continue the existing driver's held source time. Starting a new funscript is an explicit playback change, not a tuning reset. Do not label a sequence restart “Resume.”
- **Restart Scene:** an explicit separate action reloads authored scene content. Carry current tuning for that scene's stable actor IDs across the reload. It is never the implementation of Stop or Start.
- **Return to Menu:** Stop, unload the performance scene and load the menu. Keep application settings and the Intiface service alive.
- **Settings:** open without replacing the session or applying defaults. Any output test is an explicit button action.

No new generalized pause system is required for this sprint. Cancellation must work even when `Time.timeScale == 0`; do not pretend time scale alone pauses audio or hardware.


### Stop, disposal and stale-run authority

There is one active game session for this sprint. The application owns its identity and its host; this is not a generalized multi-session system. Explicit application/host Stop cancels every run registered to that current session, every accepted/queued actor/player operation, its clock and all local toy output, including Settings test output. A current session's public `run.Stop()` requests that same stop. Before B03/B04 exist, B01 uses the single run and its bound service directly; B03/B04 connect it to the app session.

`Dispose()` is idempotent resource cleanup, not another global Stop command. It closes that run's own authority and cleans up only work still owned by its identity. Standalone `using (PerformanceRun ...)` callers receive the same owned cleanup. Disposing or explicitly stopping an old run after a new run starts cannot stop the replacement session, its follower or playback. Capture identity before starting work; never acquire a fresh toy generation from a stopped/disposed facade.

A stopped facade is permanently unable to mutate state: actor/player commands settle with the existing cancellation outcome; toy void calls silently do nothing. Already-running script continuations may unwind but cannot regain authority. Only a new run or an explicit Settings action can acquire a new output lease.

The host latches its terminal outcome before disposing: normal return → Completed; explicit cancellation → Stopped; non-toy failure → Failed. Cleanup and late continuations cannot change Completed into Stopped or overwrite a newer run's state. Stop revokes authority synchronously; it does not wait for backend acknowledgements or dead continuations.

Hardware Stop submission and new output use the same transport-order fence. Enqueue the stop fence as part of local Stop; newer output waits only for its submission ordering, never its receipt. Do not allow an old cleanup task to submit a late StopAll after replacement output has begun.

## Ordered implementation packets

Execute B01 through B08 in order. B05 has two separately recorded parts: B05a session creation, then B05b menu migration. B02 covers in-instance tuning; B03 adds app caching/persistence; B06 supplies the actual menu and end-to-end navigation. No packet depends on a later packet to meet its own gate. Each packet ends with changed-file notes and its actual status in `docs/production-consolidation/progress.json`. Do not claim playback from inspection.

### B01 — Make toy output silent and cancellation complete

**Change:** `DazPose/Runtime/Performer/Authoring/ScopedToys.cs`, `PerformanceRun.cs` and `PerformanceToyResult.cs`; `DazPose/Runtime/Toys/ToyControlService.cs`, `ToyMotionFollower.cs` and `Buttplug/ButtplugToyBackend.cs`. Update the affected callers/runners listed below in the same packet.

1. Canonical output is `void Vibrate(float level, int? channel = null)`, `Oscillate`, `RampVibration(float target, float seconds, int? channel = null)`, `RampOscillation`, `MoveTo(float position, float seconds)`, `Follow()`, `StopFollowing()` and `Stop()`. Levels/positions are 0–1. Stop is output cancellation, not connection teardown.
2. Remove toy output from `PerformanceRun.Execute` and its failure policy. Construct `ScopedToys` even when `sceneBindings.Toys` is null; its null-service implementation drops every request silently. This is B01's independently usable interim shape; B03 later injects the app service. Missing service/device/route/channel, invalid toy-only arguments, disconnect/rejection/exception are silent no-ops. Remove `requireToys` as a Begin blocker now; the remaining generalized Begin preflight is removed in B04.
3. Mark `PerformanceRun.HasToys` and `ScopedToys.CanOutput` obsolete, remove every production author call, and move capability/status queries to the app settings snapshot surface in B03. They may remain temporary compatibility diagnostics only; the root guide never recommends availability branches. Migrate `FirstPerformanceVoid/Runtime/FirstContactPerformance.cs` now: unconditional toy calls plus the same authored delays in every connection state. B04 later extracts its host.
4. Retain obsolete `*Async` signatures only as compatibility adapters. Submit through the void path, complete immediately and report local submission/no-op, never hardware acknowledgement. Change `PerformanceToyResult` so `Succeeded` is true for that compatibility outcome, `SuccessfulBindings` is zero and `Errors` empty; do not fabricate a device count to satisfy the current `SuccessfulBindings > 0` implementation. Migrate all non-adapter production callers to void methods. No success/failure branching remains.
5. Ramps schedule local output and return immediately. Scene pacing belongs to `run.Delay`. Stop recommending `ScopedToys.Play`; its compatibility adapter delegates to run-owned playback and Follow. It must not create a second clock or expose backend failure. New code explicitly starts the shared clock via `run.StartPlayback`.
6. Use a run lease plus service/route generations. Check the lease before acquiring route ownership and before queuing, then check after every await and immediately before dispatch. Revoke the run lease permanently on Stop/Dispose. Cancelled ramps exit with zero scaled delta time; followers unsubscribe and pending sends are discarded. Treat re-Follow of the same driver as an ownership change when the run changes.
7. Cancel the actual selected routes, including `HwPositionWithDuration` for position commands. Implement synchronous local `StopAllOutput`: revoke output authority, stop ramps, detach followers, clear pending dispatch and enqueue best-effort StopAll through the transport-order fence. Never wait for receipt. Recheck `isCurrent` after the await inside `ToyTransportDispatchOrderGate.SubmitAsync` and before invoking transport submission.
8. Follow the Stop/Dispose distinction above. Cancel the operation that partially failed as well as its siblings. Cleanup is once-only, uses captured identities and reverses acquisition order where dependencies require it. Stale cleanup cannot touch a new run's follower/playback or submit a new global stop.
9. Keep Unity object access, clock sampling, frame-based ramps, facade calls and UI events on the Unity main thread. Backend transport awaits may remain off-thread (`ConfigureAwait(false)`); pass immutable route/value/generation data to those workers. Synchronize generation checks/queues and marshal snapshot notifications to the main thread. Every internal task is observed by the service with a silent catch; no async-void output workers or unobserved exceptions.
10. Remove toy-path `Debug.LogException` / `LogWarning` calls in the service/backend, including connection callbacks, disposal and shutdown. Inspect the pinned Buttplug package's logging configuration during implementation and suppress any app-side client logging at its own sink; do not disable Unity's global logger. Third-party app-console output is not an accepted exception to the silence requirement. Intiface's own external UI/logs are outside the app. An unknown vendor logging hook is not permission to weaken the contract.

**Required bounded checks, using the existing fake-backend seam in `RemediationPersistentOwnershipRunner`:** add/update cases for missing service, disconnected backend, unsupported route, rejected and delayed acknowledgements; all produce the same authored beat markers without an acknowledgement wait or run failure. Start a ramp and a follower, stop mid-wait at normal and zero time scale, then release the delayed backend and continue several frames: zero subsequent **ordinary output submissions for the retired run**. A deliberate StopAll submission is recorded separately. Cover timed-position routes, a stale facade invoked after a replacement run starts, late old Dispose, and a failing non-toy operation with partial accepted work. The replacement run must continue. This is a small cancellation regression check, not a hardware gate or new framework.

**Done when:** those checks pass and the source inventory has zero production `Toys.*Async`, `HasToys` or `CanOutput` call sites outside explicitly obsolete adapter definitions. The audit baseline has one production caller file, `FirstContactPerformance.cs`; do not defer its migration. B08 still supplies saved-scene Play acceptance.

### B02 — Preserve tuning and repair pose defaults

**Change:** `DazPose/Runtime/Performer/Authoring/ScopedPerformer.cs`, `PerformanceTuningOverride.cs`, `PerformanceRun.cs` and `DazPose/Runtime/Performer/SuccubusPerformer.cs`. Add `PerformerTuning.cs` beside the scoped facade.

1. Make `PoseAsync(pose)` use `actor.DefaultPoseTransition` through the existing scoped kernel. Preserve `PoseAsync(pose, PoseTransition)` for explicit choices. Never insert Snap or a replacement duration.
2. Add `ScopedPerformer.SetTuning(PerformerTuning values)` with the existing nullable-property vocabulary. Validate the requested change before applying it; write only supplied values to their existing actor properties. No captured old values or cleanup registration.
3. Retain `PerformanceTuningOverride` / `OverrideTuning` only as obsolete forwarding compatibility wrappers. They adopt persistent semantics immediately. Remove restoration closures and equality/curve-fingerprint restoration heuristics. Never mutate shared profiles/assets to implement a per-instance change.
4. Remove tuning restoration from completion, failure, Stop, Dispose, disable and replay. Check `docs/production-consolidation/SETTINGS.csv` against actor/player/motion/wardrobe cleanup. Keep one backing owner for each group; record its capture/apply fields for B03.
5. Separate cancellation of pose/expression interpolation from resetting initial state in `CaptureCancellation` / `CapturePersistentCancellation`. Retain last evaluated state; do not call `ResetPerformance` from lifecycle cleanup. Committed wardrobe stays committed; an uncommitted wardrobe transaction may discard its own partial work.
6. Verify and reuse the existing `DazPose/Editor/Performer/PerformerDefaultsEditor.cs` mechanism rather than assuming runtime persistence exists. Static evidence: `QueueApply` captures explicit selected saved performer/grip fields through SessionState/GlobalObjectId; `ApplyQueued` applies with Undo on EnteredEditMode. It does **not** provide app caching or disk persistence. Preserve this explicit editor operation; extend its supported-field mapping only as needed.
7. B02 ends at in-instance semantics and the field-owner inventory. App cache, reload handling and disk Save Current Tuning belong to B03; its top-level menu wrapper belongs to B05. Do not create a temporary settings singleton here.

**Done when:** a setting changed once stays changed through Stop/Start, natural completion and failure cleanup in the same instance; plain pose blends with the configured default; explicit transition still works. Rewrite the old tuning-restores assertion now. Scene-reload and saved-next-launch checks belong to B03/B08.

### B03 — Put toys and settings at application lifetime

**Add:** `DazPose/Runtime/Application/GameApplication.cs`, `GameSettings.cs`, `GameSettingsService.cs`, `SceneCatalog.cs` and `SceneTuningSnapshot.cs` in namespace `DazPose.Game`, within Runtime. Use DazPose.Game rather than DazPose.Application to avoid shadowing UnityEngine.Application in existing DazPose namespaces.

**Add assets:** `Resources/DazPose/GameApplication.prefab` and `Production/Application/SceneCatalog.asset`. One app prefab contains toy/settings/session services and no scene actor/camera/clock references.

**Change:** `Player/Runtime/PlayerController.cs`, `DazPose/Runtime/Performer/PerformanceSceneBindings.cs`, `Authoring/PerformanceRun.cs` and `DazPose/Runtime/Toys/ToyControlService.cs`. Extend `PerformerDefaultsEditor.cs` only for explicit supported editor transfer.

1. Implement idempotent `GameApplication.EnsureStarted()`: load one bootstrap prefab, `DontDestroyOnLoad` it, initialize saved settings once, reset static references correctly with domain reload disabled. Supply `StartScene(id)`, `RestartCurrentScene()`, `ReturnToMenu()` and `StopSession()` as app services before B06 adds buttons.
2. Keep one Intiface backend across scene changes. Connect/Disconnect/Scan are explicit Settings actions; calling `run.Toys` never connects. Ordinary unload stops output but keeps the service/connection.
3. Inject that service into each new run while preserving B01's null-service facade. Remove `PlayerController`'s scene-local toy ownership fallback. Keep old serialized service references only for B05 migration.
4. Expose immutable public connection/device/feature snapshots and settings commands. No backend/vendor types or friend access in Presentation. Catch connection/discovery errors silently; settings reads passive current state, never delivery acknowledgements.
5. Persist versioned endpoint, enabled routes, feature mapping/ranges/inversion and audio settings in `Application.persistentDataPath/DazPose/game-settings.json`, using temp-file replacement. Keep discovery indexes session-only. Prefer backend stable device identity; without one, match saved name/capabilities only when unique. Ambiguous devices remain unselected until chosen explicitly. Intiface handles Bluetooth pairing.
6. Keep each scene's MotionDriver scene-owned. Run teardown unbinds it; the persistent service must not retain unloaded scene objects.
7. Add serialized `sceneId` and `actorId` to bindings. Initializer/catalog reconcile one stable scene ID; each scene actor has its own actor ID, never the shared prefab ID. New Performance Scene generates a new scene ID for the copy. Add Current Scene detects an ID copied from another catalog entry and assigns the new scene a distinct ID; do not merge tuning across scenes.
8. Implement `CaptureSceneTuning(bindings)`, `ApplySceneTuning(bindings)` and `SaveCurrentTuning(bindings)` on GameSettingsService. Capture supported **live** fields from existing owners, including Play Mode Inspector edits. Capture before Stop/disposal and before app-controlled scene unload; host OnDisable provides an idempotent fallback before its teardown. Never cache a value restored by cleanup.
9. Scope a snapshot to that scene/actor and its explicitly bound player view/motion driver. Include actor pose/expression/life/gaze tuning, supported grip values and player/motion scalar settings from B02's inventory. Wardrobe selection and current pose are content state, not tuning. Asset/rig references and shared profile contents are not serialized into settings. Toy/audio preferences are app-global and stay in game-settings.json.
10. `SaveCurrentTuning` explicitly captures that scope and writes `Application.persistentDataPath/DazPose/scene-tuning.json` keyed by sceneId/actorId. In Play Mode the menu uses this app API; the existing explicit editor instance-transfer operation remains separate and Undoable. No unrequested prefab/shared-asset write and no per-frame disk persistence.

**Tuning precedence (per field):**

| Event | Value that wins |
| --- | --- |
| First instantiation in a new app/Play session | Serialized scene/prefab value, overlaid only by fields present in explicitly saved scene tuning. |
| Reload/revisit in the same app session | Current cached live value wins over saved disk and freshly instantiated defaults. |
| Partial saved/cached snapshot | Missing fields fall through to the next source; never replace them with zero/default DTO values. |
| Live script/Inspector change | The live owner is current truth. Capture it on Stop/unload/explicit Save before destroying the instance; do not replay old cache onto a still-live owner. |
| Explicit Save Current Tuning | Capture current values for the selected binding scope, update cache and disk. Do not persist unrelated actors/scenes. |

A normal Edit Mode edit retains normal serialization. If a previously saved runtime field should change too, explicitly save its new value; do not silently erase an intentional saved override. No Save action is needed for current-session Stop/Start or navigation.

**Done when:** using a bounded service/scene probe and existing saved scene data, EnsureStarted twice yields one app service; unloading/loading through the app service preserves service identity/settings; latest live tuning survives reload, and an explicitly saved value survives a fresh app session with absent fields falling through. This service-level check does not require B04's new host or B06's menu. Actual MainMenu → A → menu → B acceptance is deferred to B06/B08.

### B04 — Deliver one saved host and one script contract

**Add:** `DazPose/Runtime/Performer/Authoring/PerformanceHost.cs`, `PerformanceSequence.cs` and `WelcomePerformance.cs`. `PerformanceSequence` is an abstract MonoBehaviour with `public abstract Awaitable ExecuteAsync(PerformanceRun run)`. Host serializes bindings, sequence and `playOnStart = true`.

**Change:** `PerformanceRun.cs`, `ScopedPerformer.cs`, `DazPose/Runtime/Performer/PerformanceSceneBindings.cs`, `PerformanceExecutionScope.cs` and `FirstPerformanceVoid/Runtime/FirstContactPerformance.cs`.

1. Host Start ensures the app, applies current tuning to the new instance before any sequence command, registers one session identity and executes its assigned sequence. Expose `StartPerformance()`, `StopPerformance()` and read-only `State` / `Failure`. States: Idle, Running, Stopped, Completed, Failed. Ignore duplicate Start while Running.
2. Implement the lifecycle contract above: current explicit Stop is session-wide; Dispose is owned cleanup; latch Completed/Stopped/Failed before cleanup and fence late continuations with the session ID. Capture tuning before teardown. Disable, destruction and Return to Menu use the same cancellation mechanics without overwriting the terminal outcome.
3. `PerformanceRun.Begin` retains basic actor/runtime/main-thread checks, removes mandatory `Validate(runtime: true)` capability/content certification, and never blocks on toys. Optional editor reference diagnostics remain; known Lara is the supported actor.
4. Store assets/marks on the sequence once. Bindings contain actor, player, playback, optional contact target and stable IDs. Preserve old serialized declarations for compatibility but hide/deprecate them; do not require duplicate content declarations.
5. Add scoped read-only `CurrentWardrobe`, `VisibilityState`, `IsSpeaking` and `IsGripping`. Gaze/camera marks are explicit semantic scene references, not skeleton paths.
6. Add `PerformanceRun.IsPlaybackRunning` and `PlaybackPosition01`; deprecate public author mutation through `run.Playback`. Use the existing MotionDriver internally; consumers Follow the bound clock without starting it.
7. Retire `PerformanceExecutionScope` from production authoring: make it internal where possible, otherwise obsolete with a forwarding path into the single kernel. Preserve serialized component identities/metas.
8. Convert FirstContactPerformance into a PerformanceSequence: remove EnsureBindings reconfiguration, captured-initial-state restore and bespoke lifetime. Its B01 toy calls remain unconditional. Intentional entrance/wardrobe choices remain explicit script instructions.
9. WelcomePerformance has one serialized PerformerPose and simply awaits `run.Performer.PoseAsync(pose)`. B05 assigns the bound Lara's `InitialPose`; current prefab reference resolves to `Generated/DazPoses/G8F/Vintage Glamour Genesis 8 Female 03.asset`, GUID `c428715ecd67662458a22d5807df2fb4`. Use the existing asset; do not generate a substitute or silently select a random pose.
10. Keep the template to serialized fields, typed verbs and ordinary awaits. No success callbacks, channel enums, polling loops or fixture runner.

**Done when:** a saved scene containing this host and WelcomePerformance starts on Play without an open testing window, completes as Completed, and supports explicit Stop/Start without tuning changes. Runtime can execute it without loading Presentation. Update affected FirstContact/Begin legacy assertions in this packet.

### B05 — Build the production DazPose editor menu

Record **B05a** (session creation and new scene commands) and **B05b** (menu relocation/gating) separately. Complete B05a first so a usable author scene exists before bulk menu cleanup. Both are required for B05 completion; this split does not add another architecture layer.

The existing `Production/Performers/Lara.prefab` and `Production/Partners/Partner.prefab` are inputs, not new build deliverables. B04 supplies WelcomePerformance.

**Add:** `DazPose/Editor/Application/GameSessionSceneSetup.cs`, `DazPoseProductionMenu.cs`, `GamePlayerBuild.cs` and a sequence script template.

Expose these exact normal commands:

| Menu path | Concrete action |
| --- | --- |
| DazPose/Scene/New Performance Scene... | Save a copy of the authored Void environment at a chosen new path; initialize its production session. |
| DazPose/Scene/Initialize Game Session | Idempotently add/bind the saved host, sequence, actor, player view and shared clock in the current scene. |
| DazPose/Scene/Add Lara Performer | Instantiate the production Lara prefab with a new stable actor ID. |
| DazPose/Scene/Add Partner | Instantiate the production Partner prefab; offer it as the host contact binding. |
| DazPose/Scene/New Sequence Script... | Create the standard C# sequence template; author attaches and assigns it after compilation. |
| DazPose/Scene/Save Current Tuning | Explicitly persist supported current tuning for the selected scene actor/session. |
| DazPose/Game/Open Main Menu | Open the saved production main menu scene with normal unsaved-scene handling. |
| DazPose/Game/Add Current Scene to Scene List | Add/update a SceneCatalog entry; preserve its stable ID and title on repeat. |
| DazPose/Game/Build Windows Player... | Build MainMenu first and enabled catalog scenes through the production build helper. |
| DazPose/Wardrobe/Open Clothing Setup | Open existing persistent setup scene. |
| DazPose/Wardrobe/Outfit Configuration | Open existing clothing configuration window. |
| DazPose/Wardrobe/Imported Outfits | Open existing import review window. |
| DazPose/Content/Bake Selected Clip as Gesture | Invoke existing production gesture bake operation. |
| DazPose/Content/Bake Selected Clip as Action | Invoke existing production action bake operation. |
| DazPose/Content/Reconcile Pending Pose Imports | Invoke existing import reconciliation. |
| DazPose/Settings/Pipeline Settings | Open existing pipeline settings. |
| DazPose/Help/Content Authoring API | Open the root API Markdown document. |
| DazPose/Help/Authoring Build Plan | Open this document. |

**B05a — Initialize Game Session algorithm:**

Return one of two explicit editor outcomes: **Ready to Play** (required references and default content are saved) or **Needs author assignment** (list exact unresolved fields, select the host and preserve partial setup). Do not claim the latter is ready, silently choose among multiple actors/cameras/drivers, or enter Play automatically. These are bounded editor assignment diagnostics, not runtime capability preflight.

1. Require an editable saved scene; support Undo and one final save. Reuse an existing host; never create a second one on repeated invocation.
2. Use its existing actor binding; otherwise the selected Lara instance; otherwise the sole Lara instance; otherwise instantiate `Production/Performers/Lara.prefab` when none exists. If several unbound actors exist, leave an explicit assignment field rather than choose arbitrarily.
3. Prefer explicit existing player/camera/clock bindings, then a selected appropriate component, then a sole suitable instance. Multiple unbound candidates produce Needs author assignment. Reuse the authored camera/player view and preserve transform, optics, lighting and volume settings; add its PlayerController/View adapter only if missing. Reuse the existing MotionDriver with its tuning, or create one only if none exists.
4. Create a `Game Session` root for host/bindings and an `Authoring Marks` root for scene targets. Bind an existing unambiguous partner or leave contact optional; a basic scene needs no partner.
5. Attach B04's saved WelcomePerformance if no sequence is assigned. Assign the bound Lara's InitialPose, whose current asset/GUID is recorded in B04. If that reference is missing, report the pose field in Needs author assignment; do not generate a new pose. Keep an author's existing sequence and content fields on repeat. Missing optional contact does not block this welcome scene.
6. Migrate a legacy scene toy component to the app service: transfer explicitly configured settings, remove the scene service with Undo only after references are migrated. Do not overwrite conflicting existing app settings silently; retain the app values and show the migration choice in the editor.
7. Before B06, host+sequence is fully playable without controls. B06 explicitly re-runs Initialize on every scene made during B05a to add exactly one controls prefab; existing bindings, sequence and tuning stay unchanged. Save runtime references, sceneId and actorId. No Play Mode injection.
8. New Performance Scene copies the original saved environment, removes only identified copied legacy performance/control roots from the **copy**, and uses production prefabs plus this initializer. Preserve environment objects and camera settings byte-for-byte where no component addition is needed.

**B05b — Move/hide the existing menu entry points:**

Static baseline, October 8: **67 MenuItem attributes across 46 files: 66 commands and one validation attribute**. Of these, 64 attributes resolve to Tools/DAZ Pose and three to DazPose/Production. The [site-by-site inventory](docs/production-consolidation/AUTHORING_MENU_INVENTORY.md) records every file/line, including constant paths and the validation pair. Seven existing commands become normal production commands; 59 existing commands plus the validation attribute become development-only. Eleven new commands complete the 18-command normal menu listed above.

| Existing owner | Disposition |
| --- | --- |
| `DazPose/Editor/Wardrobe/ClothingSetupBuilder.cs`, `ClothingSetupWindow.cs`, `DazPose/Editor/WardrobeImportReviewWindow.cs` | Route normal open/configure/review entries to DazPose/Wardrobe. Fixture preparation/rebuild stays development-only. |
| `DazPose/Editor/PerformerGestureBaker.cs`, `PerformerActionBaker.cs` | Selected-clip bake entries under DazPose/Content; acceptance asset generation development-only. |
| `DazPose/Editor/DazPoseBrowserImportProcessor.cs`, `DazPosePipelineSettingsWindow.cs` | Route production commands as above. |
| `DazPose/Editor/Performer/ProductionPerformerBuilder.cs`, `ProductionTestEnvironmentBuilder.cs`, `SceneBindingMigration.cs` | Existing rebuild/validation/migration entries under development; initializer replaces migration as the normal workflow. Do not invoke environment rebuilding to create new performances. |
| `DazPose/Editor/PerformerSalsaConfigurator.cs`, `DazPoseAlwaysExportMorphValidator.cs`, `DazPoseBridgeMaterialCopy.cs` | Keep specialist setup/audit operations under DazPose/Development when enabled. |
| `DazPose/Editor/ParticleBody/PerformerSurfaceBindingBaker.cs`, `DazPose/Editor/HandGrip/HandGripCalibrationWindow.cs`, `DazPose/Editor/Partner/PartnerCharacterPreparation.cs` | Specialist rig preparation under development; no automatic recalibration during session initialization. |
| `DazPose/Editor/DazPoseEditorCommands.cs`, Lara candidate/install/first-outfit/second-outfit/review builders | Hide retired fixture/candidate proof entries from the normal menu. Keep implementation and GUIDs for historical runners. |
| `FirstPerformanceVoid/Editor/*Setup.cs` and Void builder/lighting/player migration/smoke copy helpers | Hide old acceptance installation and stage-retuning entries. New scene command does not call them. |
| `DazPose/Validation/Editor/Legacy/**` | Gate all menu attributes, including BT1, motion, grip, anatomy, partner, animation, locomotion and seating fixture commands. |

Use `DAZPOSE_DEVELOPMENT_TOOLS` around legacy menu attributes and their paired validation attributes; when enabled, use `DazPose/Development/Legacy/...`. Keep underlying callable methods available to existing runners. Search constant menu paths as well as inline `MenuItem` strings. No first-party `Tools/DAZ Pose` entries remain in the normal installation. Do not rename third-party menus or asset-creation menus accidentally.

**Done when:** all 67 baseline sites have a recorded disposition, the normal menu has the 18 listed commands, and no first-party Tools/DAZ Pose path remains (including constants). Re-run the source search over DazPose, FirstPerformanceVoid and Player; inspect conditional guards for all 60 legacy attributes including the validation pair. With DAZPOSE_DEVELOPMENT_TOOLS absent, none is exposed. Initialize twice yields the same saved objects/tuning; ambiguity reports Needs author assignment; the default unambiguous Lara scene reports Ready to Play and actually plays.

### B06 — Main menu, settings and scene navigation

**Add:** `DazPose/Presentation/DazPose.Presentation.asmdef` referencing Runtime and the installed uGUI runtime assembly. Add `MainMenuController.cs`, `SettingsMenuController.cs` and `PerformanceControlsController.cs` with saved Canvas-based UI prefabs. Use uGUI already present as `com.unity.ugui` in the project manifest; do not add a UI Toolkit/UXML workflow for this small shell. Reuse an existing EventSystem or create one when absent; never duplicate it on navigation.

**Add assets:** `Production/Scenes/MainMenu.unity`, `Production/Application/PerformanceControls.prefab` and `SettingsPanel.prefab`. Extend B03's app services with UI binding only; scene navigation already has an application owner. Re-run Initialize on the saved B05a scenes to install their controls, then save them.

1. SceneCatalog entries contain stable ID, title, short description, enabled flag and runtime scene path. Editor-only scene selection resolves to that path for builds. Main menu lists enabled entries; selecting one calls `GameApplication.StartScene(id)`.
2. Main menu provides scene selection, Settings and Quit. In-scene controls provide Stop, Start, Restart Scene, Settings and Return to Menu. Controllers delegate to GameApplication/PerformanceHost; no animation/wardrobe/toy timing loops in button handlers.
3. Settings displays Intiface endpoint, explicit Connect/Disconnect/Scan buttons, discovered features and route enablement, feature channel/range/inversion controls where supported, and explicit Test / Stop All Outputs buttons. Settings actions obtain their own current output lease; session Stop invalidates them too. Stop All Outputs invokes B01's synchronous local cancellation without stopping actor choreography. Test has no success/failure message; troubleshoot in Intiface.
4. Settings edits save through GameSettingsService. Show current passive connection state, never output acknowledgements/errors. No connection-required screen, device warning modal or disabled scene selection. A missing Intiface server changes nothing about scene execution.
5. Include master/speech volume controls backed by existing audio owners or a single shared mixer setting; do not implement scene-specific audio preferences. Opening the panel only reads values.
6. App navigation performs Stop before unloading scene references, loads the selected scene, then lets its saved host begin. Returning to menu preserves settings, current tuning cache and the connection. Duplicate scene launch requests while a transition is underway are ignored.
7. Configure build scenes with MainMenu first and catalog scenes included. Preserve unrelated existing build entries; production build helper includes only the intended game entry/catalog scenes. Do not define `DAZPOSE_VALIDATION` or include fixture scenes for a production build.
8. Keep runtime code independent of Presentation. Settings/presentation consume read-only state and public commands; they never access private backend internals or add a runtime dependency on Validation/Editor.

**Done when:** MainMenu → scene A → Settings → menu → scene B works in the actual player and direct scene Play still works without a menu-first ritual.

### B07 — Author the actual content trial

**Add:** `Production/Content/DazPose.Content.asmdef` referencing Runtime only; `AuthoringShowcase.cs`, `AuthoringCompanion.cs` and saved scenes `Production/Scenes/AuthoringShowcase.unity` / `AuthoringCompanion.unity`.

1. Build both scenes from saved copies of the authored Void environment with production Lara prefabs. Keep each host and its sequence saved in the scene.
2. Showcase is a roughly 90–120 second composition: arrival, gaze, speech with a compatible expression/gesture, smooth pose, authored relocation, camera movement, wardrobe/layer/hair changes, contact acquisition, one shared funscript driver feeding visual contact and optional toy output, explicit release and departure. Toy commands are unconditional fire-and-forget instructions; scene timing uses Delay.
3. Companion exercises remaining author verbs in ordinary content beats: walk/turn, seating/stand, full-body action, spell/aura, tracking, and repeated wardrobe no-ops. Reuse prepared assets; no new rig or import pipeline.
4. Both scripts use only the documented run/scoped API plus serialized content/semantic marks. No direct controller poking, fixture discovery, global Find calls, bespoke poll loops, capability list or duplicate asset manifest.
5. Sequences explicitly handle their intended entrance from current visibility/state. The host does not reset appearance or tune values for them.
6. Register both scenes in the catalog. The authoring exercise must reveal missing verbs as bounded API additions, rather than burying a workaround in the UI or example script.

**Done when:** the content is readable as a sequence of actions, runs through the same app shell as future scenes, and requires no testing window.

### B08 — Acceptance and delivery

Acceptance centers on the saved scenes and the user's observation. Do not build a new validation campaign, independent-agent import gate, toy hardware certification or character capability matrix for this sprint.

1. Compile changed assemblies and fix actual errors. Use narrow cancellation checks for B01 where necessary; do not substitute assertion counts for scene playback.
2. Press Play in each exact delivered performance scene with no helper injection. Check composition, smooth default pose, speech, camera, wardrobe, motion and effects. Include the still-pending second-outfit toe appearance observation in the wardrobe review; leave it pending until actually observed.
3. Change representative existing tuning, Stop while active work is running, then Start again. Confirm unchanged tuning and no surviving prior-run speech/ramps/followers/queued actions. Repeat Stop while game time is zero to cover cancelled waits. Check other settings' cleanup paths against the inventory rather than inventing a universal test framework.
4. Run with Intiface absent and with any locally available connection state. Scene behavior is identical. Do not wait for a physical device or judge output success. Explicit Stop proves local cancellation, not hardware receipt.
5. Build the exact MainMenu and both delivered performance scenes for Windows. Document the built Unity player's absolute .exe path, build time and included scenes. Launch that executable with Start-Process in the signed-in user's interactive session (request the tool's desktop escalation when required; this is an explicitly visible test launch). Poll only that process until it has a nonzero MainWindowHandle and Responding = True; verify launcher identity is DESKTOP-MQUSNML\artwh and the process SessionId matches. Do not reuse the Avalonia title DazPoseTool V0 as the Unity player check, and do not claim a running invisible process is ready. Verify main menu, settings persistence and scene switching in the actual player; close only the test player when finished. Record this procedure in the packet handoff.
6. Verify Initialize is idempotent, normal menus contain no old fixture clutter, and the production player has no validation assembly dependency.
7. Update this plan, root API guide and shared progress with **actual** implementation/Play/player observations. Record unresolved visual issues plainly. User visual approval remains separate from compile/source findings; do not fabricate approval.
8. Preserve existing import evidence and recovery inputs. This sprint neither reruns nor retroactively declares the separate historical clean-context import exercise complete.


### Required runner dispositions

These are edits to existing validation source during the named implementation packet, not instructions to run the historical R01–R13 campaign now. Files below are in `DazPose/Validation/` except the explicitly qualified production smoke example. Keep prior reports and assertion counts as dated evidence; changed assertions receive new names and a contract-revision field. Never rewrite historical results or restore rejected runtime behavior to satisfy an obsolete expectation.

| Runner / current expectation | Change and disposition | Packet |
| --- | --- | --- |
| `RemediationPersistentOwnershipRunner.cs`: `toyFinalAcknowledgementSucceeds`, `coalescedRampWaitsForFinalAcknowledgement`, `noDeviceOutputFailsBeforeDispatch`, `deliberateStopCancelsToy` | Rewrite as silent/no-wait output and local cancellation checks in B01. A compatibility Awaitable returns immediately, so the old “pending toy await cancels” assertion is replaced, not preserved. Fake backend counts submissions, not hardware success. | B01 |
| Same runner: `toyCapabilityQueryIsSideEffectFree`, `noDeviceCapabilityIsFalse`, `missingOptionalPlayerAndToysFailClearly` | Retire author capability-branch expectations; separate missing-player error from callable no-op toys. Device snapshot checks belong to Settings/service tests, not scene branches. Preserve unrelated actor/grip/playback ownership checks. | B01; B03 snapshots |
| `RemediationAuthorSurfaceRunner.cs`: `tuningOverrideClampsAndRestores` | Replace with explicit-change validation plus tuning survives Stop/Dispose. Isolate fixture cleanup explicitly so one check does not contaminate the next; runtime must not restore tuning for the fixture. | B02 |
| Same runner: FirstContact ResetAsync/replay/status, `scopedPlaybackFacadeDispatches`, `optionalToysAbsentDoesNotAbort` | Adapt to PerformanceHost state and scoped read-only playback. Natural completion = Completed; explicit Stop = Stopped; replay starts without hidden reset. Absence of a scene-local toy component is no longer an availability predicate. | B04 |
| `RemediationScopedCommandRunner.cs`: `invalidBeginFailsBeforePlayback` / InvalidBindings for missing speech component | Replace the mandatory-preflight expectation with basic missing-actor checks and command-local missing-speech diagnostics. Keep paused Delay cancellation, typed non-toy failures and replacement-ownership checks; add cleanup of the partially failing operation. | B01 partial cleanup; B04 Begin |
| `RemediationPreflightRunner.cs` and `RemediationContentPreflightRunner.cs` | Retain as optional development diagnostics against the existing explicit Validate API and historical content declarations. Remove from the current authoring acceptance path. Do not make Begin call them. Update only compilation compatibility; do not expand their matrix. | B04 |
| `RemediationValidationPlayerRunner.cs` | Keep as an opt-in historical smoke witness; its explicit Validate call does not become a production startup requirement. Adapt scoped API calls if compilation requires it. New saved-scene acceptance is B07/B08, not this auto-quit fixture. | B04/B08 |
| `RemediationContentLocationRunner.cs`, toe/import runners and `FirstPerformanceVoid/Runtime/RemediationProductionSmokeRunner.cs` | Preserve their existing scope and dated evidence. No new gate, blanket deletion, assertion weakening or rerun is required. Fix genuine compile references only if affected. | As affected |

Update the existing remediation runner/aggregator documentation to label superseded semantics. Do not claim a new full historical-suite pass just because changed code compiles.

### B08 observable outcomes

Record this small checklist alongside the normal scene review; no new validation framework:

- Missing service, disconnected/rejected/slow backend: same ordered authored beat markers and same local delay choices, no acknowledgement-dependent wait or toy exception.
- Ramp/follower/queued timed-position work cancelled at normal and zero time scale: no later ordinary transport submission by the retired run; explicit StopAll is counted separately.
- Old facade/continuation/Dispose after new Start: no revived old output and no cancellation of the replacement follower/clock.
- Non-toy partial failure: its accepted partial work and siblings are cancelled.
- Natural return remains Completed after Dispose; explicit Stop is Stopped; actor failure is Failed. Late events cannot relabel a replacement run.
- Live tuning survives Stop/Start and navigation/reload, including a Play Mode Inspector edit. Explicit Save survives a fresh launch; missing saved fields retain serialized values.
- Initializer reports Ready to Play only for saved usable references; ambiguous selection reports exact assignments needed and does not alter existing tuning.
- MainMenu → A → menu → B keeps one application/connection owner. The output Test button remains silent even when nothing happens.

Compile/source checks, deterministic cancellation checks, delivered-scene Play, built-player Play and user visual approval each get separate evidence/status. They are not substitutes for one another.

### When a packet cannot meet its contract

Record the exact missing seam and finish independent work. Do not “fix” the problem by reintroducing toy acknowledgement/failure handling, availability branches, lifecycle tuning restore, runtime capability preflight, scene-owned connections, hidden test-window execution, or substituted environment/rig defaults. Do not mark the packet complete by weakening its check. If a true product decision remains, present the specific conflict; ordinary implementation details stay the implementer's responsibility.

## Implementation handoff

Implementation is authorized and underway. B01–B04 passed bounded Unity checks. B05a saved Welcome played successfully without runner injection; B05b compiles with 18 normal commands and 60 gated legacy attributes. B06's saved UI callbacks passed navigation/reload and paused Stop/Start in graphics Editor Play. Both B07 saved scenes reached Completed through their own saved hosts. B08's production Windows build succeeded with zero errors and its delivered player was verified visible/responding during the recorded run. As of October 11, the user accepted actual player navigation, Settings persistence, Stop/Start, Restart and scene switching. Artistic approval remains separate; see docs/production-consolidation/HANDOFF.md and authoring-reports/B08_MANUAL_CHECKS.md.

The trial exposed concrete API/content seams: copied action/gesture clips require .anim; hair IDs include their outfit prefix; replay requires explicit standing/entrance choices; scoped grip now acquires a held center before explicit motion following. These are implemented without changing source clips, rig calibration, released wardrobe or lifecycle tuning.

Use [HANDOFF.md](docs/production-consolidation/HANDOFF.md) and [progress.json](docs/production-consolidation/progress.json) for current results and any remaining acceptance steps. Historical failures and earlier dated reports remain preserved. User visual approval and second-outfit toe appearance remain separate, pending observations.

### October 11 authoring exercise

The remaining player functional checks are user-accepted. Follow [CONTENT_AUTHORING_WALKTHROUGH.md](CONTENT_AUTHORING_WALKTHROUGH.md) for a fresh scene. Manual sequence attachment, mark creation, partner staging and scattered asset discovery are recorded authoring friction, not silently implemented features. Close focused gaps with the user as the exercise proceeds. No new runtime execution was performed for this documentation update.

### October 11 behavior-settings persistence revision

The user requires automatic persistence of changed behavior settings. This supersedes earlier explicit-save requirements in B03/B05a: the host observes live changes during Play (including paused, stopped and completed states), the settings service atomically saves changed snapshots per scene/actor, and unchanged snapshots cause no disk writes. The production Save Current Tuning command is removed. Scene marks and content assignments retain normal Unity scene saving.
