using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DazPose.Performer;
using UnityEngine;

namespace DazPose.Validation
{
    /// <summary>R06 execution witness for the single scoped finite-command kernel.</summary>
    public sealed class RemediationScopedCommandRunner : MonoBehaviour
    {
        [Serializable] private sealed class Assertion
        { public string name, expected, actual; public bool passed; }

        [Serializable] private sealed class Report
        {
            public string ticket = "R06", startedUtc, completedUtc, sourceHash, exception;
            public string[] inputAssetHashes;
            public bool complete, passed;
            public List<Assertion> assertions = new List<Assertion>();
        }

        [SerializeField] private SuccubusPerformer actor;
        [SerializeField] private PerformanceSceneBindings bindings;
        [SerializeField] private AudioClip shortClip;
        [SerializeField] private AudioClip secondClip;
        [SerializeField] private AudioClip longClip;
        [SerializeField] private string reportPath, sourceHash, inputHash;
        private bool finished;
        private Report report;

#if UNITY_EDITOR
        public void Configure(SuccubusPerformer owner, PerformanceSceneBindings sceneBindings,
            AudioClip first, AudioClip second, AudioClip longAudio, string output, string source, string input)
        {
            actor = owner;
            bindings = sceneBindings;
            shortClip = first;
            secondClip = second;
            longClip = longAudio != null ? longAudio : first;
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
                for (int frame = 0; frame < 240 && (actor == null || !actor.IsRuntimeReady); ++frame)
                    await Awaitable.NextFrameAsync();
                await RunChecks();
                Finish(report.assertions.All(item => item.passed), null);
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private async Awaitable RunChecks()
        {
            Record("fixtureReferences", actor != null && bindings != null && shortClip != null && secondClip != null,
                "Actor, bindings and two speech clips are required.");
            if (actor == null || bindings == null || shortClip == null || secondClip == null) return;
            if (longClip == null) longClip = shortClip;

            await CheckBeginAndSerialSuccess();
            await CheckConcurrentSpeechAndGaze();
            await CheckSynchronousStartException();
            await CheckUnexpectedTypedResultAndReplacement();
            await CheckStopBeforeStartAndIdempotence();
            await CheckStopDuringRequest();
            await CheckStopDuringScaledDelay();
            await CheckFirstFailureCancelsSibling();
            await CheckStopFinishRace();
            await CheckInvalidBegin();
        }

        private async Awaitable CheckBeginAndSerialSuccess()
        {
            var run = BeginRun(out GameObject holder);
            try
            {
                SpeechCompletion first = await run.Performer.SayAsync(shortClip);
                SpeechCompletion second = await run.Performer.SayAsync(secondClip);
                Record("serialTypedSuccess", first == SpeechCompletion.Finished && second == SpeechCompletion.Finished,
                    first + ", " + second);
            }
            catch (Exception exception) { Record("serialTypedSuccess", false, exception.ToString()); }
            finally { run.Dispose(); DestroyObject(holder); }
        }

        private async Awaitable CheckConcurrentSpeechAndGaze()
        {
            var run = BeginRun(out GameObject holder);
            var target = new GameObject("R06 gaze target");
            target.transform.position = actor.transform.position + actor.transform.forward * 2f + Vector3.up;
            try
            {
                Awaitable<SpeechCompletion> speech = run.Performer.SayAsync(shortClip);
                Awaitable<GazeCompletion> gaze = run.Performer.LookAtAsync(target.transform);
                SpeechCompletion speechResult = await speech;
                GazeCompletion gazeResult = await gaze;
                Record("concurrentSpeechAndGaze", speechResult == SpeechCompletion.Finished && gazeResult == GazeCompletion.Acquired,
                    speechResult + ", " + gazeResult);
            }
            catch (Exception exception) { Record("concurrentSpeechAndGaze", false, exception.ToString()); }
            finally { run.Dispose(); DestroyObject(target); DestroyObject(holder); }
        }

        private async Awaitable CheckSynchronousStartException()
        {
            var run = BeginRun(out GameObject holder);
            try
            {
                Awaitable<SpeechCompletion> invalid = run.Performer.SayAsync(null);
                await invalid;
                Record("synchronousValidationException", false, "null speech clip unexpectedly completed");
            }
            catch (PerformanceCommandException exception)
            { Record("synchronousValidationException", exception.OriginalStatus == "StartException", exception.ToString()); }
            catch (Exception exception) { Record("synchronousValidationException", false, exception.ToString()); }
            finally { run.Dispose(); DestroyObject(holder); }
        }

        private async Awaitable CheckUnexpectedTypedResultAndReplacement()
        {
            var run = BeginRun(out GameObject holder);
            try
            {
                Awaitable<SpeechCompletion> first = run.Performer.SayAsync(longClip);
                await Awaitable.NextFrameAsync();
                actor.StopSpeaking();
                await first;
                Record("unexpectedTypedResult", false, "superseded speech unexpectedly succeeded");
            }
            catch (PerformanceCommandException exception)
            {
                bool mapped = exception.OriginalStatus == SpeechCompletion.Cancelled.ToString();
                Record("unexpectedTypedResult", mapped, exception.ToString());
                try
                {
                    SpeechCompletion replacement = await actor.SayAsync(secondClip);
                    Record("externalReplacementSurvivesStaleCancellation", replacement == SpeechCompletion.Finished, replacement.ToString());
                }
                catch (Exception replacementException)
                { Record("externalReplacementSurvivesStaleCancellation", false, replacementException.ToString()); }
            }
            catch (Exception exception) { Record("unexpectedTypedResult", false, exception.ToString()); }
            finally { run.Dispose(); DestroyObject(holder); }
        }

        private async Awaitable CheckStopBeforeStartAndIdempotence()
        {
            var run = BeginRun(out GameObject holder);
            run.Stop();
            run.Stop();
            try
            {
                await run.Delay(0f);
                Record("stopBeforeStart", false, "stopped Delay unexpectedly completed");
            }
            catch (OperationCanceledException exception)
            { Record("stopBeforeStart", true, exception.Message); }
            catch (Exception exception) { Record("stopBeforeStart", false, exception.ToString()); }
            finally { run.Dispose(); DestroyObject(holder); }
        }

        private async Awaitable CheckStopDuringRequest()
        {
            var run = BeginRun(out GameObject holder);
            try
            {
                Awaitable<SpeechCompletion> operation = run.Performer.SayAsync(longClip);
                await Awaitable.NextFrameAsync();
                run.Stop();
                await operation;
                Record("stopDuringRequest", false, "speech completed after explicit Stop");
            }
            catch (OperationCanceledException exception) { Record("stopDuringRequest", true, exception.Message); }
            catch (Exception exception) { Record("stopDuringRequest", false, exception.ToString()); }
            finally { run.Dispose(); DestroyObject(holder); }
        }

        private async Awaitable CheckStopDuringScaledDelay()
        {
            var run = BeginRun(out GameObject holder);
            float originalScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                Awaitable delay = run.Delay(.25f);
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();
                run.Stop();
                await delay;
                Record("stopDuringPausedDelay", false, "paused Delay completed without Stop cancellation");
            }
            catch (OperationCanceledException exception) { Record("stopDuringPausedDelay", true, exception.Message); }
            catch (Exception exception) { Record("stopDuringPausedDelay", false, exception.ToString()); }
            finally { Time.timeScale = originalScale; run.Dispose(); DestroyObject(holder); }
        }

