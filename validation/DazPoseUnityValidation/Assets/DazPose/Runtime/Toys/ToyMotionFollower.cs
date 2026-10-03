using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DazPose.Motion;
using UnityEngine;

namespace DazPose.Toys
{
    /// <summary>One bounded, feature-specific consumer of the authoritative MotionDriver timeline.</summary>
    internal sealed class ToyMotionFollower : IDisposable
    {
        private const int UnknownGapMilliseconds = 50;
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
        private int _lastFromAction = -1;
        private int _lastToAction = -1;
        private double _lastSentDuration;
        private float _sourcePosition;
        private float _mappedPosition;
        private double _lastSendRealtime;
        private string _lastError = string.Empty;
        private ToyOutputBinding _positionHoldBinding;

        private readonly struct PendingOutput
        {
            public readonly float Value;
            public readonly uint DurationMilliseconds;
            public readonly int Generation;
            public readonly int ServiceGeneration;
            public PendingOutput(float value, uint durationMilliseconds, int generation, int serviceGeneration)
            { Value = value; DurationMilliseconds = durationMilliseconds; Generation = generation; ServiceGeneration = serviceGeneration; }
        }

        public ToyMotionFollower(ToyControlService service, MotionDriver driver,
            ToyMotionBinding binding, ToyOutputBinding activeOutput, uint messageTimingGapMilliseconds)
        {
            _service = service;
            _driver = driver;
            _binding = binding;
            _activeOutput = activeOutput;
            _sendGapMilliseconds = (int)Math.Max(messageTimingGapMilliseconds, UnknownGapMilliseconds);
            _positionHoldBinding = new ToyOutputBinding(activeOutput.DeviceIndex,
                activeOutput.FeatureIndex, ToyOutputCapability.Position);
            _driver.Sampled += OnSampled;
            _driver.RunningChanged += OnRunningChanged;
            _driver.Sought += OnSought;
            if (_driver.IsRunning) SynchronizeCurrent();
            SetDiagnostic();
        }

        public void Dispose() => Dispose(stopHardware: true);

