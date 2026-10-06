using System;
using UnityEngine;

namespace DazPose.Performer.HandGrip
{
    public enum GripPlanFailure { None, InvalidTarget, VerticalReachImpossible, NoReachableStagingPose }

    public readonly struct GripReachEnvelope
    {
        public readonly float ArmLength;
        public readonly float Minimum;
        public readonly float Maximum;
        public readonly float Preferred;

        public GripReachEnvelope(float upper, float lower, float minimumFraction = 0.2f,
            float maximumFraction = 0.95f, float preferredFraction = 0.8f)
        {
            if (!GripFrame.IsFinite(upper) || !GripFrame.IsFinite(lower) || upper <= 0f || lower <= 0f
                || !GripFrame.IsFinite(minimumFraction) || !GripFrame.IsFinite(maximumFraction)
                || !GripFrame.IsFinite(preferredFraction) || minimumFraction < 0f
                || maximumFraction >= 1f || maximumFraction <= minimumFraction
                || preferredFraction < minimumFraction || preferredFraction > maximumFraction)
                throw new ArgumentException("Arm lengths and safe reach fractions must define a finite nonempty envelope.");
            ArmLength = upper + lower;
            Minimum = Mathf.Max(ArmLength * minimumFraction, Mathf.Abs(upper - lower) + ArmLength * 0.01f);
            Maximum = ArmLength * maximumFraction;
            Preferred = ArmLength * preferredFraction;
            if (Minimum >= Maximum) throw new ArgumentException("Arm cannot satisfy the configured safe envelope.");
        }

        public bool Contains(float distance) => GripFrame.IsFinite(distance)
            && distance >= Minimum && distance <= Maximum;
    }

    public readonly struct GripReachPlan
    {
        public readonly bool Valid;
        public readonly bool RequiresWalking;
        public readonly Pose Root;
        public readonly float Margin;
        public readonly GripPlanFailure Failure;

        public GripReachPlan(bool valid, bool walk, Pose root, float margin, GripPlanFailure failure)
        { Valid = valid; RequiresWalking = walk; Root = root; Margin = margin; Failure = failure; }
    }

    /// <summary>No root animation, navigation, clocks, or allocations. Target samples are cached per plan.</summary>
    public sealed class GripReachPlanner
    {
        // General targets are sampled, not mathematically certified. Targets may opt into exact
        // whole-span reach certification through IGripTargetStraightSpan.
        public const int SampleCount = 17;
        private readonly Vector3[] _wrists = new Vector3[SampleCount];
        public Vector3 GetSampleWrist(int index) => _wrists[index];

        public GripReachPlan Plan(Pose root, Vector3 shoulderLocal, GripReachEnvelope envelope,
            IGripTarget target, Vector3 anchorPosition, Quaternion anchorRotation,
            Quaternion calibration, float clearance, float twistDegrees = 0f, bool searchStaging = true)
        {
            GripFrame mid;
            bool certifySegment = false;
            try
            {
                mid = target.Evaluate(0.5f);
                for (int index = 0; index < SampleCount; index++)
                    _wrists[index] = GripPalmTarget.Evaluate(target.Evaluate(index / (float)(SampleCount - 1)),
                        anchorPosition, anchorRotation, calibration, clearance, twistDegrees).Wrist.position;
                if (target is IGripTargetStraightSpan straightSpan
                    && straightSpan.TryGetStraightSpan(out GripFrame startFrame, out GripFrame endFrame))
                    certifySegment = MatchesCertifiedSpan(startFrame, endFrame, anchorPosition,
                        anchorRotation, calibration, clearance, twistDegrees);
            }
            catch (Exception)
            { return Failed(root, GripPlanFailure.InvalidTarget); }

            Vector3 shoulder = root.position + root.rotation * shoulderLocal;
            if (Check(shoulder, envelope, certifySegment, out float margin, out _))
                return new GripReachPlan(true, false, root, margin, GripPlanFailure.None);

            if (!searchStaging) return Failed(root, GripPlanFailure.NoReachableStagingPose);
            float shoulderHeight = shoulder.y;
            for (int index = 0; index < SampleCount; index++)
                if (Mathf.Abs(_wrists[index].y - shoulderHeight) > envelope.Maximum)
                    return Failed(root, GripPlanFailure.VerticalReachImpossible);

            Vector3 side = Vector3.ProjectOnPlane(mid.Normal, Vector3.up);
            if (side.sqrMagnitude < 1e-8f) side = Vector3.ProjectOnPlane(root.position - mid.Center, Vector3.up);
            if (side.sqrMagnitude < 1e-8f) side = -(root.rotation * Vector3.forward);
            side.Normalize();
            Vector3 lateral = Vector3.Cross(Vector3.up, side);
            Pose best = root;
            float bestScore = float.NegativeInfinity;
            float bestMargin = 0f;
            // 13 distances × 7 lateral offsets × 5 yaw offsets; stable iteration resolves ties.
            for (int distanceIndex = 0; distanceIndex < 13; distanceIndex++)
            for (int lateralIndex = -3; lateralIndex <= 3; lateralIndex++)
            for (int yawIndex = -2; yawIndex <= 2; yawIndex++)
            {
                Vector3 position = mid.Center + side * (envelope.ArmLength * (0.3f + distanceIndex * 0.075f))
                    + lateral * (lateralIndex * envelope.ArmLength * 0.1f);
                position.y = root.position.y;
                Vector3 facing = Vector3.ProjectOnPlane(mid.Center - position, Vector3.up);
                if (facing.sqrMagnitude < 1e-8f) continue;
                Quaternion rotation = Quaternion.AngleAxis(yawIndex * 10f, Vector3.up)
                    * Quaternion.LookRotation(facing.normalized, Vector3.up);
                shoulder = position + rotation * shoulderLocal;
                if (!Check(shoulder, envelope, certifySegment, out margin, out float preferredError)) continue;
                float walk = Vector3.Distance(position, root.position) / envelope.ArmLength;
                float yaw = Quaternion.Angle(rotation, root.rotation) / 180f;
                float score = margin / envelope.ArmLength * 100f - preferredError / envelope.ArmLength * 2f
                    - walk * 0.15f - yaw * 0.1f;
                if (score <= bestScore) continue;
                bestScore = score; best = new Pose(position, rotation); bestMargin = margin;
            }
            return float.IsNegativeInfinity(bestScore)
                ? Failed(root, GripPlanFailure.NoReachableStagingPose)
                : new GripReachPlan(true, true, best, bestMargin, GripPlanFailure.None);
        }

