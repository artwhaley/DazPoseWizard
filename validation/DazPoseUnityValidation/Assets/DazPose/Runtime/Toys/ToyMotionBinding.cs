using System;
using UnityEngine;

namespace DazPose.Toys
{
    public enum ToyMotionStrategy { Auto, Position, HwPositionWithDuration }

    [Serializable]
    public sealed class ToyMotionBinding
    {
        public ToyOutputBinding Output { get; }
        public ToyMotionStrategy Strategy { get; set; }
        public bool Invert { get; set; }
        public float Minimum { get; set; }
        public float Maximum { get; set; }

        public ToyMotionBinding(ToyOutputBinding output, ToyMotionStrategy strategy = ToyMotionStrategy.Auto,
            bool invert = false, float minimum = 0f, float maximum = 1f)
        {
            Output = output;
            Strategy = strategy;
            Invert = invert;
            Minimum = Mathf.Clamp01(minimum);
            Maximum = Mathf.Clamp01(maximum);
            if (Minimum > Maximum) throw new ArgumentException("Minimum motion position must not exceed maximum.");
        }

        public float Map(float position01)
        {
            float clamped = Mathf.Clamp01(position01);
            if (Invert) clamped = 1f - clamped;
            return Mathf.Lerp(Minimum, Maximum, clamped);
        }
    }
}
