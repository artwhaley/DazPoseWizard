using UnityEngine;

namespace DazPose.Performer
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Performer/Environment/Seat")]
    public sealed class PerformerSeat : MonoBehaviour
    {
        [SerializeField] private Transform approachAnchor;
        [SerializeField] private Transform seatAnchor;
        [SerializeField] private PerformerSeatingProfile seatingProfile;
        [SerializeField] private PerformerSeatedStyle defaultStyle = PerformerSeatedStyle.CrossLegs;
        [SerializeField, Range(0f, 90f)] private float facingDisagreementWarningDegrees = 20f;

        public Transform ApproachAnchor => approachAnchor;
        public Transform SeatAnchor => seatAnchor;
        public PerformerSeatingProfile SeatingProfile => seatingProfile;
        public PerformerSeatedStyle DefaultStyle => defaultStyle;
        public float FacingDisagreementWarningDegrees => facingDisagreementWarningDegrees;

        internal bool TryCaptureFrames(out Vector3 approachPosition, out Quaternion approachRotation,
            out Vector3 seatPosition, out Quaternion seatRotation, out string reason)
        {
            approachPosition = default;
            approachRotation = default;
            seatPosition = default;
            seatRotation = default;
            if (approachAnchor == null)
            {
                reason = name + " needs an ApproachAnchor child transform.";
                return false;
            }
            if (seatAnchor == null)
            {
                reason = name + " needs a SeatAnchor child transform.";
                return false;
            }
            if (seatingProfile == null)
            {
                reason = name + " needs a baked PerformerSeatingProfile. Run Tools > DAZ Pose > Seating > Bake KAWAII Seating for Generic Lara.";
                return false;
            }
            if (!seatingProfile.IsReady(out reason)) return false;
            if (approachAnchor == seatAnchor)
            {
                reason = name + " must use different ApproachAnchor and SeatAnchor transforms.";
                return false;
            }
            if (Vector3.ProjectOnPlane(approachAnchor.forward, Vector3.up).sqrMagnitude < 0.0001f
                || Vector3.ProjectOnPlane(seatAnchor.forward, Vector3.up).sqrMagnitude < 0.0001f)
            {
                reason = name + " has an anchor whose blue/+Z axis is vertical. ApproachAnchor and SeatAnchor need a usable horizontal facing direction.";
                return false;
            }

            approachPosition = approachAnchor.position;
            approachRotation = YawRotation(approachAnchor.forward);
            seatPosition = seatAnchor.position;
            seatRotation = YawRotation(seatAnchor.forward);
            reason = null;
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            DrawAnchor(approachAnchor, new Color(1f, 0.75f, 0.1f, 1f), 0.4f);
            DrawAnchor(seatAnchor, new Color(0.15f, 0.8f, 1f, 1f), 0.3f);
        }

        private static void DrawAnchor(Transform anchor, Color color, float length)
        {
            if (anchor == null) return;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(anchor.position, 0.055f);
            Gizmos.DrawLine(anchor.position, anchor.position + anchor.forward * length);
        }

        internal static Quaternion YawRotation(Vector3 forward)
        {
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            return forward.sqrMagnitude < 0.0001f
                ? Quaternion.identity
                : Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }
}
