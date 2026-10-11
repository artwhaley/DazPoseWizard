using System;
using System.Text;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer.HandGrip
{
    public static class HandGripSpatialProof
    {
        public static string Run(Animator animator, HandGripRigProfile profile, AnimationClip baseClip)
        {                var report = new StringBuilder();
            string[] expressionFailures = PerformerExpressionRuntimeSelfTests.Run();
            if (expressionFailures.Length != 0) throw new InvalidOperationException(string.Join("\n", expressionFailures));
            report.AppendLine("PASS: existing expression runtime regression checks.");
            PlayableGraph graph = PlayableGraph.Create("Grip Spatial Proof");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            PerformerArmIKLayer arm = null;
            PerformerHandGripLayer fingers = null;
            try
            {
                var source = AnimationClipPlayable.Create(graph, baseClip);
                source.SetSpeed(0d);
                arm = new PerformerArmIKLayer(graph, source, animator);
                fingers = new PerformerHandGripLayer(graph, arm.Output, animator, profile);
                var output = AnimationPlayableOutput.Create(graph, "Spatial Proof", animator);
                output.SetSourcePlayable(fingers.OutputPlayable);
                graph.Play(); graph.Evaluate(0f);
                Vector3 center = arm.Upper.position + animator.transform.forward * (arm.UpperLength + arm.LowerLength) * 0.7f;
                Vector3 normal = -animator.transform.forward;
                float[] radii = { 0.015f, 0.025f, 0.035f };
                float previousIndexCurl = 1f;
                foreach (float radius in radii)
                for (int index = 0; index < 5; index++)
                {
                    GripFrame frame = GripFrame.Create(center + Vector3.up * ((index / 4f - 0.5f) * 0.12f), Vector3.up, normal, radius);
                    GripPalmTarget desired = profile.EvaluatePalm(frame);
                    arm.SetTarget(desired.Wrist, arm.Upper.position + animator.transform.rotation * profile.ElbowHintLocalDirection * 0.5f, 1f);
                    fingers.SetState(true, frame, 1f, 1f, HandGripStatus.Clear);
                    graph.Evaluate(0f);
                    Vector3 actual = arm.Hand.TransformPoint(profile.PalmAnchorLocalPosition);
                    float positionError = Vector3.Distance(actual, desired.Palm.position);
                    float angle = Quaternion.Angle(arm.Hand.rotation * profile.PalmAnchorLocalRotation, desired.Palm.rotation);
                    var solve = fingers.GetSolveResult();
                    report.AppendLine("radius=" + radius.ToString("F3") + " p=" + (index / 4f).ToString("F2")
                        + " error_mm=" + (positionError * 1000f).ToString("F3") + " angle=" + angle.ToString("F3")
                        + " contact=" + solve.Status + " curls=" + solve.ThumbCurl.ToString("F3") + "/" + solve.IndexCurl.ToString("F3")
                        + "/" + solve.MiddleCurl.ToString("F3") + "/" + solve.RingCurl.ToString("F3") + "/" + solve.LittleCurl.ToString("F3"));
                    for (int probe = 0; probe < fingers.ProbeCount; probe++)
                        if (fingers.TryGetProbeDiagnostic(probe, out Vector3 point, out HandGripProbeState status))
                        {
                            if (status == HandGripProbeState.Penetrating)
                                throw new InvalidOperationException("Accepted probe penetrates: " + probe + "\n" + report);
                            report.AppendLine(" probe=" + probe + " radial=" + HandGripSolver.RadialDistance(frame, point).ToString("F5")
                                + " allowed=" + (radius + fingers.GetProbeRadius(probe) + profile.ContactClearance).ToString("F5") + " " + status);
                        }
                    if (index == 0)
                    {
                        if (solve.IndexCurl > previousIndexCurl + 0.001f)
                            throw new InvalidOperationException("Larger radius increased wrapping curl.");
                        previousIndexCurl = solve.IndexCurl;
                    }
                    if (solve.Status != HandGripStatus.Contact)
                        throw new InvalidOperationException("Contact acquisition failed: " + solve.Status + "\n" + report);
                    if (positionError > 0.005f || angle > 5f || !GripFrame.IsFinite(actual))
                        throw new InvalidOperationException("Actual Generic arm IK failed spatial tolerance.\n" + report);
                }
                foreach (float tilt in new[] { 0f, 30f })
                foreach (float twist in new[] { -90f, -45f, 0f, 45f, 90f })
                {
                    Quaternion tiltRotation = Quaternion.AngleAxis(tilt, animator.transform.right);
                    GripFrame frame = GripFrame.Create(center, tiltRotation * Vector3.up, tiltRotation * normal, 0.025f);
                    GripPalmTarget desired = profile.EvaluatePalm(frame, twist);
                    arm.SetTarget(desired.Wrist, arm.Upper.position + animator.transform.rotation * profile.ElbowHintLocalDirection * 0.5f, 1f);
                    fingers.SetState(true, frame, 1f, 1f, HandGripStatus.Clear);
                    graph.Evaluate(0f);
                    float error = Vector3.Distance(arm.Hand.TransformPoint(profile.PalmAnchorLocalPosition), desired.Palm.position);
                    var solve = fingers.GetSolveResult();
                    for (int digit = 0; digit < 5; digit++)
                        if (fingers.GetDigitStatus(digit) != HandGripStatus.Contact)
                            throw new InvalidOperationException("Digit " + digit + " did not acquire contact at twist " + twist);
                    report.AppendLine("tilt=" + tilt + " twist=" + twist + " error_mm=" + error * 1000f + " contact=" + solve.Status);
                    if (error > 0.005f || solve.Status != HandGripStatus.Contact)
                        throw new InvalidOperationException("Tilt/twist acceptance failed.\n" + report);
                }
                var watch = System.Diagnostics.Stopwatch.StartNew();
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                for (int iteration = 0; iteration < 1000; iteration++) graph.Evaluate(0f);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                watch.Stop();
                report.AppendLine("1000 evaluated IK/contact frames: " + watch.Elapsed.TotalMilliseconds.ToString("F2")
                    + " ms; main-thread managed allocation=" + allocated + " bytes (editor graph evaluation, not player profiler).");
                report.AppendLine("PASS: actual Generic arm position/orientation and valid contact, 15 radius/path combinations plus ten tilt/twist combinations.");
                return report.ToString();
            }
            finally
            {
                fingers?.Dispose(); arm?.Dispose();
                if (graph.IsValid()) graph.Destroy();
            }
        }
    }
}
