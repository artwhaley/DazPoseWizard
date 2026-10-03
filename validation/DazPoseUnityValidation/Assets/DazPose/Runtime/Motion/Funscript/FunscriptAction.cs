using System;

namespace DazPose.Motion
{
    /// <summary>One authored Funscript command, retaining its millisecond timestamp and raw position.</summary>
    public readonly struct FunscriptAction
    {
        public readonly long AtMilliseconds;
        public readonly int Position;

        public FunscriptAction(long atMilliseconds, int position)
        {
            if (atMilliseconds < 0L)
                throw new ArgumentOutOfRangeException(nameof(atMilliseconds), "Action timestamps cannot be negative.");
            AtMilliseconds = atMilliseconds;
            Position = position;
        }
    }
}
