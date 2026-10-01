# Simple Lara Humanoid / KAWAII test

Run **Tools > DAZ Pose > Animation Audit > Create or Refresh Simple Humanoid Test**, then press Play.

The command creates and opens `Assets/Scenes/LaraHumanoidKawaiiTest.unity`. Re-running it replaces this generated test scene and refreshes its dedicated controller. Unity offers to save any currently modified scene before switching.

## Inputs

- User-configured model and imported Avatar: `Assets/TestCharacter/laraHumanoid.fbx`.
- Source motion: `Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_Idle01_breathing.FBX`.

Neither input's importer is rewritten. Canonical `Assets/TestCharacter/lara.fbx` is not used by this test.

## Scene and controller

The scene contains one imported Lara, a floor, a camera, and a light. The model uses its imported Humanoid Avatar and a stock Animator Controller at `Assets/DazPose/AnimationAudit/SimpleHumanoidTest/LaraHumanoidBreathing.controller`.

The controller has one layer and one default state whose motion is the original breathing idle clip. Playback starts through Unity's ordinary Animator when Play Mode starts. Speed is 1, root motion is off, mirroring is off, and foot IK is off. There are no runtime scripts, override controllers, neutral-pose clips, or pose corrections. Imported bone transforms and the model's original root rotation and scale are preserved.

## Manual observation

1. Before Play, expect the model's normal imported A-pose.
2. Press Play. Expect the breathing idle to begin directly.
3. Inspect shoulders, upper arms, elbows, hands, torso, hips, and knees. Use Scene view to inspect the front and side during playback.
4. Stop Play. The model should return to its imported A-pose.

A deformation in this scene should be investigated in the imported Avatar configuration and source Humanoid motion. This scene does not exercise the animation audit harness or performer systems. Do not add joint offsets to compensate for a bad retarget result.
