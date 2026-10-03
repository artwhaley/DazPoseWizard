using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DazPose.Toys
{
    /// <summary>Small index-keyed registry of immutable runtime device snapshots.</summary>
    internal sealed class ToyDeviceRegistry
    {
        private readonly object _gate = new object();
        private readonly SortedDictionary<uint, ToyDevice> _devices = new SortedDictionary<uint, ToyDevice>();

        public IReadOnlyList<ToyDevice> Snapshot
        {
            get
            {
                lock (_gate) return new ReadOnlyCollection<ToyDevice>(_devices.Values.ToArray());
            }
        }

        public int Count
        {
            get { lock (_gate) return _devices.Count; }
        }

        public bool TryGet(uint deviceIndex, out ToyDevice device)
        {
            lock (_gate) return _devices.TryGetValue(deviceIndex, out device);
        }

        public void Set(ToyDevice device)
        {
            if (device == null) return;
            lock (_gate) _devices[device.DeviceIndex] = device;
        }

        public bool Remove(uint deviceIndex, out ToyDevice device)
        {
            lock (_gate)
            {
                if (!_devices.TryGetValue(deviceIndex, out device)) return false;
                _devices.Remove(deviceIndex);
                return true;
            }
        }

        public ToyDevice[] Clear()
        {
            lock (_gate)
            {
                ToyDevice[] removed = _devices.Values.ToArray();
                _devices.Clear();
                return removed;
            }
        }
    }
}