        private bool MatchesCertifiedSpan(GripFrame start, GripFrame end, Vector3 anchorPosition,
            Quaternion anchorRotation, Quaternion calibration, float clearance, float twistDegrees)
        {
            if (!start.IsValid || !end.IsValid
                || (end.Center - start.Center).sqrMagnitude <= 1e-12f
                || (start.Tangent - end.Tangent).sqrMagnitude > 1e-6f
                || (start.Normal - end.Normal).sqrMagnitude > 1e-6f
                || (start.Binormal - end.Binormal).sqrMagnitude > 1e-6f
                || Mathf.Abs(start.Radius - end.Radius) > 0.00001f)
                return false;

            Vector3 startWrist = GripPalmTarget.Evaluate(start, anchorPosition, anchorRotation,
                calibration, clearance, twistDegrees).Wrist.position;
            Vector3 endWrist = GripPalmTarget.Evaluate(end, anchorPosition, anchorRotation,
                calibration, clearance, twistDegrees).Wrist.position;
            const float pathToleranceMeters = 0.0001f;
            if (Vector3.Distance(_wrists[0], startWrist) > pathToleranceMeters
                || Vector3.Distance(_wrists[SampleCount - 1], endWrist) > pathToleranceMeters)
                return false;

            // Sanity-check the provider's claimed certificate against every planning sample.
            // The capability contract, not finite sampling, remains the whole-span guarantee.
            for (int index = 1; index < SampleCount - 1; index++)
            {
                Vector3 expected = Vector3.Lerp(startWrist, endWrist,
                    index / (float)(SampleCount - 1));
                if (Vector3.Distance(_wrists[index], expected) > pathToleranceMeters) return false;
            }
            return true;
        }

        private bool Check(Vector3 shoulder, GripReachEnvelope envelope, bool certifySegment,
            out float margin, out float preferredError)
        {
            margin = float.PositiveInfinity; preferredError = 0f;
            for (int index = 0; index < SampleCount; index++)
            {
                float distance = Vector3.Distance(shoulder, _wrists[index]);
                if (!envelope.Contains(distance)) return false;
                margin = Mathf.Min(margin, Mathf.Min(distance - envelope.Minimum, envelope.Maximum - distance));
                preferredError += Mathf.Abs(distance - envelope.Preferred) / SampleCount;
            }
            if (certifySegment)
            {
                Vector3 segment = _wrists[SampleCount - 1] - _wrists[0];
                float t = segment.sqrMagnitude <= 1e-12f ? 0f
                    : Mathf.Clamp01(Vector3.Dot(shoulder - _wrists[0], segment) / segment.sqrMagnitude);
                float minimum = Vector3.Distance(shoulder, _wrists[0] + segment * t);
                if (minimum < envelope.Minimum) return false;
                margin = Mathf.Min(margin, minimum - envelope.Minimum);
            }
            return true;
        }

        private static GripReachPlan Failed(Pose root, GripPlanFailure failure)
            => new GripReachPlan(false, false, root, 0f, failure);
    }
}
