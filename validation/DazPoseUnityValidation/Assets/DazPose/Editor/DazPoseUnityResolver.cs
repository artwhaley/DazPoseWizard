using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.UnityValidation;
using UnityEditor;
using UnityEngine;

namespace DazPose.Editor.Importing
{
    internal static class DazPoseUnityResolver
    {
        private const float RestFitWarningThresholdMeters = 0.02f;
        private static readonly HashSet<string> RestCalibrationBoneIds = new(StringComparer.Ordinal)
        {
            "hip", "pelvis", "abdomenLower", "abdomen2", "chest", "chest_2", "neck", "neck_2", "head",
            "lCollar", "rCollar", "lShldr", "rShldr", "lForeArm", "rForeArm", "lHand", "rHand",
            "lThigh", "rThigh", "lShin", "rShin", "lFoot", "rFoot"
        };
        internal static bool TryResolvePose(Transform root, string jsonPath,
            out ResolvedUnityPose resolvedPose, out string failure)
            => TryResolvePose(root, jsonPath, out resolvedPose, out failure, true);
        internal static bool TryResolvePose(Transform root, string jsonPath, out ResolvedUnityPose resolvedPose,
            out string failure, bool temporaryReference)
        {
            resolvedPose = null;
            failure = null;
            var pose = DazPoseJsonLoader.Load(jsonPath);
            var state = DazPoseRestPoseService.Capture(root, false, !temporaryReference);
            var originalTransformsByPath = root.GetComponentsInChildren<Transform>(true)
                .ToDictionary(item => DazPoseTransformPath.Get(root, item), StringComparer.Ordinal);
            GameObject evaluationRootObject = null;
            try
            {
                evaluationRootObject = UnityEngine.Object.Instantiate(root.gameObject, root.parent, false);
                evaluationRootObject.name = root.name;
                evaluationRootObject.hideFlags = HideFlags.HideAndDontSave;
                var evaluationRoot = evaluationRootObject.transform;
                DazPoseRestPoseService.RestoreSnapshot(evaluationRoot, state);
                var bindingRoot = FindBindingRoot(evaluationRoot);
                var poseTargetIds = new HashSet<string>((pose.poseChannels ?? Array.Empty<DazPoseChannel>())
                    .Where(channel => channel.supported && !string.IsNullOrEmpty(channel.targetId))
                    .Select(channel => channel.targetId), StringComparer.Ordinal);
                var hasActiveMorphs = (pose.figureControls ?? Array.Empty<DazPoseFigureControl>())
                    .Any(control => control != null && Mathf.Abs(control.value) > 1e-7f);
                if (poseTargetIds.Count == 0 && !hasActiveMorphs)
                    throw new InvalidDataException("The canonical pose contains no active skeletal targets or figure controls.");

                var warnings = new List<string>();
                if (evaluationRoot.lossyScale.x <= 0 || evaluationRoot.lossyScale.y <= 0 || evaluationRoot.lossyScale.z <= 0)
                    warnings.Add("Character root has a non-positive scale; the pose was not applied.");
                if (!ApproximatelyUniformOne(evaluationRoot.lossyScale))
                    warnings.Add("Character root world scale is not approximately (1,1,1); DAZ centimeters to Unity meters may not match.");

                var resolutions = Array.Empty<DazPoseBoneResolution>();
                var missingTargets = Array.Empty<DazPoseBoneResolution>();
                var missingCalibration = Array.Empty<DazPoseBoneResolution>();
                var resolved = Array.Empty<DazPoseBoneResolution>();
                var resolvedBones = new List<ResolvedBonePose>();
                Transform skeletonRootOriginal = null;
                DazPoseRestBasisFit fit = null;
                var skeletalPose = poseTargetIds.Count > 0;
                if (skeletalPose)
                {
                    var skeletonRootEvaluation = FindSkeletonRoot(evaluationRoot);
                    var skeletonRootPath = DazPoseTransformPath.Get(evaluationRoot, skeletonRootEvaluation);
                    if (!originalTransformsByPath.TryGetValue(skeletonRootPath, out skeletonRootOriginal))
                        throw new InvalidOperationException("The Genesis 8 Female skeleton root could not be mapped back to the selected character.");
                    resolutions = DazPoseSkeletonResolver.Resolve(skeletonRootEvaluation, pose);
                    missingTargets = resolutions.Where(item => poseTargetIds.Contains(item.Definition.id) && item.Status != "resolved").ToArray();
                    missingCalibration = resolutions.Where(item => RestCalibrationBoneIds.Contains(item.Definition.id) && item.Status != "resolved").ToArray();
                    resolved = resolutions.Where(item => item.Status == "resolved").ToArray();
                    foreach (var item in resolutions.Where(item => item.Status != "resolved"))
                        warnings.Add(item.Status + " bone mapping: DAZ id='" + item.Definition.id + "', name='" + item.Definition.name + "', expected parent id='" + item.Definition.parentId + "'.");

                    try
                    {
                        var dazPoints = new List<Vector3>();
                        var unityPoints = new List<Vector3>();
                        foreach (var item in resolved.Where(item => RestCalibrationBoneIds.Contains(item.Definition.id)))
                        {
                            dazPoints.Add(DazPoseJsonLoader.Vector(item.Definition.restWorldPositionCm));
                            unityPoints.Add(item.Transform.position);
                        }
                        fit = DazPoseRestBasisCalibration.Fit(dazPoints, unityPoints);
                        if (fit.RmsErrorMeters > RestFitWarningThresholdMeters)
                            warnings.Add("DAZ-to-Unity rest landmark fit RMS is " + (fit.RmsErrorMeters * 1000).ToString("F1") + " mm (over 20 mm). Pose application was stopped; inspect the rest matrices and verify this is the matching neutral FBX.");
                    }
                    catch (Exception exception)
                    {
                        warnings.Add("Could not derive DAZ-to-Unity rest basis: " + exception.Message);
                    }

                    foreach (var item in missingCalibration)
                        warnings.Add("Rest calibration anchor did not resolve: DAZ id='" + item.Definition.id + "', name='" + item.Definition.name + "', parent id='" + item.Definition.parentId + "'.");
                    if (fit != null)
                    {
                        foreach (var item in resolved.Where(item => poseTargetIds.Contains(item.Definition.id)))
                        {
                            var dazRest = DazPoseJsonLoader.Vector(item.Definition.restWorldPositionCm) * 0.01f;
                            var predicted = fit.Basis.MultiplyVector(dazRest) + fit.Translation;
                            var errorMm = Vector3.Distance(predicted, item.Transform.position) * 1000f;
                            if (errorMm > RestFitWarningThresholdMeters * 1000f)
                                warnings.Add("Active pose target rest position differs by " + errorMm.ToString("F1") + " mm after the shared basis fit: DAZ id='" + item.Definition.id + "', name='" + item.Definition.name + "'. No per-bone correction was added.");
                        }
                    }

                    var reportPath = string.Empty;
                    if (!temporaryReference)
                    {
                        var report = BuildReport(evaluationRoot, pose, resolutions, poseTargetIds, RestCalibrationBoneIds, warnings, fit);
                        reportPath = Path.Combine(ProjectRoot, "TestOutput", "pose-application-report.json");
                        WriteReport(reportPath, report);
                    }
                    if (missingTargets.Length > 0 || missingCalibration.Length > 0 || fit == null
                        || fit.RmsErrorMeters > RestFitWarningThresholdMeters
                        || warnings.Any(value => value.StartsWith("Could not derive", StringComparison.Ordinal)))
                    {
                        failure = "Pose resolution stopped: target mapping or rest calibration failed. No scene object was changed."
                            + (temporaryReference ? string.Empty : " Review the report at " + reportPath + ".");
                        if (!temporaryReference) Debug.LogError(failure);
                        return false;
                    }

                    var targets = new List<PoseTarget>(resolved.Length);
                    foreach (var item in resolved)
                    {
                        var bone = item.Definition;
                        var restRotationDaz = DazPoseJsonLoader.Quaternion(bone.restWorldRotation);
                        var poseRotationDaz = DazPoseJsonLoader.Quaternion(bone.evaluatedWorldRotation);
                        var deltaDaz = Quaternion.Normalize(poseRotationDaz * Quaternion.Inverse(restRotationDaz));
                        var deltaUnity = DazPoseRestBasisCalibration.ConvertWorldRotationDelta(fit.Basis, deltaDaz);
                        var targetRotation = Quaternion.Normalize(deltaUnity * item.Transform.rotation);
                        var restPositionDaz = DazPoseJsonLoader.Vector(bone.restWorldPositionCm);
                        var posePositionDaz = DazPoseJsonLoader.Vector(bone.evaluatedWorldPositionCm);
                        var targetPosition = item.Transform.position + DazPoseRestBasisCalibration.ConvertDazCentimeterDelta(fit.Basis, posePositionDaz - restPositionDaz);
                        targets.Add(new PoseTarget { Transform = item.Transform, Position = targetPosition, Rotation = targetRotation });
                    }
                    foreach (var target in targets.OrderBy(item => DazPoseTransformPath.DepthFrom(evaluationRoot, item.Transform)))
                        target.Transform.SetPositionAndRotation(target.Position, target.Rotation);

                    var channelTargets = (pose.poseChannels ?? Array.Empty<DazPoseChannel>())
                        .Where(channel => channel.supported && !string.IsNullOrEmpty(channel.targetId))
                        .GroupBy(channel => channel.targetId, StringComparer.Ordinal)
                        .ToDictionary(group => group.Key, group => new
                        {
                            Rotation = group.Any(channel => channel.property == "rotation"),
                            Translation = group.Any(channel => channel.property == "translation")
                        }, StringComparer.Ordinal);
                    var restByPath = (state.transforms ?? Array.Empty<DazPoseRestTransform>()).ToDictionary(item => item.path, StringComparer.Ordinal);
                    foreach (var item in resolved)
                    {
                        var instancePath = DazPoseTransformPath.Get(evaluationRoot, item.Transform);
                        if (!originalTransformsByPath.TryGetValue(instancePath, out var originalTransform))
                            throw new InvalidOperationException("Could not map resolved DAZ bone '" + item.Definition.id + "' back to the selected character at " + instancePath + ".");
                        if (!item.Transform.IsChildOf(skeletonRootEvaluation) && item.Transform != skeletonRootEvaluation)
                            throw new InvalidOperationException("Resolved DAZ bone '" + item.Definition.id + "' is outside the configured skeleton root: " + instancePath + ".");
                        if (!restByPath.TryGetValue(instancePath, out var restTransform))
                            throw new InvalidOperationException("The captured import rest pose has no entry for resolved DAZ bone '" + item.Definition.id + "'.");
                        channelTargets.TryGetValue(item.Definition.id, out var channels);
                        var localRotation = item.Transform.localRotation;
                        var localPosition = item.Transform.localPosition;
                        var localScale = item.Transform.localScale;
                        resolvedBones.Add(new ResolvedBonePose
                        {
                            DazBoneId = item.Definition.id,
                            DazBoneName = item.Definition.name,
                            InstancePath = instancePath,
                            AnimationPath = AnimationUtility.CalculateTransformPath(item.Transform, bindingRoot),
                            MappingMethod = item.Method,
                            Transform = originalTransform,
                            HasPosition = Vector3.Distance(restTransform.localPosition, localPosition) > 1e-5f,
                            HasRotation = Quaternion.Angle(restTransform.localRotation, localRotation) > 0.001f,
                            HasScale = false,
                            HasDazTranslationChannel = channels != null && channels.Translation,
                            HasDazRotationChannel = channels != null && channels.Rotation,
                            LocalPosition = localPosition,
                            LocalRotation = localRotation,
                            LocalScale = localScale
                        });
                    }
                }

                var morphControls = DazPoseMorphResolver.Resolve(root, pose.figureControls);
                if (morphControls.Count > 0)
                {
                    try
                    {
                        var confirmed = DazPoseRequiredMorphManifestStore.MarkConfirmed(ProjectRoot, morphControls);
                        if (confirmed > 0) Debug.Log("Confirmed " + confirmed + " direct morph(s) in the project Required Morph Manifest.");
                    }
                    catch (Exception exception)
                    {
                        warnings.Add("Could not update the Required Morph Manifest after direct blendshape resolution: " + exception.Message);
                    }
                }

                resolvedPose = new ResolvedUnityPose
                {
                    CharacterRoot = root,
                    SkeletonRoot = skeletonRootOriginal,
                    BindingRoot = root,
                    Definition = pose,
                    RestState = state,
                    SourcePoseJsonPath = jsonPath,
                    CharacterName = root.name,
                    PoseTargetBoneCount = poseTargetIds.Count,
                    UnresolvedRequiredBoneCount = missingTargets.Length + missingCalibration.Length,
                    AmbiguousBoneCount = resolutions.Count(item => item.Status == "ambiguous"),
                    Bones = resolvedBones,
                    MorphControls = morphControls,
                    Warnings = warnings.ToArray()
                };
                var resolvedMorphBindingCount = morphControls.Sum(item => item.Bindings.Count);
                Debug.Log("Resolved DAZ pose on " + root.name + ": driven bones " + resolvedBones.Count(item => item.HasRotation || item.HasPosition || item.HasScale)
                    + " | active figure controls " + morphControls.Count + " | renderer bindings " + resolvedMorphBindingCount
                    + (fit == null ? string.Empty : " | rest-fit RMS " + (fit.RmsErrorMeters * 1000).ToString("F1") + " mm"));
                return true;
            }
            finally
            {
                if (evaluationRootObject != null) UnityEngine.Object.DestroyImmediate(evaluationRootObject);
            }
        }

