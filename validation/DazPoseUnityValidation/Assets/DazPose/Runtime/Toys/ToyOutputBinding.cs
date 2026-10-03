using System;

namespace DazPose.Toys
{
    /// <summary>Runtime-only identity for one output on one independently addressable feature.</summary>
    [Serializable]
    public readonly struct ToyOutputBinding : IEquatable<ToyOutputBinding>
    {
        public uint DeviceIndex { get; }
        public uint FeatureIndex { get; }
        public ToyOutputCapability Capability { get; }

        public ToyOutputBinding(uint deviceIndex, uint featureIndex, ToyOutputCapability capability)
        {
            DeviceIndex = deviceIndex;
            FeatureIndex = featureIndex;
            Capability = capability;
        }

        public bool Equals(ToyOutputBinding other) => DeviceIndex == other.DeviceIndex
            && FeatureIndex == other.FeatureIndex && Capability == other.Capability;
        public override bool Equals(object obj) => obj is ToyOutputBinding other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(DeviceIndex, FeatureIndex, (int)Capability);
        public static bool operator ==(ToyOutputBinding left, ToyOutputBinding right) => left.Equals(right);
        public static bool operator !=(ToyOutputBinding left, ToyOutputBinding right) => !left.Equals(right);
        public override string ToString() => "device " + DeviceIndex + "/feature " + FeatureIndex + "/" + Capability;
    }
}
