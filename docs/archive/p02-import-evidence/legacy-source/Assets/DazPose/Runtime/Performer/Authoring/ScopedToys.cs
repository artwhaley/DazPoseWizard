using System;
using DazPose.Motion;
using DazPose.Toys;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Optional toy commands bound to one performance run and the existing scene service.</summary>
    public sealed class ScopedToys
    {
        private readonly PerformanceRun run;
        private readonly ToyControlService service;

        internal ScopedToys(PerformanceRun owner, ToyControlService boundService)
        {
            run = owner;
            service = boundService;
        }

        public bool CanOutput(ToyOutputCapability capability, int? channel = null) =>
            service != null && service.CanOutput(capability, channel);

        public void Play(FunscriptMotionProgram program, bool loop = false)
        {
            run.ThrowIfActive();
            service.Play(program, loop);
            run.RegisterPersistentCleanup(service.CapturePlaybackCancellation());
        }

        public void Follow(MotionDriver driver)
        {
            run.ThrowIfActive();
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            service.Follow(driver);
            run.RegisterPersistentCleanup(service.CapturePlaybackCancellation());
        }

        public void Follow()
        {
            run.ThrowIfActive();
            if (run.Playback == null)
                throw new PerformanceCommandException("scene", "Toys.Follow", "MissingBinding",
                    "The performance has no bound playback driver to follow.");
            Follow(run.Playback);
        }

        public void StopFollowing()
        {
            run.ThrowIfActive();
            service.StopFollowing();
        }

        public Awaitable<PerformanceToyResult> VibrateAsync(float level) =>
            Execute("Vibrate", ToyOutputCapability.Vibrate, null,
                () => service.VibrateTrackedAsync(level));

        public Awaitable<PerformanceToyResult> VibrateAsync(float level, int channel) =>
            Execute("Vibrate", ToyOutputCapability.Vibrate, channel,
                () => service.VibrateTrackedAsync(level, channel));

        public Awaitable<PerformanceToyResult> OscillateAsync(float level) =>
            Execute("Oscillate", ToyOutputCapability.Oscillate, null,
                () => service.OscillateTrackedAsync(level));

        public Awaitable<PerformanceToyResult> OscillateAsync(float level, int channel) =>
            Execute("Oscillate", ToyOutputCapability.Oscillate, channel,
                () => service.OscillateTrackedAsync(level, channel));

        public Awaitable<PerformanceToyResult> RampVibrationAsync(float target, float seconds) =>
            Execute("RampVibration", ToyOutputCapability.Vibrate, null,
                () => service.RampVibrationTrackedAsync(target, seconds));

        public Awaitable<PerformanceToyResult> RampVibrationAsync(float target, float seconds, int channel) =>
            Execute("RampVibration", ToyOutputCapability.Vibrate, channel,
                () => service.RampVibrationTrackedAsync(target, seconds, channel));

        public Awaitable<PerformanceToyResult> RampOscillationAsync(float target, float seconds) =>
            Execute("RampOscillation", ToyOutputCapability.Oscillate, null,
                () => service.RampOscillationTrackedAsync(target, seconds));

        public Awaitable<PerformanceToyResult> RampOscillationAsync(float target, float seconds, int channel) =>
            Execute("RampOscillation", ToyOutputCapability.Oscillate, channel,
                () => service.RampOscillationTrackedAsync(target, seconds, channel));

        public Awaitable<PerformanceToyResult> MoveToAsync(float position, float seconds) =>
            Execute("MoveTo", ToyOutputCapability.Position, null,
                () => service.MoveToTrackedAsync(position, seconds));

        private async Awaitable<PerformanceToyResult> Execute(string verb,
            ToyOutputCapability capability, int? channel,
            Func<Awaitable<ToyCommandReport>> start)
        {
            ToyCommandReport report = await run.Execute("Toys." + verb, start,
                result => result != null && result.Succeeded,
                result => result == null ? "null" : string.Join("; ", result.Errors),
                result => new PerformanceCommandException("toys", verb,
                    result == null ? "NoResult" : string.Join("; ", result.Errors),
                    "The requested toy output was not acknowledged by any bound route."),
                captureCancellation: () => service.CaptureOutputCancellation(capability, channel));
            return new PerformanceToyResult(report.Operation, report.SuccessfulBindings, report.Errors);
        }
    }
}