        private static DazPoseValidationReport BuildReport(Transform root, DazPoseDefinition pose,
            DazPoseBoneResolution[] resolutions, HashSet<string> poseTargetIds, HashSet<string> calibrationBoneIds,
            List<string> warnings, DazPoseRestBasisFit fit)
        {
            var mappings = resolutions.Select(item => new DazPoseMappingDiagnostic
            {
                id = item.Definition.id,
                name = item.Definition.name,
                expectedParentId = item.Definition.parentId,
                transformPath = item.Transform == null ? string.Empty : DazPoseTransformPath.Get(root, item.Transform),
                method = item.Method,
                status = item.Status,
                detail = item.Detail,
                usedForRestCalibration = calibrationBoneIds.Contains(item.Definition.id),
                activePoseTarget = poseTargetIds.Contains(item.Definition.id),
                restFitErrorMm = fit == null || item.Transform == null ? -1 : (Vector3.Distance(
                    fit.Basis.MultiplyVector(DazPoseJsonLoader.Vector(item.Definition.restWorldPositionCm) * 0.01f) + fit.Translation,
                    item.Transform.position) * 1000f)
            }).ToArray();
            return new DazPoseValidationReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                character = root.name,
                pose = DazPoseJsonLoader.PoseName(pose.source.poseFile, pose.source.poseAssetId),
                figureAssetId = pose.source.figureAssetId,
                poseAssetId = pose.source.poseAssetId,
                boneCount = pose.bones.Length,
                poseChannelCount = pose.poseChannels == null ? 0 : pose.poseChannels.Length,
                poseTargetCount = poseTargetIds.Count,
                resolvedCount = resolutions.Count(item => item.Status == "resolved"),
                missingCount = resolutions.Count(item => item.Status == "missing"),
                ambiguousCount = resolutions.Count(item => item.Status == "ambiguous"),
                rootLocalScale = root.localScale,
                rootLossyScale = root.lossyScale,
                dazUnits = pose.coordinateSystem == null ? "centimeter" : pose.coordinateSystem.lengthUnit,
                unityUnits = "meter",
                centimetersToMeters = 0.01f,
                restFitRmsMm = fit == null ? -1 : fit.RmsErrorMeters * 1000f,
                restFitMaxMm = fit == null ? -1 : fit.MaxErrorMeters * 1000f,
                restFitSampleCount = fit == null ? 0 : fit.SampleCount,
                restCalibrationBoneIds = calibrationBoneIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                dazToUnityWorldBasis = fit == null ? DazPoseRestBasisCalibration.MatrixValues(Matrix4x4.identity) : DazPoseRestBasisCalibration.MatrixValues(fit.Basis),
                dazToUnityBasisDeterminant = fit == null ? 1 : fit.BasisDeterminant,
                dazToUnityWorldTranslation = fit == null ? Vector3.zero : fit.Translation,
                warnings = warnings.ToArray(),
                mappings = mappings,
                transforms = root.GetComponentsInChildren<Transform>(true).Select(item => DescribeTransform(root, item)).ToArray()
            };
        }

