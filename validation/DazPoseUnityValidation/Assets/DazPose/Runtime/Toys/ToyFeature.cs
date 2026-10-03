using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DazPose.Toys
{
    /// <summary>Immutable game-owned snapshot of one independently addressable device feature.</summary>
    public sealed class ToyFeature
    {
        private readonly ReadOnlyCollection<ToyOutputRange> _outputs;

        public uint FeatureIndex { get; }
        public string Description { get; }
        public IReadOnlyList<ToyOutputRange> Outputs => _outputs;

        public ToyFeature(uint featureIndex, string description, IEnumerable<ToyOutputRange> outputs)
        {
            FeatureIndex = featureIndex;
            Description = description ?? string.Empty;
            _outputs = Array.AsReadOnly((outputs ?? Array.Empty<ToyOutputRange>())
                .OrderBy(output => output.Capability).ToArray());
        }

        public bool Supports(ToyOutputCapability capability) => TryGetOutput(capability, out _);

        public bool TryGetOutput(ToyOutputCapability capability, out ToyOutputRange output)
        {
            for (int i = 0; i < _outputs.Count; i++)
            {
                if (_outputs[i].Capability != capability) continue;
                output = _outputs[i];
                return true;
            }

            output = default;
            return false;
        }
    }
}
