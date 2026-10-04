using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DazPose.Motion;
using DazPose.Toys.Buttplug;
using DazPose.Performer;
using UnityEngine;

namespace DazPose.Toys
{
    /// <summary>Scene-level optional Intiface connection with capability-first scene commands.</summary>
    [DisallowMultipleComponent]
    public sealed class ToyControlService : MonoBehaviour
    {
        public const string DefaultServerAddress = "ws://127.0.0.1:12345";
        private const int ShutdownTimeoutMilliseconds = 10000;
        private const uint UnknownDurationMilliseconds = 50;

        [SerializeField] private string serverAddress = DefaultServerAddress;

        private sealed class PendingOutput
        {
            public float Value;
            public uint DurationMilliseconds;
            public long Revision;
            public long Owner;
            public long DispatchVersion;
            public int LifecycleGeneration;
        }

        private sealed class OutputState
        {
            public readonly object Gate = new object();
            public long Revision;
            public long Owner;
            public long DispatchVersion;
            public float DesiredValue;
            public bool HasDesiredValue;
            public PendingOutput Pending;
            public bool WorkerActive;
        }

        private sealed class ControlBinding
        {
            public PerformerControlSurface Surface;
            public ToyOutputCapability Capability;
            public int? Channel;
            public readonly Dictionary<ToyOutputBinding, long> Owners = new Dictionary<ToyOutputBinding, long>();
            public readonly Dictionary<ToyOutputBinding, float> LastValues = new Dictionary<ToyOutputBinding, float>();
            public readonly HashSet<ToyOutputBinding> Superseded = new HashSet<ToyOutputBinding>();
            public Action<float> Changed;
        }

        private readonly struct OutputOwner
        {
            public readonly ToyOutputBinding Binding;
            public readonly long Revision;
            public OutputOwner(ToyOutputBinding binding, long revision)
            { Binding = binding; Revision = revision; }
        }

        private readonly ConcurrentQueue<Action> _mainThreadEvents = new ConcurrentQueue<Action>();
        private readonly ConcurrentDictionary<ToyOutputBinding, OutputState> _outputStates =
            new ConcurrentDictionary<ToyOutputBinding, OutputState>();
        private readonly ConcurrentDictionary<ToyOutputBinding, string> _followerDiagnostics =
            new ConcurrentDictionary<ToyOutputBinding, string>();
        private readonly ConcurrentDictionary<ToyOutputBinding, string> _outputErrors =
            new ConcurrentDictionary<ToyOutputBinding, string>();
        private static readonly IReadOnlyList<ToyDevice> EmptyDevices = Array.AsReadOnly(Array.Empty<ToyDevice>());
        private readonly Dictionary<ToyOutputBinding, ToyMotionBinding> _motionBindings =
            new Dictionary<ToyOutputBinding, ToyMotionBinding>();
        private readonly Dictionary<ToyOutputBinding, ToyMotionFollower> _followers =
            new Dictionary<ToyOutputBinding, ToyMotionFollower>();
        private readonly List<ControlBinding> _controlBindings = new List<ControlBinding>();
        private readonly HashSet<ToyOutputBinding> _knownIntensityRoutes = new HashSet<ToyOutputBinding>();

        private IToyBackend _backend;
        private Task _shutdownTask;
        private MotionDriver _motionDriver;
        private bool _destroyed;
        private bool _connectRequested;
        private bool _connectInFlight;
        private bool _scanRequested;
        private bool _scanInFlight;
        private bool _scanStartedOnConnection;
        private float _nextReconnectAt;
        private float _retryDelaySeconds = 1f;
        private int _lifecycleGeneration;
        private long _nextOwner;
        private float _vibrationLevel;
        private float _oscillationLevel;
        private float _strokeMinimum;
        private float _strokeMaximum = 1f;
        private bool _strokeInverted;
        private string _lastOutputError = string.Empty;
        private int _outputsSent;

        public string ServerAddress
        {
            get => serverAddress;
            set => serverAddress = value ?? string.Empty;
        }

        internal ToyConnectionState ConnectionState => _backend != null
            ? _backend.ConnectionState : ToyConnectionState.Disconnected;
        internal string StatusMessage => _backend != null ? _backend.StatusMessage : "Disconnected.";
        internal string LastError => _backend != null ? _backend.LastError : string.Empty;
        internal bool IsConnected => _backend != null && _backend.IsConnected;
        internal bool IsScanning => _backend != null && _backend.IsScanning;
        internal IReadOnlyList<ToyDevice> ConnectedDevices => _backend != null ? _backend.Devices : EmptyDevices;
        internal float VibrationLevel => _vibrationLevel;
        internal float OscillationLevel => _oscillationLevel;
        public float StrokeMinimum => _strokeMinimum;
        public float StrokeMaximum => _strokeMaximum;
        public bool StrokeInverted => _strokeInverted;
        internal bool IsControlBound(PerformerControlSurface control) =>
            control != null && _controlBindings.Any(binding => binding.Surface == control);
        internal bool IsFollowingMotion => _motionDriver != null;
        internal MotionDriver FollowedMotionDriver => _motionDriver;
        internal int OutputsSent => Volatile.Read(ref _outputsSent);
        internal string LastOutputError => _lastOutputError;
        public bool CanVibrate => GetTargets(ToyOutputCapability.Vibrate, null).Count > 0;
        public bool CanOscillate => GetTargets(ToyOutputCapability.Oscillate, null).Count > 0;
        public bool CanStroke => GetMotionFeatures().Count > 0;
        internal int OutputCommandGeneration => Volatile.Read(ref _lifecycleGeneration);

        internal event Action<ToyConnectionState, string> StateChanged;
        internal event Action<ToyDevice> DeviceAdded;
        internal event Action<uint> DeviceRemoved;
        internal event Action<bool> ScanningChanged;

        private void Awake() => EnsureBackend();

        private void OnEnable()
        {
            _destroyed = false;
            EnsureBackend();
            if (_shutdownTask != null && _shutdownTask.IsCompleted) _shutdownTask = null;
        }

