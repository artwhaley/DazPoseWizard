using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEngine;

namespace DazPose.Validation
{
    /// <summary>Saved-scene R12 player witness for ordinary-folder content and scoped Stop/replay.</summary>
    [DisallowMultipleComponent]
    public sealed class RemediationValidationPlayerRunner : MonoBehaviour
    {
        [Serializable] private sealed class Assertion
        {
            public string name;
            public string expected;
            public string actual;
            public bool passed;
        }

        [Serializable] private sealed class Report
        {
            public int schemaVersion = 1;
            public string startedUtc;
            public string completedUtc;
            public string sourceHash;
            public string inputAssetHash;
            public string graphicsDevice;
            public bool passed;
            public Assertion[] assertions;
        }

        [SerializeField] private SuccubusPerformer performer;
        [SerializeField] private PerformanceSceneBindings bindings;
        [SerializeField] private PerformerGesture ordinaryFolderGesture;
        [SerializeField] private PerformerAction ordinaryFolderAction;
        [SerializeField] private AudioClip replaySpeechClip;
        [SerializeField] private AudioClip interruptedSpeechClip;
        [SerializeField] private string sourceHash;
        [SerializeField] private string inputAssetHash;
        private readonly List<Assertion> assertions = new List<Assertion>();
        private bool complete;

#if UNITY_EDITOR
        public void Configure(SuccubusPerformer actor, PerformanceSceneBindings sceneBindings,
            PerformerGesture gesture, PerformerAction action, AudioClip replayClip, AudioClip longClip,
            string source, string input)
        {
            performer = actor;
            bindings = sceneBindings;
            ordinaryFolderGesture = gesture;
            ordinaryFolderAction = action;
            replaySpeechClip = replayClip;
            interruptedSpeechClip = longClip;
            sourceHash = source;
            inputAssetHash = input;
        }
#endif

        private async void Start()
        {
            // The ticket launcher intentionally hides this graphics-enabled player.
            // Keep Unity's frame loop active so the async validation can progress.
            Application.runInBackground = true;
            string started = DateTime.UtcNow.ToString("O");
            PerformanceRun activeRun = null;
            try
            {
                Check("graphicsDevice", "Direct3D12 is active", SystemInfo.graphicsDeviceType + ";" + SystemInfo.graphicsDeviceName,
                    SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D12);
                Check("serializedReferences", "actor, bindings, ordinary-folder Gesture/Action and speech clips are assigned",
                    "actor=" + (performer != null) + ";bindings=" + (bindings != null) + ";gesture=" + (ordinaryFolderGesture != null)
                    + ";action=" + (ordinaryFolderAction != null) + ";replayClip=" + (replaySpeechClip != null)
                    + ";interruptClip=" + (interruptedSpeechClip != null),
                    performer != null && bindings != null && ordinaryFolderGesture != null && ordinaryFolderAction != null
                        && replaySpeechClip != null && interruptedSpeechClip != null);
                RequireLast();

                for (int frame = 0; frame < 600 && !performer.IsRuntimeReady; frame++) await Awaitable.NextFrameAsync();
                Check("performerRuntimeReady", "Lara runtime initializes in the saved scene",
                    "ready=" + performer.IsRuntimeReady + ";speechSource=" + (performer.SpeechAudioSource != null) + ";gesture=" + performer.GestureAvailable
                    + ";action=" + performer.ActionAvailable,
                    performer.IsRuntimeReady && performer.SpeechAudioSource != null && performer.GestureAvailable && performer.ActionAvailable);
                RequireLast();

                BindingIssue[] bindingIssues = bindings.Validate(runtime: true);
                Check("savedTypedBindingsValid", "runtime Speech, Gesture and Action declarations validate",
                    bindingIssues.Length == 0 ? "no issues" : string.Join(" | ", bindingIssues.Select(issue => issue.ToString())),
                    bindingIssues.Length == 0);
                RequireLast();

                activeRun = PerformanceRun.Begin(bindings);
                VisibilityCompletion visibility = await activeRun.Performer.DissolveInAsync(.25f);
                Check("hiddenActorDissolvesInBeforeContent", "Visible", visibility + ";state=" + performer.VisibilityState,
                    visibility == VisibilityCompletion.Visible && performer.VisibilityState == PerformerVisibilityState.Visible);
                RequireLast();

                GestureCompletion gesture = await activeRun.Performer.GestureAsync(ordinaryFolderGesture);
                Check("ordinaryFolderGestureExecutesInPlayer", "Completed", gesture.ToString(), gesture == GestureCompletion.Completed);
                RequireLast();
                ActionCompletion action = await activeRun.Performer.PerformAsync(ordinaryFolderAction);
                Check("ordinaryFolderActionExecutesInPlayer", "Completed", action.ToString(), action == ActionCompletion.Completed);
                RequireLast();

                Awaitable<SpeechCompletion> interrupted = activeRun.Performer.SayAsync(interruptedSpeechClip);
                bool speechStarted = await WaitForSpeech(interruptedSpeechClip, 120);
                activeRun.Stop();
                bool cancelled = false;
                try { await interrupted; }
                catch (OperationCanceledException) { cancelled = true; }
                await Awaitable.NextFrameAsync();
                bool speechCleared = performer.CurrentSpeechClip == null;
                Check("scopedStopCancelsActiveSpeech", "accepted speech starts, Stop cancels it and clears the actor source",
                    "started=" + speechStarted + ";cancelled=" + cancelled + ";cleared=" + speechCleared,
                    speechStarted && cancelled && speechCleared);
                RequireLast();
                activeRun.Dispose();
                activeRun = null;

                using (PerformanceRun replay = PerformanceRun.Begin(bindings))
                {
                    SpeechCompletion completion = await replay.Performer.SayAsync(replaySpeechClip);
                    Check("scopedReplayCompletesSpeech", "Finished", completion.ToString(), completion == SpeechCompletion.Finished);
                    RequireLast();
                }
            }
            catch (Exception exception)
            {
                Check("playerRunnerException", "no exception", exception.ToString(), false);
            }
            finally
            {
                activeRun?.Dispose();
                await Awaitable.NextFrameAsync();
                Finish(started);
            }
        }

