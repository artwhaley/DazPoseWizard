using System;
using UnityEngine;

namespace DazPose.Performer.HandGrip
{
    public enum HandGripDigit
    {
        Thumb = 0,
        Index = 1,
        Middle = 2,
        Ring = 3,
        Little = 4
    }

    public enum HandGripStatus
    {
        Clear,
        Contact,
        BasePenetration,
        InvalidTarget,
        InvalidProfile
    }

    public enum HandGripProbeState
    {
        Clear,
        NearContact,
        Penetrating
    }

    [Serializable]
    public struct HandGripProbeDefinition
    {
        [Min(0)] public int jointIndex;
        public Vector3 localPosition;
        [Min(0f)] public float radius;

        public HandGripProbeDefinition(int jointIndex, Vector3 localPosition, float radius)
        {
            this.jointIndex = jointIndex;
            this.localPosition = localPosition;
            this.radius = radius;
        }
    }

    [Serializable]
    public sealed class HandGripDigitProfile
    {
        [SerializeField] private HandGripDigit digit;
        [SerializeField] private string[] jointPaths = Array.Empty<string>();
        [SerializeField] private Vector3[] jointLocalPositions = Array.Empty<Vector3>();
        [SerializeField] private Quaternion[] openLocalRotations = Array.Empty<Quaternion>();
        [SerializeField] private Quaternion[] closedLocalRotations = Array.Empty<Quaternion>();
        [SerializeField, Range(-0.5f, 0.5f)] private float curlBias;
        [SerializeField] private HandGripProbeDefinition[] probes = Array.Empty<HandGripProbeDefinition>();

        public HandGripDigit Digit => digit;
        public string[] JointPaths => jointPaths ?? Array.Empty<string>();
        public Vector3[] JointLocalPositions => jointLocalPositions ?? Array.Empty<Vector3>();
        public Quaternion[] OpenLocalRotations => openLocalRotations ?? Array.Empty<Quaternion>();
        public Quaternion[] ClosedLocalRotations => closedLocalRotations ?? Array.Empty<Quaternion>();
        public float CurlBias => curlBias;
        public HandGripProbeDefinition[] Probes => probes ?? Array.Empty<HandGripProbeDefinition>();

        public void ConfigureForEditor(HandGripDigit digit, string[] paths, Vector3[] localPositions,
            Quaternion[] open, Quaternion[] closed, HandGripProbeDefinition[] probeDefinitions,
            float perDigitCurlBias = 0f)
        {
            this.digit = digit;
            jointPaths = paths ?? Array.Empty<string>();
            jointLocalPositions = localPositions ?? Array.Empty<Vector3>();
            openLocalRotations = open ?? Array.Empty<Quaternion>();
            closedLocalRotations = closed ?? Array.Empty<Quaternion>();
            probes = probeDefinitions ?? Array.Empty<HandGripProbeDefinition>();
            curlBias = Mathf.Clamp(perDigitCurlBias, -0.5f, 0.5f);
        }

        public void CaptureOpenForEditor(Quaternion[] values)
        {
            if (values == null || values.Length != jointPaths.Length)
                throw new ArgumentException("Open calibration must include one rotation for each configured joint.", nameof(values));
            openLocalRotations = (Quaternion[])values.Clone();
        }

        public void CaptureClosedForEditor(Quaternion[] values)
        {
            if (values == null || values.Length != jointPaths.Length)
                throw new ArgumentException("Closed calibration must include one rotation for each configured joint.", nameof(values));
            closedLocalRotations = (Quaternion[])values.Clone();
        }

        public void SetClosedRotationForEditor(int index, Quaternion value)
        {
            if (index < 0 || index >= closedLocalRotations.Length) throw new ArgumentOutOfRangeException(nameof(index));
            closedLocalRotations[index] = value;
        }
    }

    internal struct HandGripJobDigit
    {
        public int JointStart;
        public int JointCount;
        public int ProbeStart;
        public int ProbeCount;
        public float CurlBias;
    }

    internal struct HandGripJobJoint
    {
        public UnityEngine.Animations.TransformStreamHandle Handle;
        public int DigitIndex;
        public Vector3 LocalPosition;
        public Quaternion OpenLocalRotation;
        public Quaternion ClosedLocalRotation;
    }

    internal struct HandGripJobProbe
    {
        public int DigitIndex;
        public int JointIndex;
        public Vector3 LocalPosition;
        public float Radius;
    }

    internal struct HandGripProbeDiagnostic
    {
        public Vector3 WorldPosition;
        public float RadialDistance;
        public float AllowedDistance;
        public HandGripProbeState State;
    }

    public readonly struct HandGripSolveResult
    {
        public readonly HandGripStatus Status;
        public readonly float ThumbCurl;
        public readonly float IndexCurl;
        public readonly float MiddleCurl;
        public readonly float RingCurl;
        public readonly float LittleCurl;

        public HandGripSolveResult(HandGripStatus status, float thumbCurl, float indexCurl,
            float middleCurl, float ringCurl, float littleCurl)
        {
            Status = status;
            ThumbCurl = thumbCurl;
            IndexCurl = indexCurl;
            MiddleCurl = middleCurl;
            RingCurl = ringCurl;
            LittleCurl = littleCurl;
        }

        public float GetCurl(HandGripDigit digit)
        {
            switch (digit)
            {
                case HandGripDigit.Thumb: return ThumbCurl;
                case HandGripDigit.Index: return IndexCurl;
                case HandGripDigit.Middle: return MiddleCurl;
                case HandGripDigit.Ring: return RingCurl;
                case HandGripDigit.Little: return LittleCurl;
                default: throw new ArgumentOutOfRangeException(nameof(digit));
            }
        }
    }

    public readonly struct HandGripAlignmentDiagnostics
    {
        public readonly Vector3 DesiredGripCenter;
        public readonly Vector3 ActualGripCenter;
        public readonly float PositionErrorMeters;
        public readonly float OrientationErrorDegrees;
        public readonly float RollErrorDegrees;

        public HandGripAlignmentDiagnostics(Vector3 desired, Vector3 actual,
            float positionError, float orientationError, float rollError)
        {
            DesiredGripCenter = desired;
            ActualGripCenter = actual;
            PositionErrorMeters = positionError;
            OrientationErrorDegrees = orientationError;
            RollErrorDegrees = rollError;
        }
    }
}
