using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace DazPose.Performer.HandGrip
{
    internal struct HandGripAnimationJob : IAnimationJob
    {
        [ReadOnly] public NativeArray<HandGripJobDigit> Digits;
        [ReadOnly] public NativeArray<HandGripJobProbe> Probes;
        public NativeArray<HandGripJobJoint> Joints;
        public NativeArray<Quaternion> IncomingRotations;
        public NativeArray<float> SolvedCurls;
        public NativeArray<HandGripStatus> DigitStatuses;
        public NativeArray<HandGripProbeDiagnostic> ProbeDiagnostics;
        public TransformStreamHandle HandHandle;
        public GripFrame Frame;
        public float GripWeight;
        public float GripStrength;
        public float ContactClearance;
        public float NearContactDistance;
        public int BinarySearchIterations;
        public bool Enabled;

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!stream.isValid || !Enabled || !Frame.IsValid || GripWeight <= 0f
                || !HandHandle.IsValid(stream)) return;

            bool allHandlesValid = true;
            for (int index = 0; index < Joints.Length; index++)
            {
                HandGripJobJoint joint = Joints[index];
                if (!joint.Handle.IsValid(stream))
                {
                    DigitStatuses[joint.DigitIndex] = HandGripStatus.InvalidProfile;
                    allHandlesValid = false;
                    continue;
                }
                IncomingRotations[index] = joint.Handle.GetLocalRotation(stream);
            }
            if (!allHandlesValid) return;

            Vector3 handPosition = HandHandle.GetPosition(stream);
            Quaternion handRotation = HandHandle.GetRotation(stream);
            HandGripSolver.Solve(Frame, handPosition, handRotation, GripWeight, GripStrength,
                ContactClearance, NearContactDistance, BinarySearchIterations, Digits,
                Joints, Probes, IncomingRotations, SolvedCurls, DigitStatuses, ProbeDiagnostics);

            for (int digitIndex = 0; digitIndex < Digits.Length; digitIndex++)
            {
                if (DigitStatuses[digitIndex] == HandGripStatus.BasePenetration
                    || DigitStatuses[digitIndex] == HandGripStatus.InvalidProfile) continue;
                HandGripJobDigit digit = Digits[digitIndex];
                float curl = SolvedCurls[digitIndex];
                for (int localIndex = 0; localIndex < digit.JointCount; localIndex++)
                {
                    int jointIndex = digit.JointStart + localIndex;
                    HandGripJobJoint joint = Joints[jointIndex];
                    if (!joint.Handle.IsValid(stream)) continue;
                    Quaternion procedural = Quaternion.Slerp(joint.OpenLocalRotation,
                        joint.ClosedLocalRotation, curl);
                    joint.Handle.SetLocalRotation(stream, Quaternion.Slerp(
                        IncomingRotations[jointIndex], procedural, GripWeight));
                }
            }
        }
    }
}
