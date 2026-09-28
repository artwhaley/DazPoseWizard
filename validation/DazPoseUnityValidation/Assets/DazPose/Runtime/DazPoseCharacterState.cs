using System;
using UnityEngine;

namespace DazPose.UnityValidation
{
    [DisallowMultipleComponent]
    public sealed class DazPoseCharacterState : MonoBehaviour
    {
        public bool hasCapturedRestPose;
        public DazPoseRestTransform[] transforms = Array.Empty<DazPoseRestTransform>();
    }

    [Serializable]
    public sealed class DazPoseRestTransform
    {
        public string path;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }
}
