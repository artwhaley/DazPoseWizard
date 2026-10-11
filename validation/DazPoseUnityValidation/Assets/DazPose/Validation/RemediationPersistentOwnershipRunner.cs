using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DazPose.Motion;
using DazPose.Performer;
using DazPose.Performer.HandGrip;
using DazPose.Player;
using DazPose.Toys;
using UnityEngine;

namespace DazPose.Validation
{
    /// <summary>R07 witness for persistent ownership, shared clocks and acknowledged toy delivery.</summary>
    public sealed class RemediationPersistentOwnershipRunner : MonoBehaviour
    {
        [Serializable] private sealed class Assertion
        { public string name, expected, actual; public bool passed; }

        [Serializable] private sealed class Report
        {
            public string ticket = "R07", contractRevision = "authoring-2026-10-08", startedUtc, completedUtc, sourceHash, exception;
            public string[] inputAssetHashes;
            public bool complete, passed;
            public List<Assertion> assertions = new List<Assertion>();
        }

        [SerializeField] private SuccubusPerformer actor;
        [SerializeField] private PerformanceSceneBindings bindings;
        [SerializeField] private PlayerController player;
        [SerializeField] private ToyControlService toys;
        [SerializeField] private MotionDriver playback;
        [SerializeField] private MonoBehaviour contactBehaviour;
        [SerializeField] private FunscriptMotionProgram program;
        [SerializeField] private PerformerAura firstAura;
        [SerializeField] private PerformerAura secondAura;
        [SerializeField] private PerformerExpression expression;
        [SerializeField] private AudioClip firstClip;
        [SerializeField] private AudioClip secondClip;
        [SerializeField] private string reportPath, sourceHash, inputHash;

        private FakeBackend backend;
        private Report report;
        private bool finished;

        private IGripTarget ContactTarget => contactBehaviour as IGripTarget;

#if UNITY_EDITOR
        public void Configure(SuccubusPerformer owner, PerformanceSceneBindings sceneBindings,
            PlayerController boundPlayer, ToyControlService boundToys, MotionDriver clock,
            MonoBehaviour contact, FunscriptMotionProgram motionProgram, PerformerAura aura0,
            PerformerAura aura1, PerformerExpression face, AudioClip clip0, AudioClip clip1,
            string output, string source, string input)
        {
            actor = owner;
            bindings = sceneBindings;
            player = boundPlayer;
            toys = boundToys;
            playback = clock;
            contactBehaviour = contact;
            program = motionProgram;
            firstAura = aura0;
            secondAura = aura1;
            expression = face;
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
                Record("fixtureReferences", actor != null && bindings != null && player != null && toys != null
                    && playback != null && program != null && firstAura != null && secondAura != null
                    && expression != null && firstClip != null && secondClip != null && ContactTarget != null,
                    "Actor, explicit bindings, playback, contact, expression, aura and speech assets are required.");
                if (!report.assertions.Last().passed) { Finish(false, null); return; }

                backend = new FakeBackend();
                toys.SetBackendForAcceptance(backend);
                toys.ConfigurePlayback(playback);
                await RunChecks();
                Finish(report.assertions.All(item => item.passed), null);
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private async Awaitable RunChecks()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-authoringB01") >= 0)
            {
                await CheckToyDelivery();
                CheckMissingOptionalBindings();
                return;
            }
            await CheckPersistentActorOwnership();
            await CheckPlayerOwnership();
            await CheckPlaybackOwnership();
            await CheckAuraReplacement();
            await CheckToyDelivery();
            await CheckGripOwnership();
            CheckMissingOptionalBindings();
        }

