using System;
using System.Linq;
using DazPose.Performer;
using UnityEngine;

namespace DazPose.FirstPerformanceVoid
{
    /// <summary>Minimal Examples-only production scene driver for ready, speech, Stop and replay.</summary>
    [DisallowMultipleComponent]
    public sealed class RemediationProductionSmokeRunner : MonoBehaviour
    {
        [SerializeField] private SuccubusPerformer performer;
        [SerializeField] private PerformanceSceneBindings bindings;
        [SerializeField] private AudioClip speechClip;
        private bool finished;
        private PerformanceRun activeRun;

#if UNITY_EDITOR
        public void Configure(SuccubusPerformer actor, PerformanceSceneBindings sceneBindings, AudioClip clip)
        {
            performer = actor;
            bindings = sceneBindings;
            speechClip = clip;
        }
#endif

        private async void Start()
        {
            // The ticket launcher intentionally hides this graphics-enabled player.
            // Keep Unity's frame loop active so the async smoke can progress.
            Application.runInBackground = true;
            try
            {
                if (performer == null || bindings == null || speechClip == null)
                    throw new InvalidOperationException("Production smoke requires serialized actor, typed bindings and speech clip.");
                for (int frame = 0; frame < 600 && !performer.IsRuntimeReady; frame++) await Awaitable.NextFrameAsync();
                if (!performer.IsRuntimeReady || performer.SpeechAudioSource == null)
                    throw new InvalidOperationException("Lara speech runtime did not become ready in the production scene.");
                BindingIssue[] issues = bindings.Validate(runtime: true);
                if (issues.Length != 0) throw new InvalidOperationException("Production typed bindings failed: " + string.Join(" | ", issues.Select(issue => issue.ToString())));

                activeRun = PerformanceRun.Begin(bindings);
                Mark("READY", "actor=" + performer.name + ";capabilities=" + bindings.RequiredCapabilities);
                Awaitable<SpeechCompletion> interruptedSpeech = activeRun.Performer.SayAsync(speechClip);
                bool speechStarted = await WaitForSpeech(speechClip, 120);
                if (!speechStarted) throw new TimeoutException("The authored speech clip did not become active before the Stop check.");
                Mark("SPEECH_STARTED", speechClip.name);
                activeRun.Stop();
                bool cancelled = false;
                try { await interruptedSpeech; }
                catch (OperationCanceledException) { cancelled = true; }
                await Awaitable.NextFrameAsync();
                bool cleared = performer.CurrentSpeechClip == null;
                if (!cancelled || !cleared)
                    throw new InvalidOperationException("Scoped Stop did not cancel and clear active speech; cancelled=" + cancelled + "; cleared=" + cleared);
                activeRun.Dispose();
                activeRun = null;
                Mark("STOPPED", "active speech cancelled and cleared");

                using (PerformanceRun replay = PerformanceRun.Begin(bindings))
                {
                    SpeechCompletion completion = await replay.Performer.SayAsync(speechClip);
                    if (completion != SpeechCompletion.Finished)
                        throw new InvalidOperationException("Replay speech did not finish; result=" + completion);
                }
                Mark("REPLAY_COMPLETE", "speech=" + speechClip.name + ";result=Finished");
                Mark("PASSED", "production Runtime/Examples smoke completed");
                Finish(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Mark("FAILED", exception.GetType().Name + ":" + exception.Message);
                Finish(1);
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

        private void Mark(string eventName, string detail) =>
            Debug.Log("R12_PRODUCTION_SMOKE|" + eventName + "|" + (detail ?? string.Empty), this);

        private void Finish(int exitCode)
        {
            if (finished) return;
            finished = true;
            activeRun?.Dispose();
            activeRun = null;
            Application.Quit(exitCode);
        }

        private void OnDisable()
        {
            activeRun?.Dispose();
            activeRun = null;
        }
    }
}
