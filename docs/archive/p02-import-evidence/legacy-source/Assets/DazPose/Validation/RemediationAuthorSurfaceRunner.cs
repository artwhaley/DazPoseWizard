using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.FirstPerformanceVoid;
using DazPose.Motion;
using DazPose.Performer;
using DazPose.Player;
using UnityEngine;

namespace DazPose.Validation
{
    /// <summary>R08 witness for the author surface, tuning ownership and First Contact migration.</summary>
    public sealed class RemediationAuthorSurfaceRunner : MonoBehaviour
    {
        [Serializable] private sealed class Assertion
        { public string name, expected, actual; public bool passed; }

        [Serializable] private sealed class Report
        {
            public string ticket = "R08", startedUtc, completedUtc, sourceHash, exception;
            public string[] inputAssetHashes;
            public bool complete, passed;
            public List<Assertion> assertions = new List<Assertion>();
        }

        [SerializeField] private FirstContactPerformance firstContact;
        [SerializeField] private SuccubusPerformer actor;
        [SerializeField] private PlayerController player;
        [SerializeField] private MotionDriver playback;
        [SerializeField] private PerformerExpression expression;
        [SerializeField] private FunscriptMotionProgram program;
        [SerializeField] private AudioClip firstClip;
        [SerializeField] private AudioClip secondClip;
        [SerializeField] private string reportPath, sourceHash, inputHash;
        private bool finished;
        private Report report;

#if UNITY_EDITOR
        public void Configure(FirstContactPerformance sequence, SuccubusPerformer owner,
            PlayerController viewer, MotionDriver clock, PerformerExpression face,
            FunscriptMotionProgram motionProgram, AudioClip clip0, AudioClip clip1,
            string output, string source, string input)
        {
            firstContact = sequence;
            actor = owner;
            player = viewer;
            playback = clock;
            expression = face;
            program = motionProgram;
            firstClip = clip0;
            secondClip = clip1;
            reportPath = output;
            sourceHash = source;
            inputHash = input;
        }
#endif