        private void Update()
        {
            while (_mainThreadEvents.TryDequeue(out Action callback))
            {
                try { callback(); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }

            if (_connectRequested && !_connectInFlight && !IsConnected
                && Time.realtimeSinceStartup >= _nextReconnectAt
                && (ConnectionState == ToyConnectionState.Disconnected || ConnectionState == ToyConnectionState.Faulted))
                _ = BeginConnectAttemptAsync();

            if (_scanRequested && IsConnected && !IsScanning && !_scanInFlight && !_scanStartedOnConnection)
                _ = StartScanAttemptAsync();

            ReconcileOutputRoutes();
            ReconcileMotionFollowers();
            ReconcileControlBindings();
        }

        private void OnDisable()
        {
            _connectRequested = false;
            _scanRequested = false;
            InvalidatePendingOutputs();
            StopFollowing(stopHardware: false);
            ClearControlBindings(sendZero: false);
            if (Application.isPlaying && !_destroyed) StartShutdown(disposeBackend: false);
        }

        private void OnApplicationQuit()
        {
            _connectRequested = false;
            _scanRequested = false;
            InvalidatePendingOutputs();
            StartShutdown(disposeBackend: false);
        }

        private void OnDestroy()
        {
            _destroyed = true;
            _connectRequested = false;
            _scanRequested = false;
            InvalidatePendingOutputs();
            ClearControlBindings(sendZero: false);
            StartShutdown(disposeBackend: true);
        }

        /// <summary>Connects once, then retries transient server loss with bounded backoff.</summary>
        public Task ConnectAsync()
        {
            if (IsConnected || _connectInFlight) return Task.CompletedTask;
            if (!Uri.TryCreate(serverAddress, UriKind.Absolute, out Uri address)
                || (address.Scheme != "ws" && address.Scheme != "wss"))
            {
                _connectRequested = false;
                return RunBackendOperation(backend => backend.ConnectAsync(serverAddress), "connect");
            }

            _connectRequested = true;
            _scanStartedOnConnection = false;
            _retryDelaySeconds = 1f;
            _nextReconnectAt = 0f;
            EnsureBackend();
            return BeginConnectAttemptAsync();
        }

        public async Task DisconnectAsync()
        {
            _connectRequested = false;
            _scanRequested = false;
            _nextReconnectAt = 0f;
            InvalidatePendingOutputs();
            StopFollowing(stopHardware: false);
            ClearControlBindings(sendZero: false);
            if (_backend == null) return;
            try { await _backend.DisconnectAsync(); }
            catch (Exception exception) { _lastOutputError = exception.Message; }
        }

        /// <summary>Sets discovery intent. It starts immediately when connected and resumes after reconnect.</summary>
        public Task StartScanningAsync()
        {
            _scanRequested = true;
            _scanStartedOnConnection = false;
            if (!IsConnected) return Task.CompletedTask;
            return StartScanAttemptAsync();
        }

        public Task StopScanningAsync()
        {
            _scanRequested = false;
            _scanStartedOnConnection = false;
            return RunBackendOperation(backend => backend.StopScanningAsync(), "stop scanning");
        }

        public void Connect() => ObserveTask(ConnectAsync(), "connect");
        public void Disconnect() => ObserveTask(DisconnectAsync(), "disconnect");
        public void StartScanning() => ObserveTask(StartScanningAsync(), "start scanning");
        public void StopScanning() => ObserveTask(StopScanningAsync(), "stop scanning");

        public void Vibrate(float level) => SetIntensity(ToyOutputCapability.Vibrate, level, null);
        public void Vibrate(float level, int channel) => SetIntensity(ToyOutputCapability.Vibrate, level, channel);
        public void Oscillate(float level) => SetIntensity(ToyOutputCapability.Oscillate, level, null);
        public void Oscillate(float level, int channel) => SetIntensity(ToyOutputCapability.Oscillate, level, channel);

        public bool HasVibrationChannel(int channel) => HasChannel(ToyOutputCapability.Vibrate, channel);
        public bool HasOscillationChannel(int channel) => HasChannel(ToyOutputCapability.Oscillate, channel);

        public void RampVibration(float target, float seconds) => ObserveEffect(RampVibrationAsync(target, seconds), "vibration ramp");
        public void RampVibration(float target, float seconds, int channel) =>
            ObserveEffect(RampVibrationAsync(target, seconds, channel), "vibration ramp");
        public void RampOscillation(float target, float seconds) => ObserveEffect(RampOscillationAsync(target, seconds), "oscillation ramp");
        public void RampOscillation(float target, float seconds, int channel) =>
            ObserveEffect(RampOscillationAsync(target, seconds, channel), "oscillation ramp");

        public Awaitable RampVibrationAsync(float target, float seconds) =>
            RampIntensityAsync(ToyOutputCapability.Vibrate, target, seconds, null);
        public Awaitable RampVibrationAsync(float target, float seconds, int channel) =>
            RampIntensityAsync(ToyOutputCapability.Vibrate, target, seconds, channel);
        public Awaitable RampOscillationAsync(float target, float seconds) =>
            RampIntensityAsync(ToyOutputCapability.Oscillate, target, seconds, null);
        public Awaitable RampOscillationAsync(float target, float seconds, int channel) =>
            RampIntensityAsync(ToyOutputCapability.Oscillate, target, seconds, channel);

        /// <summary>Maps a normalized scene position to the current stroke range and inversion.</summary>
        public void SetStrokeLimits(float minimum, float maximum)
        {
            ValidateUnit(minimum, nameof(minimum));
            ValidateUnit(maximum, nameof(maximum));
            if (minimum > maximum) throw new ArgumentException("Minimum stroke limit must not exceed maximum.");
            _strokeMinimum = minimum;
            _strokeMaximum = maximum;
            UpdateFollowerMapping();
            foreach (ToyMotionFollower follower in _followers.Values) follower.Resynchronize();
        }

        public void SetStrokeInverted(bool inverted)
        {
            _strokeInverted = inverted;
            UpdateFollowerMapping();
            foreach (ToyMotionFollower follower in _followers.Values) follower.Resynchronize();
        }

        public void MoveTo(float position, float seconds) => ObserveEffect(MoveToAsync(position, seconds), "stroke move");

        public async Awaitable MoveToAsync(float position, float seconds)
        {
            ValidateUnit(position, nameof(position));
            ValidateDuration(seconds, nameof(seconds));
            float sourcePosition = _motionDriver != null
                ? _motionDriver.CurrentSample.Position01 : 0f;
            StopFollowing(stopHardware: false);
            List<ToyOutputBinding> targets = GetMotionTargets();
            InvalidateMotionOwners(targets);
            SuppressControlOutputs(targets);
            float mapped = MapStroke(position);
            var transitions = new List<OutputTransition>();
            var owners = new List<OutputOwner>();
            foreach (ToyOutputBinding target in targets)
            {
                long owner = NextOwner();
                float initial = GetDesired(target, MapStroke(sourcePosition));
                long revision = BeginOwner(target, owner, initial);
                float from = initial;
                owners.Add(new OutputOwner(target, revision));
                if (target.Capability == ToyOutputCapability.HwPositionWithDuration && seconds > 0f)
                {
                    uint duration = ToMilliseconds(seconds);
                    QueueOwned(target, revision, mapped, duration);
                }
                else
                {
                    transitions.Add(new OutputTransition(target, revision, from, mapped));
                }
            }

            await AnimateMoveAsync(transitions, owners, seconds);
        }

        /// <summary>Uses the existing scene MotionDriver and FunscriptPlayback; no second timeline is created.</summary>
        public void Play(FunscriptMotionProgram program, bool loop = false)
        {
            if (program == null) throw new ArgumentNullException(nameof(program));
            MotionDriver driver = _motionDriver != null ? _motionDriver : FindAnyObjectByType<MotionDriver>();
            if (driver == null) throw new InvalidOperationException("No scene MotionDriver is available.");
            driver.FunscriptProgram = program;
            driver.Loop = loop;
            driver.SourceMode = MotionSourceMode.Funscript;
            Follow(driver);
            driver.RestartMotion();
        }

        public void Follow(MotionDriver driver)
        {
            if (_motionDriver == driver) return;
            StopFollowing(stopHardware: false);
            InvalidateMotionOwners(GetMotionTargets());
            _motionDriver = driver;
            ReconcileMotionFollowers();
        }

        public void StopFollowing() => StopFollowing(stopHardware: true);

        public void BindVibration(PerformerControlSurface control) => BindControl(control, ToyOutputCapability.Vibrate, null);
        public void BindVibration(PerformerControlSurface control, int channel) =>
            BindControl(control, ToyOutputCapability.Vibrate, channel);
        public void BindOscillation(PerformerControlSurface control) => BindControl(control, ToyOutputCapability.Oscillate, null);
        public void BindOscillation(PerformerControlSurface control, int channel) =>
            BindControl(control, ToyOutputCapability.Oscillate, channel);

        public void UnbindControl(PerformerControlSurface control)
        {
            if (control == null) return;
            ControlBinding binding = _controlBindings.FirstOrDefault(candidate => candidate.Surface == control);
            if (binding == null) return;
            RemoveControlBinding(binding, sendZero: true);
        }

        /// <summary>Invalidates every active effect and attempts the backend's broad stop.</summary>
        public void Stop() => ObserveTask(StopAsync(), "stop all");

        public async Task StopAsync()
        {
            InvalidatePendingOutputs();
            StopFollowing(stopHardware: false);
            ClearControlBindings(sendZero: false);
            _vibrationLevel = 0f;
            _oscillationLevel = 0f;
            if (_backend != null)
            {
                try { await _backend.StopAllAsync(); }
                catch (Exception exception) { _lastOutputError = "stop all: " + exception.Message; }
            }
        }

        internal string GetFollowerDiagnostic(ToyOutputBinding binding) =>
            _followerDiagnostics.TryGetValue(binding, out string value) ? value : "inactive";

        internal string GetOutputDiagnostic(ToyOutputBinding binding)
        {
            if (!_outputStates.TryGetValue(binding, out OutputState state)) return "idle";
            lock (state.Gate)
                return "requested=" + state.DesiredValue.ToString("P0") + "; queued="
                    + (state.Pending != null ? "yes" : "no") + "; output="
                    + (_outputErrors.TryGetValue(binding, out string error) ? error : "ready");
        }

        private async Task BeginConnectAttemptAsync()
        {
            if (_backend == null || _connectInFlight || !_connectRequested) return;
            _connectInFlight = true;
            try
            {
                await _backend.ConnectAsync(serverAddress);
                if (IsConnected)
                {
                    _scanStartedOnConnection = false;
                    _retryDelaySeconds = 1f;
                    _nextReconnectAt = 0f;
                    if (_scanRequested) await StartScanAttemptAsync();
                }
                else if (_connectRequested && IsRetryableConnectionError()) ScheduleReconnect();
            }
            catch (Exception exception)
            {
                _lastOutputError = exception.Message;
                if (_connectRequested && IsRetryableConnectionError()) ScheduleReconnect();
            }
            finally { _connectInFlight = false; }
        }

        private bool IsRetryableConnectionError()
        {
            if (!Uri.TryCreate(serverAddress, UriKind.Absolute, out Uri address)
                || (address.Scheme != "ws" && address.Scheme != "wss")) return false;
            string message = (LastError + " " + StatusMessage).ToLowerInvariant();
            return !message.Contains("valid ws://") && !message.Contains("incompatible")
                && !message.Contains("unsupported protocol") && !message.Contains("handshake version")
                && !message.Contains("version mismatch");
        }

        private void ScheduleReconnect()
        {
            float baseDelay = Mathf.Min(30f, Mathf.Max(1f, _retryDelaySeconds));
            _nextReconnectAt = Time.realtimeSinceStartup + baseDelay + UnityEngine.Random.Range(0f, 0.35f);
            _retryDelaySeconds = Mathf.Min(30f, baseDelay * 2f);
        }

        private async Task StartScanAttemptAsync()
        {
            if (_backend == null || !IsConnected || !_scanRequested || _scanInFlight) return;
            _scanInFlight = true;
            try { await _backend.StartScanningAsync(); }
            catch (Exception exception) { _lastOutputError = exception.Message; }
            finally { _scanInFlight = false; _scanStartedOnConnection = true; }
        }

        private void SetIntensity(ToyOutputCapability capability, float value, int? channel)
        {
            ValidateUnit(value, nameof(value));
            if (channel.HasValue && channel.Value < 0) throw new ArgumentOutOfRangeException(nameof(channel));
            value = Mathf.Clamp01(value);
            if (capability == ToyOutputCapability.Vibrate) _vibrationLevel = value;
            else _oscillationLevel = value;
            List<ToyOutputBinding> targets = GetTargets(capability, channel);
            SuppressControlOutputs(targets);
            foreach (ToyOutputBinding target in targets) QueueManual(target, value, 0);
        }

        private async Awaitable RampIntensityAsync(ToyOutputCapability capability, float target,
            float seconds, int? channel)
        {
            ValidateUnit(target, nameof(target));
            ValidateDuration(seconds, nameof(seconds));
            if (channel.HasValue && channel.Value < 0) throw new ArgumentOutOfRangeException(nameof(channel));
            target = Mathf.Clamp01(target);
            if (capability == ToyOutputCapability.Vibrate) _vibrationLevel = target;
            else _oscillationLevel = target;
            List<ToyOutputBinding> outputs = GetTargets(capability, channel);
            SuppressControlOutputs(outputs);
            var transitions = new List<OutputTransition>();
            foreach (ToyOutputBinding output in outputs)
            {
                long owner = NextOwner();
                float start = GetDesired(output, 0f);
                long revision = BeginOwner(output, owner, start);
                transitions.Add(new OutputTransition(output, revision, start, target));
            }
            await AnimateTransitionsAsync(transitions, seconds);
        }

        private async Awaitable AnimateTransitionsAsync(List<OutputTransition> transitions, float seconds)
        {
            if (transitions.Count == 0 && seconds <= 0f) return;
            bool waitWithoutOutputs = transitions.Count == 0;
            if (seconds <= 0f)
            {
                foreach (OutputTransition transition in transitions)
                    QueueOwned(transition.Binding, transition.Revision, transition.Target, 0);
                return;
            }

            float elapsed = 0f;
            while (elapsed < seconds && (waitWithoutOutputs || AnyTransitionStillOwned(transitions)))
            {
                await Awaitable.NextFrameAsync();
                elapsed = Mathf.Min(seconds, elapsed + Mathf.Max(0f, Time.deltaTime));
                float progress = Mathf.Clamp01(elapsed / seconds);
                foreach (OutputTransition transition in transitions)
                {
                    if (!IsOwner(transition.Binding, transition.Revision)) continue;
                    float value = Mathf.LerpUnclamped(transition.Start, transition.Target, progress);
                    QueueOwned(transition.Binding, transition.Revision, value, 0);
                }
                if (Time.deltaTime <= 0f) await Awaitable.NextFrameAsync();
            }
        }

        private async Awaitable AnimateMoveAsync(List<OutputTransition> transitions,
            List<OutputOwner> owners, float seconds)
        {
            if (owners.Count == 0 && seconds <= 0f) return;
            bool waitWithoutOutputs = owners.Count == 0;
            if (seconds <= 0f)
            {
                foreach (OutputTransition transition in transitions)
                    QueueOwned(transition.Binding, transition.Revision, transition.Target, 0);
                return;
            }

            float elapsed = 0f;
            while (elapsed < seconds && (waitWithoutOutputs || AnyOutputStillOwned(owners)))
            {
                await Awaitable.NextFrameAsync();
                elapsed = Mathf.Min(seconds, elapsed + Mathf.Max(0f, Time.deltaTime));
                float progress = Mathf.Clamp01(elapsed / seconds);
                foreach (OutputTransition transition in transitions)
                {
                    if (!IsOwner(transition.Binding, transition.Revision)) continue;
                    QueueOwned(transition.Binding, transition.Revision,
                        Mathf.LerpUnclamped(transition.Start, transition.Target, progress), 0);
                }
                if (Time.deltaTime <= 0f) await Awaitable.NextFrameAsync();
            }
        }

        private bool AnyOutputStillOwned(List<OutputOwner> owners)
        {
            foreach (OutputOwner owner in owners)
                if (IsOwner(owner.Binding, owner.Revision)) return true;
            return false;
        }

        private readonly struct OutputTransition
        {
            public readonly ToyOutputBinding Binding;
            public readonly long Revision;
            public readonly float Start;
            public readonly float Target;
            public OutputTransition(ToyOutputBinding binding, long revision, float start, float target)
            { Binding = binding; Revision = revision; Start = start; Target = target; }
        }

        private bool AnyTransitionStillOwned(List<OutputTransition> transitions)
        {
            foreach (OutputTransition transition in transitions)
                if (IsOwner(transition.Binding, transition.Revision)) return true;
            return false;
        }

        private void QueueManual(ToyOutputBinding binding, float value, uint durationMilliseconds)
        {
            long owner = NextOwner();
            long revision = BeginOwner(binding, owner, value);
            QueueOwned(binding, revision, value, durationMilliseconds);
        }

        private long BeginOwner(ToyOutputBinding binding, long owner, float initialValue)
        {
            OutputState state = _outputStates.GetOrAdd(binding, _ => new OutputState());
            lock (state.Gate)
            {
                state.Revision++;
                state.Owner = owner;
                state.DispatchVersion++;
                state.DesiredValue = initialValue;
                state.HasDesiredValue = true;
                state.Pending = null;
                return state.Revision;
            }
        }

        private float GetDesired(ToyOutputBinding binding, float fallback)
        {
            if (!_outputStates.TryGetValue(binding, out OutputState state)) return fallback;
            lock (state.Gate) return state.HasDesiredValue ? state.DesiredValue : fallback;
        }

        private bool IsOwner(ToyOutputBinding binding, long revision)
        {
            if (!_outputStates.TryGetValue(binding, out OutputState state)) return false;
            lock (state.Gate) return state.Revision == revision;
        }

        private static bool IsDispatchCurrent(OutputState state, long revision, long dispatchVersion)
        {
            lock (state.Gate)
                return state.Revision == revision && state.DispatchVersion == dispatchVersion;
        }

        private void QueueOwned(ToyOutputBinding binding, long revision, float value, uint durationMilliseconds)
        {
            OutputState state = _outputStates.GetOrAdd(binding, _ => new OutputState());
            bool startWorker = false;
            lock (state.Gate)
            {
                if (state.Revision != revision) return;
                state.DesiredValue = value;
                state.HasDesiredValue = true;
                long dispatchVersion = ++state.DispatchVersion;
                state.Pending = new PendingOutput
                {
                    Value = value,
                    DurationMilliseconds = durationMilliseconds,
                    Revision = revision,
                    Owner = state.Owner,
                    DispatchVersion = dispatchVersion,
                    LifecycleGeneration = OutputCommandGeneration
                };
                if (!state.WorkerActive)
                {
                    state.WorkerActive = true;
                    startWorker = true;
                }
            }
            if (startWorker) _ = DrainOutputAsync(binding, state);
        }

        private async Task DrainOutputAsync(ToyOutputBinding binding, OutputState state)
        {
            while (true)
            {
                PendingOutput pending;
                lock (state.Gate)
                {
                    if (state.Pending == null)
                    {
                        state.WorkerActive = false;
                        return;
                    }
                    pending = state.Pending;
                    state.Pending = null;
                }

                if (!IsPendingCurrent(state, pending)) continue;
                try
                {
                    bool sent = await DispatchOutputAsync(binding, pending.Value,
                        pending.DurationMilliseconds, pending.LifecycleGeneration, state,
                        pending.Revision, pending.DispatchVersion);
                    if (sent)
                    {
                        Interlocked.Increment(ref _outputsSent);
                        _outputErrors.TryRemove(binding, out _);
                    }
                }
                catch (Exception exception)
                {
                    _lastOutputError = binding + ": " + exception.Message;
                    _outputErrors[binding] = exception.Message;
                }
            }
        }

        private static bool IsPendingCurrent(OutputState state, PendingOutput pending)
        {
            lock (state.Gate)
                return state.Revision == pending.Revision && state.Owner == pending.Owner
                    && state.DispatchVersion == pending.DispatchVersion;
        }

        internal async Task<bool> DispatchOutputAsync(ToyOutputBinding binding, float normalized,
            uint durationMilliseconds, int commandGeneration)
        {
            return await DispatchOutputAsync(binding, normalized, durationMilliseconds,
                commandGeneration, null, 0, 0);
        }

        private async Task<bool> DispatchOutputAsync(ToyOutputBinding binding, float normalized,
            uint durationMilliseconds, int commandGeneration, OutputState state, long revision,
            long dispatchVersion)
        {
            if (commandGeneration != OutputCommandGeneration || _backend == null || !_backend.IsConnected) return false;
            if (state != null && !IsDispatchCurrent(state, revision, dispatchVersion)) return false;
            if (!FeatureSupports(binding)) return false;
            if (float.IsNaN(normalized) || float.IsInfinity(normalized)) return false;
            if (binding.Capability == ToyOutputCapability.HwPositionWithDuration)
            {
                ToyOutputRange range = FindRange(binding);
                if (range.HasDurationRange)
                    durationMilliseconds = (uint)Mathf.Clamp((int)Math.Min(int.MaxValue, durationMilliseconds),
                        range.MinimumDurationMilliseconds, range.MaximumDurationMilliseconds);
                else if (durationMilliseconds == 0) durationMilliseconds = UnknownDurationMilliseconds;
            }
            Func<bool> stillCurrent = () => commandGeneration == OutputCommandGeneration
                && (state == null || IsDispatchCurrent(state, revision, dispatchVersion));
            await _backend.SendOutputAsync(binding, Mathf.Clamp01(normalized), durationMilliseconds, stillCurrent);
            return commandGeneration == OutputCommandGeneration
                && (state == null || IsDispatchCurrent(state, revision, dispatchVersion));
        }

        internal long BeginFollowerOwnership(ToyOutputBinding binding, float initialValue) =>
            BeginOwner(binding, NextOwner(), initialValue);

        internal void ReleaseFollowerOwnership(ToyOutputBinding binding, long revision)
        {
            if (!_outputStates.TryGetValue(binding, out OutputState state)) return;
            lock (state.Gate)
            {
                if (state.Revision != revision) return;
                state.Revision++;
                state.Owner = NextOwner();
                state.DispatchVersion++;
                state.Pending = null;
            }
        }

        internal async Task<bool> DispatchFollowerOutputAsync(ToyOutputBinding binding, long revision,
            float normalized, uint durationMilliseconds, int commandGeneration)
        {
            if (!_outputStates.TryGetValue(binding, out OutputState state)) return false;
            long dispatchVersion;
            lock (state.Gate)
            {
                if (state.Revision != revision) return false;
                state.DesiredValue = normalized;
                state.HasDesiredValue = true;
                dispatchVersion = ++state.DispatchVersion;
            }
            return await DispatchOutputAsync(binding, normalized, durationMilliseconds,
                commandGeneration, state, revision, dispatchVersion);
        }

        internal void SetFollowerDiagnostic(ToyOutputBinding binding, string diagnostic) =>
            _followerDiagnostics[binding] = diagnostic ?? string.Empty;

        internal ToyOutputBinding ResolveMotionOutput(ToyMotionBinding binding)
        {
            var position = new ToyOutputBinding(binding.Output.DeviceIndex,
                binding.Output.FeatureIndex, ToyOutputCapability.Position);
            var timed = new ToyOutputBinding(binding.Output.DeviceIndex,
                binding.Output.FeatureIndex, ToyOutputCapability.HwPositionWithDuration);
            bool supportsPosition = FeatureSupports(position);
            bool supportsTimed = FeatureSupports(timed);
            if (binding.Strategy == ToyMotionStrategy.Position && supportsPosition) return position;
            if (binding.Strategy == ToyMotionStrategy.HwPositionWithDuration && supportsTimed) return timed;
            if (_motionDriver != null && _motionDriver.TryGetCurrentTargetSegment(out _) && supportsTimed) return timed;
            if (supportsPosition) return position;
            return timed;
        }

        private void ReconcileMotionFollowers()
        {
            if (_motionDriver == null)
            {
                StopFollowing(stopHardware: false);
                return;
            }

            var available = new HashSet<ToyOutputBinding>();
            foreach (ToyDevice device in ConnectedDevices)
            foreach (ToyFeature feature in device.Features)
            {
                bool supportsPosition = feature.Supports(ToyOutputCapability.Position);
                bool supportsTimed = feature.Supports(ToyOutputCapability.HwPositionWithDuration);
                if (!supportsPosition && !supportsTimed) continue;
                var key = new ToyOutputBinding(device.DeviceIndex, feature.FeatureIndex,
                    supportsPosition ? ToyOutputCapability.Position : ToyOutputCapability.HwPositionWithDuration);
                available.Add(key);
                if (!_motionBindings.ContainsKey(key))
                    _motionBindings.Add(key, new ToyMotionBinding(key, ToyMotionStrategy.Auto,
                        _strokeInverted, _strokeMinimum, _strokeMaximum));
            }

            foreach (ToyOutputBinding stale in _followers.Keys.Where(key => !available.Contains(key)).ToArray())
            {
                _followers[stale].Dispose(stopHardware: false);
                _followers.Remove(stale);
                _motionBindings.Remove(stale);
            }
            foreach (ToyOutputBinding key in available)
            {
                if (_followers.ContainsKey(key)) continue;
                ToyMotionBinding settings = _motionBindings[key];
                var follower = new ToyMotionFollower(this, _motionDriver, settings,
                    ResolveMotionOutput(settings), GetDeviceTimingGap(key.DeviceIndex));
                _followers.Add(key, follower);
            }
        }

        private void UpdateFollowerMapping()
        {
            foreach (ToyMotionBinding binding in _motionBindings.Values)
            {
                binding.Minimum = _strokeMinimum;
                binding.Maximum = _strokeMaximum;
                binding.Invert = _strokeInverted;
            }
        }

        private void StopFollowing(bool stopHardware)
        {
            foreach (KeyValuePair<ToyOutputBinding, ToyMotionFollower> pair in _followers)
                pair.Value.Dispose(stopHardware);
            _followers.Clear();
            _motionBindings.Clear();
            _motionDriver = null;
        }

        private void InvalidateMotionOwners(IEnumerable<ToyOutputBinding> routes)
        {
            var features = new HashSet<Tuple<uint, uint>>();
            foreach (ToyOutputBinding route in routes)
                features.Add(Tuple.Create(route.DeviceIndex, route.FeatureIndex));
            foreach (Tuple<uint, uint> feature in features)
            foreach (ToyOutputCapability capability in new[]
                { ToyOutputCapability.Position, ToyOutputCapability.HwPositionWithDuration })
            {
                var binding = new ToyOutputBinding(feature.Item1, feature.Item2, capability);
                if (!_outputStates.TryGetValue(binding, out OutputState state)) continue;
                lock (state.Gate)
                {
                    state.Revision++;
                    state.Owner = NextOwner();
                    state.DispatchVersion++;
                    state.Pending = null;
                }
            }
        }

        private List<ToyOutputBinding> GetTargets(ToyOutputCapability capability, int? channel)
        {
            var targets = new List<ToyOutputBinding>();
            foreach (ToyDevice device in ConnectedDevices.OrderBy(device => device.DeviceIndex))
            {
                List<ToyFeature> features = device.Features
                    .Where(feature => feature.Supports(capability))
                    .OrderBy(feature => feature.FeatureIndex).ToList();
                if (channel.HasValue)
                {
                    if (channel.Value < features.Count)
                        targets.Add(new ToyOutputBinding(device.DeviceIndex, features[channel.Value].FeatureIndex, capability));
                }
                else
                {
                    foreach (ToyFeature feature in features)
                        targets.Add(new ToyOutputBinding(device.DeviceIndex, feature.FeatureIndex, capability));
                }
            }
            return targets;
        }

        private List<ToyOutputBinding> GetMotionTargets()
        {
            var targets = new List<ToyOutputBinding>();
            foreach (ToyDevice device in ConnectedDevices.OrderBy(device => device.DeviceIndex))
            foreach (ToyFeature feature in device.Features.OrderBy(feature => feature.FeatureIndex))
            {
                if (feature.Supports(ToyOutputCapability.HwPositionWithDuration))
                    targets.Add(new ToyOutputBinding(device.DeviceIndex, feature.FeatureIndex, ToyOutputCapability.HwPositionWithDuration));
                else if (feature.Supports(ToyOutputCapability.Position))
                    targets.Add(new ToyOutputBinding(device.DeviceIndex, feature.FeatureIndex, ToyOutputCapability.Position));
            }
            return targets;
        }

        private List<ToyFeature> GetMotionFeatures()
        {
            var features = new List<ToyFeature>();
            foreach (ToyDevice device in ConnectedDevices)
                features.AddRange(device.Features.Where(feature => feature.Supports(ToyOutputCapability.Position)
                    || feature.Supports(ToyOutputCapability.HwPositionWithDuration)));
            return features;
        }

        private bool HasChannel(ToyOutputCapability capability, int channel)
        {
            if (channel < 0) return false;
            return ConnectedDevices.Any(device => device.Features.Count(feature => feature.Supports(capability)) > channel);
        }

        private bool FeatureSupports(ToyOutputBinding binding)
        {
            foreach (ToyDevice device in ConnectedDevices)
            {
                if (device.DeviceIndex != binding.DeviceIndex) continue;
                foreach (ToyFeature feature in device.Features)
                    if (feature.FeatureIndex == binding.FeatureIndex) return feature.Supports(binding.Capability);
                return false;
            }
            return false;
        }

        private ToyOutputRange FindRange(ToyOutputBinding binding)
        {
            foreach (ToyDevice device in ConnectedDevices)
            {
                if (device.DeviceIndex != binding.DeviceIndex) continue;
                foreach (ToyFeature feature in device.Features)
                    if (feature.FeatureIndex == binding.FeatureIndex && feature.TryGetOutput(binding.Capability, out ToyOutputRange range))
                        return range;
            }
            return default;
        }

        private uint GetDeviceTimingGap(uint deviceIndex)
        {
            foreach (ToyDevice device in ConnectedDevices)
                if (device.DeviceIndex == deviceIndex) return device.MessageTimingGapMilliseconds;
            return 0;
        }

        private float MapStroke(float position)
        {
            float normalized = _strokeInverted ? 1f - Mathf.Clamp01(position) : Mathf.Clamp01(position);
            return Mathf.Lerp(_strokeMinimum, _strokeMaximum, normalized);
        }

        private void ReconcileOutputRoutes()
        {
            var current = new HashSet<ToyOutputBinding>();
            current.UnionWith(GetTargets(ToyOutputCapability.Vibrate, null));
            current.UnionWith(GetTargets(ToyOutputCapability.Oscillate, null));
            foreach (ToyOutputBinding removed in _knownIntensityRoutes.Where(binding => !current.Contains(binding)).ToArray())
            {
                if (FeatureExists(removed.DeviceIndex, removed.FeatureIndex)) QueueManual(removed, 0f, 0);
                _outputStates.TryRemove(removed, out _);
            }
            _knownIntensityRoutes.Clear();
            _knownIntensityRoutes.UnionWith(current);
        }

        private bool FeatureExists(uint deviceIndex, uint featureIndex) => ConnectedDevices.Any(device =>
            device.DeviceIndex == deviceIndex && device.Features.Any(feature => feature.FeatureIndex == featureIndex));

        private void BindControl(PerformerControlSurface control, ToyOutputCapability capability, int? channel)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (channel.HasValue && channel.Value < 0) throw new ArgumentOutOfRangeException(nameof(channel));
            ControlBinding prior = _controlBindings.FirstOrDefault(binding => binding.Surface == control);
            if (prior != null) RemoveControlBinding(prior, sendZero: true);
            SuppressControlOutputs(GetTargets(capability, channel));
            var binding = new ControlBinding { Surface = control, Capability = capability, Channel = channel };
            binding.Changed = _ => UpdateControlBinding(binding);
            _controlBindings.Add(binding);
            control.ValueChanged += binding.Changed;
            UpdateControlBinding(binding);
        }

