using System;
using System.Collections.Generic;
using DazPose.Performer;
using DazPose.Performer.HandGrip;
namespace DazPose.UnityValidation
{
    /// <summary>Stable validation-only entry point; runtime production has no dependency on these tests.</summary>
    public static class MaintainedRuntimeRegressionSuite
    {
        public static string[] Run()
        {
            var failures = new List<string>();
            failures.AddRange(HandGripRuntimeSelfTests.Run());
            failures.AddRange(PerformerExpressionRuntimeSelfTests.Run());
            failures.AddRange(PerformerSpeechRuntimeSelfTests.Run());
            failures.AddRange(PerformerMotionRuntimeSelfTests.Run());
            failures.AddRange(PerformerDissolveRuntimeSelfTests.Run());
            failures.AddRange(PerformerMagicRuntimeSelfTests.Run());
            return failures.ToArray();
        }
    }
}
