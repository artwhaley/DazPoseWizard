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

## Camera mounts

A player body prefab can carry `PlayerCameraMounts`: two child transforms, High and Low, above the head. `Camera.StartAt(High|Low)` calls `PlayerCameraMounts.PlaceView`, which uses `PlayerView.ResetPerformanceView` to put the `Player` root and `ViewRig` on the mount pose and cancel any move or tracking. `HeadPose` is untouched. The mounts follow the body, so place the body first. Create them once with **DazPose > Player > Add Camera Mounts to PlayerBody**.

## Historical migration and validation

The one-use lounge migration and old Tools menu/Play-panel recipes are retired. The runtime API above remains the reference; do not recreate a fixture or rerun migration. Art checks the retained scenes and current Studio manually. The implementation now lives under `Assets/DazPose/Player/Runtime/PlayerView.cs`.
