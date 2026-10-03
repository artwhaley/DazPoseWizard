namespace DazPose.Motion
{
    /// <summary>A source evaluation before MotionDriver assigns its monotonic sequence number.</summary>
    public readonly struct MotionSourceSample
    {
        public readonly double TimeSeconds;
        public readonly float Phase01;
        public readonly float Position01;
        public readonly float Velocity;
        public readonly MotionDirection Direction;

        public MotionSourceSample(double timeSeconds, float phase01, float position01,
            float velocity, MotionDirection direction)
        {
            TimeSeconds = timeSeconds;
            Phase01 = phase01;
            Position01 = position01;
            Velocity = velocity;
            Direction = direction;
        }
    }
}
