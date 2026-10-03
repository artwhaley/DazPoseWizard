namespace DazPose.Motion
{
    /// <summary>One authored command transition, with raw endpoint actions and interpreted targets.</summary>
    public readonly struct FunscriptSegment
    {
        public readonly int FromActionIndex;
        public readonly int ToActionIndex;
        public readonly FunscriptAction From;
        public readonly FunscriptAction To;
        public readonly double StartSeconds;
        public readonly double EndSeconds;
        public readonly double DurationSeconds;
        public readonly float FromPosition01;
        public readonly float ToPosition01;

        internal FunscriptSegment(int fromActionIndex, int toActionIndex,
            FunscriptAction from, FunscriptAction to, float fromPosition01, float toPosition01)
        {
            FromActionIndex = fromActionIndex;
            ToActionIndex = toActionIndex;
            From = from;
            To = to;
            StartSeconds = from.AtMilliseconds / 1000d;
            EndSeconds = to.AtMilliseconds / 1000d;
            DurationSeconds = EndSeconds - StartSeconds;
            FromPosition01 = fromPosition01;
            ToPosition01 = toPosition01;
        }
    }
}
