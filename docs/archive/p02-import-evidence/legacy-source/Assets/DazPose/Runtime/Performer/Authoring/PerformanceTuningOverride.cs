using System;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Nullable, actor-property-only tuning changes owned by one performance run.</summary>
    public sealed class PerformanceTuningOverride
    {
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

        internal Action ApplyTo(SuccubusPerformer actor)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            Validate();
            var rollback = new List<Action>();
            var cleanup = new List<Action>();
            try
            {
                Apply(ref rollback, ref cleanup, () => actor.DefaultExpressionBlendTime, value => actor.DefaultExpressionBlendTime = value, DefaultExpressionBlendTime);
                Apply(ref rollback, ref cleanup, () => actor.MotionOwnershipBlendSeconds, value => actor.MotionOwnershipBlendSeconds = value, MotionOwnershipBlendSeconds);
                Apply(ref rollback, ref cleanup, () => actor.DefaultPoseTransition, value => actor.DefaultPoseTransition = value, DefaultPoseTransition, PoseTransitionEquals);
                Apply(ref rollback, ref cleanup, () => actor.BreathingEnabled, value => actor.BreathingEnabled = value, BreathingEnabled);
                Apply(ref rollback, ref cleanup, () => actor.BreathsPerMinute, value => actor.BreathsPerMinute = value, BreathsPerMinute);
                if (HasBreathingCurve) ApplyCurve(actor, ref rollback, ref cleanup);
                Apply(ref rollback, ref cleanup, () => actor.MorphBreathingEnabled, value => actor.MorphBreathingEnabled = value, MorphBreathingEnabled);
                Apply(ref rollback, ref cleanup, () => actor.MorphBreathingStrength, value => actor.MorphBreathingStrength = value, MorphBreathingStrength);
                Apply(ref rollback, ref cleanup, () => actor.BreatheStrength, value => actor.BreatheStrength = value, BreatheStrength);
                Apply(ref rollback, ref cleanup, () => actor.BreatheBellyStrength, value => actor.BreatheBellyStrength = value, BreatheBellyStrength);
                Apply(ref rollback, ref cleanup, () => actor.BoneBreathingEnabled, value => actor.BoneBreathingEnabled = value, BoneBreathingEnabled);
                Apply(ref rollback, ref cleanup, () => actor.BoneBreathingStrength, value => actor.BoneBreathingStrength = value, BoneBreathingStrength);
                Apply(ref rollback, ref cleanup, () => actor.GazeEnabled, value => actor.GazeEnabled = value, GazeEnabled);
                Apply(ref rollback, ref cleanup, () => actor.HeadGazeEnabled, value => actor.HeadGazeEnabled = value, HeadGazeEnabled);
                Apply(ref rollback, ref cleanup, () => actor.HeadGazeWeight, value => actor.HeadGazeWeight = value, HeadGazeWeight);
                Apply(ref rollback, ref cleanup, () => actor.HeadGazeResponse, value => actor.HeadGazeResponse = value, HeadGazeResponse);
                Apply(ref rollback, ref cleanup, () => actor.HeadGazeMaxYaw, value => actor.HeadGazeMaxYaw = value, HeadGazeMaxYaw);
                Apply(ref rollback, ref cleanup, () => actor.HeadGazeMaxPitch, value => actor.HeadGazeMaxPitch = value, HeadGazeMaxPitch);
                Apply(ref rollback, ref cleanup, () => actor.EyeGazeEnabled, value => actor.EyeGazeEnabled = value, EyeGazeEnabled);
                Apply(ref rollback, ref cleanup, () => actor.EyeGazeWeight, value => actor.EyeGazeWeight = value, EyeGazeWeight);
                Apply(ref rollback, ref cleanup, () => actor.EyeGazeResponse, value => actor.EyeGazeResponse = value, EyeGazeResponse);
                Apply(ref rollback, ref cleanup, () => actor.EyeGazeMaxYaw, value => actor.EyeGazeMaxYaw = value, EyeGazeMaxYaw);
                Apply(ref rollback, ref cleanup, () => actor.EyeGazeMaxPitch, value => actor.EyeGazeMaxPitch = value, EyeGazeMaxPitch);
                Apply(ref rollback, ref cleanup, () => actor.GazeReleaseResponse, value => actor.GazeReleaseResponse = value, GazeReleaseResponse);
                Apply(ref rollback, ref cleanup, () => actor.GazeAcquireToleranceDegrees, value => actor.GazeAcquireToleranceDegrees = value, GazeAcquireToleranceDegrees);
                Apply(ref rollback, ref cleanup, () => actor.AttentionLifeEnabled, value => actor.AttentionLifeEnabled = value, AttentionLifeEnabled);
                Apply(ref rollback, ref cleanup, () => actor.AttentionLifeSeed, value => actor.AttentionLifeSeed = value, AttentionLifeSeed);
                Apply(ref rollback, ref cleanup, () => actor.EyeFixationLifeEnabled, value => actor.EyeFixationLifeEnabled = value, EyeFixationLifeEnabled);
                Apply(ref rollback, ref cleanup, () => actor.EyeFixationMaxHorizontalDegrees, value => actor.EyeFixationMaxHorizontalDegrees = value, EyeFixationMaxHorizontalDegrees);
                Apply(ref rollback, ref cleanup, () => actor.EyeFixationMaxVerticalDegrees, value => actor.EyeFixationMaxVerticalDegrees = value, EyeFixationMaxVerticalDegrees);
                ApplyPair(ref rollback, ref cleanup, () => actor.EyeFixationMinimumHoldSeconds, value => actor.EyeFixationMinimumHoldSeconds = value, () => actor.EyeFixationMaximumHoldSeconds, value => actor.EyeFixationMaximumHoldSeconds = value, EyeFixationMinimumHoldSeconds, EyeFixationMaximumHoldSeconds);
                Apply(ref rollback, ref cleanup, () => actor.EyeFixationCenterBias, value => actor.EyeFixationCenterBias = value, EyeFixationCenterBias);
                Apply(ref rollback, ref cleanup, () => actor.HeadAttentionLifeEnabled, value => actor.HeadAttentionLifeEnabled = value, HeadAttentionLifeEnabled);
                Apply(ref rollback, ref cleanup, () => actor.HeadAttentionMaxTiltDegrees, value => actor.HeadAttentionMaxTiltDegrees = value, HeadAttentionMaxTiltDegrees);
                Apply(ref rollback, ref cleanup, () => actor.HeadAttentionMaxChinDegrees, value => actor.HeadAttentionMaxChinDegrees = value, HeadAttentionMaxChinDegrees);
                ApplyPair(ref rollback, ref cleanup, () => actor.HeadAttentionMinimumHoldSeconds, value => actor.HeadAttentionMinimumHoldSeconds = value, () => actor.HeadAttentionMaximumHoldSeconds, value => actor.HeadAttentionMaximumHoldSeconds = value, HeadAttentionMinimumHoldSeconds, HeadAttentionMaximumHoldSeconds);
                Apply(ref rollback, ref cleanup, () => actor.HeadAttentionTransitionResponse, value => actor.HeadAttentionTransitionResponse = value, HeadAttentionTransitionResponse);
                Apply(ref rollback, ref cleanup, () => actor.BlinkEnabled, value => actor.BlinkEnabled = value, BlinkEnabled);
                Apply(ref rollback, ref cleanup, () => actor.BlinkStrength, value => actor.BlinkStrength = value, BlinkStrength);
                ApplyPair(ref rollback, ref cleanup, () => actor.BlinkMinimumIntervalSeconds, value => actor.BlinkMinimumIntervalSeconds = value, () => actor.BlinkMaximumIntervalSeconds, value => actor.BlinkMaximumIntervalSeconds = value, BlinkMinimumIntervalSeconds, BlinkMaximumIntervalSeconds);
                Apply(ref rollback, ref cleanup, () => actor.BlinkCloseDurationSeconds, value => actor.BlinkCloseDurationSeconds = value, BlinkCloseDurationSeconds);
                Apply(ref rollback, ref cleanup, () => actor.BlinkClosedDurationSeconds, value => actor.BlinkClosedDurationSeconds = value, BlinkClosedDurationSeconds);
                Apply(ref rollback, ref cleanup, () => actor.BlinkOpenDurationSeconds, value => actor.BlinkOpenDurationSeconds = value, BlinkOpenDurationSeconds);
            }
            catch
            {
                for (int i = rollback.Count - 1; i >= 0; --i) try { rollback[i](); } catch { }
                throw;
            }
            return () =>
            {
                for (int i = cleanup.Count - 1; i >= 0; --i)
                    try { cleanup[i](); } catch (Exception exception) { Debug.LogWarning("Tuning restore failed: " + exception.Message, actor); }
            };
        }

        private void ApplyCurve(SuccubusPerformer actor, ref List<Action> rollback, ref List<Action> cleanup)
        {
            AnimationCurve original = actor.BreathingCurve;
            AnimationCurve requested = CloneCurve(breathingCurve);
            rollback.Add(() => actor.BreathingCurve = original);
            actor.BreathingCurve = requested;
            AnimationCurve applied = actor.BreathingCurve;
            AnimationCurve fingerprint = CloneCurve(applied);
            cleanup.Add(() =>
            {
                if (ReferenceEquals(actor.BreathingCurve, applied) && CurveEquals(actor.BreathingCurve, fingerprint))
                    actor.BreathingCurve = original;
            });
        }

        private static void Apply<T>(ref List<Action> rollback, ref List<Action> cleanup,
            Func<T> get, Action<T> set, T? requested) where T : struct
        {
            if (!requested.HasValue) return;
            T original = get();
            rollback.Add(() => set(original));
            set(requested.Value);
            T applied = get();
            cleanup.Add(() => { if (EqualityComparer<T>.Default.Equals(get(), applied)) set(original); });
        }

        private static void Apply(ref List<Action> rollback, ref List<Action> cleanup,
            Func<PoseTransition> get, Action<PoseTransition> set, PoseTransition? requested,
            Func<PoseTransition, PoseTransition, bool> equals)
        {
            if (!requested.HasValue) return;
            PoseTransition original = get();
            rollback.Add(() => set(original));
            set(requested.Value);
            PoseTransition applied = get();
            cleanup.Add(() => { if (equals(get(), applied)) set(original); });
        }

        private static void ApplyPair(ref List<Action> rollback, ref List<Action> cleanup,
            Func<float> getMin, Action<float> setMin, Func<float> getMax, Action<float> setMax,
            float? requestedMin, float? requestedMax)
        {
            if (!requestedMin.HasValue && !requestedMax.HasValue) return;
            float originalMin = getMin(), originalMax = getMax();
            rollback.Add(() => { setMin(originalMin); setMax(originalMax); });
            if (requestedMin.HasValue) setMin(requestedMin.Value);
            if (requestedMax.HasValue) setMax(requestedMax.Value);
            float appliedMin = getMin(), appliedMax = getMax();
            cleanup.Add(() =>
            {
                if (Mathf.Approximately(getMin(), appliedMin) && Mathf.Approximately(getMax(), appliedMax))
                { setMin(originalMin); setMax(originalMax); }
            });
        }

        internal void Validate()
        {
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

        private static bool PoseTransitionEquals(PoseTransition left, PoseTransition right) =>
            Mathf.Approximately(left.Duration, right.Duration) && Mathf.Approximately(left.Windup, right.Windup)
            && Mathf.Approximately(left.Overshoot, right.Overshoot) && CurveEquals(left.Curve, right.Curve);

        private static bool CurveEquals(AnimationCurve left, AnimationCurve right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.preWrapMode != right.preWrapMode || left.postWrapMode != right.postWrapMode) return false;
            Keyframe[] a = left.keys, b = right.keys;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; ++i)
                if (!a[i].time.Equals(b[i].time) || !a[i].value.Equals(b[i].value)
                    || !a[i].inTangent.Equals(b[i].inTangent) || !a[i].outTangent.Equals(b[i].outTangent)
                    || !a[i].inWeight.Equals(b[i].inWeight) || !a[i].outWeight.Equals(b[i].outWeight)
                    || a[i].weightedMode != b[i].weightedMode || a[i].tangentMode != b[i].tangentMode) return false;
            return true;
        }

        private static AnimationCurve CloneCurve(AnimationCurve source)
        {
            if (source == null) return null;
            return new AnimationCurve(source.keys) { preWrapMode = source.preWrapMode, postWrapMode = source.postWrapMode };
        }
    }
}
