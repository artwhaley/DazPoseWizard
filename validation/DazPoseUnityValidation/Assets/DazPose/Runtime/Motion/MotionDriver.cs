using System;
using UnityEngine;

namespace DazPose.Motion
{
    /// <summary>Authoritative source-independent publisher of MotionSample values.</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class MotionDriver : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float frequencyHz = 1f;
        [SerializeField] private bool playOnEnable;
        [SerializeField] private MotionSourceMode sourceMode = MotionSourceMode.Sine;
        [SerializeField] private FunscriptMotionProgram funscriptProgram;
        [SerializeField] private bool loopFunscript;

        private SineMotionSource _sineSource;
        private FunscriptPlayback _funscriptPlayback;
        private IMotionSource _activeSource;
        private long _sequence;

        public bool IsRunning { get; private set; }

        public MotionSourceMode SourceMode
        {
            get => sourceMode;
            set
            {
                if (value != MotionSourceMode.Sine && value != MotionSourceMode.Funscript)
                    throw new ArgumentOutOfRangeException(nameof(value));
                if (sourceMode == value) return;

                sourceMode = value;
                RefreshActiveSource();
                if (_activeSource == null)
                {
                    if (IsRunning) StopMotion();
                    return;
                }

                _activeSource.Reset();
                if (IsRunning) PublishSample(_activeSource.CurrentSample);
                else CurrentSample = CreateSample(_sequence, _activeSource.CurrentSample);
            }
        }

