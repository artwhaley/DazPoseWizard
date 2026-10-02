using UnityEngine;

namespace DazPose.Performer
{
    [CreateAssetMenu(menuName = "Performer/Locomotion Profile", fileName = "Locomotion Profile")]
    public sealed class PerformerLocomotionProfile : ScriptableObject
    {
        [Header("Normalized KAWAII Walk01")]
        [SerializeField] private PerformerLocomotionMotion startA;
        [SerializeField] private PerformerLocomotionMotion startB;
        [SerializeField] private PerformerLocomotionMotion walkLoop;
        [SerializeField] private PerformerLocomotionMotion stopA;
        [SerializeField] private PerformerLocomotionMotion stopB;
        [SerializeField] private PerformerLocomotionMotion turnLeft90;
        [SerializeField] private PerformerLocomotionMotion turnRight90;
        [SerializeField] private PerformerLocomotionMotion turnLeft180;
        [SerializeField] private PerformerLocomotionMotion turnRight180;
        [Header("Performance")]
        [SerializeField, Range(0.1f, 1f)] private float playbackSpeed = 0.665f;
        [SerializeField, Range(0f, 90f)] private float smallTurnThreshold = 45f;
        [SerializeField, Min(0f)] private float arrivalPositionTolerance = 0.07f;
        [SerializeField, Range(0f, 15f)] private float arrivalHeadingTolerance = 3f;
        [SerializeField, Min(0f)] private float bodyBlendSeconds = 0.16f;
        [SerializeField, Min(0f)] private float idleToLocomotionBlendSeconds = 0.5f;
        [SerializeField, Min(0f)] private float locomotionToIdleBlendSeconds = 0.5f;
        [SerializeField, Min(0f)] private float maxSteeringDegreesPerSecond = 10f;
        [SerializeField, Min(0f)] private float maximumEndpointCorrection = 0.12f;
        [SerializeField, Range(0f, 1f)] private float maximumStopWarpFraction = 0.2f;
        [SerializeField, Min(0f)] private float maximumTurnWarpDegrees = 50f;
        [SerializeField, Min(0f)] private float minimumWalkDistance = 0.9f;

        public PerformerLocomotionMotion StartA => startA;
        public PerformerLocomotionMotion StartB => startB;
        public PerformerLocomotionMotion WalkLoop => walkLoop;
        public PerformerLocomotionMotion StopA => stopA;
        public PerformerLocomotionMotion StopB => stopB;
        public PerformerLocomotionMotion TurnLeft90 => turnLeft90;
        public PerformerLocomotionMotion TurnRight90 => turnRight90;
        public PerformerLocomotionMotion TurnLeft180 => turnLeft180;
        public PerformerLocomotionMotion TurnRight180 => turnRight180;
        public float PlaybackSpeed => playbackSpeed;
        public float SmallTurnThreshold => smallTurnThreshold;
        public float ArrivalPositionTolerance => arrivalPositionTolerance;
        public float ArrivalHeadingTolerance => arrivalHeadingTolerance;
        public float BodyBlendSeconds => Mathf.Max(0.5f, bodyBlendSeconds);
        public float IdleToLocomotionBlendSeconds => Mathf.Max(0.5f, idleToLocomotionBlendSeconds);
        public float LocomotionToIdleBlendSeconds => Mathf.Max(1f, locomotionToIdleBlendSeconds);
        public float MaxSteeringDegreesPerSecond => maxSteeringDegreesPerSecond;
        public float MaximumEndpointCorrection => maximumEndpointCorrection;
        public float MaximumStopWarpFraction => maximumStopWarpFraction;
        public float MaximumTurnWarpDegrees => maximumTurnWarpDegrees;
        public float MinimumWalkDistance => minimumWalkDistance;

        public bool IsReady(out string reason)
        {
            var motions = new[] { startA, startB, walkLoop, stopA, stopB,
                turnLeft90, turnRight90, turnLeft180, turnRight180 };
            foreach (var motion in motions)
            {
                if (motion == null || motion.BodyClip == null || motion.DurationSeconds <= 0f)
                {
                    reason = "The normalized KAWAII Walk01 profile is incomplete. Run Tools > DAZ Pose > Locomotion > Bake KAWAII Walk01 for Generic Lara.";
                    return false;
                }
            }
            if (!walkLoop.BodyClip.isLooping)
            {
                reason = "The baked Walk01 loop clip must be marked looping.";
                return false;
            }
            if (playbackSpeed <= 0f || walkLoop.CycleDistance <= 0.01f
                || walkLoop.NominalPlanarDisplacement.z <= 0.05f
                || startA.NominalPlanarDisplacement.z <= 0.03f
                || startB.NominalPlanarDisplacement.z <= 0.03f
                || stopA.NominalPlanarDisplacement.z <= 0.03f
                || stopB.NominalPlanarDisplacement.z <= 0.03f)
            {
                reason = "The baked KAWAII Walk01 Start/Loop/Stop family is missing required forward root motion.";
                return false;
            }
            if (Mathf.Abs(turnLeft90.NominalYawDegrees) < 45f || turnLeft90.NominalYawDegrees >= 0f
                || turnRight90.NominalYawDegrees < 45f
                || Mathf.Abs(turnLeft180.NominalYawDegrees) < 135f || turnLeft180.NominalYawDegrees >= 0f
                || turnRight180.NominalYawDegrees < 135f)
            {
                reason = "The baked KAWAII turn clips do not contain the expected signed root yaw. The bake must prove trajectory extraction before WalkTo can run.";
                return false;
            }
            reason = null;
            return true;
        }

#if UNITY_EDITOR
        public void Configure(PerformerLocomotionMotion[] motions)
        {
            startA = motions[0]; startB = motions[1]; walkLoop = motions[2];
            stopA = motions[3]; stopB = motions[4];
            turnLeft90 = motions[5]; turnRight90 = motions[6];
            turnLeft180 = motions[7]; turnRight180 = motions[8];
            playbackSpeed = 0.665f;
            smallTurnThreshold = 45f;
            arrivalPositionTolerance = 0.07f;
            arrivalHeadingTolerance = 3f;
            bodyBlendSeconds = 0.16f;
            idleToLocomotionBlendSeconds = 0.5f;
            locomotionToIdleBlendSeconds = 0.5f;
            maxSteeringDegreesPerSecond = 10f;
            maximumEndpointCorrection = 0.12f;
            maximumStopWarpFraction = 0.2f;
            maximumTurnWarpDegrees = 50f;
            minimumWalkDistance = Mathf.Max(0.9f,
                Mathf.Max(motions[0].NominalPlanarDisplacement.magnitude, motions[1].NominalPlanarDisplacement.magnitude)
                + Mathf.Min(motions[3].NominalPlanarDisplacement.magnitude,
                    motions[4].NominalPlanarDisplacement.magnitude) - maximumEndpointCorrection);
        }
#endif
    }
}