        private static DazPoseTransformDiagnostic DescribeTransform(Transform root, Transform item)
        {
            var parent = item.parent;
            return new DazPoseTransformDiagnostic
            {
                name = item.name,
                path = DazPoseTransformPath.Get(root, item),
                parentName = parent == null ? string.Empty : parent.name,
                parentPath = parent == null || parent == root.parent ? string.Empty : DazPoseTransformPath.Get(root, parent),
                localPosition = item.localPosition,
                localRotation = item.localRotation,
                localScale = item.localScale,
                worldPosition = item.position,
                worldRotation = item.rotation,
                localToWorldMatrix = MatrixValues(item.localToWorldMatrix)
            };
        }

        private static float[] MatrixValues(Matrix4x4 m) => new[]
        {
            m.m00, m.m01, m.m02, m.m03, m.m10, m.m11, m.m12, m.m13,
            m.m20, m.m21, m.m22, m.m23, m.m30, m.m31, m.m32, m.m33
        };

        private static Transform FindSkeletonRoot(Transform characterRoot)
        {
            var candidates = characterRoot.GetComponentsInChildren<Transform>(true)
                .Where(item => item.name == "Genesis8Female" && item.parent == characterRoot).ToArray();
            if (candidates.Length != 1)
                throw new InvalidOperationException("Expected exactly one direct child named Genesis8Female under selected character '" + characterRoot.name
                    + "', found " + candidates.Length + ". The G8F skeleton root must be a unique direct child.");
            return candidates[0];
        }

        private static Transform FindBindingRoot(Transform characterRoot)
        {
            if (characterRoot == null) throw new ArgumentNullException(nameof(characterRoot));
            FindSkeletonRoot(characterRoot);
            return characterRoot;
        }

        private static bool ApproximatelyUniformOne(Vector3 scale)
            => Mathf.Abs(scale.x - 1) < 0.001f && Mathf.Abs(scale.y - 1) < 0.001f && Mathf.Abs(scale.z - 1) < 0.001f;

        private static void WriteReport(string path, DazPoseValidationReport report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        private sealed class PoseTarget
        {
            public Transform Transform;
            public Vector3 Position;
            public Quaternion Rotation;
        }
    }
}