        private void UpdateControlBinding(ControlBinding binding)
        {
            if (binding.Surface == null) return;
            List<ToyOutputBinding> targets = GetTargets(binding.Capability, binding.Channel);
            var current = new HashSet<ToyOutputBinding>(targets);
            if (targets.Any(target => !binding.Superseded.Contains(target)))
            {
                if (binding.Capability == ToyOutputCapability.Vibrate) _vibrationLevel = binding.Surface.Value01;
                else _oscillationLevel = binding.Surface.Value01;
            }
            foreach (ToyOutputBinding old in binding.Owners.Keys.Where(output => !current.Contains(output)).ToArray())
            {
                long revision = binding.Owners[old];
                if (IsOwner(old, revision)) QueueOwned(old, revision, 0f, 0);
                binding.Owners.Remove(old);
                binding.LastValues.Remove(old);
            }
            foreach (ToyOutputBinding target in targets)
            {
                if (binding.Superseded.Contains(target)) continue;
                if (!binding.Owners.TryGetValue(target, out long revision) || !IsOwner(target, revision))
                {
                    revision = BeginOwner(target, NextOwner(), binding.Surface.Value01);
                    binding.Owners[target] = revision;
                    binding.LastValues.Remove(target);
                }
                if (binding.LastValues.TryGetValue(target, out float priorValue)
                    && Mathf.Approximately(priorValue, binding.Surface.Value01)) continue;
                binding.LastValues[target] = binding.Surface.Value01;
                QueueOwned(target, revision, binding.Surface.Value01, 0);
            }
        }

