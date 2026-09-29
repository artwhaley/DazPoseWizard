using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace DazPose.Performer
{
    internal struct PerformerPoseExtrapolationJob : IAnimationJob
    {
        [ReadOnly] public NativeArray<TransformStreamHandle> TransformHandles;
        [ReadOnly] public NativeArray<PropertyStreamHandle> BlendShapeHandles;
        [ReadOnly] public NativeArray<PerformerPoseTransformState> SourceTransforms;
        [ReadOnly] public NativeArray<PerformerPoseTransformState> TargetTransforms;
        [ReadOnly] public NativeArray<float> SourceBlendShapes;
        [ReadOnly] public NativeArray<float> TargetBlendShapes;
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

                var source = SourceTransforms[index];
                var target = TargetTransforms[index];
                handle.SetLocalPosition(stream,
                    Vector3.LerpUnclamped(source.LocalPosition, target.LocalPosition, Progress));
                handle.SetLocalRotation(stream,
                    NormalizeOrSource(Quaternion.SlerpUnclamped(source.LocalRotation, target.LocalRotation, Progress),
                        source.LocalRotation));
                handle.SetLocalScale(stream,
                    Vector3.LerpUnclamped(source.LocalScale, target.LocalScale, Progress));
            }

            for (var index = 0; index < BlendShapeHandles.Length; index++)
            {
                var handle = BlendShapeHandles[index];
                if (handle.IsValid(stream))
                    handle.SetFloat(stream, Mathf.LerpUnclamped(SourceBlendShapes[index], TargetBlendShapes[index], Progress));
            }
        }

        private static Quaternion NormalizeOrSource(Quaternion value, Quaternion source)
        {
            var magnitude = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            if (magnitude < 1e-8f || float.IsNaN(magnitude) || float.IsInfinity(magnitude)) return source;
            return new Quaternion(value.x / magnitude, value.y / magnitude, value.z / magnitude, value.w / magnitude);
        }
    }
}