        private async Awaitable CheckPersistentActorOwnership()
        {
            var run = BeginRun(out GameObject holder);
            var gazeOwned = new GameObject("R07 owned gaze target");
            var gazeReplacement = new GameObject("R07 replacement gaze target");
            gazeOwned.transform.position = actor.transform.position + actor.transform.forward * 1.5f + Vector3.up;
            gazeReplacement.transform.position = actor.transform.position - actor.transform.forward * 1.5f + Vector3.up;
            try
            {
                PoseCompletion pose = await run.Performer.PoseAsync(actor.InitialPose);
                Record("finitePoseCompletes", pose == PoseCompletion.Settled, pose.ToString());

                ExpressionCompletion face = await run.Performer.ExpressionAsync(expression, .75f, 0f);
                Record("finiteExpressionCompletes", face == ExpressionCompletion.Settled, face.ToString());

                GazeCompletion gaze = await run.Performer.LookAtAsync(gazeOwned.transform);
                Record("finiteGazeCompletes", gaze == GazeCompletion.Acquired, gaze.ToString());
                actor.LookAt(gazeReplacement.transform);
                run.Stop();
                Record("newerRawGazeSurvivesStop", actor.HasGazeTarget
                    && Vector3.Distance(actor.RawGazeTargetPosition, gazeReplacement.transform.position) < .01f,
                    actor.RawGazeTargetPosition.ToString("F4"));
                Record("poseAndExpressionPersistOnStop", actor.DesiredPose == actor.InitialPose
                    && actor.DesiredExpression == expression,
                    "pose=" + (actor.DesiredPose == actor.InitialPose) + "; expression="
                    + (actor.DesiredExpression == actor.InitialExpression));
            }
            catch (Exception exception) { Record("persistentActorOwnership", false, exception.ToString()); }
            finally
            {
                run.Dispose();
                DestroyObject(gazeOwned);
                DestroyObject(gazeReplacement);
                DestroyObject(holder);
                await Awaitable.NextFrameAsync();
            }
        }

        private async Awaitable CheckPlayerOwnership()
        {
            var run = BeginRun(out GameObject holder);
            var moveTarget = new GameObject("R07 player move target");
            var trackTarget = new GameObject("R07 player track target");
            var replacement = new GameObject("R07 replacement track target");
            moveTarget.transform.position = player.transform.position + Vector3.right * .25f;
            trackTarget.transform.position = actor.transform.position + Vector3.forward * 2f + Vector3.up;
            replacement.transform.position = actor.transform.position - Vector3.forward * 2f + Vector3.up;
            try
            {
                PlayerViewCompletion moved = await run.Player.MoveToAsync(moveTarget.transform, 0f);
                Record("playerMoveUsesCompleted", moved == PlayerViewCompletion.Completed,
                    moved.ToString());
                run.Player.Track(trackTarget.transform, 0f);
                player.Track(replacement.transform, 0f);
                run.Stop();
                Record("newerRawTrackSurvivesStop", ReferenceEquals(player.View.CurrentTrackTarget, replacement.transform),
                    player.View.CurrentTrackTarget != null ? player.View.CurrentTrackTarget.name : "none");
                Record("completedPlayerMovePersists", Vector3.Distance(player.transform.position, moveTarget.transform.position) < .001f,
                    player.transform.position.ToString("F4"));
            }
            catch (Exception exception) { Record("playerOwnership", false, exception.ToString()); }
            finally
            {
                player.StopTracking();
                run.Dispose();
                DestroyObject(moveTarget);
                DestroyObject(trackTarget);
                DestroyObject(replacement);
                DestroyObject(holder);
                await Awaitable.NextFrameAsync();
            }
        }

