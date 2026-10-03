using System.Collections.Generic;
using DazPose.Motion;
using DazPose.Performer;
using DazPose.Toys;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.Motion.Editor
{
    /// <summary>Batch- and menu-runnable acceptance checks against the repository's real script asset.</summary>
    public static class FunscriptAcceptanceRunner
    {
        private const string AcceptancePath = "Assets/motiondrive.funscript";

        [MenuItem("Tools/DAZ Pose/Motion/Run Funscript Acceptance Checks")]
        public static async void Run()
        {
            var failures = new List<string>();
            AssetDatabase.ImportAsset(AcceptancePath, ImportAssetOptions.ForceUpdate);
            FunscriptMotionProgram program = AssetDatabase.LoadAssetAtPath<FunscriptMotionProgram>(AcceptancePath);
            if (program == null)
            {
                failures.Add("Assets/motiondrive.funscript did not import as a FunscriptMotionProgram.");
                Finish(failures, null);
                return;
            }

            failures.AddRange(FunscriptRuntimeSelfTests.Run());
            failures.AddRange(PerformerMotionRuntimeSelfTests.Run());
            ValidateSuppliedProgram(program, failures);
            ValidateSceneReference(program, failures);
            failures.AddRange(await ToyStackAcceptanceSelfTests.RunAsync(program));
            if (failures.Count == 0)
            {
                FunscriptAction first = program.GetAction(0);
                FunscriptAction last = program.GetAction(program.ActionCount - 1);
                Debug.Log("FUNSCRIPT_ACCEPTANCE_PASSED: asset=" + AcceptancePath
                    + "; actions=" + program.ActionCount
                    + "; first=" + first.AtMilliseconds + "ms/" + first.Position
                    + "; last=" + last.AtMilliseconds + "ms/" + last.Position
                    + "; metadataDuration=" + program.MetadataDurationSeconds.ToString("F3")
                    + "s; effectiveDuration=" + program.DurationSeconds.ToString("F3")
                    + "s; range=" + program.Range + "; inverted=" + program.Inverted + ".", program);
            }
            Finish(failures, program);
        }

        private static void ValidateSceneReference(FunscriptMotionProgram program, List<string> failures)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/FirstPerformanceVoid.unity");
            try
            {
                MotionDriver driver = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    driver = root.GetComponentInChildren<MotionDriver>(true);
                    if (driver != null) break;
                }
                if (driver == null || !driver.HasFunscriptProgram || driver.FunscriptProgram != program)
                {
                    failures.Add("FirstPerformanceVoid MotionDriver does not resolve the imported Funscript asset; the source button will be disabled.");
                    return;
                }
                driver.SourceMode = MotionSourceMode.Funscript;
                driver.Seek(12.280d);
                if (Mathf.Abs(driver.CurrentSample.Position01 - 0.20f) > 0.0001f)
                    failures.Add("Scene MotionDriver did not publish the Funscript midpoint after selecting and seeking the source.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void ValidateSuppliedProgram(FunscriptMotionProgram program, List<string> failures)
        {
            if (program.Version != "1.0") failures.Add("Supplied Funscript version was not 1.0.");
            if (program.ActionCount != 1715) failures.Add("Expected 1715 source actions; imported " + program.ActionCount + ".");
            if (program.Range != 100) failures.Add("Expected range 100; imported " + program.Range + ".");
            if (program.Inverted) failures.Add("Expected supplied script inverted=false.");
            if (program.MetadataDurationSeconds != 996d)
                failures.Add("Expected declared metadata duration 996 seconds; imported " + program.MetadataDurationSeconds + ".");
            if (program.DurationSeconds != 996d)
                failures.Add("Expected effective playback duration 996 seconds; imported " + program.DurationSeconds + ".");
            if (program.Title != "4111500.mp4")
                failures.Add("Unexpected supplied-script title: '" + program.Title + "'.");

            if (program.ActionCount == 0) return;
            FunscriptAction first = program.GetAction(0);
            FunscriptAction last = program.GetAction(program.ActionCount - 1);
            if (first.AtMilliseconds != 11440L || first.Position != 10)
                failures.Add("Expected first action 11440ms/10; imported " + first.AtMilliseconds + "ms/" + first.Position + ".");
            if (last.AtMilliseconds != 953120L || last.Position != 0)
                failures.Add("Expected final action 953120ms/0; imported " + last.AtMilliseconds + "ms/" + last.Position + ".");

            var playback = new FunscriptPlayback(program);
            CheckPosition(playback, 0d, 0.10f, "pre-first hold", failures);
            CheckPosition(playback, 11.440d, 0.10f, "first action", failures);
            CheckPosition(playback, 12.280d, 0.20f, "first segment midpoint", failures);
            CheckPosition(playback, 13.120d, 0.30f, "second action", failures);
            CheckPosition(playback, 996d, 0f, "metadata-duration tail", failures);
        }

        private static void CheckPosition(FunscriptPlayback playback, double timeSeconds,
            float expected, string label, List<string> failures)
        {
            playback.Seek(timeSeconds);
            if (Mathf.Abs(playback.Position01 - expected) > 0.0001f)
                failures.Add(label + " at " + timeSeconds.ToString("F3") + " seconds expected "
                    + expected.ToString("F3") + " but got " + playback.Position01.ToString("F6") + ".");
        }

        private static void Finish(List<string> failures, Object context)
        {
            if (failures.Count == 0)
            {
                Debug.Log("Funscript parser and playback self-tests passed.", context);
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }

            Debug.LogError("FUNSCRIPT_ACCEPTANCE_FAILED (" + failures.Count + "): "
                + string.Join("\n", failures), context);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
