using UnityEngine;

namespace DazPose.Performer
{
    [CreateAssetMenu(menuName = "Performer/Seating Profile", fileName = "Seating Profile")]
    public sealed class PerformerSeatingProfile : ScriptableObject
    {
        [Header("KAWAII → validated Lara Humanoid → Generic Lara")]
        [SerializeField] private PerformerSeatingMotion sitStart;
        [SerializeField] private PerformerSeatingMotion sitEnd;
        [SerializeField] private PerformerSeatingMotion crossLegsStart;
        [SerializeField] private PerformerSeatingMotion crossLegsLoop;
        [SerializeField] private PerformerSeatingMotion crossLegsEnd;
        [SerializeField] private PerformerSeatingMotion basicIdleLoopCandidate;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float playbackSpeed = 1f;
        [SerializeField, Min(0f)] private float bodyBlendSeconds = 0.18f;
        [SerializeField, Min(0f)] private float crossLegsExitBlendSeconds = 0.5f;
        [SerializeField, Min(0f)] private float finalBlendSeconds = 0.5f;
        [SerializeField, Min(0f)] private float approachAlignmentSeconds = 0.18f;
        [SerializeField, Min(0f)] private float maximumApproachPositionErrorMeters = 0.05f;
        [SerializeField, Min(0f)] private float maximumApproachFacingErrorDegrees = 4f;

        [Header("Measured Transition Seams")]
        [SerializeField, Range(0f, 1f)] private float crossLegsExitLoopPhase;
        [SerializeField, Range(0f, 1f)] private float sitEndEntryPhase;
        [SerializeField] private Vector3 crossLegsBodyRootOffset;
        [SerializeField] private bool hasCrossLegsSeatContactRebase;

        public PerformerSeatingMotion SitStart => sitStart;
        public PerformerSeatingMotion SitEnd => sitEnd;
        public PerformerSeatingMotion CrossLegsStart => crossLegsStart;
        public PerformerSeatingMotion CrossLegsLoop => crossLegsLoop;
        public PerformerSeatingMotion CrossLegsEnd => crossLegsEnd;
        public PerformerSeatingMotion BasicIdleLoopCandidate => basicIdleLoopCandidate;
        public float PlaybackSpeed => playbackSpeed;
        public float BodyBlendSeconds => bodyBlendSeconds;
        public float CrossLegsExitBlendSeconds => crossLegsExitBlendSeconds;
        public float FinalBlendSeconds => finalBlendSeconds;
        public float ApproachAlignmentSeconds => approachAlignmentSeconds;
        public float MaximumApproachPositionErrorMeters => maximumApproachPositionErrorMeters;
        public float MaximumApproachFacingErrorDegrees => maximumApproachFacingErrorDegrees;
        public float CrossLegsExitLoopPhase => crossLegsExitLoopPhase;
        public float SitEndEntryPhase => sitEndEntryPhase;
        public Vector3 CrossLegsBodyRootOffset => crossLegsBodyRootOffset;
        public bool HasCrossLegsSeatContactRebase => hasCrossLegsSeatContactRebase;

        public bool IsReady(out string reason)
        {
            var required = new[] { sitStart, sitEnd, crossLegsStart, crossLegsLoop, crossLegsEnd };
            foreach (var motion in required)
            {
                if (motion == null)
                {
                    reason = "The KAWAII seating profile is incomplete. Run Tools > DAZ Pose > Seating > Bake KAWAII Seating for Generic Lara.";
                    return false;
                }
                if (!motion.IsReady(out reason)) return false;
            }
            if (basicIdleLoopCandidate != null)
                if (!basicIdleLoopCandidate.IsReady(out reason)) return false;
            if (!crossLegsLoop.BodyClip.isLooping)
            {
                reason = "The baked CrossLegs seated loop must be marked looping.";
                return false;
            }
            if (playbackSpeed <= 0f)
            {
                reason = "Seating playback speed must be greater than zero.";
                return false;
            }
            if (!hasCrossLegsSeatContactRebase)
            {
                reason = "This seating profile predates the Cross Legs seat-contact fix. Run Tools > DAZ Pose > Seating > Bake KAWAII Seating for Generic Lara before using it.";
                return false;
            }
            float crossLegsEntryContactError = Vector3.Distance(
                sitStart.PelvisOffsetAt(1f), crossLegsStart.PelvisOffsetAt(0f));
            if (crossLegsEntryContactError > 0.01f)
            {
                reason = "The baked Cross Legs Start pelvis is " + crossLegsEntryContactError.ToString("0.000")
                    + " m from the Sit Start seat contact. Run Tools > DAZ Pose > Seating > Bake KAWAII Seating for Generic Lara again.";
                return false;
            }
            reason = null;
            return true;
        }

#if UNITY_EDITOR
        public void Configure(PerformerSeatingMotion[] motions, float exitLoopPhase,
            float sitEndEntry, Vector3 bodyRootOffset)
        {
            sitStart = motions[0];
            sitEnd = motions[1];
            crossLegsStart = motions[2];
            crossLegsLoop = motions[3];
            crossLegsEnd = motions[4];
            basicIdleLoopCandidate = motions.Length > 5 ? motions[5] : null;
            playbackSpeed = 1f;
            bodyBlendSeconds = 0.18f;
            crossLegsExitBlendSeconds = 0.5f;
            finalBlendSeconds = 0.5f;
            approachAlignmentSeconds = 0.18f;
            maximumApproachPositionErrorMeters = 0.05f;
            maximumApproachFacingErrorDegrees = 4f;
            crossLegsExitLoopPhase = Mathf.Repeat(exitLoopPhase, 1f);
            sitEndEntryPhase = Mathf.Clamp01(sitEndEntry);
            crossLegsBodyRootOffset = bodyRootOffset;
            hasCrossLegsSeatContactRebase = true;
        }
#endif
    }
}