        private async void Start()
        {
            report = new Report
            {
                startedUtc = DateTime.UtcNow.ToString("O"),
                sourceHash = sourceHash ?? string.Empty,
                inputAssetHashes = new[] { inputHash ?? string.Empty }
            };
            try
            {
                for (int frame = 0; frame < 300 && (actor == null || !actor.IsRuntimeReady); ++frame)
                    await Awaitable.NextFrameAsync();
                Record("fixtureReferences", firstContact != null && actor != null && player != null && playback != null
                    && expression != null && program != null && firstClip != null && secondClip != null,
                    "First Contact, actor/player/playback and declared assets are required.");
                if (!report.assertions.Last().passed) { Finish(false, null); return; }

                Record("playModeTimeAdvances", Time.timeScale > 0f && Time.deltaTime > 0f,
                    "timeScale=" + Time.timeScale + "; deltaTime=" + Time.deltaTime);
                await CheckAuthorWrappers();
                await CheckFirstContactNaturalCompletion();
                await CheckFirstContactStopAndReplay();
                await CheckMissingRequiredReferences();
                Finish(report.assertions.All(item => item.passed), null);
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private async Awaitable CheckAuthorWrappers()
        {
            var run = BeginRun(out GameObject holder);
            var target = new GameObject("R08 author target");
            target.transform.position = actor.transform.position + actor.transform.forward * 1.5f + Vector3.up;
            try
            {
                PoseCompletion pose = await run.Performer.PoseAsync(actor.InitialPose);
                ExpressionCompletion face = await run.Performer.ExpressionAsync(expression, .6f, 0f);
                GazeCompletion gaze = await run.Performer.LookAtAsync(target.transform);
                run.Performer.LookAt(target.transform);
                run.Performer.ClearGaze();
                Record("newScopedPoseExpressionGazeSurface", pose == PoseCompletion.Settled
                    && face == ExpressionCompletion.Settled && gaze == GazeCompletion.Acquired,
                    pose + "; " + face + "; " + gaze);

                bool originalBreathing = actor.BreathingEnabled;
                float originalRate = actor.BreathsPerMinute;
                var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
                var tuning = new PerformanceTuningOverride
                {
                    BreathingEnabled = !originalBreathing,
                    BreathsPerMinute = -4f,
                    HasBreathingCurve = true,
                    BreathingCurve = curve,
                    BlinkMinimumIntervalSeconds = 6f,
                    BlinkMaximumIntervalSeconds = 2f
                };
                run.Performer.OverrideTuning(tuning);
                bool clamped = actor.BreathingEnabled == !originalBreathing
                    && Mathf.Approximately(actor.BreathsPerMinute, 0f)
                    && actor.BlinkMaximumIntervalSeconds >= actor.BlinkMinimumIntervalSeconds;
                run.Stop();
                Record("tuningOverrideClampsAndRestores", clamped
                    && actor.BreathingEnabled == originalBreathing
                    && Mathf.Approximately(actor.BreathsPerMinute, originalRate),
                    "clamped=" + clamped + "; restored=" + (actor.BreathingEnabled == originalBreathing));

                playback.StopMotion();
                DestroyObject(holder);
                await Awaitable.NextFrameAsync();
                run = BeginRun(out holder);
                run.StartPlayback(program);
                Record("scopedPlaybackFacadeDispatches", playback.IsRunning && run.Playback == playback,
                    "running=" + playback.IsRunning);
            }
            catch (Exception exception) { Record("authorFacadeDispatch", false, exception.ToString()); }
            finally
            {
                run.Dispose();
                playback.StopMotion();
                playback.ResetMotion();
                DestroyObject(target);
                DestroyObject(holder);
                await Awaitable.NextFrameAsync();
            }
        }

        private async Awaitable CheckFirstContactNaturalCompletion()
        {
            VisibilityCompletion visible = await EnsureActorVisible();
            Record("firstContactVisiblePrecondition", visible == VisibilityCompletion.Visible, visible.ToString());
            firstContact.Run();
            float initialDelayDeadline = Time.realtimeSinceStartup + 10f;
            while (firstContact.IsRunning && string.Equals(firstContact.Status, "Establishing", StringComparison.Ordinal)
                && Time.realtimeSinceStartup < initialDelayDeadline)
                await Awaitable.NextFrameAsync();
            Record("firstContactAdvancesPastInitialDelay", !string.Equals(firstContact.Status, "Establishing", StringComparison.Ordinal)
                || !firstContact.IsRunning, firstContact.Status + "; running=" + firstContact.IsRunning
                + "; timeScale=" + Time.timeScale + "; deltaTime=" + Time.deltaTime);
            bool timedOut = await WaitForSequence(45f);
            Record("firstContactNaturalCompletion", !timedOut && !firstContact.IsRunning
                && firstContact.Status.StartsWith("Complete", StringComparison.Ordinal),
                firstContact.Status + "; running=" + firstContact.IsRunning + "; timedOut=" + timedOut);
            Record("optionalToysAbsentDoesNotAbort", player.BoundToyService == null
                && !firstContact.Status.StartsWith("ABORTED", StringComparison.Ordinal),
                "bound toys=" + (player.BoundToyService != null) + "; status=" + firstContact.Status);
        }

        private async Awaitable CheckFirstContactStopAndReplay()
        {
            if (firstContact.IsRunning) firstContact.Stop();
            await firstContact.ResetAsync();
            VisibilityCompletion visible = await EnsureActorVisible();
            Record("firstContactStopVisiblePrecondition", visible == VisibilityCompletion.Visible, visible.ToString());
            firstContact.Run();
            for (int frame = 0; frame < 900 && firstContact.IsRunning; ++frame)
            {
                await Awaitable.NextFrameAsync();
                if (frame == 30) firstContact.Stop();
            }
            Record("firstContactStopCancelsOwnedRun", !firstContact.IsRunning
                && firstContact.Status.StartsWith("Stopped", StringComparison.Ordinal), firstContact.Status);
            await firstContact.ResetAsync();
            visible = await EnsureActorVisible();
            Record("firstContactReplayVisiblePrecondition", visible == VisibilityCompletion.Visible, visible.ToString());
            firstContact.Run();
            bool timedOut = await WaitForSequence(45f);
            Record("firstContactResetThenReplay", !timedOut && !firstContact.IsRunning
                && firstContact.Status.StartsWith("Complete", StringComparison.Ordinal), firstContact.Status);
        }

        private async Awaitable CheckMissingRequiredReferences()
        {
            var holder = new GameObject("R08 missing First Contact references");
            var missing = holder.AddComponent<FirstContactPerformance>();
            missing.Configure(actor, player, null, null, null, null, null, null, firstClip, secondClip);
            missing.Run();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (missing.IsRunning && Time.realtimeSinceStartup < deadline) await Awaitable.NextFrameAsync();
            Record("missingSeatAndTargetsFailClearly", missing.Status.StartsWith("ABORTED", StringComparison.Ordinal)
                && missing.Status.Contains("missing", StringComparison.OrdinalIgnoreCase), missing.Status);
            DestroyObject(holder);
        }

        private async Awaitable<bool> WaitForSequence(float maxSeconds)
        {
            float deadline = Time.realtimeSinceStartup + maxSeconds;
            while (firstContact.IsRunning && Time.realtimeSinceStartup < deadline)
                await Awaitable.NextFrameAsync();
            return firstContact.IsRunning;
        }

        private async Awaitable<VisibilityCompletion> EnsureActorVisible()
        {
            if (actor.VisibilityState == PerformerVisibilityState.Visible) return VisibilityCompletion.Visible;
            return await actor.DissolveInAsync(.05f);
        }

        private PerformanceRun BeginRun(out GameObject holder)
        {
            holder = new GameObject("R08 scoped author bindings");
            var sceneBindings = holder.AddComponent<PerformanceSceneBindings>();
            sceneBindings.Configure(actor, player, playback, null, PerformerCapability.Pose | PerformerCapability.Speech);
            sceneBindings.DeclareAssets(null, new[] { expression }, new[] { firstClip, secondClip });
            return PerformanceRun.Begin(sceneBindings);
        }

        private void Record(string name, bool passed, string actual)
        { report.assertions.Add(new Assertion { name = name, expected = "pass", actual = actual ?? string.Empty, passed = passed }); }

        private void Finish(bool passed, string exception)
        {
            if (finished) return;
            finished = true;
            report.exception = exception;
            report.passed = passed;
            report.complete = true;
            report.completedUtc = DateTime.UtcNow.ToString("O");
            string output = string.IsNullOrWhiteSpace(reportPath) ? Path.Combine("TestOutput", "remediation", "R08.json") : reportPath;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(passed ? 0 : 1);
#else
            if (Application.isBatchMode) Application.Quit(passed ? 0 : 1);
#endif
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (value == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(value); else UnityEngine.Object.Destroy(value);
#else
            UnityEngine.Object.Destroy(value);
#endif
        }
    }
}
