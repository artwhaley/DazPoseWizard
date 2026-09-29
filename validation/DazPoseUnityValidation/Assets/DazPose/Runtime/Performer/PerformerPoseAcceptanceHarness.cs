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

        private bool _running;
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
            var playableCount = performer.RuntimePlayableCount;

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

            CheckPlacement("Outer Lara placement", startPlacement, CapturePlacement(placementRoot), failures);
            if (performer.RuntimePlayableCount != playableCount)
                failures.Add("Playable count changed from " + playableCount + " to " + performer.RuntimePlayableCount + ".");
            Finish(failures);
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
                Debug.Log("Performer pose checks passed: endpoints, custom curve, windup/overshoot, interruptions, latest-wins, blendshape continuity, graph lifecycle, and outer placement.", this);
            else
                Debug.LogError("Performer pose checks failed:\n- " + string.Join("\n- ", failures), this);
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
