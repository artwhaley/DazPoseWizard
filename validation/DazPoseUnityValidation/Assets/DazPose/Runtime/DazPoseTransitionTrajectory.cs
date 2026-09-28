using UnityEngine;

namespace DazPose.UnityValidation
{
    public static class DazPoseTransitionTrajectory
    {
        public const float WindupPhaseFraction = 0.15f;
        public const float SettlePhaseFraction = 0.20f;

        public static float Evaluate(float normalizedTime, DazPoseTransitionOptions options)
        {
            var time = Mathf.Clamp01(normalizedTime);
            var windup = options.WindupFraction;
            var overshoot = options.OvershootFraction;

            if (windup == 0f && overshoot == 0f)
                return ApplyEase(time, options.Ease);

            var hasWindup = windup > 0f;
            var hasOvershoot = overshoot > 0f;
            var driveStart = hasWindup ? WindupPhaseFraction : 0f;
            var driveEnd = hasOvershoot ? 1f - SettlePhaseFraction : 1f;

            if (hasWindup && time < driveStart)
            {
                var phaseTime = time / driveStart;
                return Mathf.LerpUnclamped(0f, -windup, ApplyEase(phaseTime, DazPoseBlendEase.SmoothStep));
            }

            if (hasOvershoot && time > driveEnd)
            {
                var phaseTime = (time - driveEnd) / SettlePhaseFraction;
                return Mathf.LerpUnclamped(1f + overshoot, 1f,
                    ApplyEase(phaseTime, DazPoseBlendEase.SmoothStep));
            }

            var driveTime = (time - driveStart) / (driveEnd - driveStart);
            var driveFrom = hasWindup ? -windup : 0f;
            var driveTo = hasOvershoot ? 1f + overshoot : 1f;
            return Mathf.LerpUnclamped(driveFrom, driveTo, ApplyEase(driveTime, options.Ease));
        }

        public static float ApplyEase(float normalizedTime, DazPoseBlendEase ease)
        {
            var time = Mathf.Clamp01(normalizedTime);
            return ease == DazPoseBlendEase.Linear
                ? time
                : time * time * (3f - 2f * time);
        }
    }
}