        private async Awaitable CheckFirstFailureCancelsSibling()
        {
            var run = BeginRun(out GameObject holder);
            bool siblingCancelled = false;
            try
            {
                Awaitable<SpeechCompletion> sibling = run.Performer.SayAsync(longClip);
                Awaitable<SpeechCompletion> failure = run.Performer.SayAsync(null);
                try { await failure; } catch (PerformanceCommandException) { }
                try { await sibling; } catch (OperationCanceledException) { siblingCancelled = true; }
                Record("firstFailureCancelsPendingSibling", siblingCancelled && run.IsStopped,
                    siblingCancelled ? "pending sibling cancelled" : "pending sibling remained active");
            }
            catch (Exception exception) { Record("firstFailureCancelsPendingSibling", false, exception.ToString()); }
            finally { run.Dispose(); DestroyObject(holder); }
        }

        private async Awaitable CheckStopFinishRace()
        {
            var run = BeginRun(out GameObject holder);
            try
            {
                Awaitable<SpeechCompletion> operation = run.Performer.SayAsync(shortClip);
                run.Stop();
                try { await operation; } catch (OperationCanceledException) { }
                Record("stopFinishRaceSettlesOnce", true, "completion settled once under Stop/finish race");
            }
            catch (Exception exception) { Record("stopFinishRaceSettlesOnce", false, exception.ToString()); }
            finally { run.Dispose(); DestroyObject(holder); }
        }

        private async Awaitable CheckInvalidBegin()
        {
            object original = GetField(actor, "speechAudioSource");
            SetField(actor, "speechAudioSource", null);
            try
            {
                var holder = new GameObject("R06 invalid begin bindings");
                var invalid = holder.AddComponent<PerformanceSceneBindings>();
                invalid.Configure(actor, null, null, null, PerformerCapability.Speech);
                try
                {
                    PerformanceRun.Begin(invalid);
                    Record("invalidBeginFailsBeforePlayback", false, "Begin unexpectedly accepted missing speech source");
                }
                catch (PerformanceCommandException exception)
                { Record("invalidBeginFailsBeforePlayback", exception.OriginalStatus == "InvalidBindings", exception.ToString()); }
                finally { DestroyObject(holder); }
            }
            catch (Exception exception) { Record("invalidBeginFailsBeforePlayback", false, exception.ToString()); }
            finally { SetField(actor, "speechAudioSource", original); await Awaitable.NextFrameAsync(); }
        }

        private PerformanceRun BeginRun(out GameObject holder)
        {
            holder = new GameObject("R06 scoped bindings");
            var probe = holder.AddComponent<PerformanceSceneBindings>();
            probe.Configure(actor, null, null, null, PerformerCapability.Speech);
            probe.DeclareAssets(null, null, new[] { shortClip, secondClip, longClip });
            return PerformanceRun.Begin(probe);
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
            string output = string.IsNullOrWhiteSpace(reportPath) ? Path.Combine("TestOutput", "remediation", "R06.json") : reportPath;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(passed ? 0 : 1);
#else
            if (Application.isBatchMode) Application.Quit(passed ? 0 : 1);
#endif
        }

        private static object GetField(object owner, string name) => owner.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private static void SetField(object owner, string name, object value) => owner.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
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
