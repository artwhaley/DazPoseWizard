using System;
using UnityEngine;

namespace DazPose.Performer
{
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

    [CreateAssetMenu(menuName = "Performer/Expression", fileName = "Performer Expression")]
    public sealed class PerformerExpression : ScriptableObject
    {
        [SerializeField] private AnimationClip clip = null;
        [SerializeField] private PerformerExpressionChannel[] channels = Array.Empty<PerformerExpressionChannel>();

        public AnimationClip Clip => clip;
        public PerformerExpressionChannel[] Channels => channels;
    }
}
