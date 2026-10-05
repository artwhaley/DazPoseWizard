# Target-driven grip execution

## Baseline / concurrent-work boundary

Baseline HEAD: `ae7a6d7a8d6972994c1daa7f6b6c642964dd825d`, main ahead of origin by two commits. Existing HandGrip foundation is committed in `72c2c8a`; acceptance setup/harness/scene/docs and SuccubusPerformer already have local modifications. Other local work includes wardrobe, anatomy, dissolve and magic. Preserve all of it. Do not stage pre-existing unrelated hunks. Git needs a command-local safe.directory exception because the checkout belongs to another Windows account; no global configuration was changed.

Unity 6000.5.9f1 is running interactively on this project. Another agent owns the p0c-native-generation validation project. Do not launch a second editor on either project or terminate those processes.

## Implementation map (before changes)

- Generic performer owns one GameTime PlayableGraph and one AnimationPlayableOutput.
- Persistent Pose → Locomotion body mixer → Seating → Action/Perform → masked Motion → Gesture → HandGrip finger job → Bone Breathing → Gaze (head/eyes only) → Expression → Blink.
- HandGrip controller samples MotionDriver.Position01 and evaluates IGripTarget, but does not place the arm. Its job reads the hand from the animation stream, solves candidate FK, then writes finger local rotations.
- Profile: Assets/DazPose/Generated/HandGrip/Profiles/LaraRightHandGrip.asset. Five digits, three controlled joints and four virtual probes per digit, seven binary iterations.
- Finger profile enum mapping is Thumb/Index/Middle/Ring/Little, bone prefixes rThumb/rIndex/rMid/rRing/rPinky. No enumeration mismatch found.
- Index, middle, ring and little have an uncontrolled rCarpal1/2/3/4 parent between hand and first controlled joint. Existing candidate FK incorrectly starts all first joints directly at hand. Intermediate arm twist bones also exist; arm IK must support this actual chain, not demand direct parenthood.
- Calibration compares positive/negative tip distances to an in-plane palm center. This cannot establish inward versus outward reliably. Existing index/little closed endpoints have opposite signs to middle/ring despite similar rest directions. The user's first/fourth observed digit order is not enough to identify anatomical digits; do not claim that mapping without a visual check.
- Current GripCenter is an in-plane point and identity orientation, not a surface pose. Radius only affects contact threshold, not wrist position.
- Acceptance setup explicitly samples the authored Motion clip to place the rod. This is the defective test path, not evidence of spatial grip.
- Runtime has no LateUpdate bone writes; editor calibration preview temporarily changes transforms and restores them.
- Existing locomotion facing overload returns Arrived/Superseded/PerformerDisabled; near-equal requests coalesce waiters, replacements deterministically resolve old waiters. Final alignment settles root/facing. Grip must use this runtime, not root writes.

## Required final order

Persistent Pose → Locomotion → Seating → Perform → Motion → Gesture → Bone Breathing → Arm IK → Finger Grip → head/face-only Gaze → Expression → Blink.

All shoulder ancestors are upstream of IK. Finger candidates must read their real parent poses and offsets from the post-IK stream. MotionDriver remains unchanged and source-neutral.

## Ticket deficiencies / guardrails

1. Five reach samples do not prove an arbitrary nonlinear IGripTarget's entire span. They suffice for maximum reach on a straight rod with constant orientation/radius; minimum reach also requires checking the closest point on that wrist segment. General targets need a sampling/error-bound contract or conservative denser checks.
2. Binary search assumes a valid-to-invalid interval. Quaternion curl paths can enter and leave a cylinder. Bracket the first collision with bounded sampling before bisection; do not accept an endpoint just because it exits the far side.
3. Finger validity must describe the rotations actually applied during blending. If the inherited open pose is already penetrating, report BasePenetration rather than fabricating valid contact.
4. A positional reach envelope does not prove wrist orientation feasibility, obstacle clearance or walkability. T0 uses unobstructed planar floor; report orientation convergence separately.
5. Raw finger visual acceptance must precede claims about Lara contact. Synthetic tests alone do not prove the skin curls inward.
6. No backend/model-switch or subagent tools are exposed in this executor. Work remains single-writer; no parallel source edits.

## Acceptance status

Implementation and verification in progress. No Unity compilation, visual finger, spatial IK, radius, walk/reacquire, sine or funscript acceptance is claimed by this document until measured results are recorded.
