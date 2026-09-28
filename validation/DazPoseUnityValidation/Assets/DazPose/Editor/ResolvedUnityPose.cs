using System;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public sealed class ResolvedUnityPose
    {
        public Transform CharacterRoot;
        public Transform AnimationRoot;
        public DazPoseDefinition Definition;
        public DazPoseCharacterState RestState;
        public string SourcePoseJsonPath;
        public string CharacterName;
        public int PoseTargetBoneCount;
        public int UnresolvedRequiredBoneCount;
        public int AmbiguousBoneCount;
        public List<ResolvedBonePose> Bones = new List<ResolvedBonePose>();
        public string[] Warnings = Array.Empty<string>();
    }

    public sealed class ResolvedBonePose
    {
        public string DazBoneId;
        public string DazBoneName;
        public string InstancePath;
        public string AnimationPath;
        public string MappingMethod;
        public Transform Transform;
        public bool HasPosition;
        public bool HasRotation;
        public bool HasScale;
        public bool HasDazTranslationChannel;
        public bool HasDazRotationChannel;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
    }

    [Serializable]
    public sealed class DazPoseAnimationClipReport
    {
        public string generatedAtUtc;
        public string figureGeneration;
        public string character;
        public string animationRoot;
        public string sourcePoseJson;
        public string sourceDazPosePath;
        public string sourcePoseAssetId;
        public string sourceFigureAssetId;
        public string generatedClipPath;
        public string assetGuid;
        public string clipName;
        public bool generationPassed;
        public string generationFailure;
        public float durationSeconds;
        public int resolvedBoneCount;
        public int poseTargetBoneCount;
        public int rotationBoneCount;
        public int positionBoneCount;
        public int scaleBoneCount;
        public int rotationCurveCount;
        public int positionCurveCount;
        public int scaleCurveCount;
        public int totalCurveCount;
        public int unresolvedRequiredBones;
        public int ambiguousBones;
        public bool directApplyParityPassed;
        public float maximumRotationErrorDegrees;
        public float maximumPositionErrorMeters;
        public float maximumScaleError;
        public float[] paritySampleTimes;
        public string[] warnings;
        public DazPoseAnimationBindingDiagnostic[] bindings;
    }

    [Serializable]
    public sealed class DazPoseAnimationBindingDiagnostic
    {
        public string dazBoneId;
        public string dazBoneName;
        public string unityPath;
        public string[] properties;
        public string rotationSource;
        public string positionSource;
    }

}
