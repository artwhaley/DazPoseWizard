using UnityEngine;

namespace DazPose.Performer
{
    internal struct PerformerAttentionLifeSettings
    {
        public bool Enabled;
        public int Seed;
        public bool EyeFixationEnabled;
        public float EyeMaxHorizontalDegrees;
        public float EyeMaxVerticalDegrees;
        public float EyeMinimumHoldSeconds;
        public float EyeMaximumHoldSeconds;
        public float EyeCenterBias;
        public bool HeadEnabled;
        public float HeadMaxTiltDegrees;
        public float HeadMaxChinDegrees;
        public float HeadMinimumHoldSeconds;
        public float HeadMaximumHoldSeconds;
        public float HeadTransitionResponse;
    }

    internal readonly struct PerformerAttentionLifeOutput
    {
        public readonly float EyeHorizontalOffsetDegrees;
        public readonly float EyeVerticalOffsetDegrees;
        public readonly float HeadTiltDegrees;
        public readonly float HeadChinDegrees;
        public readonly Quaternion PreferredHeadBias;

        public PerformerAttentionLifeOutput(float eyeHorizontalOffsetDegrees,
            float eyeVerticalOffsetDegrees, float headTiltDegrees, float headChinDegrees,
            Quaternion preferredHeadBias)
        {
            EyeHorizontalOffsetDegrees = eyeHorizontalOffsetDegrees;
            EyeVerticalOffsetDegrees = eyeVerticalOffsetDegrees;
            HeadTiltDegrees = headTiltDegrees;
            HeadChinDegrees = headChinDegrees;
            PreferredHeadBias = preferredHeadBias;
        }
    }

    internal struct PerformerDeterministicRandom
    {
        private uint _state;

        public PerformerDeterministicRandom(int seed, uint stream)
        {
            _state = Mix(unchecked((uint)seed) ^ stream);
            if (_state == 0u) _state = 0x6D2B79F5u;
        }

        public float Next01()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        public float Range(float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, Next01());
        }

        private uint NextUInt()
        {
            var value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }

