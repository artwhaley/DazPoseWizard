using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DazPose.Toys
{
    /// <summary>Immutable game-owned snapshot of one device connected to the Buttplug server.</summary>
    public sealed class ToyDevice
    {
        private readonly ReadOnlyCollection<ToyFeature> _features;

        public uint DeviceIndex { get; }
        public string Name { get; }
        public string DisplayName { get; }
        public uint MessageTimingGapMilliseconds { get; }
        public IReadOnlyList<ToyFeature> Features => _features;

        public ToyDevice(uint deviceIndex, string name, string displayName, uint messageTimingGapMilliseconds,
            IEnumerable<ToyFeature> features)
        {
            DeviceIndex = deviceIndex;
            Name = name ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            MessageTimingGapMilliseconds = messageTimingGapMilliseconds;
            _features = Array.AsReadOnly((features ?? Array.Empty<ToyFeature>())
                .OrderBy(feature => feature.FeatureIndex).ToArray());
        }
    }
}
