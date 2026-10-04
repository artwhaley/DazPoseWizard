# Toy authoring and test panel build plan

Status: T0 accepted, 2026-10-03. Runtime, panel, Funscript motion, generic toy
output, and real-hardware validation are complete. This document records the
accepted T0 implementation; additional toy vocabulary and Lara's physical
lever/knob interaction are deferred to later work, not open T0 acceptance items.

## Outcome

A scene author controls toys with capability verbs and normalized values. The service automatically targets the compatible devices Intiface currently exposes. Connection failures, absent devices, unsupported capabilities, and missing optional channels do not interrupt the performance.

Retain a small internal routing table for future extensions. Populate it automatically and provide no user routing editor, assignment toggles, exclusions, saved device profiles, or settings menu in this pass. Intiface owns device selection and hardware configuration.

Use the existing scene ToyControlService and PlayerController.Toys access point. Preserve working Funscript playback and transport. Replace ambiguous Level authoring, shared output scheduling, and the current toy test furniture.

## Verified Handy boundaries

Through the stock Intiface integration, The Handy exposes HwPositionWithDuration. The published definitions for the original Handy and FW4+/Handy 2 do not advertise Oscillate. Therefore Oscillate(level) cannot command native repeated stroking on that integration. Do not silently translate this command into position playback.

The standard Handy protocol handlers do not expose an onboard stroke-limit-setting operation through our Buttplug client surface. Scene position mapping is available to implement generically; it does not reset a limit stored in the device. Intiface's Handy documentation directs users to the manufacturer's settings for restricted physical travel.

The user's Funscript hardware test works. Preserve that behavior. The stuttering test was sine playback on timed-position hardware. Remove the short-timed-move fallback used for sources with no target segments.

Sources checked on 2026-10-03:

