# P0.F — Teleport acceptance

## Install in Edit Mode

Open the current `FirstPerformanceVoid` scene and run:

**Tools > DAZ Pose > First Performance Void > Install Teleport Acceptance Harness**

This focused, repeatable installer creates the HDRP particle materials, magenta teleport prefab, and `FirstContactTeleportProfile` only when missing; wires the profile to the existing `SuccubusPerformer`; and creates `PerformanceMarkers/TeleportAcceptance/TeleportMark_A` and `TeleportMark_B` only when missing. A starts at Lara's current root position and orientation. B uses the existing floor-level Walk across Floor location when it is at least 3m away, otherwise a 5m forward floor point. Existing markers keep their tuning on reruns. The command saves only the loaded lounge scene and these new teleport assets. It does not run the room builder, migration, or First Contact installer.

The command copies the existing smoke harness `poseB` into the arrival-pose test field (falling back to poseA, then poseC if needed). The selected pose is the existing **Vintage Glamour Genesis 8 Female 22** in the current scene. No pose or expression asset is authored.

The project already contains the user-added `Assets/teleportsound.mp3`. The installer imports it and assigns it to both spatial teleport cues when available. Audio is optional: if the AudioClip cannot be imported or is removed, the visual and teleport behavior still work silently. The profile starts at 0.15s to hide, 0.25s to reveal, and 0.45s to complete. Rerunning setup does not replace existing profile timings, asset assignments, or marker positions.

## APIs

```csharp
Lara.TeleportTo(Vector3 worldPosition);
Lara.TeleportTo(Transform target);
Lara.TeleportTo(Vector3 worldPosition, PerformerPose arrivalPose);
Lara.TeleportTo(Transform target, PerformerPose arrivalPose);

await Lara.TeleportToAsync(Vector3 worldPosition);
await Lara.TeleportToAsync(Transform target);
await Lara.TeleportToAsync(Vector3 worldPosition, PerformerPose arrivalPose);
await Lara.TeleportToAsync(Transform target, PerformerPose arrivalPose);
```

The `Vector3` form preserves Lara's facing. The `Transform` form snapshots its position and planar forward when requested. An arrival pose calls the existing `Pose(arrivalPose, PoseTransition.Snap)` while every descendant Renderer is force-hidden, then waits through a later animation frame before restoring each Renderer to its exact previous `forceRenderingOff` value. There is no temporary teleport body layer. Gaze, expression, breath, blink, attention, and speech runtimes stay enabled. Only standing performers may teleport; locomotion, seated states, and overlapping teleports are rejected. Speech and persistent pose transitions are allowed.

Departure and arrival effects are instantiated as independent world-space objects at the old and new root. The prefabricated burst combines a white-hot magenta core, upward/downward violet sparks, an expanding floor ring, and residual motes. Teleport audio uses short-lived independent 3D AudioSources, never the speech source. Disabling the performer restores renderer flags, destroys active teleport VFX/audio, and completes the waiter with `PerformerDisabled`.

## Manual tests in Play Mode

The existing room control panel adds three P0.F buttons. These tests do not change First Contact choreography.

### A — ordinary teleport

Start with Lara visible and standing near mark A. Click **TELEPORT A → B**. Confirm the magenta flash conceals her, there is no visible travel, and she appears at B with her previous body pose. Then click **TELEPORT B → A**. Confirm the reverse and check that gaze/expression/ambient movement continues.

### B — persistent arrival pose

Use the smoke harness to set a visibly different standing pose (Pose A), then click **TELEPORT B + ARRIVAL POSE**. Lara should disappear fully, snap to B, and appear already in Vintage Glamour 22. Check `DesiredPose` and `SettledPose` in the smoke harness; both should name the selected arrival pose. Wait until the flash particles finish and confirm the pose remains.

### C — independent expression and gaze

Set an expression and gaze target in the existing smoke harness, then use an ordinary teleport button. Confirm both remain active after arrival.

### D — disable lifecycle

Start a teleport, then disable only the `SuccubusPerformer` component in the Inspector before it completes. The asynchronous panel status should report `PerformerDisabled`, all previously visible renderers must become visible again, and the transient teleport objects must clean up.

The target completion is about 0.45 gameplay seconds; particle tails may continue after arrival. If the effect reads poorly in the room, tune its prefab materials/particle modules in the Inspector; rerunning installation preserves an existing prefab and its manual tuning.
