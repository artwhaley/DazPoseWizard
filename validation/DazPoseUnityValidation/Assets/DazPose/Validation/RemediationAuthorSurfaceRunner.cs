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
            public string ticket = "R08", contractRevision = "authoring-2026-10-08", startedUtc, completedUtc, sourceHash, exception;
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
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-authoringB04") >= 0)
                {
                    await CheckSavedHostContract();
                    Finish(report.assertions.TrueForAll(item => item.passed), null);
                    return;
                }
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-authoringB03") >= 0)
                {
                    await CheckApplicationSettings();
                    Finish(report.assertions.TrueForAll(item => item.passed), null);
                    return;
                }
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-authoringB02") >= 0)
                {
                    await CheckPersistentTuningAndSmoothPose();
                    Finish(report.assertions.All(item => item.passed), null);
                    return;
                }
                await CheckAuthorWrappers();
                await CheckFirstContactNaturalCompletion();
                await CheckFirstContactStopAndReplay();
                await CheckMissingRequiredReferences();
                Finish(report.assertions.All(item => item.passed), null);
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private async Awaitable CheckSavedHostContract()
        {
            var holder=new GameObject("B04 host probe");
            var b=holder.AddComponent<PerformanceSceneBindings>();
            b.Configure(actor,player,playback,null,PerformerCapability.None);
            b.ConfigureIdentity("host-probe","lara");
            var sequence=holder.AddComponent<AuthoringHostProbeSequence>();
            var host=holder.AddComponent<PerformanceHost>();
            host.PlayOnStart=false; host.Configure(b,sequence);
            float original=actor.BreathsPerMinute; actor.BreathsPerMinute=20;
            sequence.seconds=.05f; host.StartPerformance();
            { float deadline=Time.realtimeSinceStartup+3f; while(host.State==PerformanceHostState.Running && Time.realtimeSinceStartup<deadline) await Awaitable.NextFrameAsync(); }
            Record("naturalHostCompletionLatched",host.State==PerformanceHostState.Completed,host.State.ToString());
            sequence.seconds=10; host.StartPerformance(); await Awaitable.NextFrameAsync();
            Time.timeScale=0; host.StopPerformance(); await Awaitable.NextFrameAsync();
            Record("pausedHostStopLatched",host.State==PerformanceHostState.Stopped,host.State.ToString());
            Time.timeScale=1; sequence.seconds=.05f; host.StartPerformance();
            { float deadline=Time.realtimeSinceStartup+3f; while(host.State==PerformanceHostState.Running && Time.realtimeSinceStartup<deadline) await Awaitable.NextFrameAsync(); }
            Record("replacementHostCompletesWithoutReset",host.State==PerformanceHostState.Completed && actor.BreathsPerMinute==20,host.State.ToString());
            sequence.fail=true; host.StartPerformance();
            { float deadline=Time.realtimeSinceStartup+3f; while(host.State==PerformanceHostState.Running && Time.realtimeSinceStartup<deadline) await Awaitable.NextFrameAsync(); }
            Record("nonToyFailureLatched",host.State==PerformanceHostState.Failed && host.Failure!=null,host.State.ToString());
            actor.BreathsPerMinute=original;
            DestroyObject(holder);
        }

        private async Awaitable CheckApplicationSettings()
        {
            var app=DazPose.Game.GameApplication.EnsureStarted();
            Record("oneApplicationOwner", app==DazPose.Game.GameApplication.EnsureStarted(), app.name);
            var holder=new GameObject("B03 tuning scope");
            var b=holder.AddComponent<PerformanceSceneBindings>();
            b.Configure(actor,player,playback,null,PerformerCapability.None);
            b.ConfigureIdentity("bounded-probe","lara");
            var directory=Path.Combine(Application.temporaryCachePath,"authoring-settings-"+Guid.NewGuid().ToString("N"));
            var settings=new DazPose.Game.GameSettingsService(null,directory);
            float original=actor.BreathsPerMinute;
            actor.BreathsPerMinute=19;
            settings.CaptureSceneTuning(b);
            actor.BreathsPerMinute=11;
            settings.ApplySceneTuning(b);
            Record("latestLiveTuningWinsOnReinstantiation", actor.BreathsPerMinute==19, actor.BreathsPerMinute.ToString());
            actor.BreathsPerMinute=12;
            var fresh=new DazPose.Game.GameSettingsService(null,directory);
            fresh.ApplySceneTuning(b);
            Record("automaticCaptureSurvivesFreshService", actor.BreathsPerMinute==19, actor.BreathsPerMinute.ToString());
            var partial=new DazPose.Game.SceneTuningSnapshot { present=new[]{"BreathsPerMinute"}, BreathsPerMinute=21 };
            float unchanged=actor.HeadGazeWeight; partial.Apply(b);
            Record("absentFieldsFallThrough", actor.BreathsPerMinute==21 && actor.HeadGazeWeight==unchanged, "partial snapshot");
            b.ConfigureIdentity("other-scene","lara");
            actor.BreathsPerMinute=17; fresh.ApplySceneTuning(b);
            Record("sceneIdentitySeparatesTuning", actor.BreathsPerMinute==17, "other scene");
            actor.BreathsPerMinute=original; // explicit isolated fixture cleanup
            DestroyObject(holder);
            await Awaitable.NextFrameAsync();
        }

        private async Awaitable CheckPersistentTuningAndSmoothPose()
        {
            float originalRate = actor.BreathsPerMinute;
            PoseTransition originalTransition = actor.DefaultPoseTransition;
            var run = BeginRun(out GameObject holder);
            try
            {
                run.Performer.SetTuning(new PerformerTuning
                {
                    BreathsPerMinute = 18f,
                    DefaultPoseTransition = PoseTransition.Smooth(.4f)
                });
#if UNITY_EDITOR
                PerformerPose next = UnityEditor.AssetDatabase.FindAssets("t:PerformerPose")
                    .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                    .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<PerformerPose>)
                    .FirstOrDefault(item => item != null && item.Clip != null && item != actor.InitialPose);
                if (next == null) throw new InvalidOperationException("No second prepared Lara pose found.");
                Awaitable<PoseCompletion> pose = run.Performer.PoseAsync(next);
                Record("omittedPoseUsesConfiguredSmoothTransition", actor.IsTransitioning
                    && Mathf.Approximately(actor.ActiveTransition.Duration, .4f), "duration=" + actor.ActiveTransition.Duration);
                await Awaitable.NextFrameAsync();
                var before = actor.CaptureEvaluatedBasePoseState();
                run.Stop();
                try { await pose; } catch (OperationCanceledException) { }
                var after = actor.CaptureEvaluatedBasePoseState();
                bool same = before.Transforms.Length == after.Transforms.Length;
                for (int i = 0; same && i < before.Transforms.Length; i++)
                    same = Vector3.Distance(before.Transforms[i].LocalPosition, after.Transforms[i].LocalPosition) < .00001f
                        && Quaternion.Angle(before.Transforms[i].LocalRotation, after.Transforms[i].LocalRotation) < .001f;
                Record("cancelledPoseRetainsEvaluatedState", same && !actor.IsTransitioning, "same=" + same);
#endif
                run.Stop(); run.Dispose();
                Record("tuningSurvivesStopAndDispose", Mathf.Approximately(actor.BreathsPerMinute, 18f)
                    && Mathf.Approximately(actor.DefaultPoseTransition.Duration, .4f), "rate=" + actor.BreathsPerMinute);
                DestroyObject(holder);
                run = BeginRun(out holder);
                Record("newRunDoesNotRestoreTuning", Mathf.Approximately(actor.BreathsPerMinute, 18f)
                    && Mathf.Approximately(actor.DefaultPoseTransition.Duration, .4f), "rate=" + actor.BreathsPerMinute);
                Awaitable<PoseCompletion> explicitPose = run.Performer.PoseAsync(actor.InitialPose, PoseTransition.Smooth(.2f));
                Record("explicitPoseDurationRemainsAvailable", Mathf.Approximately(actor.ActiveTransition.Duration, .2f),
                    "duration=" + actor.ActiveTransition.Duration);
                await explicitPose;
                Awaitable<ExpressionCompletion> face = run.Performer.ExpressionAsync(expression, .8f, .3f);
                await Awaitable.NextFrameAsync();
                run.Stop();
                try { await face; } catch (OperationCanceledException) { }
                Record("cancelledExpressionDoesNotRestoreInitial", !actor.IsExpressionTransitioning
                    && actor.DesiredExpression == expression, "expression=" + actor.DesiredExpression);
            }
            catch (Exception exception) { Record("persistentTuningAndSmoothPose", false, exception.ToString()); }
            finally
            {
                run.Dispose(); DestroyObject(holder);
                // Explicit isolated-fixture cleanup, never a production lifecycle policy.
                actor.BreathsPerMinute = originalRate; actor.DefaultPoseTransition = originalTransition;
            }
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
                var tuning = new PerformerTuning
                {
                    BreathingEnabled = !originalBreathing,
                    BreathsPerMinute = -4f,
                    HasBreathingCurve = true,
                    BreathingCurve = curve,
                    BlinkMinimumIntervalSeconds = 6f,
                    BlinkMaximumIntervalSeconds = 2f
                };
                run.Performer.SetTuning(tuning);
                bool clamped = actor.BreathingEnabled == !originalBreathing
                    && Mathf.Approximately(actor.BreathsPerMinute, 0f)
                    && actor.BlinkMaximumIntervalSeconds >= actor.BlinkMinimumIntervalSeconds;
                run.Stop();
                Record("explicitTuningClampsAndPersists", clamped
                    && actor.BreathingEnabled == !originalBreathing
                    && Mathf.Approximately(actor.BreathsPerMinute, 0f),
                    "clamped=" + clamped + "; restored=" + (actor.BreathingEnabled == originalBreathing));

                playback.StopMotion();
                DestroyObject(holder);
                await Awaitable.NextFrameAsync();
                run = BeginRun(out holder);
                run.StartPlayback(program);
                Record("scopedPlaybackFacadeDispatches", run.IsPlaybackRunning && run.PlaybackPosition01>=0,
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
            Record("applicationToysNeverGateFirstContact", !firstContact.Status.StartsWith("ABORTED", StringComparison.Ordinal),
                "bound toys=" + (player.BoundToyService != null) + "; status=" + firstContact.Status);
        }

        private async Awaitable CheckFirstContactStopAndReplay()
        {
            if (firstContact.IsRunning) firstContact.Stop();
            firstContact.Stop();
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
            firstContact.Stop();
            visible = await EnsureActorVisible();
            Record("firstContactReplayVisiblePrecondition", visible == VisibilityCompletion.Visible, visible.ToString());
            firstContact.Run();
            bool timedOut = await WaitForSequence(45f);
            Record("firstContactHostReplayWithoutReset", !timedOut && !firstContact.IsRunning
                && firstContact.Status.StartsWith("Complete", StringComparison.Ordinal), firstContact.Status);
        }

        private async Awaitable CheckMissingRequiredReferences()
        {
            var holder = new GameObject("R08 missing First Contact references");
            var missing = holder.AddComponent<FirstContactPerformance>();
            missing.Configure(actor, player, null, null, null, null, null, null, firstClip, secondClip);
            var b=holder.AddComponent<PerformanceSceneBindings>(); b.Configure(actor,player,playback,null,PerformerCapability.None);
            var host=holder.AddComponent<PerformanceHost>(); host.PlayOnStart=false; host.Configure(b,missing);
            missing.Run();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (missing.IsRunning && Time.realtimeSinceStartup < deadline) await Awaitable.NextFrameAsync();
            Record("missingTargetFailsAtCommand", host.State==PerformanceHostState.Failed && host.Failure is PerformanceCommandException, missing.Status);
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
    public sealed class AuthoringHostProbeSequence : PerformanceSequence {
        public float seconds;
        public bool fail;
        public override async Awaitable ExecuteAsync(PerformanceRun run) {
            await run.Delay(seconds);
            if(fail) throw new InvalidOperationException("bounded non-toy failure");
        }
    }

}
