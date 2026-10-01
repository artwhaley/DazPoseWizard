# Animation library evaluation with Lara

## Open the updated scene

Out of Play Mode, run **Tools > DAZ Pose > Animation Audit > Prepare and Open Audit Scene**.
This regenerates `Assets/Scenes/AnimationAudit.unity`, the installed clip catalog, and its dedicated Animator controller. Run this command once after updating these scripts; merely opening the old scene retains its old setup. Press Play yourself.

The scene uses one instance of your configured `Assets/TestCharacter/laraHumanoid.fbx`, its imported Avatar, and ordinary Animator states that reference the original imported clips. The proven breathing idle starts by default. The separate simple breathing test remains available.

## Browse clips

- Choose KAWAII or Animset Pro, category, and optional text search. Both installed libraries are available on the same Lara.
- Click a clip to play it immediately. **Play / Restart** restarts the selected clip.
- Use speed, Pause/Resume, Stop, repeat, and the pose-phase slider. Scrubbing pauses playback; it is for examining poses, not measuring travel.
- **Unity retarget Foot IK** switches between two stock controller variants on the same Lara and restarts the selected clip. Both variants use identical source clips and state names; only each state's Foot IK flag differs. This uses Unity's source foot targets, with no terrain raycasts, custom IK, or pose offsets. The default remains off for a controlled comparison.
- Source path, subclip, duration, import loop setting, rig type, and importer messages appear below the controls. Clips without a playable Humanoid state are listed with Play disabled.
- Right-drag over the character preview to orbit; use the wheel there to zoom. Scrolling the controls does not zoom the camera.

## First root-motion and walk test

1. Leave root motion off, speed at 1, cycles at 3, and choose walk family **01** under **Start → Walk → Stop**.
2. Watch the start, three walk cycles, and stop. Look at shoulder/arm posture, feet, and the joins between stages. Final pose is held.
3. Enable **Apply actual animation root motion**, then press **01** again to replay the complete sequence. Toggling the setting alone restarts the currently selected individual clip.
4. Compare movement against the 1-metre floor grid and the displayed root displacement, planar path, and yaw. Root motion off should hold the Animator object's position. Root motion on only moves it when the imported animation supplies movement; no scripted walking speed is added.
5. Repeat with families **02–07**. Set sequence blend to zero to inspect the raw joins, then compare with the default 0.08-second blend.
6. Select the desired WalkXX family, then use the left/right 90° or 180° turn buttons to preview turn → start → walk → stop. With no WalkXX selected, these buttons use family 01. Inspect actual heading change, not just the visible body pose.

If root motion on still produces zero travel or yaw, report that clip/family and its displayed import settings. That result needs investigation of the source motion/import settings; this scene does not fabricate displacement.

## Other families and measurements

Walk pivots and all other individual clips are available through the browser. Selecting a Speak, Sit, or Sleep family with matching Start/Loop/End clips exposes a button for that complete sequence. Missing family stages are not substituted with unrelated animations.

The seat is a visual reference, with no alignment solver. **Place at seat approach** sets a new restart position and heading; adjust the seat in Scene view before Play as needed. There is no IK or automatic placement correction.

Root measurements cover the complete run since the last restart or **Zero metrics**. Foot height and velocity are inspection aids; the planted label is a heuristic. **Save measurement** appends the clip or sequence label, root/mirror/Foot IK settings, elapsed time, displacement, path, and yaw to `docs/animation-audit/LocomotionMeasurements.csv` under the Unity project. An incompatible older CSV is preserved, with new rows written to `SingleLaraFootIKMeasurements.csv` instead. For sequences, the source-path field identifies the final stage; the measurement label identifies the whole chain.

## Recover from the old Animator graph error

Close the Animator tab displaying the former `AnimationAuditController`, keeping Unity open. Then run **Prepare and Open Audit Scene** out of Play Mode. The refreshed scene uses newly generated `LibraryPlayback` controllers. Future refreshes preserve their state nodes instead of deleting and recreating them. The old controller is retained for recovery but is no longer assigned to this scene. The updated Foot IK checkbox allows the stance test without opening an Animator graph.

This evaluates imported Humanoid playback. It does not establish the later retargeting/baking path back onto canonical Generic Lara's Genesis skeleton. Neither Lara importer nor either animation package is modified by scene preparation.
