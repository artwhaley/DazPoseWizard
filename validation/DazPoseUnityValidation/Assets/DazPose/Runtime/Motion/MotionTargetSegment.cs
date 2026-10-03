using System;

namespace DazPose.Motion
{
    /// <summary>Source-neutral authored position target and time bounds for hardware motion.</summary>
    public readonly struct MotionTargetSegment
    {
        public readonly double StartTimeSeconds;
        public readonly double EndTimeSeconds;
        public readonly float StartPosition01;
        public readonly float TargetPosition01;
        public readonly int FromActionIndex;
        public readonly int ToActionIndex;
        public double DurationSeconds => Math.Max(0d, EndTimeSeconds - StartTimeSeconds);

        public MotionTargetSegment(double startTimeSeconds, double endTimeSeconds,
            float startPosition01, float targetPosition01, int fromActionIndex = -1, int toActionIndex = -1)
        {
            StartTimeSeconds = startTimeSeconds;
            EndTimeSeconds = endTimeSeconds;
            StartPosition01 = UnityEngine.Mathf.Clamp01(startPosition01);
            TargetPosition01 = UnityEngine.Mathf.Clamp01(targetPosition01);
            FromActionIndex = fromActionIndex;
            ToActionIndex = toActionIndex;
        }
    }
}
