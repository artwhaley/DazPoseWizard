using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace DazPose.UnityValidation
{
    internal struct DazPoseTransformEndpoint
    {
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
    }

    internal struct DazPoseTransitionExtrapolationJob : IAnimationJob
    {
        [ReadOnly]
        public NativeArray<TransformStreamHandle> TransformHandles;
        [ReadOnly]
        public NativeArray<DazPoseTransformEndpoint> SourcePose;
        [ReadOnly]
        public NativeArray<DazPoseTransformEndpoint> TargetPose;
        public float Progress;

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!stream.isValid || (Progress >= 0f && Progress <= 1f)) return;

            for (var index = 0; index < TransformHandles.Length; index++)
            {
                var handle = TransformHandles[index];
                if (!handle.IsValid(stream)) continue;

                var source = SourcePose[index];
                var target = TargetPose[index];
                handle.SetLocalPosition(stream, Vector3.LerpUnclamped(source.LocalPosition, target.LocalPosition, Progress));
                var rotation = Quaternion.SlerpUnclamped(source.LocalRotation, target.LocalRotation, Progress);
                handle.SetLocalRotation(stream, Quaternion.Normalize(rotation));
            }
        }
    }
}
