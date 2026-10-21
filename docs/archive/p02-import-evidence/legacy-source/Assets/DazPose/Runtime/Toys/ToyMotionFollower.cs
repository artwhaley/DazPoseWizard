using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DazPose.Motion;
using UnityEngine;

namespace DazPose.Toys
{
    /// <summary>One bounded consumer of the existing MotionDriver timeline for one device feature.</summary>
    internal sealed class ToyMotionFollower : IDisposable
    {
        private const float PositionDeadband = 0.005f;
        private readonly ToyControlService _service;
        private readonly MotionDriver _driver;
        private readonly ToyMotionBinding _binding;
        private ToyOutputBinding _activeOutput;
        private readonly int _sendGapMilliseconds;
        private readonly object _gate = new object();
        private PendingOutput? _pending;
        private bool _workerActive;
        private bool _disposed;
        private int _generation;
        private double _lastSentAt;
        private float _lastMapped = float.NaN;
        private long _lastSegmentRevision = -1;
        private double _lastSentDuration;
        private float _sourcePosition;
        private float _mappedPosition;
        private double _lastSendRealtime;
        private string _lastError = string.Empty;
        private ToyOutputBinding _positionHoldBinding;
        private long _outputRevision;
        private long _sourceRevision;

        private readonly struct PendingOutput
        {
            public readonly ToyOutputBinding Binding;
            public readonly float Value;
            public readonly uint DurationMilliseconds;
            public readonly double SegmentEndTimeSeconds;
            public readonly double SegmentObservedAtSeconds;
            public readonly bool IsTimedSegment;
            public readonly int Generation;
            public readonly int ServiceGeneration;
            public readonly long OutputRevision;

            public PendingOutput(ToyOutputBinding binding, float value, uint durationMilliseconds,
                double segmentEndTimeSeconds, double segmentObservedAtSeconds, bool isTimedSegment,
                int generation, int serviceGeneration, long outputRevision)
            {
                Binding = binding;
                Value = value;
                DurationMilliseconds = durationMilliseconds;
                SegmentEndTimeSeconds = segmentEndTimeSeconds;
                SegmentObservedAtSeconds = segmentObservedAtSeconds;
                IsTimedSegment = isTimedSegment;
                Generation = generation;
                ServiceGeneration = serviceGeneration;
                OutputRevision = outputRevision;
            }
        }

        public ToyMotionFollower(ToyControlService service, MotionDriver driver,
            ToyMotionBinding binding, ToyOutputBinding activeOutput, uint messageTimingGapMilliseconds)
        {
            _service = service;
            _driver = driver;
            _binding = binding;
            _activeOutput = activeOutput;
            _sourceRevision = _driver.SourceRevision;
            _sendGapMilliseconds = (int)Math.Max(messageTimingGapMilliseconds, 50u);
            _positionHoldBinding = new ToyOutputBinding(activeOutput.DeviceIndex,
                activeOutput.FeatureIndex, ToyOutputCapability.Position);
            _outputRevision = _service.BeginFollowerOwnership(activeOutput,
                _binding.Map(_driver.CurrentSample.Position01));
            _driver.Sampled += OnSampled;
            _driver.RunningChanged += OnRunningChanged;
            _driver.Sought += OnSought;
            _driver.TargetSegmentChanged += OnTargetSegmentChanged;
            if (_driver.IsRunning) SynchronizeCurrent();
            SetDiagnostic();
        }

        public void Dispose() => Dispose(stopHardware: true);

        public void Dispose(bool stopHardware)
        {
            long revision;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _generation++;
                _pending = null;
                revision = _outputRevision;
            }
            _driver.Sampled -= OnSampled;
            _driver.RunningChanged -= OnRunningChanged;
            _driver.Sought -= OnSought;
            _driver.TargetSegmentChanged -= OnTargetSegmentChanged;
            _service.SetFollowerDiagnostic(_activeOutput, "inactive");
            if (stopHardware) _ = StopAndReleaseAsync(_service.OutputCommandGeneration, revision);
            else _service.ReleaseFollowerOwnership(_activeOutput, revision);
            SetDiagnostic();
        }

        public void Resynchronize()
        {
            if (_disposed) return;
            UpdateResolvedStrategy();
            RenewOutputOwnership();
            lock (_gate) { _generation++; _pending = null; }
            _lastSegmentRevision = -1;
            SynchronizeCurrent();
        }

