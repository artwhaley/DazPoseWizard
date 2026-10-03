using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Client;
using Buttplug.Core;
using UnityEngine;

namespace DazPose.Toys.Buttplug
{
    /// <summary>Buttplug client transport and event-backed runtime device registry.</summary>
    internal sealed class ButtplugToyBackend : IToyBackend
    {
        private const int RequestTimeoutMilliseconds = 5000;
        private const int ShutdownRequestTimeoutMilliseconds = 1500;

        private readonly object _stateLock = new object();
        private readonly SemaphoreSlim _operationGate = new SemaphoreSlim(1, 1);
        private readonly ToyDeviceRegistry _registry = new ToyDeviceRegistry();
        private readonly Dictionary<uint, ButtplugClientDevice> _nativeDevices = new Dictionary<uint, ButtplugClientDevice>();

        private ButtplugClient _client;
        private volatile bool _intentionalDisconnect;
        private volatile bool _disposed;
        private ToyConnectionState _connectionState = ToyConnectionState.Disconnected;
        private string _statusMessage = "Disconnected.";
        private string _lastError = string.Empty;
        private bool _isScanning;

        public event Action<ToyConnectionState, string> StateChanged;
        public event Action<ToyDevice> DeviceAdded;
        public event Action<uint> DeviceRemoved;
        public event Action<bool> ScanningChanged;

        public ToyConnectionState ConnectionState
        {
            get { lock (_stateLock) return _connectionState; }
        }

        public string StatusMessage
        {
            get { lock (_stateLock) return _statusMessage; }
        }

        public string LastError
        {
            get { lock (_stateLock) return _lastError; }
        }

        public bool IsConnected
        {
            get
            {
                lock (_stateLock)
                    return _connectionState == ToyConnectionState.Connected && _client != null && _client.Connected;
            }
        }

        public bool IsScanning
        {
            get { lock (_stateLock) return _isScanning; }
        }

        public IReadOnlyList<ToyDevice> Devices
        {
            get
            {
                lock (_stateLock)
                    return _registry.Snapshot;
            }
        }

        public async Task ConnectAsync(string serverAddress)
        {
            await _operationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed) return;
                if (IsConnected) return;

                if (!Uri.TryCreate(serverAddress, UriKind.Absolute, out Uri address)
                    || (address.Scheme != "ws" && address.Scheme != "wss"))
                {
                    SetLastError("Enter a valid ws:// or wss:// Intiface server address.");
                    SetState(ToyConnectionState.Faulted, LastError);
                    return;
                }

                DisposeClient();
                ClearDevices();
                SetScanning(false);
                _intentionalDisconnect = false;
                SetLastError(string.Empty);
                SetState(ToyConnectionState.Connecting, "Connecting to " + address + "…");

                var client = new ButtplugClient("DazPoseWizard");
                _client = client;
                Subscribe(client);

                try
                {
                    var connector = new ButtplugWebsocketConnector(address);
                    using (var cancellation = new CancellationTokenSource(RequestTimeoutMilliseconds))
                        await client.ConnectAsync(connector, cancellation.Token).ConfigureAwait(false);

                    if (!client.Connected)
                        throw new InvalidOperationException("The Buttplug client did not complete its connection handshake.");

                    SynchronizeDevices(client.Devices);
                    SetLastError(string.Empty);
                    SetState(ToyConnectionState.Connected, "Connected to " + address + ".");
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    ClearDevices();
                    SetScanning(false);
                    SetLastError(Describe(exception));
                    SetState(ToyConnectionState.Faulted, LastError);
                    DisposeClient();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetLastError(Describe(exception));
                SetState(ToyConnectionState.Faulted, LastError);
            }
            finally
            {
                _operationGate.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            await _operationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed) return;
                ButtplugClient client = _client;
                if (client == null)
                {
                    ClearDevices();
                    SetScanning(false);
                    SetState(ToyConnectionState.Disconnected, "Disconnected.");
                    return;
                }

                _intentionalDisconnect = true;
                SetState(ToyConnectionState.Disconnecting, "Stopping outputs and disconnecting.");
                if (client.Connected)
                {
                    await TryShutdownOperationAsync(
                        token => client.StopScanningAsync(token), "Stop scanning").ConfigureAwait(false);
                    await TryShutdownOperationAsync(
                        token => client.StopAllDevicesAsync(token), "Stop all devices").ConfigureAwait(false);
                    await TryShutdownOperationAsync(
                        _ => client.DisconnectAsync(), "Disconnect").ConfigureAwait(false);
                }

