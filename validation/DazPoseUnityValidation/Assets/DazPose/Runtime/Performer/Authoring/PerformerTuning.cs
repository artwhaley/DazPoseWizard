using System;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Explicit persistent changes to actor tuning; unspecified properties are untouched.</summary>
    public class PerformerTuning
    {
        public float? InitialExpressionIntensity { get; set; }
        public float? DefaultExpressionBlendTime { get; set; }
        public float? MotionOwnershipBlendSeconds { get; set; }
        public PoseTransition? DefaultPoseTransition { get; set; }
        public bool? BreathingEnabled { get; set; }
        public float? BreathsPerMinute { get; set; }
        public bool HasBreathingCurve { get; set; }
        private AnimationCurve breathingCurve;
        public AnimationCurve BreathingCurve { get => CloneCurve(breathingCurve); set => breathingCurve = CloneCurve(value); }
        public bool? MorphBreathingEnabled { get; set; }
        public float? MorphBreathingStrength { get; set; }
        public float? BreatheStrength { get; set; }
        public float? BreatheBellyStrength { get; set; }
        public bool? BoneBreathingEnabled { get; set; }
        public float? BoneBreathingStrength { get; set; }
        public bool? GazeEnabled { get; set; }
        public bool? HeadGazeEnabled { get; set; }
        public float? HeadGazeWeight { get; set; }
        public float? HeadGazeResponse { get; set; }
        public float? HeadGazeMaxYaw { get; set; }
        public float? HeadGazeMaxPitch { get; set; }
        public bool? EyeGazeEnabled { get; set; }
        public float? EyeGazeWeight { get; set; }
        public float? EyeGazeResponse { get; set; }
        public float? EyeGazeMaxYaw { get; set; }
        public float? EyeGazeMaxPitch { get; set; }
        public float? GazeReleaseResponse { get; set; }
        public float? GazeAcquireToleranceDegrees { get; set; }
        public bool? AttentionLifeEnabled { get; set; }
        public int? AttentionLifeSeed { get; set; }
        public bool? EyeFixationLifeEnabled { get; set; }
        public float? EyeFixationMaxHorizontalDegrees { get; set; }
        public float? EyeFixationMaxVerticalDegrees { get; set; }
        public float? EyeFixationMinimumHoldSeconds { get; set; }
        public float? EyeFixationMaximumHoldSeconds { get; set; }
        public float? EyeFixationCenterBias { get; set; }
        public bool? HeadAttentionLifeEnabled { get; set; }
        public float? HeadAttentionMaxTiltDegrees { get; set; }
        public float? HeadAttentionMaxChinDegrees { get; set; }
        public float? HeadAttentionMinimumHoldSeconds { get; set; }
        public float? HeadAttentionMaximumHoldSeconds { get; set; }
        public float? HeadAttentionTransitionResponse { get; set; }
        public bool? BlinkEnabled { get; set; }
        public float? BlinkStrength { get; set; }
        public float? BlinkMinimumIntervalSeconds { get; set; }
        public float? BlinkMaximumIntervalSeconds { get; set; }
        public float? BlinkCloseDurationSeconds { get; set; }
        public float? BlinkClosedDurationSeconds { get; set; }
        public float? BlinkOpenDurationSeconds { get; set; }

        internal void ApplyTo(SuccubusPerformer actor)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            Validate();
            if(InitialExpressionIntensity.HasValue) actor.InitialExpressionIntensity=InitialExpressionIntensity.Value;
            if (DefaultExpressionBlendTime.HasValue) actor.DefaultExpressionBlendTime = DefaultExpressionBlendTime.Value;
            if (MotionOwnershipBlendSeconds.HasValue) actor.MotionOwnershipBlendSeconds = MotionOwnershipBlendSeconds.Value;
            if (DefaultPoseTransition.HasValue) actor.DefaultPoseTransition = DefaultPoseTransition.Value;
            if (BreathingEnabled.HasValue) actor.BreathingEnabled = BreathingEnabled.Value;
            if (BreathsPerMinute.HasValue) actor.BreathsPerMinute = BreathsPerMinute.Value;
            if (MorphBreathingEnabled.HasValue) actor.MorphBreathingEnabled = MorphBreathingEnabled.Value;
            if (MorphBreathingStrength.HasValue) actor.MorphBreathingStrength = MorphBreathingStrength.Value;
            if (BreatheStrength.HasValue) actor.BreatheStrength = BreatheStrength.Value;
            if (BreatheBellyStrength.HasValue) actor.BreatheBellyStrength = BreatheBellyStrength.Value;
            if (BoneBreathingEnabled.HasValue) actor.BoneBreathingEnabled = BoneBreathingEnabled.Value;
            if (BoneBreathingStrength.HasValue) actor.BoneBreathingStrength = BoneBreathingStrength.Value;
            if (GazeEnabled.HasValue) actor.GazeEnabled = GazeEnabled.Value;
            if (HeadGazeEnabled.HasValue) actor.HeadGazeEnabled = HeadGazeEnabled.Value;
            if (HeadGazeWeight.HasValue) actor.HeadGazeWeight = HeadGazeWeight.Value;
            if (HeadGazeResponse.HasValue) actor.HeadGazeResponse = HeadGazeResponse.Value;
            if (HeadGazeMaxYaw.HasValue) actor.HeadGazeMaxYaw = HeadGazeMaxYaw.Value;
            if (HeadGazeMaxPitch.HasValue) actor.HeadGazeMaxPitch = HeadGazeMaxPitch.Value;
            if (EyeGazeEnabled.HasValue) actor.EyeGazeEnabled = EyeGazeEnabled.Value;
            if (EyeGazeWeight.HasValue) actor.EyeGazeWeight = EyeGazeWeight.Value;
            if (EyeGazeResponse.HasValue) actor.EyeGazeResponse = EyeGazeResponse.Value;
            if (EyeGazeMaxYaw.HasValue) actor.EyeGazeMaxYaw = EyeGazeMaxYaw.Value;
            if (EyeGazeMaxPitch.HasValue) actor.EyeGazeMaxPitch = EyeGazeMaxPitch.Value;
            if (GazeReleaseResponse.HasValue) actor.GazeReleaseResponse = GazeReleaseResponse.Value;
            if (GazeAcquireToleranceDegrees.HasValue) actor.GazeAcquireToleranceDegrees = GazeAcquireToleranceDegrees.Value;
            if (AttentionLifeEnabled.HasValue) actor.AttentionLifeEnabled = AttentionLifeEnabled.Value;
            if (AttentionLifeSeed.HasValue) actor.AttentionLifeSeed = AttentionLifeSeed.Value;
            if (EyeFixationLifeEnabled.HasValue) actor.EyeFixationLifeEnabled = EyeFixationLifeEnabled.Value;
            if (EyeFixationMaxHorizontalDegrees.HasValue) actor.EyeFixationMaxHorizontalDegrees = EyeFixationMaxHorizontalDegrees.Value;
            if (EyeFixationMaxVerticalDegrees.HasValue) actor.EyeFixationMaxVerticalDegrees = EyeFixationMaxVerticalDegrees.Value;
            if (EyeFixationMinimumHoldSeconds.HasValue) actor.EyeFixationMinimumHoldSeconds = EyeFixationMinimumHoldSeconds.Value;
            if (EyeFixationMaximumHoldSeconds.HasValue) actor.EyeFixationMaximumHoldSeconds = EyeFixationMaximumHoldSeconds.Value;
            if (EyeFixationCenterBias.HasValue) actor.EyeFixationCenterBias = EyeFixationCenterBias.Value;
            if (HeadAttentionLifeEnabled.HasValue) actor.HeadAttentionLifeEnabled = HeadAttentionLifeEnabled.Value;
            if (HeadAttentionMaxTiltDegrees.HasValue) actor.HeadAttentionMaxTiltDegrees = HeadAttentionMaxTiltDegrees.Value;
            if (HeadAttentionMaxChinDegrees.HasValue) actor.HeadAttentionMaxChinDegrees = HeadAttentionMaxChinDegrees.Value;
            if (HeadAttentionMinimumHoldSeconds.HasValue) actor.HeadAttentionMinimumHoldSeconds = HeadAttentionMinimumHoldSeconds.Value;
            if (HeadAttentionMaximumHoldSeconds.HasValue) actor.HeadAttentionMaximumHoldSeconds = HeadAttentionMaximumHoldSeconds.Value;
            if (HeadAttentionTransitionResponse.HasValue) actor.HeadAttentionTransitionResponse = HeadAttentionTransitionResponse.Value;
            if (BlinkEnabled.HasValue) actor.BlinkEnabled = BlinkEnabled.Value;
            if (BlinkStrength.HasValue) actor.BlinkStrength = BlinkStrength.Value;
            if (BlinkMinimumIntervalSeconds.HasValue) actor.BlinkMinimumIntervalSeconds = BlinkMinimumIntervalSeconds.Value;
            if (BlinkMaximumIntervalSeconds.HasValue) actor.BlinkMaximumIntervalSeconds = BlinkMaximumIntervalSeconds.Value;
            if (BlinkCloseDurationSeconds.HasValue) actor.BlinkCloseDurationSeconds = BlinkCloseDurationSeconds.Value;
            if (BlinkClosedDurationSeconds.HasValue) actor.BlinkClosedDurationSeconds = BlinkClosedDurationSeconds.Value;
            if (BlinkOpenDurationSeconds.HasValue) actor.BlinkOpenDurationSeconds = BlinkOpenDurationSeconds.Value;
            if (HasBreathingCurve) actor.BreathingCurve = CloneCurve(breathingCurve);
        }

        internal void Validate()
        {
            ValidateFinite(InitialExpressionIntensity, nameof(InitialExpressionIntensity));
            ValidateFinite(DefaultExpressionBlendTime, nameof(DefaultExpressionBlendTime));
            ValidateFinite(MotionOwnershipBlendSeconds, nameof(MotionOwnershipBlendSeconds));
            if (DefaultPoseTransition.HasValue) DefaultPoseTransition.Value.Validate();
            ValidateFinite(BreathsPerMinute, nameof(BreathsPerMinute));
            if (HasBreathingCurve)
            {
                if (breathingCurve == null) throw new ArgumentException("HasBreathingCurve requires BreathingCurve.");
                ValidateCurve(breathingCurve);
            }
            ValidateFinite(MorphBreathingStrength, nameof(MorphBreathingStrength));
            ValidateFinite(BreatheStrength, nameof(BreatheStrength));
            ValidateFinite(BreatheBellyStrength, nameof(BreatheBellyStrength));
            ValidateFinite(BoneBreathingStrength, nameof(BoneBreathingStrength));
            ValidateFinite(HeadGazeWeight, nameof(HeadGazeWeight));
            ValidateFinite(HeadGazeResponse, nameof(HeadGazeResponse));
            ValidateFinite(HeadGazeMaxYaw, nameof(HeadGazeMaxYaw));
            ValidateFinite(HeadGazeMaxPitch, nameof(HeadGazeMaxPitch));
            ValidateFinite(EyeGazeWeight, nameof(EyeGazeWeight));
            ValidateFinite(EyeGazeResponse, nameof(EyeGazeResponse));
            ValidateFinite(EyeGazeMaxYaw, nameof(EyeGazeMaxYaw));
            ValidateFinite(EyeGazeMaxPitch, nameof(EyeGazeMaxPitch));
            ValidateFinite(GazeReleaseResponse, nameof(GazeReleaseResponse));
            ValidateFinite(GazeAcquireToleranceDegrees, nameof(GazeAcquireToleranceDegrees));
            ValidateFinite(EyeFixationMaxHorizontalDegrees, nameof(EyeFixationMaxHorizontalDegrees));
            ValidateFinite(EyeFixationMaxVerticalDegrees, nameof(EyeFixationMaxVerticalDegrees));
            ValidateFinite(EyeFixationMinimumHoldSeconds, nameof(EyeFixationMinimumHoldSeconds));
            ValidateFinite(EyeFixationMaximumHoldSeconds, nameof(EyeFixationMaximumHoldSeconds));
            ValidateFinite(EyeFixationCenterBias, nameof(EyeFixationCenterBias));
            ValidateFinite(HeadAttentionMaxTiltDegrees, nameof(HeadAttentionMaxTiltDegrees));
            ValidateFinite(HeadAttentionMaxChinDegrees, nameof(HeadAttentionMaxChinDegrees));
            ValidateFinite(HeadAttentionMinimumHoldSeconds, nameof(HeadAttentionMinimumHoldSeconds));
            ValidateFinite(HeadAttentionMaximumHoldSeconds, nameof(HeadAttentionMaximumHoldSeconds));
            ValidateFinite(HeadAttentionTransitionResponse, nameof(HeadAttentionTransitionResponse));
            ValidateFinite(BlinkStrength, nameof(BlinkStrength));
            ValidateFinite(BlinkMinimumIntervalSeconds, nameof(BlinkMinimumIntervalSeconds));
            ValidateFinite(BlinkMaximumIntervalSeconds, nameof(BlinkMaximumIntervalSeconds));
            ValidateFinite(BlinkCloseDurationSeconds, nameof(BlinkCloseDurationSeconds));
            ValidateFinite(BlinkClosedDurationSeconds, nameof(BlinkClosedDurationSeconds));
            ValidateFinite(BlinkOpenDurationSeconds, nameof(BlinkOpenDurationSeconds));
        }

        private static void ValidateFinite(float? value, string name)
        {
            if (value.HasValue && (float.IsNaN(value.Value) || float.IsInfinity(value.Value)))
                throw new ArgumentOutOfRangeException(name, "Value must be finite.");
        }

        private static void ValidateCurve(AnimationCurve curve)
        {
            foreach (Keyframe key in curve.keys)
                if (float.IsNaN(key.time) || float.IsInfinity(key.time) || float.IsNaN(key.value)
                    || float.IsInfinity(key.value) || float.IsNaN(key.inTangent) || float.IsInfinity(key.inTangent)
                    || float.IsNaN(key.outTangent) || float.IsInfinity(key.outTangent))
                    throw new ArgumentOutOfRangeException(nameof(BreathingCurve), "Curve keys must be finite.");
        }

        private static AnimationCurve CloneCurve(AnimationCurve source)
        {
            if (source == null) return null;
            return new AnimationCurve(source.keys) { preWrapMode = source.preWrapMode, postWrapMode = source.postWrapMode };
        }
    }
}