        private static uint Mix(uint value)
        {
            value += 0x9E3779B9u;
            value ^= value >> 16;
            value *= 0x85EBCA6Bu;
            value ^= value >> 13;
            value *= 0xC2B2AE35u;
            value ^= value >> 16;
            return value;
        }
    }

    internal sealed class PerformerAttentionLife
    {
        private const uint EyeRandomStream = 0xE1E00001u;
        private const uint HeadRandomStream = 0xA77E0002u;
        private const int MaximumCatchUpEvents = 256;

        private readonly Vector3 _headLocalAim;
        private readonly Vector3 _headLocalRight;
        private PerformerAttentionLifeSettings _settings;
        private PerformerDeterministicRandom _eyeRandom;
        private PerformerDeterministicRandom _headRandom;
        private float _eyeEventCountdown;
        private float _headEventCountdown;
        private float _eyeHorizontalOffset;
        private float _eyeVerticalOffset;
        private float _desiredHeadTilt;
        private float _desiredHeadChin;
        private float _headTilt;
        private float _headChin;
        private bool _gazeActive;
        private bool _eyeEventsActive;
        private bool _headEventsActive;

        public float EyeEventCountdown => _eyeEventsActive ? Mathf.Max(0f, _eyeEventCountdown) : 0f;
        public float HeadEventCountdown => _headEventsActive ? Mathf.Max(0f, _headEventCountdown) : 0f;
        public PerformerAttentionLifeSettings Settings => _settings;

        public PerformerAttentionLifeOutput CurrentOutput
        {
            get
            {
                var bias = Quaternion.identity;
                if (_headLocalAim.sqrMagnitude > 0.9f && _headLocalRight.sqrMagnitude > 0.9f)
                {
                    var tilt = Quaternion.AngleAxis(_headTilt, _headLocalAim);
                    var chin = Quaternion.AngleAxis(_headChin, _headLocalRight);
                    bias = tilt * chin;
                }

                return new PerformerAttentionLifeOutput(_eyeHorizontalOffset, _eyeVerticalOffset,
                    _headTilt, _headChin, bias);
            }
        }

        public PerformerAttentionLife(Vector3 headLocalAim, Vector3 headLocalRight,
            PerformerAttentionLifeSettings settings)
        {
            _headLocalAim = headLocalAim.normalized;
            _headLocalRight = headLocalRight.normalized;
            _settings = Sanitize(settings);
            ResetRandomStreams(_settings.Seed);
        }

        public void Configure(PerformerAttentionLifeSettings settings)
        {
            settings = Sanitize(settings);
            if (settings.Seed != _settings.Seed)
            {
                _settings = settings;
                ResetRandomStreams(settings.Seed);
                return;
            }

            _settings = settings;
        }

        public void SetGazeActive(bool active)
        {
            _gazeActive = active;
            if (active) return;

            _eyeHorizontalOffset = 0f;
            _eyeVerticalOffset = 0f;
            _desiredHeadTilt = 0f;
            _desiredHeadChin = 0f;
            _eyeEventCountdown = 0f;
            _headEventCountdown = 0f;
        }

        public void Advance(float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            var lifeActive = _settings.Enabled && _gazeActive;
            var eyeActive = lifeActive && _settings.EyeFixationEnabled;
            var headActive = lifeActive && _settings.HeadEnabled;

            AdvanceEyeLife(eyeActive, deltaTime);
            AdvanceHeadLife(headActive, deltaTime);

            var response = Mathf.Max(0f, _settings.HeadTransitionResponse);
            var alpha = response <= 0f ? 1f : 1f - Mathf.Exp(-response * deltaTime);
            _headTilt = Mathf.Lerp(_headTilt, _desiredHeadTilt, alpha);
            _headChin = Mathf.Lerp(_headChin, _desiredHeadChin, alpha);
            if (Mathf.Abs(_headTilt) < 0.001f && Mathf.Abs(_desiredHeadTilt) < 0.001f) _headTilt = 0f;
            if (Mathf.Abs(_headChin) < 0.001f && Mathf.Abs(_desiredHeadChin) < 0.001f) _headChin = 0f;
        }

        private void AdvanceEyeLife(bool active, float deltaTime)
        {
            if (active != _eyeEventsActive)
            {
                _eyeEventsActive = active;
                _eyeEventCountdown = active ? SampleEyeHold() : 0f;
                if (!active)
                {
                    _eyeHorizontalOffset = 0f;
                    _eyeVerticalOffset = 0f;
                }
            }

            if (!active) return;
            _eyeEventCountdown -= deltaTime;
            var eventCount = 0;
            while (_eyeEventCountdown <= 0f && eventCount++ < MaximumCatchUpEvents)
            {
                _eyeHorizontalOffset = SampleCenteredOffset(_settings.EyeMaxHorizontalDegrees,
                    _settings.EyeCenterBias);
                _eyeVerticalOffset = SampleCenteredOffset(_settings.EyeMaxVerticalDegrees,
                    _settings.EyeCenterBias);
                _eyeEventCountdown += SampleEyeHold();
            }
        }

        private void AdvanceHeadLife(bool active, float deltaTime)
        {
            if (active != _headEventsActive)
            {
                _headEventsActive = active;
                _headEventCountdown = active ? SampleHeadHold() : 0f;
                if (!active)
                {
                    _desiredHeadTilt = 0f;
                    _desiredHeadChin = 0f;
                }
            }

            if (!active) return;
            _headEventCountdown -= deltaTime;
            var eventCount = 0;
            while (_headEventCountdown <= 0f && eventCount++ < MaximumCatchUpEvents)
            {
                _desiredHeadTilt = _headRandom.Range(-_settings.HeadMaxTiltDegrees,
                    _settings.HeadMaxTiltDegrees);
                _desiredHeadChin = _headRandom.Range(-_settings.HeadMaxChinDegrees,
                    _settings.HeadMaxChinDegrees);
                _headEventCountdown += SampleHeadHold();
            }
        }

        private float SampleCenteredOffset(float maximum, float centerBias)
        {
            var sample = _eyeRandom.Next01() * 2f - 1f;
            var centered = Mathf.Sign(sample) * Mathf.Pow(Mathf.Abs(sample), centerBias);
            return centered * maximum;
        }

        private float SampleEyeHold()
        {
            return _eyeRandom.Range(_settings.EyeMinimumHoldSeconds, _settings.EyeMaximumHoldSeconds);
        }

        private float SampleHeadHold()
        {
            return _headRandom.Range(_settings.HeadMinimumHoldSeconds, _settings.HeadMaximumHoldSeconds);
        }

        private void ResetRandomStreams(int seed)
        {
            _eyeRandom = new PerformerDeterministicRandom(seed, EyeRandomStream);
            _headRandom = new PerformerDeterministicRandom(seed, HeadRandomStream);
            _eyeEventCountdown = 0f;
            _headEventCountdown = 0f;
            _eyeHorizontalOffset = 0f;
            _eyeVerticalOffset = 0f;
            _desiredHeadTilt = 0f;
            _desiredHeadChin = 0f;
            _headTilt = 0f;
            _headChin = 0f;
            _eyeEventsActive = false;
            _headEventsActive = false;
        }

        private static PerformerAttentionLifeSettings Sanitize(PerformerAttentionLifeSettings settings)
        {
            settings.EyeMaxHorizontalDegrees = Mathf.Clamp(settings.EyeMaxHorizontalDegrees, 0f, 8f);
            settings.EyeMaxVerticalDegrees = Mathf.Clamp(settings.EyeMaxVerticalDegrees, 0f, 6f);
            settings.EyeMinimumHoldSeconds = Mathf.Max(0.05f, settings.EyeMinimumHoldSeconds);
            settings.EyeMaximumHoldSeconds = Mathf.Max(settings.EyeMinimumHoldSeconds,
                settings.EyeMaximumHoldSeconds);
            settings.EyeCenterBias = Mathf.Clamp(settings.EyeCenterBias, 1f, 4f);
            settings.HeadMaxTiltDegrees = Mathf.Clamp(settings.HeadMaxTiltDegrees, 0f, 15f);
            settings.HeadMaxChinDegrees = Mathf.Clamp(settings.HeadMaxChinDegrees, 0f, 12f);
            settings.HeadMinimumHoldSeconds = Mathf.Max(0.1f, settings.HeadMinimumHoldSeconds);
            settings.HeadMaximumHoldSeconds = Mathf.Max(settings.HeadMinimumHoldSeconds,
                settings.HeadMaximumHoldSeconds);
            settings.HeadTransitionResponse = Mathf.Max(0f, settings.HeadTransitionResponse);
            return settings;
        }
    }
}
