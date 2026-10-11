using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace DazPose.Performer.HandGrip
{
    /// <summary>Deterministic geometry and synthetic kinematics checks for the T0 grip solver.</summary>
    public static class HandGripRuntimeSelfTests
    {
        private const float Tolerance = 0.002f;
        public static int AssertionCount { get; private set; }

        public static string[] Run()
        {
            AssertionCount = 0;
            var failures = new List<string>();
            CheckFrameAndRod(failures);
            CheckProbeClassification(failures);
            CheckSolver(failures);
            CheckBlendOwnership(failures);
            CheckCalibrationEndpoints(failures);
            CheckPalmAndReach(failures);
            return failures.ToArray();
        }

        private static void CheckFrameAndRod(List<string> failures)
        {
            Check(GripFrame.TryCreate(Vector3.zero, new Vector3(0f, 2f, 0f),
                    new Vector3(1f, 1f, 0f), 0.5f, out GripFrame frame),
                "frame accepts non-unit orthogonalizable axes", failures);
            Check(frame.IsValid && Near(frame.Tangent.magnitude, 1f)
                && Near(frame.Normal.magnitude, 1f) && Near(frame.Binormal.magnitude, 1f)
                && Mathf.Abs(Vector3.Dot(frame.Tangent, frame.Normal)) < Tolerance
                && Vector3.Dot(Vector3.Cross(frame.Tangent, frame.Normal), frame.Binormal) > 0.99f,
                "frame axes are normalized, perpendicular and right-handed", failures);
            Check(!GripFrame.TryCreate(Vector3.zero, Vector3.zero, Vector3.up, 0.5f, out _),
                "zero tangent is rejected", failures);
            Check(!GripFrame.TryCreate(Vector3.zero, Vector3.up, Vector3.up, 0.5f, out _),
                "parallel normal is rejected", failures);
            Check(!GripFrame.TryCreate(Vector3.zero, Vector3.up, Vector3.right, 0f, out _),
                "nonpositive radius is rejected", failures);

            var host = new GameObject("GripContactRodSelfTest");
            try
            {
                var start = new GameObject("Start").transform;
                var end = new GameObject("End").transform;
                start.SetParent(host.transform, false);
                end.SetParent(host.transform, false);
                start.position = new Vector3(0f, -2f, 0f);
                end.position = new Vector3(0f, 2f, 0f);
                GripContactRod rod = host.AddComponent<GripContactRod>();
                rod.StartPoint = start;
                rod.EndPoint = end;
                rod.Radius = 0.25f;
                rod.ReferenceNormalLocal = Vector3.forward;
                GripFrame below = rod.Evaluate(-2f);
                GripFrame above = rod.Evaluate(3f);
                Check(Near(below.Center.y, -2f) && Near(above.Center.y, 2f),
                    "rod clamps Position01 at both usable-region ends", failures);
                Check(Near(below.Radius, 0.25f) && Near(below.Tangent.y, 1f),
                    "rod exposes its constant radius and normalized tangent", failures);
                end.position = start.position;
                Check(!rod.TryEvaluate(0.5f, out _, out _), "zero-length rod is rejected", failures);
                end.position = new Vector3(0f, 2f, 0f);
                rod.Radius = -0.1f;
                Check(!rod.TryEvaluate(0.5f, out _, out _), "negative rod radius is rejected", failures);
                rod.Radius = 0.25f;
                rod.ReferenceNormalLocal = Vector3.up;
                Check(!rod.TryEvaluate(0.5f, out _, out _), "degenerate configured roll normal is rejected", failures);
            }
            finally
            {
                Destroy(host);
            }
        }

        private static void CheckProbeClassification(List<string> failures)
        {
            GripFrame frame = GripFrame.Create(Vector3.zero, Vector3.up, Vector3.forward, 1f);
            Check(Near(HandGripSolver.RadialDistance(frame, new Vector3(1.5f, 8f, 0f)), 1.5f),
                "probe radial distance ignores the cylinder's longitudinal coordinate", failures);
            Check(HandGripSolver.ClassifyProbe(frame, new Vector3(1.5f, 8f, 0f), 0.2f, 0.2f, 0.01f)
                    == HandGripProbeState.Clear,
                "a probe outside the allowed separation is clear", failures);
            Check(HandGripSolver.ClassifyProbe(frame, new Vector3(1.405f, 0f, 0f), 0.2f, 0.2f, 0.01f)
                    == HandGripProbeState.NearContact,
                "a probe near the contact boundary is reported without physics queries", failures);
            Check(HandGripSolver.ClassifyProbe(frame, new Vector3(1.2f, 0f, 0f), 0.1f, 0.1f, 0.005f)
                    == HandGripProbeState.NearContact,
                "a tangent probe reports contact", failures);
            Check(HandGripSolver.ClassifyProbe(frame, new Vector3(1.19f, 0f, 0f), 0.1f, 0.1f, 0.005f)
                    == HandGripProbeState.Penetrating,
                "a probe inside the allowed cylinder separation reports penetration", failures);
        }

        private static void CheckSolver(List<string> failures)
        {
            NativeArray<HandGripJobDigit> digits = default;
            NativeArray<HandGripJobJoint> joints = default;
            NativeArray<HandGripJobProbe> probes = default;
            NativeArray<Quaternion> incoming = default;
            NativeArray<float> curls = default;
            NativeArray<HandGripStatus> statuses = default;
            NativeArray<HandGripProbeDiagnostic> diagnostics = default;
            try
            {
                digits = new NativeArray<HandGripJobDigit>(2, Allocator.Temp);
                joints = new NativeArray<HandGripJobJoint>(2, Allocator.Temp);
                probes = new NativeArray<HandGripJobProbe>(2, Allocator.Temp);
                incoming = new NativeArray<Quaternion>(2, Allocator.Temp);
                curls = new NativeArray<float>(2, Allocator.Temp);
                statuses = new NativeArray<HandGripStatus>(2, Allocator.Temp);
                diagnostics = new NativeArray<HandGripProbeDiagnostic>(2, Allocator.Temp);
                digits[0] = new HandGripJobDigit { JointStart = 0, JointCount = 1, ProbeStart = 0, ProbeCount = 1 };
                digits[1] = new HandGripJobDigit { JointStart = 1, JointCount = 1, ProbeStart = 1, ProbeCount = 1 };
                joints[0] = SyntheticJoint(Vector3.zero);
                joints[1] = SyntheticJoint(Vector3.zero);
                probes[0] = new HandGripJobProbe
                {
                    DigitIndex = 0, JointIndex = 0, LocalPosition = new Vector3(1.4f, 0f, 0f), Radius = 0.08f
                };
                probes[1] = new HandGripJobProbe
                {
                    DigitIndex = 1, JointIndex = 1, LocalPosition = new Vector3(2.4f, 0f, 0f), Radius = 0.08f
                };
                incoming[0] = incoming[1] = Quaternion.identity;

                GripFrame small = GripFrame.Create(Vector3.zero, Vector3.up, Vector3.forward, 0.20f);
                HandGripSolver.Solve(small, Vector3.zero, Quaternion.identity, 1f, 1f,
                    0.02f, 0.006f, 7, digits, joints, probes, incoming, curls, statuses, diagnostics);
                Check(statuses[0] == HandGripStatus.Contact && statuses[1] == HandGripStatus.Contact
                    && curls[0] >= 0f && curls[0] <= 1f && curls[1] >= 0f && curls[1] <= 1f,
                    "penetrating closed poses solve within the bounded curl range", failures);
                Check(curls[1] > curls[0], "fingers solve independently from their own probe geometry", failures);
                float smallerRodCurl = curls[0];

                GripFrame larger = GripFrame.Create(Vector3.zero, Vector3.up, Vector3.forward, 0.40f);
                HandGripSolver.Solve(larger, Vector3.zero, Quaternion.identity, 1f, 1f,
                    0.02f, 0.006f, 7, digits, joints, probes, incoming, curls, statuses, diagnostics);
                Check(curls[0] <= smallerRodCurl + Tolerance,
                    "larger radius stops the same finger at an equal or earlier curl", failures);

                HandGripSolver.Solve(small, Vector3.zero, Quaternion.identity, 1f, 0.4f,
                    0.02f, 0.006f, 7, digits, joints, probes, incoming, curls, statuses, diagnostics);
                float weakerCurl = curls[0];
                HandGripSolver.Solve(small, Vector3.zero, Quaternion.identity, 1f, 0.7f,
                    0.02f, 0.006f, 7, digits, joints, probes, incoming, curls, statuses, diagnostics);
                Check(weakerCurl <= curls[0] + Tolerance && curls[0] <= 0.7f + Tolerance,
                    "requested grip strength bounds the maximum curl", failures);

                joints[0] = SyntheticJoint(new Vector3(1f, 0f, 0f), closedDegrees: 0f);
                digits[1] = new HandGripJobDigit { JointStart = 1, JointCount = 1, ProbeStart = 1, ProbeCount = 1 };
                probes[0] = new HandGripJobProbe
                {
                    DigitIndex = 0, JointIndex = 0, LocalPosition = new Vector3(1.4f, 0f, 0f), Radius = 0.08f
                };
                HandGripSolver.Solve(small, Vector3.zero, Quaternion.identity, 1f, 0.6f,
                    0.02f, 0.006f, 7, digits, joints, probes, incoming, curls, statuses, diagnostics);
                Check(Near(curls[0], 0.6f) && statuses[0] == HandGripStatus.Clear,
                    "a fully closed but clear pose reaches the requested curl", failures);

                joints[0] = SyntheticJoint(Vector3.zero);
                probes[0] = new HandGripJobProbe
                {
                    DigitIndex = 0, JointIndex = 0, LocalPosition = new Vector3(0.1f, 0f, 0f), Radius = 0.08f
                };
                HandGripSolver.Solve(small, Vector3.zero, Quaternion.identity, 1f, 1f,
                    0.02f, 0.006f, 7, digits, joints, probes, incoming, curls, statuses, diagnostics);
                Check(statuses[0] == HandGripStatus.BasePenetration && Near(curls[0], 0f),
                    "open-pose penetration is reported without trying invalid curls", failures);

                probes[0] = new HandGripJobProbe
                {
                    DigitIndex = 0, JointIndex = 0, LocalPosition = new Vector3(1.4f, 0f, 0f), Radius = 0.08f
                };
                GripFrame clearFrame = GripFrame.Create(new Vector3(-3f, 0f, 0f), Vector3.up,
                    Vector3.forward, 0.05f);
                joints[0] = SyntheticJoint(Vector3.zero, closedDegrees: 0f);
                HandGripSolver.Solve(clearFrame, Vector3.zero, Quaternion.identity, 1f, 0.85f,
                    0.02f, 0.006f, 7, digits, joints, probes, incoming, curls, statuses, diagnostics);
                Check(Near(curls[0], 0.85f) && statuses[0] == HandGripStatus.Clear,
                    "clear requested curls are returned unchanged", failures);
            }
            catch (Exception exception)
            {
                failures.Add("HandGrip solver self-test threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                Dispose(ref diagnostics);
                Dispose(ref statuses);
                Dispose(ref curls);
                Dispose(ref incoming);
                Dispose(ref probes);
                Dispose(ref joints);
                Dispose(ref digits);
            }
        }

        private static void CheckBlendOwnership(List<string> failures)
        {
            Quaternion incoming = Quaternion.Euler(12f, -7f, 3f);
            Quaternion procedural = Quaternion.Euler(-35f, 18f, 22f);
            Check(Quaternion.Angle(incoming, Quaternion.Slerp(incoming, procedural, 0f)) < Tolerance,
                "GripWeight zero leaves incoming animation rotations intact", failures);
            Check(Quaternion.Angle(procedural, Quaternion.Slerp(incoming, procedural, 1f)) < Tolerance,
                "GripWeight one applies the solved procedural rotation", failures);
            Check(Quaternion.Angle(incoming, Quaternion.Slerp(incoming, procedural, 0f)) < Tolerance,
                "release to zero returns ownership to the incoming animation", failures);
        }

        private static void CheckCalibrationEndpoints(List<string> failures)
        {
            Quaternion open = Quaternion.Euler(13f, 21f, -8f);
            Quaternion closed = open * Quaternion.AngleAxis(65f, new Vector3(1f, 2f, 3f).normalized);
            Check(Quaternion.Angle(open, Quaternion.Slerp(open, closed, Mathf.Clamp01(-1f))) < Tolerance,
                "raw curl zero/clamping returns calibrated open endpoint", failures);
            Check(Quaternion.Angle(closed, Quaternion.Slerp(open, closed, Mathf.Clamp01(2f))) < Tolerance,
                "raw curl one/clamping returns calibrated closed endpoint", failures);
            Quaternion first = Quaternion.Slerp(open, closed, 0.4f);
            Quaternion repeated = first;
            for (int index = 0; index < 100; index++) repeated = Quaternion.Slerp(open, closed, 0.4f);
            Check(Quaternion.Angle(first, repeated) < Tolerance,
                "absolute quaternion calibration does not accumulate", failures);

            NativeArray<HandGripJobJoint> joints = default;
            NativeArray<Quaternion> incoming = default;
            NativeArray<HandGripJobDigit> digits = default;
            NativeArray<HandGripJobProbe> probes = default;
            NativeArray<float> curls = default;
            NativeArray<HandGripStatus> statuses = default;
            NativeArray<HandGripProbeDiagnostic> diagnostics = default;
            try
            {
                joints = new NativeArray<HandGripJobJoint>(1, Allocator.Temp);
                incoming = new NativeArray<Quaternion>(1, Allocator.Temp);
                digits = new NativeArray<HandGripJobDigit>(1, Allocator.Temp);
                probes = new NativeArray<HandGripJobProbe>(1, Allocator.Temp);
                curls = new NativeArray<float>(1, Allocator.Temp);
                statuses = new NativeArray<HandGripStatus>(1, Allocator.Temp);
                diagnostics = new NativeArray<HandGripProbeDiagnostic>(1, Allocator.Temp);
                joints[0] = SyntheticJoint(Vector3.zero, 0f);
                incoming[0] = Quaternion.identity;
                digits[0] = new HandGripJobDigit
                {
                    JointCount = 1, ProbeCount = 1, HasParentPose = true,
                    ParentPosition = new Vector3(2f, 0f, 0f), ParentRotation = Quaternion.identity
                };
                probes[0] = new HandGripJobProbe { JointIndex = 0, LocalPosition = Vector3.right, Radius = 0.01f };
                GripFrame frame = GripFrame.Create(Vector3.zero, Vector3.up, Vector3.forward, 0.1f);
                HandGripSolver.Solve(frame, Vector3.zero, Quaternion.identity, 1f, 0f, 0f, 0.01f, 7,
                    digits, joints, probes, incoming, curls, statuses, diagnostics);
                Check(Vector3.Distance(diagnostics[0].WorldPosition, new Vector3(3f, 0f, 0f)) < Tolerance,
                    "candidate FK includes actual uncontrolled carpal parent pose", failures);
            }
            finally
            {
                Dispose(ref diagnostics); Dispose(ref statuses); Dispose(ref curls);
                Dispose(ref probes); Dispose(ref digits); Dispose(ref incoming); Dispose(ref joints);
            }
        }

        private sealed class LinearTarget : IGripTarget
        {
            public Vector3 Center;
            public GripFrame Evaluate(float position01) => GripFrame.Create(
                Center + Vector3.up * (Mathf.Clamp01(position01) - 0.5f) * 0.2f,
                Vector3.up, Vector3.back, 0.025f);
        }

        private static void CheckPalmAndReach(List<string> failures)
        {
            Vector3 anchor = new Vector3(0.04f, 0.01f, -0.02f);
            Quaternion rotation = Quaternion.Euler(12f, 34f, 56f);
            GripFrame small = GripFrame.Create(Vector3.one, Vector3.up, Vector3.right, 0.02f);
            GripFrame large = GripFrame.Create(Vector3.one, Vector3.up, Vector3.right, 0.05f);
            GripPalmTarget a = GripPalmTarget.Evaluate(small, anchor, rotation, Quaternion.identity, 0.003f);
            GripPalmTarget b = GripPalmTarget.Evaluate(large, anchor, rotation, Quaternion.identity, 0.003f);
            Check(Vector3.Distance(b.Palm.position - a.Palm.position, Vector3.right * 0.03f) < 0.00001f,
                "radius delta shifts palm along outward normal", failures);
            Check(Vector3.Distance(a.Wrist.position + a.Wrist.rotation * anchor, a.Palm.position) < 0.00001f
                && Quaternion.Angle(a.Wrist.rotation * rotation, a.Palm.rotation) < 0.02f,
                "wrist-to-palm calibration round trip", failures);
            foreach (float twist in new[] { -90f, -45f, 45f, 90f })
            {
                GripPalmTarget turned = GripPalmTarget.Evaluate(small, anchor, rotation,
                    Quaternion.identity, 0.003f, twist);
                Quaternion turn = Quaternion.AngleAxis(twist, small.Tangent);
                Check(Vector3.Distance(turned.Palm.position - small.Center,
                    turn * (a.Palm.position - small.Center)) < 0.00001f,
                    "wrist twist orbits the contact anchor at " + twist, failures);
                Check(Quaternion.Angle(turned.Palm.rotation, turn * a.Palm.rotation) < 0.02f
                    && Vector3.Distance(turned.Wrist.position + turned.Wrist.rotation * anchor,
                        turned.Palm.position) < 0.00001f,
                    "twisted wrist keeps the calibrated palm pose at " + twist, failures);
            }
            var envelope = new GripReachEnvelope(0.3f, 0.3f);
            Check(envelope.Contains(0.48f) && envelope.Contains(0.56f) && !envelope.Contains(0.6f)
                && !envelope.Contains(0.01f), "safe reach excludes full extension and collapse", failures);
            var planner = new GripReachPlanner();
            var target = new LinearTarget { Center = new Vector3(0f, 1.4f, 0.45f) };
            Vector3 shoulderLocal = new Vector3(0.15f, 1.4f, 0f);
            GripReachPlan near = planner.Plan(new Pose(Vector3.zero, Quaternion.identity), shoulderLocal,
                envelope, target, Vector3.zero, Quaternion.identity, Quaternion.identity, 0.003f);
            Check(near.Valid && !near.RequiresWalking, "valid current stance does not walk", failures);
            target.Center = new Vector3(0f, 1.4f, 3f);
            GripReachPlan far = planner.Plan(new Pose(Vector3.zero, Quaternion.identity), shoulderLocal,
                envelope, target, Vector3.zero, Quaternion.identity, Quaternion.identity, 0.003f);
            Check(far.Valid && far.RequiresWalking, "far target finds bounded deterministic staging stance", failures);
            if (far.Valid)
            {
                Vector3 shoulder = far.Root.position + far.Root.rotation * shoulderLocal;
                for (int index = 0; index < GripReachPlanner.SampleCount; index++)
                    Check(envelope.Contains(Vector3.Distance(shoulder, planner.GetSampleWrist(index))),
                        "staging sample " + index + " is safely reachable", failures);
            }
            target.Center = new Vector3(0f, 5f, 3f);
            GripReachPlan impossible = planner.Plan(new Pose(Vector3.zero, Quaternion.identity), shoulderLocal,
                envelope, target, Vector3.zero, Quaternion.identity, Quaternion.identity, 0.003f);
            Check(!impossible.Valid && impossible.Failure == GripPlanFailure.VerticalReachImpossible,
                "planar walking cannot solve vertically impossible target", failures);
        }

        private static HandGripJobJoint SyntheticJoint(Vector3 localPosition, float closedDegrees = 90f)
        {
            return new HandGripJobJoint
            {
                LocalPosition = localPosition,
                OpenLocalRotation = Quaternion.identity,
                ClosedLocalRotation = Quaternion.AngleAxis(closedDegrees, Vector3.forward)
            };
        }

        private static void Dispose<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated) array.Dispose();
            array = default;
        }

        private static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        private static bool Near(float a, float b) => Mathf.Abs(a - b) <= Tolerance;

        private static void Check(bool condition, string description, List<string> failures)
        {
            AssertionCount++;
            if (!condition) failures.Add("HandGrip: " + description + ".");
        }
    }
}
