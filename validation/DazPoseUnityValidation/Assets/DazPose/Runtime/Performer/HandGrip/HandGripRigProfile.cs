using System;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.Performer.HandGrip
{
    /// <summary>Project-owned calibration for one Generic-rig hand.</summary>
    [CreateAssetMenu(menuName = "DAZ Pose/Performer/Hand Grip Rig Profile", fileName = "HandGripRigProfile")]
    public sealed class HandGripRigProfile : ScriptableObject
    {
        [SerializeField] private string handPath;
        [SerializeField] private HandGripDigitProfile[] digits = Array.Empty<HandGripDigitProfile>();
        [SerializeField] private Vector3 gripCenterLocalPosition;
        [SerializeField] private Quaternion gripCenterLocalRotation = Quaternion.identity;
        [SerializeField, Min(0f)] private float contactClearance = 0.003f;
        [SerializeField, Min(0f)] private float nearContactDistance = 0.006f;
        [SerializeField, Min(0f)] private float gripInSeconds = 0.25f;
        [SerializeField, Min(0f)] private float releaseSeconds = 0.25f;
        [SerializeField, Range(1, 16)] private int binarySearchIterations = 7;

        public string HandPath => handPath;
        public HandGripDigitProfile[] Digits => digits ?? Array.Empty<HandGripDigitProfile>();
        public Vector3 GripCenterLocalPosition => gripCenterLocalPosition;
        public Quaternion GripCenterLocalRotation => gripCenterLocalRotation;
        public float ContactClearance => contactClearance;
        public float NearContactDistance => nearContactDistance;
        public float GripInSeconds => gripInSeconds;
        public float ReleaseSeconds => releaseSeconds;
        public int BinarySearchIterations => Mathf.Clamp(binarySearchIterations, 1, 16);

        public bool IsReady(Animator animator, out string reason)
        {
            if (!TryValidate(animator, out _, out reason)) return false;
            reason = null;
            return true;
        }

        internal bool TryValidate(Animator animator, out Transform hand, out string reason)
        {
            hand = null;
            if (animator == null)
            {
                reason = "A Generic Animator is required.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(handPath))
            {
                reason = "The hand path is empty.";
                return false;
            }
            hand = FindByPath(animator.transform, handPath);
            if (hand == null)
            {
                reason = "The configured hand path '" + handPath + "' could not be resolved under the Animator.";
                return false;
            }
            if (digits == null || digits.Length != 5)
            {
                reason = "A profile must contain exactly five digit calibrations.";
                return false;
            }

            var seenDigits = new bool[5];
            var seenPaths = new HashSet<string>(StringComparer.Ordinal);
            for (int digitIndex = 0; digitIndex < digits.Length; digitIndex++)
            {
                HandGripDigitProfile digit = digits[digitIndex];
                if (digit == null || (int)digit.Digit < 0 || (int)digit.Digit >= seenDigits.Length
                    || seenDigits[(int)digit.Digit])
                {
                    reason = "Digit entries must uniquely cover Thumb, Index, Middle, Ring and Little.";
                    return false;
                }
                seenDigits[(int)digit.Digit] = true;
                int jointCount = digit.JointPaths.Length;
                if (jointCount == 0 || digit.JointLocalPositions.Length != jointCount
                    || digit.OpenLocalRotations.Length != jointCount || digit.ClosedLocalRotations.Length != jointCount)
                {
                    reason = digit.Digit + " calibration must contain matching bone paths, offsets and open/closed rotations.";
                    return false;
                }
                Transform previousJoint = null;
                for (int jointIndex = 0; jointIndex < jointCount; jointIndex++)
                {
                    string path = digit.JointPaths[jointIndex];
                    if (string.IsNullOrWhiteSpace(path) || !seenPaths.Add(path)
                        || FindByPath(animator.transform, path) == null)
                    {
                        reason = digit.Digit + " joint path '" + path + "' is missing or duplicated.";
                        return false;
                    }
                    if (!IsFinite(digit.JointLocalPositions[jointIndex])
                        || !IsFinite(digit.OpenLocalRotations[jointIndex])
                        || !IsFinite(digit.ClosedLocalRotations[jointIndex]))
                    {
                        reason = digit.Digit + " calibration contains non-finite joint data.";
                        return false;
                    }
                    Transform bone = FindByPath(animator.transform, path);
                    if (!bone.IsChildOf(hand))
                    {
                        reason = digit.Digit + " joint '" + bone.name + "' is outside the configured hand subtree.";
                        return false;
                    }
                    if (previousJoint != null && !bone.IsChildOf(previousJoint))
                    {
                        reason = digit.Digit + " joints are not ordered from proximal to distal.";
                        return false;
                    }
                    previousJoint = bone;
                }
                if (digit.Probes.Length == 0)
                {
                    reason = digit.Digit + " must have at least one virtual contact probe.";
                    return false;
                }
                foreach (HandGripProbeDefinition probe in digit.Probes)
                {
                    if (probe.jointIndex < 0 || probe.jointIndex >= jointCount
                        || !IsFinite(probe.localPosition) || !GripFrame.IsFinite(probe.radius) || probe.radius <= 0f)
                    {
                        reason = digit.Digit + " contains an invalid contact probe.";
                        return false;
                    }
                }
            }
            if (!GripFrame.IsFinite(contactClearance) || contactClearance < 0f
                || !GripFrame.IsFinite(nearContactDistance) || nearContactDistance < 0f
                || !GripFrame.IsFinite(gripInSeconds) || gripInSeconds < 0f
                || !GripFrame.IsFinite(releaseSeconds) || releaseSeconds < 0f)
            {
                reason = "Clearance, contact range and blend durations must be finite and nonnegative.";
                return false;
            }
            reason = null;
            return true;
        }

        public void ConfigureForEditor(string resolvedHandPath, HandGripDigitProfile[] fingerProfiles,
            Vector3 centerLocalPosition, Quaternion centerLocalRotation, float clearance = 0.003f,
            float nearDistance = 0.006f, float blendIn = 0.25f, float blendOut = 0.25f, int iterations = 7)
        {
            handPath = resolvedHandPath;
            digits = fingerProfiles ?? Array.Empty<HandGripDigitProfile>();
            gripCenterLocalPosition = centerLocalPosition;
            gripCenterLocalRotation = centerLocalRotation;
            contactClearance = Mathf.Max(0f, clearance);
            nearContactDistance = Mathf.Max(0f, nearDistance);
            gripInSeconds = Mathf.Max(0f, blendIn);
            releaseSeconds = Mathf.Max(0f, blendOut);
            binarySearchIterations = Mathf.Clamp(iterations, 1, 16);
        }

        public void ReplaceDigitForEditor(HandGripDigitProfile replacement)
        {
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            var copy = new List<HandGripDigitProfile>(Digits);
            int index = copy.FindIndex(item => item != null && item.Digit == replacement.Digit);
            if (index < 0) copy.Add(replacement);
            else copy[index] = replacement;
            digits = copy.ToArray();
        }

        private static Transform FindByPath(Transform root, string path)
        {
            Transform current = root;
            foreach (string segment in path.Split('/'))
            {
                current = current.Find(segment);
                if (current == null) return null;
            }
            return current;
        }

        private static bool IsFinite(Vector3 value) => GripFrame.IsFinite(value);

        private static bool IsFinite(Quaternion value) => GripFrame.IsFinite(value.x)
            && GripFrame.IsFinite(value.y) && GripFrame.IsFinite(value.z) && GripFrame.IsFinite(value.w)
            && (value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w) > 1e-8f;
    }
}
