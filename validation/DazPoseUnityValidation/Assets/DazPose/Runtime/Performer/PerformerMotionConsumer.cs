using System;
using DazPose.Motion;

namespace DazPose.Performer
{
    /// <summary>Adapts authoritative MotionDriver samples to one performer's masked visual layer.</summary>
    internal sealed class PerformerMotionConsumer : IDisposable
    {
        private readonly MotionDriver _driver;
        private readonly PerformerMotionLayer _layer;
        private bool _disposed;

        public bool IsBound => !_disposed;
        public float MotionWeight => _layer.MotionWeight;
        public float SampledPosition01 => _layer.SampledPosition01;
        public string ActiveVariantName => _layer.ActiveVariantName;

        public PerformerMotionConsumer(MotionDriver driver, PerformerMotionLayer layer)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _driver.Sampled += OnSampled;
            _driver.RunningChanged += OnRunningChanged;

            MotionSample current = _driver.CurrentSample;
            _layer.SetSample(current);
            if (_driver.IsRunning) _layer.SetEngaged(true);
        }

        public void SetVariant(int index, float blendSeconds) => _layer.SetVariant(index, blendSeconds);

        public void Advance(float deltaSeconds) => _layer.Advance(deltaSeconds);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _driver.Sampled -= OnSampled;
            _driver.RunningChanged -= OnRunningChanged;
        }

        private void OnSampled(MotionSample sample)
        {
            // Reset publishes a zero sample after RunningChanged(false). Keep the last scrubbed
            // location while ownership fades away; the next start samples CurrentSample first.
            if (_driver.IsRunning) _layer.SetSample(sample);
        }

        private void OnRunningChanged(bool running)
        {
            if (running)
            {
                _layer.SetSample(_driver.CurrentSample);
                _layer.SetEngaged(true);
            }
            else
            {
                _layer.SetEngaged(false);
            }
        }
    }
}
