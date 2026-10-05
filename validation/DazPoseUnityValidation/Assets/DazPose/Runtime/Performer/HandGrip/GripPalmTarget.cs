using System;
using UnityEngine;

namespace DazPose.Performer.HandGrip
{
    public readonly struct GripPalmTarget
    {
        public readonly Pose Palm;
        public readonly Pose Wrist;

        private GripPalmTarget(Pose palm, Pose wrist) { Palm = palm; Wrist = wrist; }

        public static GripPalmTarget Evaluate(in GripFrame frame, Vector3 anchorPosition,
            Quaternion anchorRotation, Quaternion frameCalibration, float clearance,
            float twistDegrees = 0f)
        {
            if (!frame.IsValid || !GripFrame.IsFinite(clearance) || clearance < 0f
                || !GripFrame.IsFinite(anchorPosition) || !GripFrame.IsFinite(twistDegrees)
                || !ValidRotation(anchorRotation) || !ValidRotation(frameCalibration))
                throw new ArgumentException("A finite valid grip frame and palm calibration are required.");
            anchorRotation = anchorRotation.normalized;
            frameCalibration = frameCalibration.normalized;
            Quaternion canonical = Quaternion.LookRotation(frame.Tangent, frame.Normal);
            // World-axis twist is premultiplied; postmultiplying a calibrated rotation
            // would rotate about the calibrated hand's axis instead of target tangent.
            Quaternion twist = Quaternion.AngleAxis(twistDegrees, frame.Tangent);
            Quaternion rotation = twist * canonical * frameCalibration;
            // Move the contact anchor around the same cylinder with its orientation.
            // Rotating at a fixed surface point would drive the fingers through it
            // at larger angles and make the contact solver reject the open pose.
            Vector3 position = frame.Center + (twist * frame.Normal) * (frame.Radius + clearance);
            Quaternion wristRotation = rotation * Quaternion.Inverse(anchorRotation);
            return new GripPalmTarget(new Pose(position, rotation),
                new Pose(position - wristRotation * anchorPosition, wristRotation));
        }

        private static bool ValidRotation(Quaternion value) => GripFrame.IsFinite(value.x)
            && GripFrame.IsFinite(value.y) && GripFrame.IsFinite(value.z) && GripFrame.IsFinite(value.w)
            && Quaternion.Dot(value, value) > 1e-8f;
    }
}