        /// <summary>Retains the accepted oscillator control in both source modes.</summary>
        public float FrequencyHz
        {
            get => frequencyHz;
            set
            {
                if (!IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value), "FrequencyHz must be finite.");
                frequencyHz = Mathf.Max(0f, value);
                EnsureSineSource();
                _sineSource.FrequencyHz = frequencyHz;
                if (sourceMode == MotionSourceMode.Sine && _activeSource != null)
                    CurrentSample = CreateSample(_sequence, _activeSource.CurrentSample);
            }
        }

        public FunscriptMotionProgram FunscriptProgram
        {
            get => funscriptProgram;
            set
            {
                if (funscriptProgram == value) return;
                funscriptProgram = value;
                _funscriptPlayback = value != null ? new FunscriptPlayback(value, loopFunscript) : null;
                RefreshActiveSource();
                if (sourceMode != MotionSourceMode.Funscript) return;
                if (_activeSource == null)
                {
                    if (IsRunning) StopMotion();
                    return;
                }
                if (IsRunning) PublishSample(_activeSource.CurrentSample);
                else CurrentSample = CreateSample(_sequence, _activeSource.CurrentSample);
            }
        }

        public FunscriptPlayback FunscriptPlayback => _funscriptPlayback;
        public bool HasFunscriptProgram => funscriptProgram != null;
        public bool SourceReady => sourceMode == MotionSourceMode.Sine || funscriptProgram != null;

        public bool Loop
        {
            get => loopFunscript;
            set
            {
                loopFunscript = value;
                if (_funscriptPlayback != null) _funscriptPlayback.Loop = value;
            }
        }

        public MotionSample CurrentSample { get; private set; }
        public event Action<MotionSample> Sampled;
        public event Action<bool> RunningChanged;

        private void Awake()
        {
            frequencyHz = IsFinite(frequencyHz) ? Mathf.Max(0f, frequencyHz) : 1f;
            if (sourceMode != MotionSourceMode.Sine && sourceMode != MotionSourceMode.Funscript)
                sourceMode = MotionSourceMode.Sine;
            EnsureSineSource();
            if (funscriptProgram != null)
                _funscriptPlayback = new FunscriptPlayback(funscriptProgram, loopFunscript);
            RefreshActiveSource();
            CurrentSample = _activeSource != null
                ? CreateSample(_sequence, _activeSource.CurrentSample)
                : new MotionSample(_sequence, 0d, 0f, 0f, 0f, MotionDirection.Stationary);
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

        /// <summary>Advances and publishes exactly one source-neutral sample.</summary>
        public void Advance(float deltaSeconds)
        {
            if (!IsRunning) return;
            if (!IsFinite(deltaSeconds) || deltaSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Delta time must be finite and nonnegative.");
            if (_activeSource == null)
            {
                StopMotion();
                return;
            }

            _activeSource.Advance(deltaSeconds);
            PublishSample(_activeSource.CurrentSample);
            if (IsRunning && _activeSource.IsComplete) StopMotion();
        }

        /// <summary>Resumes at the held source time without resetting it.</summary>
        public void StartMotion()
        {
            if (IsRunning) return;
            RefreshActiveSource();
            if (_activeSource == null)
            {
                Debug.LogError("Funscript source is selected, but no FunscriptMotionProgram is assigned.", this);
                return;
            }
            if (_activeSource.IsComplete) return;
            IsRunning = true;
            PublishRunningChanged(true);
        }

        /// <summary>Pauses advancement and preserves the current sample and source time.</summary>
        public void StopMotion()
        {
            if (!IsRunning) return;
            IsRunning = false;
            PublishRunningChanged(false);
        }

        /// <summary>Moves the selected source to its start and runs it.</summary>
        public void RestartMotion()
        {
            RefreshActiveSource();
            if (_activeSource == null)
            {
                Debug.LogError("Funscript source is selected, but no FunscriptMotionProgram is assigned.", this);
                return;
            }
            _activeSource.Reset();
            if (IsRunning)
            {
                PublishSample(_activeSource.CurrentSample);
                return;
            }

            CurrentSample = CreateSample(_sequence, _activeSource.CurrentSample);
            StartMotion();
        }

        /// <summary>Stops and resets the selected source to its authored initial position.</summary>
        public void ResetMotion()
        {
            StopMotion();
            RefreshActiveSource();
            if (_activeSource == null)
            {
                CurrentSample = new MotionSample(_sequence, 0d, 0f, 0f, 0f, MotionDirection.Stationary);
                return;
            }
            _activeSource.Reset();
            CurrentSample = CreateSample(_sequence, _activeSource.CurrentSample);
        }

        /// <summary>Seeks the selected source and immediately publishes its resulting sample.</summary>
        public void Seek(double timeSeconds)
        {
            if (double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds) || timeSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(timeSeconds), "Seek time must be finite and nonnegative.");
            RefreshActiveSource();
            if (_activeSource == null)
                throw new InvalidOperationException("Cannot seek because the selected motion source is unavailable.");

            _activeSource.Seek(timeSeconds);
            PublishSample(_activeSource.CurrentSample);
            if (IsRunning && _activeSource.IsComplete) StopMotion();
        }

        private void EnsureSineSource()
        {
            if (_sineSource == null) _sineSource = new SineMotionSource(frequencyHz);
        }

        private void RefreshActiveSource()
        {
            EnsureSineSource();
            if (funscriptProgram != null && (_funscriptPlayback == null
                || _funscriptPlayback.Program != funscriptProgram))
                _funscriptPlayback = new FunscriptPlayback(funscriptProgram, loopFunscript);
            if (_funscriptPlayback != null) _funscriptPlayback.Loop = loopFunscript;
            _activeSource = sourceMode == MotionSourceMode.Sine ? (IMotionSource)_sineSource : _funscriptPlayback;
        }

        private void PublishSample(MotionSourceSample sourceSample)
        {
            _sequence++;
            MotionSample sample = CreateSample(_sequence, sourceSample);
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

        private static MotionSample CreateSample(long sequence, MotionSourceSample sourceSample)
        {
            return new MotionSample(sequence, sourceSample.TimeSeconds,
                Mathf.Clamp01(sourceSample.Phase01), Mathf.Clamp01(sourceSample.Position01),
                sourceSample.Velocity, sourceSample.Direction);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
