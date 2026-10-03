namespace DazPose.Motion
{
    /// <summary>Small timeline seam shared by procedural and authored motion sources.</summary>
    public interface IMotionSource
    {
        MotionSourceSample CurrentSample { get; }
        double DurationSeconds { get; }
        bool IsComplete { get; }

        void Advance(double deltaSeconds);
        void Reset();
        void Seek(double timeSeconds);
    }
}