        private void SuppressControlOutputs(List<ToyOutputBinding> targets)
        {
            if (targets.Count == 0) return;
            var addressed = new HashSet<ToyOutputBinding>(targets);
            foreach (ControlBinding control in _controlBindings)
            foreach (ToyOutputBinding target in control.Owners.Keys.Where(addressed.Contains).ToArray())
            {
                long revision = control.Owners[target];
                control.Superseded.Add(target);
                control.Owners.Remove(target);
                control.LastValues.Remove(target);
                if (IsOwner(target, revision)) QueueManual(target, 0f, 0);
            }
        }

        private void RemoveControlBinding(ControlBinding binding, bool sendZero)
        {
            binding.Surface.ValueChanged -= binding.Changed;
            _controlBindings.Remove(binding);
            if (!sendZero) return;
            foreach (KeyValuePair<ToyOutputBinding, long> owned in binding.Owners.ToArray())
                if (IsOwner(owned.Key, owned.Value)) QueueManual(owned.Key, 0f, 0);
            binding.Owners.Clear();
            binding.LastValues.Clear();
        }

        private void ReconcileControlBindings()
        {
            foreach (ControlBinding binding in _controlBindings.ToArray())
            {
                if (binding.Surface == null) { RemoveControlBinding(binding, sendZero: false); continue; }
                UpdateControlBinding(binding);
            }
        }

