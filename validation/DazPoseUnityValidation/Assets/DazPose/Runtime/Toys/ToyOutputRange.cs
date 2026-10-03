using System;

namespace DazPose.Toys
{
    /// <summary>Advertised value and, where applicable, duration limits for one output.</summary>
    public readonly struct ToyOutputRange
    {
        public ToyOutputCapability Capability { get; }
        public bool HasValueRange { get; }
        public int MinimumValue { get; }
        public int MaximumValue { get; }
        public bool HasDurationRange { get; }
        public int MinimumDurationMilliseconds { get; }
        public int MaximumDurationMilliseconds { get; }

        private ToyOutputRange(ToyOutputCapability capability, bool hasValueRange, int minimumValue,
            int maximumValue, bool hasDurationRange, int minimumDurationMilliseconds,
            int maximumDurationMilliseconds)
        {
            Capability = capability;
            HasValueRange = hasValueRange;
            MinimumValue = minimumValue;
            MaximumValue = maximumValue;
            HasDurationRange = hasDurationRange;
            MinimumDurationMilliseconds = minimumDurationMilliseconds;
            MaximumDurationMilliseconds = maximumDurationMilliseconds;
        }

        public static ToyOutputRange Unknown(ToyOutputCapability capability) =>
            new ToyOutputRange(capability, false, 0, 0, false, 0, 0);

        public static ToyOutputRange Value(ToyOutputCapability capability, int minimum, int maximum)
        {
            if (minimum > maximum) throw new ArgumentOutOfRangeException(nameof(minimum));
            return new ToyOutputRange(capability, true, minimum, maximum, false, 0, 0);
        }

        public static ToyOutputRange PositionWithDuration(int minimumPosition, int maximumPosition,
            int minimumDurationMilliseconds, int maximumDurationMilliseconds)
        {
            return PositionWithDurationRanges(true, minimumPosition, maximumPosition, true,
                minimumDurationMilliseconds, maximumDurationMilliseconds);
        }

        public static ToyOutputRange PositionWithDurationValueOnly(int minimumPosition, int maximumPosition)
        {
            return PositionWithDurationRanges(true, minimumPosition, maximumPosition, false, 0, 0);
        }

        public static ToyOutputRange PositionWithDurationRanges(bool hasValueRange, int minimumPosition,
            int maximumPosition, bool hasDurationRange, int minimumDurationMilliseconds,
            int maximumDurationMilliseconds)
        {
            if (hasValueRange && minimumPosition > maximumPosition)
                throw new ArgumentOutOfRangeException(nameof(minimumPosition));
            if (hasDurationRange && minimumDurationMilliseconds > maximumDurationMilliseconds)
                throw new ArgumentOutOfRangeException(nameof(minimumDurationMilliseconds));
            return new ToyOutputRange(ToyOutputCapability.HwPositionWithDuration, hasValueRange,
                minimumPosition, maximumPosition, hasDurationRange,
                minimumDurationMilliseconds, maximumDurationMilliseconds);
        }
    }
}
