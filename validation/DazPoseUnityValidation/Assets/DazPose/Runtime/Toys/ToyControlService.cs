using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DazPose.Motion;
using DazPose.Toys.Buttplug;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace DazPose.Toys
{
    /// <summary>Scene-level owner of the optional Intiface connection and device registry.</summary>
    [DisallowMultipleComponent]
    public sealed class ToyControlService : MonoBehaviour
    {
        public const string DefaultServerAddress = "ws://127.0.0.1:12345";
        private const int ShutdownTimeoutMilliseconds = 10000;

        [SerializeField] private string serverAddress = DefaultServerAddress;

        private readonly ConcurrentQueue<Action> _mainThreadEvents = new ConcurrentQueue<Action>();
        private static readonly IReadOnlyList<ToyDevice> EmptyDevices = Array.AsReadOnly(Array.Empty<ToyDevice>());
        private IToyBackend _backend;
        private Task _shutdownTask;
        private bool _destroyed;
        private float _level;
        private readonly HashSet<ToyOutputBinding> _levelBindings = new HashSet<ToyOutputBinding>();
        private readonly Dictionary<ToyOutputBinding, ToyMotionBinding> _motionBindings = new Dictionary<ToyOutputBinding, ToyMotionBinding>();
        private readonly Dictionary<ToyOutputBinding, ToyMotionFollower> _followers = new Dictionary<ToyOutputBinding, ToyMotionFollower>();
        private MotionDriver _motionDriver;
        private ToyCommandReport _lastCommandReport = new ToyCommandReport("None", 0, Array.Empty<string>());
        private readonly object _levelPumpGate = new object();
        private readonly SemaphoreSlim _outputCommandGate = new SemaphoreSlim(1, 1);
        private bool _levelPumpActive;
        private bool _hasPendingLevel;
        private float _pendingLevel;
        private int _levelGeneration;
        private int _outputCommandGeneration;
        private long _lastLevelDispatchAt;
        private const int MinimumLevelDispatchGapMilliseconds = 50;

        public string ServerAddress
        {
            get => serverAddress;
            set => serverAddress = value ?? string.Empty;
        }

        public ToyConnectionState ConnectionState => _backend != null
            ? _backend.ConnectionState : ToyConnectionState.Disconnected;
        public string StatusMessage => _backend != null ? _backend.StatusMessage : "Disconnected.";
        public string LastError => _backend != null ? _backend.LastError : string.Empty;
        public bool IsConnected => _backend != null && _backend.IsConnected;
        public bool IsScanning => _backend != null && _backend.IsScanning;
        public IReadOnlyList<ToyDevice> ConnectedDevices => _backend != null ? _backend.Devices : EmptyDevices;
        public float Level => _level;
        public ToyCommandReport LastCommandReport => _lastCommandReport;
        public IReadOnlyCollection<ToyOutputBinding> LevelBindings => new ReadOnlyCollection<ToyOutputBinding>(_levelBindings.ToArray());
        public IReadOnlyCollection<ToyMotionBinding> MotionBindings => new ReadOnlyCollection<ToyMotionBinding>(_motionBindings.Values.ToArray());
        public bool IsFollowingMotion => _motionDriver != null;
        public MotionDriver FollowedMotionDriver => _motionDriver;
        internal int OutputCommandGeneration => Volatile.Read(ref _outputCommandGeneration);

        public event Action<ToyConnectionState, string> StateChanged;
        public event Action<ToyDevice> DeviceAdded;
        public event Action<uint> DeviceRemoved;
        public event Action<bool> ScanningChanged;

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
            ReconcileMotionFollowers();
        }

        private void OnDisable()
        {
            InvalidatePendingOutputs();
            StopFollowing(stopHardware: false);
            if (Application.isPlaying && !_destroyed) StartShutdown(disposeBackend: false);
        }

        private void OnApplicationQuit()
        {
            InvalidatePendingOutputs();
            StartShutdown(disposeBackend: false);
        }

        private void OnDestroy()
        {
            _destroyed = true;
            InvalidatePendingOutputs();
            StartShutdown(disposeBackend: true);
        }

        public Task ConnectAsync() => RunBackendOperation(backend => backend.ConnectAsync(serverAddress), "connect");
        public Task DisconnectAsync()
        {
            InvalidatePendingOutputs();
            StopFollowing(stopHardware: false);
            EnsureBackend();
            return DisconnectBackendAsync();
        }
        public Task StartScanningAsync() => RunBackendOperation(backend => backend.StartScanningAsync(), "start scanning");
        public Task StopScanningAsync() => RunBackendOperation(backend => backend.StopScanningAsync(), "stop scanning");
        public Task StopAllAsync()
        {
            InvalidatePendingOutputs();
            StopFollowing(stopHardware: false);
            _level = 0f;
            EnsureBackend();
            return StopAllBehindPendingOutputsAsync();
        }

        public bool SetLevelBinding(ToyOutputBinding binding, bool assigned)
        {
            if (binding.Capability != ToyOutputCapability.Vibrate
                && binding.Capability != ToyOutputCapability.Oscillate) return false;
            if (!FeatureSupports(binding)) return false;
            if (assigned) return _levelBindings.Add(binding) || _levelBindings.Contains(binding);
            _levelBindings.Remove(binding);
            return true;
        }

        public bool IsLevelBindingAssigned(ToyOutputBinding binding) => _levelBindings.Contains(binding);

        public bool SetMotionBinding(ToyOutputBinding binding, bool assigned,
            ToyMotionStrategy strategy = ToyMotionStrategy.Auto, bool invert = false,
            float minimum = 0f, float maximum = 1f)
        {
            if (binding.Capability != ToyOutputCapability.Position
                && binding.Capability != ToyOutputCapability.HwPositionWithDuration) return false;
            if (!FeatureSupports(binding)) return false;
            if (assigned)
            {
                if (minimum < 0f || maximum > 1f || minimum > maximum
                    || float.IsNaN(minimum) || float.IsNaN(maximum)) return false;
                foreach (ToyOutputBinding other in _motionBindings.Keys.Where(key =>
                    key.DeviceIndex == binding.DeviceIndex && key.FeatureIndex == binding.FeatureIndex
                    && key.Capability != binding.Capability).ToArray())
                {
                    _motionBindings.Remove(other);
                    if (_followers.TryGetValue(other, out ToyMotionFollower prior))
                    {
                        prior.Dispose(stopHardware: false);
                        _followers.Remove(other);
                    }
                }
                if (!_motionBindings.TryGetValue(binding, out ToyMotionBinding settings))
                {
                    settings = new ToyMotionBinding(binding, strategy, invert, minimum, maximum);
                    _motionBindings.Add(binding, settings);
                }
                else
                {
                    settings.Strategy = strategy;
                    settings.Invert = invert;
                    settings.Minimum = minimum;
                    settings.Maximum = maximum;
                    if (_followers.TryGetValue(binding, out ToyMotionFollower prior))
                    {
                        prior.Dispose(stopHardware: false);
                        _followers.Remove(binding);
                    }
                }
                ReconcileMotionFollowers();
            }
            else
            {
                _motionBindings.Remove(binding);
                if (_followers.TryGetValue(binding, out ToyMotionFollower follower))
                {
                    follower.Dispose(stopHardware: true);
                    _followers.Remove(binding);
                }
            }
            return true;
        }

        public bool IsMotionBindingAssigned(ToyOutputBinding binding) => _motionBindings.ContainsKey(binding);
        public bool IsMotionFeatureAssigned(uint deviceIndex, uint featureIndex)
        {
            foreach (ToyOutputBinding binding in _motionBindings.Keys)
                if (binding.DeviceIndex == deviceIndex && binding.FeatureIndex == featureIndex) return true;
            return false;
        }
        public bool TryGetMotionBinding(ToyOutputBinding binding, out ToyMotionBinding value) =>
            _motionBindings.TryGetValue(binding, out value);

        /// <summary>Changes the semantic level and independently reports each assigned output result.</summary>
        public async Task<ToyCommandReport> SetLevelAsync(float normalized)
        {
            if (float.IsNaN(normalized) || float.IsInfinity(normalized))
                throw new ArgumentOutOfRangeException(nameof(normalized), "Level must be finite.");
            int commandGeneration = OutputCommandGeneration;
            float requestedLevel = UnityEngine.Mathf.Clamp01(normalized);
            _level = requestedLevel;
            ToyOutputBinding[] targets = _levelBindings.ToArray();
            var errors = new List<string>();
            int successes = 0;
            bool superseded = false;
            foreach (ToyOutputBinding binding in targets)
            {
                if (commandGeneration != OutputCommandGeneration)
                {
                    superseded = true;
                    break;
                }
                try
                {
                    bool sent = await DispatchOutputAsync(binding, requestedLevel, 0, commandGeneration).ConfigureAwait(false);
                    if (sent) successes++;
                    else { superseded = true; break; }
                }
                catch (Exception exception) { errors.Add(binding + ": " + exception.Message); }
            }
            var report = new ToyCommandReport("Set level " + requestedLevel.ToString("P0")
                + (superseded ? " (superseded by stop)" : string.Empty), successes, errors);
            if (commandGeneration == OutputCommandGeneration) _lastCommandReport = report;
            return report;
        }

        public void SetLevel(float normalized)
        {
            if (float.IsNaN(normalized) || float.IsInfinity(normalized))
                throw new ArgumentOutOfRangeException(nameof(normalized), "Level must be finite.");
            _level = Mathf.Clamp01(normalized);
            lock (_levelPumpGate)
            {
                _pendingLevel = _level;
                _hasPendingLevel = true;
                if (_levelPumpActive) return;
                _levelPumpActive = true;
                int generation = _levelGeneration;
                _ = DrainLevelRequestsAsync(generation);
            }
        }

        private async Task DrainLevelRequestsAsync(int generation)
        {
            while (true)
            {
                float requested;
                lock (_levelPumpGate)
                {
                    if (generation != _levelGeneration) return;
                    if (!_hasPendingLevel)
                    {
                        _levelPumpActive = false;
                        return;
                    }
                    requested = _pendingLevel;
                    _hasPendingLevel = false;
                }

                if (_lastLevelDispatchAt > 0)
                {
                    double waitMilliseconds = MinimumLevelDispatchGapMilliseconds
                        - (Stopwatch.GetTimestamp() - _lastLevelDispatchAt) * 1000d / Stopwatch.Frequency;
                    if (waitMilliseconds > 0d)
                        await Task.Delay((int)Math.Ceiling(waitMilliseconds)).ConfigureAwait(false);
                }
                lock (_levelPumpGate)
                {
                    if (generation != _levelGeneration) return;
                    if (_hasPendingLevel)
                    {
                        requested = _pendingLevel;
                        _hasPendingLevel = false;
                    }
                }
                try
                {
                    await SetLevelAsync(requested).ConfigureAwait(false);
                    _lastLevelDispatchAt = Stopwatch.GetTimestamp();
                }
                catch (Exception exception) { Debug.LogWarning("Toy level output failed: " + exception.Message, this); }
            }
        }

        /// <summary>Explicit manual bench command; it does not create or change an assignment.</summary>
        public async Task<ToyCommandReport> TestOutputAsync(ToyOutputBinding binding,
            float normalized, uint durationMilliseconds = 0)
        {
            if (!FeatureSupports(binding)) throw new InvalidOperationException("That feature does not advertise " + binding.Capability + ".");
            if (binding.Capability == ToyOutputCapability.HwPositionWithDuration && durationMilliseconds == 0)
                throw new ArgumentOutOfRangeException(nameof(durationMilliseconds));
            if (float.IsNaN(normalized) || float.IsInfinity(normalized)) throw new ArgumentOutOfRangeException(nameof(normalized));
            int commandGeneration = OutputCommandGeneration;
            float value = UnityEngine.Mathf.Clamp01(normalized);
            try
            {
                bool sent = await DispatchOutputAsync(binding, value, durationMilliseconds, commandGeneration).ConfigureAwait(false);
                if (!sent)
                    return new ToyCommandReport("Manual " + binding.Capability, 0,
                        new[] { "Command was superseded by Stop All." });
                return _lastCommandReport = new ToyCommandReport("Manual " + binding.Capability, 1, Array.Empty<string>());
            }
            catch (Exception exception)
            {
                return _lastCommandReport = new ToyCommandReport("Manual " + binding.Capability, 0,
                    new[] { binding + ": " + exception.Message });
            }
        }

        public async Task<ToyCommandReport> StopAllWithReportAsync()
        {
            try
            {
                int devices = ConnectedDevices.Count;
                await StopAllAsync().ConfigureAwait(false);
                string lastError = LastError;
                return _lastCommandReport = new ToyCommandReport("Stop all", string.IsNullOrEmpty(lastError) ? devices : 0,
                    string.IsNullOrEmpty(lastError) ? Array.Empty<string>() : new[] { lastError });
            }
            catch (Exception exception)
            {
                return _lastCommandReport = new ToyCommandReport("Stop all", 0, new[] { exception.Message });
            }
        }

        public void Follow(MotionDriver driver)
        {
            if (_motionDriver == driver) return;
            StopFollowing(stopHardware: false);
            _motionDriver = driver;
            ReconcileMotionFollowers();
        }

        public void StopFollowing() => StopFollowing(stopHardware: true);

        private void StopFollowing(bool stopHardware)
        {
            foreach (ToyMotionFollower follower in _followers.Values) follower.Dispose(stopHardware);
            _followers.Clear();
            _motionDriver = null;
        }

        /// <summary>Fire-and-report facade for immediate UI use.</summary>
        public void Connect() => Observe(ConnectAsync(), "connect");
        public void Disconnect() => Observe(DisconnectAsync(), "disconnect");
        public void StartScanning() => Observe(StartScanningAsync(), "start scanning");
        public void StopScanning() => Observe(StopScanningAsync(), "stop scanning");
        public void StopAll() => Observe(StopAllAsync(), "stop all");
        public void Stop() => StopAll();
        public Task<ToyCommandReport> StopAsync() => StopAllWithReportAsync();

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
        }

        internal async Task<bool> DispatchOutputAsync(ToyOutputBinding binding, float normalized,
            uint durationMilliseconds, int commandGeneration)
        {
            await _outputCommandGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (commandGeneration != OutputCommandGeneration) return false;
                EnsureBackend();
                if (_backend == null) throw new InvalidOperationException("Toy backend is unavailable.");
                if (binding.Capability == ToyOutputCapability.HwPositionWithDuration)
                {
                    ToyOutputRange range = default;
                    bool hasRange = false;
                    foreach (ToyDevice device in ConnectedDevices)
                    {
                        if (device.DeviceIndex != binding.DeviceIndex) continue;
                        foreach (ToyFeature feature in device.Features)
                            if (feature.FeatureIndex == binding.FeatureIndex
                                && feature.TryGetOutput(binding.Capability, out range)) hasRange = true;
                        break;
                    }
                    if (hasRange && range.HasDurationRange)
                        durationMilliseconds = (uint)Mathf.Clamp((int)Math.Min(int.MaxValue, durationMilliseconds),
                            range.MinimumDurationMilliseconds, range.MaximumDurationMilliseconds);
                    else if (durationMilliseconds == 0) durationMilliseconds = UnknownDurationMilliseconds;
                }
                await _backend.SendOutputAsync(binding, normalized, durationMilliseconds).ConfigureAwait(false);
                return true;
            }
            finally { _outputCommandGate.Release(); }
        }

        private const uint UnknownDurationMilliseconds = 50;

        internal async Task<bool> StopFeatureAsync(ToyOutputBinding binding, int commandGeneration)
        {
            await _outputCommandGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (commandGeneration != OutputCommandGeneration || _backend == null) return false;
                await _backend.StopFeatureAsync(binding).ConfigureAwait(false);
                return true;
            }
            finally { _outputCommandGate.Release(); }
        }

        internal void SetFollowerDiagnostic(ToyOutputBinding binding, string diagnostic)
        {
            _followerDiagnostics[binding] = diagnostic ?? string.Empty;
        }

        private readonly ConcurrentDictionary<ToyOutputBinding, string> _followerDiagnostics =
            new ConcurrentDictionary<ToyOutputBinding, string>();
        public string GetFollowerDiagnostic(ToyOutputBinding binding) =>
            _followerDiagnostics.TryGetValue(binding, out string value) ? value : "inactive";

        internal ToyOutputBinding ResolveMotionOutput(ToyMotionBinding binding)
        {
            bool supportsPosition = FeatureSupports(new ToyOutputBinding(binding.Output.DeviceIndex,
                binding.Output.FeatureIndex, ToyOutputCapability.Position));
            bool supportsDuration = FeatureSupports(new ToyOutputBinding(binding.Output.DeviceIndex,
                binding.Output.FeatureIndex, ToyOutputCapability.HwPositionWithDuration));
            if (binding.Strategy == ToyMotionStrategy.Position && supportsPosition)
                return new ToyOutputBinding(binding.Output.DeviceIndex, binding.Output.FeatureIndex, ToyOutputCapability.Position);
            if (binding.Strategy == ToyMotionStrategy.HwPositionWithDuration && supportsDuration)
                return new ToyOutputBinding(binding.Output.DeviceIndex, binding.Output.FeatureIndex, ToyOutputCapability.HwPositionWithDuration);
            if (binding.Strategy == ToyMotionStrategy.Auto && _motionDriver != null && supportsDuration
                && _motionDriver.TryGetCurrentTargetSegment(out _))
                return new ToyOutputBinding(binding.Output.DeviceIndex, binding.Output.FeatureIndex, ToyOutputCapability.HwPositionWithDuration);
            if (supportsPosition)
                return new ToyOutputBinding(binding.Output.DeviceIndex, binding.Output.FeatureIndex, ToyOutputCapability.Position);
            return new ToyOutputBinding(binding.Output.DeviceIndex, binding.Output.FeatureIndex, ToyOutputCapability.HwPositionWithDuration);
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

        private void ReconcileMotionFollowers()
        {
            if (_motionDriver == null) return;
            var available = new HashSet<ToyOutputBinding>();
            foreach (ToyDevice device in ConnectedDevices)
                foreach (ToyFeature feature in device.Features)
                    foreach (ToyOutputRange output in feature.Outputs)
                    {
                        var key = new ToyOutputBinding(device.DeviceIndex, feature.FeatureIndex, output.Capability);
                        if (_motionBindings.ContainsKey(key)) available.Add(key);
                    }

            foreach (ToyOutputBinding stale in _followers.Keys.Where(key => !available.Contains(key)).ToArray())
            {
                _followers[stale].Dispose(stopHardware: false);
                _followers.Remove(stale);
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

        private uint GetDeviceTimingGap(uint deviceIndex)
        {
            foreach (ToyDevice device in ConnectedDevices)
                if (device.DeviceIndex == deviceIndex) return device.MessageTimingGapMilliseconds;
            return 0;
        }

        private Task RunBackendOperation(Func<IToyBackend, Task> operation, string name)
        {
            EnsureBackend();
            if (_backend == null) return Task.FromResult(0);
            try { return operation(_backend) ?? Task.FromResult(0); }
            catch (Exception exception)
            {
                Debug.LogWarning("Toy backend " + name + " failed: " + exception.Message, this);
                return Task.FromResult(0);
            }
        }

        private void InvalidatePendingOutputs()
        {
            Interlocked.Increment(ref _outputCommandGeneration);
            lock (_levelPumpGate)
            {
                _levelGeneration++;
                _hasPendingLevel = false;
                _levelPumpActive = false;
            }
        }

        private async Task DisconnectBackendAsync()
        {
            if (_backend == null) return;
            await _outputCommandGate.WaitAsync().ConfigureAwait(false);
            try { await _backend.DisconnectAsync().ConfigureAwait(false); }
            finally { _outputCommandGate.Release(); }
        }

        private async Task StopAllBehindPendingOutputsAsync()
        {
            if (_backend == null) return;
            await _outputCommandGate.WaitAsync().ConfigureAwait(false);
            try { await _backend.StopAllAsync().ConfigureAwait(false); }
            finally { _outputCommandGate.Release(); }
        }

        private void Observe(Task operation, string name)
        {
            _ = ObserveAsync(operation, name);
        }

        private async Task ObserveAsync(Task operation, string name)
        {
            try { await operation; }
            catch (Exception exception)
            {
                Debug.LogWarning("Toy backend " + name + " failed: " + exception.Message, this);
            }
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
                if (await Task.WhenAny(disconnect, timeout).ConfigureAwait(false) != disconnect)
                {
                    ObserveAbandonedTask(disconnect);
                    Debug.LogWarning("Toy shutdown timed out after " + ShutdownTimeoutMilliseconds + " ms.");
                }
                else
                {
                    await disconnect.ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Toy shutdown failed: " + exception.Message);
            }
            finally
            {
                if (disposeBackend) backend.Dispose();
            }
        }

        private static async Task DisposeAfterShutdownAsync(Task shutdown, IToyBackend backend)
        {
            try { await shutdown.ConfigureAwait(false); }
            catch { }
            backend.Dispose();
        }

        private static void ObserveAbandonedTask(Task task)
        {
            task.ContinueWith(completed =>
            {
                if (completed.IsFaulted) _ = completed.Exception;
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        private void OnBackendStateChanged(ToyConnectionState state, string message) =>
            _mainThreadEvents.Enqueue(() => RaiseStateChanged(state, message));

        private void OnBackendDeviceAdded(ToyDevice device) =>
            _mainThreadEvents.Enqueue(() => RaiseDeviceAdded(device));

        private void OnBackendDeviceRemoved(uint deviceIndex) =>
            _mainThreadEvents.Enqueue(() => RaiseDeviceRemoved(deviceIndex));

        private void OnBackendScanningChanged(bool scanning) =>
            _mainThreadEvents.Enqueue(() => RaiseScanningChanged(scanning));

        private void RaiseStateChanged(ToyConnectionState state, string message)
        {
            Action<ToyConnectionState, string> handlers = StateChanged;
            if (handlers == null) return;
            foreach (Delegate callback in handlers.GetInvocationList())
            {
                try { ((Action<ToyConnectionState, string>)callback)(state, message); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }

        private void RaiseDeviceAdded(ToyDevice device)
        {
            Action<ToyDevice> handlers = DeviceAdded;
            if (handlers == null) return;
            foreach (Delegate callback in handlers.GetInvocationList())
            {
                try { ((Action<ToyDevice>)callback)(device); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }

        private void RaiseDeviceRemoved(uint deviceIndex)
        {
            Action<uint> handlers = DeviceRemoved;
            if (handlers == null) return;
            foreach (Delegate callback in handlers.GetInvocationList())
            {
                try { ((Action<uint>)callback)(deviceIndex); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }

        private void RaiseScanningChanged(bool scanning)
        {
            Action<bool> handlers = ScanningChanged;
            if (handlers == null) return;
            foreach (Delegate callback in handlers.GetInvocationList())
            {
                try { ((Action<bool>)callback)(scanning); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }
    }
}