        public void Dispose(bool stopHardware)
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _generation++;
                _pending = null;
            }
            _driver.Sampled -= OnSampled;
            _driver.RunningChanged -= OnRunningChanged;
            _driver.Sought -= OnSought;
            if (stopHardware) _ = StopHardwareAsync(_service.OutputCommandGeneration);
            SetDiagnostic();
        }

        private void OnSampled(MotionSample sample)
        {
            if (_disposed || !_driver.IsRunning) return;
            UpdateResolvedStrategy();
            _sourcePosition = sample.Position01;
            _mappedPosition = _binding.Map(sample.Position01);
            if (_activeOutput.Capability == ToyOutputCapability.Position)
                Enqueue(_mappedPosition, 0, false);
            else if (!_driver.TryGetCurrentTargetSegment(out _))
                Enqueue(_mappedPosition, (uint)_sendGapMilliseconds, true);
            else if (_driver.TryGetCurrentTargetSegment(out MotionTargetSegment segment)
                && (segment.FromActionIndex != _lastFromAction || segment.ToActionIndex != _lastToAction))
                QueueSegment(segment);
            SetDiagnostic();
        }

        private void OnRunningChanged(bool running)
        {
            if (_disposed) return;
            if (running)
            {
                UpdateResolvedStrategy();
                SynchronizeCurrent();
            }
            else
            {
                lock (_gate) { _generation++; _pending = null; }
                _ = StopHardwareAsync(_service.OutputCommandGeneration);
            }
        }

        private void OnSought()
        {
            if (_disposed) return;
            UpdateResolvedStrategy();
            lock (_gate) { _generation++; _pending = null; }
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
                Enqueue(_mappedPosition, 0, true);
            else if (_driver.TryGetCurrentTargetSegment(out MotionTargetSegment segment))
                QueueSegment(segment);
            else
                Enqueue(_mappedPosition, (uint)_sendGapMilliseconds, true);
        }

        private void QueueSegment(MotionTargetSegment segment)
        {
            _lastFromAction = segment.FromActionIndex;
            _lastToAction = segment.ToActionIndex;
            double remaining = Math.Max(0.01d, segment.EndTimeSeconds - _driver.CurrentSample.TimeSeconds);
            uint duration = (uint)Math.Max(1d, Math.Min(uint.MaxValue, Math.Round(remaining * 1000d)));
            _sourcePosition = _driver.CurrentSample.Position01;
            _mappedPosition = _binding.Map(segment.TargetPosition01);
            Enqueue(_mappedPosition, duration, true);
        }

        private void UpdateResolvedStrategy()
        {
            if (_binding.Strategy != ToyMotionStrategy.Auto) return;
            ToyOutputBinding desired = _service.ResolveMotionOutput(_binding);
            if (desired == _activeOutput) return;
            _activeOutput = desired;
            _positionHoldBinding = new ToyOutputBinding(desired.DeviceIndex,
                desired.FeatureIndex, ToyOutputCapability.Position);
            lock (_gate) { _generation++; _pending = null; }
            _lastFromAction = -1;
            _lastToAction = -1;
            SynchronizeCurrent();
        }

        private void Enqueue(float value, uint duration, bool force)
        {
            lock (_gate)
            {
                if (_disposed) return;
                if (!force && !float.IsNaN(_lastMapped) && Mathf.Abs(value - _lastMapped) < PositionDeadband
                    && (!_pending.HasValue || Mathf.Abs(value - _pending.Value.Value) < PositionDeadband)) return;
                _pending = new PendingOutput(value, duration, _generation, _service.OutputCommandGeneration);
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

                if (_lastSentAt > 0d)
                {
                    double waitMilliseconds = _sendGapMilliseconds - (Stopwatch.GetTimestamp() - _lastSentAt)
                        * 1000d / Stopwatch.Frequency;
                    if (waitMilliseconds > 0d) await Task.Delay((int)Math.Ceiling(waitMilliseconds)).ConfigureAwait(false);
                }
                lock (_gate)
                {
                    if (_disposed || next.Generation != _generation) continue;
                }

                try
                {
                    bool sent = await _service.DispatchOutputAsync(_activeOutput, next.Value,
                        next.DurationMilliseconds, next.ServiceGeneration).ConfigureAwait(false);
                    if (!sent) continue;
                    _lastMapped = next.Value;
                    _lastSentDuration = next.DurationMilliseconds / 1000d;
                    _lastSendRealtime = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                    _lastSentAt = Stopwatch.GetTimestamp();
                    _lastError = string.Empty;
                }
                catch (Exception exception) { _lastError = exception.Message; }
                SetDiagnostic();
            }
        }

        private async Task StopHardwareAsync(int commandGeneration)
        {
            try
            {
                bool held = await _service.DispatchOutputAsync(_positionHoldBinding,
                    _binding.Map(_driver.CurrentSample.Position01), 0, commandGeneration).ConfigureAwait(false);
                if (!held) return;
                _lastError = string.Empty;
            }
            catch
            {
                try
                {
                    await _service.StopFeatureAsync(_activeOutput, commandGeneration).ConfigureAwait(false);
                    _lastError = _activeOutput.Capability == ToyOutputCapability.HwPositionWithDuration
                        ? "Stop requested for device; a current hardware-managed move may finish."
                        : string.Empty;
                }
                catch (Exception exception) { _lastError = exception.Message; }
            }
            SetDiagnostic();
        }

        private void SetDiagnostic()
        {
            bool pending;
            int generation;
            lock (_gate) { pending = _pending.HasValue || _workerActive; generation = _generation; }
            _service.SetFollowerDiagnostic(_binding.Output,
                "strategy=" + _activeOutput.Capability + "; source=" + _sourcePosition.ToString("F3")
                + "; mapped=" + _mappedPosition.ToString("F3") + "; last="
                + (float.IsNaN(_lastMapped) ? "none" : _lastMapped.ToString("F3")) + "; duration="
                + _lastSentDuration.ToString("F3") + "s; sent=" + _lastSendRealtime.ToString("F2")
                + "s; gap=" + _sendGapMilliseconds + "ms; pending=" + (pending ? "yes" : "no")
                + "; generation=" + generation + "; error=" + (_lastError.Length == 0 ? "none" : _lastError));
        }
    }
}