        private async Awaitable<bool> WaitForSpeech(AudioClip clip, int maxFrames)
        {
            for (int frame = 0; frame < maxFrames; frame++)
            {
                if (performer != null && performer.CurrentSpeechClip == clip) return true;
                await Awaitable.NextFrameAsync();
            }
            return performer != null && performer.CurrentSpeechClip == clip;
        }

        private void Check(string name, string expected, string actual, bool passed) =>
            assertions.Add(new Assertion { name = name, expected = expected, actual = actual ?? string.Empty, passed = passed });

        private void RequireLast()
        {
            if (!assertions[assertions.Count - 1].passed)
                throw new InvalidOperationException(assertions[assertions.Count - 1].name + ": " + assertions[assertions.Count - 1].actual);
        }

        private void Finish(string started)
        {
            if (complete) return;
            complete = true;
            var report = new Report
            {
                startedUtc = started,
                completedUtc = DateTime.UtcNow.ToString("O"),
                sourceHash = sourceHash ?? string.Empty,
                inputAssetHash = inputAssetHash ?? string.Empty,
                graphicsDevice = SystemInfo.graphicsDeviceType + ";" + SystemInfo.graphicsDeviceName,
                passed = assertions.Count > 0 && assertions.All(assertion => assertion.passed),
                assertions = assertions.ToArray()
            };
            string output = ReadArgument("-remediationPlayerReport");
            if (string.IsNullOrWhiteSpace(output)) output = Path.Combine(Application.persistentDataPath, "remediation-validation-report.json");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                Debug.Log("R12_VALIDATION_PLAYER_" + (report.passed ? "PASSED" : "FAILED") + "|" + output);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Application.Quit(2);
                return;
            }
            Application.Quit(report.passed ? 0 : 1);
        }

        private static string ReadArgument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1].Trim('"') : string.Empty;
        }
    }
}
