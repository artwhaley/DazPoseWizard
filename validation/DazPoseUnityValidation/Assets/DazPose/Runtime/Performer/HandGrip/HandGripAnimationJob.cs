using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace DazPose.Performer.HandGrip
{
    internal struct HandGripAnimationJob : IAnimationJob
    {
        public NativeArray<HandGripJobDigit> Digits;
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
        public UnityEngine.Animations.Rigging.FloatProperty ArmEvaluationMarker;
        public bool CalibrationMode;
        public Vector4 RawCurls;
        public float LittleCurl;

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!stream.isValid || !Enabled || (!CalibrationMode && !Frame.IsValid) || GripWeight <= 0f
                || !HandHandle.IsValid(stream)) return;

            if (!CalibrationMode && ArmEvaluationMarker.Get(stream) < 0.5f)
            {
                for (int digit = 0; digit < DigitStatuses.Length; digit++)
                    DigitStatuses[digit] = HandGripStatus.InvalidProfile;
                return;
            }
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
                joint.LocalPosition = joint.Handle.GetLocalPosition(stream);
                Joints[index] = joint;
            }
            if (!allHandlesValid) return;

            Vector3 handPosition = HandHandle.GetPosition(stream);
            Quaternion handRotation = HandHandle.GetRotation(stream);
            for (int index = 0; index < Digits.Length; index++)
            {
                HandGripJobDigit digit = Digits[index];
                if (!digit.ParentHandle.IsValid(stream))
                {
                    DigitStatuses[index] = HandGripStatus.InvalidProfile;
                    return;
                }
                digit.ParentPosition = digit.ParentHandle.GetPosition(stream);
                digit.ParentRotation = digit.ParentHandle.GetRotation(stream);
                digit.HasParentPose = true;
                Digits[index] = digit;
            }
            if (CalibrationMode)
            {
                for (int index = 0; index < Digits.Length; index++)
                {
                    SolvedCurls[index] = Mathf.Clamp01(index < 4 ? RawCurls[index] : LittleCurl);
                    DigitStatuses[index] = HandGripStatus.Clear;
                }
            }
            else HandGripSolver.Solve(Frame, handPosition, handRotation, GripWeight, GripStrength,
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
