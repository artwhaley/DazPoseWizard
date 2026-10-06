# Performance command contract — Day 1

Established October 5, 2026 from the approved consolidation plan and current source. This is the target contract for Days 2–7. Implementation and test gaps below remain findings; documenting a behavior does not implement it.

**Authoring boundary**

Authors select an actor, content asset and semantic target. `SuccubusPerformer` and `PlayerController` remain the public facades. Existing typed completion enums/results stay intact. A small execution scope observes them and adds actor/command/target context to diagnostics. It does not become another animation engine or force all operations through a global queue.

Before a run, validate its declared capabilities and bound references. Optional toys are explicitly optional; missing required targets/assets/capabilities prevent playback. Preflight cannot guarantee that a target will remain alive or reachable; live failures still report a precise terminal outcome. Scene IDs are stable and unique, resolved within the scene's declared bindings rather than arbitrary global object discovery.

**Time and completion**

| Family | Target completion or lifetime | Repetition and overlap |
| --- | --- | --- |
| Pose/expression | Async settles when the requested state reaches its transition endpoint; the state persists afterwards | New state replaces old intent; superseded waits settle once |
| Gaze/attention/life | Persistent state or target until cleared/replaced; optional acquisition query/wait is distinct | Compatible with speech/body actions, bounded by configured head/eye channels |
| Speech | Finishes when the requested queued clip finishes playback, not at clip submission | Existing FIFO; stopping speech settles current and queued owned requests |
| Walk/turn/seat/action | Completes on documented arrival/seated/standing/action endpoint | Root/body ownership conflicts resolved consistently, not by arbitrary overlap |
| Gesture/spell | Finite authored event completes at its own end | Compatible overlays only; a body-channel conflict follows explicit policy |
| Aura | Persistent visual state until replaced/cleared | Independent of a finite spell, subject to effect binding lifetime |
| Teleport/dissolve | Completes at destination/reveal/hide endpoint with stable visibility and released transition ownership | Conflicting relocation/visibility requests return Busy until existing finite work settles |
| Wardrobe | Completes after atomic commit/no-op/failure; state describes committed appearance | Existing bounded FIFO (32 pending requests); relative operations resolve at execution |
| Grip | Acquisition completes at `Acquired`; contact stays active afterwards | Replacement supersedes previous acquisition; release blends out and relinquishes arm/finger ownership |
| Motion | Starting playback establishes owned running state; program end/stopping is distinct from consumer binding | Explicit driver/session; attach/follow never restarts the clock |
| Player view | Move/orientation completes at respective endpoint; tracking persists | Existing independent position/orientation channels, latest intent wins per channel |
| Toys | A command is applied/dispatched according to backend result; output remains until replaced/stopped | Device/output binding owners prevent stale dispatch; unavailable required output is a failure |

Authored delays and animated body/motion progression use scaled gameplay time, so game pause freezes progression. Speech currently uses a real AudioSource and `isPlaying`; networking/backend dispatch uses task/real-time scheduling. Day 3 must make pause explicit: pause/resume owned speech with gameplay, freeze authored ramps/playback, and send/hold the configured neutral hardware state when pausing a performance. Do not claim that `Time.timeScale=0` already pauses audio or physical output. Backend stop/disconnect cleanup must still progress while gameplay is paused. Physical receipt/motion cannot be inferred from mock transport or local command submission.

Each Unity Awaitable is consumed once. Concurrent finite actions are independently observed; the first failure terminates the run promptly and cancels its remaining owned work. Never await the same pooled Awaitable twice or leave a discarded finite-operation failure invisible.

**Default and setting policy**

Precedence: existing profile/asset defaults → serialized actor/scene overrides → explicit command options. Use one backing owner for a setting. A script override changes only that actor/device/session, never a shared ScriptableObject/material. Group existing fields in inspectors and expose validated semantic settings; do not create a duplicate settings store just to reorganize presentation.

Run reset restores its declared initial state and clears temporary overrides. A runtime tuning change is temporary unless explicitly applied to supported defaults with Undo/prefab override semantics. Outfit Save cannot capture camera, pose, contact calibration or anatomy inspection probes as outfit defaults. Missing optional shoe calibration retains the accepted baked-pose/no-support policy.

**Ownership rules**

Speech, gaze, facial/life channels can continue alongside compatible body/wardrobe/contact work. Keep the existing single actor graph and channel order. Existing speech/SALSA, expression/blink and pose/gesture masks must keep their distinct channel contributions; "owns face" is too coarse.

Public root movement, seat transitions, relocation and full-body actions requested while grip owns the arm use coordinated release first, then acquire body/root ownership. An explicit strict option returns Busy instead. The same rules apply to direct facade calls and the sequence adapter. A grip controller's internal walk-to-acquire/reacquire is part of that same contact request, not a conflicting external movement request.

Visibility/relocation and wardrobe effect rebinding share an exclusive transition owner. They do not begin competing particle/material lifecycles. Wardrobe retains its queued relative commands; an absolute setup preview may supersede only its own pending absolute selection. A command queued behind transient visibility ownership must remain owned/cancellable; reject before acceptance if it cannot be safely scheduled.

