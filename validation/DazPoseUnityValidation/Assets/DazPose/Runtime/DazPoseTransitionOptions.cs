using System;

namespace DazPose.UnityValidation
{
    [Serializable]
    public readonly struct DazPoseTransitionOptions : IEquatable<DazPoseTransitionOptions>
    {
        public float DurationSeconds { get; }
        public DazPoseBlendEase Ease { get; }
        public float WindupFraction { get; }
        public float OvershootFraction { get; }

        public DazPoseTransitionOptions(float durationSeconds, DazPoseBlendEase ease,
            float windupFraction, float overshootFraction)
        {
            ValidateFinite(durationSeconds, nameof(durationSeconds));
            ValidateFraction(windupFraction, nameof(windupFraction));
            ValidateFraction(overshootFraction, nameof(overshootFraction));
            if (durationSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration must be zero or greater.");
            if (ease != DazPoseBlendEase.Linear && ease != DazPoseBlendEase.SmoothStep)
                throw new ArgumentOutOfRangeException(nameof(ease), "Choose Linear or SmoothStep easing.");

            DurationSeconds = durationSeconds;
            Ease = ease;
            WindupFraction = windupFraction;
            OvershootFraction = overshootFraction;
        }

        public bool Equals(DazPoseTransitionOptions other)
        {
            return DurationSeconds.Equals(other.DurationSeconds)
                && Ease == other.Ease
                && WindupFraction.Equals(other.WindupFraction)
                && OvershootFraction.Equals(other.OvershootFraction);
        }

        public override bool Equals(object obj) => obj is DazPoseTransitionOptions other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = DurationSeconds.GetHashCode();
                hash = (hash * 397) ^ (int)Ease;
                hash = (hash * 397) ^ WindupFraction.GetHashCode();
                hash = (hash * 397) ^ OvershootFraction.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(DazPoseTransitionOptions left, DazPoseTransitionOptions right) => left.Equals(right);
        public static bool operator !=(DazPoseTransitionOptions left, DazPoseTransitionOptions right) => !left.Equals(right);

        private static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
        }

        private static void ValidateFraction(float value, string parameterName)
        {
            ValidateFinite(value, parameterName);
            if (value < 0f || value > 1f)
                throw new ArgumentOutOfRangeException(parameterName, "Windup and overshoot fractions must be between 0 and 1.");
        }
    }
}
