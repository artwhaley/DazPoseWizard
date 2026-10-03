# Toy Control Stack BT.1–BT.6

BT.1 uses Buttplug C# 5.0.1 rebuilt from its official source against Unity's Newtonsoft.Json runtime in the embedded `com.dazpose.buttplug-csharp` package. The upstream NuGet binary requires a JSON method missing from Unity's bundled version and faults during connection; rebuilding selects the compatible overload. The current OpenUPM Buttplug Unity 4.0.0 package bundles an older API that lacks the v4 feature output model, including `OutputType.HwPositionWithDuration`. The embedded package preserves the upstream license, records the assembly checksum in its README, and can be rebuilt with `scripts/build-buttplug-unity.ps1`.

The scene-level `ToyControl` service remains separate from Lara and MotionDriver. BT.1 diagnostics are part of the existing First Performance Void runtime panel; no additional test window is created.

## Offline registry checks

In Unity, run **Tools > DAZ Pose > Toys > Run BT.1 Offline Registry Tests**. These checks cover the four projected output capabilities, output ranges, per-feature separation, empty-feature snapshots, and registry add/remove/clear behavior. They do not require an Intiface server or device.

## Intiface manual gate

1. Start Intiface Central and enable its Buttplug server. The default address is `ws://127.0.0.1:12345`; edit the address in the existing First Performance Void panel if the server uses another endpoint.
2. Open `Assets/Scenes/FirstPerformanceVoid.unity`, enter Play Mode, and use the **TOYS / INTIFACE — BT.1** section in the existing First Performance Void controls.
3. Connect, start scanning, and add an Intiface simulated device or a compatible physical device.
4. Confirm device name/index, each separate feature index/description, and all advertised supported output ranges appear. For `HwPositionWithDuration`, check both position and duration ranges.
5. Remove a device and confirm the registry entry disappears. Use **STOP ALL** and confirm no error is reported.
6. Stop or close the Intiface server and confirm the panel reports a fault and clears connected-device availability.

The BT.1 connection result was manually accepted before the later stages were wired.

## Connection regression verification — 2026-10-03

The original NuGet binary reproduced `MissingMethodException` for
`Newtonsoft.Json.Linq.JToken.ToString(Newtonsoft.Json.Formatting)` under Unity's
Mono runtime. The rebuilt library passed an isolated Unity 6000.5.9f1 run using
the real `ToyControlService`: connect to the running local Intiface server, start
scanning, stop scanning, stop all, disconnect, and confirm registry cleanup.
The offline capability/range/registry checks also passed. Intiface reported zero
connected devices, so physical feature discovery and hardware stop behavior
still require the manual gate above.

## BT.2–BT.6 implementation

The runtime supports generic output routing for the four capabilities in this
sprint: `Vibrate`, `Oscillate`, `Position`, and `HwPositionWithDuration`. It
does not branch on device names or brands. Discovery alone never starts an
output; Level and Motion assignments are explicit runtime actions and are not
persisted across sessions.

`ToyControlService.SetLevel(float)` and `SetLevelAsync(float)` set a clamped
normalized Level. Assignments target individual Vibrate/Oscillate features and
use Buttplug's percentage command builders. Each feature reports independently
so one unavailable output does not block the rest. NaN and infinity are
rejected. Rapid slider updates collapse into a bounded latest-value pump.
`PlayerController.Toys` exposes the service without tying toy output to Lara or
her animation.

Motion assignments use `ToyOutputBinding` and `ToyMotionBinding`. Followers
read the existing `MotionDriver`; they do not create another player or clock.
`MotionTargetSegment` adapts the active Funscript segment at the MotionDriver
boundary, keeping Buttplug code independent of Funscript types. Auto prefers
`HwPositionWithDuration` when a current segment is available and otherwise
uses `Position` when supported. Position-only features consume normalized
samples. Duration-only features consume remaining segment time when available;
for a source without segment data they receive bounded target updates using
the conservative send interval.

Continuous output keeps only the latest pending value. The device's advertised
`MessageTimingGap` is enforced, with 50 ms as the minimum/default, and changes
smaller than 0.005 are suppressed. Motion ranges map through optional minimum,
maximum, and inversion settings. A bind or seek sends the current target with
the remaining Funscript segment time; it does not replay skipped segments.
Pause stops future motion sends and attempts a generic position hold or device
stop. Resume synchronizes from the current MotionDriver state. A device-managed
move may finish if the device does not support direct Position.

`StopAllAsync` invalidates queued Level and Motion requests and waits for any
already-dispatched output before issuing Buttplug's device-wide stop. This
prevents an older queued request from moving a device after STOP ALL. Disconnect
and shutdown also invalidate queued outputs and perform best-effort
stop/disconnect work. Device removal makes runtime bindings unavailable; a
later feature with the same runtime device/feature tuple can be used again, but
assignments are not saved across sessions.

The existing First Performance Void controls contain the TOYS / INTIFACE
section for BT.1–BT.5 and the BT.6 control acceptance buttons. No separate test
window is created. Connect and scan stay non-blocking. Each listed supported
feature has a raw bench command, and Level and Motion assignments are toggled
by role. The panel displays service state, device feature ranges, outgoing
Level report, selected motion strategy, current/mapped/last positions, duration,
send gap, pending state, generation, and the last error. STOP ALL remains
available when no assignments exist.

For BT.6, `PerformerControlSurface.Value01` is independent visual state and
`LaraControlAnimator.SetControlAsync(surface, value01)` animates a reach, lever
movement, and return. It does not call the toy service. In Play Mode, the
existing panel creates a small acceptance lever near Lara's `rHand` when it
finds the performer; its buttons run `.30 → .66 → .30 → 1.00`. Choreography can
call toy output and this animation separately.

## BT.2–BT.6 verification

The Unity 6000.5.9f1 stack acceptance harness passed against the current source.
It verifies normalized level routing and clamping, NaN rejection, feature-level
failure isolation, Position coalescing and mapping, Funscript duration segment
commands, remaining-duration seek and resume, pause, STOP ALL cancellation of
queued fan-out, and normalized Lara control endpoints and return pose. The
existing First Performance Void controls and runtime stack also compile against
the Unity project references. Unity emitted only existing obsolete API and
unused fake-event warnings.

The real Intiface transport check passed for connect, scan start/stop, StopAll,
disconnect, and registry cleanup. Intiface had no connected feature available
during that check, so physical Vibrate/Oscillate/Position/PositionWithDuration
commands and live Lara animation in the scene have not been hardware/manual
verified. Use the existing First Performance Void panel with Intiface simulated
devices or compatible devices to complete those physical acceptance checks.
The automated checks use a fake backend and do not claim to prove actuator
behavior.

Only the four scoped output capabilities are routed. Rotate, Constrict, Spray,
Temperature, LED, input sensors, direct Bluetooth, vendor APIs, and XToys are
not implemented. The Unity project embeds Buttplug C# 5.0.1 from official
source, rebuilt against Unity's bundled Newtonsoft.Json runtime; the current
official Buttplug Unity wrapper exposes an older feature API and is not used.