        private void ClearControlBindings(bool sendZero)
        {
            foreach (ControlBinding binding in _controlBindings.ToArray()) RemoveControlBinding(binding, sendZero);
        }

        private Task RunBackendOperation(Func<IToyBackend, Task> operation, string name)
        {
            EnsureBackend();
            if (_backend == null) return Task.CompletedTask;
            try { return operation(_backend) ?? Task.CompletedTask; }
            catch (Exception exception) { _lastOutputError = name + ": " + exception.Message; return Task.CompletedTask; }
        }

        private void InvalidatePendingOutputs()
        {
            Interlocked.Increment(ref _lifecycleGeneration);
            foreach (OutputState state in _outputStates.Values)
                lock (state.Gate)
                {
                    state.Revision++;
                    state.Owner = NextOwner();
                    state.DispatchVersion++;
                    state.DesiredValue = 0f;
                    state.HasDesiredValue = true;
                    state.Pending = null;
                }
        }

        private long NextOwner() => Interlocked.Increment(ref _nextOwner);

        private void EnsureBackend()
        {
            if (_backend != null || _destroyed) return;
            AttachBackend(new ButtplugToyBackend());
        }

        private void AttachBackend(IToyBackend backend)
        {
            _backend = backend;
            _backend.StateChanged += OnBackendStateChanged;
            _backend.DeviceAdded += OnBackendDeviceAdded;
            _backend.DeviceRemoved += OnBackendDeviceRemoved;
            _backend.ScanningChanged += OnBackendScanningChanged;
        }

