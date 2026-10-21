using System;
using UnityEngine;

namespace DazPose.Performer
{
    [Flags]
    public enum PerformerExpressionBoneProperties
    {
        None = 0,
        LocalPosition = 1,
        LocalRotation = 2
    }

    [Serializable]
    public struct PerformerExpressionChannel
    {
        [SerializeField] private string rendererPath;
        [SerializeField] private string blendShapeName;
        [SerializeField] private float targetWeight;

        public string RendererPath => rendererPath;
        public string BlendShapeName => blendShapeName;
        public float TargetWeight => targetWeight;

        public PerformerExpressionChannel(string rendererPath, string blendShapeName, float targetWeight)
        {
            this.rendererPath = rendererPath;
            this.blendShapeName = blendShapeName;
            this.targetWeight = targetWeight;
        }
    }

    [Serializable]
    public struct PerformerExpressionBoneChannel
    {
        [SerializeField] private string transformPath;
        [SerializeField] private string dazBoneId;
        [SerializeField] private PerformerExpressionBoneProperties properties;
        [SerializeField] private Vector3 targetLocalPosition;
        [SerializeField] private Quaternion targetLocalRotation;

        public string TransformPath => transformPath;
        public string DazBoneId => dazBoneId;
        public PerformerExpressionBoneProperties Properties => properties;
        public Vector3 TargetLocalPosition => targetLocalPosition;
        public Quaternion TargetLocalRotation => targetLocalRotation;

        public PerformerExpressionBoneChannel(string transformPath, string dazBoneId,
            PerformerExpressionBoneProperties properties, Vector3 targetLocalPosition, Quaternion targetLocalRotation)
        {
            this.transformPath = transformPath;
            this.dazBoneId = dazBoneId;
            this.properties = properties;
            this.targetLocalPosition = targetLocalPosition;
            this.targetLocalRotation = targetLocalRotation;
        }
    }

    [CreateAssetMenu(menuName = "Performer/Expression", fileName = "Performer Expression")]
    public sealed class PerformerExpression : ScriptableObject
    {
        [SerializeField] private AnimationClip clip = null;
        [SerializeField] private PerformerExpressionChannel[] channels = Array.Empty<PerformerExpressionChannel>();
        [SerializeField] private PerformerExpressionBoneChannel[] boneChannels = Array.Empty<PerformerExpressionBoneChannel>();

        public AnimationClip Clip => clip;
        public PerformerExpressionChannel[] Channels => channels;
        public PerformerExpressionBoneChannel[] BoneChannels => boneChannels ?? Array.Empty<PerformerExpressionBoneChannel>();
    }
}
