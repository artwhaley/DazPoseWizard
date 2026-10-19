using UnityEngine;
namespace DazPose.Player
{
    /// <summary>Exactly the finite/acquire/follow runtime aim calculation; contains no scene writes.</summary>
    public static class PlayerViewEndpoint
    {
        public static Quaternion DesiredRotation(Vector3 worldTarget, Vector3 actualOrigin, Quaternion currentRigRotation)
        {
            Vector3 direction = worldTarget - actualOrigin;
            if (direction.sqrMagnitude < 1e-8f) return currentRigRotation;
            direction.Normalize();
            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(direction, up)) > 0.999f) up = Vector3.forward;
            return Quaternion.LookRotation(direction, up);
        }
    }
}
