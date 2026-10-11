using System;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.Performer
{
    internal static class PerformerSpeechRuntimeSelfTests
    {
        public static string[] Run()
        {
            var failures = new List<string>();
            var host = new GameObject("P09ASpeechRuntimeSelfTest");
            var source = host.AddComponent<AudioSource>();
            var clipA = CreateClip("P09A_Speech_A");
            var clipB = CreateClip("P09A_Speech_B");
            var clipC = CreateClip("P09A_Speech_C");
            var clipD = CreateClip("P09A_Speech_D");
            PerformerSpeech speech = null;

            try
            {
                source.playOnAwake = true;
                source.loop = true;
                source.volume = 0.37f;
                source.pitch = 0.83f;
                source.spatialBlend = 0.62f;
                source.dopplerLevel = 0.28f;
                source.minDistance = 1.7f;
                source.maxDistance = 31f;
                source.rolloffMode = AudioRolloffMode.Linear;

                speech = new PerformerSpeech(source);
                Check(!source.loop && !source.playOnAwake, "speech source disables looping and play-on-awake", failures);
                Check(Mathf.Abs(source.volume - 0.37f) < 0.0001f
                    && Mathf.Abs(source.pitch - 0.83f) < 0.0001f
                    && Mathf.Abs(source.spatialBlend - 0.62f) < 0.0001f
                    && Mathf.Abs(source.dopplerLevel - 0.28f) < 0.0001f
                    && Mathf.Abs(source.minDistance - 1.7f) < 0.0001f
                    && Mathf.Abs(source.maxDistance - 31f) < 0.0001f
                    && source.rolloffMode == AudioRolloffMode.Linear,
                    "speech runtime preserves authored audio-design settings", failures);

                CheckThrows<ArgumentNullException>(() => speech.Say(null), "Say rejects a null clip", failures);
                CheckThrows<ArgumentNullException>(() => speech.SayAsync(null), "SayAsync rejects a null clip", failures);
                source.enabled = false;
                CheckThrows<InvalidOperationException>(() => speech.Say(clipA), "Say rejects an inactive speech AudioSource", failures);
                source.enabled = true;

                var single = new Observation();
                Observe(speech.SayAsync(clipA), single);
                Check(speech.IsSpeaking && speech.CurrentSpeechClip == clipA
                    && speech.PendingSpeechCount == 0 && source.clip == clipA,
                    "an idle SayAsync request starts immediately on its dedicated AudioSource", failures);
                CompleteCurrent(speech, source);
                Check(single.Completed && single.Result == SpeechCompletion.Finished
                    && single.CompletionCount == 1
                    && !speech.IsSpeaking && speech.CurrentSpeechClip == null
                    && speech.PendingSpeechCount == 0 && source.clip == null,
                    "a single request completes Finished after observed playback ends", failures);

                var completionOrder = new List<string>();
                var orderA = new Observation();
                var orderB = new Observation();
                var orderC = new Observation();
                Observe(speech.SayAsync(clipA), orderA, () => completionOrder.Add("A"));
                Observe(speech.SayAsync(clipB), orderB, () => completionOrder.Add("B"));
                Observe(speech.SayAsync(clipC), orderC, () => completionOrder.Add("C"));
                Check(speech.CurrentSpeechClip == clipA && speech.PendingSpeechCount == 2 && source.clip == clipA,
                    "A, B, C remain queued in FIFO order without interrupting A", failures);
                CompleteCurrent(speech, source);
                Check(orderA.Completed && orderA.Result == SpeechCompletion.Finished
                    && speech.CurrentSpeechClip == clipB && speech.PendingSpeechCount == 1 && source.clip == clipB
                    && completionOrder.Count == 1 && completionOrder[0] == "A",
                    "A completion promotes B before resuming A waiters", failures);
                CompleteCurrent(speech, source);
                Check(orderB.Completed && orderB.Result == SpeechCompletion.Finished
                    && speech.CurrentSpeechClip == clipC && speech.PendingSpeechCount == 0 && source.clip == clipC
                    && completionOrder.Count == 2 && completionOrder[1] == "B",
                    "B completes before C starts", failures);
                CompleteCurrent(speech, source);
                Check(orderC.Completed && orderC.Result == SpeechCompletion.Finished
                    && completionOrder.Count == 3 && completionOrder[2] == "C" && !speech.IsSpeaking,
                    "the full queue completes A, B, C in order", failures);

                var duplicateStartCount = speech.StartedRequestCount;
                var duplicateA = new Observation();
                var duplicateB = new Observation();
                Observe(speech.SayAsync(clipA), duplicateA);
                var firstDuplicateId = speech.CurrentRequestId;
                Observe(speech.SayAsync(clipA), duplicateB);
                Check(speech.PendingSpeechCount == 1 && speech.CurrentRequestId == firstDuplicateId,
                    "identical clips occupy separate current and pending requests", failures);
                CompleteCurrent(speech, source);
                Check(speech.CurrentSpeechClip == clipA && speech.CurrentRequestId != firstDuplicateId
                    && duplicateA.Result == SpeechCompletion.Finished && !duplicateB.Completed,
                    "a repeated clip starts again as a distinct request", failures);
                CompleteCurrent(speech, source);
                Check(duplicateB.Completed && duplicateB.Result == SpeechCompletion.Finished
                    && speech.StartedRequestCount - duplicateStartCount == 2,
                    "both identical requests genuinely start and finish", failures);

                var mixedA = new Observation();
                var mixedB = new Observation();
                speech.Say(clipA);
                Observe(speech.SayAsync(clipB), mixedB);
                speech.Say(clipC);
                Observe(speech.SayAsync(clipA), mixedA);
                Check(speech.CurrentSpeechClip == clipA && speech.PendingSpeechCount == 3 && source.clip == clipA,
                    "fire-and-forget and awaitable calls share one FIFO", failures);
                CompleteCurrent(speech, source);
                Check(speech.CurrentSpeechClip == clipB && !mixedB.Completed,
                    "mixed API requests start in enqueue order", failures);
                CompleteCurrent(speech, source);
                Check(mixedB.Completed && mixedB.Result == SpeechCompletion.Finished
                    && speech.CurrentSpeechClip == clipC && !mixedA.Completed,
                    "SayAsync for B completes only after B while C is promoted first", failures);
                CompleteCurrent(speech, source);
                Check(speech.CurrentSpeechClip == clipA && !mixedA.Completed,
                    "a later fire-and-forget C remains ahead of the later async A", failures);
                CompleteCurrent(speech, source);
                Check(mixedA.Completed && mixedA.Result == SpeechCompletion.Finished && !speech.IsSpeaking,
                    "mixed APIs preserve request identity and completion", failures);

                var cancelA = new Observation();
                var cancelB = new Observation();
                var cancelC = new Observation();
                Observe(speech.SayAsync(clipA), cancelA, () => speech.Say(clipD));
                Observe(speech.SayAsync(clipB), cancelB);
                Observe(speech.SayAsync(clipC), cancelC);
                speech.StopSpeaking();
                Check(cancelA.Completed && cancelA.Result == SpeechCompletion.Cancelled
                    && cancelA.CompletionCount == 1
                    && cancelB.Completed && cancelB.Result == SpeechCompletion.Cancelled
                    && cancelB.CompletionCount == 1
                    && cancelC.Completed && cancelC.Result == SpeechCompletion.Cancelled
                    && cancelC.CompletionCount == 1
                    && speech.CurrentSpeechClip == clipD && speech.PendingSpeechCount == 0
                    && source.clip == clipD && speech.IsSpeaking,
                    "StopSpeaking cancels the snapshot and preserves a reentrant new request", failures);
                speech.StopSpeaking();
                Check(!speech.IsSpeaking && speech.PendingSpeechCount == 0 && source.clip == null,
                    "a second explicit stop leaves speech idle", failures);

                var naturalA = new Observation();
                var naturalB = new Observation();
                Observe(speech.SayAsync(clipA), naturalA, () => speech.Say(clipD));
                Observe(speech.SayAsync(clipB), naturalB);
                CompleteCurrent(speech, source);
                Check(naturalA.Completed && naturalA.Result == SpeechCompletion.Finished
                    && speech.CurrentSpeechClip == clipB && speech.PendingSpeechCount == 1 && source.clip == clipB,
                    "natural completion starts an already queued B before A's waiter enqueues D", failures);
                CompleteCurrent(speech, source);
                Check(naturalB.Completed && naturalB.Result == SpeechCompletion.Finished
                    && speech.CurrentSpeechClip == clipD && speech.PendingSpeechCount == 0,
                    "natural-finish reentrancy keeps B ahead of D", failures);
                CompleteCurrent(speech, source);

                var disabledA = new Observation();
                var disabledB = new Observation();
                Observe(speech.SayAsync(clipA), disabledA, () =>
                {
                    try { speech.Say(clipD); }
                    catch (InvalidOperationException) { disabledA.ReentrantSayRejected = true; }
                });
                Observe(speech.SayAsync(clipB), disabledB);
                speech.Dispose();
                speech.Dispose();
                Check(disabledA.Completed && disabledA.Result == SpeechCompletion.PerformerDisabled
                    && disabledA.CompletionCount == 1
                    && disabledB.Completed && disabledB.Result == SpeechCompletion.PerformerDisabled
                    && disabledB.CompletionCount == 1
                    && disabledA.ReentrantSayRejected && !speech.IsSpeaking
                    && speech.PendingSpeechCount == 0 && source.clip == null && !source.isPlaying,
                    "Dispose is idempotent, clears audio and queue, and rejects reentrant speech", failures);
                CheckThrows<InvalidOperationException>(() => speech.Say(clipD),
                    "disposed speech runtime rejects new requests", failures);

                var reenabledSpeech = new PerformerSpeech(source);
                reenabledSpeech.Say(clipC);
                Check(reenabledSpeech.CurrentSpeechClip == clipC && reenabledSpeech.PendingSpeechCount == 0,
                    "a fresh runtime starts new speech without restoring a disposed queue", failures);
                reenabledSpeech.Dispose();

                for (var cycle = 0; cycle < 3; cycle++)
                {
                    var repeatedRuntime = new PerformerSpeech(source);
                    var repeatedWaiter = new Observation();
                    Observe(repeatedRuntime.SayAsync(clipA), repeatedWaiter);
                    repeatedRuntime.Say(clipB);
                    repeatedRuntime.Dispose();
                    repeatedRuntime.Dispose();
                    Check(repeatedWaiter.Completed && repeatedWaiter.Result == SpeechCompletion.PerformerDisabled
                        && repeatedWaiter.CompletionCount == 1 && repeatedRuntime.PendingSpeechCount == 0
                        && !repeatedRuntime.IsSpeaking && source.clip == null && !source.isPlaying,
                        "repeated enable/queue/disable cycle " + (cycle + 1) + " completes once and clears audio", failures);
                }
            }
            catch (Exception exception)
            {
                failures.Add("Speech runtime self-test threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                speech?.Dispose();
                source.Stop();
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(clipA);
                UnityEngine.Object.Destroy(clipB);
                UnityEngine.Object.Destroy(clipC);
                UnityEngine.Object.Destroy(clipD);
            }

            return failures.ToArray();
        }

        private static AudioClip CreateClip(string name)
        {
            return AudioClip.Create(name, 44100, 1, 44100, false);
        }

        private static void CompleteCurrent(PerformerSpeech speech, AudioSource source)
        {
            speech.AdvanceForAcceptance(true);
            source.Stop();
            speech.AdvanceForAcceptance(false);
        }

        private static void Observe(Awaitable<SpeechCompletion> awaitable, Observation observation,
            Action onCompleted = null)
        {
            var awaiter = awaitable.GetAwaiter();
            awaiter.OnCompleted(() =>
            {
                try
                {
                    observation.Result = awaiter.GetResult();
                    observation.Completed = true;
                    observation.CompletionCount++;
                    onCompleted?.Invoke();
                }
                catch (Exception exception)
                {
                    observation.Error = exception;
                }
            });
        }

        private static void CheckThrows<TException>(Action action, string label, List<string> failures)
            where TException : Exception
        {
            try { action(); failures.Add(label + " failed (no exception was thrown)."); }
            catch (TException) { }
            catch (Exception exception) { failures.Add(label + " failed with " + exception.GetType().Name + "."); }
        }

        private static void Check(bool condition, string label, List<string> failures)
        {
            if (!condition) failures.Add(label + " failed.");
        }

        private sealed class Observation
        {
            public bool Completed;
            public bool ReentrantSayRejected;
            public int CompletionCount;
            public SpeechCompletion Result;
            public Exception Error;
        }
    }
}
