using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Generic body clip plus the actor-root and pelvis trajectories measured during its Humanoid bake.</summary>
    [CreateAssetMenu(menuName = "Performer/Seating Motion", fileName = "Seating Motion")]
    public sealed class PerformerSeatingMotion : ScriptableObject
    {
        [SerializeField] private AnimationClip bodyClip;
        [SerializeField] private AnimationCurve rootX = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField] private AnimationCurve rootY = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField] private AnimationCurve rootZ = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField] private AnimationCurve rootYaw = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField] private AnimationCurve pelvisX = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField] private AnimationCurve pelvisY = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField] private AnimationCurve pelvisZ = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField] private float durationSeconds;
        [SerializeField] private bool looping;
        [SerializeField, Range(0f, 1f)] private float sourceEntryPhase;
        [SerializeField] private string sourceAssetPath;

        public AnimationClip BodyClip => bodyClip;
        public float DurationSeconds => durationSeconds;
        public bool Looping => looping;
        public float SourceEntryPhase => sourceEntryPhase;
        public string SourceAssetPath => sourceAssetPath;
        public Vector3 RootPositionAt(float normalizedTime) => new Vector3(
            rootX.Evaluate(Mathf.Clamp01(normalizedTime)),
            rootY.Evaluate(Mathf.Clamp01(normalizedTime)),
            rootZ.Evaluate(Mathf.Clamp01(normalizedTime)));
        public float RootYawAt(float normalizedTime) => rootYaw.Evaluate(Mathf.Clamp01(normalizedTime));
        public Vector3 PelvisOffsetAt(float normalizedTime) => new Vector3(
            pelvisX.Evaluate(Mathf.Clamp01(normalizedTime)),
            pelvisY.Evaluate(Mathf.Clamp01(normalizedTime)),
            pelvisZ.Evaluate(Mathf.Clamp01(normalizedTime)));

        /// <summary>Shared actor trajectory for playback and the editor seating preview.</summary>
        public void EvaluateAnchoredRoot(float time, float entryTime, Vector3 originPosition,
            Quaternion originRotation, Vector3 targetPosition, Quaternion targetRotation,
            float finalBlendSeconds, float playbackSpeed, out Vector3 position, out Quaternion rotation)
        {
            float phase = Mathf.Clamp01(time / Mathf.Max(0.0001f, durationSeconds));
            float entryPhase = Mathf.Clamp01(entryTime / Mathf.Max(0.0001f, durationSeconds));
            Vector3 rawPosition = originPosition + originRotation
                * (RootPositionAt(phase) - RootPositionAt(entryPhase));
            Quaternion rawRotation = originRotation
                * Quaternion.AngleAxis(RootYawAt(phase) - RootYawAt(entryPhase), Vector3.up);
            float finalWindowStart = Mathf.Max(entryTime,
                durationSeconds - finalBlendSeconds * playbackSpeed);
            float convergence = time >= durationSeconds ? 1f
                : Mathf.InverseLerp(finalWindowStart, durationSeconds, time);
            convergence = convergence * convergence * (3f - 2f * convergence);
            position = Vector3.Lerp(rawPosition, targetPosition, convergence);
            float yawCorrection = Vector3.SignedAngle(rawRotation * Vector3.forward,
                targetRotation * Vector3.forward, Vector3.up);
            rotation = Quaternion.AngleAxis(yawCorrection * convergence, Vector3.up) * rawRotation;
        }

        public bool IsReady(out string reason)
        {
            if (bodyClip == null || durationSeconds <= 0f)
            {
                reason = name + " is missing its baked Generic body clip or duration. Run Tools > DAZ Pose > Seating > Bake KAWAII Seating for Generic Lara.";
                return false;
            }
            if (rootX == null || rootY == null || rootZ == null || rootYaw == null
                || pelvisX == null || pelvisY == null || pelvisZ == null
                || rootX.length == 0 || rootY.length == 0 || rootZ.length == 0
                || rootYaw.length == 0 || pelvisX.length == 0 || pelvisY.length == 0 || pelvisZ.length == 0)
            {
                reason = name + " is missing a baked root/contact trajectory. Run Tools > DAZ Pose > Seating > Bake KAWAII Seating for Generic Lara.";
                return false;
            }
            reason = null;
            return true;
        }

#if UNITY_EDITOR
        public void SetBakedData(AnimationClip clip, AnimationCurve x, AnimationCurve y,
            AnimationCurve z, AnimationCurve yaw, AnimationCurve contactX,
            AnimationCurve contactY, AnimationCurve contactZ, float duration,
            bool loop, float entryPhase, string source)
        {
            bodyClip = clip;
            rootX = x;
            rootY = y;
            rootZ = z;
            rootYaw = yaw;
            pelvisX = contactX;
            pelvisY = contactY;
            pelvisZ = contactZ;
            durationSeconds = duration;
            looping = loop;
            sourceEntryPhase = Mathf.Repeat(entryPhase, 1f);
            sourceAssetPath = source;
        }
#endif
    }
}
