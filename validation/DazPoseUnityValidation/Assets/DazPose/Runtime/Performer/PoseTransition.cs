using System;
using UnityEngine;

namespace DazPose.Performer
{
    [Serializable]
    public struct PoseTransition
    {
        [Min(0f)] public float Duration;
        [Range(0f, 1f)] public float Windup;
        [Range(0f, 1f)] public float Overshoot;
        public AnimationCurve Curve;

        public static PoseTransition Default => new PoseTransition(
            0.7f, 0f, 0f, CreateDefaultCurve());

        public static PoseTransition Snap => new PoseTransition(
            0f, 0f, 0f, CreateDefaultCurve());

        public static PoseTransition Smooth(float duration)
        {
            return new PoseTransition(duration, 0f, 0f, CreateDefaultCurve());
        }

        public PoseTransition(float duration, float windup, float overshoot, AnimationCurve curve)
        {
            Duration = duration;
            Windup = windup;
            Overshoot = overshoot;
            Curve = curve;
        }

        public void Validate()
        {
            ValidateFinite(Duration, nameof(Duration));
            ValidateFraction(Windup, nameof(Windup));
            ValidateFraction(Overshoot, nameof(Overshoot));
            if (Duration < 0f)
                throw new ArgumentOutOfRangeException(nameof(Duration), "Duration must be zero or greater.");
        }

        internal AnimationCurve EffectiveCurve => Curve ?? CreateDefaultCurve();

        private static AnimationCurve CreateDefaultCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        private static void ValidateFraction(float value, string parameterName)
        {
            ValidateFinite(value, parameterName);
            if (value < 0f || value > 1f)
                throw new ArgumentOutOfRangeException(parameterName,
                    "Windup and overshoot must be between zero and one.");
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
        }
    }
}
