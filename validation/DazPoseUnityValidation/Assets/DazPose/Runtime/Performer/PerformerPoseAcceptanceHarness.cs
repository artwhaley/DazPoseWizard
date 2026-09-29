using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.Performer
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Performer/Development/Pose Acceptance Checks")]
    public sealed class PerformerPoseAcceptanceHarness : MonoBehaviour
    {
        private const float PositionTolerance = 1e-4f;
        private const float RotationToleranceDegrees = 0.01f;
        private const float ScaleTolerance = 1e-4f;
        private const float BlendShapeTolerance = 0.05f;

        [SerializeField] private SuccubusPerformer performer;
        [SerializeField] private PerformerPose poseA = null;
        [SerializeField] private PerformerPose poseB = null;
        [SerializeField] private PerformerPose poseC = null;
        [SerializeField] private Transform gazeTarget;

        private bool _running;
        private bool _awaitableChecksComplete;
        private Vector3? _gazeTargetPositionBeforeTests;
        public string Status { get; private set; } = "Press F5 to run acceptance checks.";

        private void Reset()
        {
            if (performer == null) performer = GetComponent<SuccubusPerformer>();
        }

        public void Run()
        {
            if (_running) return;
            if (performer == null || poseA == null || poseB == null || poseC == null)
            {
                Debug.LogError("Assign SuccubusPerformer and all three PerformerPose assets before running acceptance checks.", this);
                return;
            }

            StartCoroutine(RunChecks());
        }

        private IEnumerator RunChecks()
        {
            _running = true;
            Status = "Running acceptance checks…";
            var failures = new List<string>();
            var animator = performer.GetComponent<Animator>();
            if (animator == null)
            {
                Finish(failures, "Animator is missing.");
                yield break;
            }

            var placementRoot = transform;
            while (placementRoot.parent != null) placementRoot = placementRoot.parent;
            var startPlacement = CapturePlacement(placementRoot);
            _gazeTargetPositionBeforeTests = gazeTarget == null ? (Vector3?)null : gazeTarget.position;
            var playableCount = performer.RuntimePlayableCount;
            var originalBreathing = CaptureBreathingSettings();
            var originalGaze = CaptureGazeSettings();
            performer.BreathingEnabled = false;
            performer.MorphBreathingStrength = 0f;
            performer.BoneBreathingStrength = 0f;
            performer.GazeEnabled = false;
            performer.ClearGaze();
            performer.SetBreathPhaseForAcceptance(originalBreathing.Phase);

            performer.Pose(poseA, PoseTransition.Snap);
            yield return null;
            var stateA = CapturePose(animator);
            performer.Pose(poseB, PoseTransition.Snap);
            yield return null;
            var stateB = CapturePose(animator);
            performer.Pose(poseC, PoseTransition.Snap);
            yield return null;
            var stateC = CapturePose(animator);
            performer.Pose(poseA, PoseTransition.Snap);
            yield return null;
            CheckPose("Zero-duration snap to A", stateA, CapturePose(animator), failures);
            if (performer.IsTransitioning) failures.Add("A zero-duration request remained in transition.");
            ValidateTrajectoryMath(failures);

            var smooth = PoseTransition.Smooth(0.65f);
            var before = CapturePose(animator);
            performer.Pose(poseB, smooth);
            if (!performer.IsTransitioning) failures.Add("A nonzero A-to-B transition completed immediately.");
            if (performer.DesiredPose != poseB || performer.SettledPose != poseA)
                failures.Add("A-to-B did not publish DesiredPose and SettledPose correctly.");
            CheckPose("A-to-B initial continuity", before, CapturePose(animator), failures);
            yield return WaitForSettlement(6f, failures, "A-to-B");
            CheckPose("A-to-B endpoint", stateB, CapturePose(animator), failures);
            if (performer.SettledPose != poseB) failures.Add("B did not become the settled pose.");

            performer.Pose(poseB, PoseTransition.Smooth(1f));
            if (performer.IsTransitioning) failures.Add("A repeated request for the settled desired pose restarted a transition.");
            performer.Pose(poseA, smooth);
            yield return WaitForSettlement(6f, failures, "B-to-A");
            CheckPose("B-to-A endpoint", stateA, CapturePose(animator), failures);

            var shaped = new PoseTransition(0.55f, 0.10f, 0.12f,
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            performer.Pose(poseB, shaped);
            yield return WaitForSettlement(6f, failures, "windup/overshoot A-to-B");
            CheckPose("Shaped transition endpoint", stateB, CapturePose(animator), failures);

            yield return InterruptAtProgress(poseA, poseB, poseC, 0.25f, stateC, failures);
            yield return InterruptAtProgress(poseA, poseB, poseC, 0.70f, stateC, failures);
            yield return CheckRapidRetargets(poseA, poseB, poseC, stateC, failures);
            yield return CheckLifecycle(animator, poseC, stateC, playableCount, failures);

            _awaitableChecksComplete = false;
            RunAwaitableChecks(failures);
            var awaitableDeadline = Time.realtimeSinceStartup + 30f;
            while (!_awaitableChecksComplete && Time.realtimeSinceStartup < awaitableDeadline)
                yield return null;
            if (!_awaitableChecksComplete)
            {
                failures.Add("PoseAsync acceptance checks did not finish before the timeout.");
                performer.Pose(poseA, PoseTransition.Snap);
                yield return null;
            }

            yield return CheckBreathingAcceptance(animator, stateA, stateB, stateC,
                startPlacement, failures);

            _awaitableChecksComplete = false;
            RunBreathingAwaitableChecks(failures);
            awaitableDeadline = Time.realtimeSinceStartup + 30f;
            while (!_awaitableChecksComplete && Time.realtimeSinceStartup < awaitableDeadline)
                yield return null;
            if (!_awaitableChecksComplete)
                failures.Add("Breathing PoseAsync acceptance checks did not finish before the timeout.");

            RestoreBreathingSettings(originalBreathing);
            performer.SetBreathPhaseForAcceptance(originalBreathing.Phase);

            yield return CheckGazeAcceptance(animator, startPlacement, failures);

            _awaitableChecksComplete = false;
            RunGazeAwaitableChecks(failures);
            awaitableDeadline = Time.realtimeSinceStartup + 45f;
            while (!_awaitableChecksComplete && Time.realtimeSinceStartup < awaitableDeadline)
                yield return null;
            if (!_awaitableChecksComplete)
                failures.Add("Gaze awaitable acceptance checks did not finish before the timeout.");

            performer.ClearGaze();
            yield return WaitForGazeRelease(2f);
            RestoreGazeSettings(originalGaze);
            RestoreBreathingSettings(originalBreathing);
            if (_gazeTargetPositionBeforeTests.HasValue && gazeTarget != null)
                gazeTarget.position = _gazeTargetPositionBeforeTests.Value;

            CheckPlacement("Outer Lara placement", startPlacement, CapturePlacement(placementRoot), failures);
            if (performer.RuntimePlayableCount != playableCount)
                failures.Add("Playable count changed from " + playableCount + " to " + performer.RuntimePlayableCount + ".");
            Finish(failures);
        }

        private IEnumerator CheckBreathingAcceptance(Animator animator, PoseSnapshot stateA,
            PoseSnapshot stateB, PoseSnapshot stateC, PlacementSnapshot startPlacement,
            List<string> failures)
        {
            var breathing = performer.BreathingRuntime;
            if (breathing == null)
            {
                failures.Add("PerformerBreathing runtime is missing.");
                yield break;
            }

            if (breathing.BreatheBindingCount == 0)
                failures.Add("Canonical Lara is missing the Breathe morph binding. " + string.Join(" ", breathing.Diagnostics));
            if (breathing.BreatheBellyBindingCount == 0)
                failures.Add("Canonical Lara is missing the BreatheBelly morph binding. " + string.Join(" ", breathing.Diagnostics));
            if (breathing.BoneBindings.Count == 0)
                failures.Add("No configured torso breathing bones resolved. " + string.Join(" ", breathing.Diagnostics));

            performer.BreathingEnabled = true;
            performer.MorphBreathingEnabled = true;
            performer.MorphBreathingStrength = 1f;
            performer.BreatheStrength = 1f;
            performer.BreatheBellyStrength = 0.7f;
            performer.BoneBreathingEnabled = false;
            performer.BoneBreathingStrength = 1f;
            performer.Pose(poseA, PoseTransition.Snap);

            performer.SetBreathPhaseForAcceptance(0f);
            var restBase = performer.CaptureEvaluatedBasePoseState();
            CheckPose("Breathing at rest must equal the base pose", ToPoseSnapshot(restBase),
                CapturePose(animator), failures);

            performer.SetBreathPhaseForAcceptance(0.32f);
            var morphInhaleBase = performer.CaptureEvaluatedBasePoseState();
            var inhaleContributions = CaptureMorphContributions(breathing.MorphBindings, morphInhaleBase);
            CheckMorphContributions("Morph-only inhale", breathing.MorphBindings, inhaleContributions,
                true, failures);
            CheckBreathingBonesEqualBase("Morph-only mode", breathing.BoneBindings,
                morphInhaleBase, failures);

            performer.SetBreathPhaseForAcceptance(0.70f);
            var morphExhaleBase = performer.CaptureEvaluatedBasePoseState();
            var exhaleContributions = CaptureMorphContributions(breathing.MorphBindings, morphExhaleBase);
            CheckMorphContributions("Morph-only exhale", breathing.MorphBindings, exhaleContributions,
                true, failures);
            if (inhaleContributions.Length != exhaleContributions.Length)
                failures.Add("Morph bindings changed between breath phases.");
            else
                for (var index = 0; index < inhaleContributions.Length; index++)
                    if (Mathf.Abs(inhaleContributions[index] - exhaleContributions[index]) < 0.05f)
                    {
                        failures.Add("Morph binding " + index + " did not change between two breath phases.");
                        break;
                    }

            performer.MorphBreathingEnabled = false;
            performer.BoneBreathingEnabled = true;
            performer.SetBreathPhaseForAcceptance(0.32f);
            var boneOnlyBase = performer.CaptureEvaluatedBasePoseState();
            CheckMorphsEqualBase("Bone-only mode", breathing.MorphBindings, boneOnlyBase, failures);
            if (!CheckBreathingBonesDifferFromBase("Bone-only mode", breathing.BoneBindings,
                    boneOnlyBase, failures))
                failures.Add("Bone-only breathing did not move any configured torso bone.");

            performer.MorphBreathingEnabled = true;
            performer.BoneBreathingEnabled = true;
            performer.MorphBreathingStrength = 1f;
            performer.BoneBreathingStrength = 1f;
            performer.SetBreathPhaseForAcceptance(0.32f);
            var combinedBase = performer.CaptureEvaluatedBasePoseState();
            CheckExpectedMorphContributions("Combined mode", breathing.MorphBindings,
                combinedBase, performer, failures);
            CheckExpectedBoneContributions("Combined mode", breathing.BoneBindings,
                combinedBase, performer, failures);
            var expectedBreathValue = Mathf.Clamp01(performer.BreathingCurve.Evaluate(performer.BreathPhase));
            if (Mathf.Abs(expectedBreathValue - performer.BreathValue) > 0.0001f)
                failures.Add("Morph and bone breathing do not share the evaluated value for the current phase.");

            yield return CheckBreathingRetargetIsolation(animator, stateA, stateB, stateC, failures);

            performer.BreathingEnabled = false;
            performer.SetBreathPhaseForAcceptance(0.32f);
            var disabledBase = performer.CaptureEvaluatedBasePoseState();
            CheckPose("Master breathing switch bypass", ToPoseSnapshot(disabledBase),
                CapturePose(animator), failures);

            performer.BreathingEnabled = true;
            performer.MorphBreathingStrength = 0f;
            performer.BoneBreathingStrength = 0f;
            performer.SetBreathPhaseForAcceptance(0.32f);
            var zeroStrengthBase = performer.CaptureEvaluatedBasePoseState();
            CheckPose("Zero breathing strengths bypass", ToPoseSnapshot(zeroStrengthBase),
                CapturePose(animator), failures);

            if (performer.DesiredPose != performer.SettledPose || performer.IsTransitioning)
                failures.Add("Continuous breathing changed the persistent pose state or transition status.");
            CheckPlacement("Breathing root placement", startPlacement, CapturePlacement(transform.root), failures);

            performer.Pose(poseA, PoseTransition.Snap);
            yield return null;
        }

        private IEnumerator CheckBreathingRetargetIsolation(Animator animator, PoseSnapshot stateA,
            PoseSnapshot stateB, PoseSnapshot stateC, List<string> failures)
        {
            var breathing = performer.BreathingRuntime;
            var midPoses = new[] { poseB, poseA, poseC };
            var nextPoses = new[] { poseC, poseB, poseA };
            var nextPoseStates = new[] { stateC, stateB, stateA };

            performer.Pose(poseA, PoseTransition.Snap);
            performer.SetBreathPhaseForAcceptance(0.32f);
            yield return null;

            for (var index = 0; index < midPoses.Length; index++)
            {
                performer.Pose(midPoses[index], PoseTransition.Smooth(1f));
                var deadline = Time.realtimeSinceStartup + 4f;
                while (performer.IsTransitioning && performer.TransitionProgress < 0.38f
                       && Time.realtimeSinceStartup < deadline)
                    yield return null;
                if (!performer.IsTransitioning || performer.TransitionProgress < 0.38f)
                {
                    failures.Add("Breathing retarget setup did not reach its interruption point.");
                    yield break;
                }

                var phaseBeforeRetarget = performer.BreathPhase;
                var evaluatedBase = performer.CaptureEvaluatedBasePoseState();
                var renderedBefore = CapturePose(animator);
                if (CheckPoseDifference(ToPoseSnapshot(evaluatedBase), renderedBefore) < 0.0001f)
                    failures.Add("Breathing was not visible during the base-state isolation check.");

                performer.Pose(nextPoses[index], new PoseTransition(0.45f, 0.06f, 0.08f,
                    AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)));
                CheckPose("Retarget source must be evaluated base only", ToPoseSnapshot(evaluatedBase),
                    ToPoseSnapshot(performer.CaptureTransitionSourcePoseState()), failures);
                CheckPose("Retarget continuity with downstream breathing", renderedBefore,
                    CapturePose(animator), failures);
                if (Mathf.Abs(phaseBeforeRetarget - performer.BreathPhase) > 0.0001f)
                    failures.Add("A pose retarget reset or advanced the breath phase synchronously.");

                yield return null;
                var phaseDelta = Mathf.Repeat(performer.BreathPhase - phaseBeforeRetarget, 1f);
                if (phaseDelta <= 0.0001f || phaseDelta > 0.2f)
                    failures.Add("The breath phase did not continue smoothly after a pose retarget.");
                yield return WaitForSettlement(6f, failures, "breathing retarget to " + nextPoses[index].name);
                CheckPose("Retarget base pose must not accumulate breathing offsets",
                    nextPoseStates[index], ToPoseSnapshot(performer.CaptureEvaluatedBasePoseState()), failures);
            }

            if (breathing.BreatheBindingCount == 0 || breathing.BreatheBellyBindingCount == 0)
                failures.Add("Retarget isolation did not exercise both requested morph channels.");
        }

        private IEnumerator CheckGazeAcceptance(Animator animator, PlacementSnapshot startPlacement,
            List<string> failures)
        {
            var gaze = performer.GazeRuntime;
            if (gaze == null)
            {
                failures.Add("PerformerGaze runtime is missing.");
                yield break;
            }
            if (!gaze.IsAvailable)
            {
                failures.Add("Canonical Lara gaze bindings failed: " + string.Join(" ", gaze.Diagnostics));
                yield break;
            }
            if (gazeTarget == null)
            {
                failures.Add("Assign the scene Gaze Target to the acceptance harness.");
                yield break;
            }

            CheckGazeCalibration("head", gaze.HeadCalibration, "head", failures);
            CheckGazeCalibration("left eye", gaze.LeftEyeCalibration, "lEye", failures);
            CheckGazeCalibration("right eye", gaze.RightEyeCalibration, "rEye", failures);

            performer.Pose(poseA, PoseTransition.Snap);
            performer.ClearGaze();
            performer.GazeEnabled = false;
            yield return null;
            var baseGazePose = CaptureGazePose(gaze);
            var firstTarget = gaze.CreateValidationTarget(18f, 7f, 2.5f);
            performer.LookAt(firstTarget);
            for (var frame = 0; frame < 3; frame++) yield return null;
            CheckGazePose("Master gaze bypass", baseGazePose, CaptureGazePose(gaze), false, false, false, failures);
            if (!performer.HasGazeTarget)
                failures.Add("Disabling the gaze development switch erased the semantic target.");

            performer.GazeEnabled = true;
            performer.HeadGazeEnabled = false;
            performer.HeadGazeWeight = 0f;
            performer.EyeGazeEnabled = true;
            performer.EyeGazeWeight = 1f;
            yield return WaitForGazeAcquisition(6f, failures, "eye-only target");
            CheckGazePose("Eye-only mode", baseGazePose, CaptureGazePose(gaze), false, true, true, failures);
            yield return ReleaseGazeToBase(baseGazePose, gaze, failures, "eye-only release");

            performer.HeadGazeEnabled = true;
            performer.HeadGazeWeight = 1f;
            performer.EyeGazeEnabled = true;
            performer.EyeGazeWeight = 0f;
            performer.LookAt(gaze.CreateValidationTarget(-27f, 8f, 2.5f));
            yield return WaitForGazeAcquisition(6f, failures, "head-only target");
            CheckGazePose("Head-only mode", baseGazePose, CaptureGazePose(gaze), true, false, false, failures);
            yield return ReleaseGazeToBase(baseGazePose, gaze, failures, "head-only release");

            performer.HeadGazeEnabled = true;
            performer.HeadGazeWeight = 0.7f;
            performer.EyeGazeEnabled = true;
            performer.EyeGazeWeight = 1f;
            performer.LookAt(gaze.CreateValidationTarget(25f, -9f, 2.5f));
            yield return WaitForGazeAcquisition(6f, failures, "combined head and eye target");
            CheckGazePose("Combined gaze", baseGazePose, CaptureGazePose(gaze), true, true, true, failures);

            var movingTargetStart = gaze.CreateValidationTarget(-20f, 2f, 2.5f);
            gazeTarget.position = movingTargetStart;
            performer.LookAt(gazeTarget);
            yield return WaitForGazeAcquisition(6f, failures, "moving Transform target");
            var previousSmoothedDirection = gaze.SmoothedHeadAimDirection;
            var movingTargetEnd = gaze.CreateValidationTarget(38f, 14f, 2.5f);
            gazeTarget.position = movingTargetEnd;
            var errorBeforeMove = Vector3.Angle(previousSmoothedDirection,
                (movingTargetEnd - gaze.HeadCalibration.Bone.position).normalized);
            for (var frame = 0; frame < 18; frame++) yield return null;
            var errorAfterMove = Vector3.Angle(gaze.SmoothedHeadAimDirection,
                (movingTargetEnd - gaze.HeadCalibration.Bone.position).normalized);
            if (Vector3.Distance(performer.RawGazeTargetPosition, movingTargetEnd) > 1e-4f
                || errorAfterMove >= errorBeforeMove - 0.5f)
                failures.Add("The persistent Transform gaze did not follow its moved target through the smoothed head aim.");

            var fixedPoint = gaze.CreateValidationTarget(-12f, 5f, 2.2f);
            performer.LookAt(fixedPoint);
            yield return WaitForGazeAcquisition(6f, failures, "fixed world target");
            gazeTarget.position = gaze.CreateValidationTarget(45f, -10f, 2.5f);
            for (var frame = 0; frame < 4; frame++) yield return null;
            if (Vector3.Distance(performer.RawGazeTargetPosition, fixedPoint) > 1e-5f)
                failures.Add("LookAt(Vector3) changed when an unrelated Transform moved.");

            performer.GazeEnabled = false;
            yield return null;
            CheckGazePose("Master bypass with an active target", baseGazePose,
                CaptureGazePose(gaze), false, false, false, failures);
            if (!performer.HasGazeTarget)
                failures.Add("The master gaze switch cleared a persistent target.");

            performer.GazeEnabled = true;
            performer.ClearGaze();
            yield return WaitForGazeRelease(3f);
            if (performer.GazeWeight > 0.01f)
                failures.Add("ClearGaze did not release gaze influence to zero.");
            CheckGazePose("ClearGaze returns to authored pose", baseGazePose,
                CaptureGazePose(gaze), false, false, false, failures);

            var farTarget = gaze.CreateValidationTarget(165f, 18f, 3f);
            var requestedHeadDirection = (farTarget - gaze.HeadCalibration.Bone.position).normalized;
            var headAim = (gaze.HeadCalibration.Bone.rotation * gaze.HeadCalibration.LocalAim).normalized;
            var headUp = (gaze.HeadCalibration.Bone.rotation * gaze.HeadCalibration.LocalUp).normalized;
            var headRight = Vector3.Cross(headUp, headAim).normalized;
            headUp = Vector3.Cross(headAim, headRight).normalized;
            var rawHeadYaw = Mathf.Atan2(Vector3.Dot(requestedHeadDirection, headRight),
                Vector3.Dot(requestedHeadDirection, headAim)) * Mathf.Rad2Deg;
            var clampedHeadAngles = PerformerGazeJob.ClampAimAngles(headAim, headRight, headUp,
                requestedHeadDirection, performer.HeadGazeMaxYaw, performer.HeadGazeMaxPitch);
            if (Mathf.Abs(rawHeadYaw) <= performer.HeadGazeMaxYaw
                || Mathf.Abs(clampedHeadAngles.x) > performer.HeadGazeMaxYaw + 0.01f
                || Mathf.Abs(clampedHeadAngles.y) > performer.HeadGazeMaxPitch + 0.01f)
                failures.Add("Behind-target head yaw/pitch did not clamp to the configured anatomical limits.");
            performer.LookAt(farTarget);
            yield return WaitForGazeAcquisition(8f, failures, "behind-the-performer target");
            CheckPlacement("Gaze limits must preserve outer performer placement", startPlacement,
                CapturePlacement(transform.root), failures);

            var beforePhase = performer.BreathPhase;
            var breathingWasEnabled = performer.BreathingEnabled;
            performer.BreathingEnabled = true;
            performer.LookAt(gaze.CreateValidationTarget(8f, 2f, 2.5f));
            for (var frame = 0; frame < 12; frame++) yield return null;
            if (Mathf.Repeat(performer.BreathPhase - beforePhase, 1f) <= 0.001f)
                failures.Add("Breathing phase did not continue while gaze was active.");
            performer.BreathingEnabled = breathingWasEnabled;

            performer.Pose(poseA, PoseTransition.Snap);
            performer.LookAt(gaze.CreateValidationTarget(-10f, 4f, 2.5f));
            yield return WaitForGazeAcquisition(6f, failures, "gaze before pose retarget");
            performer.Pose(poseB, PoseTransition.Smooth(1.2f));
            var deadline = Time.realtimeSinceStartup + 4f;
            while (performer.IsTransitioning && performer.TransitionProgress < 0.38f
                   && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!performer.IsTransitioning || performer.TransitionProgress < 0.38f)
                failures.Add("Gaze/base-state isolation setup did not reach the pose interruption point.");
            else
            {
                var capturedBase = performer.CaptureEvaluatedBasePoseState();
                var targetBeforeRetarget = performer.GazeTargetDescription;
                performer.Pose(poseC, new PoseTransition(0.45f, 0.06f, 0.08f,
                    AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)));
                CheckPose("Gaze must not contaminate the base transition source",
                    ToPoseSnapshot(capturedBase), ToPoseSnapshot(performer.CaptureTransitionSourcePoseState()), failures);
                if (!performer.HasGazeTarget || performer.GazeTargetDescription != targetBeforeRetarget)
                    failures.Add("Pose retargeting replaced the persistent gaze intention.");
                yield return WaitForSettlement(6f, failures, "gaze-active pose retarget");
            }

            performer.ClearGaze();
            yield return WaitForGazeRelease(3f);
            performer.Pose(poseA, PoseTransition.Snap);
            yield return null;
        }

        private IEnumerator ReleaseGazeToBase(GazePoseSnapshot basePose, PerformerGaze gaze,
            List<string> failures, string description)
        {
            performer.ClearGaze();
            yield return WaitForGazeRelease(3f);
            if (performer.GazeWeight > 0.01f)
                failures.Add(description + " did not reach zero gaze weight.");
            CheckGazePose(description, basePose, CaptureGazePose(gaze), false, false, false, failures);
        }

        private IEnumerator WaitForGazeAcquisition(float timeoutSeconds, List<string> failures,
            string description)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (performer.HasGazeTarget && !performer.IsGazeAcquired
                   && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!performer.HasGazeTarget || !performer.IsGazeAcquired)
                failures.Add(description + " did not acquire before the timeout.");
        }

        private IEnumerator WaitForGazeRelease(float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (performer.GazeWeight > 0.01f && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private static GazePoseSnapshot CaptureGazePose(PerformerGaze gaze)
        {
            return new GazePoseSnapshot(gaze.HeadCalibration.Bone.localRotation,
                gaze.LeftEyeCalibration.Bone.localRotation, gaze.RightEyeCalibration.Bone.localRotation);
        }

        private static void CheckGazePose(string description, GazePoseSnapshot expected,
            GazePoseSnapshot actual, bool headShouldMove, bool leftEyeShouldMove,
            bool rightEyeShouldMove, List<string> failures)
        {
            CheckGazeRotation(description, "head", expected.Head, actual.Head, headShouldMove, failures);
            CheckGazeRotation(description, "left eye", expected.LeftEye, actual.LeftEye, leftEyeShouldMove, failures);
            CheckGazeRotation(description, "right eye", expected.RightEye, actual.RightEye, rightEyeShouldMove, failures);
        }

        private static void CheckGazeRotation(string description, string semanticName,
            Quaternion expected, Quaternion actual, bool shouldMove, List<string> failures)
        {
            var difference = Quaternion.Angle(expected, actual);
            if (shouldMove && difference <= RotationToleranceDegrees)
                failures.Add(description + " did not rotate the " + semanticName + ".");
            if (!shouldMove && difference > RotationToleranceDegrees)
                failures.Add(description + " changed the " + semanticName + " without a gaze contribution.");
        }

        private static void CheckGazeCalibration(string semanticName,
            PerformerGazeBoneCalibration calibration, string expectedBoneName, List<string> failures)
        {
            if (calibration.Bone == null || calibration.Bone.name != expectedBoneName)
            {
                failures.Add("The " + semanticName + " gaze bone did not resolve to exact transform '"
                             + expectedBoneName + "'.");
                return;
            }

            if (!IsFinite(calibration.LocalAim) || !IsFinite(calibration.LocalUp)
                || !IsFinite(calibration.LocalRight) || Mathf.Abs(calibration.LocalAim.magnitude - 1f) > 0.001f
                || Mathf.Abs(calibration.LocalUp.magnitude - 1f) > 0.001f
                || Mathf.Abs(calibration.LocalRight.magnitude - 1f) > 0.001f
                || Mathf.Abs(Vector3.Dot(calibration.LocalAim, calibration.LocalUp)) > 0.001f
                || Mathf.Abs(Vector3.Dot(calibration.LocalAim, calibration.LocalRight)) > 0.001f
                || Mathf.Abs(Vector3.Dot(calibration.LocalUp, calibration.LocalRight)) > 0.001f)
                failures.Add("The " + semanticName + " calibrated anatomical aim basis is invalid.");
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                   && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                   && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private async void RunBreathingAwaitableChecks(List<string> failures)
        {
            try
            {
                performer.BreathingEnabled = true;
                performer.MorphBreathingEnabled = true;
                performer.BoneBreathingEnabled = true;
                performer.MorphBreathingStrength = 1f;
                performer.BoneBreathingStrength = 1f;
                performer.Pose(poseA, PoseTransition.Snap);
                performer.SetBreathPhaseForAcceptance(0.20f);
                var phaseAtStart = performer.BreathPhase;
                var settledRequest = StartPoseRequest(poseB, PoseTransition.Smooth(0.30f));
                await WaitForCompletionObservation(settledRequest, 5f, failures,
                    "PoseAsync while breathing is active");
                if (settledRequest.Result != PoseCompletion.Settled || performer.SettledPose != poseB
                    || performer.IsTransitioning)
                    failures.Add("PoseAsync did not complete at base-pose settlement while breathing was active.");
                var phaseAtSettlement = performer.BreathPhase;
                await Awaitable.NextFrameAsync();
                if (Mathf.Repeat(phaseAtSettlement - phaseAtStart, 1f) <= 0.01f
                    || Mathf.Repeat(performer.BreathPhase - phaseAtSettlement, 1f) <= 0.0001f)
                    failures.Add("Breathing did not continue independently through and after PoseAsync settlement.");

                performer.Pose(poseA, PoseTransition.Snap);
                performer.SetBreathPhaseForAcceptance(0.25f);
                var superseded = StartPoseRequest(poseB, PoseTransition.Smooth(1f));
                var deadline = Time.realtimeSinceStartup + 4f;
                while (performer.IsTransitioning && performer.TransitionProgress < 0.35f
                       && Time.realtimeSinceStartup < deadline)
                    await Awaitable.NextFrameAsync();
                if (!performer.IsTransitioning || performer.TransitionProgress < 0.35f)
                    failures.Add("Breathing-active supersession setup did not reach the requested transition point.");
                var phaseBeforeSupersession = performer.BreathPhase;
                performer.Pose(poseC, PoseTransition.Smooth(0.25f));
                if (!superseded.Completed || superseded.Result != PoseCompletion.Superseded
                    || performer.DesiredPose != poseC)
                    failures.Add("Breathing-active PoseAsync supersession did not preserve latest-wins behavior.");
                if (Mathf.Abs(phaseBeforeSupersession - performer.BreathPhase) > 0.0001f)
                    failures.Add("Superseding an awaited pose reset the breath phase.");
                await WaitForSettlementAsync(5f, failures, "breathing-active superseding pose");
                if (performer.SettledPose != poseC)
                    failures.Add("The breathing-active superseding pose did not settle at the latest target.");
            }
            catch (Exception exception)
            {
                failures.Add("Breathing PoseAsync checks threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                _awaitableChecksComplete = true;
            }
        }

        private async void RunGazeAwaitableChecks(List<string> failures)
        {
            var originalSettings = CaptureGazeSettings();
            GameObject targetA = null;
            GameObject targetB = null;
            GameObject lostTarget = null;
            try
            {
                var gaze = performer.GazeRuntime;
                if (gaze == null || !gaze.IsAvailable)
                {
                    failures.Add("Gaze awaitable checks require a successfully initialized PerformerGaze runtime.");
                    return;
                }

                performer.GazeEnabled = true;
                performer.HeadGazeEnabled = true;
                performer.EyeGazeEnabled = true;
                performer.HeadGazeWeight = 0.7f;
                performer.EyeGazeWeight = 1f;
                performer.HeadGazeResponse = 4f;
                performer.EyeGazeResponse = 12f;
                performer.GazeAcquireToleranceDegrees = 3f;
                performer.GazeReleaseResponse = 20f;
                targetA = new GameObject("Gaze Acceptance Target A");
                targetB = new GameObject("Gaze Acceptance Target B");
                targetA.transform.position = gaze.CreateValidationTarget(15f, 5f, 2.5f);
                targetB.transform.position = gaze.CreateValidationTarget(-35f, 10f, 2.5f);

                performer.Pose(poseA, PoseTransition.Snap);
                var firstSameTarget = StartGazeRequest(targetA.transform);
                await Awaitable.NextFrameAsync();
                var generationBeforeDuplicate = gaze.IntentionGeneration;
                var secondSameTarget = StartGazeRequest(targetA.transform);
                if (gaze.IntentionGeneration != generationBeforeDuplicate)
                    failures.Add("A second same-Transform waiter restarted gaze acquisition.");
                await WaitForGazeCompletionObservation(firstSameTarget, 8f, failures,
                    "first same-target gaze waiter");
                await WaitForGazeCompletionObservation(secondSameTarget, 8f, failures,
                    "second same-target gaze waiter");
                if (firstSameTarget.Result != GazeCompletion.Acquired
                    || secondSameTarget.Result != GazeCompletion.Acquired)
                    failures.Add("Same-target gaze waiters did not both complete as Acquired.");

                var generationBeforeMovedDuplicate = gaze.IntentionGeneration;
                targetA.transform.position = gaze.CreateValidationTarget(-48f, 12f, 2.5f);
                var movedSameTarget = StartGazeRequest(targetA.transform);
                if (movedSameTarget.Completed || gaze.IntentionGeneration != generationBeforeMovedDuplicate)
                    failures.Add("A moved but semantically identical target restarted gaze or reported stale acquisition.");
                await WaitForGazeCompletionObservation(movedSameTarget, 8f, failures,
                    "same target after its Transform moved");
                if (movedSameTarget.Result != GazeCompletion.Acquired)
                    failures.Add("A same-Transform waiter did not reacquire after the target moved.");

                performer.ClearGaze();
                await WaitForGazeReleaseAsync(3f);
                performer.HeadGazeResponse = 0.1f;
                performer.EyeGazeResponse = 0.1f;
                performer.GazeAcquireToleranceDegrees = 0.1f;
                targetA.transform.position = gaze.CreateValidationTarget(78f, -12f, 3f);
                targetB.transform.position = gaze.CreateValidationTarget(-70f, 16f, 3f);
                var superseded = StartGazeRequest(targetA.transform);
                await Awaitable.NextFrameAsync();
                var headBeforeRetarget = gaze.HeadCalibration.Bone.localRotation;
                var leftEyeBeforeRetarget = gaze.LeftEyeCalibration.Bone.localRotation;
                var rightEyeBeforeRetarget = gaze.RightEyeCalibration.Bone.localRotation;
                performer.LookAt(targetB.transform);
                if (!superseded.Completed || superseded.Result != GazeCompletion.Superseded)
                    failures.Add("A replaced pending gaze request did not complete as Superseded immediately.");
                CheckGazeRotation("Gaze retarget continuity", "head", headBeforeRetarget,
                    gaze.HeadCalibration.Bone.localRotation, false, failures);
                CheckGazeRotation("Gaze retarget continuity", "left eye", leftEyeBeforeRetarget,
                    gaze.LeftEyeCalibration.Bone.localRotation, false, failures);
                CheckGazeRotation("Gaze retarget continuity", "right eye", rightEyeBeforeRetarget,
                    gaze.RightEyeCalibration.Bone.localRotation, false, failures);
                var retargetWaiter = StartGazeRequest(targetB.transform);
                performer.HeadGazeResponse = 12f;
                performer.EyeGazeResponse = 20f;
                await WaitForGazeCompletionObservation(retargetWaiter, 8f, failures,
                    "latest gaze target after supersession");
                if (retargetWaiter.Result != GazeCompletion.Acquired)
                    failures.Add("The latest gaze target did not acquire after superseding the first target.");

                performer.ClearGaze();
                await WaitForGazeReleaseAsync(3f);
                performer.HeadGazeResponse = 0.1f;
                performer.EyeGazeResponse = 0.1f;
                targetA.transform.position = gaze.CreateValidationTarget(100f, 0f, 3f);
                var clearedDuringAcquisition = StartGazeRequest(targetA.transform);
                await Awaitable.NextFrameAsync();
                performer.ClearGaze();
                if (!clearedDuringAcquisition.Completed
                    || clearedDuringAcquisition.Result != GazeCompletion.Superseded)
                    failures.Add("ClearGaze during acquisition did not supersede the pending waiter.");
                await WaitForGazeReleaseAsync(3f);

                lostTarget = new GameObject("Gaze Acceptance Lost Target");
                lostTarget.transform.position = gaze.CreateValidationTarget(-100f, 0f, 3f);
                var targetLost = StartGazeRequest(lostTarget.transform);
                await Awaitable.NextFrameAsync();
                Destroy(lostTarget);
                await WaitForGazeCompletionObservation(targetLost, 4f, failures,
                    "destroyed Transform gaze target");
                if (targetLost.Result != GazeCompletion.TargetLost || performer.HasGazeTarget)
                    failures.Add("A destroyed gaze Transform did not resolve as TargetLost and release the intention.");
                await WaitForGazeReleaseAsync(3f);
                lostTarget = null;

                performer.HeadGazeResponse = 20f;
                performer.EyeGazeResponse = 30f;
                performer.GazeAcquireToleranceDegrees = 8f;
                var fixedPoint = gaze.CreateValidationTarget(5f, 2f, 2.5f);
                var fixedAwaiter = StartGazeRequest(fixedPoint);
                await WaitForGazeCompletionObservation(fixedAwaiter, 5f, failures,
                    "fixed world point gaze waiter");
                if (fixedAwaiter.Result != GazeCompletion.Acquired)
                    failures.Add("LookAtAsync(Vector3) did not complete as Acquired.");

                performer.ClearGaze();
                await WaitForGazeReleaseAsync(3f);
                performer.HeadGazeResponse = 20f;
                performer.EyeGazeResponse = 30f;
                performer.GazeAcquireToleranceDegrees = 8f;
                var constrainedWaiter = StartGazeRequest(gaze.CreateValidationTarget(145f, 12f, 2.5f));
                await WaitForGazeCompletionObservation(constrainedWaiter, 5f, failures,
                    "async gaze toward a target beyond anatomical limits");
                if (constrainedWaiter.Result != GazeCompletion.Acquired)
                    failures.Add("LookAtAsync did not complete Acquired for a target beyond anatomical limits.");

                performer.ClearGaze();
                await WaitForGazeReleaseAsync(3f);
                performer.HeadGazeResponse = 0.1f;
                performer.EyeGazeResponse = 0.1f;
                performer.GazeAcquireToleranceDegrees = 0.1f;
                var slowGaze = StartGazeRequest(gaze.CreateValidationTarget(145f, 10f, 3f));
                await Awaitable.NextFrameAsync();
                var poseWhileGazeAcquires = StartPoseRequest(poseB, PoseTransition.Smooth(0.15f));
                await WaitForCompletionObservation(poseWhileGazeAcquires, 4f, failures,
                    "PoseAsync while gaze is still acquiring");
                if (poseWhileGazeAcquires.Result != PoseCompletion.Settled || performer.SettledPose != poseB)
                    failures.Add("PoseAsync did not settle independently while gaze was acquiring.");
                if (slowGaze.Completed)
                    failures.Add("LookAtAsync completed before its slow, out-of-range gaze solution settled.");
                performer.ClearGaze();
                if (!slowGaze.Completed || slowGaze.Result != GazeCompletion.Superseded)
                    failures.Add("Clearing a slow pending gaze did not complete its waiter as Superseded.");
                await WaitForGazeReleaseAsync(3f);

                performer.HeadGazeResponse = 20f;
                performer.EyeGazeResponse = 30f;
                performer.GazeAcquireToleranceDegrees = 10f;
                performer.Pose(poseA, PoseTransition.Snap);
                var poseInProgress = StartPoseRequest(poseB, PoseTransition.Smooth(2f));
                await Awaitable.NextFrameAsync();
                targetA.transform.position = gaze.CreateValidationTarget(4f, 1f, 2.5f);
                targetB.transform.position = gaze.CreateValidationTarget(-28f, 8f, 2.8f);
                var gazeDuringPose = StartGazeRequest(targetA.transform);
                await WaitForGazeCompletionObservation(gazeDuringPose, 3f, failures,
                    "LookAtAsync while a body pose transition is active");
                if (gazeDuringPose.Result != GazeCompletion.Acquired || !performer.IsTransitioning)
                    failures.Add("LookAtAsync was coupled to pose settlement instead of gaze acquisition.");
                var gazeRetargetedDuringPose = StartGazeRequest(targetB.transform);
                if (!performer.IsTransitioning)
                    failures.Add("The body pose transition settled before a second gaze target could be issued.");
                await WaitForGazeCompletionObservation(gazeRetargetedDuringPose, 3f, failures,
                    "gaze retarget during a body pose transition");
                if (gazeRetargetedDuringPose.Result != GazeCompletion.Acquired || !performer.IsTransitioning)
                    failures.Add("Gaze could not retarget and acquire independently while the body pose transition continued.");
                await WaitForCompletionObservation(poseInProgress, 5f, failures,
                    "PoseAsync while gaze remains active");
                if (poseInProgress.Result != PoseCompletion.Settled)
                    failures.Add("PoseAsync did not settle while persistent gaze remained active.");

                performer.ClearGaze();
                await WaitForGazeReleaseAsync(3f);
                performer.HeadGazeResponse = 0.1f;
                performer.EyeGazeResponse = 0.1f;
                performer.GazeAcquireToleranceDegrees = 0.1f;
                var pendingOnDisable = StartGazeRequest(gaze.CreateValidationTarget(130f, -15f, 3f));
                await Awaitable.NextFrameAsync();
                if (pendingOnDisable.Completed)
                    failures.Add("The lifecycle test target acquired before the performer-disable check.");
                performer.enabled = false;
                if (!pendingOnDisable.Completed || pendingOnDisable.Result != GazeCompletion.PerformerDisabled)
                    failures.Add("Disabling the performer did not complete the pending gaze waiter as PerformerDisabled.");
                performer.enabled = true;
                await Awaitable.NextFrameAsync();
            }
            catch (Exception exception)
            {
                failures.Add("Gaze awaitable checks threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                if (!performer.enabled) performer.enabled = true;
                if (targetA != null) Destroy(targetA);
                if (targetB != null) Destroy(targetB);
                if (lostTarget != null) Destroy(lostTarget);
                RestoreGazeSettings(originalSettings);
                _awaitableChecksComplete = true;
            }
        }

        private GazeCompletionObservation StartGazeRequest(Transform target)
        {
            var observation = new GazeCompletionObservation();
            ObserveGazeRequest(observation, target);
            return observation;
        }

        private GazeCompletionObservation StartGazeRequest(Vector3 worldPosition)
        {
            var observation = new GazeCompletionObservation();
            ObserveGazeRequest(observation, worldPosition);
            return observation;
        }

        private async void ObserveGazeRequest(GazeCompletionObservation observation, Transform target)
        {
            try
            {
                observation.Result = await performer.LookAtAsync(target);
            }
            catch (Exception exception)
            {
                observation.Error = exception;
            }
            finally
            {
                observation.Completed = true;
            }
        }

        private async void ObserveGazeRequest(GazeCompletionObservation observation, Vector3 worldPosition)
        {
            try
            {
                observation.Result = await performer.LookAtAsync(worldPosition);
            }
            catch (Exception exception)
            {
                observation.Error = exception;
            }
            finally
            {
                observation.Completed = true;
            }
        }

        private static async Awaitable WaitForGazeCompletionObservation(
            GazeCompletionObservation observation, float timeoutSeconds, List<string> failures,
            string description)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!observation.Completed && Time.realtimeSinceStartup < deadline)
                await Awaitable.NextFrameAsync();
            if (!observation.Completed)
            {
                failures.Add(description + " did not resolve before the timeout.");
                return;
            }
            if (observation.Error != null)
                failures.Add(description + " threw " + observation.Error.GetType().Name + ": " + observation.Error.Message);
        }

        private async Awaitable WaitForGazeReleaseAsync(float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (performer.GazeWeight > 0.01f && Time.realtimeSinceStartup < deadline)
                await Awaitable.NextFrameAsync();
            if (performer.GazeWeight > 0.01f)
                throw new TimeoutException("Gaze influence did not release before the timeout.");
        }

        private static float[] CaptureMorphContributions(
            IReadOnlyList<PerformerBreathingMorphBindingInfo> bindings, PerformerPoseSnapshot baseState)
        {
            var result = new float[bindings.Count];
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                result[index] = binding.Renderer.GetBlendShapeWeight(binding.BlendShapeIndex)
                                - baseState.BlendShapes[binding.BaseBlendShapeIndex];
            }
            return result;
        }

        private static void CheckMorphContributions(string description,
            IReadOnlyList<PerformerBreathingMorphBindingInfo> bindings, IReadOnlyList<float> contributions,
            bool shouldMove, List<string> failures)
        {
            for (var index = 0; index < bindings.Count; index++)
            {
                if (shouldMove && contributions[index] <= 0.05f)
                    failures.Add(description + " did not add weight to '" + bindings[index].SemanticName
                                 + "' on " + bindings[index].RendererPath + ".");
                if (!shouldMove && Mathf.Abs(contributions[index]) > BlendShapeTolerance)
                    failures.Add(description + " changed base morph '" + bindings[index].SemanticName + "'.");
            }
        }

        private static void CheckMorphsEqualBase(string description,
            IReadOnlyList<PerformerBreathingMorphBindingInfo> bindings, PerformerPoseSnapshot baseState,
            List<string> failures)
        {
            CheckMorphContributions(description, bindings, CaptureMorphContributions(bindings, baseState),
                false, failures);
        }

        private static void CheckBreathingBonesEqualBase(string description,
            IReadOnlyList<PerformerBreathingBoneBindingInfo> bindings, PerformerPoseSnapshot baseState,
            List<string> failures)
        {
            CheckBreathingBones(description, bindings, baseState, false, failures);
        }

        private static bool CheckBreathingBonesDifferFromBase(string description,
            IReadOnlyList<PerformerBreathingBoneBindingInfo> bindings, PerformerPoseSnapshot baseState,
            List<string> failures)
        {
            return CheckBreathingBones(description, bindings, baseState, true, failures);
        }

        private static bool CheckBreathingBones(string description,
            IReadOnlyList<PerformerBreathingBoneBindingInfo> bindings, PerformerPoseSnapshot baseState,
            bool shouldMove, List<string> failures)
        {
            var anyMoved = false;
            foreach (var binding in bindings)
            {
                var expected = baseState.Transforms[binding.BaseTransformIndex];
                var positionDelta = Vector3.Distance(binding.Bone.localPosition, expected.LocalPosition);
                var rotationDelta = Quaternion.Angle(binding.Bone.localRotation, expected.LocalRotation);
                var moved = positionDelta > PositionTolerance || rotationDelta > RotationToleranceDegrees;
                anyMoved |= moved;
                if (shouldMove && !moved)
                    failures.Add(description + " did not move configured bone '" + binding.Bone.name + "'.");
                if (!shouldMove && moved)
                    failures.Add(description + " applied a breathing delta to bone '" + binding.Bone.name + "'.");
            }
            return anyMoved;
        }

        private static void CheckExpectedMorphContributions(string description,
            IReadOnlyList<PerformerBreathingMorphBindingInfo> bindings, PerformerPoseSnapshot baseState,
            SuccubusPerformer currentPerformer, List<string> failures)
        {
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                var relativeStrength = binding.SemanticName == "Breathe"
                    ? currentPerformer.BreatheStrength : currentPerformer.BreatheBellyStrength;
                var baseWeight = baseState.BlendShapes[binding.BaseBlendShapeIndex];
                var room = Mathf.Max(0f, binding.PositiveMaximumWeight - baseWeight);
                var expected = Mathf.Min(PerformerBreathing.MorphWeightAmplitude * currentPerformer.BreathValue
                                         * currentPerformer.MorphBreathingStrength * relativeStrength, room);
                var actual = binding.Renderer.GetBlendShapeWeight(binding.BlendShapeIndex) - baseWeight;
                if (Mathf.Abs(actual - expected) > BlendShapeTolerance)
                    failures.Add(description + " did not use the shared breath value for '"
                                 + binding.SemanticName + "' on " + binding.RendererPath + ".");
            }
        }

        private static void CheckExpectedBoneContributions(string description,
            IReadOnlyList<PerformerBreathingBoneBindingInfo> bindings, PerformerPoseSnapshot baseState,
            SuccubusPerformer currentPerformer, List<string> failures)
        {
            var inhale = currentPerformer.BreathValue * currentPerformer.BoneBreathingStrength;
            foreach (var binding in bindings)
            {
                var expected = baseState.Transforms[binding.BaseTransformIndex];
                var expectedPosition = expected.LocalPosition + binding.FullInhaleLocalPositionDelta * inhale;
                var expectedRotation = expected.LocalRotation
                                      * Quaternion.Euler(binding.FullInhaleLocalRotationDelta * inhale);
                if (Vector3.Distance(binding.Bone.localPosition, expectedPosition) > PositionTolerance
                    || Quaternion.Angle(binding.Bone.localRotation, expectedRotation) > RotationToleranceDegrees)
                    failures.Add(description + " did not derive bone '" + binding.Bone.name
                                 + "' from the same breath value as the morph channels.");
            }
        }

        private static PoseSnapshot ToPoseSnapshot(PerformerPoseSnapshot source)
        {
            var transforms = new TransformSnapshot[source.Transforms.Length];
            for (var index = 0; index < transforms.Length; index++)
            {
                var state = source.Transforms[index];
                transforms[index] = new TransformSnapshot(state.LocalPosition, state.LocalRotation, state.LocalScale);
            }
            return new PoseSnapshot(transforms, (float[])source.BlendShapes.Clone());
        }

        private static float CheckPoseDifference(PoseSnapshot left, PoseSnapshot right)
        {
            var difference = 0f;
            var transformCount = Mathf.Min(left.Transforms.Length, right.Transforms.Length);
            for (var index = 0; index < transformCount; index++)
            {
                difference = Mathf.Max(difference,
                    Vector3.Distance(left.Transforms[index].Position, right.Transforms[index].Position));
                difference = Mathf.Max(difference,
                    Quaternion.Angle(left.Transforms[index].Rotation, right.Transforms[index].Rotation) / 180f);
            }
            var blendShapeCount = Mathf.Min(left.BlendShapes.Length, right.BlendShapes.Length);
            for (var index = 0; index < blendShapeCount; index++)
                difference = Mathf.Max(difference, Mathf.Abs(left.BlendShapes[index] - right.BlendShapes[index]));
            return difference;
        }

        private BreathingHarnessSettings CaptureBreathingSettings()
        {
            return new BreathingHarnessSettings(performer.BreathingEnabled,
                performer.MorphBreathingEnabled, performer.MorphBreathingStrength,
                performer.BreatheStrength, performer.BreatheBellyStrength,
                performer.BoneBreathingEnabled, performer.BoneBreathingStrength, performer.BreathPhase);
        }

        private GazeHarnessSettings CaptureGazeSettings()
        {
            return new GazeHarnessSettings(performer.GazeEnabled,
                performer.GazeAcquireToleranceDegrees, performer.HeadGazeEnabled,
                performer.HeadGazeWeight, performer.HeadGazeResponse, performer.HeadGazeMaxYaw,
                performer.HeadGazeMaxPitch, performer.EyeGazeEnabled, performer.EyeGazeWeight,
                performer.EyeGazeResponse, performer.EyeGazeMaxYaw, performer.EyeGazeMaxPitch,
                performer.GazeReleaseResponse);
        }

        private void RestoreGazeSettings(GazeHarnessSettings settings)
        {
            performer.GazeEnabled = settings.GazeEnabled;
            performer.GazeAcquireToleranceDegrees = settings.AcquireToleranceDegrees;
            performer.HeadGazeEnabled = settings.HeadEnabled;
            performer.HeadGazeWeight = settings.HeadWeight;
            performer.HeadGazeResponse = settings.HeadResponse;
            performer.HeadGazeMaxYaw = settings.HeadMaxYaw;
            performer.HeadGazeMaxPitch = settings.HeadMaxPitch;
            performer.EyeGazeEnabled = settings.EyesEnabled;
            performer.EyeGazeWeight = settings.EyeWeight;
            performer.EyeGazeResponse = settings.EyeResponse;
            performer.EyeGazeMaxYaw = settings.EyeMaxYaw;
            performer.EyeGazeMaxPitch = settings.EyeMaxPitch;
            performer.GazeReleaseResponse = settings.ReleaseResponse;
        }

        private void RestoreBreathingSettings(BreathingHarnessSettings settings)
        {
            performer.BreathingEnabled = settings.BreathingEnabled;
            performer.MorphBreathingEnabled = settings.MorphBreathingEnabled;
            performer.MorphBreathingStrength = settings.MorphBreathingStrength;
            performer.BreatheStrength = settings.BreatheStrength;
            performer.BreatheBellyStrength = settings.BreatheBellyStrength;
            performer.BoneBreathingEnabled = settings.BoneBreathingEnabled;
            performer.BoneBreathingStrength = settings.BoneBreathingStrength;
        }

        private async void RunAwaitableChecks(List<string> failures)
        {
            try
            {
                var alreadySettled = await performer.PoseAsync(poseC, PoseTransition.Smooth(0.5f));
                if (alreadySettled != PoseCompletion.Settled || performer.IsTransitioning)
                    failures.Add("PoseAsync on the already settled pose did not complete immediately as Settled.");

                var snap = StartPoseRequest(poseA, PoseTransition.Snap);
                if (!snap.Completed || snap.Error != null || snap.Result != PoseCompletion.Settled
                    || performer.DesiredPose != poseA || performer.SettledPose != poseA || performer.IsTransitioning)
                    failures.Add("PoseAsync with Snap did not settle synchronously before another frame.");

                var ordinary = StartPoseRequest(poseB, PoseTransition.Smooth(0.20f));
                if (ordinary.Completed || !performer.IsTransitioning || performer.DesiredPose != poseB)
                    failures.Add("PoseAsync did not suspend while an ordinary transition was still running.");
                await WaitForCompletionObservation(ordinary, 5f, failures, "ordinary PoseAsync transition");
                if (ordinary.Result != PoseCompletion.Settled || performer.SettledPose != poseB)
                    failures.Add("An ordinary PoseAsync transition did not complete with Settled at its target.");

                performer.Pose(poseA, PoseTransition.Snap);
                var reentrant = StartPoseRequest(poseB, PoseTransition.Smooth(1f));
                reentrant.OnCompleted = result =>
                {
                    if (result == PoseCompletion.Superseded)
                        performer.Pose(poseA, PoseTransition.Smooth(0.15f));
                };
                await Awaitable.NextFrameAsync();
                performer.Pose(poseC, PoseTransition.Smooth(0.25f));
                if (!reentrant.Completed || reentrant.Result != PoseCompletion.Superseded
                    || performer.DesiredPose != poseA || !performer.IsTransitioning)
                    failures.Add("A superseded PoseAsync continuation could not safely issue a reentrant pose command.");
                await WaitForSettlementAsync(5f, failures, "reentrant superseding pose");

                performer.Pose(poseA, PoseTransition.Snap);
                var first = StartPoseRequest(poseB, PoseTransition.Smooth(0.25f));
                var replacement = StartPoseRequest(poseC, PoseTransition.Smooth(0.20f));
                await WaitForCompletionObservation(first, 5f, failures, "PoseAsync supersession by PoseAsync");
                await WaitForCompletionObservation(replacement, 5f, failures, "latest PoseAsync target");
                if (first.Result != PoseCompletion.Superseded || replacement.Result != PoseCompletion.Settled
                    || performer.SettledPose != poseC)
                    failures.Add("A newer PoseAsync did not supersede the old waiter and settle the latest target.");

                performer.Pose(poseA, PoseTransition.Snap);
                var waiterOne = StartPoseRequest(poseB, PoseTransition.Smooth(0.30f));
                var waiterTwo = StartPoseRequest(poseB, PoseTransition.Smooth(1f));
                if (performer.ActiveTransition.Duration < 0.299f || performer.ActiveTransition.Duration > 0.301f)
                    failures.Add("A second waiter for the same desired pose restarted its transition.");
                await WaitForCompletionObservation(waiterOne, 5f, failures, "first same-target waiter");
                await WaitForCompletionObservation(waiterTwo, 5f, failures, "second same-target waiter");
                if (waiterOne.Result != PoseCompletion.Settled || waiterTwo.Result != PoseCompletion.Settled)
                    failures.Add("Independent same-target PoseAsync waiters did not both settle.");

                performer.Pose(poseA, PoseTransition.Snap);
                var disabled = StartPoseRequest(poseB, PoseTransition.Smooth(1f));
                performer.enabled = false;
                if (!disabled.Completed || disabled.Result != PoseCompletion.PerformerDisabled)
                    failures.Add("Disabling the performer did not complete its outstanding PoseAsync waiter as PerformerDisabled.");
                performer.enabled = true;
                await Awaitable.NextFrameAsync();
            }
            catch (Exception exception)
            {
                failures.Add("PoseAsync acceptance checks threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                _awaitableChecksComplete = true;
            }
        }

        private CompletionObservation StartPoseRequest(PerformerPose pose, PoseTransition transition)
        {
            var observation = new CompletionObservation();
            ObservePoseRequest(observation, pose, transition);
            return observation;
        }

        private async void ObservePoseRequest(CompletionObservation observation,
            PerformerPose pose, PoseTransition transition)
        {
            try
            {
                observation.Result = await performer.PoseAsync(pose, transition);
            }
            catch (Exception exception)
            {
                observation.Error = exception;
            }
            finally
            {
                observation.Completed = true;
            }

            observation.OnCompleted?.Invoke(observation.Result);
        }

        private static async Awaitable WaitForCompletionObservation(CompletionObservation observation,
            float timeoutSeconds, List<string> failures, string description)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!observation.Completed && Time.realtimeSinceStartup < deadline)
                await Awaitable.NextFrameAsync();
            if (!observation.Completed)
            {
                failures.Add(description + " did not resolve before the timeout.");
                return;
            }
            if (observation.Error != null)
                failures.Add(description + " threw " + observation.Error.GetType().Name + ": " + observation.Error.Message);
        }

        private async Awaitable WaitForSettlementAsync(float timeoutSeconds, List<string> failures, string description)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (performer.IsTransitioning && Time.realtimeSinceStartup < deadline)
                await Awaitable.NextFrameAsync();
            if (performer.IsTransitioning) failures.Add(description + " did not settle before the timeout.");
        }

        private sealed class CompletionObservation
        {
            public bool Completed;
            public PoseCompletion Result;
            public Exception Error;
            public Action<PoseCompletion> OnCompleted;
        }

        private sealed class GazeCompletionObservation
        {
            public bool Completed;
            public GazeCompletion Result;
            public Exception Error;
        }

        private IEnumerator InterruptAtProgress(PerformerPose start, PerformerPose middle, PerformerPose target,
            float progress, PoseSnapshot targetState, List<string> failures)
        {
            performer.Pose(start, PoseTransition.Snap);
            yield return null;
            performer.Pose(middle, PoseTransition.Smooth(1.2f));
            var deadline = Time.realtimeSinceStartup + 4f;
            while (performer.IsTransitioning && performer.TransitionProgress < progress
                   && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (!performer.IsTransitioning || performer.TransitionProgress < progress)
            {
                failures.Add("Transition did not reach the " + (progress * 100f).ToString("F0") + "% interruption point.");
                yield break;
            }

            var visibleBefore = CapturePose(performer.GetComponent<Animator>());
            performer.Pose(target, new PoseTransition(0.45f, 0.08f, 0.10f,
                AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)));
            CheckPose("Interruption continuity at " + (progress * 100f).ToString("F0") + "%",
                visibleBefore, CapturePose(performer.GetComponent<Animator>()), failures);
            if (performer.DesiredPose != target || performer.SettledPose != start || !performer.IsTransitioning)
                failures.Add("The " + (progress * 100f).ToString("F0") + "% interruption did not replace the desired pose immediately.");

            yield return WaitForSettlement(6f, failures, "interruption to " + target.name);
            if (performer.DesiredPose != target || performer.SettledPose != target || performer.IsTransitioning)
                failures.Add("The interrupted transition did not settle on its latest target.");
            CheckPose("Interrupted target endpoint", targetState,
                CapturePose(performer.GetComponent<Animator>()), failures);
        }

        private IEnumerator CheckRapidRetargets(PerformerPose start, PerformerPose first,
            PerformerPose latest, PoseSnapshot latestState, List<string> failures)
        {
            performer.Pose(start, PoseTransition.Snap);
            yield return null;
            performer.Pose(first, PoseTransition.Smooth(1f));
            yield return null;

            var before = CapturePose(performer.GetComponent<Animator>());
            performer.Pose(latest, PoseTransition.Smooth(0.25f));
            CheckPose("Rapid retarget continuity 1", before,
                CapturePose(performer.GetComponent<Animator>()), failures);
            before = CapturePose(performer.GetComponent<Animator>());
            performer.Pose(start, PoseTransition.Smooth(0.25f));
            CheckPose("Rapid retarget continuity 2", before,
                CapturePose(performer.GetComponent<Animator>()), failures);
            before = CapturePose(performer.GetComponent<Animator>());
            performer.Pose(latest, PoseTransition.Smooth(0.25f));
            CheckPose("Rapid retarget continuity 3", before,
                CapturePose(performer.GetComponent<Animator>()), failures);
            if (performer.DesiredPose != latest) failures.Add("Rapid retargeting did not keep the latest desired pose.");

            yield return WaitForSettlement(6f, failures, "rapid latest-wins retarget");
            if (performer.DesiredPose != latest || performer.SettledPose != latest || performer.IsTransitioning)
                failures.Add("Rapid retargeting queued an obsolete pose instead of settling on the latest target.");
            CheckPose("Rapid retarget latest endpoint", latestState,
                CapturePose(performer.GetComponent<Animator>()), failures);
        }

        private IEnumerator CheckLifecycle(Animator animator, PerformerPose pose,
            PoseSnapshot expected, int expectedPlayableCount, List<string> failures)
        {
            for (var index = 0; index < 3; index++)
            {
                performer.enabled = false;
                yield return null;
                if (performer.RuntimePlayableCount != 0)
                    failures.Add("Disabling the performer left a live playable graph.");

                performer.enabled = true;
                yield return null;
                if (performer.DesiredPose != pose || performer.SettledPose != pose || performer.IsTransitioning)
                    failures.Add("Performer re-enable did not restore its last desired pose.");
                CheckPose("Lifecycle re-enable " + (index + 1), expected, CapturePose(animator), failures);
                if (performer.RuntimePlayableCount != expectedPlayableCount)
                    failures.Add("Playable count after re-enable was " + performer.RuntimePlayableCount
                        + "; expected " + expectedPlayableCount + ".");
            }
        }

        private IEnumerator WaitForSettlement(float timeoutSeconds, List<string> failures, string description)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (performer.IsTransitioning && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (performer.IsTransitioning) failures.Add(description + " did not settle before the timeout.");
        }

        private static void ValidateTrajectoryMath(List<string> failures)
        {
            var linear = new PoseTransition(1f, 0f, 0f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            var slowStartCurve = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.75f, 0.18f),
                new Keyframe(1f, 1f, 0f, 0f));
            var slowStart = new PoseTransition(1f, 0f, 0f, slowStartCurve);
            if (Mathf.Abs(PoseTransitionTrajectory.Evaluate(0.35f, linear)
                          - PoseTransitionTrajectory.Evaluate(0.35f, slowStart)) < 0.1f)
                failures.Add("The custom curve did not change main-drive progression.");
            if (PoseTransitionTrajectory.Evaluate(0f, slowStart) != 0f
                || PoseTransitionTrajectory.Evaluate(1f, slowStart) != 1f)
                failures.Add("Custom curve endpoints were not exact.");

            var shaped = new PoseTransition(1f, 0.10f, 0.12f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            if (PoseTransitionTrajectory.Evaluate(0.075f, shaped) >= 0f)
                failures.Add("Windup did not travel below the source.");
            if (PoseTransitionTrajectory.Evaluate(0.90f, shaped) <= 1f)
                failures.Add("Overshoot did not travel past the target.");
            if (PoseTransitionTrajectory.Evaluate(1f, shaped) != 1f)
                failures.Add("Windup/overshoot did not finish at the exact target.");
        }

        private static PoseSnapshot CapturePose(Animator animator)
        {
            var transforms = animator.GetComponentsInChildren<Transform>(true);
            var renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var transformValues = new TransformSnapshot[transforms.Length];
            for (var index = 0; index < transforms.Length; index++)
            {
                var item = transforms[index];
                transformValues[index] = new TransformSnapshot(item.localPosition, item.localRotation, item.localScale);
            }

            var blendShapeCount = 0;
            foreach (var renderer in renderers)
                if (renderer != null && renderer.sharedMesh != null) blendShapeCount += renderer.sharedMesh.blendShapeCount;
            var blendShapeValues = new float[blendShapeCount];
            var blendShapeIndex = 0;
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMesh == null) continue;
                for (var index = 0; index < renderer.sharedMesh.blendShapeCount; index++)
                    blendShapeValues[blendShapeIndex++] = renderer.GetBlendShapeWeight(index);
            }
            return new PoseSnapshot(transformValues, blendShapeValues);
        }

        private static void CheckPose(string description, PoseSnapshot expected, PoseSnapshot actual,
            List<string> failures)
        {
            if (expected.Transforms.Length != actual.Transforms.Length
                || expected.BlendShapes.Length != actual.BlendShapes.Length)
            {
                failures.Add(description + " changed the transform or blendshape count.");
                return;
            }

            for (var index = 0; index < expected.Transforms.Length; index++)
            {
                var a = expected.Transforms[index];
                var b = actual.Transforms[index];
                if (Vector3.Distance(a.Position, b.Position) > PositionTolerance
                    || Quaternion.Angle(a.Rotation, b.Rotation) > RotationToleranceDegrees
                    || Vector3.Distance(a.Scale, b.Scale) > ScaleTolerance)
                {
                    failures.Add(description + " differed at transform index " + index + ".");
                    break;
                }
            }

            for (var index = 0; index < expected.BlendShapes.Length; index++)
            {
                if (Mathf.Abs(expected.BlendShapes[index] - actual.BlendShapes[index]) > BlendShapeTolerance)
                {
                    failures.Add(description + " differed at blendshape index " + index + ".");
                    break;
                }
            }
        }

        private static PlacementSnapshot CapturePlacement(Transform root)
        {
            return new PlacementSnapshot(root.position, root.rotation, root.lossyScale);
        }

        private static void CheckPlacement(string description, PlacementSnapshot expected,
            PlacementSnapshot actual, List<string> failures)
        {
            if (Vector3.Distance(expected.Position, actual.Position) > PositionTolerance
                || Quaternion.Angle(expected.Rotation, actual.Rotation) > RotationToleranceDegrees
                || Vector3.Distance(expected.Scale, actual.Scale) > ScaleTolerance)
                failures.Add(description + " changed during pose tests.");
        }

        private void Finish(List<string> failures, string additionalFailure = null)
        {
            if (additionalFailure != null) failures.Add(additionalFailure);
            _running = false;
            Status = failures.Count == 0 ? "Acceptance checks passed."
                : "Acceptance checks found " + failures.Count + " issue(s). See Console.";
            if (failures.Count == 0)
                Debug.Log("Performer checks passed: pose endpoints, windup/overshoot, pose awaitables, breathing modes, gaze calibration/modes/limits, gaze target tracking, gaze awaitables, base-state isolation, graph lifecycle, and root placement.", this);
            else
                Debug.LogError("Performer pose checks failed:\n- " + string.Join("\n- ", failures), this);
        }

        private readonly struct BreathingHarnessSettings
        {
            public readonly bool BreathingEnabled;
            public readonly bool MorphBreathingEnabled;
            public readonly float MorphBreathingStrength;
            public readonly float BreatheStrength;
            public readonly float BreatheBellyStrength;
            public readonly bool BoneBreathingEnabled;
            public readonly float BoneBreathingStrength;
            public readonly float Phase;

            public BreathingHarnessSettings(bool breathingEnabled, bool morphBreathingEnabled,
                float morphBreathingStrength, float breatheStrength, float breatheBellyStrength,
                bool boneBreathingEnabled, float boneBreathingStrength, float phase)
            {
                BreathingEnabled = breathingEnabled;
                MorphBreathingEnabled = morphBreathingEnabled;
                MorphBreathingStrength = morphBreathingStrength;
                BreatheStrength = breatheStrength;
                BreatheBellyStrength = breatheBellyStrength;
                BoneBreathingEnabled = boneBreathingEnabled;
                BoneBreathingStrength = boneBreathingStrength;
                Phase = phase;
            }
        }

        private readonly struct GazeHarnessSettings
        {
            public readonly bool GazeEnabled;
            public readonly float AcquireToleranceDegrees;
            public readonly bool HeadEnabled;
            public readonly float HeadWeight;
            public readonly float HeadResponse;
            public readonly float HeadMaxYaw;
            public readonly float HeadMaxPitch;
            public readonly bool EyesEnabled;
            public readonly float EyeWeight;
            public readonly float EyeResponse;
            public readonly float EyeMaxYaw;
            public readonly float EyeMaxPitch;
            public readonly float ReleaseResponse;

            public GazeHarnessSettings(bool gazeEnabled, float acquireToleranceDegrees,
                bool headEnabled, float headWeight, float headResponse, float headMaxYaw,
                float headMaxPitch, bool eyesEnabled, float eyeWeight, float eyeResponse,
                float eyeMaxYaw, float eyeMaxPitch, float releaseResponse)
            {
                GazeEnabled = gazeEnabled;
                AcquireToleranceDegrees = acquireToleranceDegrees;
                HeadEnabled = headEnabled;
                HeadWeight = headWeight;
                HeadResponse = headResponse;
                HeadMaxYaw = headMaxYaw;
                HeadMaxPitch = headMaxPitch;
                EyesEnabled = eyesEnabled;
                EyeWeight = eyeWeight;
                EyeResponse = eyeResponse;
                EyeMaxYaw = eyeMaxYaw;
                EyeMaxPitch = eyeMaxPitch;
                ReleaseResponse = releaseResponse;
            }
        }

        private readonly struct GazePoseSnapshot
        {
            public readonly Quaternion Head;
            public readonly Quaternion LeftEye;
            public readonly Quaternion RightEye;

            public GazePoseSnapshot(Quaternion head, Quaternion leftEye, Quaternion rightEye)
            {
                Head = head;
                LeftEye = leftEye;
                RightEye = rightEye;
            }
        }

        private readonly struct PoseSnapshot
        {
            public readonly TransformSnapshot[] Transforms;
            public readonly float[] BlendShapes;

            public PoseSnapshot(TransformSnapshot[] transforms, float[] blendShapes)
            {
                Transforms = transforms;
                BlendShapes = blendShapes;
            }
        }

        private readonly struct TransformSnapshot
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;

            public TransformSnapshot(Vector3 position, Quaternion rotation, Vector3 scale)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }
        }

        private readonly struct PlacementSnapshot
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;

            public PlacementSnapshot(Vector3 position, Quaternion rotation, Vector3 scale)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }
        }
    }
}
