using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Per-shoe fit and support data, independent of a particular scene.</summary>
    [CreateAssetMenu(menuName="DAZ Pose/Footwear Profile")]
    public sealed class PerformerFootwearProfile : ScriptableObject
    {
        public string sourceName;
        public float standingHeight;
        [Tooltip("Inward foot-surface offset as a fraction of foot width; the shared bones and shoes retain their size.")]
        [Range(0,.1f)] public float footShrink=.05f;
        public bool rigidShoe=true, constrainToes=true;
        public Vector3 leftHeel,leftToe,rightHeel,rightToe;
        public float groundOffset;
        [Tooltip("Distance above the floor over which standing support blends into the swing pose.")]
        public float swingBlendHeight=.1f;
        [Tooltip("Import reference data for a future equipment consumer; does not implement equip/unequip.")]
        public Mesh referenceBody;
        public WardrobeOutfitDefinition.LocalBone[] referenceToeBones;
    }
}
