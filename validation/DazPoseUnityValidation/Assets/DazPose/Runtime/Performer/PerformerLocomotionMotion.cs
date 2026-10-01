using System;
using UnityEngine;

namespace DazPose.Performer
{
    public enum LocomotionSupportFoot { Unknown, Left, Right, Both, Neither }

    [CreateAssetMenu(menuName = "Performer/Locomotion Motion", fileName = "Locomotion Motion")]
    public sealed class PerformerLocomotionMotion : ScriptableObject
    {
        [SerializeField] private AnimationClip bodyClip;
        [SerializeField] private AnimationCurve rootX = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField] private AnimationCurve rootZ = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField] private AnimationCurve rootYaw = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField] private AnimationCurve planarDistance = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField, Range(0f, 1f)] private float entryGaitPhase;
        [SerializeField, Range(0f, 1f)] private float exitGaitPhase;
        [SerializeField] private LocomotionSupportFoot entrySupportFoot;
        [SerializeField] private LocomotionSupportFoot exitSupportFoot;
        [SerializeField] private float loopEntryPhase;
        [SerializeField] private float durationSeconds;
        [SerializeField] private Vector3 nominalPlanarDisplacement;
        [SerializeField] private float nominalYawDegrees;
        [SerializeField] private string sourceAssetPath;
        [SerializeField] private string bakeNotes;

        public AnimationClip BodyClip => bodyClip;
        public float DurationSeconds => durationSeconds;
        public float EntryGaitPhase => entryGaitPhase;
        public float ExitGaitPhase => exitGaitPhase;
        public LocomotionSupportFoot EntrySupportFoot => entrySupportFoot;
        public LocomotionSupportFoot ExitSupportFoot => exitSupportFoot;
        public float LoopEntryPhase => loopEntryPhase;
        public Vector3 NominalPlanarDisplacement => nominalPlanarDisplacement;
        public float NominalYawDegrees => nominalYawDegrees;
        public string SourceAssetPath => sourceAssetPath;
        public string BakeNotes => bakeNotes;

        public Vector3 PositionAt(float normalizedTime) => new Vector3(
            rootX.Evaluate(Mathf.Clamp01(normalizedTime)), 0f,
            rootZ.Evaluate(Mathf.Clamp01(normalizedTime)));
        public float YawAt(float normalizedTime) => rootYaw.Evaluate(Mathf.Clamp01(normalizedTime));
        public float PlanarDistanceAt(float normalizedTime) => planarDistance.Evaluate(Mathf.Clamp01(normalizedTime));

        public Vector3 SamplePositionDelta(float from, float to)
        {
            if (BodyClip == null) return Vector3.zero;
            if (!BodyClip.isLooping || to >= from) return PositionAt(to) - PositionAt(from);
            return PositionAt(1f) - PositionAt(from) + PositionAt(to) - PositionAt(0f);
        }

        public float SampleYawDelta(float from, float to)
        {
            if (BodyClip == null) return 0f;
            if (!BodyClip.isLooping || to >= from) return YawAt(to) - YawAt(from);
            return YawAt(1f) - YawAt(from) + YawAt(to) - YawAt(0f);
        }

        public float DistanceBetweenPhases(float from, float to)
        {
            if (BodyClip == null) return 0f;
            if (to >= from) return Mathf.Max(0f, PlanarDistanceAt(to) - PlanarDistanceAt(from));
            return Mathf.Max(0f, PlanarDistanceAt(1f) - PlanarDistanceAt(from) + PlanarDistanceAt(to));
        }

        public float CycleDistance => BodyClip == null ? 0f : PlanarDistanceAt(1f);

#if UNITY_EDITOR
        public void SetBakedData(AnimationClip clip, AnimationCurve x, AnimationCurve z,
            AnimationCurve yaw, AnimationCurve distance, float duration, string source,
            LocomotionSupportFoot entryFoot, LocomotionSupportFoot exitFoot, string notes)
        {
            bodyClip = clip;
            rootX = x;
            rootZ = z;
            rootYaw = yaw;
            planarDistance = distance;
            durationSeconds = duration;
            sourceAssetPath = source;
            entrySupportFoot = entryFoot;
            exitSupportFoot = exitFoot;
            nominalPlanarDisplacement = new Vector3(x.Evaluate(1f), 0f, z.Evaluate(1f));
            nominalYawDegrees = yaw.Evaluate(1f);
            bakeNotes = notes;
        }

        public void SetPhaseMetadata(float entryPhase, float exitPhase, float loopEntry)
        {
            entryGaitPhase = Mathf.Repeat(entryPhase, 1f);
            exitGaitPhase = Mathf.Repeat(exitPhase, 1f);
            loopEntryPhase = Mathf.Repeat(loopEntry, 1f);
        }
#endif
    }
}
