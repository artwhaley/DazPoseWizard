using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace DazPose.Performer
{
    internal struct PerformerBaseStateCaptureJob : IAnimationJob
    {
        [ReadOnly] public NativeArray<TransformStreamHandle> TransformHandles;
        [ReadOnly] public NativeArray<PropertyStreamHandle> BlendShapeHandles;
        public NativeArray<PerformerPoseTransformState> CapturedTransforms;
        public NativeArray<float> CapturedBlendShapes;

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
                CapturedTransforms[index] = new PerformerPoseTransformState
                {
                    LocalPosition = handle.GetLocalPosition(stream),
                    LocalRotation = handle.GetLocalRotation(stream),
                    LocalScale = handle.GetLocalScale(stream)
                };
            }

            for (var index = 0; index < BlendShapeHandles.Length; index++)
            {
                var handle = BlendShapeHandles[index];
                if (handle.IsValid(stream)) CapturedBlendShapes[index] = handle.GetFloat(stream);
            }
        }
    }
}
