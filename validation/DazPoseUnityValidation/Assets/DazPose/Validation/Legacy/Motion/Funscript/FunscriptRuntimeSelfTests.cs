using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DazPose.Motion
{
    /// <summary>Deterministic parser, timeline, segment, looping, and MotionDriver checks.</summary>
    public static class FunscriptRuntimeSelfTests
    {
        private const float Tolerance = 0.0002f;
        private const string BasicJson = "{\"version\":\"1.0\",\"inverted\":false,\"range\":100,"
            + "\"metadata\":{\"duration\":4,\"title\":\"Fixture\",\"description\":\"linear\",\"creator\":\"test\","
            + "\"future\":{\"tags\":[\"ignored\",null,{\"x\":true}]}},\"futureRoot\":[1,\"ignored\"],"
            + "\"actions\":[{\"at\":1000,\"pos\":10,\"extra\":{\"x\":1}},"
            + "{\"at\":2000,\"pos\":50},{\"at\":3000,\"pos\":0}]}";

        public static string[] Run()
        {
            var failures = new List<string>();
            var programs = new List<FunscriptMotionProgram>();
            try
            {
                CheckParser(programs, failures);
                CheckTimeline(programs, failures);
                CheckSegmentsAndSeek(programs, failures);
                CheckDriver(programs, failures);
            }
            catch (Exception exception)
            {
                failures.Add("Funscript self-test threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                foreach (FunscriptMotionProgram program in programs)
                    DestroyObject(program);
            }
            return failures.ToArray();
        }

        private static void CheckParser(List<FunscriptMotionProgram> programs, List<string> failures)
        {
            FunscriptMotionProgram program = Parse(BasicJson, programs);
            Check(program.Version == "1.0" && !program.Inverted && program.Range == 100,
                "version, range, and inverted fields parse", failures);
            Check(program.Title == "Fixture" && program.Description == "linear" && program.Creator == "test",
                "standard metadata strings parse", failures);
            Check(Near(program.MetadataDurationSeconds, 4d) && Near(program.DurationSeconds, 4d),
                "declared metadata duration is retained as the effective duration", failures);
            Check(program.ActionCount == 3 && program.Actions.Count == 3,
                "source action count remains unchanged", failures);
            Check(program.GetAction(0).AtMilliseconds == 1000L && program.GetAction(0).Position == 10
                && program.GetAction(2).AtMilliseconds == 3000L && program.GetAction(2).Position == 0,
                "authored milliseconds and raw positions are retained", failures);
            Check(Near(program.NormalizePosition(program.GetAction(0)), 0.1f)
                && Near(program.NormalizePosition(new FunscriptAction(0, 30)), 0.3f)
                && Near(program.NormalizePosition(new FunscriptAction(0, 100)), 1f),
                "raw action positions normalize by Range", failures);
            Check(Near(Parse("{\"actions\":[{\"at\":0,\"pos\":0},{\"at\":500,\"pos\":100}]}", programs)
                    .DurationSeconds, 0.5d),
                "missing range and duration use the conventional range and final action time", failures);
            Check(Near(Parse("{\"range\":100,\"metadata\":{\"duration\":1},\"actions\":[{\"at\":0,\"pos\":0},{\"at\":3000,\"pos\":1}]}", programs)
                    .DurationSeconds, 3d),
                "effective duration is at least the final action time", failures);

            FunscriptMotionProgram hugeTimestamp = Parse(
                "{\"actions\":[{\"at\":9007199254740993,\"pos\":1}]}", programs);
            Check(hugeTimestamp.GetAction(0).AtMilliseconds == 9007199254740993L,
                "64-bit authored timestamps are parsed without floating-point conversion", failures);

            CheckRejected("{\"actions\":[]}", "empty actions are rejected", failures);
            CheckRejected("{\"actions\":[{\"at\":-1,\"pos\":0}]}", "negative timestamps are rejected", failures);
            CheckRejected("{\"range\":100,\"actions\":[{\"at\":0,\"pos\":101}]}", "out-of-range positions are rejected", failures);
            CheckRejected("{\"actions\":[{\"at\":1000,\"pos\":0},{\"at\":999,\"pos\":1}]}", "unordered timestamps are rejected", failures);
            CheckRejected("{\"actions\":[{\"at\":1.5,\"pos\":0}]}", "fractional action timestamps are rejected", failures);
            CheckRejected("{\"actions\":[{\"at\":0,\"pos\":1.5}]}", "fractional action positions are rejected", failures);
            CheckRejected("{\"metadata\":{\"duration\":1e999},\"actions\":[{\"at\":0,\"pos\":0}]}",
                "nonfinite metadata durations are rejected", failures);
            CheckRejected("{\"inverted\":\"false\",\"actions\":[{\"at\":0,\"pos\":0}]}",
                "nonboolean inversion values are rejected", failures);
            CheckRejected("{\"actions\":[{\"at\":0,\"pos\":0}] } trailing", "trailing malformed JSON is rejected", failures);
        }

        private static void CheckTimeline(List<FunscriptMotionProgram> programs, List<string> failures)
        {
            FunscriptMotionProgram program = Parse(BasicJson, programs);
            var playback = new FunscriptPlayback(program);
            Check(Near(playback.CurrentTimeSeconds, 0d) && Near(playback.Position01, 0.1f)
                && !playback.HasCurrentSegment,
                "time zero holds the first authored position before its timestamp", failures);

            playback.Seek(1d);
            Check(Near(playback.Position01, 0.1f) && Near(playback.Velocity, 0.4f)
                && playback.Direction == MotionDirection.Increasing
                && Near(playback.CurrentSample.Phase01, 0.25f),
                "an exact action timestamp returns that position and timeline phase", failures);
            playback.Seek(1.5d);
            Check(Near(playback.Position01, 0.3f) && Near(playback.Velocity, 0.4f)
                && playback.Direction == MotionDirection.Increasing,
                "mid-segment playback is linear with analytic constant velocity", failures);
            playback.Seek(2d);
            Check(Near(playback.Position01, 0.5f) && Near(playback.Velocity, -0.5f)
                && playback.Direction == MotionDirection.Decreasing,
                "action boundaries resolve exactly to the authored target", failures);
            playback.Seek(2.5d);
            Check(Near(playback.Position01, 0.25f) && playback.Direction == MotionDirection.Decreasing,
                "reverse authored motion interpolates backward", failures);
            playback.Seek(3.5d);
            Check(Near(playback.Position01, 0f) && Near(playback.Velocity, 0f)
                && playback.Direction == MotionDirection.Stationary,
                "post-last timeline holds the final position with zero velocity", failures);

            FunscriptMotionProgram inverted = Parse(
                "{\"inverted\":true,\"range\":100,\"metadata\":{\"duration\":2},"
                + "\"actions\":[{\"at\":0,\"pos\":20},{\"at\":1000,\"pos\":70}]}", programs);
            var invertedPlayback = new FunscriptPlayback(inverted);
            Check(inverted.GetAction(0).Position == 20
                && Near(inverted.NormalizePosition(inverted.GetAction(0)), 0.2f)
                && Near(inverted.InterpretPosition(inverted.GetAction(0)), 0.8f),
                "raw action positions remain unchanged while inversion affects interpretation", failures);
            invertedPlayback.Seek(0.5d);
            Check(Near(invertedPlayback.Position01, 0.55f)
                && Near(invertedPlayback.Velocity, -0.5f)
                && invertedPlayback.Direction == MotionDirection.Decreasing,
                "inverted interpolation and velocity use interpreted positions", failures);

            FunscriptMotionProgram duplicates = Parse(
                "{\"range\":100,\"metadata\":{\"duration\":4},\"actions\":["
                + "{\"at\":1000,\"pos\":20},{\"at\":1000,\"pos\":70},{\"at\":2000,\"pos\":90},"
                + "{\"at\":2000,\"pos\":30},{\"at\":3000,\"pos\":50}]}", programs);
            var duplicatePlayback = new FunscriptPlayback(duplicates);
            Check(duplicates.ActionCount == 5 && duplicates.GetSegmentStartingAtAction(0).DurationSeconds == 0d,
                "duplicate timestamps and zero-duration authored segments remain preserved", failures);
            duplicatePlayback.Seek(0.999d);
            Check(Near(duplicatePlayback.Position01, 0.2f), "pre-duplicate time holds the first source action", failures);
            duplicatePlayback.Seek(1d);
            Check(Near(duplicatePlayback.Position01, 0.7f)
                && duplicatePlayback.CurrentSegment.FromActionIndex == 1
                && duplicatePlayback.CurrentSegment.ToActionIndex == 2,
                "last source action at a duplicate timestamp wins without division by zero", failures);
            duplicatePlayback.Seek(2d);
            Check(Near(duplicatePlayback.Position01, 0.3f)
                && duplicatePlayback.CurrentSegment.FromActionIndex == 3,
                "last of multiple duplicate targets is active at the exact timestamp", failures);

            FunscriptMotionProgram startsAtZero = Parse(
                "{\"metadata\":{\"duration\":2},\"actions\":["
                + "{\"at\":0,\"pos\":10},{\"at\":0,\"pos\":40},{\"at\":1000,\"pos\":80}]}", programs);
            var zeroStartPlayback = new FunscriptPlayback(startsAtZero);
            var zeroStartEvents = new List<int>();
            zeroStartPlayback.SegmentChanged += segment => zeroStartEvents.Add(segment.FromActionIndex);
            Check(Near(zeroStartPlayback.Position01, 0.4f) && zeroStartPlayback.HasCurrentSegment
                && zeroStartPlayback.CurrentSegment.FromActionIndex == 1,
                "reset at a duplicate zero timestamp resolves to its last authored action", failures);
            zeroStartPlayback.Advance(0.25d);
            Check(zeroStartEvents.Count == 1 && zeroStartEvents[0] == 1,
                "playback announces its initial segment on first advancement", failures);
        }

        private static void CheckSegmentsAndSeek(List<FunscriptMotionProgram> programs, List<string> failures)
        {
            FunscriptMotionProgram program = Parse(BasicJson, programs);
            Check(program.TryGetSegmentAt(1d, out FunscriptSegment first)
                && first.FromActionIndex == 0 && first.ToActionIndex == 1
                && first.From.AtMilliseconds == 1000L && first.From.Position == 10
                && first.To.AtMilliseconds == 2000L && first.To.Position == 50
                && Near(first.StartSeconds, 1d) && Near(first.EndSeconds, 2d)
                && Near(first.DurationSeconds, 1d) && Near(first.ToPosition01, 0.5f),
                "segment exposes raw endpoint actions and interpreted target timing", failures);
            Check(!program.TryGetSegmentAt(double.NaN, out _)
                && !program.TryGetSegmentAt(3d, out _),
                "segment lookup safely returns false outside an active segment", failures);

            var eventPlayback = new FunscriptPlayback(program);
            int segmentEvents = 0;
            FunscriptSegment lastNotified = default;
            eventPlayback.SegmentChanged += segment => { segmentEvents++; lastNotified = segment; };
            eventPlayback.Seek(1.25d);
            eventPlayback.Seek(1.75d);
            Check(segmentEvents == 1 && lastNotified.FromActionIndex == 0,
                "SegmentChanged fires once while remaining in an authored segment", failures);
            eventPlayback.Seek(2.5d);
            Check(segmentEvents == 2 && lastNotified.FromActionIndex == 1,
                "SegmentChanged identifies the next authored segment", failures);
            eventPlayback.Seek(3.5d);
            Check(segmentEvents == 2 && !eventPlayback.HasCurrentSegment,
                "post-last hold is not reported as a command segment", failures);
            eventPlayback.Seek(2.5d);
            Check(segmentEvents == 3 && lastNotified.FromActionIndex == 1,
                "seek emits only the resulting segment, not each skipped segment", failures);

            var advancingPlayback = new FunscriptPlayback(program);
            var advancedSegments = new List<int>();
            advancingPlayback.SegmentChanged += segment => advancedSegments.Add(segment.FromActionIndex);
            advancingPlayback.Advance(2.5d);
            Check(advancedSegments.Count == 2 && advancedSegments[0] == 0 && advancedSegments[1] == 1,
                "forward advancement reports every authored segment boundary crossed", failures);

            var builder = new StringBuilder("{\"range\":100,\"metadata\":{\"duration\":5000},\"actions\":[");
            const int actionCount = 4096;
            for (int i = 0; i < actionCount; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append("{\"at\":").Append(i * 1000)
                    .Append(",\"pos\":").Append(i % 101).Append('}');
            }
            builder.Append("]}");
            FunscriptMotionProgram longProgram = Parse(builder.ToString(), programs);
            var longPlayback = new FunscriptPlayback(longProgram);
            int longSeekEvents = 0;
            longPlayback.SegmentChanged += _ => longSeekEvents++;
            longPlayback.Seek(3500.5d);
            Check(longProgram.ActionCount == actionCount && longPlayback.HasCurrentSegment
                && longPlayback.CurrentSegment.FromActionIndex == 3500
                && longPlayback.CurrentSegment.ToActionIndex == 3501
                && longSeekEvents == 1,
                "binary seek finds a distant segment directly and emits one resulting notification", failures);

            FunscriptMotionProgram loopProgram = Parse(
                "{\"range\":100,\"metadata\":{\"duration\":2},\"actions\":["
                + "{\"at\":0,\"pos\":0},{\"at\":1000,\"pos\":100}]}", programs);
            var loopPlayback = new FunscriptPlayback(loopProgram, loop: true);
            loopPlayback.Advance(0.25d);
            loopPlayback.Advance(2d);
            Check(!loopPlayback.IsComplete && Near(loopPlayback.CurrentTimeSeconds, 0.25d)
                && Near(loopPlayback.Position01, 0.25f),
                "loop playback wraps and rebuilds its cursor without completing", failures);

            var loopEvents = new List<int>();
            loopPlayback.SegmentChanged += segment => loopEvents.Add(segment.FromActionIndex);
            loopPlayback.Advance(4d);
            Check(loopEvents.Count == 2,
                "large looping advancement reports the segment re-entered on each wrap", failures);

            FunscriptMotionProgram zeroDuration = Parse(
                "{\"actions\":[{\"at\":0,\"pos\":60}]}", programs);
            var endedImmediately = new FunscriptPlayback(zeroDuration);
            endedImmediately.Advance(0.1d);
            Check(endedImmediately.IsComplete && Near(endedImmediately.Position01, 0.6f)
                && Near(endedImmediately.Velocity, 0f),
                "zero-duration one-action playback safely holds its authored position", failures);
        }

        private static void CheckDriver(List<FunscriptMotionProgram> programs, List<string> failures)
        {
            FunscriptMotionProgram program = Parse(BasicJson, programs);
            var host = new GameObject("FunscriptMotionDriverSelfTest");
            MotionDriver driver = null;
            try
            {
                driver = host.AddComponent<MotionDriver>();
                driver.FunscriptProgram = program;
                driver.SourceMode = MotionSourceMode.Funscript;
                Check(driver.SourceMode == MotionSourceMode.Funscript && !driver.IsRunning
                    && Near(driver.CurrentSample.TimeSeconds, 0d) && Near(driver.CurrentSample.Position01, 0.1f),
                    "source selection begins Funscript at timeline zero and its first authored target", failures);

                var samples = new List<MotionSample>();
                var events = new List<string>();
                bool finalSampleObservedWhileRunning = false;
                driver.Sampled += samples.Add;
                driver.Sampled += sample =>
                {
                    if (Near(sample.TimeSeconds, 4d)) finalSampleObservedWhileRunning = driver.IsRunning;
                };
                driver.RunningChanged += running => events.Add(running ? "running" : "stopped");
                driver.StartMotion();
                driver.Advance(1.5f);
                Check(driver.IsRunning && Near(driver.CurrentSample.TimeSeconds, 1.5d)
                    && Near(driver.CurrentSample.Position01, 0.3f)
                    && Near(driver.CurrentSample.Velocity, 0.4f)
                    && Near(driver.CurrentSample.Phase01, 0.375f),
                    "Funscript source publishes timeline time, normalized phase, and analytic motion", failures);

                driver.StopMotion();
                MotionSample held = driver.CurrentSample;
                driver.Advance(0.75f);
                Check(SameSample(held, driver.CurrentSample), "Stop pauses Funscript time and sample", failures);
                driver.StartMotion();
                driver.Advance(1f);
                Check(Near(driver.CurrentSample.TimeSeconds, 2.5d)
                    && Near(driver.CurrentSample.Position01, 0.25f)
                    && driver.CurrentSample.Direction == MotionDirection.Decreasing,
                    "Start resumes the held Funscript timeline and reverse scrubbing", failures);

                long beforeSeekSequence = driver.CurrentSample.Sequence;
                driver.Seek(2.75d);
                Check(driver.IsRunning && driver.CurrentSample.Sequence == beforeSeekSequence + 1
                    && Near(driver.CurrentSample.TimeSeconds, 2.75d)
                    && Near(driver.CurrentSample.Position01, 0.125f),
                    "Seek publishes one authoritative sample and continues while running", failures);
                driver.RestartMotion();
                Check(driver.IsRunning && Near(driver.CurrentSample.TimeSeconds, 0d)
                    && Near(driver.CurrentSample.Position01, 0.1f),
                    "Restart resets Funscript to its first interpreted position and runs", failures);
                driver.StopMotion();
                driver.ResetMotion();
                Check(!driver.IsRunning && Near(driver.CurrentSample.TimeSeconds, 0d)
                    && Near(driver.CurrentSample.Position01, 0.1f),
                    "Reset stops at the first authored Funscript position", failures);
                CheckSequencesIncrease(samples, failures);

                driver.Loop = true;
                events.Clear();
                driver.StartMotion();
                driver.Advance(4.25f);
                Check(driver.IsRunning && Near(driver.CurrentSample.TimeSeconds, 0.25d)
                    && Near(driver.CurrentSample.Position01, 0.1f)
                    && events.Count == 1 && events[0] == "running",
                    "looping Funscript wraps its timeline without a stop transition", failures);
                driver.StopMotion();

                driver.Loop = false;
                driver.ResetMotion();
                events.Clear();
                samples.Clear();
                driver.StartMotion();
                driver.Advance(50f);
                Check(!driver.IsRunning && Near(driver.CurrentSample.TimeSeconds, 4d)
                    && Near(driver.CurrentSample.Phase01, 1f) && Near(driver.CurrentSample.Position01, 0f)
                    && driver.CurrentSample.Direction == MotionDirection.Stationary,
                    "non-looping end publishes and holds the final authored position through metadata duration", failures);
                Check(samples.Count == 1 && events.Count == 2
                    && events[0] == "running" && events[1] == "stopped"
                    && samples[0].Sequence == driver.CurrentSample.Sequence
                    && finalSampleObservedWhileRunning,
                    "end publishes its final sample before RunningChanged(false)", failures);

                driver.SourceMode = MotionSourceMode.Sine;
                Check(Near(driver.CurrentSample.Position01, 0f)
                    && driver.SourceMode == MotionSourceMode.Sine,
                    "switching back to Sine uses the same driver and consumer contract", failures);
                driver.StartMotion();
                driver.Advance(0.25f);
                Check(Near(driver.CurrentSample.Position01, 0.5f),
                    "the original 1 Hz sine source still advances as before", failures);
                driver.SourceMode = MotionSourceMode.Funscript;
                Check(driver.IsRunning && Near(driver.CurrentSample.TimeSeconds, 0d)
                    && Near(driver.CurrentSample.Position01, 0.1f),
                    "switching source while running publishes the new source without rebuilding consumers", failures);
                driver.StopMotion();
            }
            catch (Exception exception)
            {
                failures.Add("MotionDriver Funscript test threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                if (driver != null) driver.StopMotion();
                DestroyObject(host);
            }
        }

        private static FunscriptMotionProgram Parse(string json, List<FunscriptMotionProgram> programs)
        {
            FunscriptMotionProgram program = FunscriptJsonParser.Parse(json);
            programs.Add(program);
            return program;
        }

        private static void CheckRejected(string json, string label, List<string> failures)
        {
            try
            {
                FunscriptMotionProgram program = FunscriptJsonParser.Parse(json);
                DestroyObject(program);
                Check(false, label, failures);
            }
            catch (FormatException) { }
        }

        private static void CheckSequencesIncrease(List<MotionSample> samples, List<string> failures)
        {
            for (int i = 1; i < samples.Count; i++)
                if (samples[i].Sequence <= samples[i - 1].Sequence)
                {
                    Check(false, "sample sequence remains monotonically increasing", failures);
                    return;
                }
            Check(true, "sample sequence remains monotonically increasing", failures);
        }

        private static bool SameSample(MotionSample a, MotionSample b) =>
            a.Sequence == b.Sequence && Near(a.TimeSeconds, b.TimeSeconds)
            && Near(a.Phase01, b.Phase01) && Near(a.Position01, b.Position01)
            && Near(a.Velocity, b.Velocity) && a.Direction == b.Direction;

        private static bool Near(float a, float b) => Mathf.Abs(a - b) <= Tolerance;
        private static bool Near(double a, double b) => Math.Abs(a - b) <= Tolerance;

        private static void Check(bool condition, string message, List<string> failures)
        {
            if (!condition) failures.Add("Funscript: " + message + ".");
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
