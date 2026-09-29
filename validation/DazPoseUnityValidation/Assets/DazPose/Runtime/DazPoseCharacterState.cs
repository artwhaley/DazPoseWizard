using System;
using UnityEngine;

namespace DazPose.UnityValidation
{
    [DisallowMultipleComponent]
    public sealed class DazPoseCharacterState : MonoBehaviour
    {
        public bool hasCapturedRestPose;
        public bool hasCapturedBlendShapes;
        public DazPoseRestTransform[] transforms = Array.Empty<DazPoseRestTransform>();
        public DazPoseRestBlendShape[] blendShapes = Array.Empty<DazPoseRestBlendShape>();
    }

    [Serializable]
    public sealed class DazPoseRestTransform
    {
        public string path;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    [Serializable]
    public sealed class DazPoseRestBlendShape
    {
        public string rendererPath;
        public int rendererComponentIndex;
        public int blendShapeCount;
        public string[] blendShapeNames = Array.Empty<string>();
        public float[] weights = Array.Empty<float>();
    }
}
