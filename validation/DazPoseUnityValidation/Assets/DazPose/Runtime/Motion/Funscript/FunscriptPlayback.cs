using System;

namespace DazPose.Motion
{
    /// <summary>Timeline cursor and linear evaluator for an immutable Funscript program.</summary>
    public sealed class FunscriptPlayback : IMotionSource
    {
        private const float DirectionEpsilon = 0.0001f;
        private readonly FunscriptMotionProgram _program;
        private int _cursor = -1;
        private int _segmentFromIndex = -1;
        private int _segmentToIndex = -1;
        private double _timeSeconds;
        private FunscriptSegment _currentSegment;
        private bool _hasCurrentSegment;
        private bool _segmentAnnouncementPending;

        public FunscriptMotionProgram Program => _program;
        public double DurationSeconds => _program.DurationSeconds;
        public bool IsComplete { get; private set; }
        private bool _loop;
        public bool Loop
        {
            get => _loop;
            set
            {
                _loop = value;
                if (value) IsComplete = false;
            }
        }
        public MotionSourceSample CurrentSample { get; private set; }
        public double CurrentTimeSeconds => CurrentSample.TimeSeconds;
        public float Position01 => CurrentSample.Position01;
        public float Velocity => CurrentSample.Velocity;
        public MotionDirection Direction => CurrentSample.Direction;
        public bool HasCurrentSegment => _hasCurrentSegment;
        public FunscriptSegment CurrentSegment => _currentSegment;

        public event Action<FunscriptSegment> SegmentChanged;

        public FunscriptPlayback(FunscriptMotionProgram program, bool loop = false)
        {
            _program = program != null ? program : throw new ArgumentNullException(nameof(program));
            Loop = loop;
            Reset();
        }

        public void Advance(double deltaSeconds)
        {
            ValidateNonnegativeFinite(deltaSeconds, nameof(deltaSeconds));
            if (IsComplete) return;
            FlushPendingSegmentAnnouncement();

            double duration = DurationSeconds;
            if (duration <= 0d)
            {
                _timeSeconds = 0d;
                _cursor = _program.ActionCount - 1;
                IsComplete = !Loop;
                UpdateSample(true);
                return;
            }

            if (Loop)
            {
                double remaining = deltaSeconds;
                while (true)
                {
                    double untilEnd = duration - _timeSeconds;
                    if (remaining < untilEnd)
                    {
                        AdvanceTimelineTo(_timeSeconds + remaining);
                        break;
                    }

                    AdvanceTimelineTo(duration);
                    remaining -= untilEnd;
                    _timeSeconds = 0d;
                    _cursor = -1;
                    AdvanceTimelineTo(0d);
                    if (remaining <= 0d) break;
                }
            }
            else
            {
                double nextTime = _timeSeconds + deltaSeconds;
                if (nextTime >= duration)
                {
                    nextTime = duration;
                    IsComplete = true;
                }
                AdvanceTimelineTo(nextTime);
            }
        }

        public void Seek(double timeSeconds)
        {
            ValidateNonnegativeFinite(timeSeconds, nameof(timeSeconds));
            _timeSeconds = Math.Min(timeSeconds, DurationSeconds);
            _cursor = _program.FindLastActionAtOrBefore(_timeSeconds);
            IsComplete = !Loop && _timeSeconds >= DurationSeconds;
            UpdateSample(true);
        }

        public void Reset()
        {
            _timeSeconds = 0d;
            _cursor = _program.FindLastActionAtOrBefore(0d);
            IsComplete = false;
            UpdateSample(false);
        }

        private void AdvanceTimelineTo(double targetTimeSeconds)
        {
            while (_cursor + 1 < _program.ActionCount)
            {
                double boundarySeconds = _program.GetAction(_cursor + 1).AtMilliseconds / 1000d;
                if (boundarySeconds > targetTimeSeconds) break;

                // Move through all same-time authored actions together. The final duplicate
                // is the target that remains active after this instant.
                _timeSeconds = boundarySeconds;
                AdvanceCursorTo(boundarySeconds);
                UpdateSample(true);
            }

            _timeSeconds = targetTimeSeconds;
            AdvanceCursorTo(targetTimeSeconds);
            UpdateSample(true);
        }

