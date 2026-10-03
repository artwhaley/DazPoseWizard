using System;
using UnityEngine;

namespace DazPose.Motion
{
    /// <summary>The original phase-continuous sine oscillator, isolated behind IMotionSource.</summary>
    internal sealed class SineMotionSource : IMotionSource
    {
        private const float DirectionEpsilon = 0.0001f;
        private double _phase;
        private double _timeSeconds;
        private float _frequencyHz;

        public double DurationSeconds => double.PositiveInfinity;
        public bool IsComplete => false;
        public float FrequencyHz
        {
            get => _frequencyHz;
            set
            {
                if (!IsFinite(value))
                    throw new ArgumentOutOfRangeException(nameof(value), "FrequencyHz must be finite.");
                _frequencyHz = Mathf.Max(0f, value);
            }
        }

        public MotionSourceSample CurrentSample => CreateSample();

        public SineMotionSource(float frequencyHz)
        {
            FrequencyHz = IsFinite(frequencyHz) ? frequencyHz : 1f;
        }

        public void Advance(double deltaSeconds)
        {
            ValidateNonnegativeFinite(deltaSeconds, nameof(deltaSeconds));
            _phase = Repeat01(_phase + deltaSeconds * _frequencyHz);
            _timeSeconds += deltaSeconds;
        }

        public void Reset()
        {
            _phase = 0d;
            _timeSeconds = 0d;
        }

        public void Seek(double timeSeconds)
        {
            ValidateNonnegativeFinite(timeSeconds, nameof(timeSeconds));
            _timeSeconds = timeSeconds;
            _phase = Repeat01(timeSeconds * _frequencyHz);
        }

        private MotionSourceSample CreateSample()
        {
            float phase = (float)Repeat01(_phase);
            float position = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
            float velocity = Mathf.PI * _frequencyHz * Mathf.Sin(phase * Mathf.PI * 2f);
            MotionDirection direction = velocity > DirectionEpsilon
                ? MotionDirection.Increasing
                : velocity < -DirectionEpsilon ? MotionDirection.Decreasing : MotionDirection.Stationary;
            return new MotionSourceSample(_timeSeconds, phase, Mathf.Clamp01(position), velocity, direction);
        }

        private static double Repeat01(double value) => value - Math.Floor(value);

        private static void ValidateNonnegativeFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(name, "Timeline values must be finite and nonnegative.");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