        private async Awaitable CheckPlaybackOwnership()
        {
            playback.StopMotion();
            playback.ResetMotion();
            var run = BeginRun(out GameObject holder);
            try
            {
                long before = playback.CommandGeneration;
                run.StartPlayback(program, false);
                Record("scopedPlaybackStartsExplicitDriver", playback.IsRunning && playback.FunscriptProgram == program,
                    "running=" + playback.IsRunning + "; generation=" + playback.CommandGeneration);
                run.Stop();
                Record("scopedPlaybackStopsItsGeneration", !playback.IsRunning,
                    "generation=" + playback.CommandGeneration + "; before=" + before);
            }
            catch (Exception exception) { Record("scopedPlaybackStartStop", false, exception.ToString()); }
            finally { run.Dispose(); DestroyObject(holder); }

            playback.ResetMotion();
            playback.RestartMotion();
            var externalRun = BeginRun(out GameObject externalHolder);
            try
            {
                try
                {
                    externalRun.StartPlayback(program);
                    Record("externalPlaybackIsRejected", false, "already-running driver was accepted");
                }
                catch (PerformanceCommandException exception)
                { Record("externalPlaybackIsRejected", exception.OriginalStatus == "AlreadyRunning", exception.ToString()); }
            }
            catch (Exception exception) { Record("externalPlaybackIsRejected", false, exception.ToString()); }
            finally
            {
                externalRun.Dispose();
                playback.StopMotion();
                playback.ResetMotion();
                DestroyObject(externalHolder);
            }

            var replacementRun = BeginRun(out GameObject replacementHolder);
            try
            {
                replacementRun.StartPlayback(program, true);
                playback.StopMotion();
                playback.StartMotion();
                replacementRun.Stop();
                Record("sameSourceReplacementSurvivesStaleStop", playback.IsRunning,
                    "command generation=" + playback.CommandGeneration);
                long revision = playback.SourceRevision;
                replacementRun = null;
                DestroyObject(replacementHolder);
                var followRun = BeginRun(out GameObject followHolder);
                try
                {
                    followRun.Toys.Follow();
                    followRun.Performer.FollowGripMotion();
                    Record("sharedClockFollowDoesNotSeek", playback.SourceRevision == revision,
                        "source revision=" + playback.SourceRevision);
                }
                catch (Exception exception) { Record("sharedClockFollowDoesNotSeek", false, exception.ToString()); }
                finally { followRun.Dispose(); DestroyObject(followHolder); }
            }
            catch (Exception exception) { Record("sameSourceReplacementSurvivesStaleStop", false, exception.ToString()); }
            finally
            {
                replacementRun?.Dispose();
                playback.StopMotion();
                playback.ResetMotion();
                DestroyObject(replacementHolder);
                await Awaitable.NextFrameAsync();
            }
        }

        private async Awaitable CheckAuraReplacement()
        {
            var run = BeginRun(out GameObject holder);
            try
            {
                run.Performer.Aura(firstAura, actor.transform);
                actor.Aura(secondAura, actor.transform);
                run.Stop();
                Record("newerAuraSurvivesStop", actor.CurrentAura == secondAura,
                    actor.CurrentAura != null ? actor.CurrentAura.name : "none");
            }
            catch (Exception exception) { Record("auraGenerationCleanup", false, exception.ToString()); }
            finally { run.Dispose(); actor.ClearAura(); DestroyObject(holder); await Awaitable.NextFrameAsync(); }
        }

        private async Awaitable WaitRealtime(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) await Awaitable.NextFrameAsync();
        }