        private void OnSampled(MotionSample sample)
        {
            if (_disposed || !_driver.IsRunning) return;
            RefreshSourceRevision();
            UpdateResolvedStrategy();
            _sourcePosition = sample.Position01;
            _mappedPosition = _binding.Map(sample.Position01);
            if (_activeOutput.Capability == ToyOutputCapability.Position)
                Enqueue(_activeOutput, _mappedPosition, 0, force: false);
            else if (_driver.TryGetCurrentTargetSegment(out MotionTargetSegment segment)
                && segment.Revision != _lastSegmentRevision)
                QueueSegment(segment);
            SetDiagnostic();
        }

        private void OnTargetSegmentChanged(MotionTargetSegment segment)
        {
            if (_disposed || !_driver.IsRunning) return;
            RefreshSourceRevision();
            UpdateResolvedStrategy();
            if (_activeOutput.Capability == ToyOutputCapability.HwPositionWithDuration)
                QueueSegment(segment);
        }

        private void OnRunningChanged(bool running)
        {
            if (_disposed) return;
            if (running)
            {
                RefreshSourceRevision();
                UpdateResolvedStrategy();
                SynchronizeCurrent();
            }
            else
            {
                RefreshSourceRevision();
                RenewOutputOwnership();
                lock (_gate) { _generation++; _pending = null; }
                _ = StopHardwareAsync(_service.OutputCommandGeneration, _outputRevision);
            }
        }

        private void OnSought()
        {
            if (_disposed) return;
            RefreshSourceRevision();
            UpdateResolvedStrategy();
            RenewOutputOwnership();
            lock (_gate) { _generation++; _pending = null; }
            _lastSegmentRevision = -1;
            _sourcePosition = _driver.CurrentSample.Position01;
            _mappedPosition = _binding.Map(_sourcePosition);
            SynchronizeCurrent();
        }

        private void SynchronizeCurrent()
        {
            if (_disposed || !_driver.IsRunning) return;
            MotionSample sample = _driver.CurrentSample;
            _sourcePosition = sample.Position01;
            _mappedPosition = _binding.Map(sample.Position01);
            if (_activeOutput.Capability == ToyOutputCapability.Position)
                Enqueue(_activeOutput, _mappedPosition, 0, force: true);
            else if (_driver.TryGetCurrentTargetSegment(out MotionTargetSegment segment))
                QueueSegment(segment);
            // Duration-only features cannot represent sampled sources. In particular, do not
            // fabricate short timed moves to approximate a sine wave.
        }

        private void QueueSegment(MotionTargetSegment segment)
        {
            if (segment.Revision == _lastSegmentRevision) return;
            _lastSegmentRevision = segment.Revision;
            double observedTime = Math.Max(segment.ObservedAtTimeSeconds, _driver.CurrentSample.TimeSeconds);
            double remaining = segment.EndTimeSeconds - observedTime;
            if (remaining <= 0d) return;
            uint duration = (uint)Math.Max(1d, Math.Min(uint.MaxValue, Math.Round(remaining * 1000d)));
            _sourcePosition = _driver.CurrentSample.Position01;
            _mappedPosition = _binding.Map(segment.TargetPosition01);
            Enqueue(_activeOutput, _mappedPosition, duration, force: true,
                segmentEndTime: segment.EndTimeSeconds, segmentObservedAt: segment.ObservedAtTimeSeconds);
        }

        private void UpdateResolvedStrategy()
        {
            if (_binding.Strategy != ToyMotionStrategy.Auto) return;
            ToyOutputBinding desired = _service.ResolveMotionOutput(_binding);
            if (desired == _activeOutput) return;
            _service.SetFollowerDiagnostic(_activeOutput, "inactive");
            _service.ReleaseFollowerOwnership(_activeOutput, _outputRevision);
            _activeOutput = desired;
            _positionHoldBinding = new ToyOutputBinding(desired.DeviceIndex,
                desired.FeatureIndex, ToyOutputCapability.Position);
            _outputRevision = _service.BeginFollowerOwnership(desired,
                _binding.Map(_driver.CurrentSample.Position01));
            lock (_gate) { _generation++; _pending = null; }
            _lastSegmentRevision = -1;
            SynchronizeCurrent();
        }