        internal void SetBackendForAcceptance(IToyBackend backend)
        {
            if (backend == null) throw new ArgumentNullException(nameof(backend));
            if (_backend != null)
            {
                _backend.StateChanged -= OnBackendStateChanged;
                _backend.DeviceAdded -= OnBackendDeviceAdded;
                _backend.DeviceRemoved -= OnBackendDeviceRemoved;
                _backend.ScanningChanged -= OnBackendScanningChanged;
                _backend.Dispose();
            }
            AttachBackend(backend);
            ReconcileMotionFollowers();
        }

        private void ObserveEffect(Awaitable effect, string name) => _ = ObserveEffectAsync(effect, name);

        private async Awaitable ObserveEffectAsync(Awaitable effect, string name)
        {
            try { await effect; }
            catch (Exception exception) { _lastOutputError = name + ": " + exception.Message; }
        }

        private void ObserveTask(Task task, string name) => _ = ObserveTaskAsync(task, name);

        private async Task ObserveTaskAsync(Task task, string name)
        {
            try { await task; }
            catch (Exception exception) { _lastOutputError = name + ": " + exception.Message; }
        }

        private void StartShutdown(bool disposeBackend)
        {
            IToyBackend backend = _backend;
            if (backend == null) return;
            if (_shutdownTask != null && !_shutdownTask.IsCompleted)
            {
                if (disposeBackend) _ = DisposeAfterShutdownAsync(_shutdownTask, backend);
                return;
            }
            _shutdownTask = ShutdownAsync(backend, disposeBackend);
        }

