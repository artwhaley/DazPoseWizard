# Directable Player View

`FirstPerformanceVoid` uses this hierarchy:

```text
FirstPerformanceVoid
└── Player
    ├── PlayerController
    ├── PlayerView
    └── ViewRig
        └── HeadPose
            └── MainCamera
```

`Player` owns the world position. `ViewRig` owns the director-controlled world orientation. `HeadPose` is the future tracked-head seam; P0.D never writes its local position or rotation. `MainCamera` remains local identity and keeps its existing `Camera`, `AudioListener`, and HDRP camera data.

`PlayerController` is the public director facade. `PlayerView` implements two independent latest-wins channels: position (`MoveTo`) and orientation (`LookAt` or `Track`). A position command snapshots a Transform's world position when issued and ignores its rotation. A finite `LookAt(Transform)` keeps reading the target while turning, then freezes at its final heading. `LookAt(Vector3)` aims at a fixed world point. `Track(Transform)` acquires smoothly and follows continuously. `StopTracking()` freezes the current `ViewRig` rotation exactly.

The common overloads take gameplay seconds directly:

```csharp
player.Track(lara.FaceViewTarget, 1f);
player.MoveTo(viewMarkLounge, 4f);
await player.LookAtAsync(lara.FaceViewTarget, 1f);
```

Move defaults to 2 seconds; finite LookAt and track acquisition default to 0.75 seconds. The continuous tracking response defaults to 0.20 seconds. Float overloads use a cubic smoothstep curve. Zero duration snaps immediately; negative, NaN, and infinite durations throw. Advanced callers can pass a `ViewTransition` containing a duration and custom `AnimationCurve`; the sampled weight is clamped to 0..1.

While a channel is active, its next command replaces it from the current transform state; the previous asynchronous command completes as `Superseded`. Position and orientation can run together. Finite commands complete as `Completed`, `Superseded`, `PlayerDisabled`, or `TargetLost`. Disabling the controller or view cancels active finite commands. Destroying a finite LookAt target holds the view and completes with `TargetLost`; a destroyed Track target holds the view and emits one warning.

## Migrate the Existing Lounge

Open `Assets/Scenes/FirstPerformanceVoid.unity` in Unity, then run **Tools > DAZ Pose > First Performance Void > Migrate to Directable Player View** once. The migration uses the currently loaded scene and reparents its existing `MainCamera`; it does not call the room builder. It preserves and checks the camera's world position, world rotation, and world scale, retains the existing camera and HDRP components, adds a Player hierarchy, creates missing view markers and Lara's head child `FaceViewTarget`, points Lara's existing camera-gaze target at `HeadPose`, and saves only the lounge scene. Existing marker positions are left in place if already authored.

The normal builder also creates this hierarchy and the markers for a newly constructed lounge. It returns without rebuilding an existing lounge.

## Manual Check

Enter Play Mode. Confirm the first frame matches the camera framing you had before migration. In the `First Performance Void` panel, press **Move Wide**, **Move Lara**, and **Move Lounge**; then press **Look At Lara** and verify the view holds after the turn. Press **Track Lara**, move or seat Lara, and verify the camera follows. While tracking, move to Lounge and confirm movement and tracking happen together. Press **Stop Tracking** while Lara is moving and confirm the view freezes. Use **Look at camera** and check mutual gaze. Finally, speak while seated and confirm the existing spatial audio still works.
