using System;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Imported equipment data. Runtime vocabulary/equipping is a separate consumer.</summary>
    [CreateAssetMenu(menuName="DAZ Pose/Wardrobe Outfit")]
    public sealed class WardrobeOutfitDefinition : ScriptableObject
    {
        [Serializable] public sealed class LocalBone
        { public string name,parent; public Vector3 position,scale; public Quaternion rotation; }
        [Serializable] public sealed class Piece
        {
            public string id,sourceNode,role;
            public bool shell;
            public bool requiresBentFootPose;
            public int coverageChannel=-1;
            public Mesh mesh;
            public Material[] materials;
            public string[] boneNames;
            public LocalBone[] localBones;
        }
        public string id,sourceFbxHash,sourceDufHash,recipeHash;
        public Mesh characterReference;
        public Mesh reviewBody;
        public PerformerFootwearProfile footwear;
        public Piece[] pieces=Array.Empty<Piece>();
    }
}