        private static async Task ShutdownAsync(IToyBackend backend, bool disposeBackend)
        {
            try
            {
                Task disconnect = backend.DisconnectAsync();
                Task timeout = Task.Delay(ShutdownTimeoutMilliseconds);
                if (await Task.WhenAny(disconnect, timeout) != disconnect)
                {
                    ObserveAbandonedTask(disconnect);
                    Debug.LogWarning("Toy shutdown timed out after " + ShutdownTimeoutMilliseconds + " ms.");
                }
                else await disconnect;
            }
            catch (Exception exception) { Debug.LogWarning("Toy shutdown failed: " + exception.Message); }
            finally { if (disposeBackend) backend.Dispose(); }
        }

        private static async Task DisposeAfterShutdownAsync(Task shutdown, IToyBackend backend)
        {
            try { await shutdown; }
            catch { }
            backend.Dispose();
        }

        private static void ObserveAbandonedTask(Task task)
        {
            task.ContinueWith(completed => { if (completed.IsFaulted) _ = completed.Exception; },
                TaskContinuationOptions.ExecuteSynchronously);
        }

        private void OnBackendStateChanged(ToyConnectionState state, string message) =>
            _mainThreadEvents.Enqueue(() =>
            {
                _scanStartedOnConnection = false;
                if (state != ToyConnectionState.Connected)
                {
                    InvalidatePendingOutputs();
                }
                RaiseStateChanged(state, message);
            });
        private void OnBackendDeviceAdded(ToyDevice device) =>
            _mainThreadEvents.Enqueue(() => RaiseDeviceAdded(device));
        private void OnBackendDeviceRemoved(uint index) =>
            _mainThreadEvents.Enqueue(() => RaiseDeviceRemoved(index));
        private void OnBackendScanningChanged(bool scanning) =>
            _mainThreadEvents.Enqueue(() => RaiseScanningChanged(scanning));

