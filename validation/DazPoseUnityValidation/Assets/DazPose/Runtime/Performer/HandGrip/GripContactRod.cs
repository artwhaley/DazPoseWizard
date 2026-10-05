using UnityEngine;

namespace DazPose.Performer.HandGrip
{
    /// <summary>Stable analytic cylinder fixture used by the hand-grip acceptance scene.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Performer/Hand Grip/Grip Contact Rod")]
    public sealed class GripContactRod : MonoBehaviour, IGripTarget
    {
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform endPoint;
        [SerializeField, Min(0f)] private float radius = 0.035f;
        [SerializeField] private Transform referenceTransform;
        [SerializeField] private Vector3 referenceNormalLocal = Vector3.up;
        [SerializeField, Range(0f, 1f)] private float currentPosition01 = 0.5f;
        [SerializeField] private bool drawGizmos = true;

        public Transform StartPoint { get => startPoint; set => startPoint = value; }
        public Transform EndPoint { get => endPoint; set => endPoint = value; }
        public Transform ReferenceTransform { get => referenceTransform; set => referenceTransform = value; }
        public Vector3 ReferenceNormalLocal { get => referenceNormalLocal; set => referenceNormalLocal = value; }
        public float CurrentPosition01 { get => currentPosition01; set => currentPosition01 = Mathf.Clamp01(value); }

        public float Radius
        {
            get => radius;
            set => radius = value;
        }

        public GripFrame Evaluate(float position01)
        {
            if (!TryEvaluate(position01, out GripFrame frame, out string reason))
                throw new System.InvalidOperationException("GripContactRod is invalid: " + reason);
            return frame;
        }

        public bool TryEvaluate(float position01, out GripFrame frame, out string reason)
        {
            frame = default;
            if (startPoint == null || endPoint == null)
            {
                reason = "Both start and end transforms must be assigned.";
                return false;
            }
            Vector3 delta = endPoint.position - startPoint.position;
            if (delta.sqrMagnitude <= 1e-8f)
            {
                reason = "The rod start and end positions must differ.";
                return false;
            }
            if (!GripFrame.IsFinite(radius) || radius <= 0f)
            {
                reason = "Radius must be finite and positive.";
                return false;
            }

            Transform normalFrame = referenceTransform != null ? referenceTransform : transform;
            Vector3 referenceNormal = normalFrame.TransformDirection(referenceNormalLocal);
            if (!GripFrame.TryCreate(Vector3.Lerp(startPoint.position, endPoint.position,
                    Mathf.Clamp01(position01)), delta.normalized, referenceNormal, radius, out frame))
            {
                reason = "The reference normal is degenerate or parallel to the rod tangent.";
                return false;
            }
            reason = null;
            return true;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos || startPoint == null || endPoint == null) return;
            if (!TryEvaluate(currentPosition01, out GripFrame frame, out _))
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(startPoint.position, endPoint.position);
                Gizmos.DrawSphere(startPoint.position, 0.012f);
                Gizmos.DrawSphere(endPoint.position, 0.012f);
                return;
            }

            Gizmos.color = new Color(0.15f, 0.75f, 1f, 1f);
            Gizmos.DrawLine(startPoint.position, endPoint.position);
            Gizmos.DrawSphere(startPoint.position, 0.012f);
            Gizmos.DrawSphere(endPoint.position, 0.012f);
            DrawRing(startPoint.position, frame.Tangent, frame.Normal, frame.Binormal, frame.Radius);
            DrawRing(endPoint.position, frame.Tangent, frame.Normal, frame.Binormal, frame.Radius);
            DrawRing(frame.Center, frame.Tangent, frame.Normal, frame.Binormal, frame.Radius);
            Gizmos.color = new Color(1f, 0.65f, 0.1f, 1f);
            Gizmos.DrawLine(startPoint.position + frame.Normal * frame.Radius,
                endPoint.position + frame.Normal * frame.Radius);
            Gizmos.DrawLine(startPoint.position - frame.Normal * frame.Radius,
                endPoint.position - frame.Normal * frame.Radius);
            Gizmos.DrawLine(startPoint.position + frame.Binormal * frame.Radius,
                endPoint.position + frame.Binormal * frame.Radius);
            Gizmos.DrawLine(startPoint.position - frame.Binormal * frame.Radius,
                endPoint.position - frame.Binormal * frame.Radius);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(frame.Center, frame.Center + frame.Tangent * 0.12f);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(frame.Center, frame.Center + frame.Normal * 0.12f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(frame.Center, frame.Center + frame.Binormal * 0.12f);
        }

        private void DrawRing(Vector3 center, Vector3 tangent, Vector3 normal, Vector3 binormal, float ringRadius)
        {
            const int segments = 32;
            Gizmos.color = new Color(1f, 0.65f, 0.1f, 1f);
            Vector3 previous = center + normal * ringRadius;
            for (int index = 1; index <= segments; index++)
            {
                float angle = index * (Mathf.PI * 2f / segments);
                Vector3 next = center + (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * ringRadius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
            Gizmos.DrawLine(center - tangent * 0.035f, center + tangent * 0.035f);
        }
    }
}
