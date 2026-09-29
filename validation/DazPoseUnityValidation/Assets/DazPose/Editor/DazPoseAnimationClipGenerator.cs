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
            if (pose == null || pose.Definition == null)
                throw new ArgumentNullException(nameof(pose), "A resolved G8F pose is required.");
            var assetPath = AssetPathFor(pose);
            var safeName = SanitizeAssetName(DazPoseJsonLoader.PoseName(pose.Definition.source.poseFile, pose.Definition.source.poseAssetId));
            return Generate(pose, assetPath, OutputFolder + "/" + safeName + ".report.json", replaceExisting);
        }

        public static DazPoseAnimationClipReport Generate(ResolvedUnityPose pose, string assetPath, string reportPath, bool replaceExisting)
        {
            if (pose == null || pose.Definition == null || pose.BindingRoot == null)
                throw new ArgumentNullException(nameof(pose), "A resolved G8F pose and common animation binding root are required.");

            assetPath = NormalizeAssetPath(assetPath);
            reportPath = NormalizeProjectRelativePath(reportPath);
            var safeName = Path.GetFileNameWithoutExtension(assetPath);
            var fullAssetPath = Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullAssetPath));
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
            var morphDiagnostics = new List<DazPoseAnimationMorphBindingDiagnostic>();
            AnimationClip backup = null;
            DazPoseAnimationClipReport report = null;
            var existingGuid = existing == null ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
            var createdAsset = false;

            try
            {
                PopulateClip(pose, clip, diagnostics, morphDiagnostics);

                var candidateParity = DazPoseClipParityValidator.Validate(pose, clip);
                report = BuildReport(pose, clip, diagnostics, morphDiagnostics, candidateParity, assetPath);
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

                report = BuildReport(pose, savedClip, diagnostics, morphDiagnostics, parity, assetPath);
                report.assetGuid = currentGuid;
                report.generationPassed = true;
                WriteReport(reportPath, report);
                Debug.Log("DAZ Pose → Unity AnimationClip\n"
                    + "Source: " + Path.GetFileName(pose.SourcePoseJsonPath) + "\n"
                    + "Character: " + pose.CharacterName + " (Genesis 8 Female)\n"
                    + "Resolved bones: " + pose.Bones.Count + " | pose targets: " + pose.PoseTargetBoneCount + " | Rotation curves: " + report.rotationCurveCount
                    + " | Position curves: " + report.positionCurveCount + " | Scale curves: " + report.scaleCurveCount
                    + " | blendshape curves: " + report.blendShapeCurveCount + "\n"
                    + "Unresolved required bones: 0 | Ambiguous bones: 0\n"
                    + "Clip duration: " + report.durationSeconds.ToString("F1") + " sec | direct/clip parity: PASS at 0, 0.5, 1.0 sec\n"
                    + "Output: " + assetPath + " | report: " + reportPath);
                return report;
            }
            catch (Exception exception)
            {
                if (report == null) report = BuildReport(pose, clip, diagnostics, morphDiagnostics, new DazPoseClipParityResult(), assetPath);
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

        internal static AnimationClip BuildCandidateClip(ResolvedUnityPose pose)
        {
            if (pose == null || pose.Definition == null || pose.BindingRoot == null)
                throw new ArgumentNullException(nameof(pose), "A resolved pose and common binding root are required.");
            var clip = new AnimationClip { name = "DAZ Pose Candidate", frameRate = 30f, legacy = false };
            try
            {
                PopulateClip(pose, clip, new List<DazPoseAnimationBindingDiagnostic>(),
                    new List<DazPoseAnimationMorphBindingDiagnostic>());
                return clip;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(clip);
                throw;
            }
        }

        private static void PopulateClip(ResolvedUnityPose pose, AnimationClip clip,
            List<DazPoseAnimationBindingDiagnostic> diagnostics, List<DazPoseAnimationMorphBindingDiagnostic> morphDiagnostics)
        {
            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();
            foreach (var bone in pose.Bones.Where(item => item.HasPosition || item.HasRotation || item.HasScale)
                         .OrderBy(item => item.AnimationPath, StringComparer.Ordinal))
            {
                ValidateAnimationPath(pose, bone);
                var properties = new List<string>();
                if (bone.HasRotation)
                {
                    AddVector4(bindings, curves, bone.AnimationPath, "m_LocalRotation", Normalize(bone.LocalRotation, bone.DazBoneId));
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

            var emittedMorphCurves = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in pose.MorphControls.SelectMany(control => control.Bindings.Select(binding => new { Control = control, Binding = binding }))
                         .OrderBy(item => item.Binding.RendererPath, StringComparer.Ordinal)
                         .ThenBy(item => item.Binding.BlendShapeName, StringComparer.Ordinal))
            {
                ValidateMorphBinding(pose, pair.Control, pair.Binding);
                var property = "blendShape." + pair.Binding.BlendShapeName;
                var curveKey = pair.Binding.RendererPath + "|" + property;
                if (!emittedMorphCurves.Add(curveKey))
                    throw new InvalidOperationException("More than one active DAZ control resolves to the same Unity blendshape curve '" + curveKey + "'.");
                AddBlendShapeCurve(bindings, curves, pair.Binding.RendererPath, property, pair.Binding.UnityWeight);
                morphDiagnostics.Add(new DazPoseAnimationMorphBindingDiagnostic
                {
                    sourceControlName = pair.Control.SourceControlName,
                    rawControlId = pair.Control.RawControlId,
                    sourceValue = pair.Control.SourceValue,
                    rendererPath = pair.Binding.RendererPath,
                    meshName = pair.Binding.Renderer.sharedMesh.name,
                    blendShapeName = pair.Binding.BlendShapeName,
                    blendShapeIndex = pair.Binding.BlendShapeIndex,
                    blendShapeFrameCount = pair.Binding.FrameCount,
                    blendShapeFrameWeights = pair.Binding.FrameWeights,
                    unityWeight = pair.Binding.UnityWeight
                });
            }

            if (bindings.Count == 0)
                throw new InvalidOperationException("The resolved pose contains no driven Transform or blendshape values.");
            AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
            clip.EnsureQuaternionContinuity();
        }

        private static DazPoseAnimationClipReport BuildReport(ResolvedUnityPose pose, AnimationClip clip,
            List<DazPoseAnimationBindingDiagnostic> diagnostics, List<DazPoseAnimationMorphBindingDiagnostic> morphDiagnostics,
            DazPoseClipParityResult parity, string assetPath)
        {
            var curveBindings = clip == null ? Array.Empty<EditorCurveBinding>() : AnimationUtility.GetCurveBindings(clip);
            var blendShapeCurves = curveBindings.Where(item => item.type == typeof(SkinnedMeshRenderer)
                && item.propertyName.StartsWith("blendShape.", StringComparison.Ordinal)).ToArray();
            return new DazPoseAnimationClipReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                figureGeneration = "Genesis 8 Female",
                character = pose.CharacterName,
                animationRoot = pose.BindingRoot.name,
                skeletonRoot = pose.SkeletonRoot == null ? string.Empty : DazPoseTransformPath.Get(pose.CharacterRoot, pose.SkeletonRoot),
                bindingRoot = DazPoseTransformPath.Get(pose.CharacterRoot, pose.BindingRoot),
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
                maximumBlendShapeWeightError = parity.MaximumBlendShapeWeightError,
                paritySampleTimes = parity.SampleTimes,
                warnings = pose.Warnings,
                bindings = diagnostics.ToArray(),
                morphBindings = morphDiagnostics.ToArray(),
                morphControlCount = pose.MorphControls.Count,
                morphBindingCount = pose.MorphControls.Sum(item => item.Bindings.Count),
                blendShapeCurveCount = blendShapeCurves.Length,
                resolvedMorphNames = pose.MorphControls.Select(item => item.SourceControlName).OrderBy(item => item, StringComparer.Ordinal).ToArray()
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
                throw new InvalidOperationException("DAZ bone '" + bone.DazBoneId + "' is not beneath the common character binding root.");
            if (bone.AnimationPath.IndexOf("[", StringComparison.Ordinal) >= 0 || bone.AnimationPath.IndexOf("]", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Animation binding path contains a scene/sibling index: " + bone.AnimationPath);
            if (bone.Transform != pose.BindingRoot && !bone.Transform.IsChildOf(pose.BindingRoot))
                throw new InvalidOperationException("Animation binding for '" + bone.DazBoneId + "' escapes the common character binding root.");
        }

        private static void ValidateMorphBinding(ResolvedUnityPose pose, ResolvedUnityMorphControl control, ResolvedUnityMorphBinding binding)
        {
            if (binding.Renderer == null || binding.Renderer.sharedMesh == null)
                throw new InvalidOperationException("Resolved morph '" + control.SourceControlName + "' has no live SkinnedMeshRenderer or mesh.");
            if (binding.Renderer.transform != pose.BindingRoot && !binding.Renderer.transform.IsChildOf(pose.BindingRoot))
                throw new InvalidOperationException("Morph renderer path escapes the common animation binding root for '" + control.SourceControlName + "'.");
            var expectedPath = AnimationUtility.CalculateTransformPath(binding.Renderer.transform, pose.BindingRoot);
            if (!string.Equals(expectedPath, binding.RendererPath, StringComparison.Ordinal)
                || binding.RendererPath.IndexOf("[", StringComparison.Ordinal) >= 0
                || binding.RendererPath.IndexOf("]", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Morph renderer binding path is not a stable relative path for '" + control.SourceControlName + "': " + binding.RendererPath + ".");
            var mesh = binding.Renderer.sharedMesh;
            if (binding.BlendShapeIndex < 0 || binding.BlendShapeIndex >= mesh.blendShapeCount
                || !string.Equals(mesh.GetBlendShapeName(binding.BlendShapeIndex), binding.BlendShapeName, StringComparison.Ordinal))
                throw new InvalidOperationException("Imported blendshape identity changed after resolving '" + control.SourceControlName + "'.");
            RequireFinite(binding.UnityWeight, control.SourceControlName + " blendshape weight");
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

        private static void AddBlendShapeCurve(List<EditorCurveBinding> bindings, List<AnimationCurve> curves, string path, string property, float value)
        {
            RequireFinite(value, property);
            var curve = new AnimationCurve(new Keyframe(0f, value), new Keyframe(DurationSeconds, value));
            for (var index = 0; index < curve.length; index++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Constant);
            }
            bindings.Add(EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), property));
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

        private static string NormalizeAssetPath(string assetPath)
        {
            var normalized = (assetPath ?? string.Empty).Trim().Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                || !normalized.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)
                || normalized.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
                throw new InvalidOperationException("Animation output must be an Assets-relative .anim path without traversal segments.");
            return normalized;
        }

        private static string NormalizeProjectRelativePath(string relativePath)
        {
            var normalized = (relativePath ?? string.Empty).Trim().Replace('\\', '/');
            if (string.IsNullOrEmpty(normalized) || normalized.StartsWith("/", StringComparison.Ordinal)
                || Path.IsPathRooted(normalized)
                || normalized.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
                throw new InvalidOperationException("Animation report path must be project-relative and cannot contain traversal segments.");
            return normalized;
        }
    }
}
