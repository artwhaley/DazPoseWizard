using System;
using UnityEngine;

namespace DazPose.Motion
{
    /// <summary>Scene-level timing and normalized-position source, independent of animation consumers.</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class MotionDriver : MonoBehaviour
    {
        private const float DirectionEpsilon = 0.0001f;

        [SerializeField, Min(0f)] private float frequencyHz = 1f;
        [SerializeField] private bool playOnEnable;

        private double _phase;
        private double _timeSeconds;
        private long _sequence;

        public bool IsRunning { get; private set; }

        public float FrequencyHz
        {
            get => frequencyHz;
            set
            {
                if (!IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value), "FrequencyHz must be finite.");
                frequencyHz = Mathf.Max(0f, value);
                CurrentSample = CreateSample(_sequence);
            }
        }

        public MotionSample CurrentSample { get; private set; }
        public event Action<MotionSample> Sampled;
        public event Action<bool> RunningChanged;

        private void Awake()
        {
            frequencyHz = IsFinite(frequencyHz) ? Mathf.Max(0f, frequencyHz) : 1f;
            CurrentSample = CreateSample(_sequence);
        }

        private void OnEnable()
        {
            if (playOnEnable) StartMotion();
        }

        private void OnDisable() => StopMotion();

        private void Update()
        {
            if (IsRunning) Advance(Time.deltaTime);
        }

        /// <summary>Advances and publishes exactly one sample. Public for deterministic harnesses.</summary>
        public void Advance(float deltaSeconds)
        {
            if (!IsRunning) return;
            if (!IsFinite(deltaSeconds) || deltaSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Delta time must be finite and nonnegative.");

            _phase = Repeat01(_phase + (double)deltaSeconds * frequencyHz);
            _timeSeconds += deltaSeconds;
            PublishSample();
        }

        /// <summary>Resumes at the held phase without changing the position.</summary>
        public void StartMotion()
        {
            if (IsRunning) return;
            IsRunning = true;
            PublishRunningChanged(true);
        }

        /// <summary>Stops advancement and preserves the current phase and position.</summary>
        public void StopMotion()
        {
            if (!IsRunning) return;
            IsRunning = false;
            PublishRunningChanged(false);
        }

        /// <summary>Moves to endpoint A and runs, preserving the sequence across the restart.</summary>
        public void RestartMotion()
        {
            _phase = 0d;
            _timeSeconds = 0d;
            if (IsRunning)
            {
                PublishSample();
                return;
            }

            CurrentSample = CreateSample(_sequence);
            StartMotion();
        }

        /// <summary>Stops and restores endpoint A. The next run starts from zero.</summary>
        public void ResetMotion()
        {
            StopMotion();
            _phase = 0d;
            _timeSeconds = 0d;
            CurrentSample = CreateSample(_sequence);
        }

        private void PublishSample()
        {
            _sequence++;
            MotionSample sample = CreateSample(_sequence);
            CurrentSample = sample;
            Action<MotionSample> handlers = Sampled;
            if (handlers == null) return;
            foreach (Action<MotionSample> handler in handlers.GetInvocationList())
            {
                try { handler(sample); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }

        private void PublishRunningChanged(bool running)
        {
            Action<bool> handlers = RunningChanged;
            if (handlers == null) return;
            foreach (Action<bool> handler in handlers.GetInvocationList())
            {
                try { handler(running); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }

        private MotionSample CreateSample(long sequence)
        {
            float phase = (float)Repeat01(_phase);
            float position = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
            float velocity = Mathf.PI * frequencyHz * Mathf.Sin(phase * Mathf.PI * 2f);
            MotionDirection direction = velocity > DirectionEpsilon
                ? MotionDirection.Increasing
                : velocity < -DirectionEpsilon ? MotionDirection.Decreasing : MotionDirection.Stationary;
            return new MotionSample(sequence, _timeSeconds, phase, Mathf.Clamp01(position), velocity, direction);
        }

        private static double Repeat01(double value) => value - Math.Floor(value);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
