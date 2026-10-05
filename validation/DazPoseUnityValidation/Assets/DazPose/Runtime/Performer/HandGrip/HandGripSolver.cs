using Unity.Collections;
using UnityEngine;

namespace DazPose.Performer.HandGrip
{
    /// <summary>Allocation-free analytic cylinder contact and bounded per-digit curl solve.</summary>
    internal static class HandGripSolver
    {
        private const float ContactEpsilon = 0.00001f;

        public static float RadialDistance(in GripFrame frame, Vector3 point)
        {
            Vector3 offset = point - frame.Center;
            Vector3 radial = offset - frame.Tangent * Vector3.Dot(offset, frame.Tangent);
            return radial.magnitude;
        }

        public static HandGripProbeState ClassifyProbe(in GripFrame frame, Vector3 point,
            float probeRadius, float clearance, float nearContactDistance)
        {
            float radialDistance = RadialDistance(frame, point);
            float allowed = frame.Radius + probeRadius + clearance;
            if (radialDistance < allowed - ContactEpsilon) return HandGripProbeState.Penetrating;
            if (radialDistance <= allowed + Mathf.Max(0f, nearContactDistance))
                return HandGripProbeState.NearContact;
            return HandGripProbeState.Clear;
        }

        public static void Solve(in GripFrame frame, Vector3 handPosition, Quaternion handRotation,
            float gripWeight, float gripStrength, float clearance, float nearContactDistance,
            int binarySearchIterations, NativeArray<HandGripJobDigit> digits,
            NativeArray<HandGripJobJoint> joints, NativeArray<HandGripJobProbe> probes,
            NativeArray<Quaternion> incomingRotations, NativeArray<float> solvedCurls,
            NativeArray<HandGripStatus> digitStatuses, NativeArray<HandGripProbeDiagnostic> diagnostics)
        {
            float weight = Mathf.Clamp01(gripWeight);
            float strength = Mathf.Clamp01(gripStrength);
            int iterations = Mathf.Clamp(binarySearchIterations, 1, 16);
            for (int digitIndex = 0; digitIndex < digits.Length; digitIndex++)
            {
                HandGripJobDigit digit = digits[digitIndex];
                float desiredCurl = Mathf.Clamp01(strength + digit.CurlBias);
                bool openPenetrates = PosePenetrates(frame, handPosition, handRotation,
                    weight, 0f, clearance, nearContactDistance, digit, joints, probes,
                    incomingRotations, default, false, out _);
                if (openPenetrates)
                {
                    solvedCurls[digitIndex] = 0f;
                    digitStatuses[digitIndex] = HandGripStatus.BasePenetration;
                    WriteDiagnostics(frame, handPosition, handRotation, weight, 0f, clearance,
                        nearContactDistance, digitIndex, digit, joints, probes, incomingRotations, diagnostics);
                    continue;
                }

                float solved = desiredCurl;
                bool requestedPenetrates = desiredCurl > 0f && PosePenetrates(frame,
                    handPosition, handRotation, weight, desiredCurl, clearance, nearContactDistance,
                    digit, joints, probes, incomingRotations, default, false, out _);
                if (requestedPenetrates)
                {
                    float low = 0f;
                    float high = desiredCurl;
                    for (int iteration = 0; iteration < iterations; iteration++)
                    {
                        float candidate = (low + high) * 0.5f;
                        if (PosePenetrates(frame, handPosition, handRotation, weight, candidate,
                                clearance, nearContactDistance, digit, joints, probes,
                                incomingRotations, default, false, out _))
                            high = candidate;
                        else
                            low = candidate;
                    }
                    solved = low;
                }

                solvedCurls[digitIndex] = solved;
                bool nearContact = WriteDiagnostics(frame, handPosition, handRotation, weight,
                    solved, clearance, nearContactDistance, digitIndex, digit, joints,
                    probes, incomingRotations, diagnostics);
                digitStatuses[digitIndex] = requestedPenetrates || nearContact
                    ? HandGripStatus.Contact : HandGripStatus.Clear;
            }
        }

        private static bool PosePenetrates(in GripFrame frame, Vector3 handPosition,
            Quaternion handRotation, float gripWeight, float curl, float clearance,
            float nearContactDistance, HandGripJobDigit digit,
            NativeArray<HandGripJobJoint> joints, NativeArray<HandGripJobProbe> probes,
            NativeArray<Quaternion> incomingRotations,
            NativeArray<HandGripProbeDiagnostic> diagnostics, bool writeDiagnostics,
            out bool nearContact)
        {
            nearContact = false;
            bool penetrates = false;
            for (int localProbeIndex = 0; localProbeIndex < digit.ProbeCount; localProbeIndex++)
            {
                HandGripJobProbe probe = probes[digit.ProbeStart + localProbeIndex];
                Vector3 point = GetProbeWorldPosition(handPosition, handRotation, gripWeight,
                    curl, probe, digit, joints, incomingRotations);
                float radialDistance = RadialDistance(frame, point);
                float allowed = frame.Radius + probe.Radius + clearance;
                HandGripProbeState state;
                if (radialDistance < allowed - ContactEpsilon)
                {
                    state = HandGripProbeState.Penetrating;
                    penetrates = true;
                }
                else if (radialDistance <= allowed + Mathf.Max(0f, nearContactDistance))
                {
                    state = HandGripProbeState.NearContact;
                    nearContact = true;
                }
                else state = HandGripProbeState.Clear;

                if (writeDiagnostics && diagnostics.IsCreated)
                    diagnostics[digit.ProbeStart + localProbeIndex] = new HandGripProbeDiagnostic
                    {
                        WorldPosition = point,
                        RadialDistance = radialDistance,
                        AllowedDistance = allowed,
                        State = state
                    };
            }
            return penetrates;
        }

        private static bool WriteDiagnostics(in GripFrame frame, Vector3 handPosition,
            Quaternion handRotation, float gripWeight, float curl, float clearance,
            float nearContactDistance, int digitIndex, HandGripJobDigit digit,
            NativeArray<HandGripJobJoint> joints, NativeArray<HandGripJobProbe> probes,
            NativeArray<Quaternion> incomingRotations,
            NativeArray<HandGripProbeDiagnostic> diagnostics)
        {
            PosePenetrates(frame, handPosition, handRotation, gripWeight, curl, clearance,
                nearContactDistance, digit, joints, probes, incomingRotations, diagnostics,
                true, out bool nearContact);
            return nearContact;
        }

        private static Vector3 GetProbeWorldPosition(Vector3 handPosition, Quaternion handRotation,
            float gripWeight, float curl, HandGripJobProbe probe, HandGripJobDigit digit,
            NativeArray<HandGripJobJoint> joints, NativeArray<Quaternion> incomingRotations)
        {
            Vector3 parentPosition = handPosition;
            Quaternion parentRotation = handRotation;
            for (int jointIndex = digit.JointStart; jointIndex <= probe.JointIndex; jointIndex++)
            {
                HandGripJobJoint joint = joints[jointIndex];
                Vector3 jointPosition = parentPosition + parentRotation * joint.LocalPosition;
                Quaternion closedRotation = Quaternion.Slerp(joint.OpenLocalRotation,
                    joint.ClosedLocalRotation, curl);
                Quaternion appliedRotation = Quaternion.Slerp(incomingRotations[jointIndex],
                    closedRotation, gripWeight);
                parentPosition = jointPosition;
                parentRotation = parentRotation * appliedRotation;
            }
            return parentPosition + parentRotation * probe.LocalPosition;
        }
    }
}