        private void RaiseStateChanged(ToyConnectionState state, string message)
        {
            Action<ToyConnectionState, string> handlers = StateChanged;
            if (handlers == null) return;
            foreach (Delegate callback in handlers.GetInvocationList())
                try { ((Action<ToyConnectionState, string>)callback)(state, message); }
                catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void RaiseDeviceAdded(ToyDevice device)
        {
            Action<ToyDevice> handlers = DeviceAdded;
            if (handlers == null) return;
            foreach (Delegate callback in handlers.GetInvocationList())
                try { ((Action<ToyDevice>)callback)(device); }
                catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void RaiseDeviceRemoved(uint index)
        {
            Action<uint> handlers = DeviceRemoved;
            if (handlers == null) return;
            foreach (Delegate callback in handlers.GetInvocationList())
                try { ((Action<uint>)callback)(index); }
                catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void RaiseScanningChanged(bool scanning)
        {
            Action<bool> handlers = ScanningChanged;
            if (handlers == null) return;
            foreach (Delegate callback in handlers.GetInvocationList())
                try { ((Action<bool>)callback)(scanning); }
                catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private static uint ToMilliseconds(float seconds)
        {
            double milliseconds = Math.Max(1d, Math.Min(uint.MaxValue, Math.Round(seconds * 1000d)));
            return (uint)milliseconds;
        }

        private static void ValidateUnit(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, "Value must be finite.");
        }

        private static void ValidateDuration(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                throw new ArgumentOutOfRangeException(name, "Duration must be finite and nonnegative.");
        }
    }
}