Body/pose/action/gesture policies must be based on the documented graph channels and masks. Preserve intentional overlays; incompatible full-body work gets a named busy/superseded result instead of implicit double-writing. Async state replacement settles superseded work exactly once.

**Contact and motion**

Expose these distinct author intentions: acquire contact; hold a calibrated position; follow a specific playback; stop following/hold; release contact. The public naming can reuse existing verbs/options. Do not add another contact clock. Target frame evaluation follows the established world-meter contract. Invalid/destroyed targets and unsupported geometry/state cannot return successful acquisition.

Only the playback owner starts, seeks, resets or stops its driver. Performer and toy consumers may bind to one shared driver; detaching one does not stop/restart other consumers. Cancelling the owning performance stops its owned playback, releases its owned grip, detaches its toy consumer and requests neutral/stop output. An unrelated owner's playback is untouched. Direct toy `Play(program)` must resolve an explicitly configured owned playback, never `FindAnyObjectByType` plus restart.

Supported right-hand/geometry/state limits are explicit profile capabilities. Rigid contact is the initial required integration floor. Member compliance/deformation is member-owned and validated separately. The prepared partner currently has morph state and shell data but no production contact adapter; rod tests prove only that fixture.

**Cancellation, disable, destruction and replay**

Every accepted finite request gets exactly one terminal completion: success, expected no-op, superseded, cancelled, disabled or failed as appropriate to its existing result type. Cancellation of a wait must cancel the underlying owned operation. Disable/destruction settles waits and prevents late continuations from acquiring new ownership or changing a replacement run.

A run has its own lifetime/generation and tracks work it owns. Cancelling a run releases that work, not every global actor/toy action. Successful wardrobe commits remain committed unless a deliberate scene reset/recall requests another state; an in-flight failed atomic commit restores prior appearance. Visibility interruption must settle at a declared stable visible/hidden state and release all effect owners. A failed reveal may report failure with committed wardrobe state; never claim successful rollback if it failed.

For a fresh replay, reset to the scene's persisted initial placement/pose/expression/visibility/wardrobe, clear transient gaze/gesture/contact and restore declared playback state. Then preflight and run again. Do not require exiting Play Mode to replay or preserve a prior run's `hasRun` flag. A reset/stop path must complete while gameplay time is paused.

**Errors and safe boundaries**

- Programmer/preflight errors: null content, ambiguous IDs, missing required binding, unsupported target/configuration. Name the actor, command and offending asset/reference. Avoid instructions that require installing an acceptance harness.
- Expected no-ops: already naked/fully dressed/no selected outfit and existing idempotent clear/stop operations. Preserve typed reasons; do not warn or invent work.
- Runtime failures: target loss/unreachable state, missing compiled fit, rebind failure, queue full, backend disconnect or ownership conflict. Preserve prior committed state and return a precise result. Unexpected failures from void wrappers are observed/reported through the same path.
- Settings/profile validation clamps only established documented ranges. Invalid assets/geometry are diagnosed; do not silently alter source shapes, thresholds or release data to make a test pass.

**Current implementation gaps to validate/repair**

Static trace at HEAD `fff898b5a89cd23971a58704e4fad9e78cd650ea`:

1. `FirstContactPerformance` owns bespoke waits/revision checks and permits one run per Play Mode. Day 3 extracts lifetime and reset.
2. Speech/wardrobe have meaningful queues and typed results, but the facade's stop/cancel surface is not uniform across finite owners. Day 3 adds scoped operation cancellation rather than just skipping awaits.
3. `GripAsync`/`Grip` expose acquisition; void `Grip` discards the awaitable, and `ReleaseGrip` returns no release wait. The controller auto-samples its driver's position. Day 3/4 needs explicit hold/follow/release lifetime semantics.
4. Public root/body guards do not consistently arbitrate external movement against active grip. Existing suppression solves arm layering, not the whole command-ownership contract. Day 3 centralizes these entrances.
5. `DissolveInAsync(float)` reveals at current placement; visible relocation guards reject Hidden. Day 3 provides hidden initialization/appearance coordination.
6. `PlayerController` and toy `Play` use scene-global discovery fallbacks; `Play` changes program and restarts that driver. Day 2/3 replaces these with explicit ownership.
7. `ClothingSetupWindow.SaveConfiguration` compiles against configuration but passes unchanged cloned `preset.Parts` back to generated preset setup. Released Save forks/rebinds a candidate. Day 1 measures these actual paths; Day 5 resolves their data/provenance coherence.
8. Many inspector fields already exist; harness-only settings are identified in SETTINGS.csv. Day 2 promotes missing controls without duplicating working fields.

Execution results and actual failures belong in BASELINE.md, not this target-policy table. User visual approval remains separate. No new product decision is required to proceed with this bounded contract; any unsupported asset behavior discovered later must be documented rather than inferred.
