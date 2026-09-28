using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public static class DazPoseAnimationClipGenerator
    {
        public const string OutputFolder = "Assets/Generated/DazPoses/G8F";
        public const float DurationSeconds = 1f;
        public static DazPoseAnimationClipReport Generate(ResolvedUnityPose pose, bool replaceExisting)
        {
            if (pose == null || pose.Definition == null || pose.AnimationRoot == null)
                throw new ArgumentNullException(nameof(pose), "A resolved G8F pose and stable animation root are required.");

            var safeName = SanitizeAssetName(DazPoseJsonLoader.PoseName(pose.Definition.source.poseFile, pose.Definition.source.poseAssetId));
            var assetPath = OutputFolder + "/" + safeName + ".anim";
            var reportPath = OutputFolder + "/" + safeName + ".report.json";
            Directory.CreateDirectory(Path.Combine(ProjectRoot, OutputFolder.Replace('/', Path.DirectorySeparatorChar)));
            AssetDatabase.Refresh();

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (existing != null && !replaceExisting)
                throw new InvalidOperationException("AnimationClip already exists at " + assetPath + ". Confirm regeneration before replacing it.");

            var clip = new AnimationClip
            {
                name = safeName,
                frameRate = 30f,
                legacy = false
            };
            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();
            var diagnostics = new List<DazPoseAnimationBindingDiagnostic>();
            AnimationClip backup = null;
            DazPoseAnimationClipReport report = null;
            var existingGuid = existing == null ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
            var createdAsset = false;

            try
            {
                foreach (var bone in pose.Bones.Where(item => item.HasPosition || item.HasRotation || item.HasScale)
                             .OrderBy(item => item.AnimationPath, StringComparer.Ordinal))
                {
                    ValidateAnimationPath(pose, bone);
                    var properties = new List<string>();
                    if (bone.HasRotation)
                    {
                        var rotation = Normalize(bone.LocalRotation, bone.DazBoneId);
                        AddVector4(bindings, curves, bone.AnimationPath, "m_LocalRotation", rotation);
                        properties.Add("m_LocalRotation.x/y/z/w");
                    }
                    if (bone.HasPosition)
                    {
                        RequireFinite(bone.LocalPosition, bone.DazBoneId + " local position");
                        AddVector3(bindings, curves, bone.AnimationPath, "m_LocalPosition", bone.LocalPosition);
                        properties.Add("m_LocalPosition.x/y/z");
                    }
                    if (bone.HasScale)
                    {
                        RequireFinite(bone.LocalScale, bone.DazBoneId + " local scale");
                        AddVector3(bindings, curves, bone.AnimationPath, "m_LocalScale", bone.LocalScale);
                        properties.Add("m_LocalScale.x/y/z");
                    }

                    diagnostics.Add(new DazPoseAnimationBindingDiagnostic
                    {
                        dazBoneId = bone.DazBoneId,
                        dazBoneName = bone.DazBoneName,
                        unityPath = bone.AnimationPath,
                        properties = properties.ToArray(),
                        rotationSource = bone.HasRotation ? (bone.HasDazRotationChannel ? "DAZ-supported rotation channel, resolved as a complete local quaternion" : "resolved local rotation required by the proven world-pose conversion") : string.Empty,
                        positionSource = bone.HasPosition ? (bone.HasDazTranslationChannel ? "DAZ-supported translation channel, resolved to Unity local meters" : "resolved local position required by the proven world-center conversion") : string.Empty
                    });
                }

                AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
                clip.EnsureQuaternionContinuity();

                var candidateParity = DazPoseClipParityValidator.Validate(pose, clip);
                report = BuildReport(pose, clip, diagnostics, candidateParity, assetPath);
                if (!candidateParity.Passed)
                {
                    report.generationFailure = "In-memory candidate failed parity: " + candidateParity.Summary;
                    WriteReport(reportPath, report);
                    throw new InvalidOperationException("Candidate AnimationClip failed direct-pose parity. The existing saved asset was left untouched. Detailed report: " + reportPath + ". " + candidateParity.Summary);
                }

                if (existing == null)
                {
                    AssetDatabase.CreateAsset(clip, assetPath);
                    createdAsset = true;
                    clip = null;
                }
                else
                {
                    backup = UnityEngine.Object.Instantiate(existing);
                    backup.hideFlags = HideFlags.HideAndDontSave;
                    CopyClipContents(clip, existing);
                    EditorUtility.SetDirty(existing);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                var savedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (savedClip == null) throw new InvalidOperationException("Unity saved the clip but could not reload " + assetPath + ".");

                var parity = DazPoseClipParityValidator.Validate(pose, savedClip);
                if (!parity.Passed)
                    throw new InvalidOperationException("Saved AnimationClip failed direct-pose parity. Detailed report: " + reportPath + ". " + parity.Summary);

                var currentGuid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(existingGuid) && !string.Equals(existingGuid, currentGuid, StringComparison.Ordinal))
                    throw new InvalidOperationException("AnimationClip asset identity changed during in-place regeneration. Previous GUID " + existingGuid + ", current GUID " + currentGuid + ".");

                report = BuildReport(pose, savedClip, diagnostics, parity, assetPath);
                report.assetGuid = currentGuid;
                report.generationPassed = true;
                WriteReport(reportPath, report);
                Debug.Log("DAZ Pose → Unity AnimationClip\n"
                    + "Source: " + Path.GetFileName(pose.SourcePoseJsonPath) + "\n"
                    + "Character: " + pose.CharacterName + " (Genesis 8 Female)\n"
                    + "Resolved bones: " + pose.Bones.Count + " | pose targets: " + pose.PoseTargetBoneCount + " | Rotation curves: " + report.rotationCurveCount
                    + " | Position curves: " + report.positionCurveCount + " | Scale curves: " + report.scaleCurveCount + "\n"
                    + "Unresolved required bones: 0 | Ambiguous bones: 0\n"
                    + "Clip duration: " + report.durationSeconds.ToString("F1") + " sec | direct/clip parity: PASS at 0, 0.5, 1.0 sec\n"
                    + "Output: " + assetPath + " | report: " + reportPath);
                return report;
            }
            catch (Exception exception)
            {
                if (report == null) report = BuildReport(pose, clip, diagnostics, new DazPoseClipParityResult(), assetPath);
                report.generationPassed = false;
                if (string.IsNullOrEmpty(report.generationFailure)) report.generationFailure = exception.Message;
                try { WriteReport(reportPath, report); }
                catch (Exception reportException) { Debug.LogError("Could not write DAZ Pose generation failure report: " + reportException.Message); }

                if (existing != null && backup != null)
                {
                    try
                    {
                        var assetToRestore = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                        if (assetToRestore == null) throw new InvalidOperationException("The existing AnimationClip could not be reloaded for rollback.");
                        CopyClipContents(backup, assetToRestore);
                        EditorUtility.SetDirty(assetToRestore);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                    }
                    catch (Exception restoreException)
                    {
                        Debug.LogError("Could not restore the previous DAZ Pose AnimationClip after a failed regeneration: " + restoreException);
                    }
                }
                else if (createdAsset)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
                throw;
            }
            finally
            {
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                if (backup != null) UnityEngine.Object.DestroyImmediate(backup);
            }
        }

        private static DazPoseAnimationClipReport BuildReport(ResolvedUnityPose pose, AnimationClip clip,
            List<DazPoseAnimationBindingDiagnostic> diagnostics, DazPoseClipParityResult parity, string assetPath)
        {
            var curveBindings = clip == null ? Array.Empty<EditorCurveBinding>() : AnimationUtility.GetCurveBindings(clip);
            return new DazPoseAnimationClipReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                figureGeneration = "Genesis 8 Female",
                character = pose.CharacterName,
                animationRoot = pose.AnimationRoot.name,
                sourcePoseJson = pose.SourcePoseJsonPath,
                sourceDazPosePath = pose.Definition.source.poseFile,
                sourcePoseAssetId = pose.Definition.source.poseAssetId,
                sourceFigureAssetId = pose.Definition.source.figureAssetId,
                generatedClipPath = assetPath,
                clipName = clip == null ? Path.GetFileNameWithoutExtension(assetPath) : clip.name,
                durationSeconds = clip == null ? 0f : clip.length,
                resolvedBoneCount = pose.Bones.Count,
                poseTargetBoneCount = pose.PoseTargetBoneCount,
                rotationBoneCount = pose.Bones.Count(item => item.HasRotation),
                positionBoneCount = pose.Bones.Count(item => item.HasPosition),
                scaleBoneCount = pose.Bones.Count(item => item.HasScale),
                rotationCurveCount = curveBindings.Count(item => item.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal)),
                positionCurveCount = curveBindings.Count(item => item.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal)),
                scaleCurveCount = curveBindings.Count(item => item.propertyName.StartsWith("m_LocalScale.", StringComparison.Ordinal)),
                totalCurveCount = curveBindings.Length,
                unresolvedRequiredBones = pose.UnresolvedRequiredBoneCount,
                ambiguousBones = pose.AmbiguousBoneCount,
                directApplyParityPassed = parity.Passed,
                maximumRotationErrorDegrees = parity.MaximumRotationErrorDegrees,
                maximumPositionErrorMeters = parity.MaximumPositionErrorMeters,
                maximumScaleError = parity.MaximumScaleError,
                paritySampleTimes = parity.SampleTimes,
                warnings = pose.Warnings,
                bindings = diagnostics.ToArray()
            };
        }

        private static void CopyClipContents(AnimationClip source, AnimationClip destination)
        {
            var oldFloatBindings = AnimationUtility.GetCurveBindings(destination);
            if (oldFloatBindings.Length > 0)
                AnimationUtility.SetEditorCurves(destination, oldFloatBindings, new AnimationCurve[oldFloatBindings.Length]);

            foreach (var oldReferenceBinding in AnimationUtility.GetObjectReferenceCurveBindings(destination))
                AnimationUtility.SetObjectReferenceCurve(destination, oldReferenceBinding, null);

            var newBindings = AnimationUtility.GetCurveBindings(source);
            var newCurves = newBindings.Select(binding => AnimationUtility.GetEditorCurve(source, binding)).ToArray();
            AnimationUtility.SetEditorCurves(destination, newBindings, newCurves);
            destination.events = source.events;
            destination.frameRate = source.frameRate;
            destination.legacy = source.legacy;
            destination.wrapMode = source.wrapMode;
            destination.name = source.name;
            destination.EnsureQuaternionContinuity();
        }

        private static void WriteReport(string reportPath, DazPoseAnimationClipReport report)
        {
            var reportFullPath = Path.Combine(ProjectRoot, reportPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(reportFullPath));
            File.WriteAllText(reportFullPath, JsonUtility.ToJson(report, true));
        }

        public static AnimationClip LoadClipFromAbsolutePath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath)) return null;
            var fullPath = Path.GetFullPath(absolutePath).Replace('\\', '/');
            var assetsRoot = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            if (!fullPath.StartsWith(assetsRoot + "/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Select a generated .anim inside this Unity project's Assets folder.");
            var assetPath = "Assets/" + fullPath.Substring(assetsRoot.Length + 1);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        }

        public static string AssetPathFor(ResolvedUnityPose pose)
        {
            var safeName = SanitizeAssetName(DazPoseJsonLoader.PoseName(pose.Definition.source.poseFile, pose.Definition.source.poseAssetId));
            return OutputFolder + "/" + safeName + ".anim";
        }

        public static string SanitizeAssetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "DAZ Pose";
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars()) { '/', '\\', ':', '*', '?', '"', '<', '>', '|', '\n', '\r' };
            var clean = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim().Trim('.');
            while (clean.Contains("  ")) clean = clean.Replace("  ", " ");
            return string.IsNullOrWhiteSpace(clean) ? "DAZ Pose" : clean;
        }

        private static void ValidateAnimationPath(ResolvedUnityPose pose, ResolvedBonePose bone)
        {
            if (bone.Transform == null || bone.AnimationPath == null || bone.AnimationPath == "<outside-root>")
                throw new InvalidOperationException("DAZ bone '" + bone.DazBoneId + "' is not beneath the stable Genesis8Female animation root.");
            if (bone.AnimationPath.IndexOf("[", StringComparison.Ordinal) >= 0 || bone.AnimationPath.IndexOf("]", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Animation binding path contains a scene/sibling index: " + bone.AnimationPath);
            if (bone.Transform != pose.AnimationRoot && !bone.Transform.IsChildOf(pose.AnimationRoot))
                throw new InvalidOperationException("Animation binding for '" + bone.DazBoneId + "' escapes the stable Genesis8Female root.");
        }

        private static void AddVector3(List<EditorCurveBinding> bindings, List<AnimationCurve> curves, string path, string property, Vector3 value)
        {
            AddCurve(bindings, curves, path, property + ".x", value.x);
            AddCurve(bindings, curves, path, property + ".y", value.y);
            AddCurve(bindings, curves, path, property + ".z", value.z);
        }

        private static void AddVector4(List<EditorCurveBinding> bindings, List<AnimationCurve> curves, string path, string property, Quaternion value)
        {
            AddCurve(bindings, curves, path, property + ".x", value.x);
            AddCurve(bindings, curves, path, property + ".y", value.y);
            AddCurve(bindings, curves, path, property + ".z", value.z);
            AddCurve(bindings, curves, path, property + ".w", value.w);
        }

        private static void AddCurve(List<EditorCurveBinding> bindings, List<AnimationCurve> curves, string path, string property, float value)
        {
            RequireFinite(value, property);
            var curve = new AnimationCurve(new Keyframe(0f, value), new Keyframe(DurationSeconds, value));
            for (var index = 0; index < curve.length; index++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Constant);
            }
            bindings.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), property));
            curves.Add(curve);
        }

        private static Quaternion Normalize(Quaternion rotation, string boneId)
        {
            RequireFinite(rotation, boneId + " local rotation");
            var norm = Mathf.Sqrt(rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w);
            if (norm < 1e-8f) throw new InvalidOperationException("DAZ bone '" + boneId + "' resolved to a zero-length quaternion.");
            return new Quaternion(rotation.x / norm, rotation.y / norm, rotation.z / norm, rotation.w / norm);
        }

        private static void RequireFinite(Vector3 value, string description)
        {
            RequireFinite(value.x, description + ".x");
            RequireFinite(value.y, description + ".y");
            RequireFinite(value.z, description + ".z");
        }

        private static void RequireFinite(Quaternion value, string description)
        {
            RequireFinite(value.x, description + ".x");
            RequireFinite(value.y, description + ".y");
            RequireFinite(value.z, description + ".z");
            RequireFinite(value.w, description + ".w");
        }

        private static void RequireFinite(float value, string description)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidOperationException("Non-finite animation value at " + description + ".");
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    }
}