- [Original Handy device definition](https://raw.githubusercontent.com/buttplugio/buttplug/master/crates/buttplug_server_device_config/device-config/protocols/thehandy.yml)
- [FW4+/Handy 2 device definition](https://raw.githubusercontent.com/buttplugio/buttplug/master/crates/buttplug_server_device_config/device-config/protocols/thehandy-v3.yml)
- [Original Handy protocol handler](https://raw.githubusercontent.com/buttplugio/buttplug/master/crates/buttplug_server/src/device/protocol_impl/thehandy/mod.rs)
- [FW4+ protocol handler](https://raw.githubusercontent.com/buttplugio/buttplug/master/crates/buttplug_server/src/device/protocol_impl/thehandy_v3/mod.rs)
- [Intiface Handy support](https://intiface.com/docs/intiface-central/brands/thehandy/)
- [Buttplug output semantics](https://buttplug.io/docs/spec/output/)
- [Buttplug device information and dispatch timing](https://buttplug.io/docs/spec/device_information/)

These are upstream definitions, not a captured live device snapshot in this
repository. The physical acceptance record documents only user-confirmed
outcomes and intentionally omits an installed Intiface build or a specific
live device feature snapshot.

## Authoring contract

Final C# casing follows the existing performer APIs. Conceptual method names below become actual documented methods, rather than only panel labels.

| Command or state | Contract |
| --- | --- |
| Connect(), ConnectAsync() | Start one connection attempt; async initialization may be awaited. Ordinary connection failure is recorded centrally and completes without interrupting the scene. |
| Disconnect(), DisconnectAsync() | End requested connection, cancel retries, invalidate pending effects, and attempt broad stop before disconnect. |
| StartScanning(), StopScanning() | Set scanning intent; scanning is nonblocking and restores after server reconnect if still requested. |
| CanVibrate, CanOscillate, CanStroke | Live availability of compatible automatically routed connected outputs. These are not physical-feedback claims. |
| HasVibrationChannel(channel), HasOscillationChannel(channel) | True when at least one connected device has that capability-local channel. |
| Vibrate(level), Oscillate(level) | Set every currently available channel of that capability; independent capability state. |
| Vibrate(level, channel), Oscillate(level, channel) | Set the numbered capability channel on every device that has it; unavailable channels do nothing. |
| RampVibration(target, seconds, optional channel), RampOscillation(...) | Start a scene-time transition from each affected output's current desired value. |
| RampVibrationAsync(...), RampOscillationAsync(...) | Await the authored transition, not device acknowledgement. |
| MoveTo(position, seconds) | Request mapped spatial movement on automatically routed motion features. Prefer hardware timed position; direct-position-only outputs use bounded sampled movement. |
| MoveToAsync(position, seconds) | Await the authored movement duration. No claim of measured physical completion. |
| Play(program, optional loop) | Use the existing MotionDriver/FunscriptPlayback system. No second Funscript parser, timeline, or hardware clock. |
| Follow(driver), StopFollowing() | Explicitly share an existing motion source with hardware; ending hardware follow leaves Lara/source playback independent. |
| SetStrokeLimits(minimum, maximum), SetStrokeInverted(bool) | Scene position mapping, shared by direct moves and playback. Defaults 0..1, uninverted. |
| BindVibration(control, optional channel), BindOscillation(control, optional channel) | Drive capability intensity from a visible scene control's normalized value. |
| UnbindControl(control) | Remove that binding and zero outputs still owned by it. |
| Stop(), StopAsync() | Cancel active toy effects and pending work, set intensity state to zero, and attempt broad stop. Future explicit commands may start effects again. |

Instant intensity commands have no author-facing acknowledgement/report variant. Keep transport Tasks and diagnostic reports internal or explicitly diagnostic. Timed actions use the project's Unity Awaitable facade convention where practical, with Task internals confined to transport.

Numeric values clamp to 0..1; invalid nonfinite values and invalid durations/ranges remain programming errors. Missing optional hardware is an ordinary no-op. No modal failure prompts or log flood from absent capabilities.

Channel semantics:

- No channel argument means all channels of that capability on each device.
- Channel 0 means the first feature advertising that capability on each device; channel 1 the second, and so on.
- Order capability features by advertised FeatureIndex within each device.
- Never interpret author channel numbers as raw Buttplug FeatureIndex or global indexes across all devices.
- A device with one vibration motor ignores requests for vibration channel 1.
- A broadcast supersedes every addressed channel; a channel-specific command supersedes only matching channels.
- Preserve feature descriptions and advertised ranges internally for diagnostics.
- A feature with both Position and timed Position appears as one motion destination with alternative command strategies.

Example initialization and scene vocabulary:

~~~csharp
await player.Toys.ConnectAsync();
player.Toys.StartScanning();
await Wait(10);

// Performer lines use the scene's existing performer/wait conventions.
succubus.DissolveIn(location);
succubus.Say("Oh, you want a little vibration?");
await Wait(2);
player.Toys.Vibrate(0.5f);
succubus.Say("That should warm you up");
~~~

Example optional branching:

~~~csharp
if (player.Toys.CanVibrate)
{
    player.Toys.Vibrate(0.5f);
    await player.Toys.RampVibrationAsync(0.8f, 5f);
}
else
{
    // Continue the alternate authored scene section.
}
~~~

Authoring examples show intent; scene code uses the project's actual dialogue, performer-motion, and wait APIs.

## Runtime rules

### Last command wins

Maintain a revision on each addressed physical output/controller. A newer authored command invalidates old pending values, ramps, moves, followers, or control bindings for that output. No priority tiers or script arbitration framework.

Changing another device, capability, or channel does not cancel unrelated effects. A plain intensity command remains at its desired value until changed, stopped, removed, or disconnected.

A replaced timed action completes its await normally when displaced; it does not continue writing and does not strand the calling scene. Await completion means the action ended, including replacement, and guarantees the target only for an uninterrupted transition. Record the distinction in developer diagnostics.

A new direct command displaces a continuously bound control for the outputs it addresses. That old binding must not regain ownership on the next frame. Rebinding explicitly selects it again.

Stop invalidates everything currently active. An explicitly issued later command takes precedence and may start output. StopFollowing targets motion ownership, while Stop remains broad.

### Automatic routing and removal

Rebuild routes from authoritative connected-device snapshots/events. Route every supported intensity feature automatically. Motion routes retain feature granularity and choose timed or direct position appropriately; do not create duplicate followers for two output capabilities of the same feature.

Do not send nonzero output merely because discovery completes. A later authored command or an active continuous scene effect produces output.

Route removal invalidates old work and attempts zero for an intensity output while it is still reachable. Device removal cannot promise delivery to disconnected hardware. Disposed control bindings attempt zero only while still owning the target; never overwrite a newer command during cleanup.

On reconnect, rebuild fresh route records. Do not reuse cached native objects or assume an old numeric device index proves identity. Capability-wide automatic routing makes user-visible persistent identity unnecessary for this pass.

One-shot commands missed while unavailable are discarded. Still-active ramps/controls may resume at their current scene-time value; motion followers synchronize to the current segment. Do not replay missed historical cues or resurrect expired effects.

### Failure isolation and transport

Use bounded output state per feature and independent progress per device. A timed-out or disconnected device must not delay healthy devices, motion playback, visual controls, or performer animation.

Remove the global output acknowledgement gate and output sharing of the backend lifecycle-operation gate. Retain small connection lifecycle synchronization where needed. Respect any required short socket-write serialization inside the client, without holding a global lock through device acknowledgements.

Each device has at most bounded active work and the latest pending request for each feature. New values replace unsent old values. Recheck output revision and current timing immediately before sending. Catch and record failure within that device's path.

Honor advertised MessageTimingGap at one dispatch layer. Avoid cumulative delays at follower, service, and backend layers. The v4 server enforces its hardware timing gap; retain bounded client work without inventing a faster device capability. Use a conservative fallback only where timing is unavailable, and keep that choice in diagnostics.

Stop uses the control path, bypasses ordinary output cadence, and invalidates pending outputs first. It must not wait for an unrelated failed device's acknowledgement timeout. Already transmitted hardware movement cannot be recalled by deleting client work.

Use feature-scoped stop if the installed client/server supports it. Where only device-wide stop is usable, retain broad Stop but avoid presenting a whole-device stop as precise feature cleanup. Zeroing intensity uses its output-specific command.

### Connection recovery

Connect is idempotent while connected or connecting. After a transient server failure, retry with jittered delays near 1, 2, 4, 8 seconds and a 30-second ceiling. Explicit disconnect, disable, destroy, or shutdown cancels retries and scanning intent.

Invalid addresses and incompatible handshakes remain visible configuration errors; do not spin retry loops for them. Restore desired scanning after a successful transient reconnect. No auto-connect merely because the component exists; Connect expresses that intent.

Reconnect gives fresh registry state. Command callers do not perform routine connection checks or parse reports. Capability checks serve authored branching.

## Execution stages

### Stage 1 — Define facade and automatic routes

Primary files: ToyControlService.cs, ToyOutputBinding.cs, ToyMotionBinding.cs, PlayerController.cs, and the existing backend/device snapshots.

Add capability-specific commands and availability/channel queries. Keep an internal route table with automatic defaults. Split vibration and oscillation desired state. Move author-facing assignment and shared Level APIs out of ordinary usage.

Inventory existing SetLevel, binding, and TestOutputAsync callers. Migrate panel and acceptance helpers; keep temporary obsolete wrappers only if a real repository consumer needs them, then remove them in the final migration stage. Do not build a router UI or persistent identity store.

Acceptance: a freshly discovered compatible feature responds to Vibrate/Oscillate without assignment; multi-device fan-out and per-device channel numbering work; missing channels and absent devices are quiet; discovery starts nothing; vibration never commands oscillation.

### Stage 2 — Replace shared dispatch and fix ownership/cleanup

Primary files: ToyControlService.cs, ButtplugToyBackend.cs, ToyMotionFollower.cs.

Implement per-device independent dispatch with bounded pending feature state. Carry authored revisions through every asynchronous boundary. Make newer commands replace old automation. Implement zero-on-intensity-route/control removal.

Repair stopping so ordinary queued work cannot revive output after Stop. Audit pause, seek, disconnect, disable, and shutdown races. Avoid whole-device side effects for cleanup where feature-specific commands can be used.

Acceptance: deliberately block one fake device and prove another continues; flood an intensity target and prove bounded work/newest value; supersede a ramp/follower and prove no stale sends; remove a reachable intensity route and observe zero; issue Stop and prove it cancels active producers; explicit later output still works.

### Stage 3 — Connection lifecycle and reconnect

Primary files: ToyControlService.cs, ButtplugToyBackend.cs.

Implement the small retry policy and scanning intent. Keep fault information centrally. Refresh routes after reconnect; clear stale object references and pending work. Shut down all timers/workers predictably.

Acceptance: late-starting Intiface connects after retry; stopping/restarting the server updates capability checks; explicit disconnect cancels retries; missing hardware never blocks an authored scene; recovery never replays old cues.

### Stage 4 — Motion path correction and scene limits

Primary files: MotionDriver.cs, MotionTargetSegment.cs, FunscriptPlayback.cs, ToyMotionFollower.cs, ToyControlService.cs.

Preserve the user's accepted Funscript path. Forward authoritative segment notifications through MotionDriver as source-neutral targets. Include playback/run identity so loops and repeated action indexes cannot suppress a new segment. Avoid a second playback implementation.

Timed-position hardware receives one mapped target per live authored segment. Calculate remaining duration at dispatch, skip expired targets, and resynchronize only for bind/seek/resume/source replacement/reconnect. Direct-position-only hardware retains bounded sample following.

Remove the duration-only per-frame/50 ms sine fallback. A source without segments can drive direct Position; it cannot drive timed Position through fabricated tiny corrections. An unsupported combination remains idle with a plain diagnostic explanation. Do not synthesize native Oscillate for The Handy.

Expose scene range and inversion and apply them to every motion entry point, including MoveTo. When range changes during playback, replace the current command once using its remaining time. Preserve source pause/seek semantics and broad stopping.

Implement Play(program) as a convenience over the existing playback components. When following a supplied driver, do not stop, seek, or change Lara's source merely because the hardware follow ends. Service-owned playback convenience may stop its own source.

Acceptance: retain the user's working Handy Funscript behavior; count one timed move per ordinary segment and document explicit resynchronization sends; test looping, seek, resume, late sends, short segments, and range changes; sine never produces a barrage of timed moves on Handy; failure of one follower does not affect other followers or Lara.

### Stage 5 — Authored ramps and visible control binding

Primary files: ToyControlService.cs, PerformerControlSurface.cs; add narrowly scoped binding/transition code if needed.

Advance normalized ramps using scene time. Compute values from elapsed progress, avoiding accumulated hardware delays. Different outputs may start from different current desired values. An absent device does not pause an awaitable scene transition.

Extend the physical control with a change notification and an authored MoveTo/MoveToAsync normalized transition. Keep visual motion independent of hardware. Bind control values to Vibrate or Oscillate through the same public command semantics and dispatch path. Support optional channel targeting.

Connect initial control value when explicitly bound, then send changes only while the binding owns the outputs. Superseding commands stop its writes. Remove/unbind zeros still-owned outputs. Disable/destroy completes active awaits and cleans up bindings.

Defer Lara's physical reach, grip, and manipulation of the visible control, along with IK and contact gating. T0 accepts normalized control animation and toy binding independently of character animation.

Acceptance: independent vibration/oscillation ramps; wait completion at authored time even without hardware; direct commands interrupt ramps; an authored lever/knob changes real toy output; visuals run without toys; toy output runs without Lara; unbinding stops; a superseded binding cannot override a newer command.

### Stage 6 — Rebuild the toy section of the existing panel

Primary file: FirstPerformanceVoidControls.cs; extract a focused drawing helper if it reduces the size of this class without creating another window.

Use the existing right-side window. Keep the panel at its corrected position and constrain every child to its available width. Confirm the actual 1920x1080 game view visually.

Top content:

1. TOYS title and always reachable STOP ALL.
2. Server address, Connect/Disconnect, Scan/Stop Scan, and concise status.
3. Connected-device names and simple capability summary.
4. Separate VIBRATION and OSCILLATION controls.
5. STROKING / FUNSCRIPT controls.
6. SCENE CONTROL binding demonstration.
7. Collapsed diagnostics.

Vibration/oscillation: label intensity/speed and current requested percentage; one visible slider for each capability; broadcast default plus optional channel selection. Avoid duplicate percentage-button rows. An unavailable capability says what is unavailable and disables its bench input, while the author API remains a quiet no-op.

Stroking: Funscript Play/Pause/Resume/Stop, scene min/max range, inversion, and a clearly labeled single timed move. Show that Handy is timed-position-capable. Disable unsupported sine-to-timed-device tests with a concise reason. Keep source choice distinct from whether hardware is following.

Scene control: one authored lever or knob, target/value slider, transition duration, and move control command. Select Vibration or Oscillation for this demonstration through BindVibration/BindOscillation. This selects the effect for the visible prop, not device routing. No Lara animation buttons in the toy panel.

Diagnostics: feature descriptions, raw ranges, transport errors, outgoing target/duration/counts, and current source. Raw single-feature bench commands, if retained, live in this collapsed area with the explicit label Test this feature. Their purpose is diagnosing transport, not ordinary scene authoring.

Remove Use as Level Output, Use as Motion Follower, strategy controls, shared LEVEL TEST, ticket numbers in titles, normalized-surface jargon, and the Lara BT.6 demonstration from the toy section. Stop auto-spawning the old lever/animator from DrawToyControls. Preserve underlying deferred character work for a separate task.

Acceptance: primary controls are readable at 1920x1080; sliders are visible adjacent to their labels; no duplicate test rows or assignment prerequisite; STOP is reachable without scrolling past devices; feature diagnostics cannot stretch or clip the normal panel; panel commands invoke the same facade that authors use.

### Stage 7 — Dependency packaging and authored integration

Keep the pinned upstream 5.0.1 rebuild. Improve scripts/build-buttplug-unity.ps1 and package documentation to identify the exact Unity/JSON inputs, source revision, binary checksum, and supported build targets. Avoid choosing an arbitrary cached JSON package when several exist. Preserve upstream license and contained backend dependency.

Update ToyControlBT1.md and add concise author examples for connection, capability branching, independent intensity, channels, ramps, Funscript, limits, and control binding.

Build an actual scene performance using PlayerController.Toys, the existing performer APIs, and the existing wait/choreography conventions. Provide a hardware-free alternate branch. Demonstrate lever-to-toy binding separately from Lara animation.

Audit the final public surface: ordinary authors see capability commands and authored waits; raw reports, transport scheduling, and assignment objects stay in diagnostics/internals. No remaining current caller relies on ambiguous shared Level.

Acceptance: the author examples compile; the normal scene has no raw device indexes, binding objects, report parsing, or per-command connection checks; the existing Intiface handshake regression still passes with the packaged binary.

## Validation and completion

Build stages in dependency order, with focused checks and commits per stage. UI work can be prepared after the facade contract is stable, but final panel behavior depends on the motion and control-binding stages.

Automated checks use fake devices to cover independent capability routing, channel behavior, delayed/failed devices, replacement races, cleanup, connection intent, ramp completion, and segment delivery. Use the installed Unity references for compilation. Do not infer physical completion from command reports.

Manual checks:

- Connect real Intiface, discover the user's Handy, and record its actual advertised outputs/version.
- Confirm accepted Funscript behavior before and after changes and inspect outgoing timed-command counts.
- Verify no sine-to-timed barrage remains.
- Validate 0..1 and reduced scene ranges; distinguish outgoing mapping from onboard settings.
- Exercise vibration and oscillation with available real devices or Intiface test devices; state which capabilities were only simulated.
- Confirm independent channels with a multichannel device or simulator.
- Stop/restart the server; confirm retries, explicit disconnect, and no stale-cue replay.
- Run an authored ramp and lever/knob binding; interrupt, unbind, and stop.
- Inspect the existing panel at 1920x1080, including its full right edge and internal scroll/slider layout.
- Run the authored scene with no toys and confirm that performer dialogue/actions continue.

If launching Unity for manual acceptance, follow AGENTS.md: use the current validation source project and the signed-in user's visible interactive desktop. Do not call a batch run or a hidden process manual visual acceptance.

Complete only when the facade, physical-control binding, independent device behavior, and replacement panel all work together. Report automated results, actual hardware results, simulated-only coverage, and remaining device-specific limitations separately.

## Scope boundaries

This work creates simple authoring vocabulary and the internals required to make it reliable. Intiface continues to own device selection, hardware reconnect, and manufacturer protocol details.

Retain internal routing extensibility; defer routing settings UI and persistent user device mapping. Defer vendor APIs, Handy cloud/native limit control, synthetic Oscillate on timed-position hardware, new procedural stroking generators, universal IK interaction, and Lara grip animation.

Do not edit ongoing Cast/Aura particle work. When committing this plan or the implementation, stage only the files belonging to the toy work.
