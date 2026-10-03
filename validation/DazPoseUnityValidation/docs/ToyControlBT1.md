# Toy Control BT.1

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

BT.2 and later work must wait until that manual gate has been completed.

## Connection regression verification — 2026-10-03

The original NuGet binary reproduced `MissingMethodException` for
`Newtonsoft.Json.Linq.JToken.ToString(Newtonsoft.Json.Formatting)` under Unity's
Mono runtime. The rebuilt library passed an isolated Unity 6000.5.9f1 run using
the real `ToyControlService`: connect to the running local Intiface server, start
scanning, stop scanning, stop all, disconnect, and confirm registry cleanup.
The offline capability/range/registry checks also passed. Intiface reported zero
connected devices, so physical feature discovery and hardware stop behavior
still require the manual gate above.
