namespace DazPose.Motion
{
    /// <summary>An immutable, authoritative normalized motion observation.</summary>
    public readonly struct MotionSample
    {
        public readonly long Sequence;
        public readonly double TimeSeconds;
        public readonly float Phase01;
        public readonly float Position01;
        public readonly float Velocity;
        public readonly MotionDirection Direction;

        public MotionSample(long sequence, double timeSeconds, float phase01,
            float position01, float velocity, MotionDirection direction)
        {
            Sequence = sequence;
            TimeSeconds = timeSeconds;
            Phase01 = phase01;
            Position01 = position01;
            Velocity = velocity;
            Direction = direction;
        }
    }
}
