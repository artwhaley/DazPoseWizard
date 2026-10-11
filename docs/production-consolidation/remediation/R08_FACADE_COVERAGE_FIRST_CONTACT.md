# R08 — finish the author surface and migrate existing sequence

Depends R07. Allowed seams: scoped adapters, FirstContactPerformance, read-only API coverage/report code. No all-up new sequence, interpreter or content registry.

1. Enumerate current SuccubusPerformer/PlayerController/ToyControlService public semantic methods and compare to VERBS.csv. Write `API_COVERAGE.csv` in this folder with existing method/signature, scoped method, success policy, cancellation owner, persistence policy and check ID. Exclude diagnostics/rig calibration/acceptance APIs explicitly, not by forgetting them.
2. Every existing primary semantic async overload forwards through the kernel; retain its parameter meanings/defaults. Add synchronous persistent/state equivalents LookAt/ClearGaze, Expression/ClearExpression, Aura/ClearAura, HoldGrip/FollowGripMotion, player Track/StopTracking with R07 ownership. Do not expose actor mask/bone paths. Add scoped `OverrideTuning(PerformanceTuningOverride values)` using the exact allowlist below. The DTO has nullable value-type properties matching each existing actor property; null means unchanged. BreathingCurve uses an explicit HasBreathingCurve flag plus a cloned curve. Capture original and actual post-clamp applied values, restoring only when the current value still equals the applied value. For curves, clone keyframes/wrap modes on entry and capture the actual applied curve reference plus keyframe/wrap fingerprint; restore only if both remain unchanged. Validate the complete DTO before any write, and restore previously written owned fields if an application setter throws. Apply minimum interval before maximum interval; restore the pair together only if both still belong to this override. Never expose a raw-actor callback, shared profile mutation or rig binding mutation. No runtime reflection.
3. Make coverage explicit for applicable pose/gaze/expression options, move/turn/seat, appearance/teleport, action/gesture, wardrobe, contact, magic, player view and toys/playback. A static inventory row may cite an existing regression for unchanged semantics; new wrapper dispatch gets at least one executed case per family. Do not mirror-test every forwarding overload.
4. Rewrite existing FirstContactPerformance's RunSequence to use `PerformanceRun`, ScopedPerformer/ScopedPlayer/ScopedToys and Delay. Remove expected-enum/channel/callback plumbing from that method. Keep serialized field names, timing, seating, clips and view marks unchanged. Construct/configure its typed bindings through existing saved refs when legacy binding is absent, with no scene-global lookup.
5. Keep public Run/Stop/ResetAsync behavior. Stop disposes its run; reset uses existing declared actor/player transforms/defaults/initial wardrobe and guards an active reset. No hasRun one-shot restriction. OnDisable stops exact owned work. Keep debug controls outside the production actor.

Run `-Ticket R08`: existing First Contact natural completion, stop during speech and movement, reset then replay, optional toys absent, missing required clip/seat detected, and new wrapper family coverage. No timing/pose/art changes. Assert RunSequence contains no PerformanceCommand/Observe/CaptureCancellation use.

Gate: API_COVERAGE has no unexplained missing semantic family; public authoring reference example compiles and preserves old First Contact behavior. Any unsupported method is explicitly failed work, not a promise for later.

## Fixed tuning allowlist

Existing actor property and type; include exactly these DTO members. No pose/expression assets, masks, bone paths or initial-state configuration in the override.

- `DefaultExpressionBlendTime`: `float`.
- `MotionOwnershipBlendSeconds`: `float`.
- `DefaultPoseTransition`: `PoseTransition`.
- `BreathingEnabled`: `bool`.
- `BreathsPerMinute`: `float`.
- `BreathingCurve`: `AnimationCurve`.
- `MorphBreathingEnabled`: `bool`.
- `MorphBreathingStrength`: `float`.
- `BreatheStrength`: `float`.
- `BreatheBellyStrength`: `float`.
- `BoneBreathingEnabled`: `bool`.
- `BoneBreathingStrength`: `float`.
- `GazeEnabled`: `bool`.
- `HeadGazeEnabled`: `bool`.
- `HeadGazeWeight`: `float`.
- `HeadGazeResponse`: `float`.
- `HeadGazeMaxYaw`: `float`.
- `HeadGazeMaxPitch`: `float`.
- `EyeGazeEnabled`: `bool`.
- `EyeGazeWeight`: `float`.
- `EyeGazeResponse`: `float`.
- `EyeGazeMaxYaw`: `float`.
- `EyeGazeMaxPitch`: `float`.
- `GazeReleaseResponse`: `float`.
- `GazeAcquireToleranceDegrees`: `float`.
- `AttentionLifeEnabled`: `bool`.
- `AttentionLifeSeed`: `int`.
- `EyeFixationLifeEnabled`: `bool`.
- `EyeFixationMaxHorizontalDegrees`: `float`.
- `EyeFixationMaxVerticalDegrees`: `float`.
- `EyeFixationMinimumHoldSeconds`: `float`.
- `EyeFixationMaximumHoldSeconds`: `float`.
- `EyeFixationCenterBias`: `float`.
- `HeadAttentionLifeEnabled`: `bool`.
- `HeadAttentionMaxTiltDegrees`: `float`.
- `HeadAttentionMaxChinDegrees`: `float`.
- `HeadAttentionMinimumHoldSeconds`: `float`.
- `HeadAttentionMaximumHoldSeconds`: `float`.
- `HeadAttentionTransitionResponse`: `float`.
- `BlinkEnabled`: `bool`.
- `BlinkStrength`: `float`.
- `BlinkMinimumIntervalSeconds`: `float`.
- `BlinkMaximumIntervalSeconds`: `float`.
- `BlinkCloseDurationSeconds`: `float`.
- `BlinkClosedDurationSeconds`: `float`.
- `BlinkOpenDurationSeconds`: `float`.
