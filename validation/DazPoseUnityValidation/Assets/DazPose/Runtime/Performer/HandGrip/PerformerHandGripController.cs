using System;
using UnityEngine;
using UnityEngine.Playables;

namespace DazPose.Performer.HandGrip
{
    /// <summary>Small runtime API and blend/target binding for a performer's one active hand grip.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Performer/Hand Grip/Performer Hand Grip Controller")]
    public sealed class PerformerHandGripController : MonoBehaviour
    {
        [SerializeField] private HandGripRigProfile profile;
        [SerializeField] private MonoBehaviour targetSource;
        [SerializeField] private bool gripOnEnable;
        [SerializeField, Range(0f, 1f)] private float gripStrength01 = 1f;
        [SerializeField] private bool drawDiagnostics = true;

        private PerformerHandGripLayer _layer;
        private DazPose.Motion.MotionDriver _motionDriver;
        private IGripTarget _target;
        private GripFrame _currentFrame;
        private bool _hasCurrentFrame;
        private bool _gripRequested;
        private float _blendWeight;
        private float _blendStartWeight;
        private float _blendTargetWeight;
        private float _blendElapsed;
        private float _blendDuration;
        private float _lastPosition01;
        private HandGripStatus _status = HandGripStatus.InvalidProfile;
        private bool _targetFailureLogged;

        public HandGripRigProfile Profile => profile;
        public bool IsAvailable => _layer != null;
        public bool IsGripRequested => _gripRequested;
        public bool DiagnosticsVisible { get => drawDiagnostics; set => drawDiagnostics = value; }
        public float GripWeight => _blendWeight;
        public float GripStrength01
        {
            get => gripStrength01;
            set => gripStrength01 = Mathf.Clamp01(value);
        }
        public float Position01 => _lastPosition01;
        public HandGripSolveResult SolveResult => _layer != null
            ? _layer.GetSolveResult()
            : new HandGripSolveResult(_status, 0f, 0f, 0f, 0f, 0f);
        public HandGripAlignmentDiagnostics AlignmentDiagnostics => _layer != null && _hasCurrentFrame
            ? _layer.GetAlignmentDiagnostics(_currentFrame)
            : default;

        internal Playable AttachToGraph(PlayableGraph graph, Playable baseSource,
            Animator animator, DazPose.Motion.MotionDriver driver)
        {
            if (_layer != null) _layer.Dispose();
            _motionDriver = driver;
            if (profile == null)
            {
                _status = HandGripStatus.InvalidProfile;
                Debug.LogWarning("HandGrip is unavailable because no HandGripRigProfile is assigned.", this);
                return baseSource;
            }
            if (!profile.IsReady(animator, out string reason))
            {
                _status = HandGripStatus.InvalidProfile;
                Debug.LogWarning("HandGrip is unavailable: " + reason, this);
                return baseSource;
            }

            try
            {
                _layer = new PerformerHandGripLayer(graph, baseSource, animator, profile);
                if (targetSource != null) _target = targetSource as IGripTarget;
                if (targetSource != null && _target == null)
                    Debug.LogWarning("The configured hand-grip target does not implement IGripTarget.", targetSource);
                if (gripOnEnable) Grip(gripStrength01);
                else _layer.SetState(false, default, 0f, gripStrength01, HandGripStatus.InvalidTarget);
                return _layer.OutputPlayable;
            }
            catch (Exception exception)
            {
                _layer?.Dispose();
                _layer = null;
                _status = HandGripStatus.InvalidProfile;
                Debug.LogWarning("HandGrip could not join the performer animation graph: " + exception.Message, this);
                return baseSource;
            }
        }

        internal void Advance(float deltaSeconds)
        {
            if (_layer == null) return;
            float dt = Mathf.Max(0f, deltaSeconds);
            if (!Mathf.Approximately(_blendWeight, _blendTargetWeight))
            {
                if (_blendDuration <= 0f) _blendWeight = _blendTargetWeight;
                else
                {
                    _blendElapsed = Mathf.Min(_blendDuration, _blendElapsed + dt);
                    float t = SmoothStep01(_blendElapsed / _blendDuration);
                    _blendWeight = Mathf.LerpUnclamped(_blendStartWeight, _blendTargetWeight, t);
                }
            }
            _blendWeight = Mathf.Clamp01(_blendWeight);
            _lastPosition01 = _motionDriver != null
                ? Mathf.Clamp01(_motionDriver.CurrentSample.Position01) : 0f;

            if (_target != null && (_gripRequested || _blendWeight > 0f))
            {
                try
                {
                    _currentFrame = _target.Evaluate(_lastPosition01);
                    _hasCurrentFrame = _currentFrame.IsValid;
                    if (!_hasCurrentFrame)
                    {
                        _status = HandGripStatus.InvalidTarget;
                        _layer.SetState(false, default, _blendWeight, gripStrength01, _status);
                        return;
                    }
                    if (targetSource is GripContactRod rod) rod.CurrentPosition01 = _lastPosition01;
                    _targetFailureLogged = false;
                    _status = HandGripStatus.Clear;
                    _layer.SetState(true, _currentFrame, _blendWeight, gripStrength01, HandGripStatus.Clear);
                }
                catch (Exception exception)
                {
                    _hasCurrentFrame = false;
                    _status = HandGripStatus.InvalidTarget;
                    _layer.SetState(false, default, _blendWeight, gripStrength01, _status);
                    if (!_targetFailureLogged)
                    {
                        Debug.LogWarning("HandGrip target evaluation failed: " + exception.Message, this);
                        _targetFailureLogged = true;
                    }
                }
            }
            else
            {
                _hasCurrentFrame = false;
                _status = _gripRequested ? HandGripStatus.InvalidTarget : HandGripStatus.Clear;
                _layer.SetState(false, default, 0f, gripStrength01, _status);
                if (!_gripRequested && _blendWeight <= 0f) _target = null;
            }
        }

