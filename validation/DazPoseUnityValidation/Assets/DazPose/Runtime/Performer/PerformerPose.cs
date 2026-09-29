using UnityEngine;

namespace DazPose.Performer
{
    [CreateAssetMenu(menuName = "Performer/Pose", fileName = "Performer Pose")]
    public sealed class PerformerPose : ScriptableObject
    {
        [SerializeField] private AnimationClip clip;

        public AnimationClip Clip => clip;
    }
}