        private void Enqueue(ToyOutputBinding binding, float value, uint duration, bool force,
            double segmentEndTime = -1d, double segmentObservedAt = -1d)
        {
            lock (_gate)
            {
                if (_disposed) return;
                if (!force && !float.IsNaN(_lastMapped) && Mathf.Abs(value - _lastMapped) < PositionDeadband
                    && (!_pending.HasValue || Mathf.Abs(value - _pending.Value.Value) < PositionDeadband)) return;
                bool timedSegment = binding.Capability == ToyOutputCapability.HwPositionWithDuration
                    && segmentEndTime >= 0d;
                _pending = new PendingOutput(binding, value, duration, segmentEndTime, segmentObservedAt,
                    timedSegment, _generation, _service.OutputCommandGeneration, _outputRevision);
                if (_workerActive) return;
                _workerActive = true;
                _ = DrainLatestAsync();
            }
        }

        private async Task DrainLatestAsync()
        {
            while (true)
            {
                PendingOutput next;
                lock (_gate)
                {
                    if (_disposed || !_pending.HasValue)
                    {
                        _workerActive = false;
                        return;
                    }
                    next = _pending.Value;
                    _pending = null;
                }

                lock (_gate)
                {
                    if (_disposed || next.Generation != _generation) continue;
                }

                uint duration = next.DurationMilliseconds;
                if (next.IsTimedSegment)
                {
                    double now = Math.Max(next.SegmentObservedAtSeconds, _driver.CurrentSample.TimeSeconds);
                    double remaining = next.SegmentEndTimeSeconds - now;
                    if (remaining <= 0d) continue;
                    duration = (uint)Math.Max(1d, Math.Min(uint.MaxValue, Math.Round(remaining * 1000d)));
                }

                try
                {
                    bool sent = await _service.DispatchFollowerOutputAsync(next.Binding, next.OutputRevision,
                        next.Value, duration, next.ServiceGeneration).ConfigureAwait(false);
                    if (!sent) continue;
                    _lastMapped = next.Value;
                    _lastSentDuration = duration / 1000d;
                    _lastSendRealtime = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                    _lastSentAt = Stopwatch.GetTimestamp();
                    _lastError = string.Empty;
                }
                catch (Exception exception) { _lastError = exception.Message; }
                SetDiagnostic();
            }
        }

        private long RenewOutputOwnership()
        {
            long previous = _outputRevision;
            _service.ReleaseFollowerOwnership(_activeOutput, previous);
            long next = _service.BeginFollowerOwnership(_activeOutput,
                _binding.Map(_driver.CurrentSample.Position01));
            _outputRevision = next;
            return next;
        }

        private void RefreshSourceRevision()
        {
            long revision = _driver.SourceRevision;
            if (revision == _sourceRevision) return;
            _sourceRevision = revision;
            lock (_gate) { _generation++; _pending = null; }
            _lastSegmentRevision = -1;
            RenewOutputOwnership();
        }

        private async Task StopHardwareAsync(int commandGeneration, long revision)
        {
            try
            {
                if (_activeOutput.Capability == ToyOutputCapability.Position)
                {
                    bool held = await _service.DispatchFollowerOutputAsync(_positionHoldBinding,
                        revision, _binding.Map(_driver.CurrentSample.Position01), 0, commandGeneration).ConfigureAwait(false);
                    if (!held) return;
                    _lastError = string.Empty;
                    SetDiagnostic();
                    return;
                }

                _lastError = "The accepted timed move may finish; no further motion targets will be sent.";
            }
            catch (Exception exception) { _lastError = exception.Message; }
            SetDiagnostic();
        }

        private async Task StopAndReleaseAsync(int commandGeneration, long revision)
        {
            await StopHardwareAsync(commandGeneration, revision).ConfigureAwait(false);
            _service.ReleaseFollowerOwnership(_activeOutput, revision);
        }

        private void SetDiagnostic()
        {
            bool pending;
            int generation;
            lock (_gate) { pending = _pending.HasValue || _workerActive; generation = _generation; }
            _service.SetFollowerDiagnostic(_activeOutput,
                "output=" + _activeOutput.Capability + "; source=" + _sourcePosition.ToString("F3")
                + "; mapped=" + _mappedPosition.ToString("F3") + "; last="
                + (float.IsNaN(_lastMapped) ? "none" : _lastMapped.ToString("F3")) + "; duration="
                + _lastSentDuration.ToString("F3") + "s; sent=" + _lastSendRealtime.ToString("F2")
                + "s; device-gap=" + _sendGapMilliseconds + "ms; pending=" + (pending ? "yes" : "no")
                + "; generation=" + generation + "; error=" + (_lastError.Length == 0 ? "none" : _lastError));
        }
    }
}
