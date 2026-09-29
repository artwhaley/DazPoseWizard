using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace DazPose.Performer
{
    internal struct PerformerPoseTransformState
    {
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
    }

    internal struct PerformerPoseStateJob : IAnimationJob
    {
        [ReadOnly] public NativeArray<TransformStreamHandle> TransformHandles;
        [ReadOnly] public NativeArray<PropertyStreamHandle> BlendShapeHandles;
        [ReadOnly] public NativeArray<PerformerPoseTransformState> TransformValues;
        [ReadOnly] public NativeArray<float> BlendShapeValues;

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!stream.isValid) return;

            for (var index = 0; index < TransformHandles.Length; index++)
            {
                var handle = TransformHandles[index];
                if (!handle.IsValid(stream)) continue;
                var value = TransformValues[index];
                handle.SetLocalPosition(stream, value.LocalPosition);
                handle.SetLocalRotation(stream, value.LocalRotation);
                handle.SetLocalScale(stream, value.LocalScale);
            }

            for (var index = 0; index < BlendShapeHandles.Length; index++)
            {
                var handle = BlendShapeHandles[index];
                if (handle.IsValid(stream)) handle.SetFloat(stream, BlendShapeValues[index]);
            }
        }
    }
}
