using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using DazPose.Toys.Buttplug;
using UnityEngine;

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
        }

        private void OnDisable()
        {
            if (Application.isPlaying && !_destroyed) StartShutdown(disposeBackend: false);
        }

        private void OnApplicationQuit()
        {
            StartShutdown(disposeBackend: false);
        }

        private void OnDestroy()
        {
            _destroyed = true;
            StartShutdown(disposeBackend: true);
        }

        public Task ConnectAsync() => RunBackendOperation(backend => backend.ConnectAsync(serverAddress), "connect");
        public Task DisconnectAsync() => RunBackendOperation(backend => backend.DisconnectAsync(), "disconnect");
        public Task StartScanningAsync() => RunBackendOperation(backend => backend.StartScanningAsync(), "start scanning");
        public Task StopScanningAsync() => RunBackendOperation(backend => backend.StopScanningAsync(), "stop scanning");
        public Task StopAllAsync() => RunBackendOperation(backend => backend.StopAllAsync(), "stop all");

        /// <summary>Fire-and-report facade for immediate UI use.</summary>
        public void Connect() => Observe(ConnectAsync(), "connect");
        public void Disconnect() => Observe(DisconnectAsync(), "disconnect");
        public void StartScanning() => Observe(StartScanningAsync(), "start scanning");
        public void StopScanning() => Observe(StopScanningAsync(), "stop scanning");
        public void StopAll() => Observe(StopAllAsync(), "stop all");

        private void EnsureBackend()
        {
            if (_backend != null || _destroyed) return;
            _backend = new ButtplugToyBackend();
            _backend.StateChanged += OnBackendStateChanged;
            _backend.DeviceAdded += OnBackendDeviceAdded;
            _backend.DeviceRemoved += OnBackendDeviceRemoved;
            _backend.ScanningChanged += OnBackendScanningChanged;
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