        public void Grip(float strength01 = 1f)
        {
            gripStrength01 = Mathf.Clamp01(strength01);
            if (targetSource != null)
            {
                IGripTarget configured = targetSource as IGripTarget;
                if (configured != null) _target = configured;
            }
            _gripRequested = true;
            BeginBlend(1f, profile != null ? profile.GripInSeconds : 0.25f);
        }

        public void Grip(MonoBehaviour newTarget, float strength01 = 1f)
        {
            if (newTarget == null) throw new ArgumentNullException(nameof(newTarget));
            IGripTarget gripTarget = newTarget as IGripTarget;
            if (gripTarget == null)
                throw new ArgumentException("The target component must implement IGripTarget.", nameof(newTarget));
            targetSource = newTarget;
            _target = gripTarget;
            Grip(strength01);
        }

        public void ReleaseGrip()
        {
            _gripRequested = false;
            BeginBlend(0f, profile != null ? profile.ReleaseSeconds : 0.25f);
        }

        internal void Detach()
        {
            _layer?.Dispose();
            _layer = null;
            _motionDriver = null;
            _hasCurrentFrame = false;
            _gripRequested = false;
            _blendWeight = _blendTargetWeight = 0f;
        }

        public HandGripStatus GetDigitStatus(HandGripDigit digit)
        {
            return _layer != null ? _layer.GetDigitStatus((int)digit) : _status;
        }

        public bool TryGetProbe(int index, out Vector3 worldPosition, out float radius,
            out HandGripProbeState state)
        {
            radius = _layer != null ? _layer.GetProbeRadius(index) : 0f;
            worldPosition = default;
            state = HandGripProbeState.Clear;
            return _layer != null && _layer.TryGetProbeDiagnostic(index, out worldPosition, out state);
        }

        private void BeginBlend(float target, float duration)
        {
            _blendStartWeight = _blendWeight;
            _blendTargetWeight = target;
            _blendElapsed = 0f;
            _blendDuration = Mathf.Max(0f, duration);
            if (_blendDuration <= 0f) _blendWeight = target;
        }

        private void OnDrawGizmos()
        {
            if (!drawDiagnostics || !_hasCurrentFrame || _layer == null) return;
            HandGripAlignmentDiagnostics alignment = AlignmentDiagnostics;
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(alignment.DesiredGripCenter, 0.012f);
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(alignment.ActualGripCenter, 0.013f);
            Gizmos.DrawLine(alignment.DesiredGripCenter, alignment.ActualGripCenter);
            for (int index = 0; index < _layer.ProbeCount; index++)
            {
                if (!TryGetProbe(index, out Vector3 point, out float radius, out HandGripProbeState state)) continue;
                Gizmos.color = state == HandGripProbeState.Penetrating ? Color.red
                    : state == HandGripProbeState.NearContact ? Color.yellow : Color.green;
                Gizmos.DrawWireSphere(point, radius);
            }
#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(alignment.DesiredGripCenter + Vector3.up * 0.03f,
                "Grip " + _status + "  Position01=" + _lastPosition01.ToString("0.00")
                + "  curl T/I/M/R/L=" + SolveResult.ThumbCurl.ToString("0.00") + "/"
                + SolveResult.IndexCurl.ToString("0.00") + "/" + SolveResult.MiddleCurl.ToString("0.00") + "/"
                + SolveResult.RingCurl.ToString("0.00") + "/" + SolveResult.LittleCurl.ToString("0.00")
                + "  align=" + (alignment.PositionErrorMeters * 1000f).ToString("0") + "mm"
                + "  angular=" + alignment.OrientationErrorDegrees.ToString("0") + "°"
                + "  roll=" + alignment.RollErrorDegrees.ToString("0") + "°");
#endif
        }

        private static float SmoothStep01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}
