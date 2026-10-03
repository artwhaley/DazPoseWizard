# Toy Control BT.1

BT.1 adds the official Buttplug Unity 4.0.0 package and a scene-level `ToyControl` service. The BT.1 diagnostics are part of the existing First Performance Void runtime panel; no additional test window is created.

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
