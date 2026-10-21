using System;
using UnityEngine;

namespace DazPose.Player
{
    /// <summary>A finite, clamped transition used by directable view commands.</summary>
    [Serializable]
    public struct ViewTransition
    {
        [Min(0f)] public float Duration;
        public AnimationCurve Curve;

        public static ViewTransition Snap => new ViewTransition { Duration = 0f };

        public static ViewTransition EaseInOut(float duration)
        {
            ValidateDuration(duration, nameof(duration));
            return new ViewTransition
            {
                Duration = duration,
                // Cubic Hermite with zero endpoint tangents is smoothstep: t²(3 - 2t).
                Curve = new AnimationCurve(
                    new Keyframe(0f, 0f, 0f, 0f),
                    new Keyframe(1f, 1f, 0f, 0f))
            };
        }

        internal void Validate(string parameterName)
        {
            ValidateDuration(Duration, parameterName);
        }

        internal float Evaluate(float normalizedTime)
        {
            float t = Mathf.Clamp01(normalizedTime);
            if (Duration <= 0f) return 1f;
            if (Curve == null) return SmoothStep(t);

            float value = Curve.Evaluate(t);
            if (float.IsNaN(value) || float.IsInfinity(value)) return SmoothStep(t);
            return Mathf.Clamp01(value);
        }

        internal static void ValidateDuration(float duration, string parameterName)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f)
                throw new ArgumentOutOfRangeException(parameterName, duration,
                    "A view transition duration must be finite and non-negative. Use zero to snap immediately.");
        }

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);
    }
}
