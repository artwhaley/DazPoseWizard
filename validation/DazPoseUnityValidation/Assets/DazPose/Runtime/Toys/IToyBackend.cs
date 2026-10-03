using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DazPose.Toys
{
    public enum ToyConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Disconnecting,
        Faulted
    }

    /// <summary>The narrow transport seam used by ToyControlService.</summary>
    internal interface IToyBackend : IDisposable
    {
        ToyConnectionState ConnectionState { get; }
        string StatusMessage { get; }
        string LastError { get; }
        bool IsConnected { get; }
        bool IsScanning { get; }
        IReadOnlyList<ToyDevice> Devices { get; }

        event Action<ToyConnectionState, string> StateChanged;
        event Action<ToyDevice> DeviceAdded;
        event Action<uint> DeviceRemoved;
        event Action<bool> ScanningChanged;

        Task ConnectAsync(string serverAddress);
        Task DisconnectAsync();
        Task StartScanningAsync();
        Task StopScanningAsync();
        Task StopAllAsync();
        Task SendOutputAsync(ToyOutputBinding binding, float normalizedValue, uint durationMilliseconds = 0);
        Task StopFeatureAsync(ToyOutputBinding binding);
    }
}
