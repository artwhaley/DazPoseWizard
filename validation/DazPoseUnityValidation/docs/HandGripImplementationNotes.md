# Target-driven HandGrip

## Ownership

`MotionDriver` remains unchanged: it publishes normalized position/phase/velocity/direction/time. `IGripTarget.Evaluate(Position01)` supplies physical center, tangent, outward normal and radius. The performer interprets those values spatially, never as a clip lookup.

Effective graph order:

Persistent Pose → existing Locomotion → Seating → Perform → Motion → Gesture → Bone Breathing → **Animation Rigging Two Bone IK** → **Finger Grip** → Gaze (head/eyes) → Expression (face only) → Blink.

The released `com.unity.animation.rigging` **1.4.1** job is invoked from a wrapper AnimationScriptPlayable inside the existing performer graph. There is no RigBuilder, second graph, Animator, LateUpdate bone rewrite or physics contact solver. A custom stream marker detects FingerGrip evaluation without its upstream IK job. Expression metadata on a grip-enabled performer is rejected if it attempts to affect transforms outside the head subtree.

## Palm geometry

Profile: `Assets/DazPose/Generated/HandGrip/Profiles/LaraRightHandGrip.asset`.

The calibrated PalmAnchor is a contact-face pose relative to rHand, not a cylinder-axis point:

`desiredPalm = frame.Center + frame.Normal * (frame.Radius + PalmClearance)`.

The desired wrist is reconstructed by inverting the hand→anchor transform. Orientation uses orthogonal frame axes plus calibration; wrist twist is premultiplied about the world-space target tangent. Increasing radius therefore moves the wrist and palm, not merely the contact threshold.

The calibration window exposes five quaternion curl previews and Scene View position/rotation handles for PalmAnchor. Raw runtime digit sliders disable contact and operate only on finger joints.

## Runtime API

`SuccubusPerformer.GripAsync(IGripTarget)` returns `Awaitable<GripCompletion>`; `Grip(target)` starts without awaiting; `ReleaseGrip()` blends out. T0 supports Lara's right hand. Diagnostics include state, target, normalized position, required locomotion, position/rotation error, reach fraction and explicit failure reason.

States: Idle / Planning / Walking / Acquiring / Active / Releasing / Failed.

Planning measures the actual upper/forearm lengths and checks a 0.20–0.95 arm-length envelope (preferred 0.80), including unequal-bone minimum distance. Already reachable stances win. Otherwise a deterministic 13×7×5 candidate search varies arm-scaled distance/lateral offset and yaw on the target-normal side. Seventeen physical wrist samples are tested. For the straight, constant-radius/orientation GripContactRod, endpoint maximum and closest-point segment minimum certify the full span. Other arbitrary targets are sampled, not continuously certified.

Walking uses the existing locomotion facing overload. IK/fingers remain off; MotionDriver is never stopped or sought by the interaction. Arrival replans before acquiring the current source position. Replans are bounded. Unsafe active targets release, reposition and reacquire. Requests supersede deterministically; generation checks prevent stale walk callbacks from completing newer grips. Release leaves the actor at its current stance.

## Contact

All five digits use three controlled joints and four virtual probes. First-joint parents include rCarpal1/2/3/4. Candidate FK reads the real parent pose and local offsets from the post-IK stream; it does not mutate scene transforms. Per-digit 32-step first-collision bracketing plus ten bisection iterations avoid accepting a closed endpoint that exited the cylinder's far side. Open-pose penetration is explicitly BasePenetration.

Finite probes and finite bracketing are an approximation of skin geometry and a continuous curl path, not mesh collision certification. Visual mesh inspection remains required.

## Acceptance

Open `Assets/Scenes/HandGripAcceptance.unity`. The existing panel hosts raw digit sliders, grip/release, radius, tilt, near/far/impossible target placement, normalized-position controls and wrist twist. Source scene FirstPerformanceVoid is not edited by this stack. The acceptance setup no longer derives rod placement from a Motion clip.

Automated isolated-project entry points:

- `DazPose.Editor.HandGrip.HandGripValidationRunner.BatchCalibration`: 52 geometry/palm/reach/contact assertions, actual Lara graph/contact proof, tilt/twist and existing expression regression checks.
- `DazPose.Editor.HandGrip.HandGripLiveProof.Run`: public API Play Mode near/far/replan/release/impossible, sine, Funscript seek/continuous/stop/resume.
- `DazPose.Editor.HandGrip.HandGripVisualProof.Run`: isolated open/closed/per-digit render evidence.

These batch entry points refuse to take over the interactive project. See TargetDrivenGripExecution.md for baseline, ticket caveats and current verification status.

## Future backend seam

`PerformerArmIKLayer` is the isolated IK backend. Final IK was not needed for the tested single-arm T0. Whole-body compensation, shoulder shrug, multiple effectors and foot anchoring are not implemented; those can replace this backend without changing target/planner/source ownership.
