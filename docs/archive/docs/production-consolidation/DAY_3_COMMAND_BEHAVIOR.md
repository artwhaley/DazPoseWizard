# Day 3 command execution behavior

`PerformanceExecutionScope` is a disposable lifetime for one performance run. Use the factory overload of `Observe(actor, channel, () => actor.CommandAsync(...), expected, label)` so the scope checks availability before starting work and captures that exact request's cancellation. Each Awaitable is consumed once. `Wait` watches independently observed results; the first unexpected result/exception cancels remaining owned work and reports actor/command context. `Delay` uses scaled gameplay time. `Cancel` is idempotent and interrupts pending operations without waiting for scaled time. Persistent state needs `Own` or `persistent: true`.

| Channel | Cancellation behavior | Replacement protection |
| --- | --- | --- |
| Pose / expression | Settles Cancelled, restores declared actor default immediately | Exact pending request |
| Speech | Cancels selected current/queued clip, preserves other FIFO requests | Request ID |
| Movement | Settles Cancelled, releases body/arrival hold | Exact request, including completed hold |
| Seating / action / gesture | Settles Cancelled, releases transition/layer ownership | Request identity |
| Visibility / teleport | Settles Cancelled, tears down effects/audio, restores stable visible state | Exact active operation |
| Wardrobe | Settles Cancelled, keeps any already committed outfit, restores stable visible state | Exact queue request and captured effect operation |
| Grip | Pending acquisition settles Released; immediate stop releases arm and owned staging walk | Generation |
| Gaze / player tracking | Clears only captured intention; tracking does not move the root | Intention identity |
| Spell | Settles Cancelled, disposes the captured instance | Instance |
| Toy intensity | Supersedes captured output owners, submits zero; later commands survive | Per-route owner/revision |
| Playback | Stops owned playback only while revision matches; detaches followed playback without changing its external clock | Explicit driver, revision, binding generation |

Ordinary speech remains FIFO. Pose/expression retain replacement semantics. Gesture and gaze remain overlays. Public root/body commands release active contact immediately before taking ownership; `WalkToAsync(mark, strictOwnership: true)` throws `PerformanceBusyException` instead. `ReleaseGrip` remains the animated release; scope stop/reset uses immediate `CancelGrip`, including while paused. Grip's internal staging walk is exempt from external arbitration.

`WalkToAsync(mark, LocomotionArrivalBehavior.Hold)` encapsulates the arrival-pose hold previously controlled by First Contact. Retain its cancellation as persistent ownership when the sequence must release that hold on stop. `DissolveInAtAsync(mark, seconds, optionalPose)` places a stably hidden actor before revealing it.

Gameplay pause freezes authored waits, movement and ramps. Speech explicitly pauses/unpauses its AudioSource. Toy output submits zero intensity and the configured neutral position, retains current intent and restores it on resume. Backend events and stop/disconnect tasks continue. These are software contracts; transport mocks do not establish physical device behavior.

First Contact owns its scope and supports Stop, ResetAsync and Run repeatedly in one Play session. Its declared reset restores initial actor/player placement, actor default pose/expression/visibility, initial wardrobe and clears owned movement/gaze/speech/effects. It does not snapshot every live inspector tuning field. The optional toy beat continues without hardware when vibration is unavailable; required actor/view/seat/content references fail with context.
