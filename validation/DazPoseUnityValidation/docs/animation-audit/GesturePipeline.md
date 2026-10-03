# Performer Gesture pipeline

`SuccubusPerformer.Gesture` plays one finite Generic animation as an additive upper-body action. It overlays the current body source, including locomotion and seating, and does not update the performer GameObject root or persistent `DesiredPose`/`SettledPose` state.

## Generated assets

Open `Assets/Scenes/FirstPerformanceVoid.unity` in Edit Mode and run:

```text
Tools > DAZ Pose > Gesture > Generate Gesture Acceptance Assets
```

The command requires exactly one `SuccubusPerformer` referenced by the scene controls, a root `Animator`, one Genesis 8 `chestLower` transform, and unique `rShldrBend`, `rForearmBend`, and `rHand` transforms. It creates or updates:

```text
Assets/DazPose/Generated/Gestures/PerformerUpperBodyGesture.mask
Assets/DazPose/Generated/Gestures/Acceptance/RightHandWave_Gesture.anim
Assets/DazPose/Generated/Gestures/Acceptance/RightHandWave.asset
```

The mask is built from the actual `SkinnedMeshRenderer.bones` beneath `chestLower`. Its active Transform paths are exactly that skeletal subtree; all Humanoid body-part flags are disabled. The Wave has rotation curves only for the three named right-arm bones, lasts 1.4 seconds, does not loop, and uses its own frame zero as the additive reference. The setup command assigns the mask to the performer and the Wave to the existing First Performance Void panel and acceptance harness. It saves those references without changing camera or environment settings.

## Baking another installed clip

Select one compatible Generic `AnimationClip` in the Project window while a scene with exactly one Lara performer is active, then run:

```text
Tools > DAZ Pose > Gesture > Bake Selected Clip as Performer Gesture
```

The baker copies only supported Transform curves whose paths resolve to skeletal transforms under `chestLower`. Root, pelvis, leg, component, blend-shape, object-reference, and event data are excluded. The output is non-looping, uses frame zero as its additive reference, and is saved as `<SourceName>_Gesture.anim` plus `<SourceName>.asset` under `Assets/DazPose/Generated/Gestures/`. The selected source clip is read-only; the baker does not retarget Humanoid motion.

## Runtime composition

The graph order is:

```text
body pose / locomotion -> seating -> Gesture -> breathing -> gaze -> expression -> blink -> Animator
```

Gesture uses two additive, identically masked clip slots so a new action can crossfade from the active clip's current sample. An action's effective blend-in and blend-out are each clamped to at most 45% of its duration. Repeating an active Gesture starts it again from frame zero and completes the old waiter as `Superseded`; gestures are never queued. Disabling the performer completes the active waiter as `PerformerDisabled` and runtime recreation does not restore a finite action.

New Gesture requests require a ready runtime, a ready asset, a valid generated mask, and stable `Visible` visibility. They are otherwise independent of locomotion, turning, and seating. A running Gesture continues through `DissolveTo`, `DissolveOut`, and `DissolveIn`; those visibility states reject new requests while the performer is dissolving or hidden.

If the mask is absent or invalid, the Gesture layer is not inserted, preserving the prior no-Gesture graph output. Actor-root position and rotation remain owned by locomotion, seating, teleport, or dissolve systems.

## Manual acceptance

After generating the assets, enter Play Mode in FirstPerformanceVoid. Use the `P0.GESTURE — ADDITIVE UPPER BODY` panel:

1. Click `GESTURE: WAVE`, then `GESTURE: WAVE ASYNC`.
2. Click `WAVE TWICE / SUPERSEDE`; the first request should report `Superseded`, then the restarted Wave should finish.
3. Try `WALK + WAVE`, `TURN + WAVE`, `SEATED + WAVE`, `LOOK + WAVE`, `EXPRESSION + WAVE`, and `SAY + WAVE`.
4. At a teleport mark while standing still, try `DISSOLVE TO + WAVE` and `OUT WHILE WAVING`.
5. Check the displayed Gesture progress/status and Visibility, Locomotion, and Seating states. A standalone Wave should leave Lara's actor root and lower body fixed, preserve the persistent Pose, and fade back to it.
6. Run the existing acceptance checks with F5 to exercise natural completion, same-asset restart, disable completion, status reset, root immutability, and persistent-state preservation.
