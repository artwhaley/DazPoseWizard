using System;
using UnityEngine;

namespace DazPose.Player
{
    /// <summary>Acquisition timing and continuous response for a persistent view track.</summary>
    [Serializable]
    public struct ViewTrackingSettings
    {
        public ViewTransition Acquire;
        [Min(0f)] public float FollowResponseSeconds;

        public static ViewTrackingSettings Default => new ViewTrackingSettings
        {
            Acquire = ViewTransition.EaseInOut(0.75f),
            FollowResponseSeconds = 0.20f
        };

        internal void Validate()
        {
            Acquire.Validate(nameof(Acquire));
            if (float.IsNaN(FollowResponseSeconds) || float.IsInfinity(FollowResponseSeconds)
                || FollowResponseSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(FollowResponseSeconds), FollowResponseSeconds,
                    "Tracking response must be finite and non-negative. Zero follows immediately.");
        }
    }
}