        private void AdvanceCursorTo(double timeSeconds)
        {
            while (_cursor + 1 < _program.ActionCount
                && _program.GetAction(_cursor + 1).AtMilliseconds / 1000d <= timeSeconds)
                _cursor++;
        }

        private void UpdateSample(bool notifySegmentChange)
        {
            float position;
            float velocity = 0f;
            if (_program.ActionCount == 0)
            {
                position = 0f;
            }
            else if (_cursor < 0)
            {
                position = _program.InterpretPosition(_program.GetAction(0));
            }
            else if (_cursor >= _program.ActionCount - 1)
            {
                position = _program.InterpretPosition(_program.GetAction(_program.ActionCount - 1));
            }
            else
            {
                FunscriptSegment segment = _program.GetSegmentStartingAtAction(_cursor);
                if (segment.DurationSeconds <= 0d)
                {
                    position = segment.ToPosition01;
                }
                else
                {
                    double progress = (_timeSeconds - segment.StartSeconds) / segment.DurationSeconds;
                    float clampedProgress = (float)Math.Max(0d, Math.Min(1d, progress));
                    position = UnityEngine.Mathf.LerpUnclamped(segment.FromPosition01,
                        segment.ToPosition01, clampedProgress);
                    velocity = (float)((segment.ToPosition01 - segment.FromPosition01) / segment.DurationSeconds);
                }
                _currentSegment = segment;
                bool segmentChanged = SetCurrentSegment(segment.FromActionIndex, segment.ToActionIndex);
                SetCurrentSample(position, velocity);
                if (notifySegmentChange && (segmentChanged || _segmentAnnouncementPending))
                {
                    _segmentAnnouncementPending = false;
                    RaiseSegmentChanged(segment);
                }
                else if (segmentChanged) _segmentAnnouncementPending = true;
                return;
            }

            ClearCurrentSegment();
            SetCurrentSample(position, velocity);
        }

        private void SetCurrentSample(float position, float velocity)
        {
            double progress = DurationSeconds > 0d
                ? Math.Max(0d, Math.Min(1d, _timeSeconds / DurationSeconds)) : 1d;
            MotionDirection direction = velocity > DirectionEpsilon
                ? MotionDirection.Increasing
                : velocity < -DirectionEpsilon ? MotionDirection.Decreasing : MotionDirection.Stationary;
            CurrentSample = new MotionSourceSample(_timeSeconds, (float)progress,
                UnityEngine.Mathf.Clamp01(position), velocity, direction);
        }

        private bool SetCurrentSegment(int fromIndex, int toIndex)
        {
            bool changed = !_hasCurrentSegment || fromIndex != _segmentFromIndex || toIndex != _segmentToIndex;
            _hasCurrentSegment = true;
            _segmentFromIndex = fromIndex;
            _segmentToIndex = toIndex;
            return changed;
        }

        private void ClearCurrentSegment()
        {
            _hasCurrentSegment = false;
            _segmentFromIndex = -1;
            _segmentToIndex = -1;
            _currentSegment = default;
            _segmentAnnouncementPending = false;
        }

        private void FlushPendingSegmentAnnouncement()
        {
            if (!_segmentAnnouncementPending || !_hasCurrentSegment) return;
            _segmentAnnouncementPending = false;
            RaiseSegmentChanged(_currentSegment);
        }

        private void RaiseSegmentChanged(FunscriptSegment segment)
        {
            Action<FunscriptSegment> handlers = SegmentChanged;
            if (handlers == null) return;
            foreach (Action<FunscriptSegment> handler in handlers.GetInvocationList())
            {
                try { handler(segment); }
                catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
            }
        }

        private static void ValidateNonnegativeFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(name, "Timeline values must be finite and nonnegative.");
        }
    }
}