        private async Awaitable CheckToyDelivery()
        {
            foreach (string mode in new[] { "connected", "disconnected", "rejected", "slow" })
            {
                backend.Reset();
                backend.Connected = mode != "disconnected";
                backend.Fail = mode == "rejected";
                backend.DelayMilliseconds = mode == "slow" ? 500 : 0;
                var run = BeginRun(out GameObject holder);
                try
                {
                    var beats = new List<string> { "before" };
                    run.Toys.Vibrate(.35f);
                    run.Toys.RampVibration(.8f, .05f);
                    run.Toys.Oscillate(.2f, 999); // Missing route is also a no-op.
                    run.Toys.Vibrate(float.NaN);
                    beats.Add("submitted");
                    await run.Delay(.02f);
                    beats.Add("after");
                    Record("silentToyBeatOrder_" + mode, !run.IsStopped && run.Failure == null
                        && string.Join(",", beats) == "before,submitted,after",
                        string.Join(",", beats));
                }
                catch (Exception exception) { Record("silentToyBeatOrder_" + mode, false, exception.ToString()); }
                finally { run.Stop(); run.Dispose(); DestroyObject(holder); await WaitRealtime(.55f); }
            }

            foreach (bool paused in new[] { false, true })
            {
                backend.Reset(); backend.DelayMilliseconds = 200;
                var run = BeginRun(out GameObject holder);
                float priorScale = Time.timeScale;
                try
                {
                    run.StartPlayback(program, true);
                    run.Toys.Follow();
                    run.Toys.RampVibration(.9f, 20f);
                    run.Toys.MoveTo(.8f, 20f);
                    await Awaitable.NextFrameAsync();
                    if (paused) Time.timeScale = 0f;
                    run.Stop();
                    int stoppedCount = backend.Commands.Count(item => item != "STOP");
                    run.Toys.Vibrate(1f);
                    run.Toys.Follow();
                    await WaitRealtime(.4f);
                    int laterCount = backend.Commands.Count(item => item != "STOP");
                    Record("stopRevokesRampFollowerAndTimedPosition_" + paused,
                        laterCount == stoppedCount && !playback.IsRunning && !toys.IsFollowingMotion,
                        "ordinaryBefore=" + stoppedCount + "; after=" + laterCount);
                }
                catch (Exception exception) { Record("stopRevokesRampFollowerAndTimedPosition_" + paused, false, exception.ToString()); }
                finally { Time.timeScale = priorScale; run.Dispose(); DestroyObject(holder); }
            }

            backend.Reset();
            var old = BeginRun(out GameObject oldHolder);
            old.Toys.RampVibration(.8f, 20f);
            old.Stop();
            var replacement = BeginRun(out GameObject replacementHolder);
            try
            {
                replacement.StartPlayback(program, true);
                replacement.Toys.Follow();
                old.Toys.Vibrate(1f); old.Toys.Follow(); old.Dispose(); old.Stop();
                await WaitRealtime(.1f);
                Record("oldFacadeAndDisposeCannotStopReplacement", !replacement.IsStopped
                    && playback.IsRunning && toys.IsFollowingMotion, "replacement=" + playback.IsRunning);

                bool partial = false;
                var raw = new AwaitableCompletionSource<int>();
                Awaitable<int> pending = replacement.Execute("partial failure",
                    () => { partial = true; return raw.Awaitable; }, value => value == 1,
                    captureCancellation: () => () => partial = false);
                raw.SetResult(0);
                try { await pending; } catch (PerformanceCommandException) { }
                Record("failedOperationReleasesPartialWork", !partial && replacement.IsStopped,
                    "partial=" + partial + "; stopped=" + replacement.IsStopped);
            }
            catch (Exception exception) { Record("oldFacadeAndDisposeCannotStopReplacement", false, exception.ToString()); }
            finally { old.Dispose(); replacement.Dispose(); DestroyObject(oldHolder); DestroyObject(replacementHolder); }
            backend.Reset();
        }

        private async Awaitable CheckGripOwnership()
        {
            var run = BeginRun(out GameObject holder);
            bool acquired = false;
            try
            {
                GripCompletion result = await run.Performer.GripAsync(ContactTarget);
                acquired = result == GripCompletion.Acquired;
                Record("scopedGripAcquires", acquired, result.ToString());
                if (acquired)
                {
                    run.Stop();
                    Record("stopReleasesAcquiredGrip", !actor.IsGripping,
                        "state=" + actor.GripState + "; gripping=" + actor.IsGripping);
                }
            }
            catch (Exception exception) { Record("scopedGripAcquires", false, exception.ToString()); }
            finally { run.Dispose(); DestroyObject(holder); }

            if (!acquired) return;
            var releaseRun = BeginRun(out GameObject releaseHolder);
            try
            {
                GripCompletion reacquired = await releaseRun.Performer.GripAsync(ContactTarget);
                if (reacquired != GripCompletion.Acquired)
                {
                    Record("animatedGripReleaseCompletes", false, reacquired.ToString());
                    return;
                }
                GripCompletion released = await releaseRun.Performer.ReleaseGripAsync();
                Record("animatedGripReleaseCompletes", released == GripCompletion.Released && !actor.IsGripping,
                    released + "; state=" + actor.GripState);
            }
            catch (Exception exception) { Record("animatedGripReleaseCompletes", false, exception.ToString()); }
            finally { releaseRun.Dispose(); actor.ReleaseGrip(); DestroyObject(releaseHolder); await Awaitable.NextFrameAsync(); }
        }

