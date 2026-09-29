using System;
using UnityEngine;

namespace DazPose.UnityValidation
{
    [Serializable]
    public sealed class DazPoseDefinition
    {
        public string format;
        public int version;
        public DazPoseSource source;
        public DazPoseCoordinateSystem coordinateSystem;
        public DazPoseFigureControl[] figureControls;
        public DazPoseChannel[] poseChannels;
        public DazPoseBone[] bones;
        public string[] warnings;
    }

    [Serializable]
    public sealed class DazPoseFigureControl
    {
        public string sourceUrl;
        public string rawControlId;
        public string name;
        public float value;
    }

    [Serializable]
    public sealed class DazPoseSource
    {
        public string figureFile;
        public string poseFile;
        public string figureAssetId;
        public string poseAssetId;
    }

    [Serializable]
    public sealed class DazPoseCoordinateSystem
    {
        public string handedness;
        public string upAxis;
        public string lengthUnit;
        public string angleUnit;
    }

    [Serializable]
    public sealed class DazPoseChannel
    {
        public string url;
        public string targetId;
        public string targetName;
        public string property;
        public string axis;
        public string leafProperty;
        public DazPoseKey[] keys;
        public bool supported;
        public bool usedIdFallback;
    }

    [Serializable]
    public sealed class DazPoseKey
    {
        public float timeSeconds;
        public float value;
    }

    [Serializable]
    public sealed class DazPoseBone
    {
        public string id;
        public string name;
        public string parentId;
        public string rotationOrder;
        public bool inheritsScale;
        public float[] center;
        public float[] end;
        public float[] orientationDegrees;
        public float[] sourceRotationDegrees;
        public float[] sourceTranslationCm;
        public float[] sourceScale;
        public float sourceGeneralScale;
        public float[] poseRotationDegrees;
        public float[] poseTranslationCm;
        public float[] restLocalRotation;
        public float[] restWorldRotation;
        public float[] restWorldPositionCm;
        public float[] restGlobalScaleMatrix;
        public float[] evaluatedLocalRotation;
        public float[] evaluatedWorldRotation;
        public float[] evaluatedWorldPositionCm;
        public float[] evaluatedGlobalScale;
        public float[] evaluatedGlobalScaleMatrix;
    }

    [Serializable]
    public sealed class DazPoseTransformDiagnostic
    {
        public string name;
        public string path;
        public string parentName;
        public string parentPath;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public Vector3 worldPosition;
        public Quaternion worldRotation;
        public float[] localToWorldMatrix;
    }

    [Serializable]
    public sealed class DazPoseExpectedBoneDiagnostic
    {
        public string name;
        public bool found;
        public int matchCount;
        public string path;
    }

    [Serializable]
    public sealed class DazPoseMappingDiagnostic
    {
        public string id;
        public string name;
        public string expectedParentId;
        public string transformPath;
        public string method;
        public string status;
        public string detail;
        public float restFitErrorMm;
        public bool usedForRestCalibration;
        public bool activePoseTarget;
    }

    [Serializable]
    public sealed class DazPoseValidationReport
    {
        public string generatedAtUtc;
        public string character;
        public string pose;
        public string figureAssetId;
        public string poseAssetId;
        public int boneCount;
        public int poseChannelCount;
        public int poseTargetCount;
        public int resolvedCount;
        public int missingCount;
        public int ambiguousCount;
        public Vector3 rootLocalScale;
        public Vector3 rootLossyScale;
        public string dazUnits;
        public string unityUnits;
        public float centimetersToMeters;
        public float restFitRmsMm;
        public float restFitMaxMm;
        public int restFitSampleCount;
        public string[] restCalibrationBoneIds;
        public float[] dazToUnityWorldBasis;
        public float dazToUnityBasisDeterminant;
        public Vector3 dazToUnityWorldTranslation;
        public string[] warnings;
        public DazPoseExpectedBoneDiagnostic[] expectedBones;
        public int skinnedRendererCount;
        public int importedBlendShapeCount;
        public DazPoseRendererDiagnostic[] renderers;
        public DazPoseMappingDiagnostic[] mappings;
        public DazPoseTransformDiagnostic[] transforms;
    }

    [Serializable]
    public sealed class DazPoseRendererDiagnostic
    {
        public string rendererPath;
        public string meshName;
        public int blendShapeCount;
        public DazPoseBlendShapeDiagnostic[] blendShapes;
    }

    [Serializable]
    public sealed class DazPoseBlendShapeDiagnostic
    {
        public int index;
        public string name;
        public int frameCount;
        public float[] frameWeights;
    }
}
