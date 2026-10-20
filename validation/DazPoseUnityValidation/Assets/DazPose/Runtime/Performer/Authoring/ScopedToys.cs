using System;
using DazPose.Motion;
using DazPose.Toys;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Silent optional output. A stopped run can never regain output authority.</summary>
    public sealed class ScopedToys
    {
        private readonly PerformanceRun run;
        private readonly ToyControlService service;
        private readonly long lease;
        internal ScopedToys(PerformanceRun owner, ToyControlService boundService)
        {
            run = owner;
            service = boundService;
            lease = service != null ? service.AcquireOutputLease() : 0;
        }

        [Obsolete("Device capabilities belong to application settings, not scene branches.")]
        public bool CanOutput(ToyOutputCapability capability, int? channel = null) =>
            service != null && service.CanOutput(capability, channel);

        private bool Active => !run.IsStopped && service != null && service.IsOutputLeaseCurrent(lease);
        private void Submit(Action command, ToyOutputCapability? capability = null, int? channel = null)
        {
            if (!Active) return;
            try
            {
                command();
                if (capability.HasValue)
                    RegisterCleanup(service.CaptureOutputCancellation(capability.Value, channel),"toy:"+capability.Value+":"+channel);
            }
            catch { } // Intiface delivery and toy-only argument errors do not affect choreography.
        }

        private void RegisterCleanup(Action cleanup,string resource) => run.RegisterPersistentCleanup(() => { try { cleanup?.Invoke(); } catch { } },resource:resource);

        public void Vibrate(float level, int? channel = null) =>
            Submit(() => { if (channel.HasValue) service.Vibrate(level, channel.Value); else service.Vibrate(level); },
                ToyOutputCapability.Vibrate, channel);
        public void Oscillate(float level, int? channel = null) =>
            Submit(() => { if (channel.HasValue) service.Oscillate(level, channel.Value); else service.Oscillate(level); },
                ToyOutputCapability.Oscillate, channel);
        public void RampVibration(float target, float seconds, int? channel = null) =>
            Submit(() => { if (channel.HasValue) service.RampVibration(target, seconds, channel.Value); else service.RampVibration(target, seconds); },
                ToyOutputCapability.Vibrate, channel);
        public void RampOscillation(float target, float seconds, int? channel = null) =>
            Submit(() => { if (channel.HasValue) service.RampOscillation(target, seconds, channel.Value); else service.RampOscillation(target, seconds); },
                ToyOutputCapability.Oscillate, channel);
        public void MoveTo(float position, float seconds) =>
            Submit(() => service.MoveTo(position, seconds), ToyOutputCapability.Position);

        public void Follow() => Follow(run.PlaybackDriver);
        public void Follow(MotionDriver driver)
        {
            if (!Active || driver == null) return;
            Submit(() =>
            {
                service.Follow(driver);
                RegisterCleanup(service.CapturePlaybackCancellation(),"toy-follow");
            });
        }
        public void StopFollowing() => Submit(service.StopFollowing);
        public void Stop() => Submit(service.StopAllOutput);
        internal void StopSessionOutput()
        {
            if (service != null && service.IsOutputLeaseCurrent(lease))
                try { service.StopAllOutput(); } catch { }
        }

        [Obsolete("Start the run's shared playback explicitly, then Follow.")]
        public void Play(FunscriptMotionProgram program, bool loop = false)
        {
            if (!Active) return;
            try { run.StartPlayback(program, loop); Follow(); } catch { }
        }

        private static Awaitable<PerformanceToyResult> Accepted(string verb)
        {
            var completion = new AwaitableCompletionSource<PerformanceToyResult>();
            completion.SetResult(new PerformanceToyResult(verb, 0, Array.Empty<string>()));
            return completion.Awaitable;
        }
        [Obsolete("Use fire-and-forget Vibrate; sequence timing belongs to Delay.")]
        public Awaitable<PerformanceToyResult> VibrateAsync(float level) { Vibrate(level); return Accepted("Vibrate"); }
        [Obsolete("Use Vibrate.")]
        public Awaitable<PerformanceToyResult> VibrateAsync(float level, int channel) { Vibrate(level, channel); return Accepted("Vibrate"); }
        [Obsolete("Use Oscillate.")]
        public Awaitable<PerformanceToyResult> OscillateAsync(float level) { Oscillate(level); return Accepted("Oscillate"); }
        [Obsolete("Use Oscillate.")]
        public Awaitable<PerformanceToyResult> OscillateAsync(float level, int channel) { Oscillate(level, channel); return Accepted("Oscillate"); }
        [Obsolete("Use RampVibration and Delay.")]
        public Awaitable<PerformanceToyResult> RampVibrationAsync(float target, float seconds) { RampVibration(target, seconds); return Accepted("RampVibration"); }
        [Obsolete("Use RampVibration and Delay.")]
        public Awaitable<PerformanceToyResult> RampVibrationAsync(float target, float seconds, int channel) { RampVibration(target, seconds, channel); return Accepted("RampVibration"); }
        [Obsolete("Use RampOscillation and Delay.")]
        public Awaitable<PerformanceToyResult> RampOscillationAsync(float target, float seconds) { RampOscillation(target, seconds); return Accepted("RampOscillation"); }
        [Obsolete("Use RampOscillation and Delay.")]
        public Awaitable<PerformanceToyResult> RampOscillationAsync(float target, float seconds, int channel) { RampOscillation(target, seconds, channel); return Accepted("RampOscillation"); }
        [Obsolete("Use MoveTo and Delay.")]
        public Awaitable<PerformanceToyResult> MoveToAsync(float position, float seconds) { MoveTo(position, seconds); return Accepted("MoveTo"); }
    }
}
