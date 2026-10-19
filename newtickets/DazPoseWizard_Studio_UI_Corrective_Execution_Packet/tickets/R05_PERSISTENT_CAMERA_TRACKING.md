# R05 — Persistent performer tracking in isolated Player-camera preview

**Priority:** P1. **Targets:** `ScenePreviewSession.cs`, `SceneDirectorStudioWindow.Rehearsal.cs`, targeted tests.

## Verified defect
`TrackLara()` computes a camera rotation once. Subsequent `Arrival()` or pose sampling does not update tracking, so First Contact rehearsal camera can look away as Lara moves.

## Design decisions
1. Preview stores a camera mode/target: `Camera.StartAt` defines the authored starting frame; `Camera.Track(Lara)` activates continuous tracking of the existing cloned performer, not a one-shot LookRotation. Keep orbit view independent from player view.
2. Tracking **re-evaluates on preview actor transform changes**, pose sample, scene mark arrival/commit-preview and Player-camera render. Maintain the camera's intended position/offset from the authored scene/controller; rotate to the correct Lara focus/head target using the actual saved `laraFocus` when mapped, with a documented fallback. Do not continually reset the shot to world origin or turn the preview camera into a free-flying tracker.
3. `Camera.StartAt` or a future stop/track-off command must apply the runtime vocabulary's state transition, not retain stale tracking. If the current vocabulary lacks stop tracking, do not invent extra user-authored commands here.
4. Preview should not drive the actual scene Camera, `Player.View` component or any persistent actor transform. Use the existing preview clone/camera and explicit setup/trace state.
5. Reconcile `PreviewRenderUtility` camera mode, optional `SourceCamera` and `Camera.Track` frames without temporarily enabling/modifying real camera components.
6. Keep advertised preview limitations (volumes, particles, grip solver, exact effects) explicit; this ticket fixes tracking, not HDRP post-processing fidelity.

## Tests
T17–T18: run Camera.StartAt → Track → Walk/Dissolve to two destinations; the Player view remains aimed at Lara at each. Orbit view remains independently controllable. Pose sample does not break tracking. Native visual confirmation is user-pending, not inferred from math alone.

**Done:** preview camera tracks every staged movement while a track command is active and preserves the scene's camera ownership.
