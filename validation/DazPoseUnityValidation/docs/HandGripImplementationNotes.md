# Procedural Hand Grip Foundation

## Implementation

The grip layer is a source-neutral `AnimationScriptPlayable` inserted into the performer graph after Gesture and before Breathing. Its job reads the current animation-stream wrist transform, evaluates contact against an `IGripTarget`, and writes only the configured finger joint rotations. Existing motion, gesture, breathing, and full-body ownership stays in the surrounding graph. Grip and release blend over the profile's configured durations.

The initial profile targets Lara's right hand, resolved from the imported rig's `rHand` hierarchy: thumb, index, middle, ring, and pinky joints. Each digit has four calibrated contact probes. `HandGripRigProfile` stores the rig paths, open and closed local rotations, grip-center transform, probe radii, 3 mm clearance, near-contact distance, blend defaults, and bounded solver iteration count. The acceptance defaults use seven binary-search iterations and 0.25 seconds for grip and release.

`GripContactRod` is the analytic acceptance target. It derives a stable world-space frame from its endpoint transforms and reference normal, clamps its longitudinal sample to `[0, 1]`, and reports malformed geometry instead of emitting an invalid frame. The solver projects each probe onto the rod axis, classifies clear, near-contact, and penetration states, and searches each digit's curl independently. A base pose already inside the allowed contact volume is reported without applying extra curl.

`HandGripCalibrationWindow` discovers the right-hand digit hierarchy, records the open pose, proposes closed local rotations and probe locations, and provides per-digit preview and capture controls. Its generated closed rotations are a calibration starting point; they still need visual approval on the acceptance scene. Temporary previews are restored when the window closes.

## Acceptance workflow

1. Open the Unity validation project and run **Tools → DAZ Pose → HandGrip → Run Geometry and Solver Tests**.
2. Run **Tools → DAZ Pose → HandGrip → Create Isolated Acceptance Scene**. This duplicates `FirstPerformanceVoid.unity` to `HandGripAcceptance.unity`, configures the generated right-hand profile and a rod aligned to the existing Motion clip's grip-center travel, and adds the runtime acceptance controls to the duplicate.
3. Open `HandGripAcceptance.unity` and enter Play Mode. Use its controls to start, stop, and restart Motion; grip and release; and compare rod radii and tilt. The diagnostics expose per-digit probe states, curl amounts, target alignment, angular alignment, and roll error.

The setup command saves only the acceptance scene and the profile at `Assets/DazPose/Generated/HandGrip/Profiles/LaraRightHandGrip.asset`; it does not save changes into the source scene. The setup estimates the rod travel from the first configured motion clip. Final artistic alignment and whether all five digits reach the intended grip remain visual acceptance decisions.

## Verification state

Unity 6000.5.9f1 completed a batch asset import and compiled the runtime and editor assemblies in an isolated mirror. The geometry and solver menu action passed there, and the scene builder logged `HANDGRIP_ACCEPTANCE_READY` with the expected right-hand paths and defaults. The generated scene and profile were copied into this project. The shared project was already open in other Unity processes, so the generated files have not yet been imported by that editor instance.

Play Mode inspection and user visual approval remain pending. The generated closed-pose suggestions and rod alignment are starting points; final hand shape, contact appearance, alignment through the full Motion stroke, and radius/tilt comparisons need review in `HandGripAcceptance.unity`.
