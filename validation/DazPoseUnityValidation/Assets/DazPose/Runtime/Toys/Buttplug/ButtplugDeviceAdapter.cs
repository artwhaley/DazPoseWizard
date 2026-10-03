using System.Collections.Generic;
using System.Linq;
using Buttplug.Client;
using Buttplug.Core.Messages;

namespace DazPose.Toys.Buttplug
{
    /// <summary>Projects only the supported output capabilities into game-owned snapshots.</summary>
    internal static class ButtplugDeviceAdapter
    {
        private static readonly OutputType[] SupportedOutputs =
        {
            OutputType.Vibrate,
            OutputType.Oscillate,
            OutputType.Position,
            OutputType.HwPositionWithDuration
        };

        public static ToyDevice CreateSnapshot(ButtplugClientDevice device)
        {
            var features = new List<ToyFeature>();
            foreach (ButtplugClientDeviceFeature source in device.Features.Values.OrderBy(feature => feature.FeatureIndex))
            {
                var outputs = new List<ToyOutputRange>();
                foreach (OutputType outputType in SupportedOutputs)
                {
                    if (!source.HasOutput(outputType)) continue;
                    ToyOutputCapability capability = ToCapability(outputType);
                    bool hasValueRange = source.TryGetOutputRange(outputType, out int minimum, out int maximum);
                    if (hasValueRange && minimum > maximum) hasValueRange = false;

                    if (outputType == OutputType.HwPositionWithDuration)
                    {
                        DeviceFeatureOutput definition = source.FeatureDefinition.GetOutput(outputType);
                        int[] duration = definition != null ? definition.Duration : null;
                        bool hasDurationRange = duration != null && duration.Length >= 2
                            && duration[0] <= duration[1];
                        outputs.Add(ToyOutputRange.PositionWithDurationRanges(hasValueRange,
                            minimum, maximum, hasDurationRange,
                            hasDurationRange ? duration[0] : 0,
                            hasDurationRange ? duration[1] : 0));
                    }
                    else if (hasValueRange)
                    {
                        outputs.Add(ToyOutputRange.Value(capability, minimum, maximum));
                    }
                    else
                    {
                        outputs.Add(ToyOutputRange.Unknown(capability));
                    }
                }

                features.Add(new ToyFeature(source.FeatureIndex, source.FeatureDescription, outputs));
            }

            return new ToyDevice(device.Index, device.Name, device.DisplayName,
                device.MessageTimingGap, features);
        }

        internal static bool TryToCapability(OutputType outputType, out ToyOutputCapability capability)
        {
            switch (outputType)
            {
                case OutputType.Vibrate: capability = ToyOutputCapability.Vibrate; return true;
                case OutputType.Oscillate: capability = ToyOutputCapability.Oscillate; return true;
                case OutputType.Position: capability = ToyOutputCapability.Position; return true;
                case OutputType.HwPositionWithDuration:
                    capability = ToyOutputCapability.HwPositionWithDuration;
                    return true;
                default:
                    capability = default;
                    return false;
            }
        }

        private static ToyOutputCapability ToCapability(OutputType outputType)
        {
            if (TryToCapability(outputType, out ToyOutputCapability capability)) return capability;
            throw new System.ArgumentOutOfRangeException(nameof(outputType));
        }
    }
}
