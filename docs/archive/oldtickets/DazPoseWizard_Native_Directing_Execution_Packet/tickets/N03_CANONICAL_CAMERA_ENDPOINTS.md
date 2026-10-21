# N03 — Real PlayerView camera endpoints, including persistent tracking

**Depends on:** N02. **Deliverable:** shared pure endpoint calculator and native stage camera adapter, runtime-equivalence regressions.

## Execute

1. Document exact runtime contract of `ScopedPlayer.StartAt`, `PlayerView.ResetPerformanceView`, `PlayerView.DesiredRotation` and `Camera.Track`. The real player position is `PlayerView` root; orientation is the `viewRig`; shot originates at existing `MainCamera` / `headPose` hierarchy. Do **not** rotate a guessed editor camera or set child MainCamera world position independently of its rig.
2. **Minimize runtime changes**: `PlayerView.ResetPerformanceView` already performs its endpoint transform; reuse proven safe math if possible and expose `DesiredRotation` pure tracking math only if needed. Add a pure helper if no edit-safe call path exists, then demonstrate runtime parity. Test zero-offset current rig **and** synthetic nonzero hierarchy offsets, vertical target fallback and actual camera origin. No altered runtime smoothing.
3. On `Camera.StartAt(location)`, stage the **saved player's root** at placement.position and `viewRig.rotation` at placement.rotation, ending previous tracking. Do not add new camera anchors. Game view must show that shot.
4. On `Camera.Track(Lara)`, `laraFocus` is **child of Lara head bone**. Keep its **local** authored transform unchanged; sample Lara pose first, then calculate world aim from actual shot camera origin to **post-sample focus world position**, with `DesiredRotation` math and rig child offsets. Stage-driven bone/focus motion must **not** trigger authored-anchor revision invalidation. Move an authored local focus offset separately with native handles; preserve last StartAt root.
5. Expose in chrome the current camera anchor, source cue, track target and live Game-view status. Select native camera location in Hierarchy and show authored gizmos. Stage during live drag; change to a later camera cue updates anchor according to that cue.
6. Ensure restoring the lease reverses preview root/viewRig/camera enable changes; camera projection, FOV, culling mask, HDRP and targetTexture remain original.

## Gate

- Two consecutive camera cues show distinct correct Game-view framing **without Play**.
- Camera track follows two subsequent Lara relocations and manual standing-mark drag; root stays anchored; Stop restores saved camera/root.
- Adjust camera mark via native gizmo and watch Game view update continuously; Ctrl+S stores mark change but not previewed player rig movement.
- Exact runtime helper equivalence tests pass; no new camera prefab, custom viewport or `PlayerController.Track()` Edit Mode call.
- N03's native verification is folded into **Art G2**. Executor may run only non-Unity source/managed checks and cannot claim Game view PASS before G2.