                ClearDevices();
                SetScanning(false);
                DisposeClient();
                SetState(ToyConnectionState.Disconnected, "Disconnected.");
            }
            catch (Exception exception)
            {
                SetLastError(Describe(exception));
                ClearDevices();
                SetScanning(false);
                DisposeClient();
                SetState(ToyConnectionState.Disconnected, "Disconnected after a best-effort shutdown. " + LastError);
            }
            finally
            {
                _intentionalDisconnect = false;
                _operationGate.Release();
            }
        }

        public async Task StartScanningAsync()
        {
            await _operationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                ButtplugClient client = GetConnectedClient();
                if (client == null) return;

                SetLastError(string.Empty);
                SetScanning(true);
                await RunRequestAsync(token => client.StartScanningAsync(token), "Start scanning",
                    RequestTimeoutMilliseconds).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                SetScanning(false);
                ReportError("Start scanning failed: " + Describe(exception));
            }
            finally
            {
                _operationGate.Release();
            }
        }

        public async Task StopScanningAsync()
        {
            await _operationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                ButtplugClient client = GetConnectedClient();
                if (client == null)
                {
                    SetScanning(false);
                    return;
                }

                await RunRequestAsync(token => client.StopScanningAsync(token), "Stop scanning",
                    RequestTimeoutMilliseconds).ConfigureAwait(false);
                SetScanning(false);
            }
            catch (Exception exception)
            {
                ReportError("Stop scanning failed: " + Describe(exception));
            }
            finally
            {
                _operationGate.Release();
            }
        }

        public async Task StopAllAsync()
        {
            await _operationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                ButtplugClient client = GetConnectedClient();
                if (client == null || Devices.Count == 0) return;
                await RunRequestAsync(token => client.StopAllDevicesAsync(token), "Stop all devices",
                    ShutdownRequestTimeoutMilliseconds).ConfigureAwait(false);
                SetLastError(string.Empty);
            }
            catch (Exception exception)
            {
                ReportError("Stop all failed: " + Describe(exception));
            }
            finally
            {
                _operationGate.Release();
            }
        }

        public void Dispose()
        {
            lock (_stateLock)
            {
                if (_disposed) return;
                _disposed = true;
            }
            DisposeClient();
            ClearDevices();
            SetScanning(false);
        }

        private ButtplugClient GetConnectedClient()
        {
            ButtplugClient client = _client;
            if (client != null && client.Connected && ConnectionState == ToyConnectionState.Connected)
                return client;
            return null;
        }

        private void Subscribe(ButtplugClient client)
        {
            client.DeviceAdded += OnDeviceAdded;
            client.DeviceRemoved += OnDeviceRemoved;
            client.ServerDisconnect += OnServerDisconnect;
            client.ErrorReceived += OnErrorReceived;
            client.ScanningFinished += OnScanningFinished;
        }

        private void Unsubscribe(ButtplugClient client)
        {
            if (client == null) return;
            client.DeviceAdded -= OnDeviceAdded;
            client.DeviceRemoved -= OnDeviceRemoved;
            client.ServerDisconnect -= OnServerDisconnect;
            client.ErrorReceived -= OnErrorReceived;
            client.ScanningFinished -= OnScanningFinished;
        }

        private void DisposeClient()
        {
            ButtplugClient client = _client;
            _client = null;
            if (client == null) return;
            Unsubscribe(client);
            try { client.Dispose(); }
            catch (Exception exception) { Debug.LogWarning("Buttplug client disposal failed: " + Describe(exception)); }
        }

        private void SynchronizeDevices(IEnumerable<ButtplugClientDevice> currentDevices)
        {
            var liveIndices = new HashSet<uint>();
            foreach (ButtplugClientDevice device in currentDevices ?? Array.Empty<ButtplugClientDevice>())
            {
                if (device == null) continue;
                liveIndices.Add(device.Index);
                RegisterDevice(device);
            }

            uint[] staleIndices;
            lock (_stateLock)
                staleIndices = _registry.Snapshot.Select(device => device.DeviceIndex)
                    .Where(index => !liveIndices.Contains(index)).ToArray();
            foreach (uint staleIndex in staleIndices) RemoveDevice(staleIndex, null);
        }

        private void RegisterDevice(ButtplugClientDevice nativeDevice)
        {
            ToyDevice snapshot;
            ToyDevice priorSnapshot = null;
            bool isSameRuntimeDevice;
            lock (_stateLock)
            {
                snapshot = ButtplugDeviceAdapter.CreateSnapshot(nativeDevice);
                bool wasPresent = _registry.TryGet(nativeDevice.Index, out priorSnapshot);
                isSameRuntimeDevice = wasPresent
                    && _nativeDevices.TryGetValue(nativeDevice.Index, out ButtplugClientDevice priorDevice)
                    && ReferenceEquals(priorDevice, nativeDevice);
                _nativeDevices[nativeDevice.Index] = nativeDevice;
                _registry.Set(snapshot);
            }

            if (isSameRuntimeDevice) return;
            if (priorSnapshot != null) RaiseDeviceRemoved(nativeDevice.Index);
            RaiseDeviceAdded(snapshot);
        }

        private void RemoveDevice(uint deviceIndex, ButtplugClientDevice expectedDevice)
        {
            ToyDevice removed;
            lock (_stateLock)
            {
                if (expectedDevice != null
                    && (!_nativeDevices.TryGetValue(deviceIndex, out ButtplugClientDevice current)
                        || !ReferenceEquals(current, expectedDevice))) return;

                if (!_registry.Remove(deviceIndex, out removed)) return;
                _nativeDevices.Remove(deviceIndex);
            }
            RaiseDeviceRemoved(removed.DeviceIndex);
        }

        private void ClearDevices()
        {
            uint[] removedIndices;
            lock (_stateLock)
            {
                removedIndices = _registry.Clear().Select(device => device.DeviceIndex).ToArray();
                _nativeDevices.Clear();
            }
            foreach (uint index in removedIndices) RaiseDeviceRemoved(index);
        }

        private void OnDeviceAdded(object sender, DeviceAddedEventArgs eventArgs)
        {
            if (eventArgs == null || eventArgs.Device == null) return;
            RegisterDevice(eventArgs.Device);
        }

        private void OnDeviceRemoved(object sender, DeviceRemovedEventArgs eventArgs)
        {
            if (eventArgs == null || eventArgs.Device == null) return;
            RemoveDevice(eventArgs.Device.Index, eventArgs.Device);
        }

        private void OnServerDisconnect(object sender, EventArgs eventArgs)
        {
            ClearDevices();
            SetScanning(false);
            if (_intentionalDisconnect) return;
            const string message = "Intiface server disconnected unexpectedly.";
            SetLastError(message);
            SetState(ToyConnectionState.Faulted, message);
        }

        private void OnErrorReceived(object sender, ButtplugExceptionEventArgs eventArgs)
        {
            if (eventArgs == null || eventArgs.Exception == null) return;
            ReportError(Describe(eventArgs.Exception));
        }

        private void OnScanningFinished(object sender, EventArgs eventArgs) => SetScanning(false);

        private void SetState(ToyConnectionState state, string message)
        {
            lock (_stateLock)
            {
                _connectionState = state;
                _statusMessage = message ?? string.Empty;
            }
            RaiseStateChanged(state, message ?? string.Empty);
        }

        private void SetLastError(string message)
        {
            lock (_stateLock) _lastError = message ?? string.Empty;
        }

        private void ReportError(string message)
        {
            SetLastError(message);
            RaiseStateChanged(ConnectionState, StatusMessage);
        }

        private void SetScanning(bool value)
        {
            bool changed;
            lock (_stateLock)
            {
                changed = _isScanning != value;
                _isScanning = value;
            }
            if (changed) RaiseScanningChanged(value);
        }

        private static async Task RunRequestAsync(Func<CancellationToken, Task> operation,
            string operationName, int timeoutMilliseconds)
        {
            using (var cancellation = new CancellationTokenSource())
            {
                Task request = operation(cancellation.Token);
                Task timeout = Task.Delay(timeoutMilliseconds);
                if (await Task.WhenAny(request, timeout).ConfigureAwait(false) != request)
                {
                    cancellation.Cancel();
                    ObserveAbandonedTask(request);
                    throw new TimeoutException(operationName + " timed out after " + timeoutMilliseconds + " ms.");
                }
                await request.ConfigureAwait(false);
            }
        }

        private async Task TryShutdownOperationAsync(Func<CancellationToken, Task> operation, string operationName)
        {
            try
            {
                await RunRequestAsync(operation, operationName, ShutdownRequestTimeoutMilliseconds).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                SetLastError(operationName + ": " + Describe(exception));
            }
        }

        private static void ObserveAbandonedTask(Task task)
        {
            task.ContinueWith(completed =>
            {
                if (completed.IsFaulted) _ = completed.Exception;
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        private static string Describe(Exception exception)
        {
            if (exception == null) return "Unknown Buttplug error.";
            Exception current = exception;
            while (current.InnerException != null) current = current.InnerException;
            return string.IsNullOrWhiteSpace(current.Message) ? current.GetType().Name : current.Message;
        }

        private void RaiseStateChanged(ToyConnectionState state, string message)
        {
            Action<ToyConnectionState, string> handlers = StateChanged;
            if (handlers == null) return;
            foreach (Delegate callback in handlers.GetInvocationList())
            {
                try { ((Action<ToyConnectionState, string>)callback)(state, message); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        private void RaiseDeviceAdded(ToyDevice device)
        {
            Action<ToyDevice> handlers = DeviceAdded;
            if (handlers == null) return;
            foreach (Delegate callback in handlers.GetInvocationList())
            {
                try { ((Action<ToyDevice>)callback)(device); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        private void RaiseDeviceRemoved(uint index)
        {
            Action<uint> handlers = DeviceRemoved;
            if (handlers == null) return;
            foreach (Delegate callback in handlers.GetInvocationList())
            {
                try { ((Action<uint>)callback)(index); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        private void RaiseScanningChanged(bool scanning)
        {
            Action<bool> handlers = ScanningChanged;
            if (handlers == null) return;
            foreach (Delegate callback in handlers.GetInvocationList())
            {
                try { ((Action<bool>)callback)(scanning); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
    }
}
