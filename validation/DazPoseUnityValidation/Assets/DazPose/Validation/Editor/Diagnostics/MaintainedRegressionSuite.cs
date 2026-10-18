using System;
using System.Collections.Generic;
using DazPose.Performer;
using DazPose.Performer.HandGrip;
using UnityEditor;
namespace DazPose.Editor.Diagnostics
{
    /// <summary>Explicit regression suites using disposable objects, with no named diagnostic scene dependency.</summary>
    public static class MaintainedRegressionSuite
    {
        [MenuItem("DazPose/Diagnostics/Run Deterministic Performer Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run deterministic diagnostics in Edit Mode.");
            var failures = new List<string>();
            failures.AddRange(DazPose.UnityValidation.MaintainedRuntimeRegressionSuite.Run());
            DazPose.UnityValidation.DazPoseEditorCommands.RunAdapterSelfTests();
            DazPose.UnityValidation.DazPoseEditorCommands.RunStage5MorphSelfTests();
            if (failures.Count != 0) throw new InvalidOperationException(string.Join("\\n", failures));
            UnityEngine.Debug.Log("Deterministic performer checks passed. Native playback, art and contact appearance still require user observation.");
        }
    }
}
