using UnityEngine;

namespace DazPose.Performer.HandGrip
{
    /// <summary>A right-handed, orthonormal frame on a cylindrical grip surface.</summary>
    public readonly struct GripFrame
    {
        private const float DirectionEpsilon = 1e-8f;

        public Vector3 Center { get; }
        public Vector3 Tangent { get; }
        public Vector3 Normal { get; }
        public Vector3 Binormal { get; }
        public float Radius { get; }

        public bool IsValid => IsFinite(Center) && IsFinite(Tangent) && IsFinite(Normal)
            && IsFinite(Binormal) && IsFinite(Radius) && Radius > 0f
            && Mathf.Abs(Tangent.sqrMagnitude - 1f) < 0.001f
            && Mathf.Abs(Normal.sqrMagnitude - 1f) < 0.001f
            && Mathf.Abs(Binormal.sqrMagnitude - 1f) < 0.001f;

        private GripFrame(Vector3 center, Vector3 tangent, Vector3 normal, Vector3 binormal, float radius)
        {
            Center = center;
            Tangent = tangent;
            Normal = normal;
            Binormal = binormal;
            Radius = radius;
        }

        public static bool TryCreate(Vector3 center, Vector3 tangent, Vector3 referenceNormal,
            float radius, out GripFrame frame)
        {
            frame = default;
            if (!IsFinite(center) || !IsFinite(tangent) || !IsFinite(referenceNormal)
                || !IsFinite(radius) || radius <= 0f || tangent.sqrMagnitude <= DirectionEpsilon)
                return false;

            Vector3 normalizedTangent = tangent.normalized;
            Vector3 projectedNormal = referenceNormal
                - normalizedTangent * Vector3.Dot(referenceNormal, normalizedTangent);
            if (projectedNormal.sqrMagnitude <= DirectionEpsilon) return false;

            Vector3 normalizedNormal = projectedNormal.normalized;
            Vector3 binormal = Vector3.Cross(normalizedTangent, normalizedNormal);
            if (binormal.sqrMagnitude <= DirectionEpsilon) return false;
            binormal.Normalize();
            normalizedNormal = Vector3.Cross(binormal, normalizedTangent).normalized;
            frame = new GripFrame(center, normalizedTangent, normalizedNormal, binormal, radius);
            return frame.IsValid;
        }

        public static GripFrame Create(Vector3 center, Vector3 tangent, Vector3 referenceNormal, float radius)
        {
            if (!TryCreate(center, tangent, referenceNormal, radius, out GripFrame frame))
                throw new System.ArgumentException("A grip frame needs finite values, a nonzero tangent, a nonparallel normal and a positive radius.");
            return frame;
        }

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