        private void CheckMissingOptionalBindings()
        {
            var holder = new GameObject("R07 missing optional bindings");
            try
            {
                var missing = holder.AddComponent<PerformanceSceneBindings>();
                missing.Configure(actor, null, null, null, PerformerCapability.Pose | PerformerCapability.Speech);
                missing.DeclareAssets(null, null, new[] { firstClip, secondClip });
                var run = PerformanceRun.Begin(missing);
                try
                {
                    bool playerRejected = false;
                    bool toysRejected = false;
                    try { _ = run.Player; } catch (PerformanceCommandException exception) { playerRejected = exception.OriginalStatus == "MissingBinding"; }
                    run.Toys.Vibrate(.2f); run.Toys.Follow();
                    Record("missingPlayerIsExplicitAndMissingToysAreSilent", playerRejected && !toysRejected,
                        "player=" + playerRejected + "; toys=" + toysRejected);
                }
                finally { run.Dispose(); }
            }
            catch (Exception exception) { Record("missingOptionalPlayerAndToysFailClearly", false, exception.ToString()); }
            finally { DestroyObject(holder); }
        }

        private PerformanceRun BeginRun(out GameObject holder)
        {
            holder = new GameObject("R07 scoped bindings");
            var sceneBindings = holder.AddComponent<PerformanceSceneBindings>();
            sceneBindings.Configure(actor, player, playback, toys, PerformerCapability.Pose | PerformerCapability.Speech);
            sceneBindings.ConfigureContactTarget(contactBehaviour);
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
            string output = string.IsNullOrWhiteSpace(reportPath) ? Path.Combine("TestOutput", "remediation", "R07.json") : reportPath;
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

        private sealed class FakeBackend : IToyBackend
        {
            private readonly List<ToyDevice> devices = new List<ToyDevice>
            {
                new ToyDevice(1, "R07 fake", "R07 fake", 0, new[]
                {
                    new ToyFeature(0, "vibration", new[] { ToyOutputRange.Value(ToyOutputCapability.Vibrate, 0, 100),
                        ToyOutputRange.Value(ToyOutputCapability.Oscillate, 0, 100),
                        ToyOutputRange.Value(ToyOutputCapability.Position, 0, 100),
                        ToyOutputRange.PositionWithDuration(0, 100, 1, 30000) })
                })
            };

            public readonly List<string> Commands = new List<string>();
            public bool Connected = true;
            public bool Fail;
            public int DelayMilliseconds;
            public ToyConnectionState ConnectionState => Connected ? ToyConnectionState.Connected : ToyConnectionState.Disconnected;
            public string StatusMessage => Connected ? "R07 fake connected" : "R07 fake disconnected";
            public string LastError => Fail ? "R07 fake failure" : string.Empty;
            public bool IsConnected => Connected;
            public bool IsScanning => false;
            public IReadOnlyList<ToyDevice> Devices => devices;
            public event Action<ToyConnectionState, string> StateChanged { add { } remove { } }
            public event Action<ToyDevice> DeviceAdded { add { } remove { } }
            public event Action<uint> DeviceRemoved { add { } remove { } }
            public event Action<bool> ScanningChanged { add { } remove { } }

            public Task ConnectAsync(string serverAddress) => Task.CompletedTask;
            public Task DisconnectAsync() { Connected = false; return Task.CompletedTask; }
            public Task StartScanningAsync() => Task.CompletedTask;
            public Task StopScanningAsync() => Task.CompletedTask;
            public Task StopAllAsync() { Commands.Add("STOP"); return Task.CompletedTask; }

            public async Task SendOutputAsync(ToyOutputBinding binding, float normalizedValue,
                uint durationMilliseconds = 0, Func<bool> stillCurrent = null)
            {
                if (!Connected) throw new InvalidOperationException("R07 fake disconnected.");
                if (DelayMilliseconds > 0) await Task.Delay(DelayMilliseconds);
                if (!Connected) throw new InvalidOperationException("R07 fake disconnected before acknowledgement.");
                if (Fail) throw new InvalidOperationException("R07 fake backend exception.");
                if (stillCurrent != null && !stillCurrent()) return;
                Commands.Add(binding + "=" + normalizedValue.ToString("F3"));
            }

            public void Reset()
            {
                Connected = true;
                Fail = false;
                DelayMilliseconds = 0;
                Commands.Clear();
            }

            public void Dispose() { }
        }
    }
}
