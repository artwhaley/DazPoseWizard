using System;
namespace DazPose.Performer
{
    /// <summary>Compatibility name; explicit values now persist after the run.</summary>
    [Obsolete("Use PerformerTuning and SetTuning. Tuning is persistent.")]
    public sealed class PerformanceTuningOverride : PerformerTuning { }
}
