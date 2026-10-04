# Toy authoring and test panel

The existing scene `ToyControlService` owns Intiface transport and routing.
Authors reach it through `player.Toys`. It automatically targets the compatible
features Intiface reports; scene code does not assign devices or feature indexes.
Discovery alone sends no output. Missing devices, unsupported capabilities, and
optional channels are quiet no-ops, so scenes should use capability checks only
when they want to author an alternate branch.

## Scene authoring

Connect and scan once during scene setup. The scan intent resumes after a
transient server reconnect. Connect retries transient connection loss with
jittered 1, 2, 4, 8… second delays capped at 30 seconds. Explicit disconnect
cancels retries and scanning intent.

```csharp
public async Awaitable WarmUpScene(PlayerController player, SuccubusPerformer succubus,
    AudioClip offer, AudioClip reply)
{
    await player.Toys.ConnectAsync();
    player.Toys.StartScanning();
    await Wait(10f);

    await succubus.SayAsync(offer);
    await Wait(2f);
    if (player.Toys.CanVibrate)
    {
        player.Toys.Vibrate(0.5f);
        await player.Toys.RampVibrationAsync(0.8f, 5f);
    }
    await succubus.SayAsync(reply);
}

private static async Awaitable Wait(float seconds)
{
    float elapsed = 0f;
    while (elapsed < seconds)
    {
        await Awaitable.NextFrameAsync();
        elapsed += Time.deltaTime;
    }
}
```

The existing First Contact performance also runs a short vibration beat after
its second line when `player.Toys.CanVibrate` is true. With no service or no
vibration feature, it follows a same-length hardware-free branch and the
performer scene continues.

`Vibrate(level)` and `Oscillate(level)` are independent commands. They broadcast
to all outputs of their own capability. `Vibrate(level, channel)` and
`Oscillate(level, channel)` select a capability-local channel on each device:
channel 0 is the first advertised feature of that type on that device, channel 1
is the second, and so on. A missing channel does nothing. New commands replace
older work for the outputs they address; unrelated outputs keep their current
state. `Stop()` invalidates active effects and requests a broad device stop.

`CanVibrate`, `CanOscillate`, `CanStroke`, `HasVibrationChannel(channel)`, and
`HasOscillationChannel(channel)` describe connected advertised features, not
physical feedback. Authoring commands do not require callers to check the
connection first.

## Stroking and Funscript

`Follow(driver)` sends the existing `MotionDriver` timeline to compatible motion
features. It does not create another player or stop the source when hardware
follow ends. A timed-position feature receives one target for each authored
Funscript segment and the remaining segment duration. A direct Position feature
receives bounded latest-value samples. Timed-position hardware receives no
fabricated frame-rate moves from a sampled sine source.

`MoveTo(position, seconds)` uses automatic motion routing and scene range
mapping. `SetStrokeLimits(minimum, maximum)` and `SetStrokeInverted(bool)` apply
to direct moves and followed motion. These values map scene positions; they do
not change limits stored inside a toy. Stock Intiface definitions for TheHandy
do not advertise native Oscillate or an onboard stroke-limit setter. Use Handy's
own settings to change its physical travel limits. `Oscillate` remains a quiet
no-op when Intiface does not report that capability; the service never fakes it
with position commands.

`Play(program, loop)` uses the scene's existing `MotionDriver` and
`FunscriptPlayback`. `StopFollowing()` stops only hardware follow; the scene
source remains independent. An already accepted timed move may finish after
following stops. Funscript segment IDs change across loops and seeks so a
repeated action pair is sent again.

## Ramps and scene controls

`RampVibration(target, seconds)` and `RampOscillation(target, seconds)` start
scene-time ramps. The `Async` forms complete when the transition finishes or a
newer command replaces it. Completion does not claim measured physical travel.

`PerformerControlSurface.Value01` is the normalized value of a visible scene
lever or knob. `BindVibration(control)` or `BindOscillation(control)` makes that
value drive the corresponding output; either accepts an optional capability
channel. `UnbindControl(control)` removes the binding and zeros outputs it still
owns. A newer explicit command takes precedence and prevents the old control
from resuming output. Lara reach, IK, and grip animation remain separate work.

## Existing in-game test panel

The toy section stays inside the existing First Performance Void window. It
contains connection and scanning controls, one stop button, device capability
summaries, separate vibration and oscillation sliders, Funscript source controls,
scene range controls, a timed move, and a visible lever binding demonstration.
The default channel is All. The collapsed Diagnostics section shows advertised
feature descriptions/ranges and motion/output state. It does not expose a device
router or author-facing assignment objects.

For Handy, the panel labels its output as timed position. The sine source does
not produce Handy timed commands. A device without a reported vibration or
oscillation output has its corresponding panel slider disabled while the scene
API remains a quiet no-op.

## Transport and dependency

Each output feature keeps one active request and one replaceable pending value.
Each device has its own timing scheduler; the scheduler honors the advertised
`MessageTimingGap` with a 50 ms conservative minimum and releases its gate before
waiting for the output acknowledgement. Ordinary output and broad Stop share a
separate short transport submit-order gate. That gate and the pacing gate are
released before waiting for hardware acknowledgement, and Stop bypasses normal
output pacing. A stalled feature does not serialize other devices. Stop and
disconnect invalidate work that has not been sent; hardware movement already
accepted by a device may finish.

The embedded Buttplug C# runtime remains the source-pinned 5.0.1 Unity rebuild.
The exact Unity editor, Roslyn SDK, Newtonsoft.Json package and binary checksums
are recorded in the package README and enforced by
`scripts/build-buttplug-unity.ps1`.

## Validation status

T0 toy integration was physically accepted on real hardware by the user. Testing
verified Intiface connection and device discovery, command delivery to compatible
connected hardware, Funscript timed motion, generic vibration and oscillation
where supported, and manual Stop behavior. The supported T0 capability paths
were verified against real connected hardware as available; the current T0
hardware vocabulary is accepted.

This acceptance does not claim every device supports every capability or identify
a particular device, firmware, Bluetooth chipset, latency, or Intiface build.
Automatic routing remains capability driven, and hardware remains optional. A
timed movement already accepted by a device may finish according to that device's
semantics. Sampled motion is not converted into fabricated timed-position
commands. `PerformerControlSurface` can animate and bind toy output independently;
Lara physically reaching for or manipulating that surface is deferred work.
