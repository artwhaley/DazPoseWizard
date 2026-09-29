using UnityEngine;

namespace DazPose.Performer
{
    public static class PoseTransitionTrajectory
    {
        public const float WindupPhaseFraction = 0.15f;
        public const float SettlePhaseFraction = 0.20f;

        public static float Evaluate(float normalizedTime, PoseTransition transition)
        {
            if (normalizedTime <= 0f) return 0f;
            if (normalizedTime >= 1f) return 1f;

            var hasWindup = transition.Windup > 0f;
            var hasOvershoot = transition.Overshoot > 0f;
            if (!hasWindup && !hasOvershoot)
                return EvaluateDriveCurve(normalizedTime, transition.EffectiveCurve);

            var driveStart = hasWindup ? WindupPhaseFraction : 0f;
            var driveEnd = hasOvershoot ? 1f - SettlePhaseFraction : 1f;

            if (hasWindup && normalizedTime < driveStart)
            {
                var phaseTime = normalizedTime / driveStart;
                return Mathf.LerpUnclamped(0f, -transition.Windup, SmoothStep(phaseTime));
            }

            if (hasOvershoot && normalizedTime > driveEnd)
            {
                var phaseTime = (normalizedTime - driveEnd) / SettlePhaseFraction;
                return Mathf.LerpUnclamped(1f + transition.Overshoot, 1f, SmoothStep(phaseTime));
            }

            var driveTime = (normalizedTime - driveStart) / (driveEnd - driveStart);
            var driveFrom = hasWindup ? -transition.Windup : 0f;
            var driveTo = hasOvershoot ? 1f + transition.Overshoot : 1f;
            return Mathf.LerpUnclamped(driveFrom, driveTo,
                EvaluateDriveCurve(driveTime, transition.EffectiveCurve));
        }

        private static float EvaluateDriveCurve(float time, AnimationCurve curve)
        {
            if (time <= 0f) return 0f;
            if (time >= 1f) return 1f;

            var start = curve.Evaluate(0f);
            var end = curve.Evaluate(1f);
            var value = curve.Evaluate(Mathf.Clamp01(time));
            if (float.IsNaN(start) || float.IsInfinity(start)
                || float.IsNaN(end) || float.IsInfinity(end)
                || float.IsNaN(value) || float.IsInfinity(value))
                return time;

            var range = end - start;
            if (Mathf.Abs(range) < 1e-6f) return time;
            return Mathf.Clamp01((value - start) / range);
        }

        private static float SmoothStep(float time)
        {
            var value = Mathf.Clamp01(time);
            return value * value * (3f - 2f * value);
        }
    }
}
