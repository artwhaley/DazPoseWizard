# Performance command contract

Revised October 8, 2026 to match the user's authoring-readiness decisions. [The root build plan](../../AUTHORING_READINESS_BUILD_PLAN.md) is the implementation authority; [the root API guide](../../CONTENT_AUTHORING_API.md) distinguishes current source from planned changes. This is a target contract, not evidence of execution.

## Authoring and presentation boundary

Content uses PerformanceRun and its scoped performer/player/toy facades. Existing runtime subsystems execute commands. PerformanceHost owns the saved scene's sequence lifetime. The application owns scene navigation, settings and Intiface. Presentation displays state and invokes public commands; no execution behavior may live only in an editor or test window.

Every actor uses the prepared Lara prefab. Begin requires basic explicit bindings and Play/main-thread use. It does not run a generalized capability/content preflight or require device availability. Content references are assigned once on the sequence, not repeated in a certification manifest.

## Completion and timing

| Family | Completion / lifetime |
| --- | --- |
| Pose / expression | Async completes when the requested blend settles. Current state persists. Omitted pose transition uses the actor's configured smooth default. |
| Gaze / attention | Target/state persists until explicitly replaced, cleared or cancelled; acquisition wait is distinct. |
| Speech | Await completes after the queued clip finishes. Stop cancels current and queued owned speech. |
| Movement / seat / action | Await completes at the defined arrival/seated/standing/action endpoint. Existing ownership rules remain. |
| Gesture / spell | Finite completion at the event endpoint; preserve compatible graph overlays. |
| Aura | Persistent until cleared/replaced/cancelled. |
| Visibility / relocation | Completes at the declared visible/hidden/arrival endpoint and releases transition ownership. |
| Wardrobe | Completes after atomic commit or successful no-op; committed appearance persists. |
| Grip | Acquisition and release have separate endpoints; held contact persists between them. |
| Motion | Run owns the clock; consumers follow without starting or rewinding it. |
| Player view | Position and orientation have separate finite channels; tracking persists. |
| Toys | Submission returns immediately. No acknowledgement, output result or toy error affects the application. |

Use run.Delay for scene timing. Local ramps and motion use their gameplay clock; backend scheduling is asynchronous. Stop must complete locally while game time is zero. Time scale alone is not a complete pause contract for audio or hardware. No new pause system is required by this sprint.

Consume each Unity Awaitable once. Non-toy finite failures retain useful actor/verb context and cancel all accepted work, including any partial work from the failing operation. Explicit cancellation is a normal stopped outcome.

## Toy contract

All toy errors are silent successful no-ops to the app: unavailable service/device/channel, disconnect, rejected output, invalid toy-only request and backend exceptions. No acknowledgement wait, exception propagation, output-error logging, toast, scene branch, retry policy or scene interruption. Intiface owns troubleshooting.

Connection, discovery and saved output-route configuration live at application lifetime. Settings may display current passive connection/device snapshots; delivery success/failure does not drive UI or scene behavior. Scenes neither connect nor disconnect.

A current session's explicit Stop invalidates all local output authority, exits ramps, detaches followers and clears queued dispatch, then enqueues best-effort hardware StopAll through the ordered submission fence without waiting for receipt. Already-delivered hardware work cannot be recalled; there is no claim of physical receipt. Stopped/disposed facades cannot obtain fresh output ownership. Old continuations and cleanup cannot enqueue work after cancellation or stop a new run's clock/follower. Dispose cleans only its captured run identity; it is not another global Stop command.

## Tuning and defaults

Settings have one existing backing owner. Explicit options override only what the author requested. Pose without a transition argument uses the actor's configured default; never force Snap.

Tuning changes persist. Stop, Start, completion, failure, Dispose, disable and scene navigation do not restore previous/default values. Remove temporary override cleanup and value-comparison restoration. Do not automatically rewrite shared profiles, materials or prefab assets.

B02 covers in-instance persistence; B03 adds the application cache/disk mechanism. At first load, serialized values are overlaid only by explicitly saved fields. On reload/navigation, current cached values win over disk and defaults. Missing snapshot fields fall through. Capture the latest live values, including Inspector edits, before teardown; never replay old cache onto a live owner. Normal Edit Mode edits retain normal serialization. Explicit Save Current Tuning persists the selected scene/actor and its bound player/motion scope in Application.persistentDataPath/DazPose/scene-tuning.json; app-global toy/audio settings use game-settings.json. No Save is needed between Stop and Start in the same session.

Cancellation of an uncommitted wardrobe transaction may discard partial transaction work. This is separate from changing tuning or rolling back a successfully committed wardrobe selection.

## Ownership and lifecycle

Preserve the existing actor graph, speech/wardrobe queues and body/channel arbitration. Do not add a global action queue or second motion clock. Public movement/relocation coordinates release of active grip using existing performer rules; explicit strict ownership remains available.

Start creates a new sequence invocation with current tuning/state. Stop cancels the entire active session and all local toy output, settles waits and releases active owners. It does not teleport, restore initial pose/expression/wardrobe or invoke ResetPerformance. Cancellation of interpolation retains evaluated pose/expression state.

Restart Scene is a separate explicit content reload preserving tuning. Return to Menu stops and unloads the scene but keeps the app service/settings alive. Natural completion latches Completed before owned Dispose; explicit cancellation latches Stopped; non-toy failure latches Failed. Late callbacks cannot relabel a newer run. None resets tuning or committed scene state.

Per-operation identities protect replacements from late cleanup. Use one cancellation kernel. Do not leave the failing operation running, use stale generations or treat cancelling a wrapper's await as cancellation of its underlying execution.

## Acceptance and preservation

Initializer reports Ready to Play only when required saved references/content are assigned; otherwise it reports Needs author assignment with exact fields. Use the actual saved scenes with their saved host/sequence; pressing Play is the authoring acceptance path. Existing fake-backend checks provide the small local cancellation regression, including stale facades and zero time scale; they are not hardware or preflight gates. Build and play those exact scenes for the required player check. No new capability matrix, portability program, hardware-certification campaign or independent import gate is added.

Preserve the authored Void environment, Lara rig and lights, user wardrobe assignments, releases, source/GUIDs and recovery baseline. Historical evidence remains in the dated day/remediation reports. Do not claim user visual approval or successful execution from this contract or source inspection.
